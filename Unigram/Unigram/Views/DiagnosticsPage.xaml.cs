using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TdWindows;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DiagnosticsPage : Page
    {
        public DiagnosticsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Verbosity.Badge = Enum.GetName(typeof(VerbosityLevel), (VerbosityLevel)ApplicationSettings.Current.VerbosityLevel);

            try
            {
                var log = new FileInfo(Path.Combine(ApplicationData.Current.LocalFolder.Path, "log"));
                Log.Badge = FileSizeConverter.Convert(log.Length);
            }
            catch { }

            try
            {
                var logold = new FileInfo(Path.Combine(ApplicationData.Current.LocalFolder.Path, "log.old"));
                LogOld.Badge = FileSizeConverter.Convert(logold.Length);
            }
            catch { }
        }

        private enum VerbosityLevel
        {
            Assert = 0,
            Error = 1,
            Warning = 2,
            Info = 3,
            Debug = 4,
            Verbose = 5
        }

        private async void Verbosity_Click(object sender, RoutedEventArgs e)
        {
            var level = ApplicationSettings.Current.VerbosityLevel;

            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            var stack = new StackPanel();
            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(new RadioButton { Tag = 0, Content = "Assert", IsChecked = level == 0 });
            stack.Children.Add(new RadioButton { Tag = 1, Content = "Error", IsChecked = level == 1 });
            stack.Children.Add(new RadioButton { Tag = 2, Content = "Warning", IsChecked = level == 2 });
            stack.Children.Add(new RadioButton { Tag = 3, Content = "Info", IsChecked = level == 3 });
            stack.Children.Add(new RadioButton { Tag = 4, Content = "Debug", IsChecked = level == 4 });
            stack.Children.Add(new RadioButton { Tag = 5, Content = "Verbose", IsChecked = level == 5 });

            dialog.Title = "Verbosity Level";
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Android.OK;
            dialog.SecondaryButtonText = Strings.Android.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var newLevel = 1;
                foreach (RadioButton current in stack.Children)
                {
                    if (current.IsChecked == true)
                    {
                        newLevel = (int)current.Tag;
                        break;
                    }
                }

                ApplicationSettings.Current.VerbosityLevel = newLevel;
                TdWindows.Log.SetVerbosityLevel(newLevel);

                Verbosity.Badge = Enum.GetName(typeof(VerbosityLevel), (VerbosityLevel)ApplicationSettings.Current.VerbosityLevel);
            }
        }

        private async void Log_Click(object sender, RoutedEventArgs e)
        {
            await ShareView.GetForCurrentView().ShowAsync(new InputMessageDocument(new InputFileLocal(Path.Combine(ApplicationData.Current.LocalFolder.Path, "log")), null, null));
        }

        private async void LogOld_Click(object sender, RoutedEventArgs e)
        {
            await ShareView.GetForCurrentView().ShowAsync(new InputMessageDocument(new InputFileLocal(Path.Combine(ApplicationData.Current.LocalFolder.Path, "log.old")), null, null));
        }
    }
}
