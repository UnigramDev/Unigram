using System;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsThemesPage : HostedPage
    {
        public SettingsThemesViewModel ViewModel => DataContext as SettingsThemesViewModel;

        public SettingsThemesPage()
        {
            InitializeComponent();
        }

        private async void Switch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.Tag is ThemeInfoBase info)
            {
                await ViewModel.SetThemeAsync(info);
            }
        }

        #region Context menu

        private void Theme_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var theme = element.Tag as ThemeInfoBase;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.ThemeCreateCommand, theme, Strings.Resources.CreateNewThemeMenu, new FontIcon { Glyph = Icons.Color });

            if (!theme.IsOfficial)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.ThemeShareCommand, theme, Strings.Resources.ShareFile, new FontIcon { Glyph = Icons.Share });
                flyout.CreateFlyoutItem(ViewModel.ThemeEditCommand, theme, Strings.Resources.Edit, new FontIcon { Glyph = Icons.Edit });
                flyout.CreateFlyoutItem(ViewModel.ThemeDeleteCommand, theme, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
            }

            args.ShowAt(flyout, element);
        }

        #endregion

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var theme = args.Item as ThemeInfoBase;
            var root = args.ItemContainer.ContentTemplateRoot as StackPanel;

            var radio = root.Children[0] as RadioButton;

            radio.Click -= Switch_Click;
            radio.Click += Switch_Click;

            if (theme is ThemeCustomInfo custom)
            {
                radio.IsChecked = SettingsService.Current.Appearance[SettingsService.Current.Appearance.RequestedTheme].Type == TelegramThemeType.Custom && string.Equals(SettingsService.Current.Appearance[SettingsService.Current.Appearance.RequestedTheme].Custom, custom.Path, StringComparison.OrdinalIgnoreCase);
            }
            else if (theme is ThemeAccentInfo accent)
            {
                radio.IsChecked = SettingsService.Current.Appearance[SettingsService.Current.Appearance.RequestedTheme].Type == accent.Type && SettingsService.Current.Appearance.Accents[accent.Type] == accent.AccentColor;
            }
            else
            {
                radio.IsChecked = SettingsService.Current.Appearance[SettingsService.Current.Appearance.RequestedTheme].Type == TelegramThemeType.Classic && SettingsService.Current.Appearance.RequestedTheme == theme.Parent;
            }
        }
    }
}
