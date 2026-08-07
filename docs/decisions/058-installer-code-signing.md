---
status: accepted
date: 2026-08-06
---

# 058. Installer code signing with Azure Artifact Signing

## Context

[ADR-053](053-velopack-packaging-and-auto-update.md) shipped a real installer and
left one consequence open: *"Builds are unsigned, so Windows SmartScreen warns on
first run. Code signing is planned separately; the pipeline has a documented slot
for it and must keep working without credentials."*

The warning is not cosmetic. `Parlotype-win-Setup.exe` is downloaded from GitHub
and therefore carries a Mark-of-the-Web, so SmartScreen shows the full-screen
"Windows protected your PC" panel with the run button hidden behind **More info**.
For a voice-to-text app whose whole pitch is that audio never leaves the machine,
"unknown publisher" is a bad first impression and a real install-funnel loss.

Two things make this more than "buy a certificate":

1. **Velopack does its own signing at specific points.** `Update.exe` must be
   signed *before* it is embedded in the `.nupkg`, and `Setup.exe` *after* its
   payload is appended. Signing `Releases/` afterwards with a generic action —
   `azure/artifact-signing-action`, `signtool` by hand — appends a certificate
   table past Setup's appended payload and risks breaking the installer. Velopack
   states this outright: signing "needs to be performed by Velopack itself".
2. **CI must still pass with no credentials.** Dry runs and pull requests from
   forks cannot reach secrets and get `id-token: write` downgraded to read. The
   packaging dry run is the only thing that catches a lost `--self-contained` or a
   `Program.Main` that stopped calling `VelopackApp.Run()`, so it cannot become
   credential-gated.

## Decision

Sign with **Azure Artifact Signing** (the service formerly called Trusted
Signing), invoked through `vpk` rather than as a separate step.

### Why Artifact Signing rather than an OV/EV certificate

- **Instant SmartScreen reputation**, like an EV certificate — an OV certificate
  starts from zero and has to earn reputation over downloads, which for a
  low-volume project can take months.
- **No hardware.** EV certificates arrive on a physical token, which cannot be
  driven from a GitHub-hosted runner without a self-hosted machine sitting in a
  drawer somewhere.
- **$9.99/month**, against several hundred a year for an EV certificate.

The cost is a 72-hour certificate lifetime, which makes RFC3161 timestamping
mandatory rather than merely advisable — an untimestamped signature starts failing
three days after release. CI asserts the timestamp is present.

### Everything in the package is signed, not just the installer

`vpk` signs the `.exe`/`.dll` files it packs as well as `Setup.exe` and
`Update.exe`. Signing only the installer would have been cheaper and is enough to
clear the SmartScreen prompt on download, but:

- `Update.exe` runs on **every** update and `Parlotype.exe` runs daily; both face
  Defender and ASR heuristics on their own. A signed installer that drops unsigned
  executables is a recognised malware shape.
- **Smart App Control** (on clean Windows 11 installs) and enterprise
  WDAC/AppLocker publisher rules evaluate every loaded binary, not just the
  launched one. Unsigned natives — whisper.cpp, sherpa-onnx, ONNX Runtime, the
  Vulkan runtime — are exactly what gets blocked there.

The signature budget is not a constraint: the base tier includes 5,000 signatures
a month and a self-contained `win-x64` publish is a few hundred PE files. If it
ever becomes one, `--signExclude <regex>` (env `VPK_SIGN_EXCLUDE`) trims the set.
Signing third-party natives under our own certificate is normal — it attests who
distributed them, not who wrote them.

### Signing is switched on by an environment variable, not a flag

`vpk pack` reads `VPK_AZURE_TRUSTED_SIGN_FILE` from the environment. Two new steps
— `Azure login (signing)` and `Enable code signing`, both `if: env.DRY_RUN ==
'false'` — write the metadata JSON and export that variable. **The `Pack` step
itself is unchanged**: the invocation a signed release runs is byte-for-byte the
one an unsigned dry run runs, so the credentialed and uncredentialed paths cannot
drift apart, and ADR-053's promise that "nothing else in this workflow changes"
holds literally.

### Authentication is OIDC; no secret is stored

An Entra app registration holds a federated credential whose subject is
`repo:mdemin729/parlotype:environment:release`, and the job declares
`environment: release`. The dlib authenticates through `DefaultAzureCredential`,
which falls through to the Azure CLI credential that `azure/login` leaves behind —
so no client secret exists to leak or rotate. The app registration holds
**Artifact Signing Certificate Profile Signer** on the signing account.

The `release` environment must carry **no** deployment branch or tag policy. One
would block the PR and `workflow_dispatch` dry runs, which declare the same
environment and never sign. Restricting to tags via a wildcard federated-credential
subject was the tighter alternative, but plain subject matching has no wildcards
and the flexible-claims form is more machinery than a single-maintainer repo needs.

### .NET 8 is installed alongside .NET 10

The Artifact Signing dlib that `vpk` bundles is built against .NET 8 and will not
load without that runtime. `setup-dotnet` now takes both versions. Nothing is
built with 8.0.

### Verification asserts on output, not exit codes

A signing failure inside `vpk` does not reliably fail the pack, so a new `Verify
signatures` step checks `Setup.exe` **and** the main executable extracted from
`Parlotype-win-Portable.zip` — the latter proving the packaged *application*
binaries were signed, not merely the installer wrapping them. Both must report
`Valid` and carry a timestamp.

## Consequences

**Easier**

- No SmartScreen panel on first install, from the first signed release — no
  reputation-building period.
- Parlotype installs under Smart App Control and under enterprise publisher rules.
- Releases carry a verifiable publisher identity, which is the precondition for
  anything later that needs one (winget, Microsoft Store).

**Harder / accepted costs**

- A tagged release now depends on Azure being reachable and on the OIDC
  federation being intact. A misconfigured region endpoint fails as a bare 403
  from `SignerSign()` that mentions nothing about regions.
- A recurring subscription is now on the critical path for shipping. If it lapses,
  tag builds fail at `Pack` rather than degrading to unsigned.
- Signing several hundred files adds a few minutes to each release build.
- Certificate rotation is invisible but real: signatures are only valid beyond 72
  hours because of the timestamp, so a broken timestamp server would silently
  produce releases that stop validating later. The verify step is the guard.
- Three repository variables and three secrets now exist in a repo that
  previously needed none. `docs/RELEASING.md` no longer says "no secrets".

## Alternatives rejected

- **`azure/artifact-signing-action` over `Releases/` after packing** — the
  obvious-looking approach, and wrong here: it cannot sign `Update.exe` before
  embedding, and post-signing `Setup.exe` appends a certificate table past its
  appended payload.
- **Signing only `Setup.exe`** — clears the download prompt, leaves every binary
  the user actually runs unsigned, and fails Smart App Control.
- **OV certificate from a commercial CA** — cheaper up front, but reputation
  accrues per certificate over download volume, which a low-volume project may
  never reach.
- **EV certificate on a hardware token** — instant reputation, but needs a
  self-hosted runner with the token attached; unacceptable operational surface for
  a single-maintainer project.
- **Storing a client secret instead of OIDC federation** — one fewer moving part
  in Azure, at the cost of a long-lived credential in repository secrets with a
  rotation schedule nobody would keep.
