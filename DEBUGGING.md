# PromptResponse Debug Logging Guide

## Overview

The PromptResponse Desktop application now includes comprehensive debug logging throughout the execution flow. This logging helps track application lifecycle, user interactions, and diagnose issues in real-time.

## Running with Debug Logging

### Using the Launcher Script (Recommended)

```bash
./run.sh
```

The launcher script automatically shows all debug output in real-time when launching the GUI.

### Manual Execution

```bash
dotnet run --project src/PromptResponse.Desktop
```

## What Gets Logged

### Application Startup (Program.cs)

**Logged Information:**
- Platform and .NET runtime version
- Working directory
- Command line arguments
- Avalonia configuration steps

**Example Output:**
```
info: PromptResponse.Desktop.Program[0]
      ================================================================================
info: PromptResponse.Desktop.Program[0]
      PromptResponse Desktop Application Starting
info: PromptResponse.Desktop.Program[0]
      ================================================================================
info: PromptResponse.Desktop.Program[0]
      Platform: Unix 6.17.0.1002
info: PromptResponse.Desktop.Program[0]
      Runtime: .NET 10.0.7
info: PromptResponse.Desktop.Program[0]
      Working Directory: /home/user/promptresponse
```

### Application Initialization (App.axaml.cs)

**Logged Events:**
- XAML resource loading
- Dependency injection container setup
- Service registration (each service logged individually)
- MainWindow creation
- Application lifetime events

**Example Output:**
```
[App] Initialize() called - Loading XAML resources
[App] XAML resources loaded successfully
[App] OnFrameworkInitializationCompleted() called
[App] Lifetime type: ClassicDesktopStyleApplicationLifetime
[App] Setting up dependency injection container...
[App] Configuring services...
[App]   - Registering IAprSerializer -> AprJsonSerializer
[App]   - Registering DocumentValidator
[App]   - Registering DataTypeValidator
[App]   - Registering IFileService -> FileService
[App]   - Registering MainWindowViewModel
[App]   - Registering FormFillingViewModel
[App] Service configuration complete
```

### MainWindow Creation (MainWindow.axaml.cs)

**Logged Events:**
- Window constructor execution
- Component initialization
- Window lifecycle events (Opened, Closing, Closed)
- Menu interactions (File Open, Save, Exit, About)

**Example Output:**
```
[MainWindow] Constructor called
[MainWindow] Initializing components...
[MainWindow] Components initialized
[MainWindow] Constructor complete
[MainWindow] Window Opened event fired
```

### ViewModel Operations (MainWindowViewModel.cs)

**Logged Events:**
- ViewModel construction
- Command initialization
- File operations (Open, Save, SaveAs)
- Document loading and parsing
- Window title updates

**Example Output:**
```
info: PromptResponse.Desktop.ViewModels.MainWindowViewModel[0]
      MainWindowViewModel constructor called
dbug: PromptResponse.Desktop.ViewModels.MainWindowViewModel[0]
      FileService type: FileService
dbug: PromptResponse.Desktop.ViewModels.MainWindowViewModel[0]
      Setting up commands...
info: PromptResponse.Desktop.ViewModels.MainWindowViewModel[0]
      MainWindowViewModel initialized successfully
```

### User Actions

**File Open:**
```
info: MainWindowViewModel[0]
      OpenFile command invoked
dbug: MainWindowViewModel[0]
      Calling FileService.OpenFileAsync()...
info: MainWindowViewModel[0]
      Document loaded successfully
dbug: MainWindowViewModel[0]
      Document Type: Template
dbug: MainWindowViewModel[0]
      Title: Employment Application Form
dbug: MainWindowViewModel[0]
      Sections: 4
dbug: MainWindowViewModel[0]
      Creating FormFillingViewModel...
info: MainWindowViewModel[0]
      Document opened and view updated
```

**File Save:**
```
info: MainWindowViewModel[0]
      SaveFile command invoked
info: MainWindowViewModel[0]
      Saving to existing file: /path/to/file.apr
dbug: MainWindowViewModel[0]
      Updating document from ViewModel...
dbug: MainWindowViewModel[0]
      Calling FileService.SaveFileAsync()...
info: MainWindowViewModel[0]
      File saved successfully
```

## Log Levels

The application uses Microsoft.Extensions.Logging with the following levels:

| Level | When Used | Example |
|-------|-----------|---------|
| **Debug** | Detailed trace information | Method calls, parameter values, internal state |
| **Information** | General flow of application | App starting, file opened, command invoked |
| **Warning** | Unexpected but handled situations | Cancelled dialog, missing optional data |
| **Error** | Errors and exceptions | Failed file operations, parse errors |
| **Critical** | Fatal errors causing shutdown | Unhandled exceptions, missing dependencies |

