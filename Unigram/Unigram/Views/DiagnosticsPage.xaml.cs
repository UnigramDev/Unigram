using System;
using Telegram.Td.Api;
using Unigram.Views.Popups;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views
{
    public sealed partial class DiagnosticsPage : HostedPage
    {
        public DiagnosticsViewModel ViewModel => DataContext as DiagnosticsViewModel;

        public DiagnosticsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<DiagnosticsViewModel>();
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

        private async void Log_Click(object sender, RoutedEventArgs e)
        {
            var log = await ApplicationData.Current.LocalFolder.TryGetItemAsync("tdlib_log.txt") as StorageFile;
            if (log != null)
            {
                await SharePopup.GetForCurrentView().ShowAsync(new InputMessageDocument(new InputFileLocal(log.Path), null, null));
            }
        }

        private async void LogOld_Click(object sender, RoutedEventArgs e)
        {
            var log = await ApplicationData.Current.LocalFolder.TryGetItemAsync("tdlib_log.txt.old") as StorageFile;
            if (log != null)
            {
                await SharePopup.GetForCurrentView().ShowAsync(new InputMessageDocument(new InputFileLocal(log.Path), null, null));
            }
        }
    }
}
