# Clawd

A little orange pixel buddy who lives on your Windows desktop.

<p align="center"><img src="docs/clawd.gif" alt="Clawd walking, waving, typing, thinking, dancing, smashing his laptop, watching a movie with popcorn and napping"></p>

He walks along your taskbar and climbs onto your windows. When you type, he types along on his tiny laptop. When you watch a video, he grabs popcorn and watches with you. Once in a while he smashes his laptop in frustration.

## Install

1. **[Download Clawd](https://github.com/finnvangronsveld/clawd/releases/latest/download/Clawd.zip)** and unzip it anywhere.
2. Double-click **`Install Clawd.cmd`**.

That's it. You don't need admin rights or anything else installed. He appears at the bottom of your screen and starts with Windows from then on.

- **Bring him back:** if you send him away, start **Clawd** from the Start menu.
- **Uninstall:** use **Settings › Apps**, or double-click `Uninstall Clawd.cmd`.

> Windows might ask whether you want to run a file downloaded from the internet. Choose **Run**. Clawd is a single readable PowerShell script ([`clawd.ps1`](clawd.ps1)), so you can see exactly what he does.

## Things to do with him

| Do this | He does |
|---|---|
| Left-click | Hops |
| Click 4× quickly | Gets dizzy |
| Drag | Flails ("wheee!") and falls when you let go. You can drop him onto a window. |
| Rub the cursor over him | Blushes and floats hearts |
| Bring the cursor near | Waves hi |
| Right-click | Opens his thought-cloud menu |
| Type | He pulls out his laptop and types along |
| Pause typing | He thinks: spinner plus "Clauding...", "Percolating..." and so on |
| Watch a video (YouTube, Netflix, Twitch, VLC, ...) | He walks over, sits with his back to you, and eats popcorn in the light of the screen |
| Stay idle for 60 s | He yawns and falls asleep (Zzz) |

He also does things on his own:
- chases your cursor
- dances
- takes coffee breaks
- thinks hard
- jumps onto the top of your windows and rides along when you move them
- shoots tiny pixel pellets at your cursor ("gotcha!" / "dang it!")
- smashes his laptop ("fixed it.")

He doesn't show up in the taskbar or in Alt+Tab.

## Privacy

Everything stays on your PC:

- **Typing:** Clawd only counts how many keys went down, never which keys.
- **Videos:** he checks the title of the window in front to notice a video, and compares it against a list of video sites and players.

Nothing is stored or sent anywhere.

## For tinkerers

Run him straight from a clone:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -STA -File .\clawd.ps1
```

Rebuild the GIF above. This renders a scripted demo off-screen, then needs Python with Pillow:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -STA -File .\clawd.ps1 -RenderFrames .\frames
python tools\make_gif.py .\frames docs\clawd.gif
```
