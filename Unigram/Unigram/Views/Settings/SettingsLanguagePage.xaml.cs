//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Settings;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsLanguagePage : HostedPage
    {
        public SettingsLanguageViewModel ViewModel => DataContext as SettingsLanguageViewModel;

        public SettingsLanguagePage()
        {
            InitializeComponent();
            Title = Strings.Resources.Language;
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is LanguagePackInfo)
            {
                ViewModel.ChangeCommand.Execute(e.ClickedItem);
            }
        }

        #region Context menu

        private void Language_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var info = List.ItemFromContainer(element) as LanguagePackInfo;

            if (!info.IsInstalled)
            {
                return;
            }

            flyout.CreateFlyoutItem(ViewModel.DeleteCommand, info, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
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

            // Table layout
            var first = false;
            var last = false;

            if (args.Item is LanguagePackInfo info)
            {
                var list = info.IsInstalled ? ViewModel.Items.FirstOrDefault() : ViewModel.Items.LastOrDefault();
                if (list == null)
                {
                    return;
                }

                var index = list.IndexOf(info);
                first = index == 0;
                last = index == list.Count - 1;
            }

            var presenter = VisualTreeHelper.GetChild(args.ItemContainer, 0) as ListViewItemPresenter;
            if (presenter != null)
            {
                presenter.CornerRadius = new CornerRadius(first ? 8 : 0, first ? 8 : 0, last ? 8 : 0, last ? 8 : 0);
            }
        }

        #endregion

        #region Binding

        private string ConvertTranslateInfo(bool enabled)
        {
            return enabled ? Strings.Resources.TranslateMessagesInfo1 : Strings.Resources.TranslateMessagesInfo1 + Environment.NewLine + Environment.NewLine + Strings.Resources.TranslateMessagesInfo2;
        }

        #endregion

    }
}
