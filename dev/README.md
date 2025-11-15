# Development Tools

This directory contains development tools and scripts for PromptResponse.

## MinIO Local S3 Testing

Scripts for running MinIO (S3-compatible storage) locally to test S3 pre-signed POST functionality.

### Quick Start

```bash
# One-time setup
./dev/minio-setup.sh

# Start MinIO
./dev/minio-start.sh

# Initialize buckets (first time only)
./dev/minio-init.sh

# ... do your development work ...

# Stop MinIO
./dev/minio-stop.sh
```

### MinIO Console

Once started, access the web UI at: **http://localhost:9001**

- **Username:** `promptresponse`
- **Password:** `promptresponse123`

### Available Scripts

| Script | Description |
|--------|-------------|
| `minio-setup.sh` | Download MinIO server and client binaries (one-time) |
| `minio-start.sh` | Start MinIO server in background |
| `minio-stop.sh` | Stop MinIO server |
| `minio-init.sh` | Create test buckets and configure policies |

### Documentation

See [docs/MINIO_SETUP.md](../docs/MINIO_SETUP.md) for comprehensive documentation including:
- Detailed setup instructions
- Testing S3 pre-signed POST
- Troubleshooting
- Integration with PromptResponse

### File Structure

```
dev/
├── README.md              # This file
├── .gitignore             # Excludes MinIO runtime files
├── minio-setup.sh         # Setup script
├── minio-start.sh         # Start script
├── minio-stop.sh          # Stop script
├── minio-init.sh          # Initialization script
└── minio/                 # Created by setup script (gitignored)
    ├── minio              # MinIO server binary
    ├── mc                 # MinIO client binary
    ├── data/              # S3 object storage
    ├── minio.pid          # Process ID file
    └── minio.log          # Server logs
```

## Why MinIO?

We use MinIO instead of Docker-based solutions because:

- ✅ Works in Claude Code Web (no Docker available)
- ✅ Works in local development environments
- ✅ Single binary, easy to set up
- ✅ Full S3 pre-signed POST support with policy validation
- ✅ Lightweight and fast
- ✅ Great web UI for debugging

## Future Tools

This directory will contain other development tools as needed:

- Test data generators
- Development database scripts
- Code generation tools
- Build helpers
