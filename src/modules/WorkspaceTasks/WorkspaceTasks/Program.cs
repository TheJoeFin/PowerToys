// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

namespace WorkspaceTasks
{
    /// <summary>
    /// Custom entry point. We disable the XAML-generated Main (see DISABLE_XAML_GENERATED_MAIN)
    /// so we can enforce single-instance behavior before the WinUI application is created.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            var instanceKey = AppInstance.FindOrRegisterForKey("PowerToys_WorkspaceTasks_Instance");

            if (instanceKey.IsCurrent)
            {
                Microsoft.UI.Xaml.Application.Start((p) =>
                {
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App();
                });
            }
        }
    }
}
