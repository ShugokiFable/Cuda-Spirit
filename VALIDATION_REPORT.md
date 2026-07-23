# Cuda Spirit 2.4.1 validation report

## Overlay icon regression fix

The transparent HUD and Advisor overlays now use vector geometry rather than Segoe MDL2 font glyphs or color emoji. Layered windows use layout rounding, device-pixel snapping, and grayscale text rendering to avoid missing boxes, substituted symbols, and DPI distortion.

## Automated source validation

```text
PASS: Parsed 27 XAML/project files.
PASS: Parsed 2 JSON files.
PASS: Checked XAML classes and event wiring.
PASS: Checked 729 resource references against 79 declared keys.
PASS: Verified 2.4.1 release metadata and 19 shell navigation destinations.
PASS: Checked the Pearl Shop CS0165 definite-assignment regression.
PASS: Checked all prior Windows compiler regressions: Thickness arity and System.IO resolution.
PASS: Checked Obsidian UI geometry: no decorative ellipses, no radii above 4, and crisp shell contracts present.
PASS: Checked layered-overlay vector icons, DPI rounding, grayscale rendering, and emoji-free status text.
PASS: Checked compatible-SDK policy and self-contained Nexus release builder.
PASS: Lexically checked 83 C# files.
PASS: Executed 2 SQLite schema/FTS blocks and smoke-tested companion tables.
PASS: validation completed with no detected errors.
```

Exit code: `0`

## Native build boundary

The source changes are XAML/vector and text-rendering changes. Native Windows WPF compilation remains authoritative and is performed by the Nexus installer or GitHub Actions workflow with warnings treated as errors and a visible-window smoke test.
