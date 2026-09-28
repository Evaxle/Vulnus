# Engine checks

The test suite runs inside Godot Mono, using the real importer, renderer, profile store and scene tree. It checks native and foreign profile imports, persistence, image/OBJ/colorset loading, half ghost fade endpoints, SSPM v1/v2 coordinates and metadata, WAV decoding, duplicate imports, malformed input, archive traversal, file-drop routing, song-list refresh and transitions into/out of gameplay.

1. Build the project and let the Godot editor generate its import cache and C# metadata.
2. Generate fixtures outside the repository:

   ```text
   python tests/generate_fixtures.py ../fixtures
   ```

   Existing fixture files take precedence. For verification against your original files, put `main.rhs`, `CAMLOCKKKKKK.settings.json`, `cursor.png`, and `Rainbow Freeze.txt` in that folder before generating. An optional `real.sspm` adds a real-map check. The generator otherwise creates small synthetic settings and maps; no copyrighted map/audio fixture is committed.

3. Run Godot with a **fresh, disposable user directory**:

   ```text
   Godot_mono.exe --path . --no-window --audio-driver Dummy --vulnus-user-dir C:/temp/vulnus-checks --vulnus-test C:/temp/fixtures
   ```

   Substitute your absolute paths. `--no-window` is a Windows option. Success exits 0 and prints `VULNUS CHECKS PASSED`. A deliberately truncated SSPM produces one expected importer error. The fixture folder receives `settings.png`, `gameplay.png`, and the converted profiles.

Local verification: Godot 3.6.2 Mono on Windows, .NET SDK 8 building net472. The original two attached settings files, cursor and colorset, generated v1/v2 maps and the repository's former SSPM sample were exercised. The game received Godot's actual `files_dropped` signal; a physical Explorer drag gesture was not automated.
