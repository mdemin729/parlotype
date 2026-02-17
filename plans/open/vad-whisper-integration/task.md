## Task

Implement Audio processing pipeline:

Microphone -> NAudio -> SileroVAD -> Whisper.Net -> Transcription

## Short description

NAudion package for capturing audio from microphone and for converting the stream to 16000 Hz mono.

SilveroVAD for detecting speech. This works a filter before sending audion data to Whisper.Net.

Whisper.Net accepts chunks of audio data with speech and transcribes it.
Use Whisper Base model size by default.

## Documentation

### GitHub local cache

There is a local cache for github repositorires.
It is located at
```pwsh
$env:GITHUB_REPOS
```

```bash
$GITHUB_REPOS
```

### Instructions

Before searching the web for documentation, you can search local repositories at:

1. NAudio: `$env:GITHUB_REPOS\naudio\NAudio`
2. SilveroVAD: `$env:GITHUB_REPOS\DimQ1\SileroVad`
3. Whisper.Net: `$env:GITHUB_REPOS\sandrohanea\whisper.net`

## Testing

Write Unit tests for the pipeline.
Use Whisper tiny model size for unit tests.
DO NOT test microphone - use `"src\Parlotype.Tests\resources\one-small-step.wav` instead.
