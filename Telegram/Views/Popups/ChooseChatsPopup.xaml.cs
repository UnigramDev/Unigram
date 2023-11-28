//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Folders;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Popups
{
    #region Options

    public record ChooseChatsOptions
    {
        public bool AllowAll => AllowChannelChats && AllowGroupChats && AllowBotChats && AllowUserChats && AllowSecretChats && AllowSelf && !CanPostMessages && !CanInviteUsers && !CanShareContact;

        public bool AllowChannelChats { get; set; } = true;
        public bool AllowGroupChats { get; set; } = true;
        public bool AllowBotChats { get; set; } = true;
        public bool AllowUserChats { get; set; } = true;
        public bool AllowSecretChats { get; set; } = true;

        public bool AllowSelf { get; set; } = true;

        public bool CanPostMessages { get; set; } = false;
        public bool CanInviteUsers { get; set; } = false;
        public bool CanShareContact { get; set; } = false;

        public bool ShowChats { get; set; } = true;
        public bool ShowContacts { get; set; } = false;

        #region Predefined

        public static readonly ChooseChatsOptions All = new()
        {
            AllowChannelChats = true,
            AllowGroupChats = true,
            AllowBotChats = true,
            AllowUserChats = true,
            AllowSecretChats = true,
            AllowSelf = true,
            CanPostMessages = false,
            CanInviteUsers = false,
            CanShareContact = false,
            ShowChats = true,
            ShowContacts = false
        };

        public static readonly ChooseChatsOptions Contacts = new()
        {
            AllowChannelChats = false,
            AllowGroupChats = false,
            AllowBotChats = true,
            AllowUserChats = true,
            AllowSecretChats = false,
            AllowSelf = false,
            CanPostMessages = false,
            CanInviteUsers = false,
            CanShareContact = true,
            ShowChats = false,
            ShowContacts = true
        };

        public static readonly ChooseChatsOptions ContactsOnly = new()
        {
            AllowChannelChats = false,
            AllowGroupChats = false,
            AllowBotChats = false,
            AllowUserChats = true,
            AllowSecretChats = false,
            AllowSelf = false,
            CanPostMessages = false,
            CanInviteUsers = false,
            CanShareContact = true,
            ShowChats = false,
            ShowContacts = true
        };

        public static readonly ChooseChatsOptions Users = new()
        {
            AllowChannelChats = false,
            AllowGroupChats = false,
            AllowBotChats = true,
            AllowUserChats = true,
            AllowSecretChats = false,
            AllowSelf = false,
            CanPostMessages = false,
            CanInviteUsers = false,
            CanShareContact = false,
            ShowChats = true,
            ShowContacts = false
        };

        public static readonly ChooseChatsOptions PostMessages = new()
        {
            AllowChannelChats = true,
            AllowGroupChats = true,
            AllowBotChats = true,
            AllowUserChats = true,
            AllowSecretChats = true,
            AllowSelf = true,
            CanPostMessages = true,
            CanInviteUsers = false,
            CanShareContact = false,
            ShowChats = true,
            ShowContacts = false
        };

        public static readonly ChooseChatsOptions InviteUsers = new()
        {
            AllowChannelChats = false,
            AllowGroupChats = false,
            AllowBotChats = true,
            AllowUserChats = true,
            AllowSecretChats = false,
            AllowSelf = false,
            CanPostMessages = false,
            CanInviteUsers = true,
            CanShareContact = false,
            ShowChats = true,
            ShowContacts = false
        };

        public static readonly ChooseChatsOptions Privacy = new()
        {
            AllowChannelChats = false,
            AllowGroupChats = true,
            AllowBotChats = true,
            AllowUserChats = true,
            AllowSecretChats = false,
            AllowSelf = false,
            CanPostMessages = false,
            CanInviteUsers = false,
            CanShareContact = false,
            ShowChats = false,
            ShowContacts = true
        };

        #endregion
    }

    #endregion

    #region Configurations

    public class ChooseChatsConfigurationGroupCall : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationGroupCall(GroupCall call)
        {
            GroupCall = call;
        }

        public GroupCall GroupCall { get; }
    }

    public class ChooseChatsConfigurationDataPackage : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationDataPackage(DataPackageView package)
        {
            Package = package;
        }

        public DataPackageView Package { get; }
    }

    public class ChooseChatsConfigurationSwitchInline : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationSwitchInline(string query, TargetChat targetChat, User bot)
        {
            Query = query;
            TargetChat = targetChat;
            Bot = bot;
        }

        public string Query { get; }

        public TargetChat TargetChat { get; }

        public User Bot { get; }
    }

    public class ChooseChatsConfigurationPostText : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationPostText(FormattedText text)
        {
            Text = text;
        }

        public FormattedText Text { get; }
    }

    public class ChooseChatsConfigurationShareMessage : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationShareMessage(Message message, bool withMyScore = false)
        {
            Message = message;
            WithMyScore = withMyScore;
        }

        public Message Message { get; }

        public bool WithMyScore { get; }
    }

    public class ChooseChatsConfigurationReplyToMessage : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationReplyToMessage(Message message, InputTextQuote quote)
        {
            Message = message;
            Quote = quote;
        }

        public Message Message { get; }

        public InputTextQuote Quote { get; }
    }

    public class ChooseChatsConfigurationShareStory : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationShareStory(long chatId, int storyId)
        {
            ChatId = chatId;
            StoryId = storyId;
        }

        public long ChatId { get; }

        public int StoryId { get; }
    }

    public class ChooseChatsConfigurationShareMessages : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationShareMessages(IList<Message> messages)
        {
            Messages = messages;
        }

        public IList<Message> Messages { get; }
    }

    public class ChooseChatsConfigurationPostLink : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationPostLink(HttpUrl url)
        {
            Url = url;
        }

        public HttpUrl Url { get; }
    }

    public class ChooseChatsConfigurationPostMessage : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationPostMessage(InputMessageContent content)
        {
            Content = content;
        }

        public InputMessageContent Content { get; }
    }

    public class ChooseChatsConfigurationStartBot : ChooseChatsConfiguration
    {
        public ChooseChatsConfigurationStartBot(User bot, string token = null)
        {
            Bot = bot;
            Token = token;
        }

        public User Bot { get; }

        public string Token { get; }
    }

    public abstract class ChooseChatsConfiguration
    {

    }

    #endregion

    public sealed partial class ChooseChatsPopup : ContentPopup
    {
        public ChooseChatsViewModel ViewModel => DataContext as ChooseChatsViewModel;

        public ChooseChatsPopup()
        {
            InitializeComponent();

            PrimaryButtonText = Strings.Send;
            SecondaryButtonText = Strings.Close;

            CaptionInput.CustomEmoji = CustomEmoji;
        }

        [Obsolete]
        public void Legacy()
        {
            DataContext = TypeResolver.Current.Resolve<ChooseChatsViewModel>();
        }

        public override void OnNavigatedTo()
        {
            IsPrimaryButtonSplit = ViewModel.IsSendAsCopyEnabled;
            EmojiPanel.DataContext = EmojiDrawerViewModel.GetForCurrentView(ViewModel.SessionId);
            ViewModel.PropertyChanged += OnPropertyChanged;

            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                PrimaryButtonText = string.Empty;
            }
        }

        protected override void OnApplyTemplate()
        {
            if (ViewModel != null)
            {
                OnNavigatedTo();
            }

            var button = GetTemplateChild("PrimarySplitButton") as Button;
            if (button != null && IsPrimaryButtonSplit)
            {
                button.Click += PrimaryButton_ContextRequested;
            }

            base.OnApplyTemplate();
        }

        private void PrimaryButton_ContextRequested(object sender, RoutedEventArgs args)
        {
            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(() => { ViewModel.SendAsCopy = true; Hide(ContentDialogResult.Primary); }, Strings.HideSenderNames, Icons.DocumentCopy);
            flyout.CreateFlyoutItem(() => { ViewModel.RemoveCaptions = true; Hide(ContentDialogResult.Primary); }, Strings.HideCaption, Icons.Block);

            flyout.ShowAt(sender as DependencyObject, FlyoutPlacementMode.BottomEdgeAlignedRight);
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

        public static async Task<Chat> PickChatAsync(string title, ChooseChatsOptions options)
        {
            var popup = new ChooseChatsPopup();
            popup.Legacy();
            popup.ViewModel.Title = title;

            var confirm = await popup.PickAsync(Array.Empty<long>(), options, ListViewSelectionMode.Single);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            return popup.ViewModel.SelectedItems.FirstOrDefault();
        }

        public static async Task<User> PickUserAsync(IClientService clientService, string title, bool contact)
        {
            return clientService.GetUser(await PickChatAsync(title, ChooseChatsOptions.ContactsOnly));
        }

        public static async Task<IList<Chat>> PickChatsAsync(string title, long[] selected, ChooseChatsOptions options)
        {
            var popup = new ChooseChatsPopup();
            popup.Legacy();
            popup.ViewModel.Title = title;
            popup.PrimaryButtonText = Strings.OK;

            var confirm = await popup.PickAsync(selected, options);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            return popup.ViewModel.SelectedItems.ToList();
        }

        public static async Task<IList<User>> PickUsersAsync(IClientService clientService, string title)
        {
            return (await PickChatsAsync(title, Array.Empty<long>(), ChooseChatsOptions.InviteUsers))?.Select(x => clientService.GetUser(x)).Where(x => x != null).ToList();
        }

        public Task<ContentDialogResult> PickAsync(IList<long> selectedItems, ChooseChatsOptions options, ListViewSelectionMode selectionMode = ListViewSelectionMode.Multiple)
        {
            ViewModel.SelectionMode = selectionMode;
            ViewModel.Options = options;
            ViewModel.IsCommentEnabled = false;
            ViewModel.IsSendAsCopyEnabled = false;
            ViewModel.IsChatSelection = true;

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
                await ViewModel.NavigatedToAsync(null, NavigationMode.New, null);
            });

            Loaded += handler;
            return this.ShowQueuedAsync();
        }

        #endregion

        #region PickFiltersAsync

        public static async Task<IList<ChatFolderElement>> AddExecute(bool include, bool allowFilters, IList<ChatFolderElement> target)
        {
            if (allowFilters)
            {
                //var target = new List<ChatFolderElement>();

                var flags = new List<FolderFlag>();
                if (include)
                {
                    flags.Add(new FolderFlag { Flag = ChatListFolderFlags.IncludeContacts });
                    flags.Add(new FolderFlag { Flag = ChatListFolderFlags.IncludeNonContacts });
                    flags.Add(new FolderFlag { Flag = ChatListFolderFlags.IncludeGroups });
                    flags.Add(new FolderFlag { Flag = ChatListFolderFlags.IncludeChannels });
                    flags.Add(new FolderFlag { Flag = ChatListFolderFlags.IncludeBots });
                }
                else
                {
                    flags.Add(new FolderFlag { Flag = ChatListFolderFlags.ExcludeMuted });
                    flags.Add(new FolderFlag { Flag = ChatListFolderFlags.ExcludeRead });
                    flags.Add(new FolderFlag { Flag = ChatListFolderFlags.ExcludeArchived });
                }

                var header = new MultipleListView();
                header.SelectionMode = ListViewSelectionMode.Multiple;
                header.ItemsSource = flags;
                header.ItemTemplate = BootStrapper.Current.Resources["FolderPickerTemplate"] as DataTemplate;
                header.ContainerContentChanging += Header_ContainerContentChanging;
                header.ItemContainerTransitions = new Windows.UI.Xaml.Media.Animation.TransitionCollection();

                foreach (var folder in target.OfType<FolderFlag>())
                {
                    var already = flags.FirstOrDefault(x => x.Flag == folder.Flag);
                    if (already != null)
                    {
                        header.SelectedItems.Add(already);
                    }
                }

                var panel = new StackPanel();
                panel.Children.Add(new Border
                {
                    Child = new TextBlock
                    {
                        Text = Strings.FilterChatTypes,
                        Padding = new Thickness(12, 16, 0, 8),
                        Style = BootStrapper.Current.Resources["BaseTextBlockStyle"] as Style
                    }
                });
                panel.Children.Add(header);
                panel.Children.Add(new Border
                {
                    Child = new TextBlock
                    {
                        Text = Strings.FilterChats,
                        Padding = new Thickness(12, 16, 0, 8),
                        Style = BootStrapper.Current.Resources["BaseTextBlockStyle"] as Style
                    }
                });

                var popup = new ChooseChatsPopup();
                popup.Legacy();
                popup.ViewModel.Title = include ? Strings.FilterAlwaysShow : Strings.FilterNeverShow;
                popup.ViewModel.AllowEmptySelection = true;
                popup.Header = panel;
                popup.PrimaryButtonText = Strings.OK;
                popup.IsPrimaryButtonEnabled = true;

                var confirm = await popup.PickAsync(target.OfType<FolderChat>().Select(x => x.Chat.Id).ToArray(), ChooseChatsOptions.All);
                if (confirm != ContentDialogResult.Primary)
                {
                    return null;
                }

                target.Clear();

                foreach (var folder in header.SelectedItems.OfType<FolderFlag>())
                {
                    target.Add(folder);
                }

                foreach (var chat in popup.ViewModel.SelectedItems)
                {
                    if (chat == null)
                    {
                        continue;
                    }

                    target.Add(new FolderChat { Chat = chat });
                }

                return target;
            }
            else
            {
                var popup = new ChooseChatsPopup();
                popup.Legacy();
                popup.ViewModel.Title = include ? Strings.FilterAlwaysShow : Strings.FilterNeverShow;
                popup.ViewModel.AllowEmptySelection = true;
                popup.PrimaryButtonText = Strings.OK;
                popup.IsPrimaryButtonEnabled = true;

                var confirm = await popup.PickAsync(target.OfType<FolderChat>().Select(x => x.Chat.Id).ToArray(), ChooseChatsOptions.All);
                if (confirm != ContentDialogResult.Primary)
                {
                    return null;
                }

                target.Clear();

                foreach (var chat in popup.ViewModel.SelectedItems)
                {
                    if (chat == null)
                    {
                        continue;
                    }

                    target.Add(new FolderChat { Chat = chat });
                }

                return target;
            }
        }

        private static void Header_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var folder = args.Item as FolderFlag;
            var content = args.ItemContainer.ContentTemplateRoot as ChatShareCell;

            content.UpdateState(args.ItemContainer.IsSelected, false, true);
            content.UpdateChatFolder(folder);
        }

        #endregion

        #region Recycle

        private bool _focused = true;

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new MultipleListViewItem(sender, false);
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            if (_focused)
            {
                _focused = false;
                args.ItemContainer.Loaded += ItemContainer_Loaded;

            }

            args.IsContainerPrepared = true;
        }

        private void ItemContainer_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is SelectorItem container)
            {
                container.Loaded -= ItemContainer_Loaded;
                container.Focus(FocusState.Pointer);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatShareCell content)
            {
                content.UpdateState(args.ItemContainer.IsSelected, false, true);
                content.UpdateChat(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        #endregion

        #region Search

        private bool _searchCollapsed = true;

        private void ShowHideSearch(bool show)
        {
            if (_searchCollapsed != show)
            {
                return;
            }

            _searchCollapsed = !show;

            FindName(nameof(SearchPanel));
            ChatListPanel.Visibility = Visibility.Visible;
            SearchPanel.Visibility = Visibility.Visible;

            SearchClear.Visibility = show
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (show)
            {
                SearchField.ControlledList = SearchPanel.Root;
            }

            var chats = ElementCompositionPreview.GetElementVisual(ChatListPanel);
            var panel = ElementCompositionPreview.GetElementVisual(SearchPanel);

            chats.CenterPoint = panel.CenterPoint = new Vector3(ChatListPanel.ActualSize / 2, 0);

            var batch = panel.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                ChatListPanel.Visibility = _searchCollapsed ? Visibility.Visible : Visibility.Collapsed;
                SearchPanel.Visibility = _searchCollapsed ? Visibility.Collapsed : Visibility.Visible;

                if (_searchCollapsed)
                {
                    ChatsPanel.Focus(FocusState.Pointer);
                }
            };

            var scale1 = panel.Compositor.CreateVector3KeyFrameAnimation();
            scale1.InsertKeyFrame(show ? 0 : 1, new Vector3(1.05f, 1.05f, 1));
            scale1.InsertKeyFrame(show ? 1 : 0, new Vector3(1));
            scale1.Duration = TimeSpan.FromMilliseconds(200);

            var scale2 = panel.Compositor.CreateVector3KeyFrameAnimation();
            scale2.InsertKeyFrame(show ? 0 : 1, new Vector3(1));
            scale2.InsertKeyFrame(show ? 1 : 0, new Vector3(0.95f, 0.95f, 1));
            scale2.Duration = TimeSpan.FromMilliseconds(200);

            var opacity1 = panel.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);
            opacity1.Duration = TimeSpan.FromMilliseconds(200);

            var opacity2 = panel.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(show ? 0 : 1, 1);
            opacity2.InsertKeyFrame(show ? 1 : 0, 0);
            opacity2.Duration = TimeSpan.FromMilliseconds(200);

            panel.StartAnimation("Scale", scale1);
            panel.StartAnimation("Opacity", opacity1);

            chats.StartAnimation("Scale", scale2);
            chats.StartAnimation("Opacity", opacity2);

            batch.End();
        }

        private void Search_GettingFocus(UIElement sender, GettingFocusEventArgs args)
        {
            if (args.FocusState == FocusState.Programmatic)
            {
                args.TryCancel();
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Pointer && _searchCollapsed)
            {
                ShowHideSearch(true);
                ViewModel.SearchChats.Query = SearchField.Text;
            }
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchField.FocusState != FocusState.Unfocused)
            {
                ShowHideSearch(true);
            }

            ViewModel.SearchChats.Query = SearchField.Text;
        }

        private void SearchClear_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchField.Text))
            {
                ShowHideSearch(false);
            }
            else
            {
                SearchField.Text = string.Empty;
            }
        }

        #endregion

        #region Comment

        private Visibility ConvertCommentVisibility(int count, bool enabled)
        {
            return count > 0 && enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Binding

        private bool ConvertButtonEnabled(bool allowEmpty, int count)
        {
            return allowEmpty || count > 0;
        }

        #endregion

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.SelectedItems = new MvxObservableCollection<Chat>(ChatsPanel.SelectedItems.Cast<Chat>());

        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ViewModel.Options.CanPostMessages && e.ClickedItem is Chat chat && (ViewModel.ClientService.IsSavedMessages(chat) || ViewModel.SelectionMode == ListViewSelectionMode.None))
            {
                if (ViewModel.SelectedItems.Empty())
                {
                    ViewModel.SelectedItems = new MvxObservableCollection<Chat>(new[] { chat });
                    ViewModel.SendCommand.Execute();

                    Hide();
                }
            }
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem;
            if (item is SearchResult result)
            {
                if (result.Chat != null)
                {
                    item = result.Chat;
                    ViewModel.ClientService.Send(new AddRecentlyFoundChat(result.Chat.Id));
                }
                else
                {
                    item = result.User;
                }
            }

            if (item is User user)
            {
                var response = await ViewModel.ClientService.SendAsync(new CreatePrivateChat(user.Id, false));
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
            ShowHideSearch(false);

            var items = ViewModel.Items;
            var selectedItems = ViewModel.SelectedItems;

            var index = items.IndexOf(chat);
            if (index >= 0)
            {
                if (index > 0)
                {
                    items.Remove(chat);
                    items.Insert(1, chat);
                }
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
            var character = Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));
            if (character.Length == 0)
            {
                return;
            }
            else if (character != "\u0016" && character != "\r" && char.IsControl(character[0]))
            {
                return;
            }
            else if (character != "\u0016" && character != "\r" && char.IsWhiteSpace(character[0]))
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement();
            if (focused is null or (not TextBox and not RichEditBox and not Button and not MenuFlyoutItem))
            {
                if (character == "\u0016" && CaptionInput.CanPasteClipboardContent)
                {
                    CaptionInput.Focus(FocusState.Keyboard);
                    CaptionInput.PasteFromClipboard();
                }
                else if (character == "\r" && IsPrimaryButtonEnabled && (SearchPanel == null || SearchPanel.Visibility == Visibility.Collapsed))
                {
                    Accept();
                }
                else
                {
                    Search_Click(null, null);

                    SearchField.Focus(FocusState.Keyboard);
                    SearchField.Text = character;
                    SearchField.SelectionStart = character.Length;
                }

                args.Handled = true;
            }
        }

        private void Accept()
        {
            if (CaptionInput.HandwritingView.IsOpen)
            {
                void handler(object s, RoutedEventArgs args)
                {
                    CaptionInput.HandwritingView.Unloaded -= handler;

                    ViewModel.Caption = CaptionInput.GetFormattedText();
                    Hide(ContentDialogResult.Primary);
                }

                CaptionInput.HandwritingView.Unloaded += handler;
                CaptionInput.HandwritingView.TryClose();
            }
            else
            {
                ViewModel.Caption = CaptionInput.GetFormattedText();
                Hide(ContentDialogResult.Primary);
            }
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(CaptionInput, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                EmojiFlyout.Hide();

                CaptionInput.InsertText(emoji.Value);
                CaptionInput.Focus(FocusState.Programmatic);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                EmojiFlyout.Hide();

                CaptionInput.InsertEmoji(sticker);
                CaptionInput.Focus(FocusState.Programmatic);
            }
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.Caption = CaptionInput.GetFormattedText();
            ViewModel.SendCommand.Execute();
        }

        private void CaptionInput_Accept(FormattedTextBox sender, EventArgs args)
        {
            Accept();
        }
    }
}
