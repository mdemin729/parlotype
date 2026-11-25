using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Platform.MacOs;

namespace Parlotype.Platform.Settings;

/// <summary>
/// <see cref="ISecretStore"/> backed by the user's macOS login keychain
/// (ADR-078). The macOS counterpart of <see cref="DpapiSecretStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each secret is a generic-password item whose service is <see cref="DefaultService"/>
/// and whose account is the secret's key, so they are listed together under
/// "Parlotype" in Keychain Access, where the user can inspect or delete them.
/// Nothing is written to <c>secrets.json</c> on macOS: the keychain is the
/// store, and it is encrypted with the login password.
/// </para>
/// <para>
/// The keychain ties an item to the code signature of the app that created it.
/// A signed release reads its own items silently. A development build is
/// re-signed ad hoc on every compile, so macOS asks whether the "new" app may
/// read the item — expected there, and the reason this is not exercised by
/// the unattended test suite against real items it did not just create.
/// </para>
/// </remarks>
[SupportedOSPlatform("macos")]
public sealed class KeychainSecretStore : ISecretStore
{
    /// <summary>The keychain "service" every Parlotype secret is filed under.</summary>
    public const string DefaultService = "Parlotype";

    private const string SecurityLib = "/System/Library/Frameworks/Security.framework/Security";

    private const int Success = 0;
    private const int ItemNotFound = -25300;  // errSecItemNotFound
    private const int DuplicateItem = -25299; // errSecDuplicateItem

    private static readonly Lazy<nint> SecurityHandle = new(() => NativeLibrary.Load(SecurityLib));

    private readonly string _service;
    private readonly ILogger<KeychainSecretStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public KeychainSecretStore(ILogger<KeychainSecretStore> logger)
        : this(DefaultService, logger)
    {
    }

    /// <summary>Testability seam: files items under a throwaway service instead of the real one.</summary>
    internal KeychainSecretStore(string service, ILogger<KeychainSecretStore> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return Read(key);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrEmpty(value))
                Delete(key);
            else
                Write(key, value);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Never throws — an item that cannot be read (the user declined the
    /// keychain prompt, the keychain is locked) is logged and treated as absent,
    /// so callers fall back to the "no key configured" path, as
    /// <see cref="DpapiSecretStore"/> does for a value it cannot decrypt.
    /// </summary>
    private string? Read(string key)
    {
        using var query = new Query(_service, key);
        query.Add(Constant("kSecReturnData"), MacOsNative.BooleanTrue);
        query.Add(Constant("kSecMatchLimit"), Constant("kSecMatchLimitOne"));

        var dictionary = query.Build();
        try
        {
            var status = SecItemCopyMatching(dictionary, out var data);
            if (status == ItemNotFound)
                return null;

            if (status != Success || data == 0)
            {
                _logger.LogWarning(
                    "Could not read secret '{Key}' from the keychain (status {Status}). Treating as absent.",
                    key, status);
                return null;
            }

            try
            {
                var length = (int)MacOsNative.CFDataGetLength(data);
                var bytes = new byte[length];
                if (length > 0)
                    Marshal.Copy(MacOsNative.CFDataGetBytePtr(data), bytes, 0, length);

                return Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                MacOsNative.CFRelease(data);
            }
        }
        finally
        {
            MacOsNative.CFRelease(dictionary);
        }
    }

    private void Write(string key, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var data = MacOsNative.CFDataCreate(0, bytes, bytes.Length);
        try
        {
            using var item = new Query(_service, key);
            item.Add(Constant("kSecValueData"), data);

            var attributes = item.Build();
            int status;
            try
            {
                status = SecItemAdd(attributes, 0);
            }
            finally
            {
                MacOsNative.CFRelease(attributes);
            }

            if (status == DuplicateItem)
                status = Update(key, data);

            if (status != Success)
                throw new InvalidOperationException($"The keychain refused to store '{key}' (status {status}).");
        }
        finally
        {
            MacOsNative.CFRelease(data);
        }
    }

    private int Update(string key, nint data)
    {
        using var query = new Query(_service, key);
        var match = query.Build();

        var valueKey = Constant("kSecValueData");
        var changes = MacOsNative.CreateDictionary((valueKey, data));
        try
        {
            return SecItemUpdate(match, changes);
        }
        finally
        {
            MacOsNative.CFRelease(changes);
            MacOsNative.CFRelease(match);
        }
    }

    private void Delete(string key)
    {
        using var query = new Query(_service, key);
        var match = query.Build();
        try
        {
            var status = SecItemDelete(match);
            if (status is not (Success or ItemNotFound))
                throw new InvalidOperationException($"The keychain refused to remove '{key}' (status {status}).");
        }
        finally
        {
            MacOsNative.CFRelease(match);
        }
    }

    /// <summary>One of the <c>CFStringRef</c> constants the Security framework exports.</summary>
    private static nint Constant(string symbol) =>
        MacOsNative.ReadPointerConstant(SecurityHandle.Value, symbol);

    /// <summary>
    /// Collects the entries of a keychain query that names one item: class,
    /// service and account. Owns the CFStrings it creates for the latter two.
    /// </summary>
    private sealed class Query : IDisposable
    {
        private readonly List<(nint Key, nint Value)> _entries = [];
        private readonly nint _service;
        private readonly nint _account;

        public Query(string service, string account)
        {
            _service = MacOsNative.CreateString(service);
            _account = MacOsNative.CreateString(account);

            _entries.Add((Constant("kSecClass"), Constant("kSecClassGenericPassword")));
            _entries.Add((Constant("kSecAttrService"), _service));
            _entries.Add((Constant("kSecAttrAccount"), _account));
        }

        public void Add(nint key, nint value) => _entries.Add((key, value));

        /// <summary>The caller releases the returned dictionary.</summary>
        public nint Build() => MacOsNative.CreateDictionary([.. _entries]);

        public void Dispose()
        {
            MacOsNative.CFRelease(_account);
            MacOsNative.CFRelease(_service);
        }
    }

    [DllImport(SecurityLib)]
    private static extern int SecItemCopyMatching(nint query, out nint result);

    [DllImport(SecurityLib)]
    private static extern int SecItemAdd(nint attributes, nint result);

    [DllImport(SecurityLib)]
    private static extern int SecItemUpdate(nint query, nint attributesToUpdate);

    [DllImport(SecurityLib)]
    private static extern int SecItemDelete(nint query);
}
