using Avalonia;
using Avalonia.Headless;
using PromptResponse.Desktop.Tests.Gui;

[assembly: AvaloniaTestApplication(typeof(HeadlessAppBuilder))]

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Boots an Avalonia <see cref="Application"/> in headless mode for the GUI test suite.
/// </summary>
/// <remarks>
/// The <see cref="AvaloniaTestApplicationAttribute"/> at assembly level points the
/// Avalonia.Headless.XUnit test framework at this builder so every <c>[AvaloniaFact]</c>
/// or <c>[AvaloniaTheory]</c> runs on a fully-initialized Avalonia dispatcher with a
/// software-rendering surface. No display server (X11/Wayland) is required.
/// </remarks>
public static class HeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                // Skia rendering with headless drawing disabled so we get a real font
                // manager (for TextBox/Button measure) AND CaptureRenderedFrame works.
                UseHeadlessDrawing = false,
            });
}
