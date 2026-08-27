# PowerToys Workspaces – Tab Sync (browser extension)

An experimental Manifest V3 browser extension (Edge / Chrome) that reads the open tabs
of the current browser window and hands them to the **PowerToys Workspaces editor**, so a
captured browser window can be saved *with its tabs* and reopened on launch.

> **Status: experiment.** This is the browser half of a larger flow. On its own it can read
> and display the current window's tabs and *attempt* to deliver them; actually receiving the
> tabs requires the native messaging host + editor changes described under
> [Remaining work](#remaining-work).

## Why an extension at all

There is no supported Windows API to read every open tab/URL of an already-running browser
window. UI Automation only reliably exposes the **active** tab's URL; the Chromium session
files (SNSS) are undocumented and lag behind the live state. A browser extension using the
`tabs` API is the only way to get **all tabs, all URLs, per window, live and reliably** — and
**Native Messaging** lets it talk to a native app over stdio **without any local HTTP server**.

## Architecture

```
Browser window (tabs)
   │  chrome.tabs.query
   ▼
Extension popup  ── "Sync URLs" ──►  background.js
                                        │  chrome.runtime.sendNativeMessage
                                        ▼
                          Native messaging host (PowerToys.exe stub)   ◄── spawned by the browser
                                        │  named pipe
                                        ▼
                          Workspaces editor  (launched if not already open)
```

Direction note: Native Messaging is always **browser-initiated** — the browser spawns the host
when the extension sends a message. That fits this flow exactly: the user clicks **Sync URLs**
in the popup, so the push originates in the browser. No persistent port or keep-alive is needed.

## Data contract

`background.js` sends the native host this JSON payload (internal pages such as `edge://`,
`chrome://`, and new-tab pages are filtered out because they can't be reopened from the
command line):

```json
{
  "type": "workspaces.tabsync",
  "version": 1,
  "browser": "msedge",
  "capturedAt": "2026-06-17T18:00:00.000Z",
  "tabs": [
    { "index": 0, "title": "Example", "url": "https://example.com", "active": true, "pinned": false }
  ]
}
```

The editor turns `tabs[].url` into the browser entry's `command-line-arguments`
(`msedge.exe https://a https://b …`), which the Workspaces launcher already replays on open.

## Load and test (developer mode)

1. Open `edge://extensions` (or `chrome://extensions`).
2. Toggle **Developer mode** on.
3. Click **Load unpacked** and select this folder
   (`src/modules/WorkspacesBrowserExtension`).
4. Pin the extension and click it. The popup lists the current window's tabs and shows a count.
5. Click **Sync URLs**. Until the native host is installed you'll see
   *"Couldn't reach the PowerToys native host…"* — that confirms the extension wiring is correct;
   the captured tabs are visible in the popup list.

Note the **Extension ID** that Edge assigns on the extensions page — the native host manifest's
`allowed_origins` must list `chrome-extension://<that-id>/`. To keep the ID stable across reloads
you can later add a `"key"` field to `manifest.json`.

## Remaining work

1. ~~**Native messaging host**~~ — **done**, see [`NativeHost/`](./NativeHost/). It receives the
   payload and writes a handoff file
   (`%LOCALAPPDATA%\Microsoft\PowerToys\Workspaces\browser-tabsync.json`) whose
   `commandLineArguments` is ready for the Workspaces `command-line-arguments` field.
2. **Editor intake** — **done** for the "editor open and editing a workspace" case. `WorkspacesEditor`
   watches the data folder (`Utils/BrowserTabSyncWatcher.cs`); when `browser-tabsync.json` changes
   while a workspace is being edited, it finds the matching browser `Application` (via
   `BaseApplication.IsEdge`/`IsChrome`) and sets its `CommandLineArguments` to the synced URLs.
   *Still pending:* launching a new workspace when **no** editor is open — that requires the native
   host to start `PowerToys.WorkspacesEditor.exe` and seed `temp-workspaces.json`.
3. **Editor UI** *(pending)*: a *Launch / focus browser* button on a captured browser entry, plus a
   clearer in-UI indication that the entry's tabs were synced (today the synced URLs simply appear in
   the entry's command-line-arguments field).
4. **Schema**: decided — reuse the existing `command-line-arguments` field on `ApplicationWrapper`.
   No schema change: the launcher already replays it as `msedge.exe <urls>`.

## A note on Edge Workspaces

Edge has its own **Workspaces** feature, and it would be ideal for the two to cooperate — but the
browser surface doesn't allow it today. Edge exposes no extension API for its workspaces (MV3 has
no workspaces concept), so the extension cannot read a workspace's identity, and there is no stable
command line to reopen a specific Edge Workspace by id. What the extension **can** read is the
**tabs** of whatever window is active — including when that window happens to be an Edge Workspace.
So PowerToys reproduces the *tabs* of the window, opened as a normal window; it does not (and
cannot, via this surface) re-bind to the live, account-synced Edge Workspace.
