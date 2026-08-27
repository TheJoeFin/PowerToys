// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace WorkspaceTasks.Models
{
    /// <summary>
    /// A lightweight, read-only view of a PowerToys Workspace as parsed from
    /// <c>workspaces.json</c>. Only the fields this experiment needs are surfaced.
    /// </summary>
    public sealed class WorkspaceSummary
    {
        public WorkspaceSummary(string id, string name, int appCount, DateTimeOffset? lastLaunched)
        {
            Id = id;
            Name = name;
            AppCount = appCount;
            LastLaunched = lastLaunched;
        }

        public string Id { get; }

        public string Name { get; }

        public int AppCount { get; }

        public DateTimeOffset? LastLaunched { get; }

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;
    }
}
