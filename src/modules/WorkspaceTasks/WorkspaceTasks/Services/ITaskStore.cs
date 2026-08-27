// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;

using WorkspaceTasks.Models;

namespace WorkspaceTasks.Services
{
    /// <summary>
    /// Persists the user's task list.
    /// </summary>
    public interface ITaskStore
    {
        Task<IReadOnlyList<WorkTask>> LoadAsync();

        Task SaveAsync(IEnumerable<WorkTask> tasks);
    }
}
