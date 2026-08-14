//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Telegram.Converters
{
    [Flags]
    public enum ThicknessFilterKind
    {
        Left = 1 << 1,
        Top = 1 << 2,
        Right = 1 << 3,
        Bottom = 1 << 4
    }

    public partial class ThicknessFilterConverter : DependencyObject, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not Thickness thickness)
            {
                throw new NotImplementedException();
            }

            Thickness result = new();

            if (Filter.HasFlag(ThicknessFilterKind.Left))
            {
                result.Left = thickness.Left;
            }

            if (Filter.HasFlag(ThicknessFilterKind.Top))
            {
                result.Top = thickness.Top;
            }

            if (Filter.HasFlag(ThicknessFilterKind.Right))
            {
                result.Right = thickness.Right;
            }

            if (Filter.HasFlag(ThicknessFilterKind.Bottom))
            {
                result.Bottom = thickness.Bottom;
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        #region Filter

        public ThicknessFilterKind Filter
        {
            get { return (ThicknessFilterKind)GetValue(FilterProperty); }
            set { SetValue(FilterProperty, value); }
        }

        public static readonly DependencyProperty FilterProperty =
            DependencyProperty.Register(nameof(Filter), typeof(ThicknessFilterKind), typeof(ThicknessFilterConverter), new PropertyMetadata(ThicknessFilterKind.Left));

        #endregion
    }
}
