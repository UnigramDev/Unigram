using System;
using Telegram.Td.Api;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views
{
    public sealed partial class DiagnosticsPage : Page
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

        private string ConvertSize(long size)
        {
            return FileSizeConverter.Convert(size);
        }

        #endregion

        private async void Log_Click(object sender, RoutedEventArgs e)
        {
            await ShareView.GetForCurrentView().ShowAsync(new InputMessageDocument(new InputFileLocal(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "log")), null, null));
        }

        private async void LogOld_Click(object sender, RoutedEventArgs e)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "log.old")))
            {
                await ShareView.GetForCurrentView().ShowAsync(new InputMessageDocument(new InputFileLocal(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "log.old")), null, null));
            }
        }
    }
}
