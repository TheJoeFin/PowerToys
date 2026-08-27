// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;

using WorkspaceTasks.Models;

namespace WorkspaceTasks.Services
{
    /// <summary>
    /// Bridges this experiment to the PowerToys Workspaces module by reading
    /// <c>workspaces.json</c> and shelling out to the Workspaces executables.
    /// </summary>
    /// <remarks>
    /// These are PowerToys-internal contracts (a JSON file and two command-line tools),
    /// not a stable public API; behavior may change between PowerToys versions.
    /// </remarks>
    public interface IWorkspacesService
    {
        /// <summary>
        /// Gets a value indicating whether the Workspaces executables could be located,
        /// i.e. whether capture/launch are available on this machine.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Reads and returns the saved workspaces from <c>workspaces.json</c>.
        /// </summary>
        Task<IReadOnlyList<WorkspaceSummary>> GetWorkspacesAsync();

        /// <summary>
        /// Launches the workspace with the given id via <c>PowerToys.WorkspacesLauncher.exe</c>.
        /// </summary>
        /// <param name="workspaceId">The GUID of the workspace to launch.</param>
        /// <param name="inNewVirtualDesktop">
        /// When <see langword="true"/>, a new Windows virtual desktop is created and switched to
        /// before launching, so the workspace opens in a fresh working space and the current
        /// desktop is left untouched.
        /// </param>
        /// <param name="newDesktopName">
        /// Optional name for the new virtual desktop (e.g. the task title). Only used when
        /// <paramref name="inNewVirtualDesktop"/> is <see langword="true"/>.
        /// </param>
        /// <returns><see langword="true"/> if the launcher process was started.</returns>
        Task<bool> LaunchWorkspaceAsync(string workspaceId, bool inNewVirtualDesktop, string newDesktopName);

        /// <summary>
        /// Captures the current window layout via <c>PowerToys.WorkspacesSnapshotTool.exe</c>,
        /// names it, appends it to <c>workspaces.json</c>, and returns the new workspace.
        /// </summary>
        /// <param name="name">Display name for the new workspace.</param>
        /// <returns>The created workspace, or <see langword="null"/> if capture failed.</returns>
        Task<WorkspaceSummary> CaptureNewWorkspaceAsync(string name);
    }
}
