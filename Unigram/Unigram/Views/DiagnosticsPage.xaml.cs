using System;
using System.Collections.Generic;
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

namespace Unigram.Views
{
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
                var log = new System.IO.FileInfo(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "0", "log"));
                Log.Badge = FileSizeConverter.Convert(log.Length);
            }
            catch { }

            try
            {
                var logold = new System.IO.FileInfo(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "0", "log.old"));
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
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

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
            await ShareView.GetForCurrentView().ShowAsync(new InputMessageDocument(new InputFileLocal(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "0", "log")), null, null));
        }

        private async void LogOld_Click(object sender, RoutedEventArgs e)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "0", "log.old")))
            {
                System.IO.File.Copy(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "0", "log.old"), System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "log.old"), true);
                await ShareView.GetForCurrentView().ShowAsync(new InputMessageDocument(new InputFileLocal(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "log.old")), null, null));
            }
        }
    }
}
