//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsThemesPage : HostedPage
    {
        public SettingsThemesViewModel ViewModel => DataContext as SettingsThemesViewModel;

        public SettingsThemesPage()
        {
            InitializeComponent();
            Title = Strings.Resources.ColorThemes;
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
