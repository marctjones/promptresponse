# Template Publishing & Gallery Workflow

This document describes the complete workflow for publishing signed templates to S3 and downloading them through a template gallery.

## Overview

The template publishing workflow enables a **distributed template marketplace** where:

1. **Template Publishers** create, sign, and publish templates to S3
2. **Form Fillers** browse the template gallery, download templates, verify signatures, fill forms, and submit back to S3

```
┌─────────────────────────────────────────────────────────────┐
│             Template Publishing Workflow                     │
└─────────────────────────────────────────────────────────────┘

Publisher Side:
┌──────────────┐   ┌──────────────┐   ┌──────────────┐
│ Create       │ → │ Sign         │ → │ Publish to   │
│ Template     │   │ Template     │   │ S3 Gallery   │
└──────────────┘   └──────────────┘   └──────────────┘

User Side:
┌──────────────┐   ┌──────────────┐   ┌──────────────┐   ┌──────────────┐
│ Browse       │ → │ Download &   │ → │ Fill Form    │ → │ Submit to    │
│ Gallery      │   │ Verify       │   │              │   │ S3 Bucket    │
└──────────────┘   └──────────────┘   └──────────────┘   └──────────────┘
```

---

## Use Cases

### Use Case 1: Government Forms Distribution
- **IRS** publishes signed tax form templates (W-4, 1040, etc.) to public S3 gallery
- **Taxpayers** browse gallery, download official signed templates
- Taxpayers verify IRS signature to ensure authenticity
- Fill out form and submit directly to IRS S3 bucket
- IRS processes submitted forms from S3

### Use Case 2: Corporate Form Library
- **HR Department** publishes internal forms (employment application, expense report, etc.)
- **Employees** browse company template gallery
- Download templates with HR's digital signature
- Fill and submit to company S3 bucket
- HR team reviews submissions via S3 browser

### Use Case 3: Open Template Marketplace
- **Community Contributors** create and publish templates
- Templates are signed by contributors for authenticity
- **Users** browse community templates
- Download, verify signature, use template
- Rate and review templates (future feature)

---

## Architecture

### S3 Bucket Structure

```
s3://template-gallery/
├── templates/
│   ├── official/
│   │   ├── irs-w4-2025.aprt          # Signed by IRS
│   │   ├── irs-1040-2025.aprt        # Signed by IRS
│   │   └── irs-w9-2025.aprt          # Signed by IRS
│   ├── hr/
│   │   ├── employment-app-v2.aprt    # Signed by HR
│   │   └── expense-report-v3.aprt    # Signed by HR
│   └── community/
│       ├── contact-form.aprt          # Signed by contributor
│       └── survey-template.aprt       # Signed by contributor
└── submitted-forms/
    ├── w4-submissions/
    ├── 1040-submissions/
    └── employment-submissions/
```

### Bucket Policies

**Template Gallery Bucket (Read-Only for Public)**:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": "*",
      "Action": "s3:GetObject",
      "Resource": "arn:aws:s3:::template-gallery/templates/*"
    },
    {
      "Effect": "Allow",
      "Principal": {
        "AWS": "arn:aws:iam::ACCOUNT:user/publisher"
      },
      "Action": ["s3:PutObject", "s3:DeleteObject"],
      "Resource": "arn:aws:s3:::template-gallery/templates/*"
    }
  ]
}
```

**Form Submission Bucket (Write via Pre-Signed POST)**:
- No public access
- Write access via pre-signed POST policies embedded in templates
- Read access for authorized reviewers only

---

## Implementation

### Services Implemented

#### ✅ ITemplatePublishingService + TemplatePublishingService

**Purpose**: Publish signed templates to S3 gallery

**Methods**:
- `PublishTemplateAsync()` - Upload signed template to S3
- `ValidateForPublishing()` - Ensure template is signed and valid
- `ListPublishedTemplatesAsync()` - List templates in gallery
- `UnpublishTemplateAsync()` - Remove template from gallery

**Validation Requirements**:
- ✅ Must be DocumentType.Template (not FilledForm)
- ✅ Must have metadata with templateId
- ✅ Must be digitally signed (has TemplateSignatures)
- ✅ Must pass APR validation

**S3 Metadata** (attached to published templates):
- `template-id`: Template identifier
- `template-version`: Version number
- `author`: Template author
- `is-signed`: "true" or "false"

#### ✅ ITemplateGalleryService + TemplateGalleryService

**Purpose**: Browse and download templates from S3 gallery

**Methods**:
- `BrowseTemplatesAsync()` - List available templates with metadata
- `DownloadTemplateAsync()` - Download template from gallery
- `DownloadAndVerifyTemplateAsync()` - Download and verify signature
- `SearchTemplatesAsync()` - Search templates by keyword

**Returns**: `TemplateGalleryItem` records with:
- Key, TemplateId, Title, Description, Author, Version
- IsSigned, Size, LastModified

---

## Workflows

### Workflow 1: Publishing a Template

#### Step 1: Create Template
```csharp
// Create template in PromptResponse Desktop
var template = new AprDocument
{
    Version = "1.0",
    DocumentType = DocumentType.Template,
    Metadata = new Metadata
    {
        Title = "Employment Application",
        TemplateId = "employment-app",
        TemplateVersion = "2.0",
        Author = "HR Department",
        Created = DateTime.UtcNow,
        Modified = DateTime.UtcNow
    },
    Sections = [ /* sections */ ]
};
```

#### Step 2: Sign Template
```csharp
// Sign template using digital signature service
var certificate = /* load publisher certificate */;
await signatureService.SignTemplateAsync(template, certificate, "Official HR template");
```

#### Step 3: Validate for Publishing
```csharp
var (isValid, errorMessage) = publishingService.ValidateForPublishing(template);
if (!isValid)
{
    ShowError($"Cannot publish: {errorMessage}");
    return;
}
```

#### Step 4: Publish to S3
```csharp
var config = new S3BucketConfig
{
    ServiceUrl = "https://s3.us-east-1.amazonaws.com",
    BucketName = "template-gallery",
    AccessKeyId = "PUBLISHER_ACCESS_KEY",
    SecretAccessKey = "PUBLISHER_SECRET_KEY",
    Region = "us-east-1",
    ForcePathStyle = false
};

