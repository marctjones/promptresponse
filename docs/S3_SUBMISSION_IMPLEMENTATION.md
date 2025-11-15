# S3 Submission Feature Implementation Guide

This document describes the S3 pre-signed POST submission feature implementation and provides guidance for completing the UI integration.

## Overview

This feature enables APR templates to include S3 pre-signed POST configuration that allows users to submit filled forms directly to S3 buckets without requiring server-side code or serverless functions.

**Status**: Backend services complete, UI integration pending

## What's Been Implemented

### ✅ Core Models (src/PromptResponse.Core/Models/)

1. **SubmissionConfig.cs** - Model for S3 submission configuration
   - Properties: Type, Url, Fields, ExpiresAt, Headers
   - Methods: `IsExpired()`, `TimeUntilExpiration()`, `IsValid()`
   - Supports "s3-presigned-post" type with future extensibility for webhooks

2. **Metadata.cs** - Updated with SubmissionConfig property
   - Added `SubmissionConfig? SubmissionConfig { get; set; }`
   - Template-specific field for form submission configuration

### ✅ Services (src/PromptResponse.Desktop/Services/)

1. **IS3SubmissionService.cs + S3SubmissionService.cs**
   - `SubmitFormAsync()` - Submits filled form to S3 using pre-signed POST
   - `CanSubmit()` - Validates submission config exists and is valid
   - `GetExpirationStatus()` - Returns expiration status and time remaining
   - Uses HttpClient with MultipartFormDataContent for S3 POST
   - Handles filename placeholders (${filename})
   - Comprehensive error handling and logging

2. **IS3BrowserService.cs + S3BrowserService.cs**
   - `ListObjectsAsync()` - Lists objects in S3 bucket with optional prefix
   - `DownloadDocumentAsync()` - Downloads and deserializes APR documents from S3
   - `TestConnectionAsync()` - Tests S3 bucket connectivity
   - `DeleteObjectAsync()` - Deletes objects from S3
   - `S3BucketConfig` record for connection configuration
   - `S3Object` record for S3 object metadata
   - Uses AWS SDK for .NET (AWSSDK.S3)

3. **ITemplatePublishingService.cs + TemplatePublishingService.cs**
   - `PublishTemplateAsync()` - Publishes signed templates to S3 gallery
   - `ValidateForPublishing()` - Validates template is signed and valid
   - `ListPublishedTemplatesAsync()` - Lists templates in gallery
   - `UnpublishTemplateAsync()` - Removes template from gallery
   - Requires templates to be digitally signed before publishing
   - Attaches metadata to S3 objects (template-id, version, author, is-signed)

4. **ITemplateGalleryService.cs + TemplateGalleryService.cs**
   - `BrowseTemplatesAsync()` - Lists available templates with metadata
   - `DownloadTemplateAsync()` - Downloads template from gallery
   - `DownloadAndVerifyTemplateAsync()` - Downloads and verifies signature
   - `SearchTemplatesAsync()` - Searches templates by keyword
   - `TemplateGalleryItem` record for template metadata display
   - Integrates with signature verification service

### ✅ Dependencies

- Added `AWSSDK.S3` package (version 3.7.403.9) to Desktop project

### ✅ Developer Tools (dev/)

1. **generate-s3-policy.sh** - Interactive script to generate pre-signed POST policies
   - Prompts for S3 configuration (endpoint, bucket, keys, region)
   - Calculates expiration timestamp
   - Generates Base64-encoded policy
   - Calculates HMAC-SHA1 signature
   - Outputs JSON ready to paste into template metadata
   - Includes helpful instructions for testing

2. **minio-setup-gallery.sh** - Sets up MinIO template gallery for testing
   - Creates `template-gallery` bucket with public read access
   - Creates directory structure (templates/official/, templates/community/)
   - Creates `form-submissions` bucket for submitted forms
   - Configures appropriate bucket policies
   - Provides configuration examples for publishing and downloading

### ✅ Test Template

