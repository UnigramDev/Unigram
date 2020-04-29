using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Services;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Reflection;
using Unigram.Controls;
using Unigram.ViewModels;

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
