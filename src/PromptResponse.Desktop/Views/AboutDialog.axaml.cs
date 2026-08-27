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
    /// Open source runtime components that ship with the desktop binary.
    /// Covers the full transitive closure of NuGet packages resolved by
    /// the runtime projects (Desktop / Core / Cli), not just the direct
    /// <c>PackageReference</c> entries — so attribution is accurate even
    /// when an upgrade pulls in a new transitive dep.
    ///
    /// License metadata sourced from each package's <c>.nuspec</c> and
    /// cross-verified against a <c>scancode-toolkit</c> scan of the
    /// published binary. Packages with <c>developmentDependency=true</c>
    /// in their nuspec (build-time helpers like <c>Avalonia.BuildServices</c>)
    /// don't ship at runtime and are intentionally excluded.
    ///
    /// Update this list whenever the transitive closure changes. The
    /// drift-guard tests in <c>AboutDialogAcknowledgementsTests</c>
    /// check parity against each project's <c>project.assets.json</c>
    /// resolved package set so omissions fail at build time.
    /// </summary>
    public static IReadOnlyList<Acknowledgement> Acknowledgements { get; } = new[]
    {
        // The runtime itself. Not a NuGet PackageReference — exempted from the parity check.
        new Acknowledgement(".NET Runtime", "10.0", "MIT License — © .NET Foundation and Contributors",
            Note: "https://dot.net/"),

        // ─── Avalonia 12.0.2 — © The Avalonia Project, MIT ────────────────────────────
        new Acknowledgement("Avalonia", "12.0.2", "MIT License — © The Avalonia Project",
            Note: "Cross-platform .NET UI framework. https://avaloniaui.net/"),
        new Acknowledgement("Avalonia.Desktop", "12.0.2", "MIT License — © The Avalonia Project",
            Note: "Desktop platform backends meta-package."),
        new Acknowledgement("Avalonia.Themes.Fluent", "12.0.2", "MIT License — © The Avalonia Project"),
        new Acknowledgement("Avalonia.Fonts.Inter", "12.0.2", "MIT License (package) — © The Avalonia Project",
            Note: "Bundles the Inter font family by Rasmus Andersson, distributed under the SIL Open Font License 1.1."),
        new Acknowledgement("Avalonia.FreeDesktop", "12.0.2", "MIT License — © The Avalonia Project",
            Note: "freedesktop.org desktop integration (Linux)."),
        new Acknowledgement("Avalonia.FreeDesktop.AtSpi", "12.0.2", "MIT License — © The Avalonia Project",
            Note: "Native Linux screen-reader (AT-SPI2) support."),
        new Acknowledgement("Avalonia.HarfBuzz", "12.0.2", "MIT License — © The Avalonia Project"),
        new Acknowledgement("Avalonia.Native", "12.0.2", "MIT License — © The Avalonia Project",
            Note: "macOS native backend."),
        new Acknowledgement("Avalonia.Remote.Protocol", "12.0.2", "MIT License — © The Avalonia Project"),
        new Acknowledgement("Avalonia.Skia", "12.0.2", "MIT License — © The Avalonia Project"),
        new Acknowledgement("Avalonia.Win32", "12.0.2", "MIT License — © The Avalonia Project",
            Note: "Windows native backend."),
        new Acknowledgement("Avalonia.X11", "12.0.2", "MIT License — © The Avalonia Project",
            Note: "Linux X11 native backend."),

        // ANGLE (Almost Native Graphics Layer Engine) — Windows-only OpenGL ES translator.
        new Acknowledgement("Avalonia.Angle.Windows.Natives", "2.1.25547.20250602",
            "BSD-3-Clause License — © The ANGLE Project Authors",
            Note: "Google's ANGLE library, repackaged for Avalonia. Renders OpenGL ES on top of Direct3D on Windows."),

        // ─── MVVM ─────────────────────────────────────────────────────────────────────
        new Acknowledgement("CommunityToolkit.Mvvm", "8.4.2", "MIT License — © .NET Foundation and Contributors",
            Note: "Source-generated MVVM helpers."),

        // ─── Expression engine ───────────────────────────────────────────────────────
        new Acknowledgement("Celly", "1.2.0", "Apache License 2.0",
            Note: "Native C# implementation of Google's Common Expression Language (CEL). Evaluates the advisory expr* hints; passes the official cel-spec conformance suite."),

        // ─── PDF engine — © Marc Jones, MIT ──────────────────────────────────────────
        new Acknowledgement("Pdfe.Core", "2.9.0", "MIT License — © Marc Jones",
            Note: "Pure-managed PDF authoring/parsing engine; powers PDF and fillable-form export. https://github.com/marctjones/pdfe"),

        // ─── Skia / HarfBuzz native + managed wrappers — © Microsoft, MIT ────────────
        new Acknowledgement("SkiaSharp", "3.119.4-preview.1.1", "MIT License — © Microsoft Corporation",
            Note: "Managed wrapper around Skia. The native libSkiaSharp.so embeds Skia (BSD-3-Clause, © Google LLC) and image codec components under their respective licenses (libpng, libwebp, libjpeg-turbo, etc.)."),
        new Acknowledgement("SkiaSharp.NativeAssets.Linux", "3.119.4-preview.1.1", "MIT License — © Microsoft Corporation"),
        new Acknowledgement("SkiaSharp.NativeAssets.macOS", "3.119.4-preview.1.1", "MIT License — © Microsoft Corporation"),
        new Acknowledgement("SkiaSharp.NativeAssets.WebAssembly", "3.119.4-preview.1.1", "MIT License — © Microsoft Corporation"),
        new Acknowledgement("SkiaSharp.NativeAssets.Win32", "3.119.4-preview.1.1", "MIT License — © Microsoft Corporation"),
        new Acknowledgement("HarfBuzzSharp", "8.3.1.3", "MIT License — © Microsoft Corporation",
            Note: "Managed wrapper around HarfBuzz (MIT, © Behdad Esfahbod and contributors)."),
        new Acknowledgement("HarfBuzzSharp.NativeAssets.Linux", "8.3.1.3", "MIT License — © Microsoft Corporation"),
        new Acknowledgement("HarfBuzzSharp.NativeAssets.macOS", "8.3.1.3", "MIT License — © Microsoft Corporation"),
        new Acknowledgement("HarfBuzzSharp.NativeAssets.WebAssembly", "8.3.1.3", "MIT License — © Microsoft Corporation"),
        new Acknowledgement("HarfBuzzSharp.NativeAssets.Win32", "8.3.1.3", "MIT License — © Microsoft Corporation"),

        // ─── Microsoft.Extensions.* 10.0.7 — © .NET Foundation, MIT ──────────────────
        new Acknowledgement("Microsoft.Extensions.Configuration", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.Configuration.Abstractions", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.Configuration.Binder", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.DependencyInjection", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.DependencyInjection.Abstractions", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.Logging", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.Logging.Abstractions", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.Logging.Configuration", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.Logging.Console", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.Options", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.Options.ConfigurationExtensions", "10.0.7", "MIT License — © .NET Foundation and Contributors"),
        new Acknowledgement("Microsoft.Extensions.Primitives", "10.0.7", "MIT License — © .NET Foundation and Contributors"),

        // ─── System.* (BCL out-of-band) — © .NET Foundation, MIT ─────────────────────
        new Acknowledgement("System.Security.Cryptography.Pkcs", "10.0.0", "MIT License — © .NET Foundation and Contributors",
            Note: "CMS/PKCS#7 signing for verifiable APR signatures."),

        // ─── Bundled fonts (not NuGet packages) ─────────────────────────────────────
        new Acknowledgement("DejaVu Sans", "2.37", "Bitstream Vera / public-domain (DejaVu changes) — GPL-compatible",
            Note: "Embedded in PDF/A archival export (PDF/A forbids non-embedded base-14 fonts)."),

        // ─── Other ────────────────────────────────────────────────────────────────────
        new Acknowledgement("MicroCom.Runtime", "0.11.4", "MIT License — © 2021 Nikita Tsukanov",
            Note: "COM interop runtime used by Avalonia."),
        new Acknowledgement("Tmds.DBus.Protocol", "0.92.0", "MIT License — © Tom Deseyn",
            Note: "D-Bus protocol implementation used by AT-SPI2 on Linux."),
    };

}

/// <summary>
/// Display record for a single open source acknowledgement entry. Bound
/// to the acknowledgements list in the dialog template. Top-level (not
/// nested in <see cref="AboutDialog"/>) so Avalonia compiled bindings
/// can resolve <c>x:DataType</c> against it.
/// </summary>
public sealed record Acknowledgement(
    string Name,
    string Version,
    string License,
    string? Note = null)
{
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
}
