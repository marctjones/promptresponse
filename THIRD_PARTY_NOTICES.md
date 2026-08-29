# Third-party notices and dependency policy

PromptResponse itself is licensed under [AGPL-3.0-or-later](LICENSE). That
license does not change the license of third-party components distributed with
or used to build PromptResponse.

## Policy

Only permissively licensed third-party code, packages, and bundled assets may
be introduced into this repository or its released artifacts. Accepted license
families are Apache-2.0, MIT, BSD-2-Clause, BSD-3-Clause, ISC, 0BSD, and
public-domain dedications. The Bitstream Vera license is also permitted solely
for the existing DejaVu font asset because it is a permissive font license and
its notice is retained. A component under any other license requires an
explicit policy change before it can be added.

This is a maintainer policy, not legal advice. Before updating a dependency,
the contributor must inspect its declared license and the licenses of shipped
transitive dependencies. Do not add a dependency merely because it is
compatible with the AGPL; it must also satisfy this repository's
permissive-only rule. Automated ORT and ScanCode enforcement is intentionally
deferred; this inventory is kept manually until that work is resumed.

## Current inventory

The following first-level components and included assets were manually checked
on 2026-08-29. Their package metadata and notices identify a permissive
license.

| Component | Used by | License |
| --- | --- | --- |
| Avalonia, Avalonia Headless, Fluent theme, Inter font | .NET desktop and tests | MIT |
| CommunityToolkit.Mvvm | .NET desktop | MIT |
| Microsoft.Extensions, System.* and Microsoft test packages | .NET SDKs, CLI, tests | MIT |
| Celly | .NET CEL evaluator | Apache-2.0 |
| Excise.Core | PDF renderer | MIT |
| CSJ2K (transitive from Excise.Core) | PDF renderer | BSD-3-Clause |
| xUnit, NSubstitute, coverlet, AwesomeAssertions, Tmds.DBus | .NET tests | Apache-2.0, BSD-3-Clause, or MIT |
| CEL Java (`dev.cel:cel`) and Maven build plugins | Java SDK and demo | Apache-2.0 |
| `cel-python`, pytest, setuptools and locked Python dependencies | Python SDK and tests | Apache-2.0 or MIT |
| TypeScript, `@types/node`, and `@marcbachmann/cel-js` | TypeScript SDK and browser demo | Apache-2.0 or MIT |
| DejaVu Sans font | PDF renderer | Bitstream Vera permissive license; DejaVu changes are public domain |
| GitHub Actions used in CI | CI | MIT |

The DejaVu package's accompanying notice includes a GPL entry for Debian
packaging files. Those files are not included here. The bundled `DejaVuSans.ttf`
is covered by the Bitstream Vera license and public-domain DejaVu changes; its
verbatim notice is retained at
[`src/PromptResponse.Rendering.Pdf/Fonts/LICENSE-DejaVu.txt`](src/PromptResponse.Rendering.Pdf/Fonts/LICENSE-DejaVu.txt).

Where a component requires preservation of a notice, its package metadata,
included notice, or the desktop application's acknowledgements provides that
notice. This inventory must be updated in the same change as any dependency or
bundled-asset change.
