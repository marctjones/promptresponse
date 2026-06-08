using System.Security.Cryptography;

namespace PromptResponse.Core.Signing;

/// <summary>
/// Helpers for the ECDSA P-256 keys used by APR signing. Uses only the .NET
/// built-in crypto (<see cref="ECDsa"/>) — no third-party dependency — with PEM
/// import/export for portability.
/// </summary>
public static class SignatureKeys
{
    /// <summary>Generates a new ECDSA P-256 key pair.</summary>
    public static ECDsa Generate() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>Exports the private key as a PKCS#8 PEM block (keep this secret).</summary>
    public static string ExportPrivateKeyPem(ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.ExportPkcs8PrivateKeyPem();
    }

    /// <summary>Exports the public key as a SubjectPublicKeyInfo PEM block.</summary>
    public static string ExportPublicKeyPem(ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.ExportSubjectPublicKeyInfoPem();
    }

    /// <summary>Imports a key (private or public) from a PEM block.</summary>
    public static ECDsa ImportFromPem(string pem)
    {
        var ec = ECDsa.Create();
        ec.ImportFromPem(pem);
        return ec;
    }
}
