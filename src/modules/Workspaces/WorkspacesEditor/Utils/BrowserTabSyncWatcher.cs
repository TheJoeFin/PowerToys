// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using ManagedCommon;
using WorkspacesCsharpLibrary.Utils;

namespace WorkspacesEditor.Utils
{
    /// <summary>
    /// Watches the Workspaces data folder for the <c>browser-tabsync.json</c> handoff file written by
    /// the Workspaces Tab Sync browser extension's native host, and raises <see cref="TabsSynced"/>
    /// when a fresh set of tabs arrives. Events are raised on a background thread; subscribers must
    /// marshal to the UI thread themselves.
    /// </summary>
    public sealed class BrowserTabSyncWatcher : IDisposable
    {
        private const string HandoffFileName = "browser-tabsync.json";

        private readonly FileSystemWatcher _watcher;

        public BrowserTabSyncWatcher()
        {
            string folder = FolderUtils.DataFolder();
            Directory.CreateDirectory(folder);

            _watcher = new FileSystemWatcher(folder, HandoffFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };

            _watcher.Created += OnChanged;
            _watcher.Changed += OnChanged;
            _watcher.Renamed += OnChanged;
            _watcher.EnableRaisingEvents = true;
        }

        public event EventHandler<BrowserTabSyncData> TabsSynced;

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnChanged;
            _watcher.Changed -= OnChanged;
            _watcher.Renamed -= OnChanged;
            _watcher.Dispose();
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            BrowserTabSyncData data = TryRead(e.FullPath);
            if (data != null)
            {
                TabsSynced?.Invoke(this, data);
            }
        }

        private static BrowserTabSyncData TryRead(string path)
        {
            // The host writes via a temp file + rename, but retry a few times in case we observe the
            // file while it is still briefly locked.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using JsonDocument document = JsonDocument.Parse(stream);
                    JsonElement root = document.RootElement;

                    return new BrowserTabSyncData
                    {
                        Browser = GetString(root, "browser") ?? "msedge",
                        CommandLineArguments = GetString(root, "commandLineArguments") ?? string.Empty,
                        Urls = GetStringArray(root, "urls"),
                    };
                }
                catch (IOException)
                {
                    Thread.Sleep(50);
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning($"Browser tab sync handoff file was not valid JSON: {ex.Message}");
                    return null;
                }
            }

            Logger.LogWarning("Browser tab sync handoff file remained locked; skipping this update.");
            return null;
        }

        private static string GetString(JsonElement element, string property) =>
            element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static IReadOnlyList<string> GetStringArray(JsonElement element, string property)
        {
            List<string> result = [];
            if (element.TryGetProperty(property, out JsonElement array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in array.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        result.Add(item.GetString());
                    }
                }
            }

            return result;
        }
    }
}