var key = await publishingService.PublishTemplateAsync(
    config,
    template,
    fileName: "templates/hr/employment-app-v2.aprt");

ShowSuccess($"Template published to gallery: {key}");
```

### Workflow 2: Downloading a Template

#### Step 1: Browse Gallery
```csharp
var galleryConfig = new S3BucketConfig
{
    ServiceUrl = "https://s3.us-east-1.amazonaws.com",
    BucketName = "template-gallery",
    AccessKeyId = "PUBLIC_ACCESS_KEY",  // Read-only credentials
    SecretAccessKey = "PUBLIC_SECRET_KEY",
    Region = "us-east-1"
};

var templates = await galleryService.BrowseTemplatesAsync(
    galleryConfig,
    prefix: "templates/official/");

// Display templates in UI
foreach (var item in templates)
{
    DisplayTemplate(item);
}
```

#### Step 2: Download and Verify
```csharp
var selectedTemplate = /* user selection */;

var (template, verificationResult) = await galleryService.DownloadAndVerifyTemplateAsync(
    galleryConfig,
    selectedTemplate.Key);

if (verificationResult == null || !verificationResult.IsValid)
{
    var response = await ShowWarningAsync(
        "Signature Verification Failed",
        $"Template signature could not be verified: {verificationResult?.ErrorMessage}\n\n" +
        "Do you still want to use this template?",
        "Yes", "No");

    if (response == "No")
    {
        return;
    }
}
else
{
    ShowSuccess($"Template signed by: {verificationResult.SignerName}");
}
```

#### Step 3: Use Template
```csharp
// Convert template to filling mode
var filledForm = template.Clone();
filledForm.DocumentType = DocumentType.FilledForm;
filledForm.Metadata.FilledBy = currentUser;
filledForm.Metadata.FilledDate = DateTime.UtcNow;

// Open in form filling view
OpenFormFillingView(filledForm);
```

### Workflow 3: Search Templates
```csharp
var searchTerm = "employment";
var results = await galleryService.SearchTemplatesAsync(
    galleryConfig,
    searchTerm,
    prefix: "templates/");

