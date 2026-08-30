using System.Security.Cryptography.X509Certificates;
using PromptResponse.Core.Expressions;
using PromptResponse.Core.Models;
using PromptResponse.Core.Signing;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels.Prompts;

namespace PromptResponse.Desktop.ViewModels.Signing;

/// <summary>
/// Owns the desktop signature workflow and its derived presentation state.
/// The shell supplies the current prompt view-models so signature coverage and
/// restoration stay synchronized with the form it owns.
/// </summary>
internal sealed class SignatureWorkflow
{
    private readonly IDocumentSessionService _session;
    private readonly IFileService _fileService;
    private readonly IDialogService _dialogService;
    private readonly Func<IEnumerable<PromptViewModelBase>> _prompts;
    private readonly List<SignatureStatusItem> _signatures = [];
    private readonly HashSet<string> _brokenAlreadyAnnounced = new(StringComparer.Ordinal);
    private HashSet<string> _validLastTime = new(StringComparer.Ordinal);
    private Dictionary<string, string> _responsesWhileValid = new(StringComparer.Ordinal);

    public SignatureWorkflow(
        IDocumentSessionService session,
        IFileService fileService,
        IDialogService dialogService,
        Func<IEnumerable<PromptViewModelBase>> prompts)
    {
        _session = session;
        _fileService = fileService;
        _dialogService = dialogService;
        _prompts = prompts;
    }

    public event Action? StateChanged;

    public IReadOnlyList<SignatureStatusItem> Signatures => _signatures;
    public bool HasSignatures => _signatures.Count > 0;
    public string SignatureSummary => _signatures.Count == 0
        ? "Not signed"
        : _signatures.Any(s => !s.ContentValid)
            ? $"{_signatures.Count} signature(s) — one or more INVALID"
            : $"{_signatures.Count} signature(s) — all verify";

    public string? BreakageNotice { get; private set; }
    public bool HasBreakageNotice => !string.IsNullOrEmpty(BreakageNotice);

    public void Refresh()
    {
        _signatures.Clear();
        var document = _session.CurrentDocument;
        var results = document?.Signatures is { Count: > 0 }
            ? AprVerifier.VerifyAll(document)
            : [];

        if (document?.Signatures is { Count: > 0 } signatures)
        {
            for (var index = 0; index < signatures.Count && index < results.Count; index++)
            {
                var signature = signatures[index];
                var result = results[index];
                var scope = signature.Scope == "template"
                    ? "form definition"
                    : string.Join(", ", signature.Fields);
                _signatures.Add(new SignatureStatusItem(
                    result.Id, result.Role.ToString(), result.SignerName, scope,
                    result.ContentValid, result.Trust.ToString(), result.Status));
            }
        }

        ApplyFieldCoverage(document);
        AnnounceNewBreakages(results);
        NotifyStateChanged();
    }

    public async Task RemoveAsync(string? signatureId)
    {
        var document = _session.CurrentDocument;
        if (document?.Signatures is not { Count: > 0 } signatures || signatureId is null) return;

        var target = signatures.FirstOrDefault(signature => signature.Id == signatureId);
        if (target is null) return;

        var signer = target.Signer?.Name ?? "someone";
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Remove this signature?",
            $"This deletes {signer}'s signature from the document.\n\n" +
            "If it no longer verifies, that fact is itself evidence — that somebody " +
            "signed this and it changed afterwards. Removing it leaves no trace that " +
            "they ever signed.\n\nRemove it anyway?");
        if (!confirmed) return;