- **examples/test-s3-submission.aprt** - Sample template with S3 submission config
  - Configured for local MinIO testing (http://localhost:9000)
  - Includes example submissionConfig with all required fields
  - Can be used to test submission feature once UI is complete

## What Needs to Be Implemented

### 🔲 UI Integration - Form Submission

#### FormFillingView.axaml
Add a "Submit to S3" button next to the Save button:

```xml
<!-- Add to the button panel -->
<Button Content="Submit to S3"
        Command="{Binding SubmitToS3Command}"
        IsVisible="{Binding CanSubmitToS3}"
        ToolTip.Tip="Submit this filled form directly to S3"
        Classes="accent" />

<!-- Add expiration warning (if expires soon) -->
<TextBlock Text="{Binding SubmissionExpirationWarning}"
           IsVisible="{Binding ShowExpirationWarning}"
           Foreground="Orange"
           FontSize="12" />
```

#### FormFillingViewModel.cs
Add submission logic:

```csharp
private readonly IS3SubmissionService _s3SubmissionService;

public bool CanSubmitToS3 =>
    Document != null && _s3SubmissionService.CanSubmit(Document);

public string? SubmissionExpirationWarning { get; private set; }
public bool ShowExpirationWarning { get; private set; }

public ICommand SubmitToS3Command { get; }

public FormFillingViewModel(/* ... */, IS3SubmissionService s3SubmissionService)
{
    _s3SubmissionService = s3SubmissionService;
    SubmitToS3Command = ReactiveCommand.CreateFromTask(SubmitToS3Async);

    // Update expiration warning
    this.WhenAnyValue(x => x.Document)
        .Subscribe(_ => UpdateExpirationWarning());
}

private void UpdateExpirationWarning()
{
    if (Document == null)
    {
        ShowExpirationWarning = false;
        return;
    }

    var (isExpired, timeRemaining) = _s3SubmissionService.GetExpirationStatus(Document);

    if (isExpired)
    {
        SubmissionExpirationWarning = "S3 submission expired - save locally";
        ShowExpirationWarning = true;
    }
    else if (timeRemaining.HasValue && timeRemaining.Value.TotalDays < 2)
    {
        SubmissionExpirationWarning =
            $"S3 submission expires in {timeRemaining.Value.Days}d {timeRemaining.Value.Hours}h";
        ShowExpirationWarning = true;
    }
    else
    {
        ShowExpirationWarning = false;
    }

    OnPropertyChanged(nameof(CanSubmitToS3));
    OnPropertyChanged(nameof(SubmissionExpirationWarning));
    OnPropertyChanged(nameof(ShowExpirationWarning));
}

private async Task SubmitToS3Async()
{
    if (Document == null)
    {
        return;
    }

    try
    {
        IsBusy = true;
        Status = "Submitting to S3...";

        var key = await _s3SubmissionService.SubmitFormAsync(Document);

        await _dialogService.ShowMessageAsync(
            "Submission Successful",
            $"Form submitted to S3 successfully!\n\nKey: {key}",
            "OK");

        Status = "Submitted to S3";
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to submit form to S3");

        await _dialogService.ShowErrorAsync(
            "Submission Failed",
            $"Failed to submit form to S3: {ex.Message}");

        Status = "Submission failed";
    }
    finally
    {
        IsBusy = false;
    }
}
```

### 🔲 UI Integration - S3 Browser

Create new views and view models for browsing S3 buckets:

#### S3BrowserView.axaml
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="PromptResponse.Desktop.Views.S3BrowserView">

  <Grid RowDefinitions="Auto,*">
    <!-- Connection Panel -->
    <StackPanel Grid.Row="0" Margin="10">
      <TextBlock Text="S3 Bucket Configuration" FontWeight="Bold" Margin="0,0,0,10"/>

      <Grid ColumnDefinitions="Auto,*" RowDefinitions="Auto,Auto,Auto,Auto,Auto,Auto">
        <TextBlock Grid.Row="0" Grid.Column="0" Text="Endpoint:" VerticalAlignment="Center" Margin="0,5"/>
        <TextBox Grid.Row="0" Grid.Column="1" Text="{Binding Config.ServiceUrl}" Margin="5"/>

        <TextBlock Grid.Row="1" Grid.Column="0" Text="Bucket:" VerticalAlignment="Center" Margin="0,5"/>
        <TextBox Grid.Row="1" Grid.Column="1" Text="{Binding Config.BucketName}" Margin="5"/>

        <TextBlock Grid.Row="2" Grid.Column="0" Text="Access Key:" VerticalAlignment="Center" Margin="0,5"/>
        <TextBox Grid.Row="2" Grid.Column="1" Text="{Binding Config.AccessKeyId}" Margin="5"/>

        <TextBlock Grid.Row="3" Grid.Column="0" Text="Secret Key:" VerticalAlignment="Center" Margin="0,5"/>
        <TextBox Grid.Row="3" Grid.Column="1" Text="{Binding Config.SecretAccessKey}"
                 PasswordChar="*" Margin="5"/>

        <TextBlock Grid.Row="4" Grid.Column="0" Text="Region:" VerticalAlignment="Center" Margin="0,5"/>
        <TextBox Grid.Row="4" Grid.Column="1" Text="{Binding Config.Region}" Margin="5"/>
      </Grid>

      <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10">
        <Button Content="Test Connection" Command="{Binding TestConnectionCommand}" Margin="5,0"/>
        <Button Content="Connect & List" Command="{Binding ListObjectsCommand}" Margin="5,0"
                Classes="accent"/>
      </StackPanel>
    </StackPanel>

    <!-- Objects List -->
    <DataGrid Grid.Row="1" Items="{Binding Objects}"
              IsReadOnly="True" GridLinesVisibility="All"
              SelectedItem="{Binding SelectedObject}">
      <DataGrid.Columns>
        <DataGridTextColumn Header="File Name" Binding="{Binding Key}" Width="*"/>
        <DataGridTextColumn Header="Size" Binding="{Binding Size}" Width="100"/>
        <DataGridTextColumn Header="Last Modified" Binding="{Binding LastModified}" Width="200"/>
      </DataGrid.Columns>
    </DataGrid>

    <!-- Action Buttons -->
    <StackPanel Grid.Row="1" VerticalAlignment="Bottom" HorizontalAlignment="Right"
                Orientation="Horizontal" Margin="10">
      <Button Content="Open" Command="{Binding OpenCommand}"
              IsEnabled="{Binding SelectedObject, Converter={x:Static ObjectConverters.IsNotNull}}"/>
      <Button Content="Delete" Command="{Binding DeleteCommand}"
              IsEnabled="{Binding SelectedObject, Converter={x:Static ObjectConverters.IsNotNull}}"/>
    </StackPanel>
  </Grid>
</UserControl>
```

#### S3BrowserViewModel.cs
```csharp
using ReactiveUI;
using System.Collections.ObjectModel;
using PromptResponse.Desktop.Services;

public class S3BrowserViewModel : ViewModelBase
{
    private readonly IS3BrowserService _s3BrowserService;
    private readonly ILogger<S3BrowserViewModel> _logger;

    public S3BucketConfig Config { get; } = new()
    {
        ServiceUrl = "http://localhost:9000",
        BucketName = "filled-forms",
        AccessKeyId = "promptresponse",
        SecretAccessKey = "promptresponse123",
        Region = "us-east-1",
        ForcePathStyle = true
    };

    public ObservableCollection<S3Object> Objects { get; } = new();

    private S3Object? _selectedObject;
    public S3Object? SelectedObject
    {
        get => _selectedObject;
        set => this.RaiseAndSetIfChanged(ref _selectedObject, value);
    }

    public ICommand TestConnectionCommand { get; }
    public ICommand ListObjectsCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand DeleteCommand { get; }

    public S3BrowserViewModel(
        IS3BrowserService s3BrowserService,
        ILogger<S3BrowserViewModel> logger)
    {
        _s3BrowserService = s3BrowserService;
        _logger = logger;

        TestConnectionCommand = ReactiveCommand.CreateFromTask(TestConnectionAsync);
        ListObjectsCommand = ReactiveCommand.CreateFromTask(ListObjectsAsync);
        OpenCommand = ReactiveCommand.CreateFromTask(OpenAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteAsync);
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            var success = await _s3BrowserService.TestConnectionAsync(Config);
            // Show success/failure message
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
        }
    }

    private async Task ListObjectsAsync()
    {
        try
        {
            var objects = await _s3BrowserService.ListObjectsAsync(Config);
            Objects.Clear();
            foreach (var obj in objects)
            {
                Objects.Add(obj);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list objects");
        }
    }

    private async Task OpenAsync()
    {
        if (SelectedObject == null) return;

        try
        {
            var document = await _s3BrowserService.DownloadDocumentAsync(
                Config,
                SelectedObject.Key);

            // Open document in FormFillingView
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open document");
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedObject == null) return;

        // Show confirmation dialog
        // If confirmed:
        await _s3BrowserService.DeleteObjectAsync(Config, SelectedObject.Key);
        await ListObjectsAsync(); // Refresh list
    }
}
```

### 🔲 Dependency Injection Setup

Update `Program.cs` to register S3 services:

```csharp
// Register S3 services
services.AddSingleton<IS3SubmissionService, S3SubmissionService>();
services.AddSingleton<IS3BrowserService, S3BrowserService>();

// Register S3BrowserViewModel
services.AddTransient<S3BrowserViewModel>();
```

### 🔲 Main Window Integration

Add S3 Browser menu item to MainWindow:

```xml
<MenuItem Header="_Tools">
  <MenuItem Header="_S3 Browser" Command="{Binding OpenS3BrowserCommand}"/>
</MenuItem>
```

## Testing Guide

### 1. Start MinIO

```bash
./dev/minio-start.sh
./dev/minio-init.sh
```

### 2. Generate Pre-Signed POST Policy

```bash
./dev/generate-s3-policy.sh
```

Follow the prompts (use defaults for MinIO):
- Endpoint: `http://localhost:9000`
- Bucket: `filled-forms`
- Access Key: `promptresponse`
- Secret Key: `promptresponse123`
- Region: `us-east-1`

Copy the generated JSON submissionConfig.

### 3. Create or Update Template

Add the generated submissionConfig to a template's metadata section.

Or use the provided test template: `examples/test-s3-submission.aprt`

### 4. Test Submission

1. Open template in PromptResponse Desktop
2. Fill out the form
3. Click "Submit to S3" button
4. Verify submission in MinIO Console (http://localhost:9001)

### 5. Test S3 Browser

1. Open S3 Browser from Tools menu
2. Use MinIO credentials (defaults should work)
3. Click "Connect & List"
4. View submitted forms
5. Click "Open" to download and view a form
6. Click "Delete" to remove a form

## Security Considerations

### Development/Testing
- MinIO credentials are hardcoded for local testing only
- Never commit real AWS credentials to repository
- S3 policies expire automatically (7 days default)

### Production
- Use IAM roles instead of long-lived credentials when possible
- Set appropriate bucket policies and ACLs
- Use HTTPS endpoints only
- Implement policy refresh mechanism for long-running templates
- Validate all inputs before submission
- Log all S3 operations for audit trail

## Troubleshooting

### "SignatureDoesNotMatch" Error
- Ensure `ForcePathStyle = true` in S3BucketConfig
- Verify region matches (use "us-east-1" for MinIO)
- Check that policy and signature were generated correctly
- Verify system clock is correct (S3 is sensitive to clock skew)

### "Policy Expired" Error
- Regenerate policy using `./dev/generate-s3-policy.sh`
- Update template with new submissionConfig
- Consider implementing policy refresh from URL

### Connection Timeout
- Verify MinIO is running: `curl http://localhost:9000/minio/health/live`
- Check firewall settings
- Ensure endpoint URL is correct

### File Not Found When Opening
- Check that the file exists in MinIO Console
- Verify bucket name and key are correct
- Ensure read permissions on bucket

## Future Enhancements

### Planned Features
- [ ] Policy refresh from remote URL
- [ ] Multiple submission targets (S3 + webhook)
- [ ] Submission history/tracking
- [ ] Automatic retry on network failure
- [ ] Progress indicator for large uploads
- [ ] S3 Browser: Search/filter objects
- [ ] S3 Browser: Bulk operations
- [ ] S3 Browser: Download as file (not just open)
- [ ] Template validation: Check submission config validity
- [ ] CLI support for S3 submission (`apr submit form.aprf`)

### Webhook Support (Future)
The architecture supports webhook submission:

```json
{
  "submissionConfig": {
    "type": "webhook",
    "url": "https://api.example.com/forms/submit",
    "headers": {
      "Authorization": "Bearer token",
      "X-Form-ID": "template-id"
    },
    "expiresAt": null
  }
}
```

Implementation would require minimal changes to S3SubmissionService.

## Documentation

See also:
- [ROADMAP.md](../ROADMAP.md) - S3 submission in Phase 2 roadmap
- [docs/ARCHITECTURE.md](ARCHITECTURE.md) - Form submission architecture
- [docs/FILE_FORMAT.md](FILE_FORMAT.md) - SubmissionConfig spec
- [docs/MINIO_SETUP.md](MINIO_SETUP.md) - MinIO development setup

## Questions?

For questions or issues:
1. Check this implementation guide
2. Review MinIO setup documentation
3. Check AWS SDK for .NET documentation
4. Open issue on GitHub
