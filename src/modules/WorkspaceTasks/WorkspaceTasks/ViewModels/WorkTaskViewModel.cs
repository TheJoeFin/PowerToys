// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using CommunityToolkit.Mvvm.ComponentModel;
using WorkspaceTasks.Models;

namespace WorkspaceTasks.ViewModels
{
    /// <summary>
    /// Observable wrapper around a <see cref="WorkTask"/> for data binding.
    /// </summary>
    public sealed partial class WorkTaskViewModel : ObservableObject
    {
        private readonly WorkTask _model;

        public WorkTaskViewModel(WorkTask model)
        {
            _model = model;
        }

        /// <summary>
        /// Raised when <see cref="IsDone"/> changes, so the owner can re-bucket and persist.
        /// </summary>
        public event EventHandler DoneChanged;

        /// <summary>
        /// Raised when the linked workspace changes, so the owner can persist.
        /// </summary>
        public event EventHandler WorkspaceChanged;

        /// <summary>
        /// Gets the underlying persisted model.
        /// </summary>
        public WorkTask Model => _model;

        public string Id => _model.Id;

        public string Title
        {
            get => _model.Title;
            set => SetProperty(_model.Title, value, _model, (m, v) => m.Title = v);
        }

        public bool IsDone
        {
            get => _model.IsDone;
            set
            {
                if (SetProperty(_model.IsDone, value, _model, (m, v) =>
                {
                    m.IsDone = v;
                    m.CompletedAt = v ? DateTimeOffset.UtcNow : null;
                }))
                {
                    OnPropertyChanged(nameof(CompletedAt));
                    DoneChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string WorkspaceId => _model.WorkspaceId;

        public string WorkspaceName => _model.WorkspaceName;

        public bool HasWorkspace => !string.IsNullOrEmpty(_model.WorkspaceId);

        /// <summary>
        /// Gets a caption for the per-task workspace button: the workspace name when linked,
        /// or a prompt to link one otherwise.
        /// </summary>
        public string WorkspaceLabel => HasWorkspace ? WorkspaceName : "Link a workspace";

        public DateTimeOffset CreatedAt => _model.CreatedAt;

        public DateTimeOffset? CompletedAt => _model.CompletedAt;

        /// <summary>
        /// Links (or, with <see langword="null"/> arguments, unlinks) a PowerToys Workspace and
        /// raises <see cref="WorkspaceChanged"/> so the owner can persist the change.
        /// </summary>
        public void SetWorkspace(string id, string name)
        {
            _model.WorkspaceId = id;
            _model.WorkspaceName = name;

            OnPropertyChanged(nameof(WorkspaceId));
            OnPropertyChanged(nameof(WorkspaceName));
            OnPropertyChanged(nameof(HasWorkspace));
            OnPropertyChanged(nameof(WorkspaceLabel));

            WorkspaceChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
