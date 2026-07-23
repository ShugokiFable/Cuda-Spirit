# Build status — Cuda Spirit 2.4.1

## Source status

The complete source passes the bundled structural, XAML, resource, navigation, SQLite, prior compiler-regression, Obsidian geometry, and packaging validators.

## Windows compilation

The application is a Windows WPF project. `publish.bat` and the GitHub Actions workflow perform the authoritative Windows compile with warnings treated as errors and require a real main window during the smoke test.

## SDK behavior

Local builds use any stable compatible .NET 8 or newer SDK already installed. The Nexus source-bootstrapper downloads the private .NET 8.0.423 SDK only when no compatible system or adjacent private SDK exists. A compiled self-contained Nexus release created by `BUILD_NEXUS_RELEASE.bat` requires no .NET runtime or SDK for end users.


## V2.4.1 overlay hotfix

Transparent overlay icons now use vector paths instead of font glyphs or emoji, with device-pixel rounding and layered-window-safe grayscale rendering.