// Results include templates where searchTerm matches:
// - TemplateId
// - Title
// - Description
// - Author
```

---

## UI Integration

### Template Publisher View

**Purpose**: Interface for publishers to sign and publish templates

```xml
<UserControl xmlns="https://github.com/avaloniaui">
  <Grid RowDefinitions="Auto,*,Auto">
    <!-- Publisher Info -->
    <StackPanel Grid.Row="0">
      <TextBlock Text="Template Publisher" FontSize="20" FontWeight="Bold"/>
      <TextBlock Text="{Binding PublisherName}" Margin="0,5"/>
    </StackPanel>

    <!-- Template Info -->
    <StackPanel Grid.Row="1" Margin="0,20">
      <TextBlock Text="Template Information"/>
      <TextBox Text="{Binding Template.Metadata.TemplateId}" Header="Template ID"/>
      <TextBox Text="{Binding Template.Metadata.TemplateVersion}" Header="Version"/>
      <TextBox Text="{Binding Template.Metadata.Author}" Header="Author"/>

      <!-- Signature Status -->
      <Border BorderBrush="{Binding SignatureStatusColor}" BorderThickness="2" Padding="10" Margin="0,10">
        <StackPanel>
          <TextBlock Text="{Binding SignatureStatus}" FontWeight="Bold"/>
          <Button Content="Sign Template" Command="{Binding SignTemplateCommand}"
                  IsVisible="{Binding !IsSigned}"/>
        </StackPanel>
      </Border>

      <!-- S3 Configuration -->
      <Expander Header="S3 Gallery Configuration" IsExpanded="False">
        <Grid ColumnDefinitions="Auto,*" RowDefinitions="Auto,Auto,Auto,Auto">
          <TextBlock Grid.Row="0" Grid.Column="0" Text="Endpoint:"/>
          <TextBox Grid.Row="0" Grid.Column="1" Text="{Binding Config.ServiceUrl}"/>

          <TextBlock Grid.Row="1" Grid.Column="0" Text="Bucket:"/>
          <TextBox Grid.Row="1" Grid.Column="1" Text="{Binding Config.BucketName}"/>

          <TextBlock Grid.Row="2" Grid.Column="0" Text="Prefix:"/>
          <TextBox Grid.Row="2" Grid.Column="1" Text="{Binding PublishPrefix}"/>
        </Grid>
      </Expander>
    </StackPanel>

    <!-- Actions -->
    <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right">
      <Button Content="Validate" Command="{Binding ValidateCommand}"/>
      <Button Content="Publish to Gallery" Command="{Binding PublishCommand}"
              IsEnabled="{Binding CanPublish}" Classes="accent"/>
    </StackPanel>
  </Grid>
</UserControl>
```

### Template Gallery View

**Purpose**: Browse and download templates from S3 gallery

```xml
<UserControl xmlns="https://github.com/avaloniaui">
  <Grid RowDefinitions="Auto,Auto,*,Auto">
    <!-- Gallery Connection -->
    <StackPanel Grid.Row="0">
      <TextBlock Text="Template Gallery" FontSize="20" FontWeight="Bold"/>
      <TextBox Text="{Binding GalleryUrl}" Watermark="S3 Gallery URL"/>
      <Button Content="Connect to Gallery" Command="{Binding ConnectCommand}"/>
    </StackPanel>

    <!-- Search -->
    <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,10">
      <TextBox Text="{Binding SearchTerm}" Watermark="Search templates..."
               Width="300"/>
      <Button Content="Search" Command="{Binding SearchCommand}"/>
      <Button Content="Show All" Command="{Binding BrowseAllCommand}"/>
    </StackPanel>

    <!-- Template List -->
    <DataGrid Grid.Row="2" Items="{Binding Templates}"
              SelectedItem="{Binding SelectedTemplate}">
      <DataGrid.Columns>
        <DataGridTemplateColumn Header="Signed" Width="60">
          <DataGridTemplateColumn.CellTemplate>
            <DataTemplate>
              <TextBlock Text="✓" IsVisible="{Binding IsSigned}"
                         Foreground="Green" FontWeight="Bold"/>
            </DataTemplate>
          </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>
        <DataGridTextColumn Header="Template ID" Binding="{Binding TemplateId}" Width="200"/>
        <DataGridTextColumn Header="Author" Binding="{Binding Author}" Width="150"/>
        <DataGridTextColumn Header="Version" Binding="{Binding Version}" Width="80"/>
        <DataGridTextColumn Header="Modified" Binding="{Binding LastModified}" Width="150"/>
      </DataGrid.Columns>
    </DataGrid>

    <!-- Actions -->
    <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Right">
      <Button Content="Download & Verify" Command="{Binding DownloadAndVerifyCommand}"
              IsEnabled="{Binding SelectedTemplate, Converter={x:Static ObjectConverters.IsNotNull}}"/>
      <Button Content="Download" Command="{Binding DownloadCommand}"
              IsEnabled="{Binding SelectedTemplate, Converter={x:Static ObjectConverters.IsNotNull}}"/>
    </StackPanel>
  </Grid>
