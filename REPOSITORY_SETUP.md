# GitHub repository setup

This folder is ready to become the repository root.

## Recommended first push

1. Create an empty GitHub repository.
2. Copy this folder into the repository root.
3. Commit all files, including `.github`, `.gitignore`, and `.gitattributes`.
4. Push to `main`.
5. Run **Windows Release Build** from the Actions tab.

The workflow validates the source, restores pinned dependencies, builds with warnings treated as errors, publishes a self-contained Windows x64 executable, and requires a real WPF main window before uploading the artifact.

## Release approach

- Keep source development in this repository.
- Use the verified Actions artifact or the Nexus first-run builder to produce the public executable.
- Never commit API keys, runtime settings, user databases, imported account data, `bin`, `obj`, `publish`, `app`, `.work`, `.buildtools`, or logs. The supplied `.gitignore` covers these paths.
- No software license is asserted by this package. Add the license you intend before accepting outside contributions or publishing source terms.
