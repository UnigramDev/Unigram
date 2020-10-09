using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Folders;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Popups
{
    public sealed partial class SharePopup : ContentPopup
    {
        public ShareViewModel ViewModel => DataContext as ShareViewModel;

        private SharePopup()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<ShareViewModel>();

            //Title = Strings.Resources.ShareSendTo;
            PrimaryButtonText = Strings.Resources.Send;
            SecondaryButtonText = Strings.Resources.Close;

            ViewModel.PropertyChanged += OnPropertyChanged;

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

            if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "BottomEdgeAlignedRight"))
            {
                MenuFlyout.Placement = FlyoutPlacementMode.BottomEdgeAlignedRight;
            }
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "PreSelectedItems", StringComparison.OrdinalIgnoreCase))
            {
                ChatsPanel.SelectedItems.Clear();
                ChatsPanel.SelectedItems.AddRange(ViewModel.SelectedItems);
            }
        }

        public object Header
        {
            get => ChatsPanel.Header;
            set => ChatsPanel.Header = value;
        }

        #region Show

        private static Dictionary<int, WeakReference<SharePopup>> _windowContext = new Dictionary<int, WeakReference<SharePopup>>();
        public static SharePopup GetForCurrentView()
        {
            return new SharePopup();

            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out WeakReference<SharePopup> reference) && reference.TryGetTarget(out SharePopup value))
            {
                return value;
            }

            var context = new SharePopup();
            _windowContext[id] = new WeakReference<SharePopup>(context);

            return context;
        }

        public static async Task<Chat> PickChatAsync(string title)
        {
            var dialog = GetForCurrentView();
            dialog.ViewModel.Title = title;

            var confirm = await dialog.PickAsync(new long[0], SearchChatsType.Private, ListViewSelectionMode.Single);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            return dialog.ViewModel.SelectedItems.FirstOrDefault();
        }

        public static async Task<User> PickUserAsync(ICacheService cacheService, string title)
        {
            return cacheService.GetUser(await PickChatAsync(title));
        }

        public static async Task<IList<Chat>> PickChatsAsync(string title, long[] selected)
        {
            var dialog = GetForCurrentView();
            dialog.ViewModel.Title = title;

            var confirm = await dialog.PickAsync(selected, SearchChatsType.Private);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            return dialog.ViewModel.SelectedItems.ToList();
        }

        public static async Task<IList<User>> PickUsersAsync(ICacheService cacheService, string title)
        {
            return (await PickChatsAsync(title, new long[0]))?.Select(x => cacheService.GetUser(x)).Where(x => x != null).ToList();
        }

        public Task<ContentDialogResult> ShowAsync(DataPackageView package)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Single;
            ViewModel.SearchType = SearchChatsType.Post;
            ViewModel.IsCommentEnabled = false;
            ViewModel.IsSendAsCopyEnabled = false;
            ViewModel.IsChatSelection = false;

            ViewModel.Clear();
            ViewModel.Package = package;

            return ShowAsync();
        }

        public Task<ContentDialogResult> ShowAsync(InlineKeyboardButtonTypeSwitchInline switchInline, User bot)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Single;
            ViewModel.SearchType = SearchChatsType.Post;
            ViewModel.IsCommentEnabled = false;
            ViewModel.IsSendAsCopyEnabled = false;
            ViewModel.IsChatSelection = false;

            ViewModel.Clear();
            ViewModel.SwitchInline = switchInline;
            ViewModel.SwitchInlineBot = bot;

            return ShowAsync();
        }

        public Task<ContentDialogResult> ShowAsync(string message, bool hasUrl)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Single;
            ViewModel.SearchType = SearchChatsType.Post;
            ViewModel.IsCommentEnabled = true;
            ViewModel.IsSendAsCopyEnabled = false;
            ViewModel.IsChatSelection = false;

            ViewModel.Clear();
            ViewModel.SendMessage = message;
            ViewModel.SendMessageUrl = hasUrl;

            return ShowAsync();
        }

        public Task<ContentDialogResult> ShowAsync(Message message, bool withMyScore = false)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Multiple;
            ViewModel.SearchType = SearchChatsType.Post;
            ViewModel.IsCommentEnabled = true;
            ViewModel.IsSendAsCopyEnabled = true;
            ViewModel.IsChatSelection = false;

            ViewModel.Clear();
            ViewModel.Messages = new[] { message };
            ViewModel.IsWithMyScore = withMyScore;

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
            ViewModel.IsSendAsCopyEnabled = true;
            ViewModel.IsChatSelection = false;

            ViewModel.Clear();
            ViewModel.Messages = messages;
            ViewModel.IsWithMyScore = withMyScore;

            return ShowAsync();
        }

        public Task<ContentDialogResult> ShowAsync(Uri link, string title)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Multiple;
            ViewModel.SearchType = SearchChatsType.Post;
            ViewModel.IsCommentEnabled = true;
            ViewModel.IsSendAsCopyEnabled = false;
            ViewModel.IsChatSelection = false;

            ViewModel.Clear();
            ViewModel.ShareLink = link;
            ViewModel.ShareTitle = title;

            return ShowAsync();
        }

        public Task<ContentDialogResult> ShowAsync(InputMessageContent inputMedia)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Multiple;
            ViewModel.SearchType = SearchChatsType.Post;
            ViewModel.IsCommentEnabled = true;
            ViewModel.IsSendAsCopyEnabled = false;
            ViewModel.IsChatSelection = false;

            ViewModel.Clear();
            ViewModel.InputMedia = inputMedia;

            //if (inputMedia is TLInputMediaGame gameMedia && gameMedia.Id is TLInputGameShortName shortName)
            //{
            //    // TODO: maybe?
            //}

            return ShowAsync();
        }

        public Task<ContentDialogResult> ShowAsync(User bot, string token = null)
        {
            ChatsPanel.SelectionMode = ListViewSelectionMode.Single;
            ViewModel.SearchType = SearchChatsType.BasicAndSupergroups;
            ViewModel.IsCommentEnabled = false;
            ViewModel.IsSendAsCopyEnabled = false;
            ViewModel.IsChatSelection = false;

            ViewModel.Clear();
            ViewModel.InviteBot = bot;
            ViewModel.InviteToken = token;

            return ShowAsync();
        }

        public Task<ContentDialogResult> PickAsync(IList<long> selectedItems, SearchChatsType type, ListViewSelectionMode selectionMode = ListViewSelectionMode.Multiple)
        {
            ChatsPanel.SelectionMode = selectionMode;
            ViewModel.SearchType = type;
            ViewModel.IsCommentEnabled = false;
            ViewModel.IsSendAsCopyEnabled = false;
            ViewModel.IsChatSelection = true;

            ViewModel.Clear();
            ViewModel.PreSelectedItems = selectedItems;

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

        #region PickFiltersAsync

        public static async Task<IList<ChatFilterElement>> AddExecute(bool include, IList<ChatFilterElement> target)
        {
            //var target = new List<ChatFilterElement>();

            var flags = new List<FilterFlag>();
            if (include)
            {
                flags.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeContacts });
                flags.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeNonContacts });
                flags.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeGroups });
                flags.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeChannels });
                flags.Add(new FilterFlag { Flag = ChatListFilterFlags.IncludeBots });
            }
            else
            {
                flags.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeMuted });
                flags.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeRead });
                flags.Add(new FilterFlag { Flag = ChatListFilterFlags.ExcludeArchived });
            }

            var header = new ListView();
            header.SelectionMode = ListViewSelectionMode.Multiple;
            header.ItemsSource = flags;
            header.ItemTemplate = App.Current.Resources["FolderPickerTemplate"] as DataTemplate;
            header.ItemContainerStyle = App.Current.Resources["DefaultListViewItemStyle"] as Style;
            header.ContainerContentChanging += Header_ContainerContentChanging;

            foreach (var filter in target.OfType<FilterFlag>())
            {
                var already = flags.FirstOrDefault(x => x.Flag == filter.Flag);
                if (already != null)
                {
                    header.SelectedItems.Add(already);
                }
            }

            var panel = new StackPanel();
            panel.Children.Add(new Border
            {
                Background = App.Current.Resources["PageBackgroundDarkBrush"] as Brush,
                Child = new TextBlock
                {
                    Text = Strings.Resources.FilterChatTypes,
                    Padding = new Thickness(12, 0, 0, 0),
                    Style = App.Current.Resources["SettingsGroupTextBlockStyle"] as Style
                }
            });
            panel.Children.Add(header);
            panel.Children.Add(new Border
            {
                Background = App.Current.Resources["PageBackgroundDarkBrush"] as Brush,
                Child = new TextBlock
                {
                    Text = Strings.Resources.FilterChats,
                    Padding = new Thickness(12, 0, 0, 0),
                    Style = App.Current.Resources["SettingsGroupTextBlockStyle"] as Style
                }
            });

            var dialog = SharePopup.GetForCurrentView();
            dialog.ViewModel.Title = include ? Strings.Resources.FilterAlwaysShow : Strings.Resources.FilterNeverShow;
            dialog.ViewModel.AllowEmptySelection = true;
            dialog.Header = panel;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.IsPrimaryButtonEnabled = true;

            var confirm = await dialog.PickAsync(target.OfType<FilterChat>().Select(x => x.Chat.Id).ToArray(), SearchChatsType.All);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            target.Clear();

            foreach (var filter in header.SelectedItems.OfType<FilterFlag>())
            {
                target.Add(filter);
            }

            foreach (var chat in dialog.ViewModel.SelectedItems)
            {
                if (chat == null)
                {
                    continue;
                }

                target.Add(new FilterChat { Chat = chat });
            }

            return target;
        }

        private static void Header_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var filter = args.Item as FilterFlag;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var title = content.Children[1] as TextBlock;
            //title.Text = Enum.GetName(typeof(ChatListFilterFlags), filter.Flag);

            switch (filter.Flag)
            {
                case ChatListFilterFlags.IncludeContacts:
                    title.Text = Strings.Resources.FilterContacts;
                    break;
                case ChatListFilterFlags.IncludeNonContacts:
                    title.Text = Strings.Resources.FilterNonContacts;
                    break;
                case ChatListFilterFlags.IncludeGroups:
                    title.Text = Strings.Resources.FilterGroups;
                    break;
                case ChatListFilterFlags.IncludeChannels:
                    title.Text = Strings.Resources.FilterChannels;
                    break;
                case ChatListFilterFlags.IncludeBots:
                    title.Text = Strings.Resources.FilterBots;
                    break;

                case ChatListFilterFlags.ExcludeMuted:
                    title.Text = Strings.Resources.FilterMuted;
                    break;
                case ChatListFilterFlags.ExcludeRead:
                    title.Text = Strings.Resources.FilterRead;
                    break;
                case ChatListFilterFlags.ExcludeArchived:
                    title.Text = Strings.Resources.FilterArchived;
                    break;
            }

            var photo = content.Children[0] as ProfilePicture;
            photo.Source = PlaceholderHelper.GetGlyph(MainPage.GetFilterIcon(filter.Flag), (int)filter.Flag, 36);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = ChatsPanel.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = ChatsPanel.ItemTemplate;
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
            var chat = args.Item as Chat;

            var photo = content.Children[0] as ProfilePicture;
            var title = content.Children[1] as TextBlock;

            photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);
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
                        photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, result.Chat, 36);
                    }
                    else if (result.User != null)
                    {
                        photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, result.User, 36);
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

            photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 48);
            title.Text = ViewModel.ProtoService.GetTitle(chat, true);

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

                var items = ViewModel.Search = new SearchChatsCollection(ViewModel.ProtoService, SearchField.Text, null, null, ViewModel.SearchType);
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

            CommentPanel.Visibility = ViewModel.SelectedItems.Count > 0 && ViewModel.IsCommentEnabled ? Visibility.Visible : Visibility.Collapsed;

            IsPrimaryButtonEnabled = ViewModel.AllowEmptySelection || ViewModel.SelectedItems.Count > 0;
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
            if (index > 0)
            {
                items.Remove(chat);
                items.Insert(1, chat);
            }
            else if (items.Count > 0)
            {
                items.Insert(1, chat);
            }
            else
            {
                items.Add(chat);
            }

            if (ChatsPanel.SelectionMode == ListViewSelectionMode.Multiple)
            {
                ChatsPanel.SelectedItems.Add(chat);
            }
            else
            {
                ChatsPanel.SelectedItem = chat;
            }
        }

        private void List_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Header2.Width = e.NewSize.Width;
            DialogsSearchListView.Width = e.NewSize.Width;
            DialogsSearchListView.Height = e.NewSize.Height;
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            Window.Current.CoreWindow.CharacterReceived += OnCharacterReceived;
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
            Window.Current.CoreWindow.CharacterReceived -= OnCharacterReceived;
        }

        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            var character = System.Text.Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));
            if (character.Length == 0)
            {
                return;
            }
            else if (character != "\r" && char.IsControl(character[0]))
            {
                return;
            }
            else if (character != "\r" && char.IsWhiteSpace(character[0]))
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement();
            if (focused == null || (focused is TextBox == false && focused is RichEditBox == false))
            {
                if (IsPrimaryButtonEnabled && character == "\r")
                {
                    ViewModel.SendCommand.Execute();
                }
                else
                {
                    Search_Click(null, null);
                    SearchField.Text = character;
                    SearchField.SelectionStart = character.Length;
                }

                args.Handled = true;
            }
        }
    }
}
