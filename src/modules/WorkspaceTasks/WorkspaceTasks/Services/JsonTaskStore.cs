// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using WorkspaceTasks.Models;

namespace WorkspaceTasks.Services
{
    /// <summary>
    /// Stores tasks as JSON under
    /// <c>%LOCALAPPDATA%\Microsoft\PowerToys\WorkspaceTasks\tasks.json</c>.
    /// The folder is separate from the Workspaces module so this experiment never
    /// risks corrupting <c>workspaces.json</c>.
    /// </summary>
    public sealed class JsonTaskStore : ITaskStore, IDisposable
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
        };

        private readonly string _filePath;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public JsonTaskStore()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "PowerToys",
                "WorkspaceTasks");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "tasks.json");
        }

        public async Task<IReadOnlyList<WorkTask>> LoadAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!File.Exists(_filePath))
                {
                    return Array.Empty<WorkTask>();
                }

                await using var stream = File.OpenRead(_filePath);
                var tasks = await JsonSerializer.DeserializeAsync<List<WorkTask>>(stream).ConfigureAwait(false);
                return tasks ?? new List<WorkTask>();
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkspaceTasks] Failed to load tasks: {ex.Message}");
                return Array.Empty<WorkTask>();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task SaveAsync(IEnumerable<WorkTask> tasks)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var list = tasks.ToList();
                var tempPath = _filePath + ".tmp";

                await using (var stream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(stream, list, SerializerOptions).ConfigureAwait(false);
                }

                // Atomic-ish replace so a crash mid-write can't truncate the real file.
                File.Copy(tempPath, _filePath, overwrite: true);
                File.Delete(tempPath);
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose()
        {
            _gate.Dispose();
        }
    }
}
