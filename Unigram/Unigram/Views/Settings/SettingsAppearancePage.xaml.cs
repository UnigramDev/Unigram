using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Unigram.Common;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsAppearancePage : Page
    {
        public SettingsAppearanceViewModel ViewModel => DataContext as SettingsAppearanceViewModel;

        public SettingsAppearancePage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsAppearanceViewModel>();

            ViewModel.PropertyChanged += OnPropertyChanged;

            var preview = ElementCompositionPreview.GetElementVisual(Preview);
            preview.Clip = preview.Compositor.CreateInsetClip();
        }

        private void Wallpaper_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsWallPaperPage));
        }

        private int _advanced;
        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            _advanced++;

            if (_advanced >= 7)
            {
                Options.Opacity = 1;

                var flyout = new MenuFlyout();
                var import = new MenuFlyoutItem { Text = "Import palette" };

                import.Click += Import_Click;

                flyout.Items.Add(import);

                var exists = File.Exists(FileUtils.GetFileName("colors.palette"));
                if (exists)
                {
                    var export = new MenuFlyoutItem { Text = "Export palette" };
                    var remove = new MenuFlyoutItem { Text = "Remove palette" };

                    export.Click += Export_Click;
                    remove.Click += Remove_Click;

                    flyout.Items.Add(export);
                    flyout.Items.Add(remove);
                }

                flyout.ShowAt((Button)sender);
            }
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".palette");

            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }

            var palette = await FileUtils.CreateFileAsync("colors.palette");
            await file.CopyAndReplaceAsync(palette);

            Theme.Current.Update();
            App.NotifyThemeChanged();

            UpdatePreview(true);
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("Palette", new[] { ".palette" });
            picker.SuggestedFileName = "colors.palette";

            var file = await picker.PickSaveFileAsync();
            if (file == null)
            {
                return;
            }

            var palette = await FileUtils.TryGetItemAsync("colors.palette");
            if (palette == null)
            {
                return;
            }

            await ((StorageFile)palette).CopyAndReplaceAsync(file);
        }

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            var palette = await FileUtils.TryGetItemAsync("colors.palette");
            if (palette == null)
            {
                return;
            }

            await palette.DeleteAsync();

            Theme.Current.Update();
            App.NotifyThemeChanged();

            UpdatePreview(true);
        }


        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("FontSize") || e.PropertyName.Equals("RequestedTheme"))
            {
                UpdatePreview(false);
            }
        }

        private void UpdatePreview(bool extended)
        {
            var current = App.Current as App;
            var theme = current.UISettings.GetColorValue(UIColorType.Background);

            if (extended)
            {
                Message2.Resources.MergedDictionaries.Clear();
                Message2.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/AccentOut.xaml") });
            }

            Preview.RequestedTheme = ApplicationSettings.Current.RequestedTheme == ElementTheme.Dark || (ApplicationSettings.Current.RequestedTheme == ElementTheme.Default && theme.R == 0 && theme.G == 0 && theme.B == 0) ? ElementTheme.Light : ElementTheme.Dark;
            Preview.RequestedTheme = ApplicationSettings.Current.RequestedTheme;
        }
    }
}
