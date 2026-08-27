# Local NuGet feed

This folder is a local NuGet package source (registered in `../nuget.config` as
`pdfe-local`). It vendors the [pdfe](https://github.com/marctjones/pdfe) PDF
engine as a packed `.nupkg` so PromptResponse can consume it **without a
cross-repo `ProjectReference`** — the two repositories stay decoupled and the
PromptResponse build is reproducible without the pdfe source checked out.

## Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Pdfe.Core` | 2.9.0 | Pure-managed PDF authoring engine (MIT). Used by `PromptResponse.Rendering.Pdf` for PDF export. |

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
