using System;
using System.ComponentModel;
using System.Reactive.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupPermissionsPage : HostedPage, ISupergroupDelegate, INavigablePage, ISearchablePage
    {
        public SupergroupPermissionsViewModel ViewModel => DataContext as SupergroupPermissionsViewModel;

        public SupergroupPermissionsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SupergroupPermissionsViewModel, ISupergroupDelegate>(this);

            InitializeTicks();

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(SearchField, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                if (string.IsNullOrWhiteSpace(SearchField.Text))
                {
                    ViewModel.Search?.Clear();
                }
                else
                {
                    ViewModel.Find(SearchField.Text);
                }
            });
        }

        private void InitializeTicks()
        {
            int j = 0;
            for (int i = 0; i < 7; i++)
            {
                var label = new TextBlock { Text = ConvertSlowModeTick(i), TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch, Style = App.Current.Resources["InfoCaptionTextBlockStyle"] as Style };
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
            Search_Click(null, null);
        }

        public void OnBackRequested(HandledEventArgs args)
        {
            if (ContentPanel.Visibility == Visibility.Collapsed)
            {
                SearchField.Text = string.Empty;
                Search_LostFocus(null, null);
                args.Handled = true;
            }
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

            ViewModel.NavigationService.Navigate(typeof(SupergroupEditRestrictedPage), state: NavigationState.GetChatMember(chat.Id, member.UserId));
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
            Footer.Text = group.IsChannel ? Strings.Resources.NoBlockedChannel : Strings.Resources.NoBlockedGroup;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            Blacklist.Badge = fullInfo.BannedCount;
            ViewModel.SlowModeDelay = fullInfo.SlowModeDelay;

            SlowmodePanel.Footer = fullInfo.SlowModeDelay > 0
                ? string.Format(Strings.Resources.SlowmodeInfoSelected, fullInfo.SlowModeDelay)
                : Strings.Resources.SlowmodeInfoOff;
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
                return Strings.Resources.SlowmodeOff;
            }
            else
            {
                if (seconds < 60)
                {
                    return string.Format(Strings.Resources.SlowmodeSeconds, seconds);
                }
                else if (seconds < 60 * 60)
                {
                    return string.Format(Strings.Resources.SlowmodeMinutes, seconds / 60);
                }
                else
                {
                    return string.Format(Strings.Resources.SlowmodeHours, seconds / 60 / 60);
                }
            }
        }

        private string ConvertSlowModeFooter(int value)
        {
            if (value == 0)
            {
                return Strings.Resources.SlowmodeInfoOff;
            }
            else
            {
                if (value < 60)
                {
                    return string.Format(Strings.Resources.SlowmodeInfoSelected, Locale.Declension("Seconds", value));
                }
                else if (value < 60 * 60)
                {
                    return string.Format(Strings.Resources.SlowmodeInfoSelected, Locale.Declension("Minutes", value / 60));
                }
                else
                {
                    return string.Format(Strings.Resources.SlowmodeInfoSelected, Locale.Declension("Hours", value / 60 / 60));
                }
            }
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

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var member = args.Item as ChatMember;

            args.ItemContainer.Tag = args.Item;
            content.Tag = args.Item;

            var user = ViewModel.ProtoService.GetUser(member.UserId);
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.GetFullName();
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = ChannelParticipantToTypeConverter.Convert(ViewModel.ProtoService, member);
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        #endregion

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            MainHeader.Visibility = Visibility.Collapsed;
            SearchField.Visibility = Visibility.Visible;

            SearchField.Focus(FocusState.Keyboard);
        }

        private void Search_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchField.Text))
            {
                MainHeader.Visibility = Visibility.Visible;
                SearchField.Visibility = Visibility.Collapsed;

                Focus(FocusState.Programmatic);
            }
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchField.Text))
            {
                ContentPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ContentPanel.Visibility = Visibility.Collapsed;
            }
        }
    }
}
