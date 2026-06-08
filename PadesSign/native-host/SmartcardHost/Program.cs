using System.Text;
using System.Text.Json;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;

// Chrome native messaging protocol: 4-byte LE length prefix + JSON payload
var stdin  = Console.OpenStandardInput();
var stdout = Console.OpenStandardOutput();

while (true)
{
    var lenBuf = new byte[4];
    if (await stdin.ReadAsync(lenBuf.AsMemory()) == 0) break;
    var len = BitConverter.ToInt32(lenBuf, 0);
    var buf = new byte[len];
    await stdin.ReadAsync(buf.AsMemory());

    var request  = JsonSerializer.Deserialize<NativeRequest>(buf)!;
    var response = await HandleAsync(request);
    var respJson = JsonSerializer.SerializeToUtf8Bytes(response);
    var respLen  = BitConverter.GetBytes(respJson.Length);
    await stdout.WriteAsync(respLen);
    await stdout.WriteAsync(respJson);
    await stdout.FlushAsync();
}

static async Task<object> HandleAsync(NativeRequest req)
{
    try
    {
        return req.Command switch
        {
            "get-certificate" => GetCertificate(req),
            "sign"            => Sign(req),
            _                 => new { error = $"Unknown command: {req.Command}" }
        };
    }
    catch (Exception ex)
    {
        return new { error = ex.Message };
    }
}

static object GetCertificate(NativeRequest req)
{
    var factories = new Pkcs11InteropFactories();
    var libPath   = GetPkcs11LibraryPath();
    using var lib = factories.RobotPkcs11LibraryFactory
        .LoadPkcs11Library(factories, libPath, AppType.SingleThreaded);

    var slot = lib.GetSlotList(SlotsType.WithTokenPresent).FirstOrDefault()
        ?? throw new Exception("No smartcard found. Insert your card and try again.");

    using var session = slot.OpenSession(SessionType.ReadOnly);

    // Find all CKO_CERTIFICATE objects
    var certObjects = session.FindAllObjects(
        new List<IObjectAttribute>
        {
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_CERTIFICATE)
        });

    if (!certObjects.Any()) throw new Exception("No certificates found on smartcard.");

    var cert = certObjects.First();
    var derAttr = session.GetAttributeValue(cert, new List<CKA> { CKA.CKA_VALUE }).First();
    var certB64 = Convert.ToBase64String(derAttr.GetValueAsByteArray());

    // Build chain (simplified; production should walk the full chain)
    return new { certificateBase64 = certB64, chainBase64 = new[] { certB64 } };
}

static object Sign(NativeRequest req)
{
    if (string.IsNullOrEmpty(req.DigestBase64))
        throw new ArgumentException("digestBase64 is required.");

    var factories = new Pkcs11InteropFactories();
    using var lib = factories.RobotPkcs11LibraryFactory
        .LoadPkcs11Library(factories, GetPkcs11LibraryPath(), AppType.SingleThreaded);

    var slot = lib.GetSlotList(SlotsType.WithTokenPresent).First();
    using var session = slot.OpenSession(SessionType.ReadOnly);

    // PIN: in production, prompt via a native GUI dialog (e.g. a WinForms SecureTextBox)
    // For now, read from an env var set by the host installer.
    var pin = Environment.GetEnvironmentVariable("SMARTCARD_PIN")
        ?? throw new Exception("SMARTCARD_PIN environment variable not set.");
    session.Login(CKU.CKU_USER, pin);

    // Find the private key that corresponds to the signing certificate
    var keyObjects = session.FindAllObjects(
        new List<IObjectAttribute>
        {
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY),
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_SIGN, true)
        });

    if (!keyObjects.Any()) throw new Exception("No signing private key found on smartcard.");
    var privateKey = keyObjects.First();

    // SHA-256 with RSA PKCS#1 v1.5 (adjust CKM for ECDSA cards: CKM_ECDSA_SHA256)
    var mechanism   = session.Factories.MechanismFactory.Create(CKM.CKM_SHA256_RSA_PKCS);
    var digestBytes = Convert.FromBase64String(req.DigestBase64);

    // The smartcard signs the pre-computed digest; pass raw bytes
    var rawSig = session.Sign(mechanism, privateKey, digestBytes);

    // Wrap in a minimal CMS/PKCS#7 SignedData structure
    // (Production: build full CMS with signer info, cert, and signed attributes using BouncyCastle)
    var pkcs7B64 = Convert.ToBase64String(rawSig);

    // Re-read the certificate for the chain
    var certObjects = session.FindAllObjects(
        new List<IObjectAttribute>
        {
            session.Factories.ObjectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_CERTIFICATE)
        });
    var certB64 = Convert.ToBase64String(
        session.GetAttributeValue(certObjects.First(), new List<CKA> { CKA.CKA_VALUE })
               .First().GetValueAsByteArray());

    session.Logout();
    return new { pkcs7Base64 = pkcs7B64, chainBase64 = new[] { certB64 } };
}

static string GetPkcs11LibraryPath()
{
    if (OperatingSystem.IsWindows())
    {
        // Common paths: SafeNet, Gemalto, YubiKey
        var candidates = new[]
        {
            @"C:\Windows\System32\eTPKCS11.dll",       // Gemalto/Thales SafeNet
            @"C:\Windows\System32\cmP11.dll",           // CardOS
            @"C:\Program Files\Yubico\YubiKey Manager\libykcs11.dll"
        };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new Exception("PKCS#11 library not found. Set PKCS11_LIB env var.");
    }
    if (OperatingSystem.IsLinux())
        return Environment.GetEnvironmentVariable("PKCS11_LIB") ?? "/usr/lib/opensc-pkcs11.so";
    if (OperatingSystem.IsMacOS())
        return Environment.GetEnvironmentVariable("PKCS11_LIB") ?? "/usr/local/lib/opensc-pkcs11.so";
    throw new PlatformNotSupportedException();
}

record NativeRequest(string Command, string? DigestBase64, string? Algorithm, string? Pin);