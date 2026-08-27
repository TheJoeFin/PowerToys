# Workspaces Tab Sync – native messaging host

The native half of the [Workspaces Tab Sync extension](../README.md). The browser spawns this
console exe on demand (Chromium **native messaging**), it reads the open-tabs payload over stdio,
and writes a handoff file the Workspaces editor can consume. **No local HTTP server.**

## Protocol

stdin/stdout, Chromium native-messaging framing: a little-endian `uint32` byte-length header
followed by that many bytes of UTF-8 JSON. stdout carries only framed responses — all diagnostics
go to `…\Workspaces\Logs\browser-sync.log`.

## What it does

1. Reads the `{ tabs: [{ url, … }] }` payload (see the extension's data contract).
2. Drops non-reopenable schemes (`edge://`, `chrome://`, new-tab/extension pages); keeps
   `http(s)`/`file`.
3. Writes `%LOCALAPPDATA%\Microsoft\PowerToys\Workspaces\browser-tabsync.json`:

   ```json
   {
     "type": "workspaces.tabsync",
     "browser": "msedge",
     "capturedAt": "2026-06-17T23:13:47Z",
     "commandLineArguments": "\"https://example.com\" \"https://bing.com\"",
     "urls": ["https://example.com", "https://bing.com"]
   }
   ```

   `commandLineArguments` is preformatted for the Workspaces `command-line-arguments` field — the
   launcher already replays that string to `msedge.exe`, opening the URLs as tabs.
4. Replies `{ "ok": true, "received": N, "handoff": "<path>" }`.

## Build

```
dotnet build src\modules\WorkspacesBrowserExtension\NativeHost\PowerToys.WorkspacesBrowserSync.csproj -c Debug -p:Platform=x64
```

Output: `bin\x64\Debug\net10.0-windows\PowerToys.WorkspacesBrowserSync.exe`. This is a standalone
experiment — it opts out of the repo build props and is **not** wired into the PowerToys installer
yet.

## Register for local testing

After loading the unpacked extension, copy its id from `edge://extensions`, then:

```powershell
.\register-dev.ps1 -ExtensionId <id-from-edge-extensions> -Browser Both
```

This fills `com.microsoft.powertoys.workspaces.template.json` into a concrete manifest and points
`HKCU\SOFTWARE\Microsoft\Edge\NativeMessagingHosts\com.microsoft.powertoys.workspaces`
(and the Chrome key) at it. Restart the browser, then click **Sync URLs** in the popup — you should
see `Sent N tab(s)…` and the handoff file appear.

## Remaining work (editor side)

The handoff file is the seam. The editor still needs to consume it — see the parent README's
[Remaining work](../README.md#remaining-work). The recommended approach reuses the existing
`command-line-arguments` field and the snapshot → `temp-workspaces.json` → `ParseTempProject()`
"new workspace" path, so no schema change is required.
