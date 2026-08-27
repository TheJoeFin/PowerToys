// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WorkspaceTasks.Converters
{
    /// <summary>
    /// Collapses an element when the bound string is null or empty; otherwise shows it.
    /// </summary>
    public sealed partial class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) =>
            string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }
}
