# Flippy: notes for Claude

Flippy is a pixel-art desktop pet for Windows. The app is a C# Windows Forms program (`src/*.cs` → `Flippy.exe`), built with the .NET Framework 4.x compiler that ships with every Windows install. Pets are **species** (data plus drawing code), so every behaviour works for every pet:

- **Flip** (a flip phone), **Hopper** (a frog) and **Flapjack** (a pancake) are the "flip" pets, after Flipforward. Flip is the default.
- **Clawd (classic)**, the original orange pet, is the fourth. (The teal blob that was briefly called Flippy is retired.)

Public repo: https://github.com/finnvangronsveld/flippy

**Keep this file and `README.md` up to date whenever behaviour changes.** The owner asked for this explicitly: he updates the Flippy website from this file, so the "For the website" section below must always match the latest release.

Local checkouts (the owner has more than one PC):

- `C:\Users\Finn Vangronsveld\PROJECTS\flippy`: the running copy is `Flippy.exe` in that folder, and the Start menu and Startup shortcuts point there. The old installer copy (`%LOCALAPPDATA%\Programs\Flippy` and its Settings › Apps entry) was removed here.
- `C:\Users\finnv\PROJECTS\Flippy` (renamed from `PROJECTS\Clawd`) on the other PC, where the shortcuts point to `PROJECTS\Flippy\Flippy.exe`.

Starting `Flippy.exe` (without `--test`) points the shortcuts at whichever exe was started. Check which PC you're on with `$env:USERPROFILE`.

## For the website (current facts)

- **Name:** Flippy, after the owner's brand Flipforward. Tagline: "A little pixel buddy who lives on your Windows desktop."
- **Latest version:** 2.6.1 (`Program.Version`). Releases: https://github.com/finnvangronsveld/flippy/releases
- **Download (always the latest):** https://github.com/finnvangronsveld/flippy/releases/latest/download/Flippy.zip
  - Unzip it and double-click `Install Flippy.cmd`.
  - Needs Windows 10 or 11. No admin rights, nothing else to install.
  - It's a single small `Flippy.exe` (about 175 KB).
