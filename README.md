# Vulnus

This is the **Godot 3 Mono/C# source project**. Open `project.godot` in a Godot 3 Mono editor (tested with 3.5.3), allow asset import to finish, and build the C# solution before running. Godot 4 and the non-Mono Godot editor cannot run this source as-is.

The previously committed `Vulnus.exe` was a Unity/IL2CPP build. It was not built from these Godot scripts. That executable, its Unity runtime/data, BepInEx injection files, Unity editor configuration, logs, and personal Unity `settings.json` have been removed. The OpenXR native libraries under `addons/` are required Godot dependencies and remain tracked.

## Maps and settings

- In the editor, maps are read from the repository's `maps/` folder and Godot's `user://maps` directory.
- In an exported desktop build, put a `maps/` directory alongside the executable. `user://maps` is also scanned.
- A map folder contains `meta.json`, one or more difficulty JSON files, audio, and an optional cover. `.vul` archives and `.sspm` imports in the user maps directory are supported.
- Settings are generated as `user://settings.bin`; on Windows this is normally `%APPDATA%\Vulnus\settings.bin`. The old Unity `settings.json` does not configure this project.
- Damaged settings fall back to defaults. Unreadable map archives and difficulty files are logged and skipped. An invalid selected difficulty or missing audio disables Play and displays an explanation.

## Controls

Mouse movement controls the cursor. Space skips long gaps. Escape or Ctrl+R ends a run and opens results. Ctrl+O opens settings in menus. F11 or Alt+Enter toggles fullscreen. The `+` button next to Play opens the available No Fail modifier.

Normal play ends at zero health; No Fail allows play to continue and marks the failed run unqualified. Retry resets the run; Return goes back to map selection.

## Build and export

1. Import the project with Godot **3 Mono** and build its C# solution using the editor's Build button. A compatible .NET SDK/MSBuild is required; the project targets .NET Framework 4.7.2.
2. Install the matching **Mono export templates** and add a Windows Desktop export preset.
3. Export into `build/` or another ignored output directory. Keep the generated executable, pack, and Mono runtime files together.
4. Copy the source `maps/` folder alongside the exported executable. Settings are created on first run in the user-data directory.

An exported EXE was not generated as part of this repair.

## Regression checks

After importing and building, run `tests/SmokeTest.tscn` with a temporary `override.cfg` containing:

```ini
[application]
config/custom_user_dir_name="VulnusSmokeTest-Local"
```

Launch the Mono engine with `--path . res://tests/SmokeTest.tscn --smoke-test`. Remove the temporary override afterwards. Do not overwrite an existing override. The test refuses to run without an isolated user-data directory containing `VulnusSmokeTest` because it creates damaged settings and archive fixtures there. The `--smoke-test` argument suppresses RhythKit event output.

The checks cover the actual Godot scenes: startup recovery, bundled map/audio parsing, search/sort, settings controls and persistence, rapid selection, modifiers, gameplay start, gap skipping, late misses/on-time hits, failure, results, Retry and Return. Screenshots are written into the isolated user-data directory.

Account login, online downloads, playlists, grouping and numeric difficulty sorting remain unfinished upstream features. Grouping and unavailable difficulty sorts are disabled. This validation covers desktop play; VR, Android, and exported builds still need device testing. Discord rich presence is unavailable because the repository did not include its referenced C# wrapper; those unresolved calls were removed so gameplay can build and run.
