using System;
using System.ComponentModel;
using System.Reactive.Linq;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Supergroups;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupAddAdministratorPage : HostedPage
    {
        public SupergroupAddAdministratorViewModel ViewModel => DataContext as SupergroupAddAdministratorViewModel;

        public SupergroupAddAdministratorPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SupergroupAddAdministratorViewModel>();

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(SearchField, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(async x =>
            {
                var items = ViewModel.Search;
                if (items != null && string.Equals(SearchField.Text, items.Query))
                {
                    await items.LoadMoreItemsAsync(0);
                    await items.LoadMoreItemsAsync(1);
                    await items.LoadMoreItemsAsync(2);
                    await items.LoadMoreItemsAsync(3);
                }
            });
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

            if (e.ClickedItem is ChatMember member)
            {
                ViewModel.NavigationService.Navigate(typeof(SupergroupEditAdministratorPage), state: NavigationState.GetChatMember(chat.Id, member.UserId));
            }
            else if (e.ClickedItem is SearchResult result)
            {
                if (result.User is User user)
                {
                    ViewModel.NavigationService.Navigate(typeof(SupergroupEditAdministratorPage), state: NavigationState.GetChatMember(chat.Id, user.Id));
                }
                else if (result.Chat is Chat temp && temp.Type is ChatTypePrivate privata)
                {
                    ViewModel.NavigationService.Navigate(typeof(SupergroupEditAdministratorPage), state: NavigationState.GetChatMember(chat.Id, privata.UserId));
                }
            }
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var member = args.Item as ChatMember;

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

        private void Search_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                //var photo = content.Children[0] as ProfilePicture;
                //photo.Source = null;

                return;
            }

            var result = args.Item as SearchResult;
            var chat = result.Chat;
            var user = result.User ?? ViewModel.ProtoService.GetUser(chat);

            if (user == null)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            if (content == null)
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
                if (result.IsPublic)
                {
                    subtitle.Text = $"@{user.Username}";
                }
                else
                {
                    subtitle.Text = LastSeenConverter.GetLabel(user, true);
                }

                if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.TextBlock", "TextHighlighters"))
                {
                    if (subtitle.Text.StartsWith($"@{result.Query}", StringComparison.OrdinalIgnoreCase))
                    {
                        var highligher = new TextHighlighter();
                        highligher.Foreground = new SolidColorBrush(Colors.Red);
                        highligher.Background = new SolidColorBrush(Colors.Transparent);
                        highligher.Ranges.Add(new TextRange { StartIndex = 1, Length = result.Query.Length });

                        subtitle.TextHighlighters.Add(highligher);
                    }
                    else
                    {
                        subtitle.TextHighlighters.Clear();
                    }
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(Search_ContainerContentChanging);
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
                ViewModel.Search = null;
            }
            else
            {
                ContentPanel.Visibility = Visibility.Collapsed;
                ViewModel.Search = new SearchMembersAndUsersCollection(ViewModel.ProtoService, ViewModel.Chat.Id, new ChatMembersFilterMembers(), SearchField.Text);
            }
        }
    }
}
