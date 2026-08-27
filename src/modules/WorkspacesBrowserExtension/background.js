// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// MV3 service worker for the PowerToys Workspaces Tab Sync extension.
//
// Flow: the popup asks us to "syncTabs". We read every tab in the active browser
// window and hand the list to a native messaging host (the PowerToys side), which
// relays it to the Workspaces editor over a named pipe. We use one-shot
// sendNativeMessage: the browser spawns the host, the host delivers the payload
// (launching the editor if needed) and replies, then the port closes.

const NATIVE_HOST = "com.microsoft.powertoys.workspaces";

// URL schemes worth saving as launch arguments. Internal pages (edge://, chrome://,
// extension/new-tab pages) can't be meaningfully reopened from the command line.
const SUPPORTED_SCHEME = /^(https?|file):/i;

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
    if (message?.type === "syncTabs") {
        syncTabs()
            .then(sendResponse)
            .catch((err) => sendResponse({ ok: false, error: String(err?.message ?? err) }));

        // Keep the message channel open for the async response.
        return true;
    }

    return false;
});

async function syncTabs() {
    const tabs = await chrome.tabs.query({ currentWindow: true });
    const payload = buildPayload(tabs);
    const response = await sendNative(payload);
    return { ok: true, count: payload.tabs.length, skipped: tabs.length - payload.tabs.length, response };
}

function buildPayload(tabs) {
    const isEdge = navigator.userAgent.includes("Edg/");

    return {
        type: "workspaces.tabsync",
        version: 1,
        browser: isEdge ? "msedge" : "chrome",
        capturedAt: new Date().toISOString(),
        tabs: tabs
            .filter((t) => typeof t.url === "string" && SUPPORTED_SCHEME.test(t.url))
            .map((t) => ({
                index: t.index,
                title: t.title ?? "",
                url: t.url,
                active: Boolean(t.active),
                pinned: Boolean(t.pinned),
            })),
    };
}

function sendNative(payload) {
    return new Promise((resolve, reject) => {
        chrome.runtime.sendNativeMessage(NATIVE_HOST, payload, (response) => {
            const error = chrome.runtime.lastError;
            if (error) {
                reject(new Error(error.message));
            } else {
                resolve(response);
            }
        });
    });
}
