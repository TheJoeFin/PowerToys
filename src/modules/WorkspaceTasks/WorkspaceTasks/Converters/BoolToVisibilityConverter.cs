// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WorkspaceTasks.Converters
{
    /// <summary>
    /// Converts a <see cref="bool"/> to <see cref="Visibility"/>. Pass "Invert" as the
    /// converter parameter to reverse the mapping.
    /// </summary>
    public sealed partial class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var flag = value is bool b && b;
            if (IsInverted(parameter))
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var visible = value is Visibility v && v == Visibility.Visible;
            return IsInverted(parameter) ? !visible : visible;
        }

        private static bool IsInverted(object parameter) =>
            parameter is string s && string.Equals(s, "Invert", StringComparison.OrdinalIgnoreCase);
    }
}
