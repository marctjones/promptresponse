# PromptResponse debug logging guide

## Running with debug output

```bash
./run.sh
```

or directly:

```bash
dotnet run --project src/PromptResponse.Desktop
```

Both print `Microsoft.Extensions.Logging` output to the console. There is no
separate debug build or flag: the default minimum level is `Debug`, set in
`Program.cs`.

## What actually logs today

Logging is not comprehensive across the desktop client — it covers startup,
shutdown, and a few services, not every ViewModel or user action. As of
2026-09, `ILogger<T>` is used in:

- `Program.cs` — process startup: platform, .NET runtime version, working
  directory, command-line arguments, and Avalonia configuration steps.
- `App.axaml.cs` — framework initialization, the restored capability profile,
  `MainWindow shown with new MainShellViewModel`, and `Auto-opened startup
  file` when a file was passed on the command line. A fatal top-level
  exception is also written to `Console.WriteLine` here and in `Program.cs`,
  since a crash during startup may happen before logging is configured.
- `Services/SettingsService.cs` — settings path, load/save outcomes.
- `Services/DialogService.cs` — dialog lifecycle.

Most ViewModel and workflow code (`MainShellViewModel`, the `Workflows/`
classes, `SectionViewModel`, the prompt ViewModels) does not log at all today.
If you need to trace a specific operation there, add `ILogger<T>` to the
relevant class rather than assuming an existing log line will show it — check
the source, not this document, for what currently emits.

## Log format

Standard `Microsoft.Extensions.Logging` console format:

```
<level>: <category>[<event-id>]
      <message>
```

For example, a real startup on this machine:

```
info: PromptResponse.Desktop.Program[0]
      PromptResponse Desktop Application Starting
info: PromptResponse.Desktop.Program[0]
      Platform: Unix 26.6.2
info: PromptResponse.Desktop.Program[0]
      Runtime: .NET 10.0.11
info: PromptResponse.Desktop.Services.SettingsService[0]
      SettingsService initialized. Settings path: /Users/<you>/Library/Application Support/PromptResponse/settings.json
info: PromptResponse.Desktop.App[0]
      MainWindow shown with new MainShellViewModel.
info: PromptResponse.Desktop.App[0]
      Auto-opened startup file: examples/contact-intake.aprt
```

`<category>` is always the fully-qualified class name doing the logging, so
`grep "PromptResponse.Desktop.App"` isolates one component's lines.

## Changing the log level

Edit the `LoggerFactory` setup in `Program.cs`:

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Debug);  // change this
});
```

From most to least verbose: `Trace`, `Debug` (current default), `Information`,
`Warning`, `Error`, `Critical`.

## Filtering

```bash
# One component
dotnet run --project src/PromptResponse.Desktop 2>&1 | grep "PromptResponse.Desktop.App"

# Errors and above
dotnet run --project src/PromptResponse.Desktop 2>&1 | grep -E "fail:|crit:"

# Save to a timestamped file while still watching it live
./run.sh 2>&1 | tee "debug-log-$(date +%Y%m%d-%H%M%S).txt"
```

## Related documentation

- `docs/ARCHITECTURE.md` — application architecture.
- `docs/DEVELOPMENT.md` — development guidelines.
- `docs/USER_GUIDE.md` — running the application and launcher scripts.
