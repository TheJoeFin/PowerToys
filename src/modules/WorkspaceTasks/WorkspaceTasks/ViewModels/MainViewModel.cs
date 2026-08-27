// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkspaceTasks.Models;
using WorkspaceTasks.Services;

namespace WorkspaceTasks.ViewModels
{
    /// <summary>
    /// Root view model: owns the task lists and the workspace association/launch flows.
    /// </summary>
    public sealed partial class MainViewModel : ObservableObject
    {
        private readonly ITaskStore _taskStore;
        private readonly IWorkspacesService _workspacesService;

        private bool _isLoaded;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddTaskCommand))]
        private string _newTaskTitle = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CaptureWorkspaceCommand))]
        private string _newWorkspaceName = string.Empty;

        [ObservableProperty]
        private WorkspaceSummary _selectedWorkspace;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage;

        public MainViewModel(ITaskStore taskStore, IWorkspacesService workspacesService)
        {
            _taskStore = taskStore;
            _workspacesService = workspacesService;

            // Persist drag-to-reorder: the ListView mutates this collection in place via Move.
            ActiveTasks.CollectionChanged += OnActiveTasksCollectionChanged;
        }

        /// <summary>
        /// Raised just before a window capture so the host window can minimize itself
        /// (otherwise this app's own window would be captured into the workspace).
        /// </summary>
        public event EventHandler CaptureStarting;

        /// <summary>
        /// Raised after a window capture completes so the host window can restore itself.
        /// </summary>
        public event EventHandler CaptureFinished;

        public ObservableCollection<WorkTaskViewModel> ActiveTasks { get; } = new();

        public ObservableCollection<WorkTaskViewModel> CompletedTasks { get; } = new();

        public ObservableCollection<WorkspaceSummary> Workspaces { get; } = new();

        public bool WorkspacesAvailable => _workspacesService.IsAvailable;

        public async Task InitializeAsync()
        {
            if (_isLoaded)
            {
                return;
            }

            _isLoaded = true;

            await RefreshWorkspacesAsync().ConfigureAwait(true);

            var tasks = await _taskStore.LoadAsync().ConfigureAwait(true);

            // Preserve the persisted order so manual drag-to-reorder sticks across sessions.
            foreach (var task in tasks)
            {
                AddToBuckets(Wrap(task));
            }

            if (!WorkspacesAvailable)
            {
                StatusMessage = "PowerToys Workspaces tools were not found. Task tracking still works; workspace capture and launch are disabled.";
            }
        }

        [RelayCommand(CanExecute = nameof(CanAddTask))]
        private async Task AddTaskAsync()
        {
            var model = new WorkTask
            {
                Title = NewTaskTitle.Trim(),
                WorkspaceId = SelectedWorkspace?.Id,
                WorkspaceName = SelectedWorkspace?.DisplayName,
            };

            AddToBuckets(Wrap(model));

            NewTaskTitle = string.Empty;
            SelectedWorkspace = null;

            await SaveAsync().ConfigureAwait(true);
        }

        private bool CanAddTask() => !string.IsNullOrWhiteSpace(NewTaskTitle);

        [RelayCommand]
        private async Task DeleteTaskAsync(WorkTaskViewModel task)
        {
            if (task is null)
            {
                return;
            }

            task.DoneChanged -= OnTaskDoneChanged;
            task.WorkspaceChanged -= OnTaskWorkspaceChanged;
            ActiveTasks.Remove(task);
            CompletedTasks.Remove(task);

            await SaveAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Links an existing workspace to a task after the fact (from the full window).
        /// </summary>
        public void AssignWorkspace(WorkTaskViewModel task, WorkspaceSummary workspace)
        {
            if (task is null || workspace is null)
            {
                return;
            }

            // SetWorkspace raises WorkspaceChanged, which triggers the persist below.
            task.SetWorkspace(workspace.Id, workspace.DisplayName);
        }

        /// <summary>
        /// Removes any workspace link from a task.
        /// </summary>
        public void ClearWorkspace(WorkTaskViewModel task) => task?.SetWorkspace(null, null);

        [RelayCommand]
        private Task LaunchWorkspaceAsync(WorkTaskViewModel task) => LaunchAsync(task, inNewVirtualDesktop: false);

        [RelayCommand]
        private Task LaunchWorkspaceInNewDesktopAsync(WorkTaskViewModel task) => LaunchAsync(task, inNewVirtualDesktop: true);

        private async Task LaunchAsync(WorkTaskViewModel task, bool inNewVirtualDesktop)
        {
            if (task is null || !task.HasWorkspace)
            {
                return;
            }

            if (inNewVirtualDesktop)
            {
                StatusMessage = $"Opening “{task.WorkspaceName}” in a new virtual desktop named “{task.Title}”…";
            }

            var launched = await _workspacesService.LaunchWorkspaceAsync(task.WorkspaceId, inNewVirtualDesktop, task.Title).ConfigureAwait(true);

            if (!launched)
            {
                StatusMessage = "Could not launch the workspace. It may have been deleted from PowerToys Workspaces.";
            }
            else if (!inNewVirtualDesktop)
            {
                StatusMessage = $"Launching workspace “{task.WorkspaceName}”…";
            }
        }

        [RelayCommand]
        private async Task RefreshWorkspacesAsync()
        {
            var workspaces = await _workspacesService.GetWorkspacesAsync().ConfigureAwait(true);

            Workspaces.Clear();
            foreach (var workspace in workspaces.OrderByDescending(w => w.LastLaunched ?? DateTimeOffset.MinValue))
            {
                Workspaces.Add(workspace);
            }

            OnPropertyChanged(nameof(WorkspacesAvailable));
        }

        [RelayCommand(CanExecute = nameof(CanCaptureWorkspace))]
        private async Task CaptureWorkspaceAsync()
        {
            if (!WorkspacesAvailable)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "Capturing the current window layout…";

            CaptureStarting?.Invoke(this, EventArgs.Empty);

            // Give the host window a moment to finish minimizing before the snapshot runs.
            await Task.Delay(500).ConfigureAwait(true);

            WorkspaceSummary captured;
            try
            {
                captured = await _workspacesService.CaptureNewWorkspaceAsync(NewWorkspaceName.Trim()).ConfigureAwait(true);
            }
            finally
            {
                CaptureFinished?.Invoke(this, EventArgs.Empty);
                IsBusy = false;
            }

            if (captured is null)
            {
                StatusMessage = "Capture failed. Make sure PowerToys Workspaces is installed.";
                return;
            }

            Workspaces.Insert(0, captured);
            SelectedWorkspace = captured;
            NewWorkspaceName = string.Empty;
            StatusMessage = $"Saved workspace “{captured.DisplayName}” with {captured.AppCount} app(s).";
        }

        private bool CanCaptureWorkspace() =>
            WorkspacesAvailable && !string.IsNullOrWhiteSpace(NewWorkspaceName);

        private WorkTaskViewModel Wrap(WorkTask model)
        {
            var vm = new WorkTaskViewModel(model);
            vm.DoneChanged += OnTaskDoneChanged;
            vm.WorkspaceChanged += OnTaskWorkspaceChanged;
            return vm;
        }

        private async void OnTaskWorkspaceChanged(object sender, EventArgs e)
        {
            await SaveAsync().ConfigureAwait(true);
        }

        private async void OnActiveTasksCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                await SaveAsync().ConfigureAwait(true);
            }
        }

        private void AddToBuckets(WorkTaskViewModel task)
        {
            if (task.IsDone)
            {
                CompletedTasks.Add(task);
            }
            else
            {
                ActiveTasks.Add(task);
            }
        }

        private async void OnTaskDoneChanged(object sender, EventArgs e)
        {
            if (sender is not WorkTaskViewModel task)
            {
                return;
            }

            if (task.IsDone)
            {
                ActiveTasks.Remove(task);
                if (!CompletedTasks.Contains(task))
                {
                    CompletedTasks.Insert(0, task);
                }
            }
            else
            {
                CompletedTasks.Remove(task);
                if (!ActiveTasks.Contains(task))
                {
                    ActiveTasks.Add(task);
                }
            }

            await SaveAsync().ConfigureAwait(true);
        }

        private async Task SaveAsync()
        {
            IEnumerable<WorkTask> all = ActiveTasks.Concat(CompletedTasks).Select(vm => vm.Model);
            await _taskStore.SaveAsync(all).ConfigureAwait(true);
        }
    }
}
