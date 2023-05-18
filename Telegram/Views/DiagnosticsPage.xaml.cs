//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.AppCenter.Crashes;
using System;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views.Popups;
using Windows.Storage;
using Windows.UI.Xaml;

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

        private async void Calls_Click(object sender, RoutedEventArgs e)
        {
            var log = await ApplicationData.Current.LocalFolder.TryGetItemAsync("tgcalls.txt") as StorageFile;
            if (log != null)
            {
                await new ChooseChatsPopup().ShowAsync(new InputMessageDocument(new InputFileLocal(log.Path), null, true, null));
            }
        }

        private async void GroupCalls_Click(object sender, RoutedEventArgs e)
        {
            var log = await ApplicationData.Current.LocalFolder.TryGetItemAsync("tgcalls_group.txt") as StorageFile;
            if (log != null)
            {
                await new ChooseChatsPopup().ShowAsync(new InputMessageDocument(new InputFileLocal(log.Path), null, true, null));
            }
        }

        private async void Log_Click(object sender, RoutedEventArgs e)
        {
            var log = await ApplicationData.Current.LocalFolder.TryGetItemAsync("tdlib_log.txt") as StorageFile;
            if (log != null)
            {
                await new ChooseChatsPopup().ShowAsync(new InputMessageDocument(new InputFileLocal(log.Path), null, true, null));
            }
        }

        private async void LogOld_Click(object sender, RoutedEventArgs e)
        {
            var log = await ApplicationData.Current.LocalFolder.TryGetItemAsync("tdlib_log.txt.old") as StorageFile;
            if (log != null)
            {
                await new ChooseChatsPopup().ShowAsync(new InputMessageDocument(new InputFileLocal(log.Path), null, true, null));
            }
        }

        private async void Dump_Click(object sender, RoutedEventArgs e)
        {
            await new ChooseChatsPopup().ShowAsync(new FormattedText(Logger.Dump(), Array.Empty<TextEntity>()));
        }

        private void Crash_Click(object sender, RoutedEventArgs e)
        {
            throw new TestCrashException();
        }
    }
}
