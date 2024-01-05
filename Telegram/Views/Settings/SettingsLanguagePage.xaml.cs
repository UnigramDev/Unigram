//
// Copyright Fela Ameghino 2015-2024
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

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Change(e.ClickedItem as LanguagePackInfo);
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

            // TODO: no x:Bind
        }

        #endregion

        #region Binding

        private string ConvertTranslateInfo(bool enabled)
        {
            return enabled ? Strings.TranslateMessagesInfo1 : Strings.TranslateMessagesInfo1 + Environment.NewLine + Environment.NewLine + Strings.TranslateMessagesInfo2;
        }

        private Visibility ConvertDoNotTranslate(bool messages, bool chats)
        {
            return messages || chats
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        #endregion

    }
}
