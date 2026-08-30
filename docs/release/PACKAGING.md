# Packaging and distribution

PromptResponse ships self-contained desktop and CLI artifacts for Linux x64, Windows x64, and macOS arm64/x64. The release workflow builds them from a `v*` tag or dispatch and attaches them to the matching GitHub release.

Run `scripts/publish.sh --rid <rid> --version <version>` to stage a local artifact, then run `scripts/release-smoke.sh` against the staged artifact rather than developer output. Linux provides desktop integration; Windows provides an installer and portable zip; current macOS artifacts are unsigned tarballs pending a notarized app bundle.
