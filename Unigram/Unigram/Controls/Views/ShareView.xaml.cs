using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LinqToVisualTree;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Composition;
using System.Diagnostics;
using Windows.UI.ViewManagement;
using Windows.Foundation.Metadata;
using Windows.UI;
using Template10.Utils;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Unigram.Common;
using Unigram.Converters;
using Windows.UI.Core;
using Telegram.Td.Api;
using Windows.UI.Xaml.Documents;
using Unigram.Collections;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Unigram.Controls.Cells;
using System.Reactive.Linq;
using Unigram.Core.Common;
using System.Threading.Tasks;

namespace Unigram.Controls.Views
{
    public sealed partial class ShareView : ContentDialog
    {
        public ShareViewModel ViewModel => DataContext as ShareViewModel;

        private ShareView()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<ShareViewModel>();

            //Title = Strings.Resources.ShareSendTo;
            PrimaryButtonText = Strings.Resources.Send;
            SecondaryButtonText = Strings.Resources.Close;

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(SearchField, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(async x =>
            {
                var items = ViewModel.Search;
                if (items != null && string.Equals(SearchField.Text, items.Query))
                {
                    await items.LoadMoreItemsAsync(2);
                    await items.LoadMoreItemsAsync(3);
                }
            });
        }

        #region Show

        private static Dictionary<int, WeakReference<ShareView>> _windowContext = new Dictionary<int, WeakReference<ShareView>>();
        public static ShareView GetForCurrentView()
        {
            return new ShareView();

            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out WeakReference<ShareView> reference) && reference.TryGetTarget(out ShareView value))
            {
                return value;
            }

            var context = new ShareView();
            _windowContext[id] = new WeakReference<ShareView>(context);