## Log Output Format

Logs follow the Microsoft.Extensions.Logging format:

```
<level>: <category>[<event-id>]
      <message>
```

**Example:**
```
info: PromptResponse.Desktop.Program[0]
      PromptResponse Desktop Application Starting
```

Where:
- `info` = Log level
- `PromptResponse.Desktop.Program` = Source class
- `[0]` = Event ID
- Message follows on next line(s)

## Execution Flow Tracking

You can track the complete execution flow by following the log entries:

### Successful Startup Flow

1. **Program.Main()** - Entry point, logging setup
2. **Program.BuildAvaloniaApp()** - Avalonia configuration
3. **App.Initialize()** - XAML loading
4. **App.OnFrameworkInitializationCompleted()** - DI setup
5. **MainWindowViewModel constructor** - ViewModel creation
6. **MainWindow constructor** - Window creation
7. **MainWindow.Opened** - Window displayed

### User Action Flow (Opening a File)

1. User clicks File → Open
2. **MainWindowViewModel.OpenFileAsync()** invoked
3. File dialog shown (logged)
4. User selects file
5. **FileService.OpenFileAsync()** called
6. Document deserialized (Core library)
7. **FormFillingViewModel** created
8. Window title updated
9. View refreshed

## Error Diagnostics

When errors occur, you'll see:

```
fail: PromptResponse.Desktop.ViewModels.MainWindowViewModel[0]
      Error opening file
      System.IO.FileNotFoundException: The file was not found.
         at ...stack trace...
```

Additionally, a console error message is printed:
```
Error opening file: The file was not found.
```

## Verifying Correct Behavior

### Expected Log Sequence on Startup

1. ✅ "PromptResponse Desktop Application Starting"
2. ✅ Platform and runtime info
3. ✅ "Building Avalonia application..."
4. ✅ "Initialize() called - Loading XAML resources"
5. ✅ "XAML resources loaded successfully"
6. ✅ "Setting up dependency injection container..."
7. ✅ All services registered (7 service log lines)
8. ✅ "Service provider built successfully"
9. ✅ "MainWindowViewModel constructor called"
10. ✅ "MainWindowViewModel initialized successfully"
11. ✅ "MainWindow created and assigned"
12. ✅ "Framework initialization completed successfully"
13. ✅ "Window Opened event fired"

If any of these steps are missing or errors appear, check the error logs for details.

## Customizing Log Level

To change the minimum log level, edit `Program.cs`:

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Debug);  // Change this
});
```

Available levels (from most to least verbose):
- `LogLevel.Trace` - Extremely detailed
- `LogLevel.Debug` - Detailed (current default)
- `LogLevel.Information` - General information
- `LogLevel.Warning` - Warnings only
- `LogLevel.Error` - Errors only
- `LogLevel.Critical` - Critical errors only

## Filtering Logs

### By Category (Class Name)

To see only logs from a specific component:

```bash
dotnet run --project src/PromptResponse.Desktop 2>&1 | grep "MainWindowViewModel"
```

### By Log Level

To see only errors and above:

```bash
dotnet run --project src/PromptResponse.Desktop 2>&1 | grep -E "fail:|crit:"
```

### By Component Tag

To see only our custom `[Component]` tagged logs:

```bash
dotnet run --project src/PromptResponse.Desktop 2>&1 | grep "\[.*\]"
```

## Troubleshooting Common Issues

### No Logs Appearing

**Problem:** No debug output visible
**Solution:** Ensure you're running via `./run.sh` or `dotnet run` (not with `--no-build` silent flags)

### Too Much Output

**Problem:** Overwhelming amount of log data
**Solution:** Change `SetMinimumLevel(LogLevel.Information)` or higher to reduce verbosity

### Missing Execution Steps

**Problem:** Expected log entry not appearing
**Solution:** Check if that code path was actually executed. Use Debug level to see more detail.

### Exception Stack Traces

**Problem:** Need full exception details
**Solution:** Exceptions are logged at Error level with full stack traces. Scroll up to find the `fail:` entries.

## Log File (Future Enhancement)

Currently logs only go to console. To save logs to file:

```bash
./run.sh 2>&1 | tee debug-log-$(date +%Y%m%d-%H%M%S).txt
```

This creates a timestamped log file while still showing output on screen.

## Performance Impact

Debug logging has minimal performance impact:
- Console output: ~1-2ms per log entry
- Structured logging: No serialization overhead for unformatted messages
- Conditional logging: Debug logs can be compiled out in Release builds

For production, switch to `Information` level or higher.

## Related Documentation

- `docs/ARCHITECTURE.md` - Application architecture
- `docs/DEVELOPMENT.md` - Development guidelines
- `LAUNCHER.md` - Running the application