        signatures.Remove(target);
        if (signatures.Count == 0) document.Signatures = null;
        _session.MarkDirty();
        _brokenAlreadyAnnounced.Remove(signatureId);
        BreakageNotice = null;
        Refresh();
    }

    public void DismissBreakageNotice()
    {
        BreakageNotice = null;
        NotifyStateChanged();
    }

    public void RestoreSignedValues()
    {
        foreach (var prompt in _prompts())
        {
            if (_responsesWhileValid.TryGetValue(prompt.Id, out var previous)
                && prompt.Response != previous)
            {
                prompt.Response = previous;
            }
        }

        Refresh();
        BreakageNotice = null;
        NotifyStateChanged();
    }

    public async Task SignAsPublisherAsync()
    {
        var document = _session.CurrentDocument;
        if (document is null) return;

        var certificatePath = await _fileService.PickCertificateAsync();
        if (string.IsNullOrEmpty(certificatePath)) return;
        var password = await _dialogService.ShowInputAsync(
            "Certificate password", "Enter the certificate password (leave blank if none):", string.Empty, isPassword: true);
        if (password is null) return;
        var urls = await _dialogService.ShowInputAsync(
            "Submission URLs", "Where is this form submitted? Separate choices with commas (all are bound into the signature)",
            string.Join(", ", document.Metadata.SubmissionUrls ?? []));
        if (urls is null) return;

        await SignWithAsync(certificatePath, password, certificate =>
        {
            if (!string.IsNullOrEmpty(urls))
                document.Metadata.SubmissionUrls = urls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            var signature = AprSigner.SignTemplate(document, certificate, DateTime.UtcNow);
            document.Metadata.Publisher ??= signature.Signer.Name;
            return signature;
        });
    }

    public async Task SignMyResponsesAsync()
    {
        var document = _session.CurrentDocument;
        if (document is null) return;

        var answered = FormExpressions.GetAllPrompts(document)
            .Where(prompt => !string.IsNullOrEmpty(prompt.Id) && !string.IsNullOrWhiteSpace(prompt.Response))
            .Select(prompt => prompt.Id)
            .ToList();
        if (answered.Count == 0)
        {
            await _dialogService.ShowConfirmationAsync(
                "Nothing to sign", "Fill in some responses first — a filler signature covers the fields you've answered.");
            return;
        }

        var certificatePath = await _fileService.PickCertificateAsync();
        if (string.IsNullOrEmpty(certificatePath)) return;
        var password = await _dialogService.ShowInputAsync(
            "Certificate password", "Enter the certificate password (leave blank if none):", string.Empty, isPassword: true);
        if (password is null) return;

        var id = $"sig{(document.Signatures?.Count ?? 0) + 1}";
        await SignWithAsync(certificatePath, password,
            certificate => AprSigner.SignFields(document, certificate, answered, DateTime.UtcNow, id));
    }

    private async Task SignWithAsync(string certificatePath, string password, Func<X509Certificate2, Signature> sign)
    {
        var document = _session.CurrentDocument;
        if (document is null) return;
        try
        {
            using var certificate = SignatureCertificates.LoadPfx(certificatePath, string.IsNullOrEmpty(password) ? null : password);
            if (!certificate.HasPrivateKey)
            {
                await _dialogService.ShowConfirmationAsync("Cannot sign", "That certificate has no private key — choose a .pfx that includes the key.");
                return;
            }

            document.Signatures ??= [];
            document.Signatures.Add(sign(certificate));
            _session.MarkDirty();
            Refresh();
        }
        catch (Exception exception)
        {
            await _dialogService.ShowConfirmationAsync("Signing failed", exception.Message);
        }
    }

    private void ApplyFieldCoverage(AprDocument? document)
    {
        var coverage = document is null
            ? new Dictionary<string, IReadOnlyList<CoveringSignature>>(StringComparer.Ordinal)
            : SignatureCoverage.ForDocument(document);
        foreach (var prompt in _prompts())
        {
            prompt.CoveringSignatures = coverage.TryGetValue(prompt.Id, out var covering) ? covering : [];
        }
    }

    private void AnnounceNewBreakages(IReadOnlyList<SignatureVerification> results)
    {
        var validNow = results.Where(result => result.ContentValid).Select(result => result.Id)
            .ToHashSet(StringComparer.Ordinal);
        var justBroke = results.Where(result => !result.ContentValid
                                                && _validLastTime.Contains(result.Id)
                                                && _brokenAlreadyAnnounced.Add(result.Id)).ToList();
        if (justBroke.Count > 0)
        {
            var who = string.Join(" and ", justBroke.Select(result => result.SignerName).Distinct(StringComparer.Ordinal));
            BreakageNotice = $"That edit means {who}'s signature no longer verifies. The document is " +
                            "still valid and still saves — but what they signed off on is not what it says now.";
        }

        if (results.Count > 0 && results.All(result => result.ContentValid))
        {
            _responsesWhileValid = _prompts().ToDictionary(prompt => prompt.Id, prompt => prompt.Response, StringComparer.Ordinal);
        }

        _brokenAlreadyAnnounced.ExceptWith(validNow);
        _validLastTime = validNow;
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
