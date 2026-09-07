// Drives the real Desktop app -- the shipped App, the shipped views, the shipped
// MainShellViewModel and DocumentDeliveryWorkflow, and the shipped HttpsSubmissionService --
// through filling a form by typing into its actual rendered TextBoxes and clicking
// "Submit via HTTPS" through the actual command, so that PUT reaches a real MinIO instance
// exactly the way a person using the desktop client would produce it.
//
// The one thing that has no keyboard-and-mouse equivalent in a headless run is trusting a
// throwaway self-signed certificate: on a real desktop this would need the OS to trust it,
// which this repository's demos have never asked of the Mac's own Keychain. Instead the
// certificate this run should trust is passed in by exact bytes and pinned in a
// HttpClientHandler passed to the real HttpsSubmissionService through the constructor seam
// it already exposes for tests -- nothing here disables validation for any other host, and
// nothing touches a trust store.
//
//     dotnet run --project tools/PromptResponse.GuiSubmitDemo.Avalonia -- \
//         <template.aprt> <presignedPutUrl> <minio-cert.crt> <screenshot.png> \
//         <captured-body.aprf> <local-save.aprf>
//
// The last two outputs exist for verification a caller (demo.sh) does afterward: the real
// FileService (not a stub -- see Shell.cs) saves a local copy the same way a person clicking
// Save would, and a DelegatingHandler captures the exact bytes the HTTP client put on the
// wire. Both should end up identical to each other and to what MinIO ends up holding --
// nothing between "Save" and "Submit" here re-serializes or re-stamps the document, so they
// aren't expected to merely agree in content, they're expected to be byte-identical.
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Models;
using PromptResponse.Desktop;
using PromptResponse.GuiSubmitDemo.Avalonia;

const double Width = 1200, Height = 900;

if (args.Length != 6)
{
    Console.Error.WriteLine(
        "Usage: <template.aprt> <presignedPutUrl> <minio-cert.crt> <screenshot.png> "
        + "<captured-body.aprf> <local-save.aprf>");
    return 1;
}
var (templatePath, presignedUrl, certPath, screenshotPath, capturedBodyPath, localSavePath) =
    (args[0], args[1], args[2], args[3], args[4], args[5]);

// The values a person would type into this form -- distinct from the CLI demo's Rex/Jane
// pair, so the two objects landing in the same bucket are visibly two different demo paths.
var values = new Dictionary<string, string>
{
    ["prompt_dog_name"] = "Buddy",
    ["prompt_breed"] = "Beagle",
    ["prompt_owner_name"] = "Sam Rivera",
    ["prompt_owner_phone"] = "+1 (555) 987-6543",
    ["prompt_rabies_vaccination_date"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
};

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

// Pin to this one certificate's exact bytes. errors == None still passes so a properly
// CA-signed MinIO deployment would need no pin at all; everything else fails exactly as
// default validation would.
var pinned = X509CertificateLoader.LoadCertificateFromFile(certPath);
var handler = new HttpClientHandler { AllowAutoRedirect = false };
handler.ServerCertificateCustomValidationCallback = (_, cert, _, errors) =>
    errors == SslPolicyErrors.None || (cert is not null && cert.RawData.AsSpan().SequenceEqual(pinned.RawData));

var shell = Shell.Create(new CapturingHandler(handler, capturedBodyPath));

var source = File.ReadAllText(templatePath);
var document = new AprBeta6Reader().ReadForm(source, AprRepresentation.Jsonc);
document.DocumentType = DocumentType.FilledForm;
document.Metadata.SubmissionUrls = [presignedUrl];
shell.Session.Set(document, filePath: null);

var window = new Window { Width = Width, Height = Height, Content = shell.View };
window.Show();
Settle(window);

Console.WriteLine("Filling the form by typing into its real rendered fields:");
foreach (var (promptId, value) in values)
{
    var container = window.GetVisualDescendants().OfType<Control>()
        .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == promptId);
    if (container is null) { Console.Error.WriteLine($"  '{promptId}' did not render."); return 1; }
    if (PrimaryInput.Of(container) is not TextBox box)
    { Console.Error.WriteLine($"  '{promptId}' has no typeable input."); return 1; }

    box.Focus();
    Dispatcher.UIThread.RunJobs();
    foreach (var ch in value) window.KeyTextInput(ch.ToString());
    Dispatcher.UIThread.RunJobs();
    Console.WriteLine($"  {promptId} = \"{box.Text}\"");
}

Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath) is { Length: > 0 } dir ? dir : ".");
using (var fs = File.Create(screenshotPath))
{
    var frame = HeadlessWindowExtensions.CaptureRenderedFrame(window)
        ?? throw new InvalidOperationException("Headless harness produced no frame.");
    frame.Save(fs, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
}
Console.WriteLine($"Screenshot of the filled form: {screenshotPath}");

// The same real FileService a "Save" menu action would use, saving the same in-session
// document Submit is about to read from -- so this file and what reaches MinIO should be
// byte-identical, the same relationship the CLI demo checks between its own filled file
// and what it submits.
//
// Not a plain `await`: SetupWithoutStarting() installs no dispatcher loop, so a
// continuation posted back to it (as an unconfigured await inside SaveFileAsync's own
// file I/O would) never runs unless something pumps it -- the same reason
// SubmitViaHttpsCommand below is driven by hand rather than awaited directly.
var saveTask = shell.Files.SaveFileAsync(document, localSavePath);
while (!saveTask.IsCompleted)
{
    Dispatcher.UIThread.RunJobs();
    Thread.Sleep(20);
}
saveTask.GetAwaiter().GetResult();
Console.WriteLine($"Saved locally (same as a real \"Save\" action would): {localSavePath}");

if (!shell.ViewModel.CanSubmitViaHttps())
{
    Console.Error.WriteLine(
        "CanSubmitViaHttps() is false -- the document isn't in fill mode with an https "
        + "submissionUrls entry, so 'Submit via HTTPS' would be disabled in the real app too.");
    return 1;
}

Console.WriteLine("Invoking the real \"Submit via HTTPS\" command:");
var command = (IAsyncRelayCommand)shell.ViewModel.SubmitViaHttpsCommand;
command.Execute(null);
// SetupWithoutStarting() runs no dispatcher loop of its own; the command's continuations
// (including the real network await inside HttpsSubmissionService) need this pumped by hand.
while (command.ExecutionTask is { IsCompleted: false })
{
    Dispatcher.UIThread.RunJobs();
    Thread.Sleep(20);
}
command.ExecutionTask?.GetAwaiter().GetResult();

window.Close();
Console.WriteLine("Done -- see the [dialog] lines above for what the real app asked and answered.");
return 0;

static void Settle(Window window)
{
    Dispatcher.UIThread.RunJobs();
    window.Measure(new Size(Width, Height));
    window.Arrange(new Rect(0, 0, Width, Height));
    window.UpdateLayout();
    HeadlessWindowExtensions.CaptureRenderedFrame(window);
    Dispatcher.UIThread.RunJobs();
}
