// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using WorkspaceTasks.Models;

namespace WorkspaceTasks.Services
{
    /// <summary>
    /// Default <see cref="IWorkspacesService"/> implementation that talks to the installed
    /// PowerToys Workspaces module via its JSON store and command-line tools.
    /// </summary>
    public sealed class WorkspacesService : IWorkspacesService
    {
        // Mirrors WorkspacesLauncher's InvokePoint enum (workspaces-common/InvokePoint.h).
        private const int InvokePointEditorButton = 0;

        private const string LauncherExeName = "PowerToys.WorkspacesLauncher.exe";
        private const string SnapshotExeName = "PowerToys.WorkspacesSnapshotTool.exe";

        private readonly string _workspacesJsonPath;
        private readonly string _tempWorkspacesJsonPath;
        private readonly string _toolsDirectory;

        public WorkspacesService()
        {
            var workspacesFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "PowerToys",
                "Workspaces");
            _workspacesJsonPath = Path.Combine(workspacesFolder, "workspaces.json");
            _tempWorkspacesJsonPath = Path.Combine(workspacesFolder, "temp-workspaces.json");

            _toolsDirectory = ResolveToolsDirectory();
        }

        public bool IsAvailable => _toolsDirectory is not null;

        public async Task<IReadOnlyList<WorkspaceSummary>> GetWorkspacesAsync()
        {
            var result = new List<WorkspaceSummary>();

            if (!File.Exists(_workspacesJsonPath))
            {
                return result;
            }

            try
            {
                var text = await File.ReadAllTextAsync(_workspacesJsonPath).ConfigureAwait(false);
                var root = JsonNode.Parse(text)?.AsObject();
                if (root is null || root["workspaces"] is not JsonArray projects)
                {
                    return result;
                }

                foreach (var node in projects)
                {
                    if (node is not JsonObject project)
                    {
                        continue;
                    }

                    var id = (string)project["id"];
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    var name = (string)project["name"] ?? string.Empty;
                    var appCount = project["applications"] is JsonArray apps ? apps.Count : 0;

                    DateTimeOffset? lastLaunched = null;
                    if (project["last-launched-time"] is JsonValue lastLaunchedValue &&
                        lastLaunchedValue.TryGetValue(out long unixSeconds) &&
                        unixSeconds > 0)
                    {
                        lastLaunched = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                    }

                    result.Add(new WorkspaceSummary(id, name, appCount, lastLaunched));
                }
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
            {
                Debug.WriteLine($"[WorkspaceTasks] Failed to read workspaces.json: {ex.Message}");
            }

            return result;
        }

        public async Task<bool> LaunchWorkspaceAsync(string workspaceId, bool inNewVirtualDesktop, string newDesktopName)
        {
            if (string.IsNullOrEmpty(workspaceId) || !IsAvailable)
            {
                return false;
            }

            var launcherPath = Path.Combine(_toolsDirectory, LauncherExeName);
            if (!File.Exists(launcherPath))
            {
                return false;
            }

            // Create + switch to a fresh desktop first, so the workspace's windows open there.
            if (inNewVirtualDesktop)
            {
                await VirtualDesktopHelper.CreateAndSwitchToNewDesktopAsync(newDesktopName).ConfigureAwait(false);
            }

            try
            {
                var startInfo = new ProcessStartInfo(launcherPath, $"{workspaceId} {InvokePointEditorButton}")
                {
                    WorkingDirectory = _toolsDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(startInfo);
                return process is not null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WorkspaceTasks] Failed to launch workspace: {ex.Message}");
                return false;
            }
        }

        public async Task<WorkspaceSummary> CaptureNewWorkspaceAsync(string name)
        {
            if (!IsAvailable)
            {
                return null;
            }

            var snapshotPath = Path.Combine(_toolsDirectory, SnapshotExeName);
            if (!File.Exists(snapshotPath))
            {
                return null;
            }

            // 1) Capture the current window layout. The snapshot tool writes a single
            //    workspace object to temp-workspaces.json (it does not touch workspaces.json).
            try
            {
                var startInfo = new ProcessStartInfo(snapshotPath)
                {
                    WorkingDirectory = _toolsDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    return null;
                }

                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WorkspaceTasks] Snapshot tool failed: {ex.Message}");
                return null;
            }

            if (!File.Exists(_tempWorkspacesJsonPath))
            {
                return null;
            }

            // 2) Read the captured project, give it a name, and commit it into workspaces.json.
            //    We manipulate the JSON nodes directly so every captured field (apps, monitors,
            //    positions, DPI data) is preserved exactly as the snapshot tool wrote it.
            try
            {
                var capturedText = await File.ReadAllTextAsync(_tempWorkspacesJsonPath).ConfigureAwait(false);
                if (JsonNode.Parse(capturedText) is not JsonObject project)
                {
                    return null;
                }

                project["name"] = name;

                var id = (string)project["id"] ?? Guid.NewGuid().ToString();
                project["id"] = id;

                var appCount = project["applications"] is JsonArray apps ? apps.Count : 0;

                JsonObject root;
                JsonArray workspaces;
                if (File.Exists(_workspacesJsonPath) &&
                    JsonNode.Parse(await File.ReadAllTextAsync(_workspacesJsonPath).ConfigureAwait(false)) is JsonObject existing &&
                    existing["workspaces"] is JsonArray existingArray)
                {
                    root = existing;
                    workspaces = existingArray;
                }
                else
                {
                    workspaces = new JsonArray();
                    root = new JsonObject { ["workspaces"] = workspaces };
                }

                workspaces.Add(project);

                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                await File.WriteAllTextAsync(_workspacesJsonPath, root.ToJsonString(options)).ConfigureAwait(false);

                return new WorkspaceSummary(id, name, appCount, null);
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException)
            {
                Debug.WriteLine($"[WorkspaceTasks] Failed to commit captured workspace: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds the directory that contains the Workspaces command-line tools, trying an
        /// explicit override first, then this app's own folder (dev builds drop alongside
        /// the tools in WinUI3Apps), then common install locations.
        /// </summary>
        private static string ResolveToolsDirectory()
        {
            foreach (var candidate in EnumerateCandidateDirectories())
            {
                if (!string.IsNullOrEmpty(candidate) &&
                    File.Exists(Path.Combine(candidate, LauncherExeName)))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateCandidateDirectories()
        {
            // Explicit override for testing / non-standard installs.
            yield return Environment.GetEnvironmentVariable("POWERTOYS_WORKSPACETASKS_TOOLS_DIR");

            // Our own folder, and a sibling WinUI3Apps folder.
            var baseDir = AppContext.BaseDirectory;
            yield return baseDir;
            yield return Path.Combine(baseDir, "WinUI3Apps");

            // Standard install locations.
            foreach (var root in new[]
            {
                Environment.GetEnvironmentVariable("ProgramW6432"),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerToys"),
            })
            {
                if (string.IsNullOrEmpty(root))
                {
                    continue;
                }

                var installRoot = root.EndsWith("PowerToys", StringComparison.OrdinalIgnoreCase)
                    ? root
                    : Path.Combine(root, "PowerToys");
                yield return installRoot;
                yield return Path.Combine(installRoot, "WinUI3Apps");
            }
        }
    }
}
