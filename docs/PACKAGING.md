# Packaging & Distribution

PromptResponse ships as **self-contained, single-file** binaries — the end user
needs **no .NET runtime installed**. Both the GUI (`promptresponse`) and the CLI
(`apr`) are produced for each target.

## Build locally

```bash
# Linux x64 (default): produces dist/promptresponse-<version>-linux-x64{,.tar.gz}
scripts/publish.sh --version 0.3.0

# Other targets
scripts/publish.sh --rid win-x64   --version 0.3.0
scripts/publish.sh --rid osx-x64   --version 0.3.0   # macOS (see issue #41)
```

Each run publishes both projects (self-contained, `PublishSingleFile`,
`IncludeNativeLibrariesForSelfExtract`), stages them with `LICENSE`/`README`,
and produces a `.tar.gz` (Linux/macOS) or `.zip` (Windows). The Linux staging
also bundles `install-desktop.sh` for desktop integration.

> Verified: the published `apr` binary runs under a stripped environment
> (`env -i`, no .NET on `PATH`), confirming the self-contained guarantee.

## Releases (CI)

`.github/workflows/release.yml` builds and attaches artifacts to a GitHub
release when you push a `v*` tag (or via `workflow_dispatch`):

- **Linux** (`ubuntu-latest`): `promptresponse-<v>-linux-x64.tar.gz`
- **Windows** (`windows-latest`): a portable `.zip` **and** an Inno Setup
  installer `promptresponse-<v>-win-x64-setup.exe`

```bash
git tag v0.3.0 && git push origin v0.3.0   # triggers the release build
```

## File associations (`.apr` / `.aprt` / `.aprf`)

The app already accepts a file path on launch (`promptresponse <file>` or
`--open <file>`), so association just hands the path to the binary.

### Linux

From an extracted tarball:

```bash
./install-desktop.sh            # per-user: binaries, MIME types, desktop entry, default handler
./install-desktop.sh --uninstall
```

This registers `application/x-apr{,t,f}` (see `packaging/linux/promptresponse.xml`),
installs a desktop entry (`packaging/linux/promptresponse.desktop`), and sets
PromptResponse as the default handler — double-clicking an APR file opens it.
The `apr` CLI is linked into `~/.local/bin`.

### Windows

The Inno Setup installer (`packaging/windows/promptresponse.iss`) registers the
`PromptResponse.Document` ProgID and associates `.apr` / `.aprt` / `.aprf`
(optional task, on by default). Build it on Windows:

```powershell
dotnet publish src/PromptResponse.Desktop -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64
dotnet publish src/PromptResponse.Cli -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o publish\win-x64
iscc /DMyAppVersion=0.3.0 /DPublishDir=publish\win-x64 /DRepoRoot=. packaging\windows\promptresponse.iss
```

(The release workflow does this automatically on a Windows runner.)

## Notes & follow-ups

- **macOS** packaging (`.app` / notarization) is tracked as issue #41.
- The portable zip on Windows requires no install but does not register file
  associations — use the installer for that.
