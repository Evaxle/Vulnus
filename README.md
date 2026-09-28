# Vulnus

Vulnus is an aim rhythm game built with **Godot 3.6 Mono and C#**. Move the cursor through notes as they reach the playfield. This repository contains the Godot source project; the old Unity binaries and loose personal maps have been removed.

## Play and customize

- **Import maps:** drop one or more `.sspm` or `.vul` files onto the window, or choose **Play → Import maps**. SSPM v1/v2 maps convert automatically, retaining notes, embedded audio, cover, mapper information and duration. Drops during gameplay wait until you return to the menu.
- **Settings:** open Settings from the menu or press **Ctrl+O**. The menu has scrollable Profiles, Gameplay, Appearance, Audio and Display tabs.
- **Profiles:** choose Default, main, CAMLOCKKKKKK or an imported profile. Create a named copy before experimenting. Changes save automatically. Import native Vulnus JSON, legacy Rhythia `.settings.json`, or `.rhs` archives. The profiles folder contains portable `.json` files for export.
- **Cursors and assets:** import multiple PNG/JPEG cursors, borders and backgrounds; OBJ note meshes; or OGG/MP3/PCM WAV hit and miss sounds. Select them from the lists. RHS archives also import embedded border, note and background assets. OBJ materials are intentionally not loaded; notes use the selected colorset.
- **Colors:** red/cyan is the default. Rainbow Freeze is bundled. Import a text file containing comma-separated six-digit hex colors, optionally prefixed with `#`.
- **Appearance:** half ghost, spawn fade, note size/opacity, cursor size/opacity/tint/rotation/spin, field of view, camera parallax and HUD visibility are configurable. Note size is visual and does not change scoring hitboxes.

User content lives under `%APPDATA%/Vulnus` on Windows, outside the checkout. Profiles remember asset names; share the corresponding assets with a profile when moving to another installation. The bundled `main` profile's note and border assets are included.

## Build

1. Install **Godot 3.6.2 Mono** (not Godot 4 or the non-Mono editor) and a .NET SDK that can build `net472`.
2. Import `project.godot` in the Mono editor and let it import the assets and generate its C# API references.
3. Build the C# solution, then run the project. From a terminal after editor initialization: `dotnet build Vulnus.csproj`.
4. To export, install the matching **Mono export templates** in Godot. The Windows preset includes the raw profile/library files needed on first launch. Generated executables belong in `build/`, not in source control.

The incomplete Discord wrappers referenced by the original source were never included. Their calls were removed so a clean checkout builds; RhythKit integration remains.

## Compatibility and testing

See [settings conversion](docs/settings-conversion.md) for supported mappings, source references and limitations, and [engine checks](tests/README.md) for repeatable import/gameplay checks.

Unsupported or malformed maps fail with a visible message. Maps requiring Rhythia mods or lacking embedded audio cannot be played through the SSPM importer. Imported archives are validated before entering the library. Reimporting identical content does not add a duplicate song.

For an isolated installation or tests, pass `--vulnus-user-dir <absolute-folder>` after the engine's normal arguments. The existing `--rhythkit-convert-sspm <file>` conversion entry point is retained.
