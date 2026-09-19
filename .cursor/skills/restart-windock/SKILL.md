---
name: restart-windock
description: >-
  Force-closes a running WinDock process and relaunches it after development
  changes that only take effect on restart. Use when finishing WPF/UI/widget/dock
  work, when MSB3027 file locks mention WinDock, when the user would otherwise
  be told to restart, or when verifying app behavior. Do not use for read-only
  questions or test-only work that does not need the running app.
---

# Restart WinDock

开发是需要重启才生效的，就自动重启，之前开着就强制关闭再重启

Do not tell the user to restart. Run the restart yourself.

## When

After any change that only appears in a running WinDock process: App, Shell, Widgets, Providers, Infrastructure, Core (when used by the app), XAML, or settings that the live process already loaded.

Skip for questions, plans, and test-only edits that you will not show in the app.

## How

From the repo root, **kill first, then build, then start**. Building while WinDock is open locks `WinDock.dll` and fails with MSB3027.

```powershell
powershell -NoProfile -File .cursor/skills/restart-windock/scripts/restart.ps1
```

The script:

1. Force-stops every `WinDock` process
2. Waits until those processes exit
3. Builds `src/WinDock.App/WinDock.App.csproj` into the normal Debug output
4. Starts `WinDock.exe`

If the script fails, fix the error and run it again. Do not copy the build to a temp folder as a workaround.

## After

One short sentence: WinDock was force-closed (if it was open) and restarted. Do not ask the user to restart.
