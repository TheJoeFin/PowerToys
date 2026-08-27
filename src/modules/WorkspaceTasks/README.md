# WorkspaceTasks (experiment)

A standalone WinUI 3 task app that pairs a simple to-do list with **PowerToys Workspaces**.
Add tasks, check them off, browse completed ones, and optionally link a task to a workspace —
either an existing one or a fresh capture of your current window layout. Launching a linked
task restores that whole window arrangement.

> **Status: experiment.** This module is intentionally **not** wired into the PowerToys runner,
> Settings UI, or installer. It builds and runs on its own so the idea can be validated before
> deciding how (or whether) to integrate it. It has **no PowerToys project references** — only
> NuGet packages — so it can be removed cleanly by deleting this folder.

## How it integrates with Workspaces

Workspaces exposes no official API, but it is built from standalone executables plus a JSON store,
which together form a usable (if unofficial, version-coupled) integration surface:

| Need | Mechanism |
|------|-----------|
| List saved workspaces | Read `%LOCALAPPDATA%\Microsoft\PowerToys\Workspaces\workspaces.json` |
| Launch a workspace | Run `PowerToys.WorkspacesLauncher.exe <workspace-GUID> 0` |
| Launch in a new virtual desktop | Create + switch to a fresh desktop, then launch (see below) |
| Capture current windows | Run `PowerToys.WorkspacesSnapshotTool.exe` → reads `temp-workspaces.json`, names it, appends to `workspaces.json` |

### Open in a new virtual desktop

Each linked task offers two launch buttons: launch in place, and **launch in a new virtual
desktop** (`Services/VirtualDesktopHelper.cs`). The latter creates a fresh desktop and switches to
it before launching, so the workspace opens in a clean "working space" and the current desktop is
left untouched. The new desktop is **named after the task** (visible in Task View) by writing the
`Name` value under `...\VirtualDesktops\Desktops\{GUID}` — the same registry location PowerToys'
`GetDesktopName` reads from.

Windows has **no documented API to create a virtual desktop** — the documented `IVirtualDesktopManager`
can only query/move windows, and the internal interface that can create desktops changes COM GUIDs
with nearly every Windows build. PowerToys itself only uses the documented interface plus the
registry (see the WindowWalker `VirtualDesktopHelper`). This experiment follows the same principle:
it creates + switches via the stable system hotkey (`Win+Ctrl+D`, documented `user32` input) and
confirms the result by reading the same `...\Explorer\VirtualDesktops` registry keys PowerToys reads,
rather than taking a dependency on the fragile undocumented COM interface used by projects like
[MaximizeToVirtualDesktop](https://github.com/shanselman/MaximizeToVirtualDesktop).

All of this lives in `Services/WorkspacesService.cs`. The task list itself is persisted separately
at `%LOCALAPPDATA%\Microsoft\PowerToys\WorkspaceTasks\tasks.json`, so this experiment never
modifies `workspaces.json` except via the documented append-on-capture flow.

The Workspaces executables are located by probing, in order: the `POWERTOYS_WORKSPACETASKS_TOOLS_DIR`
environment variable, this app's own folder (dev builds drop into `WinUI3Apps` next to the tools),
then standard install locations. If they aren't found, task tracking still works and the
workspace features are disabled.

## Project layout

```
WorkspaceTasks/
  Program.cs                     Custom Main (single-instance), DISABLE_XAML_GENERATED_MAIN
  WorkspaceTasksXAML/            App (tray bootstrap) + MainWindow + TrayFlyoutWindow
  Models/                        WorkTask (persisted), WorkspaceSummary (read-only view)
  Services/                      ITaskStore/JsonTaskStore, IWorkspacesService/WorkspacesService,
                                 VirtualDesktopHelper
  Tray/                          TrayIcon + TrayNativeMethods (Shell_NotifyIcon interop)
  ViewModels/                    MainViewModel, WorkTaskViewModel (CommunityToolkit.Mvvm)
  Views/                         TasksPage (the full UI)
  Converters/                    Small value converters used by the XAML
  Assets/                        Page-curl.ico (tray + app icon)
```

## System tray flyout

The app starts to the **notification area** (no window shown). The tray icon is hand-rolled on the
documented `Shell_NotifyIcon` Win32 API via a message-only window (`Tray/TrayIcon.cs`), so the
experiment stays self-contained with no extra NuGet dependency:

- **Left-click** the tray icon → toggles a compact, borderless **flyout** anchored near the tray
  (`TrayFlyoutWindow`) for quick add / check-off / launch-in-new-desktop. It hides itself on focus
  loss, like a Windows 11 flyout.
- **Right-click** → context menu with **Open Workspace Tasks** (full window) and **Exit**.

The tray and app icon is `Assets/Page-curl.ico`.

## Build

```
msbuild src\modules\WorkspaceTasks\WorkspaceTasks\WorkspaceTasks.csproj -t:Restore;Build -p:Platform=x64 -p:Configuration=Debug
```

Output: `x64\Debug\WinUI3Apps\PowerToys.WorkspaceTasks.exe`.

## Known limitations (experiment scope)

- Capture grabs **all** visible windows (the app minimizes itself first, but there is no
  per-window edit step like the Workspaces Editor offers).
- The exe arguments and JSON schema are PowerToys-internal contracts and may change between
  PowerToys versions.
- No tray icon / flyout yet — that's a natural next step if the idea validates.
