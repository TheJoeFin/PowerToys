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
    /// The parsed contents of the browser tab-sync handoff file written by the native messaging host.
    /// </summary>
    public sealed class BrowserTabSyncData
    {
        public string Browser { get; init; }

        public string CommandLineArguments { get; init; }

        public IReadOnlyList<string> Urls { get; init; } = Array.Empty<string>();
    }
}
