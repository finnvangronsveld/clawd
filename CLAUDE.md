# Clawd: notes for Claude

Clawd is a pixel-art desktop pet for Windows: a little orange guy who lives on the desktop. **V2** is a C# Windows Forms app (`src/*.cs` → `Clawd.exe`), built with the .NET Framework 4.x compiler that ships with every Windows install. Public repo: https://github.com/finnvangronsveld/clawd

**Keep this file and `README.md` up to date whenever behaviour changes.** The owner asked for this explicitly.

## Layout

| Path | What |
|---|---|
| `src/Program.cs` | Entry point: single instance (mutex + poke event), the fixed 60 Hz loop, shortcuts, the settings window, test switches, `--render-frames`. |
| `src/Native.cs` | All P/Invoke: layered windows, window queries (`Usable`, `FindTop`, `FindClimbTarget`), input (idle time, key-press **counts**), DPI, monitors, auto-hide taskbar, CPU load. |
| `src/Layered.cs` | `Surface` (a DIB section GDI+ draws into) and `LayeredWindow`: per-pixel alpha via `UpdateLayeredWindow`, topmost, no-activate, tool window. |
| `src/Art.cs` | Palette (`Pal`), the cell canvas (`Cells`), glyphs, `Look` (everything about this frame's pose), and `Sprite.DrawFront` / `DrawBack` (all the pixel art). |
| `src/Render.cs` | `Renderer`: cells → screen. Draws two windows: **Body** (Clawd plus the gun; this is the only window that takes clicks) and **Fx** (click-through: shadow, movie-night glow, particles, speech bubble). Handles squash/stretch and rotation. |
| `src/Pet.cs` | The body and senses: position (feet), physics, platforms, mouse (drag, throw, clicks), sensors, particles, the per-tick `Step()` and `Draw()`, and frame skipping. |
| `src/Behaviours.cs` | `Triggers()` (what grabs his attention), `PickActivity()` (weighted by settings and needs), and `Behave()` (the big mode switch), plus gun, trickshots, web-swing, movie night, file comments and the smash. |
| `src/Effects.cs` | Pellets and the web line: small click-through overlay windows sized to their content. |
| `src/Cloud.cs` | The thought-cloud right-click menu (anti-aliased) with mood bars. |
| `src/Food.cs` | Snack sprites and `FoodItem`: a draggable window with gravity. |
| `src/Settings.cs` | `Settings` (feature toggles + size/activity/chattiness sliders + the settings form), `Needs` (food/energy/fun/love), and `Store` (key=value files in `%APPDATA%\Clawd`). |
| `src/Media.cs` | Play/pause via WinRT GSMTC, called through reflection (no winmd references needed). |
| `src/World.cs` | Monitor, DPI → scale `S` and `K`, floor, `GroundAt`. |
| `src/Demo.cs` | The scripted README show on a fake desktop, rendered off-screen. |
| `build.ps1` | Compiles `Clawd.exe` (C# 5, `/target:winexe`, icon `clawd.ico`). |
| `tools/install.ps1`, `tools/uninstall.ps1` (+ the two `.cmd` wrappers) | Per-user install to `%LOCALAPPDATA%\Programs\Clawd`: Start menu + Startup shortcuts and a Settings › Apps entry (version 2.0). Their params exist only for sandbox testing. |
| `tools/make_gif.py` | Frames → `docs/clawd.gif` (one shared palette, no dithering). |
| `legacy/clawd-v1.ps1` | The v1 PowerShell pet, for reference only. |

`Clawd.exe`, `dist/` and `frames/` are build output and gitignored. Settings, needs and logs live in `%APPDATA%\Clawd`.

## Gotchas (read before editing)

- **C# 5 only.** `csc.exe` v4.8 has no `$"..."`, `?.`, `nameof`, expression-bodied members, `out var`, tuples or local functions. Lambdas, `var`, LINQ and anonymous types are fine.
- **Never call `Show()` in a way that activates.** `LayeredWindow` overrides `ShowWithoutActivation` and returns `MA_NOACTIVATE`, so he never steals focus. Overlays are click-through (`WS_EX_TRANSPARENT`).
- **Hit-testing:** with `UpdateLayeredWindow`, pixels with alpha 0 are click-through. That's why the shadow, glow and bubble live in the separate click-through **Fx** window, not in **Body**.
- **Screenshots:** a normal `CopyFromScreen`/`BitBlt` skips layered windows. Add the `CAPTUREBLT` flag (`0x40000000`).
- **Coordinates:**
  - `X`, `Y` = his **feet** in screen pixels.
  - Art is drawn in cells: his 22×16 box sits at `Sprite.OX`, `Sprite.OY` in a 60×34 canvas.
  - `S` = screen pixels per cell (from DPI and the size setting), and `K = S/3` scales speeds.
  - `CX()`/`CY()` convert cells to screen.
- **Timing:** the simulation runs at a fixed 60 ticks/s (behaviours count ticks). `Draw()` skips frames when nothing visible changed, and only moves the windows when just his position changed. If you add a visual, put it in `Look` and in `Look.Key()`.
- `Look` is **reset every tick**; modes set it each frame. Persistent things (like `suit`) live in `Pet`.
- The running `Clawd.exe` locks the file. Stop it before building (`Get-Process Clawd | Stop-Process`).

## Modes

- **Free** (can be interrupted by triggers): `walk`, `idle`, `chase`.
- **Everything else:**
  - `fall` (hops, jumps and **throws**: bounces, spin, dizzy on a hard landing)
  - `crouch`, `goclimb`
  - `wave`, `pet`, `dizzy`, `dance`, `bop` (music), `coffee`, `think`, `typing`
  - `yawn`, `sleep` (nap when tired: `napping`)
  - `morning`, `hungry`, `gofood`, `eat`, `sniff`, `filechew`, `catch` (clipboard), `hot` (CPU), `lowbatt`, `charged`
  - `smash`, `gun`, `trick`
  - `gowatch`, `watch`, `paused`
  - `suitup`, `swing`, `heropose`

**Adding a behaviour:**
1. Add a `case` in `Behave`.
2. Add a trigger in `Triggers` and/or a weight in `PickActivity` (respect `cfg` toggles and needs).
3. Add a menu item in `Pet.OpenMenu`.
4. If it can be switched off, add a settings toggle (`Settings.Labels` plus the field).
5. Optionally add a demo segment.
6. Update the README and this file.

## Testing (run it and look)

- **Build:** `powershell -ExecutionPolicy Bypass -File build.ps1`. Warnings are fine.
- **Test switches:** `Clawd.exe --test --verbose --mode <mode> --x <screenX>`.
  - `--test` uses its own mutex and poke event, so it runs next to the real one, and logs to `%APPDATA%\Clawd\clawd-test.log`.
  - `--mode` forces a mode after 1.5 s. The special values are `food`, `menu`, `watch` and `paused`, which fake a video.
  - `--verbose` logs every mode change with his position.
- **Screenshots:** grab the screen with `BitBlt` + `CAPTUREBLT`, using a DPI-aware Windows PowerShell 5.1 helper. The owner's screen is 3200×2000 at 200 %.
- **Demo:** `Clawd.exe --render-frames <dir>` (about 1400 frames, 800×270), then build contact sheets to check them.
- **CPU:** measure `TotalProcessorTime` over 10 s. V2 uses about 3–4 % of one core while walking or idle (v1 used about 22 %).
- **Installer:** run `tools/install.ps1` / `tools/uninstall.ps1` with `-Dest -MenuDir -StartupDir -RegName -DataDir -Quiet` into a temp folder. Never test into the real Start menu.

## Release checklist

1. `build.ps1`, then `Clawd.exe --render-frames <tmp>` and `python tools/make_gif.py <tmp> docs/clawd.gif`.
2. Update `README.md` and this file. Bump `Program.Version`, the assembly version and `DisplayVersion` in `install.ps1`.
3. Commit, ending the message with the Co-Authored-By line, and push.
4. Build `dist/Clawd.zip`. It holds `Clawd.exe`, `README.md`, both `.cmd` files and `tools/install.ps1` + `tools/uninstall.ps1`.
5. Run `"C:\Program Files\GitHub CLI\gh.exe" release create vX.Y dist/Clawd.zip ...`. The README links `releases/latest/download/Clawd.zip`.
6. Restart the owner's Clawd: `Get-Process Clawd | Stop-Process`, then start `PROJECTS\Clawd\Clawd.exe`.

## Product rules

- **Privacy:**
  - Keyboard: count key presses only, never which keys.
  - Read window titles, media state, battery, CPU and clipboard *events* locally only. Never read clipboard contents or file contents.
  - Nothing is sent anywhere.
  - Keep the README privacy section accurate.
- **Public-facing text** (README, menu, releases): don't use trademarked character names. The spider costume is the "web-slinger suit".
- **Look:**
  - Chunky pixel art for Clawd himself, with smooth, soft effects around him (shadow, glow, particles, bubbles).
  - Dark-brown outline (`Pal.Outline`), a light top-left edge and a shaded bottom-right.
  - Clawd orange is `217,119,87`.
- He must never steal focus or show up in the taskbar or Alt+Tab.
