//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupPermissionsPage : HostedPage, ISupergroupDelegate, ISearchablePage
    {
        public SupergroupPermissionsViewModel ViewModel => DataContext as SupergroupPermissionsViewModel;

        public SupergroupPermissionsPage()
        {
            InitializeComponent();
            Title = Strings.ChannelPermissions;

            InitializeTicks();
        }

        private void InitializeTicks()
        {
            int j = 0;
            for (int i = 0; i < 7; i++)
            {
                var label = new TextBlock { Text = ConvertSlowModeTick(i), TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch, Style = BootStrapper.Current.Resources["InfoCaptionTextBlockStyle"] as Style };
                Grid.SetColumn(label, j);

                SlowmodeTicks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                if (i < 6)
                {
                    SlowmodeTicks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                SlowmodeTicks.Children.Add(label);
                j += 2;
            }

            Grid.SetColumnSpan(Slowmode, SlowmodeTicks.ColumnDefinitions.Count);
        }

        public void Search()
        {
            SearchField.StartBringIntoView();
            SearchField.Focus(FocusState.Keyboard);
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            var member = e.ClickedItem as ChatMember;
            if (member == null)
            {
                return;
            }

            ViewModel.NavigationService.Navigate(typeof(SupergroupEditRestrictedPage), state: NavigationState.GetChatMember(chat.Id, member.MemberId));
        }

        #region Context menu

        private void Member_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
        }

        #endregion

        #region Binding

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            AddNew.Visibility = group.CanRestrictMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Text = group.IsChannel ? Strings.NoBlockedChannel : Strings.NoBlockedGroup;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            Blacklist.Badge = fullInfo.BannedCount;
            ViewModel.SlowModeDelay = fullInfo.SlowModeDelay;

            SlowmodePanel.Footer = fullInfo.SlowModeDelay > 0
                ? string.Format(Strings.SlowmodeInfoSelected, fullInfo.SlowModeDelay)
                : Strings.SlowmodeInfoOff;
        }

        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }



        private int ConvertSlowMode(int value)
        {
            switch (Math.Max(0, Math.Min(60 * 60, value)))
            {
                case 0:
                default:
                    return 0;
                case 10:
                    return 1;
                case 30:
                    return 2;
                case 60:
                    return 3;
                case 5 * 60:
                    return 4;
                case 15 * 60:
                    return 5;
                case 60 * 60:
                    return 6;
            }
        }

        private void ConvertSlowModeBack(double value)
        {
            switch (value)
            {
                case 0:
                    ViewModel.SlowModeDelay = 0;
                    break;
                case 1:
                    ViewModel.SlowModeDelay = 10;
                    break;
                case 2:
                    ViewModel.SlowModeDelay = 30;
                    break;
                case 3:
                    ViewModel.SlowModeDelay = 60;
                    break;
                case 4:
                    ViewModel.SlowModeDelay = 5 * 60;
                    break;
                case 5:
                    ViewModel.SlowModeDelay = 15 * 60;
                    break;
                case 6:
                    ViewModel.SlowModeDelay = 60 * 60;
                    break;
            }
        }

        private string ConvertSlowModeTick(double value)
        {
            var seconds = 0;
            switch (value)
            {
                case 0:
                    seconds = 0;
                    break;
                case 1:
                    seconds = 10;
                    break;
                case 2:
                    seconds = 30;
                    break;
                case 3:
                    seconds = 60;
                    break;
                case 4:
                    seconds = 5 * 60;
                    break;
                case 5:
                    seconds = 15 * 60;
                    break;
                case 6:
                    seconds = 60 * 60;
                    break;
            }

            if (seconds == 0)
            {
                return Strings.SlowmodeOff;
            }
            else
            {
                if (seconds < 60)
                {
                    return string.Format(Strings.SlowmodeSeconds, seconds);
                }
                else if (seconds < 60 * 60)
                {
                    return string.Format(Strings.SlowmodeMinutes, seconds / 60);
                }
                else
                {
                    return string.Format(Strings.SlowmodeHours, seconds / 60 / 60);
                }
            }
        }

        private string ConvertSlowModeFooter(int value)
        {
            if (value == 0)
            {
                return Strings.SlowmodeInfoOff;
            }
            else
            {
                if (value < 60)
                {
                    return string.Format(Strings.SlowmodeInfoSelected, Locale.Declension("Seconds", value));
                }
                else if (value < 60 * 60)
                {
                    return string.Format(Strings.SlowmodeInfoSelected, Locale.Declension("Minutes", value / 60));
                }
                else
                {
                    return string.Format(Strings.SlowmodeInfoSelected, Locale.Declension("Hours", value / 60 / 60));
                }
            }
        }

        private string ConvertCanSendCount(int count)
        {
            return $"{count}/9";
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
                args.ItemContainer.ContextRequested += Member_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is UserCell content)
            {
                content.UpdateSupergroupMember(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        #endregion
    }
}
