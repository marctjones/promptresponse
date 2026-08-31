using System.Security.Cryptography.X509Certificates;

namespace PromptResponse.Core.Beta6;

/// <summary>Certificate-trust result for a valid beta.6 CMS attestation proof.</summary>
public enum AprAttestationTrust
{
    /// <summary>Chains to a configured trusted root or pinned certificate.</summary>
    Trusted,
    /// <summary>Valid but unpinned self-signed certificate.</summary>
    SelfSigned,
    /// <summary>Valid proof whose certificate chain is not trusted.</summary>
    Untrusted,
    /// <summary>Invalid or missing proof.</summary>
    Invalid,
}

/// <summary>Caller-selected certificate trust policy for beta.6 attestation proofs.</summary>
public sealed class AprAttestationTrustOptions
{
    /// <summary>Trusted roots or pinned certificates.</summary>
    public IReadOnlyCollection<X509Certificate2>? TrustAnchors { get; init; }
    /// <summary>Whether chain verification checks revocation online.</summary>
    public bool CheckRevocation { get; init; }
    /// <summary>Uses the operating-system trust store without revocation checks.</summary>
    public static AprAttestationTrustOptions Default { get; } = new();
}

internal static class AprAttestationTrustEvaluator
{
    internal static (AprAttestationTrust Trust, string Status) Evaluate(
        X509Certificate2 certificate, AprAttestationTrustOptions options)
    {
        var selfSigned = string.Equals(certificate.Subject, certificate.Issuer, StringComparison.Ordinal);
        if (selfSigned)
        {
            var pinned = options.TrustAnchors?.Any(anchor => anchor.RawData.AsSpan().SequenceEqual(certificate.RawData)) ?? false;
            return pinned
                ? (AprAttestationTrust.Trusted, "trusted — pinned self-signed certificate")
                : (AprAttestationTrust.SelfSigned, "valid attestation; self-signed certificate, identity not verified");
        }

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = options.CheckRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
        if (options.TrustAnchors is { Count: > 0 } anchors)
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            foreach (var anchor in anchors)
            {
                chain.ChainPolicy.CustomTrustStore.Add(anchor);
                chain.ChainPolicy.ExtraStore.Add(anchor);
            }
        }

        if (chain.Build(certificate)) return (AprAttestationTrust.Trusted, "trusted — certificate chains to a trusted root");
        return (AprAttestationTrust.Untrusted, "valid attestation, but the certificate is not trusted");
    }
}
