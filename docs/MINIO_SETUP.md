# MinIO Local Development Setup

This guide explains how to set up and use MinIO for local S3 pre-signed POST testing with PromptResponse.

## Overview

**MinIO** is a high-performance, S3-compatible object storage server that we use for local development and testing of S3 pre-signed POST functionality. It runs as a standalone binary without requiring Docker.

### Why MinIO?

- ✅ Full S3 pre-signed POST support with policy validation
- ✅ No Docker required - runs as standalone binary
- ✅ Works in Claude Code Web and local development
- ✅ Lightweight and fast
- ✅ Web UI for debugging (http://localhost:9001)
- ✅ Production-ready S3 API compatibility
- ✅ Free and open source

### What We're Testing

The S3 pre-signed POST feature allows templates to include submission configuration that enables users to submit filled forms directly to S3 buckets without server-side code:

```
Template → Contains S3 policy → User fills form → Submit → Direct upload to S3
```

## Quick Start

### 1. Setup (One-Time)

Download MinIO server and client binaries:

```bash
./dev/minio-setup.sh
```

This will:
- Download MinIO server binary
- Download MinIO client (mc) binary
- Create necessary directories
- Verify installations

**Expected output:**
```
=========================================
MinIO Setup for PromptResponse
=========================================
OS: linux
Architecture: amd64

Downloading MinIO server...
✓ MinIO server downloaded
Downloading MinIO client (mc)...
✓ MinIO client downloaded

✓ MinIO setup complete!
```

### 2. Start MinIO

```bash
./dev/minio-start.sh
```

This will:
- Start MinIO server on ports 9000 (API) and 9001 (Console)
- Run in background
- Save PID to `dev/minio/minio.pid`
- Log output to `dev/minio/minio.log`

**Expected output:**
```
=========================================
Starting MinIO Server
=========================================
✓ MinIO is ready!

MinIO Console: http://localhost:9001
MinIO API:     http://localhost:9000
Username:      promptresponse
Password:      promptresponse123
```

### 3. Initialize Buckets

```bash
./dev/minio-init.sh
```

This will:
- Configure MinIO client
- Create test buckets (`filled-forms`, `templates`, `test-uploads`)
- Set bucket policies

**Expected output:**
```
=========================================
Initializing MinIO for PromptResponse
=========================================

Creating buckets...
  ✓ Created bucket 'filled-forms'
  ✓ Created bucket 'templates'
  ✓ Created bucket 'test-uploads'

✓ MinIO initialization complete!
```

### 4. Access MinIO Console

Open your browser to **http://localhost:9001**

- **Username:** `promptresponse`
- **Password:** `promptresponse123`

You can:
- Browse buckets and uploaded files
- View bucket policies
- Monitor uploads in real-time
- Debug S3 operations

### 5. Stop MinIO (When Done)

```bash
./dev/minio-stop.sh
```

## Daily Workflow

```bash
# Start MinIO
./dev/minio-start.sh

# ... do your development work ...

# Stop MinIO when done
./dev/minio-stop.sh
```

## Testing S3 Pre-Signed POST

### Using AWS SDK with MinIO

When generating pre-signed POST policies for testing, configure your S3 client to use MinIO:

**C# Example (AWS SDK for .NET):**

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;

var config = new AmazonS3Config
{
    ServiceURL = "http://localhost:9000",
    ForcePathStyle = true,  // Required for MinIO
    SignatureVersion = "4",
    AuthenticationRegion = "us-east-1"
};

var credentials = new BasicAWSCredentials(
    "promptresponse",
    "promptresponse123"
);

using var s3Client = new AmazonS3Client(credentials, config);

// Generate pre-signed POST policy
var request = new PostPolicyRequest
{
    BucketName = "filled-forms",
    Key = "test-form.aprf",
    Expiration = DateTime.UtcNow.AddDays(7)
};

// Add policy conditions
request.Conditions.Add(new PostPolicyCondition("acl", "private"));
request.Conditions.Add(new PostPolicyCondition("content-type", "application/json"));

var postPolicy = s3Client.GetPreSignedURL(request);
```

**Key Configuration Requirements:**

1. **ServiceURL:** `http://localhost:9000` (MinIO endpoint)
2. **ForcePathStyle:** `true` (use path-style URLs, not virtual-hosted)
3. **Region:** `us-east-1` (MinIO default)
4. **Credentials:** `promptresponse` / `promptresponse123`

### Testing Upload

**Using curl:**

```bash
# Example pre-signed POST
curl -X POST http://localhost:9000/filled-forms \
  -F "key=test-form.aprf" \
  -F "policy=<base64-encoded-policy>" \
  -F "signature=<signature>" \
  -F "AWSAccessKeyId=promptresponse" \
  -F "file=@/path/to/filled-form.aprf"
```

**Using PromptResponse Desktop/CLI:**

Once S3 submission is implemented, the app will read `submissionConfig` from the template and POST directly to MinIO:

```json
{
  "submissionConfig": {
    "type": "s3-presigned-post",
    "url": "http://localhost:9000/",
    "fields": {
      "key": "filled-forms/${filename}",
      "AWSAccessKeyId": "promptresponse",
      "policy": "...",
      "signature": "...",
      "acl": "private"
    },
    "expiresAt": "2025-12-31T12:00:00Z"
  }
}
```

## Available Buckets

### `filled-forms` (Production-Like)
- **Purpose:** Test filled form submissions
- **Policy:** Private (requires authentication)
- **Use for:** Testing authenticated uploads with pre-signed POST

### `templates` (Production-Like)
- **Purpose:** Store template files
- **Policy:** Private (requires authentication)
- **Use for:** Testing template distribution

### `test-uploads` (Development)
- **Purpose:** Quick testing and debugging
- **Policy:** Public download (anyone can read)
- **Use for:** Rapid testing without policy constraints

## Troubleshooting

### MinIO won't start

**Check if port is already in use:**
```bash
lsof -i :9000
lsof -i :9001
```

**Check logs:**
```bash
cat dev/minio/minio.log
```

### Can't connect to MinIO

**Verify MinIO is running:**
```bash
curl http://localhost:9000/minio/health/live
```

**Expected:** `200 OK`

### Pre-signed POST returns SignatureDoesNotMatch

**Common causes:**
1. ❌ Not using `ForcePathStyle = true`
2. ❌ Wrong region (use `us-east-1`)
3. ❌ Incorrect credentials
4. ❌ Clock skew (system time incorrect)

**Fix:**
```csharp
var config = new AmazonS3Config
{
    ServiceURL = "http://localhost:9000",
    ForcePathStyle = true,           // ← Add this
    AuthenticationRegion = "us-east-1"  // ← Add this
};
```

### CORS errors in browser

MinIO requires CORS configuration for browser uploads. Set CORS rules:

```bash
./dev/minio/mc anonymous set-json dev/minio/cors-policy.json local/filled-forms
```

**cors-policy.json:**
```json
{
  "CORSRules": [{
    "AllowedOrigins": ["*"],
    "AllowedMethods": ["GET", "POST"],
    "AllowedHeaders": ["*"]
  }]
}
```

## File Structure

```
dev/
├── minio-setup.sh         # One-time setup (download binaries)
├── minio-start.sh         # Start MinIO server
├── minio-stop.sh          # Stop MinIO server
├── minio-init.sh          # Initialize buckets
└── minio/                 # Created by setup script
    ├── minio              # MinIO server binary
    ├── mc                 # MinIO client binary
    ├── data/              # S3 object storage
    ├── minio.pid          # Process ID file
    └── minio.log          # Server logs
```

## Environment Variables

The start script sets these environment variables:

- `MINIO_ROOT_USER=promptresponse`
- `MINIO_ROOT_PASSWORD=promptresponse123`
- `MINIO_BROWSER_REDIRECT_URL=http://localhost:9001`

**Note:** These are development credentials. Never use in production!

## Comparison with Real AWS S3

| Feature | MinIO (Local) | AWS S3 (Real) |
|---------|--------------|---------------|
| Pre-signed POST | ✅ Supported | ✅ Supported |
| Policy validation | ✅ Full support | ✅ Full support |
| Expiration checking | ✅ Yes | ✅ Yes |
| CORS support | ✅ Yes | ✅ Yes |
| Endpoint | localhost:9000 | s3.amazonaws.com |
| Cost | Free | Pay per request/storage |
| Network latency | ~1ms | ~50-200ms |
| API compatibility | ~99% | 100% |

### Known Differences

1. **Virtual-hosted URLs:** MinIO prefers path-style URLs (use `ForcePathStyle=true`)
2. **Region:** MinIO defaults to `us-east-1`
3. **SSL/TLS:** MinIO runs HTTP by default (HTTPS requires certificate setup)

These differences don't affect pre-signed POST testing.

## Security Notes

### Development Only

This setup is for **local development and testing only**:

- ⚠️ Uses weak credentials (`promptresponse123`)
- ⚠️ Runs on HTTP (not HTTPS)
- ⚠️ Accessible from localhost only
- ⚠️ No authentication on test bucket

### For Production

When implementing real S3 submission:
- ✅ Use real AWS credentials with IAM policies
- ✅ Use HTTPS endpoints only
- ✅ Implement policy expiration (7 days max)
- ✅ Restrict file sizes and content types
- ✅ Use private bucket policies
- ✅ Enable bucket encryption
- ✅ Monitor uploads with CloudWatch/CloudTrail

## Integration with PromptResponse

### Phase 1: Template Format (Current)

Templates can include `submissionConfig` in metadata:

```json
{
  "version": "1.0",
  "documentType": "template",
  "metadata": {
    "title": "Employment Application",
    "submissionConfig": {
      "type": "s3-presigned-post",
      "url": "http://localhost:9000/",
      "fields": {
        "key": "filled-forms/${filename}",
        "AWSAccessKeyId": "promptresponse",
        "policy": "...",
        "signature": "..."
      },
      "expiresAt": "2025-12-31T12:00:00Z"
    }
  }
}
```

### Phase 2: Desktop Implementation (Future)

Desktop app will:
1. Read `submissionConfig` from template
2. Check policy expiration
3. Show "Submit to S3" button (in addition to Save)
4. POST filled form to S3 endpoint
5. Display success/error feedback

### Phase 3: CLI Implementation (Future)

```bash
# Submit filled form to S3
dotnet run --project src/PromptResponse.Cli -- submit filled-form.aprf

# Or use S3 config from template
dotnet run --project src/PromptResponse.Cli -- submit filled-form.aprf --use-template-config
```

## Additional Resources

- [MinIO Documentation](https://min.io/docs/minio/linux/index.html)
- [MinIO Client (mc) Guide](https://min.io/docs/minio/linux/reference/minio-mc.html)
- [AWS S3 Pre-signed POST](https://docs.aws.amazon.com/AmazonS3/latest/userguide/PresignedUrlUploadObject.html)
- [PromptResponse Architecture](./ARCHITECTURE.md#form-submission-architecture)
- [APR File Format](./FILE_FORMAT.md)

## Getting Help

If you encounter issues:

1. Check MinIO logs: `cat dev/minio/minio.log`
2. Verify MinIO is running: `curl http://localhost:9000/minio/health/live`
3. Check bucket list: `./dev/minio/mc ls local`
4. Open MinIO Console: http://localhost:9001

For S3 pre-signed POST questions, see the AWS SDK documentation for your language.
