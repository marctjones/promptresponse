using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PromptResponse.Desktop.Views;

/// <summary>
/// About dialog. Shows the application name, version, license, and an
/// acknowledgements list of every third-party open source runtime dependency
/// the desktop binary ships.
///
/// The acknowledgements list is hand-maintained: it must stay in sync with
/// the runtime <c>PackageReference</c> entries in
/// <c>src/PromptResponse.Desktop/PromptResponse.Desktop.csproj</c> and
/// <c>src/PromptResponse.Core/PromptResponse.Core.csproj</c>. Test-only
/// dependencies (xUnit, NSubstitute, AwesomeAssertions, coverlet, …) don't
/// ship in the user-facing binary and are intentionally excluded — they
/// belong in CONTRIBUTING / dev docs, not the user About dialog.
/// </summary>
public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();

        var versionLine = this.FindControl<TextBlock>("VersionLine");
        if (versionLine != null)
        {
            versionLine.Text = $"Version {AssemblyVersion}  ·  .NET {System.Environment.Version}";
        }

        var list = this.FindControl<ItemsControl>("AcknowledgementsList");
        if (list != null)
        {
            list.ItemsSource = Acknowledgements;
        }

        var close = this.FindControl<Button>("CloseButton");
        if (close != null)
        {
            close.Click += (_, _) => Close();
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private static string AssemblyVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1";

    /// <summary>
    /// Open source runtime dependencies that ship with the desktop binary.
    /// Update this list whenever a runtime <c>PackageReference</c> changes
    /// (add / remove / version bump). The corresponding test
    /// <c>AboutDialogAcknowledgementsTests</c> guards parity with the actual
    /// .csproj entries so divergence is caught at build time.
    /// </summary>
    public static IReadOnlyList<Acknowledgement> Acknowledgements { get; } = new[]
    {
        new Acknowledgement(".NET Runtime", "10.0", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Avalonia", "12.0.2", "MIT License — © The Avalonia Project",
            Note: "Cross-platform .NET UI framework."),
        new Acknowledgement("Avalonia.Desktop", "12.0.2", "MIT License — © The Avalonia Project",
            Note: "Desktop platform backends (Win32, X11, macOS)."),
        new Acknowledgement("Avalonia.Themes.Fluent", "12.0.2", "MIT License — © The Avalonia Project"),
        new Acknowledgement("Avalonia.Fonts.Inter", "12.0.2", "MIT License (package) — © The Avalonia Project",
            Note: "Bundles the Inter font family by Rasmus Andersson, distributed under the SIL Open Font License 1.1."),
        new Acknowledgement("CommunityToolkit.Mvvm", "8.4.2", "MIT License — © .NET Foundation and Contributors",
            Note: "Source-generated MVVM helpers."),
        new Acknowledgement("Microsoft.Extensions.DependencyInjection", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.Logging.Abstractions", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.Logging.Console", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
    };

    /// <summary>
    /// Display record for a single open source acknowledgement entry. Bound
    /// to the acknowledgements list in the dialog template.
    /// </summary>
    public sealed record Acknowledgement(
        string Name,
        string Version,
        string License,
        string? Note = null)
    {
        public bool HasNote => !string.IsNullOrWhiteSpace(Note);
    }
}
