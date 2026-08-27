# Local NuGet feed

This folder is a local NuGet package source (registered in `../nuget.config` as
`pdfe-local`). It vendors the [excise](https://github.com/marctjones/excise) PDF
engine as a packed `.nupkg` so PromptResponse can consume it **without a
cross-repo `ProjectReference`** — the two repositories stay decoupled and the
PromptResponse build is reproducible without the pdfe source checked out.

## Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Excise.Core` | 3.8.0 | Pure-managed PDF authoring engine (MIT). Used by `PromptResponse.Rendering.Pdf` for PDF export. |

## Refreshing after a pdfe change

When pdfe ships a new version you want to pick up:

```bash
# 1. Pack the new version from your local pdfe checkout
dotnet pack ~/Projects/pdfe/Pdfe.Core/Pdfe.Core.csproj -c Release -o /tmp/pdfe-pack

# 2. Copy the .nupkg here
cp /tmp/pdfe-pack/Pdfe.Core.<version>.nupkg local-nuget/

# 3. Bump the <PackageReference Version="..."> in
#    src/PromptResponse.Rendering.Pdf/PromptResponse.Rendering.Pdf.csproj
#    and remove the old .nupkg from this folder.

# 4. Restore
dotnet restore
```

> Once pdfe publishes `Pdfe.Core` to nuget.org (tracked upstream in
> marctjones/pdfe#383), this local feed can be dropped in favor of the public
> package.

## Upgrading

The engine was renamed `pdfe` → `Excise` at 3.0.0; the changelog records the
rename as the whole of the breaking change, with the engine byte-for-byte
identical underneath. Upgrading from `Pdfe.Core` 2.9.0 was therefore mechanical:
swap the package id and the `Pdfe.*` namespaces for `Excise.*`.

To vendor a new release, pack from a tag rather than a branch so the build stays
reproducible:

```bash
git -C ../excise worktree add --detach /tmp/excise-tag vX.Y.Z
dotnet pack /tmp/excise-tag/Excise.Core/Excise.Core.csproj \
    -c Release -p:Version=X.Y.Z -o local-nuget/
git -C ../excise worktree remove /tmp/excise-tag
```

The csproj on `develop` does not carry the release version, so `-p:Version` is
required and must match the tag.

Note that 3.x pulls in a JPEG 2000 codec (CSJ2K, BSD) and its two Microsoft
transitive dependencies. All three are disclosed in the About dialog, which
`AboutDialogAcknowledgementsTests` enforces — that test is what caught them.
