// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using WinUIEx;
using WorkspaceTasks.Tray;
using WorkspaceTasks.ViewModels;

namespace WorkspaceTasks
{
    /// <summary>
    /// A compact, borderless flyout shown from the system tray for quick task access. It anchors
    /// near the notification area and hides itself when it loses focus, like a Windows 11 flyout.
    /// </summary>
    public sealed partial class TrayFlyoutWindow : WindowEx
    {
        private const int FlyoutWidthDip = 360;
        private const int FlyoutHeightDip = 540;
        private const int MarginDip = 12;

        public TrayFlyoutWindow()
        {
            ViewModel = App.Current.MainViewModel;
            InitializeComponent();

            RootPanel.DataContext = ViewModel;
            Title = "Workspace Tasks";

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, false);
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            AppWindow.IsShownInSwitchers = false;

            Activated += OnActivated;
        }

        public MainViewModel ViewModel { get; }

        /// <summary>
        /// Shows or hides the flyout depending on its current visibility.
        /// </summary>
        public void Toggle()
        {
            if (AppWindow.IsVisible)
            {
                AppWindow.Hide();
            }
            else
            {
                ShowNearTray();
            }
        }

        private void ShowNearTray()
        {
            var hwnd = this.GetWindowHandle();
            var dpi = TrayNativeMethods.GetDpiForWindow(hwnd);
            var scale = dpi == 0 ? 1.0 : dpi / 96.0;

            var width = (int)(FlyoutWidthDip * scale);
            var height = (int)(FlyoutHeightDip * scale);
            var margin = (int)(MarginDip * scale);

            AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var work = area.WorkArea;
            var x = work.X + work.Width - width - margin;
            var y = work.Y + work.Height - height - margin;
            AppWindow.Move(new Windows.Graphics.PointInt32(x, y));

            AppWindow.Show();
            Activate();
            this.BringToFront();
            QuickAddBox.Focus(FocusState.Programmatic);
        }

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                AppWindow.Hide();
            }
        }

        private void QuickAddBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && ViewModel.AddTaskCommand.CanExecute(null))
            {
                ViewModel.AddTaskCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OpenFullApp_Click(object sender, RoutedEventArgs e)
        {
            AppWindow.Hide();
            App.Current.ShowMainWindow();
        }
    }
}