- **Not code-signed:** SmartScreen may warn. Click "More info", then "Run anyway".
- **Demo GIF:** `docs/flippy.gif` (raw: https://raw.githubusercontent.com/finnvangronsveld/flippy/main/docs/flippy.gif). App icon: `flippy.ico`.
- **Pets** (right-click → "Change pet...", or in Settings):

  | Pet | Id | Tagline | Signature move |
  |---|---|---|---|
  | **Flip** (default) | `phone` | a flip phone with feelings | front flip ("flip open!") |
  | **Hopper** | `frog` | a frog who flips | front flip |
  | **Flapjack** | `pancake` | golden, fluffy, flippable | front flip |
  | **Clawd (classic)** | `clawd` | the original orange buddy | smashes his laptop |

- **Features** (every pet can do all of them):
  - **Moving around:**
    - walks along the taskbar
    - climbs onto windows and rides along when you move them
  - **Typing:** types along on a tiny laptop when you type, and thinks when you pause.
  - **Media:**
    - Movie night: he sits under a playing video with popcorn, lit from behind by the screen, and comments on it.
    - If you pause the video, he throws a tantrum.
    - He puts on headphones when music plays.
  - **Handling him:**
    - drag and throw him (physics, spins, bounces, lands dizzy)
    - he hops when clicked, gets dizzy after 4 quick clicks, and blushes when you rub him
  - **Claude Code buddy:** he tells you when Claude Code is done (hops and waves, with the project name) or has a question / needs your OK (jumps with a red "!" until you click him or start typing). One click in Settings connects it (Claude Code hooks). Terminal and the desktop app's Code tab.
  - **Pocket:** drop up to 3 files or folders on him; take them back from his menu (click = copy, then paste anywhere; or drag them out). Hold the cursor still on him and the pocket pops up.
  - **Flipforward:** on flipforward.be he gets heart eyes ("that's my creator!"); on flippy.flipforward.be (his home) he cheers; he stays happy while the site is in front.
  - **Food and files:**
    - feed him snacks: a cookie, pizza, apple or donut
    - drop a file on him and he has an opinion, then pockets it
  - **Needs:**
    - food, energy, fun and love, shown in his menu and remembered between sessions
    - he naps when you're idle and wears a nightcap at night
  - **PC reactions:** low battery, charging, busy CPU, copying, late at night, and coming back after a break.
  - **On his own:**
    - chases the cursor, dances, takes coffee breaks
    - aims a pellet gun at your cursor and does trickshots (360 no-scope, ricochets)
    - superheroes (generic, no real characters): web-slinger (pendulum swinging, a rope-like web line), caped flyer (flies across the screen), speedster (zooms with a lightning trail), night guardian (grapples up, glides down), shield hero (throws a bouncing shield)
    - an anime battle with his shadow clone: auras, a dash and clash, a beam struggle he wins
    - builds a sandcastle in a corner (six stages; it crumbles after a few minutes). Throw him into it and it bursts into sand, and he sulks
    - smashes his laptop, or does his signature move
  - **Menus:**
    - a hologram right-click menu: he projects it above his head (beam, glow, scanlines, flickers on). Name and mood bars, Play and Tricks button grids, then Pocket, Change pet (live previews), Settings, Bye
    - a settings window: pick the pet, turn each feature on or off, set size, activity and chattiness, start with Windows, reset needs
  - **Stays out of the way:** never steals focus, and isn't in the taskbar or Alt+Tab.
- **Privacy:** everything stays on the PC. Nothing is sent anywhere, and data lives in `%APPDATA%\Flippy`.
  - **Typing:** counts key presses, never which keys.
  - **Videos:** reads the title of the window in front and Windows' media play/pause state.
  - **Websites:** reads the browser tab title in front (and the browser's process name) only to spot flipforward.be and flippy.flipforward.be.
  - **PC reactions:** battery, CPU and "something was copied" events, never the copied contents.
  - **Dropped files:** looks only at the file name.
  - **Menus:** while a menu is open, it notices mouse clicks (only where they happen) so it knows when to close.
  - **Pocket:** only the paths of dropped files are remembered (`pocket.txt`); files are never opened, moved or copied until you take them out.
  - **Claude Code:** the hooks pass only the project folder name and Claude's short status message or question to the pet, locally.
- **Open source:** C#, built with the compiler that ships with Windows. Public repo above.
- **Changelog:**
  - **2.6.1:** throwing him into his sandcastle destroys it (a burst of sand, then he sulks).
  - **2.6:** five superheroes (web-slinger with real pendulum physics and a wobbly web line, caped flyer, speedster, night guardian, shield hero), the anime battle, sandcastles, Flipforward website reactions, and the pocket pops up when you hover him.
  - **2.5:** Claude Code buddy (tells you when Claude is done or has a question), a pocket for 3 files, the hologram menu, and animation polish: walking feet really step, a bounce on every step, a squash when he turns, and Flip's mouth expressions now read clearly.
  - **2.4:** New right-click menu: a proper panel at the cursor instead of the thought cloud; "Change pet" is a page inside it. Closing on an outside click now really works (the click watcher runs on its own thread).
  - **2.3:** The menus (the right-click cloud and "Change pet...") now close reliably on a click outside them, on Esc, when you switch windows, or after 4 s with the cursor away.
  - **2.2:**
    - Flip is now the default pet, and Hopper and Flapjack were added.
    - The blob is retired.
    - The pet falls off a window that gets covered (Alt+Tab).
  - **2.1:** renamed to Flippy; added the pet picker and the species system.
  - **2.0:** rewritten as a C# app, with better graphics, throw physics, feeding and needs, PC reactions and settings.
  - **1.x:** the original PowerShell pet (movie night, gun, trickshots, climbing, web-slinger suit).

## Layout

| Path | What |
|---|---|
| `src/Program.cs` | Entry point: single instance (`Local\FlippyDesktopPet` + `Local\FlippyPoke`), migration calls, the fixed 60 Hz loop, shortcuts (`Flippy.lnk`), the settings window, and the dev and test switches. |
| `src/Species.cs` | `Species` (id, name, short name, tagline, palette, prop anchors, lines, signature mode, `DrawBody` / `DrawFace` / `DrawBack`) and `SpeciesList` (`All`, `Get` with fallback to the default, `Current`). |
| `src/SpeciesTemplate.cs` | `TemplateSpecies`: a pet drawn from a hand-authored pixel template. Outline and edge light/shade are added automatically; arms, feet and the face (big or bead eyes) are procedural at anchor points; the back view is derived. Use this for new pets. |
| `src/SpeciesFlipPets.cs` | Flip (default), Hopper and Flapjack, as templates. |
| `src/SpeciesClassic.cs` | The classic orange pet. Deleting this file and its entry in `SpeciesList.All` removes him. |
| `src/Art.cs` | `Pal`, `Cells` (the pixel canvas), `Glyphs`, `Look` (this frame's pose), and `Sprite`. `Sprite` holds the shared props (headphones, nightcap, mug, snacks, laptop, the "!"), the species-agnostic superhero suits (`L.Suit` + `L.Hero`: web via `DrawSuit`, caped/speed/night/shield via `DrawHero` recolouring + emblems; capes behind the body via `DrawCape`, `L.Cape` 0 hanging / 1 streaming / 2 spread; `DrawShield`), the popcorn bucket, the gun bitmaps and `IconArt`. |
| `src/Render.cs` | `Renderer`: cells → screen. **Body** window (the pet plus the gun; the only window that takes clicks) and **Fx** window (click-through: shadow, movie-night glow, particles, bubble). Squash/stretch and rotation. |
| `src/Pet.cs` + `src/Behaviours.cs` | `partial class Pet`: body, senses, mouse, menu (`OpenMenu`), `SwitchTo`, `Triggers`, `PickActivity`, and the mode switch (`Behave`). |
| `src/Pocket.cs` | The pocket: up to 3 paths in `%APPDATA%\Flippy\pocket.txt` (newest last; a 4th drops the oldest). |
| `src/ClaudeHooks.cs` | Claude Code alerts. `Notify` = the `--notify <stop|notification|question>` side (reads the hook JSON from the **raw stdin handle**; a GUI exe has no Console streams), writes a line to `claude-inbox.txt` and sets `Local\FlippyNotify`. `TakeInbox` = the running pet's side. `Connect`/`Disconnect` edit `~/.claude/settings.json` (backup `.flippy-backup`, pretty-printed, other hooks untouched; ours are recognised by `<exe> --notify <event>`). |
| `src/Battle.cs` | The anime battle: `Battle.Step` runs the script (appear, power-up, dash + clash, charge, beam struggle, explosion, victory) and returns a `BattlePose` for the pet; the shadow clone, auras, beams and sparks are drawn in one click-through window behind him. |
| `src/Castle.cs` | The sandcastle: pixel art in 6 stages in its own click-through window; `Finish()` keeps it 3 minutes, then it crumbles. |
| `src/Menu.cs` | `PetMenu` + `MenuItem`: the **hologram** right-click menu. **One** layered window, projected above his head (`Pet.HeadPoint()`): a beam from his head, glow, scanlines, corner brackets; it flickers on over ~14 ticks (`openT`; rows are laid out even while hidden). Main page: name, tagline, mood bars, button grids per `MenuItem.Group` ("Play", "Tricks"), then rows. `MenuItem.Page(text, hint, target)` opens a page in the same window: `"pets"` (Change pet) and `"pocket"` (click = copy, drag = `DoDragDrop` out, x = remove). An item runs on mouse-up over the same row it went down on; the menu hides first. |
| `src/Settings.cs` | `Store` (key=value files in `%APPDATA%\Flippy`), `Settings` (toggles, sliders, `Pet=<id>`, and the settings form with its Pet section), and `Needs`. |
| `src/Migration.cs` | The **only** code that knows the old name. It copies settings and needs from the old folder once, stops an old running instance, and replaces old shortcuts. |
| `src/Food.cs`, `Effects.cs`, `Media.cs`, `Native.cs`, `Layered.cs`, `World.cs` | Snacks, pellets and the web line, play/pause (WinRT via reflection), P/Invoke, per-pixel-alpha windows, and monitors/DPI. |
| `src/Demo.cs` | The scripted README show on a fake desktop, rendered off-screen. |
| `src/DevTools.cs` | `--dump-sprites` (pixel-diffable poses) and `--make-icon`. |
| `build.ps1` | Compiles `Flippy.exe` (C# 5, icon `flippy.ico`). `-Out bin\Dev.exe` builds elsewhere, so a running copy doesn't lock the output. |
| `tools/install.ps1`, `tools/uninstall.ps1`, `Install Flippy.cmd`, `Uninstall Flippy.cmd` | Per-user install to `%LOCALAPPDATA%\Programs\Flippy`: Start menu + Startup `Flippy.lnk` and a Settings › Apps entry "Flippy". The installer also removes an old pre-rename install. The params exist only for sandbox testing. |
| `tools/make_gif.py`, `tools/diff_sprites.py`, `tools/contact_sheet.py` | Demo GIF, sprite pixel-diff, labelled pose sheets. |
| `legacy/v1.ps1` | The v1 PowerShell pet, for reference only. Don't edit it. |

`Flippy.exe`, `bin/`, `dist/`, `frames/` and the logs are gitignored. Settings, needs and logs live in `%APPDATA%\Flippy`.

## Gotchas (read before editing)

- **C# 5 only.** `csc.exe` v4.8 has no `$"..."`, `=>` members, `?.`, `nameof`, `out var`, tuples, local functions or auto-property initialisers. Lambdas, `var` and LINQ are fine. Keep sources ASCII; the `.cmd` files stay CRLF (`.gitattributes`).
- **Focus:** `LayeredWindow` never activates (`ShowWithoutActivation`, `MA_NOACTIVATE`). Overlays are click-through. Pixels with alpha 0 are click-through, which is why soft effects live in the **Fx** window.
- **Screenshots:** a plain `BitBlt`/`CopyFromScreen` skips layered windows. Use `CAPTUREBLT` (`0x40000000`).
- **Coordinates:**
  - `X`, `Y` = his **feet** in screen pixels.
  - The pet's 22×16 box sits at `Sprite.OX`, `Sprite.OY` in a 60×34 cell canvas. Some pets stick out above it (Flip's antenna, Flapjack's butter).
  - `S` = screen pixels per cell (from DPI and the size setting), and `K = S/3` scales speeds.
- **Timing:** a fixed 60 ticks/s. `Draw()` skips frames when nothing changed. Any new visual must be in `Look` **and** `Look.Key()`.
- `Look` is reset every tick, and modes set it each frame. Persistent state (`suit`, `Dir`) lives in `Pet`.
- **Art:** draw only into `Cells` (`Rect`/`Dot`/`Glyph`), never anti-aliased. Chunky pixels, a dark outline, a light top-left edge and a shaded bottom-right.
- **Menus close via `ClickWatch`** (in `Native.cs`):
  - A low-level mouse hook (`WH_MOUSE_LL`) on its **own background thread** (with its own message loop). It must stay there: Windows silently drops a hook whose thread answers slowly, and the UI thread is often busy (media polling, window scans, drawing). That was the 2.3 bug.
  - It only records button-down points while **armed** (`Arm()` when a menu opens, `Disarm()` in `CloseMenus()`). Polling the buttons each tick is a backup.
  - Every tick, `Pet.Step` closes the menu if any of these happened:
    - a click that isn't on the menu panel, from `ClickWatch.ClickedOutside(menu.PanelContains)`. Clicks on the pet count as outside; the soft shadow too.
    - Esc
    - the foreground window changed
    - the cursor has been away for 4 s
  - While a file is dragged out of the pocket (`menu.Dragging`) the menu stays open.
  - Right-click on the pet toggles: the press closes the menu, and the release doesn't reopen it (`ClosedAtMs`).
  - Don't go back to the "pressed since last call" bit of `GetAsyncKeyState`. It's shared system-wide and misses clicks.
  - `--verbose` logs "menu opened rows: i@x,y ...", "menu: pets page rows: ...", "outside click (hook|poll) at x,y" and "menu closed: click|esc|focus|away".
- **Superheroes:** `StartHero(id)` -> `suitup` -> `swing` / `fly` / `zoom` / `glide` / `shieldthrow` -> `heropose`. `HeroMode()` lists the modes that keep the suit on. Swinging is a pendulum in angle space (`theta`, `omega`, rope length `ropeL`, gravity `0.21*K`): attaching converts his velocity to `omega`, reeling in keeps angular momentum, he lets go on the upswing with the swing's own velocity. Anchors: your cursor, a window top (`Native.FindTop`), the top of the screen, else a point in the air ahead. `fx.Web(..., wobble, sag, phase, reach)` draws the line as a wobbly curve that unrolls when shot.
- **Battle / sandcastle / sites:** `StartBattle()`, `StartBeach()` (walks to the nearer bottom corner first). In `fall`, a thrown pet that touches the castle (`Castle.Hits`) calls `SmashCastle()` (sand burst, castle hidden) and sulks in `castlesad` after landing. Sites: every 30 ticks, if the foreground window is a browser (`Native.IsBrowser`, by process name) its title is matched ("Flippy" + "pixel buddy" = home, "flipforward" = creator) -> mode `site` once per visit (again after 10 min).
- **Pocket peek:** cursor still on him for 40 ticks with files in the pocket -> the menu opens on the pocket page with `peek = true` (closes after 40 ticks away instead of 240; going "<" to the main menu ends the peek).
- **Template pets:** a stepping foot (`L.Phase` 1 or 3) is lifted one cell and moved forward (`L.Face`). `MouthRows = 2` for small faces (Flip's LCD) keeps every mouth inside two rows. Walking/chasing also bobs the whole sprite one cell per step (`bobY` in `Pet`, kept out of the shadow).
- **Species:** behaviours never draw pet-specific pixels. Props are placed with the species' anchors (`MugSipX`, `HeldY`, `BangY`, `MaskLX`...).

## Adding a pet

1. Easiest: derive from `TemplateSpecies` (see `SpeciesFlipPets.cs`): draw `Rows` with one char per pixel, map chars to `Colors`, set the anchors, call `Build()`. Otherwise add `src/Species<Name>.cs` with a class deriving `Species`. Set `Id`, `Name`, `ShortName`, `Tagline`, `Hello`, `Lines`, `SignatureMode` / `SignatureLine`, the palette (`Body`, `Light`, `Shade`, `Outline`, `Back`, `Accent`, `Specular`) and any anchors that differ from the defaults.
2. Implement `DrawBody` (every `Arms` value: out, typeL, typeR, down, up, upL, upR, wave1, wave2, sip, hold, fan, aim, plus the back-view ones rest, reach and eat), `DrawFace` (every `EyeStyle`: normal, up, down, wide, blink, sleep, happy, dizzy, heart, angry, sad; and every `Mouth`: none, o, smile, frown, yawn, chomp, chew; plus `Blush`) and `DrawBack`.
3. Add it to `SpeciesList.All`. The first entry is the default.
4. If the signature is a new mode, add a `case` in `Behave`. Otherwise point `SignatureMode` at an existing mode.
5. Check his art: `bin\Dev.exe --dump-sprites <dir> --species <id>`, then `python tools/contact_sheet.py <dir> sheet.png 4`.
6. Run a live test: `--test --pet <id> --mode <mode>` for walk, sleep, watch, gun, suitup, coffee, typing, dance, food, goclimb, signature and picker.
7. Update the README's "Pick your pet" section and this file.

## Testing (run it and look)

- **Build:** `powershell -ExecutionPolicy Bypass -File build.ps1 -Out bin\Dev.exe`.
- **Test switches:** `bin\Dev.exe --test --verbose [--pet <id>] [--mode <mode>] [--x <screenX>] [--data <dir>] [--old-data <dir>] [--busy]`.
  - `--test` uses its own mutex and poke event, and a **temp data folder** (`%TEMP%\FlippyTestData`, or `--data`). It never touches the real `%APPDATA%\Flippy`, and it skips the real migration and shortcut handling.
  - `--mode` takes any mode name, or `food`, `menu`, `picker`, `switch`, `signature`, `watch` or `paused`.
  - **New modes:** `--mode hero-web|hero-caped|hero-speed|hero-night|hero-shield`, `battle`, `beach`, `castlethrow` (a finished castle next to him, then he gets thrown at it), `site-home`, `site-creator`. With `--verbose`, hero modes log `hero <mode> t x y rot` every 5 ticks (check for jumps).
  - **Claude alerts:** start a test pet, then pipe a hook JSON **file** into `bin\Dev.exe --notify stop|notification|question --test` (Git Bash `echo` mangles backslashes, so write the JSON with Python). `--claude-hooks on|off` connects/disconnects; set `FLIPPY_CLAUDE_SETTINGS=<copy of settings.json>` to test on a copy, never on the real file.
  - **Pocket:** `--mode pocket` opens the pocket page; seed `<data>\pocket.txt`. Drag tests need the drag source in **another process** (a drag is a modal loop) and real `mouse_event` moves; Explorer is the trustworthy drop target.
  - **Menu closing:** open a menu with `--mode menu` or `picker` (add `--busy` to stall the UI thread 400 ms every half second). Click a dummy window that's already in front, the pet, an item, and "Change pet" → a pet (row centres are in the log), and check the log and that no menu window is left visible. Send the synthetic clicks from a **DPI-aware** process, or the coordinates are off. Pass `--pet` so the test data's saved pet doesn't skew the switch test. The owner may be using the mouse during tests: a stray "outside click" at a point you didn't click is him.
  - `--old-data` tests the settings migration from a fake old folder.
  - Logs go to `<data>\flippy-test.log`.
- **Art refactors:** `--dump-sprites` before and after, then `python tools/diff_sprites.py <before> <after> <diff_out>`.
- **Demo:** `--render-frames <dir>` (about 1400 frames, 800×270), then contact sheets.
- **CPU:** measure `TotalProcessorTime` over 10 s. Expect about 3–4 % of one core.
- **Installer:** use `-Dest -MenuDir -StartupDir -RegName -OldDest -OldRegName -Quiet` (uninstall: `-DataDir`) into a temp folder, never into the real Start menu or registry. Test both a fresh install and an upgrade over a fake old install.

## Rename and migration notes

The app used to have a different name, and the classic pet still has it. The old name may appear only in:

- the classic species file
- `src/Migration.cs`
- the old-install cleanup lines in `tools/install.ps1`
- `legacy/`
- the README credit line

Old git history keeps the old name, and that's fine.

## Release checklist

1. Run `build.ps1` (→ `Flippy.exe`). If the default pet's art changed, also run `bin\Dev.exe --make-icon flippy.ico`.
2. Run `bin\Dev.exe --render-frames <tmp>`, then `python tools/make_gif.py <tmp> docs/flippy.gif`.
3. Update `README.md` and this file, including the "For the website" version and changelog. Bump the version in `Program.Version`, `AssemblyVersion`/`AssemblyFileVersion` and `DisplayVersion` in `tools/install.ps1`.
4. Commit, ending the message with the Co-Authored-By line, and push.
5. Build `dist/Flippy.zip`. It holds `Flippy.exe`, `flippy.ico`, `README.md`, both `.cmd` files and `tools/install.ps1` + `tools/uninstall.ps1`.
6. Run `"C:\Program Files\GitHub CLI\gh.exe" release create vX.Y dist/Flippy.zip ...`. The README links `releases/latest/download/Flippy.zip`.
7. Restart the owner's pet: `Get-Process Flippy | Stop-Process`, then start `Flippy.exe` in this PC's checkout (see "Local checkouts" above).

## Product rules

- **Privacy:**
  - Keyboard: count key presses only, never which keys.
  - Read window titles, media state, battery, CPU and clipboard *events* locally only. Never read clipboard or file contents.
  - Store settings, the chosen pet, needs and the pocket (paths only) only in `%APPDATA%\Flippy`.
  - The pocket never opens, moves or copies a file by itself; only the user taking it out does.
  - Claude Code hooks pass only the project folder name and Claude's short status message or question.
  - Nothing is sent anywhere.
- **Public-facing text:** don't use trademarked character names. The heroes are the "web-slinger", "caped flyer", "speedster", "night guardian" and "shield hero"; the battle beam is "<NAME>... BEEEAM!".
- **Look:**
  - Chunky crisp pixel art for the pets, with smooth soft effects around them.
  - The retired teal blob ("flippy" id) falls back to the default pet if it is still in someone's settings.
- He must never steal focus or show up in the taskbar or Alt+Tab.
