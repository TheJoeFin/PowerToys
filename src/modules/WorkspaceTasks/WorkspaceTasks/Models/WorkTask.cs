// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

namespace WorkspaceTasks.Models
{
    /// <summary>
    /// A single to-do item, optionally paired with a PowerToys Workspace.
    /// This is the serialized, persisted shape (see <see cref="Services.JsonTaskStore"/>).
    /// </summary>
    public sealed class WorkTask
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("isDone")]
        public bool IsDone { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [JsonPropertyName("completedAt")]
        public DateTimeOffset? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the id (GUID string) of the associated PowerToys Workspace, if any.
        /// This is the same id used in <c>workspaces.json</c> and accepted by
        /// <c>PowerToys.WorkspacesLauncher.exe</c>.
        /// </summary>
        [JsonPropertyName("workspaceId")]
        public string WorkspaceId { get; set; }

        /// <summary>
        /// Gets or sets a cached display name for the associated workspace, so the UI can show
        /// something meaningful even if the workspace was later removed from <c>workspaces.json</c>.
        /// </summary>
        [JsonPropertyName("workspaceName")]
        public string WorkspaceName { get; set; }
    }
}
