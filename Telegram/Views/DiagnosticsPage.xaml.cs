//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
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

        private void Exception_Click(object sender, RoutedEventArgs e)
        {
            ElementCompositionPreview.GetElementVisual(null);
        }

        private void Crash_Click(object sender, RoutedEventArgs e)
        {
            NativeUtils.Crash();
        }

        private void Logger_Click(object sender, RoutedEventArgs e)
        {
            Client.Execute(new AddLogMessage(0, "This should produce a stack trace"));
        }

        private void Anonymous_Click(object sender, RoutedEventArgs e)
        {
            MessageHelper.CopyText(XamlRoot, SettingsService.Current.AnonymousUserId);
        }
    }
}