</UserControl>
```

---

## Testing with MinIO

### Setup Test Environment

#### 1. Start MinIO
```bash
./dev/minio-start.sh
./dev/minio-init.sh
```

#### 2. Create Template Gallery Bucket
```bash
./dev/minio/mc mb local/template-gallery
./dev/minio/mc mb local/template-gallery/templates
./dev/minio/mc mb local/template-gallery/templates/official
```

#### 3. Set Public Read Policy for Templates
```bash
# Allow public read on templates
./dev/minio/mc anonymous set download local/template-gallery/templates
```

### Publish Test Template

```bash
# In PromptResponse Desktop:
# 1. Create/open template
# 2. Sign template with test certificate
# 3. Open Template Publisher view
# 4. Configure S3:
#    - Endpoint: http://localhost:9000
#    - Bucket: template-gallery
#    - Prefix: templates/official/
#    - Access Key: promptresponse
#    - Secret Key: promptresponse123
# 5. Click "Publish to Gallery"
```

### Browse Gallery

```bash
# In PromptResponse Desktop:
# 1. Open Template Gallery view
# 2. Configure:
#    - Endpoint: http://localhost:9000
#    - Bucket: template-gallery
#    - Prefix: templates/
#    - Access Key: promptresponse (read-only could be different)
#    - Secret Key: promptresponse123
# 3. Click "Connect to Gallery"
# 4. Browse available templates
# 5. Select template and click "Download & Verify"
```

### Verify in MinIO Console

1. Open http://localhost:9001
2. Login: promptresponse / promptresponse123
3. Navigate to `template-gallery` bucket
4. View `templates/` folder
5. See published templates with metadata

---

## Security Considerations

### Template Signing is Required

- Templates MUST be digitally signed before publishing
- Publishing service validates signature presence
- Users SHOULD verify signatures before using templates
- Unsigned templates should show warning in gallery

### Access Control

**Publishers** need:
- Write access to template gallery bucket
- Valid digital signature certificate
- Strong AWS credentials (never commit to code!)

**Users** need:
- Read access to template gallery bucket (can be public)
- No credentials needed for public galleries
- Signature verification capability

### Best Practices

1. **Use Separate Buckets**:
   - `template-gallery` - Public read, publisher write
   - `form-submissions` - Private, pre-signed POST only

2. **Rotate Credentials**:
   - Publisher credentials should be rotated regularly
   - Use IAM roles when possible
   - Never hardcode production credentials

3. **Verify Everything**:
   - Always verify template signatures before use
   - Check template validity dates
   - Validate against expected publisher certificates

4. **Audit Trail**:
   - Log all publishing actions
   - Track template downloads
   - Monitor for suspicious activity

5. **HTTPS Only** (Production):
   - Use HTTPS endpoints for all S3 operations
   - Verify SSL certificates
   - Enable bucket encryption

---

## Future Enhancements

### Planned Features
- [ ] Template ratings and reviews
- [ ] Template usage statistics
- [ ] Automatic template updates (notify users of new versions)
- [ ] Template categories and tags
- [ ] Multi-region gallery replication
- [ ] CDN integration for faster downloads
- [ ] Template preview before download
- [ ] Batch template publishing
- [ ] Template deprecation workflow

### Potential Integrations
- [ ] GitHub integration (publish templates from repositories)
- [ ] Template validation service (automated quality checks)
- [ ] Community moderation (report/review templates)
- [ ] Template analytics (track usage, completion rates)

---

## Example: Complete Workflow

### Scenario: IRS Publishing W-4 Template

```
Day 1 - Publisher (IRS):
1. Create W-4 template in PromptResponse Desktop
2. Sign with IRS official certificate
3. Add submission config pointing to IRS form bucket
4. Open Template Publisher view
5. Configure S3 gallery: s3://irs-forms-gallery/templates/2025/
6. Publish: irs-w4-2025.aprt
7. Verify in gallery

Day 2 - Taxpayer:
1. Open PromptResponse Desktop
2. Go to Template Gallery
3. Connect to IRS gallery: s3://irs-forms-gallery/templates/2025/
4. Search: "W-4"
5. See: irs-w4-2025.aprt (Signed by IRS, v2025.1)
6. Download & Verify
7. See: "✓ Verified: Signed by Internal Revenue Service"
8. Accept and open template
9. Fill out W-4 form
10. Submit to IRS via embedded S3 pre-signed POST
11. Receive confirmation

Day 3 - IRS:
1. Open S3 Browser view
2. Connect to submission bucket
3. See submitted W-4 forms
4. Download and review submissions
5. Process forms
```

---

## Troubleshooting

### "Template must be signed" Error
**Problem**: Attempting to publish unsigned template
**Solution**: Sign template using Certificate Manager before publishing

### "Access Denied" on Publish
**Problem**: Insufficient S3 permissions
**Solution**: Verify IAM policy grants PutObject permission

### "Template not found" on Download
**Problem**: Incorrect bucket/key or permissions
**Solution**: Verify bucket name, prefix, and read permissions

### Signature Verification Fails
**Problem**: Template signature invalid
**Solution**: Template may be corrupted, re-download from gallery

---

## Documentation

Related documentation:
- [S3_SUBMISSION_IMPLEMENTATION.md](S3_SUBMISSION_IMPLEMENTATION.md) - S3 submission feature
- [MINIO_SETUP.md](MINIO_SETUP.md) - Local S3 testing with MinIO
- [FILE_FORMAT.md](FILE_FORMAT.md) - APR file format specification
- [ARCHITECTURE.md](ARCHITECTURE.md) - System architecture

---

## Questions?

For questions or issues:
1. Review this workflow guide
2. Check MinIO setup documentation
3. Verify S3 bucket policies
4. Review AWS SDK documentation
5. Open issue on GitHub
