//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsThemesPage : HostedPage
    {
        public SettingsThemesViewModel ViewModel => DataContext as SettingsThemesViewModel;

        public SettingsThemesPage()
        {
            InitializeComponent();
            Title = Strings.ColorThemes;
        }

        private async void Switch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.Tag is ThemeInfoBase info)
            {
                await ViewModel.SetThemeAsync(info);
            }
        }

        #region Binding

        private SolidColorBrush ConvertAccent(IList<ThemeAccentInfo> accents, int index)
        {
            if (accents != null && accents.Count > index)
            {
                return new SolidColorBrush(accents[index].SelectionColor);
            }

            return null;
        }

        #endregion

        #region Context menu

        private void Theme_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var theme = element.Tag as ThemeInfoBase;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.CreateTheme, theme, Strings.CreateNewThemeMenu, Icons.Color);

            if (theme is ThemeCustomInfo custom)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.ShareTheme, custom, Strings.ShareFile, Icons.Share);
                flyout.CreateFlyoutItem(ViewModel.EditTheme, custom, Strings.Edit, Icons.Edit);
                flyout.CreateFlyoutItem(ViewModel.DeleteTheme, custom, Strings.Delete, Icons.Delete, destructive: true);
            }

            flyout.ShowAt(sender, args);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Theme_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var theme = args.Item as ThemeInfoBase;
            var radio = args.ItemContainer.ContentTemplateRoot as RadioButton;

            if (args.ItemContainer.ContentTemplateRoot is StackPanel root)
            {
                radio = root.Children[0] as RadioButton;
            }

            radio.Click -= Switch_Click;
            radio.Click += Switch_Click;

            if (theme is ThemeCustomInfo custom)
            {
                radio.RequestedTheme = custom.Parent == TelegramTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                radio.IsChecked = SettingsService.Current.Appearance[SettingsService.Current.Appearance.RequestedTheme].Type == TelegramThemeType.Custom && string.Equals(SettingsService.Current.Appearance[SettingsService.Current.Appearance.RequestedTheme].Custom, custom.Path, StringComparison.OrdinalIgnoreCase);
            }
            else if (theme is ThemeAccentInfo accent)
            {
                radio.RequestedTheme = accent.Parent == TelegramTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                radio.IsChecked = SettingsService.Current.Appearance[SettingsService.Current.Appearance.RequestedTheme].Type == accent.Type && SettingsService.Current.Appearance.Accents[accent.Type] == accent.AccentColor;
            }
            else
            {
                radio.RequestedTheme = theme.Parent == TelegramTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                radio.IsChecked = SettingsService.Current.Appearance[SettingsService.Current.Appearance.RequestedTheme].Type == TelegramThemeType.Classic && SettingsService.Current.Appearance.RequestedTheme == theme.Parent;
            }

            args.ItemContainer.Tag = args.Item;
        }

        #endregion

        private void Theme_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (sender is RadioButton radio && args.NewValue is ThemeInfoBase theme)
            {
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
}
