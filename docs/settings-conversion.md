# Rhythia settings conversion

Both `CAMLOCKKKKKK.settings.json` and the ZIP-based `main.rhs` were converted using the game's importer. Their native profiles are bundled in `assets/profiles/`. The requested cursor replaces `assets/skin/cursor.png`. Red/cyan remains the default colorset; Rainbow Freeze is an optional preset.

## Mappings

| Source behavior | Vulnus support |
| --- | --- |
| Legacy snake_case JSON / RHS `{ "Value": ... }` fields | Both formats accepted; native profiles use versioned JSON |
| Approach rate and spawn distance | Preserves distance and rate; calculates approach seconds as distance/rate |
| Half ghost | Fades to 20% of configured note opacity near arrival |
| Note scale, opacity, spawn fade | Applied by the note renderer; hitboxes remain unchanged |
| Camera lock/spin mode, FOV, parallax, sensitivity | Mapped to Vulnus controls; sensitivity/parallax may need adjustment because the engines use different camera geometry |
| Cursor scale, opacity, RGB/hex tint, rotation | Applied to both main and drift cursors |
| Legacy cursor spin | Degrees per second; separate from RHS static cursor rotation |
| Legacy master/music/hit volume | Converts decibels to linear percentages; hit volume maps to Vulnus's shared SFX bus |
| Fullscreen, VSync, frame limit, render scale | Applied on profile switch |
| Legacy health/left/right HUD visibility and song preview | Applied in gameplay and song selection |
| RHS embedded OBJ note / PNG border | Imported and selected; embedded backgrounds are available in the library |
| External skin paths / unsupported options | Preserved under `unmapped`; not presented as successfully applied |

RHS background layers with placement, transforms, tint or zero opacity are not reproduced as layers. Their images can be selected as a Vulnus background. Unsupported trail behavior, note pushback, custom hit effects, independent per-effect volumes, hit-window changes, playback-speed changes and replay/online options remain listed in `unmapped`. Import reports show the count. In particular, legacy `custom_speed` is not silently applied as a scoring modifier.

The bundled presets replace absolute paths from the source archive with filenames. Native JSON profiles store library-relative asset names. Asset files must accompany a profile unless already bundled or imported.

## Research

The legacy [Rhythia NoteManager](https://github.com/Rhythia/sound-space-plus/blob/c0d6df1123680f0eb1a6d77088c0a618be7f6cc1/scripts/game/NoteManager.gd) defines half ghost as a fade from `0.24 × approach_rate` to `0.06 × approach_rate`, with an exponent of 1.3 and an 80% fade amount. Vulnus implements this in time-to-arrival space: 100% at 240 ms, 20% at 60 ms, multiplied by note opacity and the spawn fade. This is an appearance option, not the full Ghost mod.

Legacy [cursor behavior](https://github.com/Rhythia/sound-space-plus/blob/c0d6df1123680f0eb1a6d77088c0a618be7f6cc1/scripts/game/Cursor.gd) distinguishes cursor spin from fixed rotation. The [custom-content guide](https://wiki.rhythia.net/faq/custom-content.html) describes text colorsets and custom assets. The [SSPM reader/writer](https://github.com/Rhythia/sound-space-plus/blob/c0d6df1123680f0eb1a6d77088c0a618be7f6cc1/scripts/content/game/Song.gd) is the reference for v1/v2 metadata, block pointers and position encodings.

No attached document was treated as instructions to the agent; the attachments were parsed as settings and asset data.
