//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsLanguagePage : HostedPage
    {
        public SettingsLanguageViewModel ViewModel => DataContext as SettingsLanguageViewModel;

        public SettingsLanguagePage()
        {
            InitializeComponent();
            Title = Strings.Language;
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton { Tag: LanguagePackInfo language })
            {
                ViewModel.Change(language);
            }
        }

        #region Context menu

        private void Language_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var info = ScrollingHost.ItemFromContainer(sender) as LanguagePackInfo;
            if (info.IsInstalled is false)
            {
                return;
            }

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.Delete, info, Strings.Delete, Icons.Delete, destructive: true);
            flyout.ShowAt(sender, args);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Language_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is RadioButton content && args.Item is LanguagePackInfo language)
            {
                content.Checked -= RadioButton_Checked;

                // Justified because Checked
                content.Tag = language;
                content.IsChecked = language == ViewModel.SelectedItem;

                content.Checked += RadioButton_Checked;

                var grid = content.Content as Grid;
                if (grid != null)
                {
                    var nativeName = grid.Children[0] as TextBlock;
                    var name = grid.Children[1] as TextBlock;

                    nativeName.Text = language.NativeName;
                    name.Text = language.Name;
                }

                args.Handled = true;
            }
        }

        #endregion

        #region Binding

        private Visibility ConvertDoNotTranslate(bool messages, bool chats)
        {
            return messages || chats
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        #endregion
    }
}
