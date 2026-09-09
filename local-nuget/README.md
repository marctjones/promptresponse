# Local NuGet feed

This folder is a local NuGet package source (registered in `../nuget.config` as
`pdfe-local`). It vendors the [excise](https://github.com/marctjones/excise) PDF
engine as a packed `.nupkg` so PromptResponse can consume it **without a
cross-repo `ProjectReference`** — the two repositories stay decoupled and the
PromptResponse build is reproducible without the pdfe source checked out.

## Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Excise.Core` | 3.9.4 | Pure-managed PDF engine (MIT). Used by `PromptResponse.Rendering.Pdf` for PDF export and AcroForm import. |

## Refreshing after a pdfe change

When pdfe ships a new version you want to pick up:

Pack from a **tag**, not a branch, so the build stays reproducible — see
"Upgrading" below for why that matters. Replace `X.Y.Z` throughout:

```bash
# 1. Pack the tagged version from an isolated worktree of the excise checkout
git -C ../excise worktree add --detach /tmp/excise-pack vX.Y.Z
dotnet pack /tmp/excise-pack/Excise.Core/Excise.Core.csproj \
    -c Release -p:Version=X.Y.Z -o local-nuget/
git -C ../excise worktree remove /tmp/excise-pack --force

# 2. Drop the old package and bump the two PackageReference versions
rm local-nuget/Excise.Core.<old>.nupkg local-nuget/Excise.Core.<old>.snupkg
#    src/PromptResponse.Rendering.Pdf/PromptResponse.Rendering.Pdf.csproj
#    tests/PromptResponse.Rendering.Pdf.Tests/PromptResponse.Rendering.Pdf.Tests.csproj

# 3. Restore the whole solution — eight projects reach Excise.Core
#    transitively, and their packages.lock.json files all need refreshing.
dotnet restore PromptResponse.sln
```

The csproj on `develop` does not carry the release version, so `-p:Version`
is required and must match the tag.

⚠️ **Verify the tag points where you think.** The `v3.9.2` tag was once cut
from a lineage that was not an ancestor of `develop`, and `v3.9.3` was later
re-pointed to a different commit. Before trusting a build, check
`git merge-base --is-ancestor vX.Y.Z origin/develop` and confirm the commit
the tag resolves to is the one you meant to package.

> Once pdfe publishes `Pdfe.Core` to nuget.org (tracked upstream in
> marctjones/pdfe#383), this local feed can be dropped in favor of the public
> package.

## History

The engine was renamed `pdfe` → `Excise` at 3.0.0; the changelog records the
rename as the whole of the breaking change, with the engine byte-for-byte
identical underneath. Upgrading from `Pdfe.Core` 2.9.0 was therefore mechanical:
swap the package id and the `Pdfe.*` namespaces for `Excise.*`.

Note that 3.x pulls in a JPEG 2000 codec (CSJ2K, BSD) and its two Microsoft
transitive dependencies. All three are disclosed in the About dialog, which
`AboutDialogAcknowledgementsTests` enforces — that test is what caught them.
