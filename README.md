# Clawd

A little orange pixel buddy who lives on your Windows desktop.

He walks along your taskbar, climbs onto your windows, types along when you type, and occasionally smashes his laptop in frustration.

![Clawd](clawd.ico)

## Running him

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -STA -File .\clawd.ps1
```

You only need to run that command the first time. On launch, Clawd creates:

- a **Start menu** entry called "Clawd". Use it to bring him back after you've said bye. If he's already running, he waves at you instead.
- a `launch.vbs` next to the script, which starts him without a console window.

**Start with Windows** is a toggle in his menu.

Nothing to install: it's a single PowerShell script using Windows Forms, plus a few Win32 calls compiled on the fly.

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

To type along with you, Clawd polls the keyboard state and only counts how many keys went down. It never records which keys you pressed, and nothing is stored or sent anywhere.

## Uninstall

1. Right-click him and choose **Bye, Clawd**.
2. Delete `Clawd.lnk` from the Start menu (`shell:programs`) and, if you enabled it, from Startup (`shell:startup`).
3. Delete this folder.
