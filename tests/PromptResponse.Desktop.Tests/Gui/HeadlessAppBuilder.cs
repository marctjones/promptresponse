using Avalonia;
using Avalonia.Headless;
using PromptResponse.Desktop;
using PromptResponse.Desktop.Tests.Gui;

[assembly: AvaloniaTestApplication(typeof(HeadlessAppBuilder))]

namespace PromptResponse.Desktop.Tests.Gui;

/// <summary>
/// Boots an Avalonia <see cref="App"/> in headless mode for the GUI test suite.
/// </summary>
/// <remarks>
/// Uses the project's real <see cref="App"/> class so FluentTheme styles are loaded;
/// otherwise <c>ItemsControl</c>, <c>Button</c>, and other templated controls render
/// with no <c>ControlTemplate</c> applied and never materialize children. That gap
/// previously hid every "live form prompts not rendering" bug.
///
/// The <see cref="AvaloniaTestApplicationAttribute"/> at assembly level points the
/// Avalonia.Headless.XUnit test framework at this builder so every <c>[AvaloniaFact]</c>
/// or <c>[AvaloniaTheory]</c> runs on a fully-initialized Avalonia dispatcher with a
/// software-rendering surface and the production theme stack.
/// </remarks>
public static class HeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}
