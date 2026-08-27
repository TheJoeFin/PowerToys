// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinUIEx;
using WorkspaceTasks.ViewModels;

namespace WorkspaceTasks
{
    /// <summary>
    /// The application's single top-level window.
    /// </summary>
    public sealed partial class MainWindow : WindowEx
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            Title = "Workspace Tasks";

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            _viewModel = App.Current.MainViewModel;
            _viewModel.CaptureStarting += OnCaptureStarting;
            _viewModel.CaptureFinished += OnCaptureFinished;

            Closed += OnClosed;
        }

        private void OnCaptureStarting(object sender, EventArgs e)
        {
            // Minimize so this app's window is not captured into the new workspace snapshot.
            (AppWindow.Presenter as OverlappedPresenter)?.Minimize();
        }

        private void OnCaptureFinished(object sender, EventArgs e)
        {
            (AppWindow.Presenter as OverlappedPresenter)?.Restore();
            this.BringToFront();
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            _viewModel.CaptureStarting -= OnCaptureStarting;
            _viewModel.CaptureFinished -= OnCaptureFinished;
        }
    }
}
