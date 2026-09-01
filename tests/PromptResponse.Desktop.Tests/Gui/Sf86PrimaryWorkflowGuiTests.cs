using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using AwesomeAssertions;
using NSubstitute;
using PromptResponse.Core.Models;
using PromptResponse.Core.Serialization;
using PromptResponse.Desktop.Profiles;
using PromptResponse.Desktop.Services;
using PromptResponse.Desktop.ViewModels;
using PromptResponse.Desktop.ViewModels.Prompts;
using PromptResponse.Desktop.Views;

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Drives the primary real-world path through the rendered shell using the
/// shipped SF-86 fixture: open, fill by keyboard, save, and reopen. It deliberately
/// uses the production file service rather than a substitute, so beta.6 stream I/O
/// and GUI bindings are exercised together.
/// </summary>
public class Sf86PrimaryWorkflowGuiTests
{
    private sealed class FixedProbe : IOsAccessibilityProbe
    {
        public bool HighContrast => false;
        public bool ReducedMotion => false;
        public bool ScreenReaderActive => false;
        public ColorScheme PreferredColorScheme => ColorScheme.Light;
    }

    private static string RepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory.Parent is not null && !File.Exists(Path.Combine(directory.FullName, "PromptResponse.sln")))
            directory = directory.Parent;
        return Path.Combine([directory.FullName, .. parts]);
    }

    [AvaloniaFact]
    public async Task ShippedSf86_OpenFillSaveAndReopen_UsesRenderedKeyboardWorkflow()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "promptresponse-gui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        var path = Path.Combine(workspace, "sf-86.aprt");
        File.Copy(RepositoryPath("examples", "sf-86-background-check.aprt"), path);

        try
        {
            var files = new FileService(new AprJsonSerializer());
            var dialogs = Substitute.For<IDialogService>();
            var session = new DocumentSessionService();
            var profile = new ProfileService(new FixedProbe(), applyAffordanceDefaults: false);
            var shell = new MainShellViewModel(files, dialogs, session, profile, new PromptViewModelFactory(profile));
            var view = new MainShellView { DataContext = shell };
            var window = view.ShowInWindow(width: 1400, height: 900);

            await shell.OpenFromPath(path, openForFilling: true);
            for (var i = 0; i < 6; i++)
            {
                window.UpdateLayout();
                GuiTestExtensions.PumpDispatcher();
            }

            shell.IsEditMode.Should().BeFalse("--open presents a template as a fillable form");
            shell.PromptViewModels.Should().HaveCount(111, "the shipped SF-86 must render every prompt");

            var investigation = view.GetVisualDescendants().OfType<ComboBox>().Single(comboBox =>
                AutomationProperties.GetName(comboBox) == "Type of Investigation Requested");
            investigation.Focus();
            investigation.IsFocused.Should().BeTrue("the first SF-86 response field is keyboard reachable");
            window.PressKey(Key.Down);
            window.PressKey(Key.Enter);
            window.PressKey(Key.Tab);

            shell.PromptViewModels.Single(prompt => prompt.Id == "prompt_investigation_type").Response
                .Should().Be("Initial Investigation", "typed UI input must update the underlying APR response");
            shell.PromptViewModels.Single(prompt => prompt.Id == "prompt_agency_use").Response
                .Should().BeEmpty("Tab moves through the form without altering another answer");

            await shell.Save();
            var reopened = await files.LoadFileAsync(path);
            reopened.Should().NotBeNull();
            reopened!.DocumentType.Should().Be(DocumentType.Template,
                "filling a template does not silently change its document type on save");
            reopened.Sections.SelectMany(Flatten).Single(prompt => prompt.Id == "prompt_investigation_type").Response
                .Should().Be("Initial Investigation", "the keyboard-entered answer must survive production beta.6 persistence");
        }
        finally
        {
            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        }

        static IEnumerable<Prompt> Flatten(Section section) =>
            section.Prompts.Concat(section.Sections.SelectMany(Flatten));
    }
}
