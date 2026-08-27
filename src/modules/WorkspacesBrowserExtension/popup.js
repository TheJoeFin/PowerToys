// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

const listEl = document.getElementById("tabs");
const statusEl = document.getElementById("status");
const syncButton = document.getElementById("sync");

async function renderTabs() {
    const tabs = await chrome.tabs.query({ currentWindow: true });

    listEl.replaceChildren();
    for (const tab of tabs) {
        const item = document.createElement("li");

        const title = document.createElement("div");
        title.className = "title";
        title.textContent = tab.title || tab.url || "(untitled)";

        const url = document.createElement("div");
        url.className = "url";
        url.textContent = tab.url || "";

        item.append(title, url);
        listEl.append(item);
    }

    statusEl.textContent = `${tabs.length} tab(s) in this window`;
}

syncButton.addEventListener("click", async () => {
    syncButton.disabled = true;
    statusEl.textContent = "Syncing…";

    try {
        const result = await chrome.runtime.sendMessage({ type: "syncTabs" });

        if (result?.ok) {
            const skipped = result.skipped ? ` (${result.skipped} internal page(s) skipped)` : "";
            statusEl.textContent = `Sent ${result.count} tab(s) to PowerToys Workspaces.${skipped}`;
        } else {
            statusEl.textContent =
                `Couldn't reach the PowerToys native host: ${result?.error ?? "unknown error"}. ` +
                "This is expected until the native host is installed.";
        }
    } catch (err) {
        statusEl.textContent = `Error: ${err.message}`;
    } finally {
        syncButton.disabled = false;
    }
});

renderTabs();
