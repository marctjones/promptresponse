using System.Security.Cryptography.X509Certificates;
using CommunityToolkit.Mvvm.ComponentModel;
using PromptResponse.Core.Signing;

namespace PromptResponse.Desktop.ViewModels;

/// <summary>
/// Makes a self-signed signing certificate and writes it to disk.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately creation only. There is no keystore here, no rotation, no revocation, no
/// renewal and no passphrase changing - those are where key management gets genuinely
/// hard, and openssl, a platform keychain or a real CA already do them properly. This
/// exists so somebody can try signing without first learning openssl, which is a
/// different goal from managing an organisation's keys.
/// </para>
/// <para>
/// The result is <em>self-signed</em>, and that is not a technicality. It proves the file
/// has not changed since signing, and it proves nothing about who signed it unless the
/// recipient already has the public certificate and pins it. The dialog says so, because
/// a signing feature that lets someone believe they have more assurance than they do is
/// worse than no signing feature.
/// </para>
/// </remarks>
public sealed partial class CreateSigningKeyViewModel : ObservableObject
{
    /// <summary>The name that appears as the signer on every signature made with this key.</summary>
    [ObservableProperty]
    private string _signerName = string.Empty;

    /// <summary>Protects the private key in the .pfx file. Empty means unprotected.</summary>
    [ObservableProperty]
    private string _password = string.Empty;

    /// <summary>How long the certificate stays valid.</summary>
    /// <remarks>
    /// Signatures made with it keep verifying after it expires - expiry is reported as a
    /// trust note, never as invalid content (specification 9.4) - so this is about how
    /// long the key should be used, not how long its signatures last.
    /// </remarks>
    [ObservableProperty]
    private int _validYears = 3;

    /// <summary>Whether to write the public certificate beside the key.</summary>
    /// <remarks>
    /// This is the file to hand to whoever will verify the signatures: they pin it as a
    /// trust anchor. Without it a self-signed signature can only ever report "self-signed",
    /// never "trusted", so defaulting it on is what makes the key useful to anyone else.
    /// </remarks>
    [ObservableProperty]
    private bool _alsoWritePublicCertificate = true;

    /// <summary>Whether the entered details are enough to create a key.</summary>
    public bool CanCreate => !string.IsNullOrWhiteSpace(SignerName) && ValidYears is >= 1 and <= 30;

    partial void OnSignerNameChanged(string value) => OnPropertyChanged(nameof(CanCreate));
    partial void OnValidYearsChanged(int value) => OnPropertyChanged(nameof(CanCreate));

    /// <summary>The file name to suggest, derived from the signer's name.</summary>
    public string SuggestedFileName
    {
        get
        {
            var cleaned = new string(SignerName
                .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')
                .ToArray())
                .Trim('-');
            while (cleaned.Contains("--", StringComparison.Ordinal))
            {
                cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
            }
            return string.IsNullOrEmpty(cleaned) ? "signing-key.pfx" : $"{cleaned}.pfx";
        }
    }

    /// <summary>Writes the key to <paramref name="pfxPath"/>, and returns what was created.</summary>
    /// <remarks>
    /// The public certificate goes beside it with a .cer extension, because the two
    /// travelling together is what lets a recipient pin the right one.
    /// </remarks>
    public CreatedSigningKey Create(string pfxPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pfxPath);

        var now = DateTimeOffset.UtcNow;
        using var certificate = SignatureCertificates.CreateSelfSigned(
            SignerName.Trim(), now.AddMinutes(-5), now.AddYears(ValidYears));

        File.WriteAllBytes(pfxPath,
            certificate.Export(X509ContentType.Pkcs12, string.IsNullOrEmpty(Password) ? null : Password));

        string? publicPath = null;
        if (AlsoWritePublicCertificate)
        {
            publicPath = Path.ChangeExtension(pfxPath, ".cer");
            File.WriteAllBytes(publicPath, certificate.Export(X509ContentType.Cert));
        }

        return new CreatedSigningKey(
            pfxPath, publicPath, certificate.Subject,
            SignatureCertificates.Sha256Thumbprint(certificate),
            certificate.NotAfter);
    }
}

/// <summary>What a successful key creation produced.</summary>
/// <param name="PrivateKeyPath">The .pfx holding the private key.</param>
/// <param name="PublicCertificatePath">The .cer to share, or null if not written.</param>
/// <param name="Subject">The certificate subject.</param>
/// <param name="Thumbprint">SHA-256 thumbprint, which is what a recipient pins.</param>
/// <param name="Expires">When the certificate stops being usable for new signatures.</param>
public sealed record CreatedSigningKey(
    string PrivateKeyPath, string? PublicCertificatePath,
    string Subject, string Thumbprint, DateTime Expires);
