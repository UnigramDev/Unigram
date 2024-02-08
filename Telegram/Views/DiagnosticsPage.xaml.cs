//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views
{
    public sealed partial class DiagnosticsPage : HostedPage
    {
        public DiagnosticsViewModel ViewModel => DataContext as DiagnosticsViewModel;

        public DiagnosticsPage()
        {
            InitializeComponent();
            Title = "Diagnostics";
        }

        #region Binding

        private string ConvertVerbosity(VerbosityLevel level)
        {
            return Enum.GetName(typeof(VerbosityLevel), level);
        }

        private string ConvertSize(ulong size)
        {
            return FileSizeConverter.Convert((long)size);
        }

        #endregion

        private void Crash_Click(object sender, RoutedEventArgs e)
        {
            ElementCompositionPreview.GetElementVisual(null);
            return;

            if (sender is FrameworkElement element)
            {
                element.SizeChanged += (s, args) =>
                {
                    element.Height++;
                };

                element.Height = element.ActualHeight + 1;
            }
        }

        private void Bridge_Click(object sender, RoutedEventArgs e)
        {
            NotifyIcon.Debug("Message received");
        }
    }
}
