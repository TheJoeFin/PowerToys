// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WorkspaceTasks.Models;
using WorkspaceTasks.ViewModels;

namespace WorkspaceTasks.Views
{
    /// <summary>
    /// Hosts the task list and workspace association UI.
    /// </summary>
    public sealed partial class TasksPage : UserControl
    {
        private WorkTaskViewModel _editingTask;
        private Flyout _openPickerFlyout;

        public TasksPage()
        {
            ViewModel = App.Current.MainViewModel;
            DataContext = ViewModel;
            InitializeComponent();
        }

        public MainViewModel ViewModel { get; }

        private void NewTaskBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && ViewModel.AddTaskCommand.CanExecute(null))
            {
                ViewModel.AddTaskCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void WorkspaceCombo_DropDownOpened(object sender, object e)
        {
            // Pull a fresh list whenever the user goes to pick a workspace.
            if (ViewModel.RefreshWorkspacesCommand.CanExecute(null))
            {
                ViewModel.RefreshWorkspacesCommand.Execute(null);
            }
        }

        private void OpenWorkspacePicker_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            _editingTask = button.DataContext as WorkTaskViewModel;
            _openPickerFlyout = button.Flyout as Flyout;

            // The picker's ListView binds to ViewModel.Workspaces; supply that context and refresh.
            if (_openPickerFlyout?.Content is FrameworkElement content)
            {
                content.DataContext = ViewModel;
            }

            if (ViewModel.RefreshWorkspacesCommand.CanExecute(null))
            {
                ViewModel.RefreshWorkspacesCommand.Execute(null);
            }
        }

        private void WorkspacePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_editingTask is null || e.AddedItems.Count == 0)
            {
                return;
            }

            var list = (ListView)sender;
            var workspace = e.AddedItems[0] as WorkspaceSummary;

            // Clear the selection so reopening the picker starts clean and doesn't re-fire.
            list.SelectedItem = null;

            ViewModel.AssignWorkspace(_editingTask, workspace);
            _openPickerFlyout?.Hide();
        }

        private void UnlinkWorkspace_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearWorkspace(_editingTask);
            _openPickerFlyout?.Hide();
        }
    }
}
