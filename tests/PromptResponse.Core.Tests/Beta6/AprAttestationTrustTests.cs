using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using AwesomeAssertions;
using PromptResponse.Core.Beta6;
using Xunit;

namespace PromptResponse.Core.Tests.Beta6;

public sealed class AprAttestationTrustTests
{
    [Fact]
    public void VerifyingAProof_ContactsNoCertificateEndpoint_UnlessTheCallerAsks()
    {
        // APR-SEC-017. The signer's certificate names its missing intermediate at a URL the
        // sender chose. The second verification opts in, which is how this test knows the
        // listener would see the fetch the first one must not make.
        using var endpoint = new CountingEndpoint();
        var now = DateTimeOffset.UtcNow;
        using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var root = Request("CN=Root", rootKey, authority: true).CreateSelfSigned(now.AddDays(-1), now.AddDays(30));
        using var intermediate = Request("CN=Intermediate", intermediateKey, authority: true)
            .Create(root, now.AddDays(-1), now.AddDays(20), [1]).CopyWithPrivateKey(intermediateKey);
        var leafRequest = Request("CN=Signer", leafKey, authority: false);
        leafRequest.CertificateExtensions.Add(new X509AuthorityInformationAccessExtension(null, [endpoint.Url]));
        using var leaf = leafRequest.Create(intermediate, now.AddDays(-1), now.AddDays(10), [2]).CopyWithPrivateKey(leafKey);
        var attestation = SignedAttestation(leaf);

        var unasked = AprAttestationProofs.Verify(attestation, new AprAttestationTrustOptions { TrustAnchors = [root] }).Single();
        var contactsUnasked = endpoint.Contacts;
        AprAttestationProofs.Verify(attestation, new AprAttestationTrustOptions { TrustAnchors = [root], CheckRevocation = true });

        unasked.ContentValid.Should().BeTrue();
        unasked.Trust.Should().Be(AprAttestationTrust.Untrusted, "the intermediate the chain needs was never fetched");
        contactsUnasked.Should().Be(0, "verifying a proof on a document someone sent must not contact a URL they chose");
        endpoint.Contacts.Should().BeGreaterThan(0,
            "a caller that asks for online checking lets the chain fetch its intermediate; without that the zero above proves nothing");
    }

    private static CertificateRequest Request(string subject, ECDsa key, bool authority)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(authority, false, 0, true));
        return request;
    }

    private static AprAttestationRecord SignedAttestation(X509Certificate2 signer)
    {
        var payload = AprAttestationProofs.SigningPayload(Attestation(proofValue: ""));
        var cms = new SignedCms(new ContentInfo(payload), detached: true);
        cms.ComputeSignature(new CmsSigner(signer) { IncludeOption = X509IncludeOption.EndCertOnly });
        return Attestation(Convert.ToBase64String(cms.Encode()));
    }

    private static AprAttestationRecord Attestation(string proofValue)
    {
        var json = JsonSerializer.Serialize(new
        {
            recordType = "attestation", aprVersion = "1.0-beta.6",
            subject = new { digest = "sha256:" + new string('0', 64), canonicalization = "jcs-sha256" },
            scope = new { kind = "document" },
            manifest = new { root = "sha256:" + new string('0', 64), entries = Array.Empty<object>() },
            proofs = new[] { new { type = AprAttestationProofs.CmsEcdsaP256Sha256, value = proofValue } },
            witnesses = Array.Empty<string>(),
        });
        using var document = JsonDocument.Parse(json);
        return new AprAttestationRecord(document.RootElement.Clone());
    }

    /// <summary>A loopback HTTP endpoint that answers 404 and counts every connection.</summary>
    private sealed class CountingEndpoint : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private int _contacts;

        public CountingEndpoint()
        {
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/intermediate.cer";
            _ = Task.Run(AnswerAsync);
        }

        public string Url { get; }

        // A chain build waits for the reply, so a contact is counted before Verify returns.
        public int Contacts => Volatile.Read(ref _contacts);

        private async Task AnswerAsync()
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    Interlocked.Increment(ref _contacts);
                    var stream = client.GetStream();
                    await stream.ReadAtLeastAsync(new byte[1], 1, throwOnEndOfStream: false);
                    await stream.WriteAsync("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray());
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
        }

        public void Dispose() => _listener.Stop();
    }
}