            return context;
        }

        public Task<ContentDialogResult> ShowAsync(InlineKeyboardButtonTypeSwitchInline switchInline, User bot)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Single;
            ViewModel.SearchType = SearchChatsType.BasicAndSupergroups;
            ViewModel.IsCommentEnabled = false;

            ViewModel.SwitchInline = switchInline;
            ViewModel.SwitchInlineBot = bot;

            ViewModel.SendMessage = null;
            ViewModel.SendMessageUrl = false;
            ViewModel.Comment = null;
            ViewModel.ShareLink = null;
            ViewModel.ShareTitle = null;
            ViewModel.Messages = null;
            ViewModel.InviteBot = null;
            ViewModel.InputMedia = null;
            ViewModel.IsWithMyScore = false;

            return ShowAsync();
        }

        public Task<ContentDialogResult> ShowAsync(string message, bool hasUrl)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Single;
            ViewModel.SearchType = SearchChatsType.Post;
            ViewModel.IsCommentEnabled = true;

            ViewModel.SendMessage = message;
            ViewModel.SendMessageUrl = hasUrl;

            ViewModel.SwitchInline = null;
            ViewModel.SwitchInlineBot = null;
            ViewModel.Comment = null;
            ViewModel.ShareLink = null;
            ViewModel.ShareTitle = null;
            ViewModel.Messages = null;
            ViewModel.InviteBot = null;
            ViewModel.InputMedia = null;
            ViewModel.IsWithMyScore = false;

            return ShowAsync();
        }

        public Task<ContentDialogResult> ShowAsync(Message message, bool withMyScore = false)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Multiple;
            ViewModel.SearchType = SearchChatsType.Post;
            ViewModel.IsCommentEnabled = true;

            ViewModel.Messages = new[] { message };
            ViewModel.IsWithMyScore = withMyScore;

            ViewModel.SwitchInline = null;
            ViewModel.SwitchInlineBot = null;
            ViewModel.SendMessage = null;
            ViewModel.SendMessageUrl = false;
            ViewModel.Comment = null;
            ViewModel.ShareLink = null;
            ViewModel.ShareTitle = null;
            ViewModel.InviteBot = null;
            ViewModel.InputMedia = null;

            var chat = ViewModel.ProtoService.GetChat(message.ChatId);
            if (chat != null && chat.Type is ChatTypeSupergroup super && super.IsChannel && ViewModel.ProtoService.GetSupergroup(super.SupergroupId) is Supergroup supergroup && supergroup.Username.Length > 0)
            {
                var link = $"{supergroup.Username}/{message.Id}";

                if (message.Content is MessageVideoNote)
                {
                    link = $"https://telesco.pe/{link}";
                }
                else
                {
                    link = MeUrlPrefixConverter.Convert(ViewModel.ProtoService, link);
                }

                var title = message.Content.GetCaption()?.Text;
                if (message.Content is MessageText text)
                {
                    title = text.Text.Text;
                }

                ViewModel.ShareLink = new Uri(link);
                ViewModel.ShareTitle = title ?? ViewModel.ProtoService.GetTitle(chat);
            }
            else if (message.Content is MessageGame game)
            {
                var viaBot = ViewModel.ProtoService.GetUser(message.ViaBotUserId);
                if (viaBot != null && viaBot.Username.Length > 0)
                {
                    ViewModel.ShareLink = new Uri(MeUrlPrefixConverter.Convert(ViewModel.ProtoService, $"{viaBot.Username}?game={game.Game.ShortName}"));
                    ViewModel.ShareTitle = game.Game.Title;
                }
            }

            return ShowAsync();
        }

        public Task<ContentDialogResult> ShowAsync(IList<Message> messages, bool withMyScore = false)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Multiple;
            ViewModel.SearchType = SearchChatsType.Post;
            ViewModel.IsCommentEnabled = true;

            ViewModel.Messages = messages;
            ViewModel.IsWithMyScore = withMyScore;

            ViewModel.SwitchInline = null;
            ViewModel.SwitchInlineBot = null;
            ViewModel.SendMessage = null;
            ViewModel.SendMessageUrl = false;
            ViewModel.Comment = null;
            ViewModel.ShareLink = null;
            ViewModel.ShareTitle = null;
            ViewModel.InviteBot = null;
            ViewModel.InputMedia = null;

            return ShowAsync();
        }

        public Task<ContentDialogResult> ShowAsync(Uri link, string title)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Multiple;
            ViewModel.SearchType = SearchChatsType.Post;
            ViewModel.IsCommentEnabled = true;

            ViewModel.ShareLink = link;
            ViewModel.ShareTitle = title;

            ViewModel.SwitchInline = null;
            ViewModel.SwitchInlineBot = null;
            ViewModel.SendMessage = null;
            ViewModel.SendMessageUrl = false;
            ViewModel.Comment = null;
            ViewModel.Messages = null;
            ViewModel.InviteBot = null;
            ViewModel.InputMedia = null;
            ViewModel.IsWithMyScore = false;

            return ShowAsync();
        }

        public Task<ContentDialogResult> ShowAsync(InputMessageContent inputMedia)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Multiple;
            ViewModel.SearchType = SearchChatsType.Post;
            ViewModel.IsCommentEnabled = true;

            ViewModel.InputMedia = inputMedia;

            ViewModel.SwitchInline = null;
            ViewModel.SwitchInlineBot = null;
            ViewModel.SendMessage = null;
            ViewModel.SendMessageUrl = false;
            ViewModel.Comment = null;
            ViewModel.ShareLink = null;
            ViewModel.ShareTitle = null;
            ViewModel.Messages = null;
            ViewModel.InviteBot = null;
            ViewModel.IsWithMyScore = false;

            //if (inputMedia is TLInputMediaGame gameMedia && gameMedia.Id is TLInputGameShortName shortName)
            //{
            //    // TODO: maybe?
            //}

            return ShowAsync();
        }

        public Task<ContentDialogResult> ShowAsync(User bot)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Single;
            ViewModel.SearchType = SearchChatsType.BasicAndSupergroups;
            ViewModel.IsCommentEnabled = false;

            ViewModel.InviteBot = bot;

            ViewModel.SwitchInline = null;
            ViewModel.SwitchInlineBot = null;
            ViewModel.SendMessage = null;
            ViewModel.SendMessageUrl = false;
            ViewModel.Comment = null;
            ViewModel.ShareLink = null;
            ViewModel.ShareTitle = null;
            ViewModel.Messages = null;
            ViewModel.IsWithMyScore = false;

            return ShowAsync();
        }

        private new Task<ContentDialogResult> ShowAsync()
        {
            ViewModel.Items.Clear();

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                Loaded -= handler;
                await ViewModel.OnNavigatedToAsync(null, NavigationMode.New, null);
            });

            Loaded += handler;
            return this.ShowQueuedAsync();
        }

        #endregion

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as StackPanel;
            var chat = args.Item as Chat;

            var photo = content.Children[0] as ProfilePicture;
            var title = content.Children[1] as TextBlock;

            photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 48, 48);
            title.Text = ViewModel.ProtoService.GetTitle(chat);
        }

        private void DialogsSearchListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is SearchResult result)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;
                if (content == null)
                {
                    return;
                }

                if (args.Phase == 0)
                {
                    var grid = content.Children[1] as Grid;

                    var title = grid.Children[0] as TextBlock;
                    if (result.Chat != null)
                    {
                        title.Text = ViewModel.ProtoService.GetTitle(result.Chat);
                    }
                    else if (result.User != null)
                    {
                        title.Text = result.User.GetFullName();
                    }

                    var verified = grid.Children[1] as FrameworkElement;

                    if (result.User != null || result.Chat.Type is ChatTypePrivate || result.Chat.Type is ChatTypeSecret)
                    {
                        var user = result.User ?? ViewModel.ProtoService.GetUser(result.Chat);
                        verified.Visibility = user != null && user.IsVerified ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else if (result.Chat != null && result.Chat.Type is ChatTypeSupergroup supergroup)
                    {
                        var group = ViewModel.ProtoService.GetSupergroup(supergroup.SupergroupId);
                        verified.Visibility = group != null && group.IsVerified ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        verified.Visibility = Visibility.Collapsed;
                    }
                }
                else if (args.Phase == 1)
                {
                    var subtitle = content.Children[2] as TextBlock;
                    if (result.User != null || result.Chat != null && result.Chat.Type is ChatTypePrivate privata)
                    {
                        var user = result.User ?? ViewModel.ProtoService.GetUser(result.Chat);
                        if (result.IsPublic)
                        {
                            subtitle.Text = $"@{user.Username}";
                        }
                        else
                        {
                            subtitle.Text = LastSeenConverter.GetLabel(user, true);
                        }
                    }
                    else if (result.Chat != null && result.Chat.Type is ChatTypeSupergroup super)
                    {
                        var supergroup = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                        if (result.IsPublic)
                        {
                            if (supergroup.MemberCount > 0)
                            {
                                subtitle.Text = string.Format("@{0}, {1}", supergroup.Username, Locale.Declension(supergroup.IsChannel ? "Subscribers" : "Members", supergroup.MemberCount));
                            }
                            else
                            {
                                subtitle.Text = $"@{supergroup.Username}";
                            }
                        }
                        else if (supergroup.MemberCount > 0)
                        {
                            subtitle.Text = Locale.Declension(supergroup.IsChannel ? "Subscribers" : "Members", supergroup.MemberCount);
                        }
                        else
                        {
                            subtitle.Text = string.Empty;
                        }
                    }
                    else
                    {
                        subtitle.Text = string.Empty;
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
                    if (result.Chat != null)
                    {
                        photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, result.Chat, 36, 36);
                    }
                    else if (result.User != null)
                    {
                        photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, result.User, 36, 36);
                    }
                }

                if (args.Phase < 2)
                {
                    args.RegisterUpdateCallback(DialogsSearchListView_ContainerContentChanging);
                }
            }

            args.Handled = true;
        }

        private void TopChats_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as StackPanel;
            var chat = args.Item as Chat;

            var grid = content.Children[0] as Grid;

            var photo = grid.Children[0] as ProfilePicture;
            var title = content.Children[1] as TextBlock;

            if (chat.Type is ChatTypePrivate privata && privata.UserId == ViewModel.ProtoService.GetMyId())
            {
                photo.Source = PlaceholderHelper.GetChat(null, chat, 48, 48);
                title.Text = Strings.Resources.SavedMessages;
            }
            else
            {
                photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 48, 48);
                title.Text = ViewModel.ProtoService.GetTitle(chat, true);
            }

            var badge = grid.Children[1] as Border;
            var text = badge.Child as TextBlock;

            badge.Visibility = chat.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            text.Text = chat.UnreadCount.ToString();
        }

        #endregion

        #region Search

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            MainHeader.Visibility = Visibility.Collapsed;
            SearchField.Visibility = Visibility.Visible;

            SearchField.Focus(FocusState.Keyboard);
        }

        private void Search_GotFocus(object sender, RoutedEventArgs e)
        {
            Search_TextChanged(null, null);
        }

        private void Search_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchField.Text))
            {
                MainHeader.Visibility = Visibility.Visible;
                SearchField.Visibility = Visibility.Collapsed;

                this.Focus(FocusState.Programmatic);
            }

            Search_TextChanged(null, null);
        }

        private async void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Unfocused && string.IsNullOrEmpty(SearchField.Text))
            {
                DialogsSearchListView.Visibility = Visibility.Collapsed;

                ViewModel.TopChats = null;
                ViewModel.Search = null;
            }
            else if (SearchField.FocusState != FocusState.Unfocused)
            {
                DialogsSearchListView.Visibility = Visibility.Visible;

                if (string.IsNullOrEmpty(SearchField.Text))
                {
                    var top = ViewModel.TopChats = new TopChatsCollection(ViewModel.ProtoService, new TopChatCategoryUsers(), 30);
                    await top.LoadMoreItemsAsync(0);
                }
                else
                {
                    ViewModel.TopChats = null;
                }

                var items = ViewModel.Search = new SearchChatsCollection(ViewModel.ProtoService, SearchField.Text, ViewModel.SearchType);
                await items.LoadMoreItemsAsync(0);
                await items.LoadMoreItemsAsync(1);
            }
        }

        private void Search_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var activePanel = ChatsPanel;
            var activeList = DialogsSearchListView;
            var activeResults = ChatsResults;

            if (activePanel.Visibility == Visibility.Visible)
            {
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Up || e.Key == Windows.System.VirtualKey.Down)
            {
                var index = e.Key == Windows.System.VirtualKey.Up ? -1 : 1;
                var next = activeList.SelectedIndex + index;
                if (next >= 0 && next < activeResults.View.Count)
                {
                    activeList.SelectedIndex = next;
                    activeList.ScrollIntoView(activeList.SelectedItem);
                }

                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var index = Math.Max(activeList.SelectedIndex, 0);
                var container = activeList.ContainerFromIndex(index) as ListViewItem;
                if (container != null)
                {
                    var peer = new ListViewItemAutomationPeer(container);
                    var invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    invokeProv.Invoke();
                }

                e.Handled = true;
            }
        }

        #endregion

        #region Comment

        private Visibility ConvertCommentVisibility(int count, bool enabled)
        {
            return count > 0 && enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.SelectedItems = new MvxObservableCollection<Chat>(ChatsPanel.SelectedItems.Cast<Chat>());
            Subtitle.Visibility = ViewModel.SelectedItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            Subtitle.Text = string.Join(", ", ViewModel.SelectedItems.Select(x => ViewModel.CacheService.GetTitle(x)));

            IsPrimaryButtonEnabled = ViewModel.SelectedItems.Count > 0;
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem;
            if (item is SearchResult result)
            {
                if (result.Chat != null)
                {
                    item = result.Chat;
                    ViewModel.ProtoService.Send(new AddRecentlyFoundChat(result.Chat.Id));
                }
                else
                {
                    item = result.User;
                }
            }

            if (item is User user)
            {
                var response = await ViewModel.ProtoService.SendAsync(new CreatePrivateChat(user.Id, false));
                if (response is Chat)
                {
                    item = response as Chat;
                }
            }

            var chat = item as Chat;
            if (chat == null)
            {
                return;
            }

            chat = ViewModel.Items.FirstOrDefault(x => x.Id == chat.Id) ?? chat;
            SearchField.Text = string.Empty;

            var items = ViewModel.Items;
            var selectedItems = ViewModel.SelectedItems;

            var index = items.IndexOf(chat);
            if (index > -1)
            {
                if (index > 0)
                {
                    items.Remove(chat);
                    items.Insert(1, chat);
                }
            }
            else
            {
                items.Insert(1, chat);
            }


            ChatsPanel.SelectedItems.Add(chat);
        }

        private void List_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Header.Width = e.NewSize.Width;
            DialogsSearchListView.Width = e.NewSize.Width;
            DialogsSearchListView.Height = e.NewSize.Height;
        }
    }
}
