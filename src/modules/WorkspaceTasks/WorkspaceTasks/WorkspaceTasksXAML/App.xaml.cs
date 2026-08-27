// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

using Microsoft.UI.Xaml;
using WorkspaceTasks.Services;
using WorkspaceTasks.Tray;
using WorkspaceTasks.ViewModels;

namespace WorkspaceTasks
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application, IDisposable
    {
        private TrayIcon _trayIcon;
        private TrayFlyoutWindow _flyout;
        private MainWindow _mainWindow;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        public App()
        {
            InitializeComponent();

            // Simple composition root. This experiment intentionally avoids a DI container
            // to keep its dependency surface minimal; wiring is explicit and easy to follow.
            var taskStore = new JsonTaskStore();
            var workspacesService = new WorkspacesService();
            MainViewModel = new MainViewModel(taskStore, workspacesService);

            UnhandledException += App_UnhandledException;
        }

        /// <summary>
        /// Gets the shared root view model for the application session.
        /// </summary>
        public MainViewModel MainViewModel { get; }

        /// <summary>
        /// Gets the current <see cref="App"/> instance.
        /// </summary>
        public static new App Current => (App)Application.Current;

        /// <summary>
        /// Shows (creating if needed) the full task window.
        /// </summary>
        public void ShowMainWindow()
        {
            _mainWindow ??= CreateMainWindow();
            _mainWindow.Activate();
        }

        /// <inheritdoc/>
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            await MainViewModel.InitializeAsync().ConfigureAwait(true);

            // The flyout window is created up front (hidden). Keeping a live window object also
            // keeps the app's message loop running while we sit in the tray with nothing shown.
            _flyout = new TrayFlyoutWindow();

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Page-curl.ico");
            _trayIcon = new TrayIcon(iconPath, "Workspace Tasks");
            _trayIcon.LeftClicked += (_, _) => _flyout.Toggle();
            _trayIcon.OpenRequested += (_, _) => ShowMainWindow();
            _trayIcon.ExitRequested += (_, _) => ExitApp();
        }

        private MainWindow CreateMainWindow()
        {
            var window = new MainWindow();
            window.Closed += (_, _) => _mainWindow = null;
            return window;
        }

        private void ExitApp()
        {
            Dispose();
            Exit();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
            GC.SuppressFinalize(this);
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // Keep the experiment alive on non-fatal UI exceptions; surface them to the debugger.
            System.Diagnostics.Debug.WriteLine($"[WorkspaceTasks] Unhandled exception: {e.Exception}");
        }
    }
}
