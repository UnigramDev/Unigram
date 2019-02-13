using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Template10.Common;
using Unigram.Common;
using Unigram.Services.Settings;
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
            DataContext = TLContainer.Current.Resolve<SettingsAppearanceViewModel>();

            ViewModel.PropertyChanged += OnPropertyChanged;

            var preview = ElementCompositionPreview.GetElementVisual(Preview);
            preview.Clip = preview.Compositor.CreateInsetClip();

            Message1.Mockup(Strings.Resources.FontSizePreviewLine1, "Lucio", Strings.Resources.FontSizePreviewReply, false, DateTime.Now.AddSeconds(-25));
            Message2.Mockup(Strings.Resources.FontSizePreviewLine2, true, DateTime.Now);

            //UpdatePreview(true);
            BackgroundPresenter.Update(ViewModel.SessionId, ViewModel.Settings, ViewModel.Aggregator);
        }

        private void Wallpaper_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsWallpaperPage));
        }

        private void NightMode_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsNightModePage));
        }

        #region Binding

        private string ConvertNightMode(NightMode mode)
        {
            return mode == NightMode.Scheduled
                ? Strings.Resources.AutoNightScheduled
                : mode == NightMode.Automatic
                ? Strings.Resources.AutoNightAutomatic
                : Strings.Resources.AutoNightDisabled;
        }

        private Visibility ConvertNightModeVisibility(NightMode mode)
        {
            return mode == NightMode.Disabled ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        private int _advanced;
        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            //_advanced++;

            //if (_advanced >= 7)
            //{
            //    Options.Opacity = 1;

            //    var flyout = new MenuFlyout();
            //    var import = new MenuFlyoutItem { Text = "Import palette" };

            //    import.Click += Import_Click;

            //    flyout.Items.Add(import);

            //    var exists = File.Exists(FileUtils.GetFileName("colors.palette"));
            //    if (exists)
            //    {
            //        var export = new MenuFlyoutItem { Text = "Export palette" };
            //        var remove = new MenuFlyoutItem { Text = "Remove palette" };

            //        export.Click += Export_Click;
            //        remove.Click += Remove_Click;

            //        flyout.Items.Add(export);
            //        flyout.Items.Add(remove);
            //    }

            //    flyout.ShowAt((Button)sender);
            //}
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            //var picker = new FileOpenPicker();
            //picker.FileTypeFilter.Add(".palette");

            //var file = await picker.PickSingleFileAsync();
            //if (file == null)
            //{
            //    return;
            //}

            //var palette = await FileUtils.CreateFileAsync("colors.palette");
            //await file.CopyAndReplaceAsync(palette);

            //Theme.Current.Update();
            ////App.NotifyThemeChanged();

            //UpdatePreview(true);
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            //var picker = new FileSavePicker();
            //picker.FileTypeChoices.Add("Palette", new[] { ".palette" });
            //picker.SuggestedFileName = "colors.palette";

            //var file = await picker.PickSaveFileAsync();
            //if (file == null)
            //{
            //    return;
            //}

            //var palette = await FileUtils.TryGetItemAsync("colors.palette");
            //if (palette == null)
            //{
            //    return;
            //}

            //await ((StorageFile)palette).CopyAndReplaceAsync(file);
        }

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            //var palette = await FileUtils.TryGetItemAsync("colors.palette");
            //if (palette == null)
            //{
            //    return;
            //}

            //await palette.DeleteAsync();

            //Theme.Current.Update();
            ////App.NotifyThemeChanged();

            //UpdatePreview(true);
        }


        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("FontSize") || e.PropertyName.Equals("RequestedTheme"))
            {
                //UpdatePreview(false);
            }
            else if (e.PropertyName.Equals("IsSystemTheme"))
            {
                //UpdatePreview(true);
            }
        }

        private void UpdatePreview(bool extended)
        {
            var current = App.Current as App;
            var theme = current.UISettings.GetColorValue(UIColorType.Background);
            var value = ViewModel.GetElementTheme();

            if (extended)
            {
                Theme.Current.Update();

                //foreach (TLWindowContext window in WindowContext.ActiveWrappers)
                //{
                //    window.UpdateTitleBar();

                //    if (window.Content is FrameworkElement element)
                //    {
                //        element.RequestedTheme = ViewModel.Settings.Appearance.RequestedTheme.HasFlag(TelegramTheme.Dark) || (ViewModel.Settings.Appearance.RequestedTheme.HasFlag(TelegramTheme.Default) && theme.R == 0 && theme.G == 0 && theme.B == 0) ? ElementTheme.Light : ElementTheme.Dark;
                //    }
                //}
            }

            foreach (TLWindowContext window in WindowContext.ActiveWrappers)
            {
                window.Dispatcher.Dispatch(() =>
                {
                    window.UpdateTitleBar();

                    if (window.Content is FrameworkElement element)
                    {
                        if (value == element.RequestedTheme)
                        {
                            element.RequestedTheme = value == ElementTheme.Dark
                                ? ElementTheme.Light
                                : ElementTheme.Dark;
                        }

                        element.RequestedTheme = value;
                    }
                });
            }
        }

        private void Switch_Click(object sender, RoutedEventArgs e)
        {
            UpdatePreview(true);
        }
    }
}
