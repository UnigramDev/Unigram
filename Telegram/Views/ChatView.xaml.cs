//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Common.Chats;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Chats;
using Telegram.Controls.Drawers;
using Telegram.Controls.Gallery;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Controls.Stories;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Stories;
using Telegram.Views.Business;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Point = Windows.Foundation.Point;
using VirtualKey = Windows.System.VirtualKey;

namespace Telegram.Views
{
    public sealed partial class ChatView : UserControlEx, INavigablePage, ISearchablePage, IDialogDelegate
    {
        private DialogViewModel _viewModel;
        public DialogViewModel ViewModel => _viewModel ??= DataContext as DialogViewModel;

        private readonly DispatcherTimer _slowModeTimer;

        private readonly Visual _rootVisual;
        private readonly Visual _textShadowVisual;

        private readonly DispatcherTimer _dateHeaderTimer;
        private readonly Visual _dateHeaderPanel;
        private readonly Visual _dateHeader;

        private readonly ZoomableListHandler _autocompleteZoomer;
        private readonly AnimatedListHandler _autocompleteHandler;

        private TaskCompletionSource<bool> _updateThemeTask;
        private readonly TaskCompletionSource<bool> _loadedThemeTask;

        private ChatBackgroundControl _backgroundControl;

        private readonly DebouncedProperty<FocusState> _focusState;
        private bool _useSystemSpellChecker = true;
        private bool _isTextReadOnly = false;

        private bool _needActivation = true;

        public ChatView()
        {
            InitializeComponent();

            // TODO: this might need to change depending on context
            _autocompleteHandler = new AnimatedListHandler(ListAutocomplete, AnimatedListType.Stickers);

            _autocompleteZoomer = new ZoomableListHandler(ListAutocomplete);
            _autocompleteZoomer.Opening = _autocompleteHandler.UnloadVisibleItems;
            _autocompleteZoomer.Closing = _autocompleteHandler.ThrottleVisibleItems;
            _autocompleteZoomer.DownloadFile = fileId => ViewModel.ClientService.DownloadFile(fileId, 32);
            _autocompleteZoomer.SessionId = () => ViewModel.ClientService.SessionId;

            _loadedThemeTask = new TaskCompletionSource<bool>();

            void AddStrategy(ChatHistoryViewItemType type, DataTemplate template, int minimum = 0)
            {
                _typeToStrategy.Add(type, new(template, minimum));
            }

            AddStrategy(ChatHistoryViewItemType.Outgoing, OutgoingMessageTemplate, 20);
            AddStrategy(ChatHistoryViewItemType.Incoming, IncomingMessageTemplate, 20);
            AddStrategy(ChatHistoryViewItemType.Service, ServiceMessageTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceUnread, ServiceMessageUnreadTemplate);
            AddStrategy(ChatHistoryViewItemType.ServicePhoto, ServiceMessagePhotoTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceBackground, ServiceMessageBackgroundTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceGiftCode, ServiceMessageGiftTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceGift, ServiceMessageGiftedTemplate);

            _focusState = new DebouncedProperty<FocusState>(100, FocusText, CanFocusText);

            Messages.Delegate = this;
            Messages.ItemsSource = _messages;
            Messages.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);

            InitializeStickers();

            //ElementComposition.GetElementVisual(this).Clip = BootStrapper.Current.Compositor.CreateInsetClip();
            ElementCompositionPreview.SetIsTranslationEnabled(ButtonMore, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ButtonAlias, true);
            ElementCompositionPreview.SetIsTranslationEnabled(TextFieldPanel, true);
            ElementCompositionPreview.SetIsTranslationEnabled(btnAttach, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ListAutocomplete, true);

            _rootVisual = ElementComposition.GetElementVisual(TextArea);

            if (DateHeaderPanel != null)
            {
                _dateHeaderTimer = new DispatcherTimer();
                _dateHeaderTimer.Interval = TimeSpan.FromMilliseconds(2000);
                _dateHeaderTimer.Tick += (s, args) =>
                {
                    _dateHeaderTimer.Stop();
                    ShowHideDateHeader(false, true);
                };

                _dateHeaderPanel = ElementComposition.GetElementVisual(DateHeaderRelative);
                _dateHeader = ElementComposition.GetElementVisual(DateHeader);

                _dateHeaderPanel.Clip = _dateHeaderPanel.Compositor.CreateInsetClip();
            }

            _debouncer = new DispatcherTimer();
            _debouncer.Interval = TimeSpan.FromMilliseconds(Constants.AnimatedThrottle);
            _debouncer.Tick += (s, args) =>
            {
                _debouncer.Stop();
                ViewVisibleMessages(false);
            };

            _slowModeTimer = new DispatcherTimer();
            _slowModeTimer.Interval = TimeSpan.FromSeconds(1);
            _slowModeTimer.Tick += (s, args) =>
            {
                var fullInfo = ViewModel.ClientService.GetSupergroupFull(ViewModel.Chat);
                if (fullInfo == null)
                {
                    _slowModeTimer.Stop();
                    return;
                }

                var expiresIn = fullInfo.SlowModeDelayExpiresIn = Math.Max(fullInfo.SlowModeDelayExpiresIn - 1, 0);
                if (expiresIn == 0)
                {
                    _slowModeTimer.Stop();
                }

                btnSendMessage.SlowModeDelay = fullInfo.SlowModeDelay;
                btnSendMessage.SlowModeDelayExpiresIn = fullInfo.SlowModeDelayExpiresIn;
            };

            _textShadowVisual = VisualUtilities.DropShadow(Separator);
            _textShadowVisual.IsVisible = false;
        }

        private bool CanFocusText(FocusState state)
        {
            if (state == FocusState.Keyboard || state == FocusState.Programmatic)
            {
                return TextField.FocusState != state;
            }

            return false;
        }

        private void FocusText(FocusState state)
        {
            if (state == FocusState.Keyboard || state == FocusState.Programmatic)
            {
                TextField.Focus(state);
            }
        }

        private void OnNavigatedTo()
        {
            SearchMask.InitializeParent(Header, ClipperOuter, DateHeaderRelative);
            GroupCall.InitializeParent(ClipperOuter);
            JoinRequests.InitializeParent(ClipperJoinRequests);
            TranslateHeader.InitializeParent(ClipperTranslate);
            ActionBar.InitializeParent(ClipperActionBar);
            ConnectedBot.InitializeParent(ClipperConnected);
            PinnedMessage.InitializeParent(Clipper);
        }

        public string GetAutomationName()
        {
            if (Title == null || Subtitle == null || ChatActionLabel == null)
            {
                return string.Empty;
            }

            var result = Title.Text.TrimEnd('.', ',');
            var identity = Identity.CurrentType switch
            {
                IdentityIconType.Fake => Strings.FakeMessage,
                IdentityIconType.Scam => Strings.ScamMessage,
                IdentityIconType.Premium => Strings.AccDescrPremium,
                IdentityIconType.Verified => Strings.AccDescrVerified,
                _ => null
            };

            if (identity != null)
            {
                result += ", " + identity;
            }

            if (ChatActionLabel.Text.Length > 0)
            {
                result += ", " + ChatActionLabel.Text;
            }
            else if (Subtitle.Text.Length > 0)
            {
                result += ", " + Subtitle.Text;
            }

            return result;
        }

        private void InitializeStickers()
        {
            StickersPanel.EmojiClick = Emojis_ItemClick;

            StickersPanel.StickerClick += Stickers_ItemClick;
            StickersPanel.StickerContextRequested += Sticker_ContextRequested;
            StickersPanel.ChoosingSticker += Stickers_ChoosingItem;
            StickersPanel.SettingsClick += StickersPanel_SettingsClick;

            StickersPanel.AnimationClick += Animations_ItemClick;
            StickersPanel.AnimationContextRequested += Animation_ContextRequested;
        }

        private void StickersPanel_SettingsClick(object sender, EventArgs e)
        {
            HideStickers();
            ViewModel.NavigationService.Navigate(typeof(SettingsStickersPage));
        }

        public void HideStickers()
        {
            ButtonStickers.Collapse();
            _focusState.Set(FocusState.Programmatic);
        }

        private ChatBackgroundControl FindBackgroundControl()
        {
            var masterDetailPanel = this.GetParent<MasterDetailPanel>();
            if (masterDetailPanel != null)
            {
                return masterDetailPanel.GetChild<ChatBackgroundControl>();
            }

            return null;
        }

        public void Deactivate(bool navigation)
        {
            if (ViewModel != null)
            {
                ViewModel.Dispose();

                ViewModel.MessageSliceLoaded -= OnMessageSliceLoaded;
                ViewModel.PropertyChanged -= OnPropertyChanged;
                ViewModel.Items.AttachChanged = null;
                ViewModel.Items.CollectionChanged -= OnCollectionChanged;

                //ViewModel.Items.Dispose();
                //ViewModel.Items.Clear();

                ViewModel.Delegate = null;
                ViewModel.TextField = null;
                ViewModel.HistoryField = null;
                ViewModel.Sticker_Click = null;

                Messages.Suspend();

                if (navigation is false)
                {
                    _albumIdToSelector.Clear();
                    _messageIdToSelector.Clear();
                    _messageIdToMessageIds.Clear();
                }
            }

            ButtonStickers.Collapse();
        }

        private readonly SynchronizedList<MessageViewModel> _messages = new();

        private void OnMessageSliceLoaded(object sender, EventArgs e)
        {
            _albumIdToSelector.Clear();
            _messageIdToSelector.Clear();
            _messageIdToMessageIds.Clear();

            if (sender is DialogViewModel viewModel)
            {
                _messages.UpdateSource(viewModel.Items);
                viewModel.MessageSliceLoaded -= OnMessageSliceLoaded;
            }

            Bindings.Update();
        }

        public void Activate(DialogViewModel viewModel)
        {
            Logger.Info($"ItemsPanelRoot.Children.Count: {Messages.ItemsPanelRoot?.Children.Count}");
            Logger.Info($"Items.Count: {Messages.Items.Count}");

            DataContext = _viewModel = viewModel;
            Messages.ViewModel = viewModel;

            _updateThemeTask = new TaskCompletionSource<bool>();
            ViewModel.MessageSliceLoaded += OnMessageSliceLoaded;
            ViewModel.TextField = TextField;
            ViewModel.HistoryField = Messages;
            ViewModel.Sticker_Click = Stickers_ItemClick;

            ViewModel.SetText(null, false);

            Messages.SetScrollingMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
            Messages.ItemsSource ??= _messages;

            CheckMessageBoxEmpty();

            SearchMask?.Update(ViewModel.Search);

            ViewModel.PropertyChanged += OnPropertyChanged;
            ViewModel.Items.AttachChanged = OnAttachChanged;
            ViewModel.Items.CollectionChanged += OnCollectionChanged;

            //Playback.Update(ViewModel.ClientService, ViewModel.PlaybackService, ViewModel.NavigationService);

            UpdateTextAreaRadius(false);

            TextField.IsReplaceEmojiEnabled = ViewModel.Settings.IsReplaceEmojiEnabled;

            if (_useSystemSpellChecker != SettingsService.Current.UseSystemSpellChecker)
            {
                _useSystemSpellChecker = SettingsService.Current.UseSystemSpellChecker;
                TextField.IsTextPredictionEnabled = _useSystemSpellChecker;
                TextField.IsSpellCheckEnabled = _useSystemSpellChecker;
            }

            TrySetFocusState(FocusState.Programmatic, false);

            StickersPanel.MaxWidth = SettingsService.Current.IsAdaptiveWideEnabled ? 1024 : double.PositiveInfinity;

            Options.Visibility = ViewModel.Type is DialogType.History or DialogType.SavedMessagesTopic
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (TextRoot.Children.Count > 1)
            {
                ShowHideChatThemeDrawer(false, TextRoot.Children[1] as ChatThemeDrawer);
            }

            if (_needActivation)
            {
                _needActivation = false;
                OnNavigatedTo();
            }

            if (viewModel.NavigationService.FrameFacade.FrameId == "ChatPreview")
            {
                _fromPreview = true;

                BackButton.Visibility = Visibility.Collapsed;
                Options.Visibility = Visibility.Collapsed;
                ClipperOuter.Visibility = Visibility.Collapsed;

                HeaderLeft.SizeChanged += Options_SizeChanged;
                HeaderLeft.Padding = new Thickness(12, 0, 0, 0);

                Messages.Margin = new Thickness(0);

                var background = new Border
                {
                    Style = Resources["HeaderBackgroundStyle"] as Style
                };

                Canvas.SetZIndex(background, 2);
                LayoutRoot.Children.Insert(0, background);
            }
        }

        private bool _fromPreview;

        public void PopupOpened()
        {
            ViewVisibleMessages(true);
        }

        public void PopupClosed()
        {
            ViewVisibleMessages();
        }

        private MessageBubble _measurement;
        private int _collectionChanging;

        private async void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            var panel = Messages.ItemsPanelRoot as ItemsStackPanel;
            if (panel == null)
            {
                return;
            }

            if (args.Action == NotifyCollectionChangedAction.Remove && panel.FirstCacheIndex < args.OldStartingIndex && panel.LastCacheIndex >= args.OldStartingIndex)
            {
                // I don't want to play this animation for now
                return;

                var owner = _measurement;
                owner ??= _measurement = new MessageBubble();

                owner.UpdateMessage(args.OldItems[0] as MessageViewModel);
                owner.Measure(new Size(ActualWidth, ActualHeight));

                var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                var anim = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                anim.InsertKeyFrame(0, new Vector3(0, -(float)owner.DesiredSize.Height, 0));
                anim.InsertKeyFrame(1, new Vector3());
                //anim.Duration = TimeSpan.FromSeconds(1);

                for (int i = panel.FirstCacheIndex; i < args.OldStartingIndex; i++)
                {
                    var container = Messages.ContainerFromIndex(i) as SelectorItem;
                    var child = VisualTreeHelper.GetChild(container, 0) as UIElement;

                    var visual = ElementComposition.GetElementVisual(child);
                    visual.StartAnimation("Offset", anim);
                }

                batch.End();
            }
            else if (args.Action == NotifyCollectionChangedAction.Add)
            {
                var message = args.NewItems[0] as MessageViewModel;
                if (message.IsInitial)
                {
                    return;
                }

                var content = message.GeneratedContent ?? message.Content;
                var pending = message.SendingState is MessageSendingStatePending { SendingId: 1 };
                var animateSendout = !message.IsChannelPost
                    && message.IsOutgoing
                    && pending
                    && message.Content is MessageText or MessageDice or MessageAnimatedEmoji
                    && message.GeneratedContent is MessageBigEmoji or MessageSticker or null;

                await panel.UpdateLayoutAsync();

                if (message.IsOutgoing && message.SendingState is MessageSendingStatePending { SendingId: not 0 } && !Messages.IsBottomReached)
                {
                    var tsc = new TaskCompletionSource<bool>();
                    Messages.ScrollToItem(message, VerticalAlignment.Bottom, new MessageBubbleHighlightOptions(false, false), tsc: tsc);
                    await tsc.Task;
                }

                var withinViewport = panel.FirstVisibleIndex <= args.NewStartingIndex && panel.LastVisibleIndex >= args.NewStartingIndex;
                if (withinViewport is false)
                {
                    if (pending && ViewModel.ComposerHeader == null)
                    {
                        ShowHideComposerHeader(false);
                    }

                    return;
                }

                if (pending && ViewModel.ComposerHeader == null)
                {
                    ShowHideComposerHeader(false, true);
                }

                var owner = Messages.ContainerFromItem(args.NewItems[0]) as SelectorItem;
                if (owner == null)
                {
                    return;
                }

                var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                var diff = owner.ActualSize.Y;

                if (animateSendout)
                {
                    var messages = ElementComposition.GetElementVisual(Messages);

                    batch.Completed += (s, args) =>
                    {
                        if (_collectionChanging-- > 1)
                        {
                            return;
                        }

                        Canvas.SetZIndex(TextArea, 0);
                        Canvas.SetZIndex(InlinePanel, 0);
                        Canvas.SetZIndex(Separator, 0);

                        if (messages.Clip is InsetClip messagesClip)
                        {
                            messagesClip.BottomInset = -8 - SettingsService.Current.Appearance.BubbleRadius;
                        }
                    };

                    _collectionChanging++;
                    Canvas.SetZIndex(TextArea, -1);
                    Canvas.SetZIndex(InlinePanel, -2);
                    Canvas.SetZIndex(Separator, -3);

                    if (messages.Clip is InsetClip messagesClip)
                    {
                        messagesClip.BottomInset = -96;
                    }

                    var head = TextArea.ActualSize.Y - 48;
                    diff = owner.ActualSize.Y > 40
                        ? owner.ActualSize.Y - head
                        : owner.ActualSize.Y;
                }

                var outer = animateSendout ? 500 * 1 : 250;
                var inner = 250 * 1;
                var delay = 0;

                var anim = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                anim.InsertKeyFrame(0, diff);
                anim.InsertKeyFrame(1, 0);
                anim.Duration = TimeSpan.FromMilliseconds(outer);
                anim.DelayTime = TimeSpan.FromMilliseconds(delay);
                anim.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

                for (int i = panel.FirstCacheIndex; i <= args.NewStartingIndex; i++)
                {
                    var container = Messages.ContainerFromIndex(i) as SelectorItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var child = VisualTreeHelper.GetChild(container, 0) as UIElement;
                    if (child == null)
                    {
                        continue;
                    }

                    var visual = ElementComposition.GetElementVisual(child);

                    if (i == args.NewStartingIndex && animateSendout)
                    {
                        var bubble = owner.GetChild<MessageBubble>();
                        var reply = message.ReplyToState != MessageReplyToState.Hidden && message.ReplyTo != null;
                        
                        var more = Math.Max(ButtonMore.ActualSize.X, ButtonAlias.ActualSize.X);
                        if (more > 0)
                        {
                            more += 8;
                        }

                        var xOffset = content switch
                        {
                            MessageBigEmoji => 48 + more,
                            MessageSticker or MessageDice => 48 + more,
                            _ => 48 + more - 12f
                        };

                        var yOffset = content switch
                        {
                            MessageBigEmoji => 66,
                            MessageSticker or MessageDice => 36,
                            _ => reply ? 29 : 44f
                        };

                        var xScale = (TextArea.ActualSize.X - xOffset) / bubble.ActualSize.X;
                        var yScale = content switch
                        {
                            MessageText => MathF.Min((float)TextField.MaxHeight, bubble.ActualSize.Y) / bubble.ActualSize.Y,
                            _ => 1
                        };

                        var fontScale = content switch
                        {
                            MessageBigEmoji => 14 / 32f,
                            MessageSticker => 20 / (180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f)),
                            _ => 1
                        };

                        bubble.AnimateSendout(xScale, yScale, fontScale, outer, inner, delay, reply);

                        anim = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                        anim.InsertKeyFrame(0, yOffset);
                        anim.InsertKeyFrame(1, 0);
                        anim.Duration = TimeSpan.FromMilliseconds(outer);
                        anim.DelayTime = TimeSpan.FromMilliseconds(delay);
                        anim.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                    }

                    visual.StartAnimation("Offset.Y", anim);
                }

                batch.End();

                if (message.IsOutgoing && message.SendingState is MessageSendingStatePending)
                {
                    _backgroundControl ??= FindBackgroundControl();
                    _backgroundControl?.UpdateBackground();
                }
            }
        }

        private void OnAttachChanged(IEnumerable<MessageViewModel> items)
        {
            foreach (var message in items)
            {
                if (message == null || !_messageIdToSelector.TryGetValue(message.Id, out var container))
                {
                    continue;
                }

                var content = container.ContentTemplateRoot as FrameworkElement;
                if (content == null)
                {
                    continue;
                }

                if (content is MessageSelector selector)
                {
                    content = selector.Content as MessageBubble;
                }

                if (content is MessageBubble bubble)
                {
                    bubble.UpdateAttach(message);
                    bubble.UpdateMessageHeader(message);
                }
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("Reply"))
            {
                CheckMessageBoxEmpty();
            }
            else if (e.PropertyName.Equals(nameof(ViewModel.IsSelectionEnabled)))
            {
                ShowHideManagePanel(ViewModel.IsSelectionEnabled);
            }
            else if (e.PropertyName.Equals(nameof(ViewModel.Search)))
            {
                SearchMask.Update(ViewModel.Search);
            }
            else if (e.PropertyName.Equals(nameof(ViewModel.IsFirstSliceLoaded)))
            {
                UpdateArrowVisibility();
            }
            else if (e.PropertyName.Equals(nameof(ViewModel.GreetingSticker)))
            {
                if (EmptyChatAnimated == null)
                {
                    return;
                }

                if (ViewModel.GreetingSticker != null)
                {
                    EmptyChatAnimated.Source = new DelayedFileSource(ViewModel.ClientService, ViewModel.GreetingSticker);
                }
                else
                {
                    EmptyChatAnimated.Source = null;
                }
            }
        }

        private void Segments_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null || chat.Id == ViewModel.ClientService.Options.MyId || sender is not ActiveStoriesSegments segments || _fromPreview)
            {
                return;
            }

            if (segments.HasActiveStories)
            {
                segments.Open(ViewModel.NavigationService, ViewModel.ClientService, chat, 36, story =>
                {
                    var transform = Segments.TransformToVisual(null);
                    var point = transform.TransformPoint(new Point());

                    return new Rect(point.X + 4, point.Y + 4, 28, 28);
                });
            }
            else
            {
                GalleryWindow.ShowAsync(ViewModel, ViewModel.StorageService, chat, Photo);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _loadedThemeTask?.TrySetResult(true);

            //Bindings.StopTracking();
            //Bindings.Update();

            ViewModel.NavigationService.Window.Activated += Window_Activated;
            ViewModel.NavigationService.Window.VisibilityChanged += Window_VisibilityChanged;

            ViewModel.NavigationService.Window.CoreWindow.CharacterReceived += OnCharacterReceived;
            ViewModel.NavigationService.Window.InputListener.KeyDown += OnAcceleratorKeyActivated;

            ViewVisibleMessages();

            TrySetFocusState(FocusState.Programmatic, true);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();

            UnloadVisibleMessages();

            ViewModel.NavigationService.Window.Activated -= Window_Activated;
            ViewModel.NavigationService.Window.VisibilityChanged -= Window_VisibilityChanged;

            ViewModel.NavigationService.Window.CoreWindow.CharacterReceived -= OnCharacterReceived;
            ViewModel.NavigationService.Window.InputListener.KeyDown -= OnAcceleratorKeyActivated;

            _loadedThemeTask?.TrySetResult(true);
            _updateThemeTask?.TrySetResult(true);

            LeakTest(false);
        }

        private bool _testLeak;

        public void LeakTest(bool enable)
        {
            if (!_testLeak)
            {
                if (enable)
                {
                    _testLeak = true;
                }
                return;
            }

            _viewModel = null;
            DataContext = null;

            ContentPanel.Children.Clear();
            LayoutRoot.Children.Clear();
            ClipperOuter.Children.Clear();

            LayoutRoot = null;
            FilledState = null;
            SidebarState = null;
            KeyboardPlaceholder = null;
            Header = null;
            ClipperOuter = null;
            ContentPanel = null;
            ReplyMarkupPanel = null;
            StickersPanel = null;
            Separator = null;
            TextArea = null;
            ChatRecord = null;
            ChatFooter = null;
            ManagePanel = null;
            SearchMask = null;
            ButtonManage = null;
            ManageCount = null;
            ButtonForward = null;
            ButtonDelete = null;
            ButtonAction = null;
            TextRoot = null;
            TextMain = null;
            ComposerHeader = null;
            ButtonMore = null;
            TextFieldPanel = null;
            btnAttach = null;
            ButtonsPanel = null;
            SecondaryButtonsPanel = null;
            ButtonStickers = null;
            ButtonRecord = null;
            btnSendMessage = null;
            btnEdit = null;
            btnVoiceMessage = null;
            btnScheduled = null;
            ButtonSilent = null;
            ButtonTimer = null;
            btnCommands = null;
            btnMarkup = null;
            ButtonMarkup = null;
            ButtonCommands = null;
            ButtonScheduled = null;
            ButtonAttach = null;
            TextField = null;
            ComposerHeaderGlyph = null;
            ComposerHeaderUpload = null;
            ComposerHeaderCancel = null;
            ComposerHeaderReference = null;
            ReplyMarkup = null;
            Messages = null;
            Arrows = null;
            InlinePanel = null;
            ListInline = null;
            ListAutocomplete = null;
            GroupCall = null;
            ClipperJoinRequests = null;
            JoinRequests = null;
            ClipperActionBar = null;
            ActionBar = null;
            ClipperTranslate = null;
            TranslateHeader = null;
            Clipper = null;
            ClipperBackground = null;
            PinnedMessage = null;
            CallbackQueryAnswerPanel = null;
            DateHeaderRelative = null;
            DateHeaderPanel = null;
            DateHeader = null;
            DateHeaderLabel = null;
            CallbackQueryAnswer = null;
            HeaderLeft = null;
            BackButton = null;
            Segments = null;
            Icon = null;
            Profile = null;
            Options = null;
            SecondaryOptions = null;
            VideoCall = null;
            Call = null;
            Subtitle = null;
            ChatActionPanel = null;
            ChatActionIndicator = null;
            ChatActionLabel = null;
            Title = null;
            Identity = null;
            Photo = null;
            Show = null;
            Hide = null;
            FlyoutArea = null;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            var mode = Window.Current.CoreWindow.ActivationMode;
            if (mode == CoreWindowActivationMode.ActivatedInForeground)
            {
                ViewVisibleMessages(true);

                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);
                if (popups.Count > 0)
                {
                    return;
                }

                var element = FocusManager.GetFocusedElement();
                if (element is not TextBox and not RichEditBox)
                {
                    TrySetFocusState(FocusState.Programmatic, true);
                }
            }
            else if (mode == CoreWindowActivationMode.Deactivated)
            {
                ViewModel.SaveDraft();
            }
        }

        private void TrySetFocusState(FocusState state, bool fast)
        {
            if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            {
                return;
            }

            if (fast)
            {
                TextField.Focus(state);
            }
            else
            {
                _focusState.Set(state);
            }
        }

        private void Window_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            if (e.Visible)
            {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);
                if (popups.Count > 0)
                {
                    return;
                }

                _focusState.Set(FocusState.Programmatic);
            }
        }

        public void Search()
        {
            var focused = FocusManager.GetFocusedElement();
            if (focused is RichTextBlock textBlock)
            {
                var message = textBlock.GetParent<MessageSelector>()?.Message;
                if (message != null)
                {
                    var selectionStart = textBlock.SelectionStart.OffsetToIndex(message.Text);
                    var selectionEnd = textBlock.SelectionEnd.OffsetToIndex(message.Text);

                    if (selectionEnd - selectionStart > 0)
                    {
                        var caption = message.GetCaption();
                        if (caption != null && caption.Text.Length >= selectionEnd && selectionEnd > 0 && selectionStart >= 0)
                        {
                            ViewModel.SearchExecute(caption.Text.Substring(selectionStart, selectionEnd - selectionStart));
                            return;
                        }
                    }
                }
            }

            ViewModel.SearchExecute(string.Empty);
        }

        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            var character = Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));
            if (character.Length == 0 || (char.IsControl(character[0]) && character != "\u0016") || char.IsWhiteSpace(character[0]))
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement();
            if (focused is null or (not TextBox and not RichEditBox))
            {
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
                {
                    if (popup.Child is not ToolTip and not TeachingTip)
                    {
                        return;
                    }
                }

                TextField.Focus(FocusState.Keyboard);

                // For some reason, this is paste
                if (character == "\u0016")
                {
                    TextField.PasteFromClipboard();
                }
                else
                {
                    TextField.InsertText(character);
                }

                args.Handled = true;
            }
        }

        private void OnAcceleratorKeyActivated(Window sender, InputKeyDownEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Delete)
            {
                if (ViewModel.IsSelectionEnabled && ViewModel.SelectedItems.Count > 0 && ViewModel.CanDeleteSelectedMessages)
                {
                    ViewModel.DeleteSelectedMessages();
                    args.Handled = true;
                }
                else
                {
                    var focused = FocusManager.GetFocusedElement();
                    if (focused is MessageSelector selector)
                    {
                        ViewModel.TryDeleteMessage(selector.Message);
                        args.Handled = true;
                    }
                }
            }
            else if (args.VirtualKey == VirtualKey.C && args.OnlyControl)
            {
                if (ViewModel.IsSelectionEnabled && ViewModel.SelectedItems.Count > 0 && ViewModel.CanCopySelectedMessage)
                {
                    ViewModel.CopySelectedMessages();
                    args.Handled = true;
                }
                else
                {
                    var focused = FocusManager.GetFocusedElement();
                    if (focused is MessageSelector selector && selector.Message != null && MessageCopy_Loaded(selector.Message))
                    {
                        ViewModel.CopyMessage(selector.Message);
                        args.Handled = true;
                    }
                    else if (focused is RichTextBlock textBlock)
                    {
                        var message = textBlock.GetParent<MessageSelector>()?.Message;
                        if (message != null)
                        {
                            var selectionStart = textBlock.SelectionStart.OffsetToIndex(message.Text);
                            var selectionEnd = textBlock.SelectionEnd.OffsetToIndex(message.Text);

                            if (selectionEnd - selectionStart > 0)
                            {
                                var caption = message.GetCaption();
                                if (caption != null && caption.Text.Length >= selectionEnd && selectionEnd > 0 && selectionStart >= 0)
                                {
                                    var quote = new MessageQuote
                                    {
                                        Message = message,
                                        Quote = caption.Substring(selectionStart, selectionEnd - selectionStart),
                                        Position = selectionStart
                                    };

                                    if (MessageCopy_Loaded(quote))
                                    {
                                        ViewModel.CopyMessage(quote);
                                    }
                                }
                            }

                            args.Handled = true;
                        }
                    }
                }
            }
            else if (args.VirtualKey == VirtualKey.R && args.RepeatCount == 1 && args.OnlyControl)
            {
                btnVoiceMessage.ToggleRecording();
                args.Handled = true;
            }
            else if (args.VirtualKey == VirtualKey.D && args.RepeatCount == 1 && args.OnlyControl)
            {
                btnVoiceMessage.StopRecording(true);
                args.Handled = true;
            }
            else if (args.VirtualKey == VirtualKey.Space && args.RepeatCount == 1 && args.OnlyKey)
            {
                if (btnVoiceMessage.IsLocked)
                {
                    ChatRecord.Pause();
                    args.Handled = true;
                }
            }
            else if (args.VirtualKey == VirtualKey.O && args.RepeatCount == 1 && args.OnlyControl)
            {
                ViewModel.SendDocument();
                args.Handled = true;
            }
            else if (args.VirtualKey == VirtualKey.PageUp && args.OnlyKey && TextField.Document.Selection.StartPosition == 0 && ViewModel.Autocomplete == null)
            {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);
                if (popups.Count > 0)
                {
                    return;
                }

                var focused = FocusManager.GetFocusedElement();
                if (focused is Selector or SelectorItem or MessageSelector or MessageService or ItemsRepeater or ChatCell or PlaybackSlider)
                {
                    return;
                }

                if (args.VirtualKey == VirtualKey.Up && (focused is TextBox or RichEditBox or ReactionButton))
                {
                    return;
                }

                var panel = Messages.ItemsPanelRoot as ItemsStackPanel;
                if (panel == null)
                {
                    return;
                }

                SelectorItem target;
                if (args.VirtualKey == VirtualKey.PageUp)
                {
                    target = Messages.ContainerFromIndex(panel.FirstVisibleIndex) as SelectorItem;
                }
                else
                {
                    target = Messages.ContainerFromIndex(panel.LastVisibleIndex) as SelectorItem;
                }

                if (target == null)
                {
                    return;
                }

                target.Focus(FocusState.Keyboard);
                args.Handled = true;
            }
            else if ((args.VirtualKey == VirtualKey.PageDown || args.VirtualKey == VirtualKey.Down) && args.OnlyKey && TextField.Document.Selection.StartPosition == TextField.Document.GetRange(int.MaxValue, int.MaxValue).EndPosition && ViewModel.Autocomplete == null)
            {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);
                if (popups.Count > 0)
                {
                    return;
                }

                var focused = FocusManager.GetFocusedElement();
                if (focused is Selector or SelectorItem or MessageSelector or MessageService or ItemsRepeater or ChatCell or PlaybackSlider)
                {
                    return;
                }

                if (args.VirtualKey == VirtualKey.Down && (focused is TextBox or RichEditBox or ReactionButton))
                {
                    return;
                }

                var panel = Messages.ItemsPanelRoot as ItemsStackPanel;
                if (panel == null)
                {
                    return;
                }

                SelectorItem target;
                if (args.VirtualKey == VirtualKey.PageUp)
                {
                    target = Messages.ContainerFromIndex(panel.FirstVisibleIndex) as SelectorItem;
                }
                else
                {
                    target = Messages.ContainerFromIndex(panel.LastVisibleIndex) as SelectorItem;
                }

                if (target == null)
                {
                    return;
                }

                target.Focus(FocusState.Keyboard);
                args.Handled = true;
            }
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs args)
        {
            if (args.Key != VirtualKey.Escape)
            {
                if (ViewModel.IsSelectionEnabled)
                {
                    ViewModel.IsSelectionEnabled = false;
                    args.Handled = true;
                }
            }
            else
            {
                if (ViewModel.Search != null)
                {
                    args.Handled = SearchMask.OnBackRequested();
                }

                if (ReplyMarkupPanel.Visibility == Visibility.Visible && ButtonMarkup.Visibility == Visibility.Visible)
                {
                    ShowHideMarkup(false, false);
                    args.Handled = true;
                }

                if (ViewModel.IsSelectionEnabled)
                {
                    ViewModel.IsSelectionEnabled = false;
                    args.Handled = true;
                }

                if (ViewModel.ComposerHeader != null)
                {
                    ViewModel.ClearReply();
                    args.Handled = true;
                }

                if (ViewModel.Autocomplete != null)
                {
                    ViewModel.Autocomplete = null;
                    args.Handled = true;
                }
            }

            Focus(FocusState.Programmatic);

            if (args.Handled)
            {
                FocusText(FocusState.Programmatic);
            }
        }

        //private bool _isAlreadyLoading;
        //private bool _isAlreadyCalled;

        private bool _oldEmpty = true;
        private bool _oldEditing;

        private void CheckButtonsVisibility()
        {
            var editing = ViewModel.ComposerHeader?.EditingMessage != null;
            var empty = TextField.IsEmpty;

            if (empty != _oldEmpty)
            {
                ButtonStickers.Source = empty
                    ? SettingsService.Current.Stickers.SelectedTab
                    : Services.Settings.StickersTab.Emoji;
            }

            FrameworkElement elementHide = null;
            FrameworkElement elementShow = null;

            if (empty != _oldEmpty && !editing)
            {
                if (empty)
                {
                    if (_oldEditing)
                    {
                        elementHide = btnEdit;
                        SendMessageButton.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        elementHide = SendMessageButton;
                        btnEdit.Visibility = Visibility.Collapsed;
                    }

                    elementShow = ButtonRecord;
                }
                else
                {
                    if (_oldEditing)
                    {
                        elementHide = btnEdit;
                        ButtonRecord.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        elementHide = ButtonRecord;
                        btnEdit.Visibility = Visibility.Collapsed;
                    }

                    elementShow = SendMessageButton;
                }
            }
            else if (editing != _oldEditing)
            {
                if (editing)
                {
                    if (_oldEmpty)
                    {
                        elementHide = ButtonRecord;
                        SendMessageButton.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        elementHide = SendMessageButton;
                        ButtonRecord.Visibility = Visibility.Collapsed;
                    }

                    elementShow = btnEdit;
                }
                else
                {
                    if (empty)
                    {
                        elementShow = ButtonRecord;
                        SendMessageButton.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        elementShow = SendMessageButton;
                        ButtonRecord.Visibility = Visibility.Collapsed;
                    }

                    elementHide = btnEdit;
                }
            }
            //else
            //{
            //    SendMessageButton.Visibility = empty || editing ? Visibility.Collapsed : Visibility.Visible;
            //    btnEdit.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
            //    ButtonRecord.Visibility = empty && !editing ? Visibility.Visible : Visibility.Collapsed;
            //}

            if (elementHide == null || elementShow == null)
            {
                return;
            }

            //elementShow.Visibility = Visibility.Visible;
            //elementHide.Visibility = Visibility.Collapsed;

            var visualHide = ElementComposition.GetElementVisual(elementHide);
            var visualShow = ElementComposition.GetElementVisual(elementShow);

            visualHide.CenterPoint = new Vector3(24);
            visualShow.CenterPoint = new Vector3(24);

            var batch = visualShow.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                elementHide.Visibility = Visibility.Collapsed;
                elementShow.Visibility = Visibility.Visible;

                visualHide.Scale = visualShow.Scale = new Vector3(1);
                visualHide.Opacity = visualShow.Opacity = 1;
            };

            var hide1 = visualShow.Compositor.CreateVector3KeyFrameAnimation();
            hide1.InsertKeyFrame(0, new Vector3(1));
            hide1.InsertKeyFrame(1, new Vector3(0));

            var hide2 = visualShow.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(0, 1);
            hide2.InsertKeyFrame(1, 0);

            visualHide.StartAnimation("Scale", hide1);
            visualHide.StartAnimation("Opacity", hide2);

            elementShow.Visibility = Visibility.Visible;

            var show1 = visualShow.Compositor.CreateVector3KeyFrameAnimation();
            show1.InsertKeyFrame(1, new Vector3(1));
            show1.InsertKeyFrame(0, new Vector3(0));

            var show2 = visualShow.Compositor.CreateScalarKeyFrameAnimation();
            show2.InsertKeyFrame(1, 1);
            show2.InsertKeyFrame(0, 0);

            visualShow.StartAnimation("Scale", show1);
            visualShow.StartAnimation("Opacity", show2);

            batch.End();

            if (editing && editing != _oldEditing || empty != _oldEmpty)
            {
                var scheduled = ElementComposition.GetElementVisual(btnScheduled);
                var commands = ElementComposition.GetElementVisual(btnCommands);
                var markup = ElementComposition.GetElementVisual(btnMarkup);

                scheduled.CenterPoint = new Vector3(24);
                commands.CenterPoint = new Vector3(24);
                markup.CenterPoint = new Vector3(24);

                var show = empty && !editing;
                if (show)
                {
                    btnScheduled.Visibility = Visibility.Visible;
                    btnCommands.Visibility = Visibility.Visible;
                    btnMarkup.Visibility = Visibility.Visible;

                    batch = commands.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                    batch.Completed += (s, args) =>
                    {
                        btnScheduled.Visibility = Visibility.Visible;
                        btnCommands.Visibility = Visibility.Visible;
                        btnMarkup.Visibility = Visibility.Visible;

                        scheduled.Scale = commands.Scale = markup.Scale = new Vector3(1);
                        scheduled.Opacity = commands.Opacity = markup.Opacity = 1;
                    };

                    scheduled.StartAnimation("Scale", show1);
                    scheduled.StartAnimation("Opacity", show2);

                    commands.StartAnimation("Scale", show1);
                    commands.StartAnimation("Opacity", show2);

                    markup.StartAnimation("Scale", show1);
                    markup.StartAnimation("Opacity", show2);

                    batch.End();
                }
                else
                {
                    batch = commands.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                    batch.Completed += (s, args) =>
                    {
                        btnScheduled.Visibility = Visibility.Collapsed;
                        btnCommands.Visibility = Visibility.Collapsed;
                        btnMarkup.Visibility = Visibility.Collapsed;

                        scheduled.Scale = commands.Scale = markup.Scale = new Vector3(1);
                        scheduled.Opacity = commands.Opacity = markup.Opacity = 1;
                    };

                    scheduled.StartAnimation("Scale", hide1);
                    scheduled.StartAnimation("Opacity", hide2);

                    commands.StartAnimation("Scale", hide1);
                    commands.StartAnimation("Opacity", hide2);

                    markup.StartAnimation("Scale", hide1);
                    markup.StartAnimation("Opacity", hide2);

                    batch.End();
                }
            }

            _oldEmpty = empty;
            _oldEditing = editing;
        }

        private void CheckMessageBoxEmpty()
        {
            CheckButtonsVisibility();

            var viewModel = ViewModel;
            if (viewModel == null || viewModel.DisableWebPagePreview)
            {
                return;
            }

            var text = TextField.Text;
            var embedded = viewModel.ComposerHeader;

            if (string.IsNullOrEmpty(text))
            {
                if (embedded != null)
                {
                    if (embedded.IsEmpty)
                    {
                        viewModel.ComposerHeader = null;
                    }
                    else if (embedded.LinkPreview != null)
                    {
                        viewModel.ComposerHeader = new MessageComposerHeader(viewModel.ClientService)
                        {
                            EditingMessage = embedded.EditingMessage,
                            ReplyToMessage = embedded.ReplyToMessage,
                            ReplyToQuote = embedded?.ReplyToQuote,
                        };
                    }
                }

                return;
            }

            TryGetWebPagePreview(viewModel.ClientService, viewModel.Chat, text, result =>
            {
                this.BeginOnUIThread(() =>
                {
                    if (!string.Equals(text, TextField.Text))
                    {
                        return;
                    }

                    if (result is LinkPreview linkPreview)
                    {
                        if (embedded != null && embedded.LinkPreviewDisabled && string.Equals(embedded.LinkPreviewUrl, linkPreview.Url, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        viewModel.ComposerHeader = new MessageComposerHeader(viewModel.ClientService)
                        {
                            EditingMessage = embedded?.EditingMessage,
                            ReplyToMessage = embedded?.ReplyToMessage,
                            ReplyToQuote = embedded?.ReplyToQuote,
                            LinkPreviewOptions = embedded?.LinkPreviewOptions,
                            LinkPreview = linkPreview,
                            LinkPreviewUrl = linkPreview.Url
                        };
                    }
                    else if (embedded != null)
                    {
                        if (embedded.IsEmpty)
                        {
                            viewModel.ComposerHeader = null;
                        }
                        else if (embedded.LinkPreview != null)
                        {
                            viewModel.ComposerHeader = new MessageComposerHeader(viewModel.ClientService)
                            {
                                EditingMessage = embedded.EditingMessage,
                                ReplyToMessage = embedded.ReplyToMessage,
                                ReplyToQuote = embedded?.ReplyToQuote,
                            };
                        }
                    }
                });
            });
        }

        private void TryGetWebPagePreview(IClientService clientService, Chat chat, string text, Action<BaseObject> result)
        {
            if (chat == null || string.IsNullOrWhiteSpace(text))
            {
                result(null);
                return;
            }

            if (chat.Type is ChatTypeSecret)
            {
                var entities = ClientEx.GetTextEntities(text);
                var urls = string.Empty;

                foreach (var entity in entities)
                {
                    if (entity.Type is TextEntityTypeUrl)
                    {
                        if (urls.Length > 0)
                        {
                            urls += " ";
                        }

                        urls += text.Substring(entity.Offset, entity.Length);
                    }
                }

                if (string.IsNullOrEmpty(urls))
                {
                    result(null);
                    return;
                }

                clientService.Send(new GetLinkPreview(new FormattedText(urls, Array.Empty<TextEntity>()), null), result);
            }
            else
            {
                clientService.Send(new GetLinkPreview(new FormattedText(text.Format(), Array.Empty<TextEntity>()), null), result);
            }
        }

        private void TextField_TextChanged(object sender, RoutedEventArgs e)
        {
            CheckMessageBoxEmpty();
        }

        private async void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            await TextField.SendAsync();
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            INavigationService service = null;

            if (_fromPreview)
            {
                service = WindowContext.Current.NavigationServices.GetByFrameId($"Main{ViewModel.ClientService.SessionId}") as NavigationService;

                var presenter = this.GetParent<MenuFlyoutPresenter>();
                if (presenter?.Parent is Popup popup)
                {
                    popup.IsOpen = false;
                }
            }

            ViewModel.OpenProfile(service ?? ViewModel.NavigationService);
        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            var flyout = new MenuFlyout();
            var header = ViewModel.ComposerHeader;

            var photoRights = !ViewModel.VerifyRights(chat, x => x.CanSendPhotos);
            var videoRights = !ViewModel.VerifyRights(chat, x => x.CanSendVideos);
            var documentRights = !ViewModel.VerifyRights(chat, x => x.CanSendDocuments);

            if (header == null || header.EditingMessage == null || (header.IsEmpty && header.LinkPreviewDisabled))
            {
                var messageRights = !ViewModel.VerifyRights(chat, x => x.CanSendBasicMessages);
                var pollRights = !ViewModel.VerifyRights(chat, x => x.CanSendPolls, Strings.GlobalAttachMediaRestricted, Strings.AttachMediaRestrictedForever, Strings.AttachMediaRestricted, out string pollsLabel);

                var pollsAllowed = chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup;
                if (!pollsAllowed && ViewModel.ClientService.TryGetUser(chat, out User user))
                {
                    pollsAllowed = user.Type is UserTypeBot;
                }

                if (photoRights || videoRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.SendMedia, Strings.PhotoOrVideo, Icons.Image);
                    flyout.CreateFlyoutItem(ViewModel.SendCamera, Strings.ChatCamera, Icons.Camera);
                }

                if (documentRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.SendDocument, Strings.ChatDocument, Icons.Document);
                }

                if (messageRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.SendLocation, Strings.ChatLocation, Icons.Location);
                }

                if (pollRights && pollsAllowed)
                {
                    flyout.CreateFlyoutItem(ViewModel.SendPoll, Strings.Poll, Icons.Poll);
                }

                if (messageRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.SendContact, Strings.AttachContact, Icons.Person);
                }

                if (ViewModel.Type is DialogType.History or DialogType.Thread)
                {
                    if (ViewModel.IsPremium
                        && ViewModel.ClientService.Options.GiftPremiumFromAttachmentMenu
                        && ViewModel.ClientService.TryGetUserFull(chat, out UserFullInfo fullInfo) && fullInfo.PremiumGiftOptions.Count > 0)
                    {
                        flyout.CreateFlyoutItem(ViewModel.GiftPremium, Strings.SendAGift, Icons.GiftPremium);
                    }

                    var bots = ViewModel.ClientService.GetBotsForChat(chat.Id);
                    if (bots.Count > 0)
                    {
                        flyout.CreateFlyoutSeparator();

                        foreach (var bot in bots)
                        {
                            var item = flyout.CreateFlyoutItem(ViewModel.OpenMiniApp, bot, bot.Name, bot.BotUserId == 1985737506 ? Icons.Wallet : Icons.Bot);
                            item.ContextRequested += AttachmentMenuBot_ContextRequested;
                        }
                    }
                }
            }
            else if (header?.EditingMessage != null)
            {
                if (photoRights || videoRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.EditMedia, Strings.PhotoOrVideo, Icons.Image);
                }

                if (documentRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.EditDocument, Strings.ChatDocument, Icons.Document);
                }

                if (header.EditingMessage.Content is MessagePhoto or MessageVideo)
                {
                    flyout.CreateFlyoutItem(ViewModel.EditCurrent, Strings.Edit, Icons.Crop);
                }
            }

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(ButtonAttach, FlyoutPlacementMode.TopEdgeAlignedLeft);
            }
        }

        private void AttachmentMenuBot_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var item = sender as MenuFlyoutItem;
            var bot = item.CommandParameter as AttachmentMenuBot;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.RemoveMiniApp, bot, Strings.BotWebViewDeleteBot, Icons.Delete);
            flyout.ShowAt(sender, args);
        }

        private void InlineBotResults_ItemClick(object sender, ItemClickEventArgs e)
        {
            var collection = ViewModel.InlineBotResults;
            if (collection == null)
            {
                return;
            }

            var result = e.ClickedItem as InlineQueryResult;
            if (result == null)
            {
                return;
            }

            ViewModel.SendBotInlineResult(result, collection.GetQueryId(result));
        }

        #region Drag & Drop

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains("application/x-tl-message"))
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
            }
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            await ViewModel.HandlePackageAsync(e.DataView);
        }
        //gridLoading.Visibility = Visibility.Visible;

        #endregion

        private async void Reply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MessageReferenceBase referenceBase)
            {
                return;
            }

            //if (sender is MessagePinned || WindowContext.IsKeyDown(VirtualKey.Control))
            {
                var message = referenceBase.MessageId;
                if (message != 0)
                {
                    await ViewModel.LoadMessageSliceAsync(null, message);

                    ViewModel.LockedPinnedMessageId = message;
                    ViewVisibleMessages();
                }
            }
            //else if (ViewModel.ComposerHeader?.WebPagePreview != null)
            //{
            //    var options = new MessageSendOptions(false, false, false, false, null, 0, true);
            //    var text = TextField.GetFormattedText(false);

            //    var response = await ViewModel.SendMessageAsync(text, options);
            //    if (response is Message message)
            //    {
            //        await new ComposeWebPagePopup(ViewModel, ViewModel.ComposerHeader, message).ShowQueuedAsync();
            //    }
            //}
            //else if (ViewModel.ComposerHeader?.ReplyToMessage != null)
            //{
            //    await new ComposeInfoPopup(ViewModel, ViewModel.ComposerHeader).ShowQueuedAsync();
            //}
        }

        private void PinnedAction_Click(object sender, RoutedEventArgs e)
        {
            if (PinnedMessage.Message?.ReplyMarkup is ReplyMarkupInlineKeyboard inlineKeyboard)
            {
                ViewModel.OpenInlineButton(PinnedMessage.Message, inlineKeyboard.Rows[0][0]);
            }
        }

        private void ReplyMarkup_ButtonClick(object sender, ReplyMarkupButtonClickEventArgs e)
        {
            if (sender is ReplyMarkupPanel panel)
            {
                ViewModel.KeyboardButtonExecute(panel.Tag as MessageViewModel, e.Button);
            }

            if (e.OneTime)
            {
                ShowHideMarkup(false, false);
            }
        }

        private void Commands_Click(object sender, RoutedEventArgs e)
        {
            TextField.SetText("/", null);
            _focusState.Set(FocusState.Keyboard);
        }

        private void Markup_Click(object sender, RoutedEventArgs e)
        {
            if (ReplyMarkupPanel.Visibility == Visibility.Visible)
            {
                ShowHideMarkup(false, true);
            }
            else
            {
                ShowHideMarkup(true);
            }
        }

        private bool _markupCollapsed = true;

        public void ShowHideMarkup(bool show, bool keyboard = true)
        {
            if (_markupCollapsed != show)
            {
                return;
            }

            _markupCollapsed = !show;

            if (show)
            {
                _textShadowVisual.IsVisible = true;
                ReplyMarkupPanel.Visibility = Visibility.Visible;

                ButtonMarkup.Glyph = Icons.ChevronDown;
                Automation.SetToolTip(ButtonMarkup, Strings.AccDescrShowKeyboard);

                Focus(FocusState.Programmatic);
                _focusState.Set(FocusState.Programmatic);
            }
            else
            {
                _textShadowVisual.IsVisible = Math.Round(InlinePanel.ActualHeight) > ViewModel.Settings.Appearance.BubbleRadius;
                ReplyMarkupPanel.Visibility = Visibility.Collapsed;

                ButtonMarkup.Glyph = Icons.BotMarkup24;
                Automation.SetToolTip(ButtonMarkup, Strings.AccDescrBotCommands);

                if (keyboard)
                {
                    Focus(FocusState.Programmatic);
                    _focusState.Set(FocusState.Keyboard);
                }
            }
        }

        private void TextField_Tapped(object sender, TappedRoutedEventArgs e)
        {
            ButtonStickers.Collapse();
        }

        public void ChangeTheme()
        {
            if (TextRoot.Children.Count > 1)
            {
                return;
            }

            var drawer = new ChatThemeDrawer(_viewModel);
            drawer.ThemeChanged += ChatThemeDrawer_ThemeChanged;
            drawer.ThemeSelected += ChatThemeDrawer_ThemeSelected;

            TextRoot.Children.Add(drawer);
            ShowHideChatThemeDrawer(true, drawer);
        }

        private void ChatThemeDrawer_ThemeChanged(object sender, ChatThemeChangedEventArgs e)
        {
            UpdateChatTheme(e.Theme);
        }

        private void ChatThemeDrawer_ThemeSelected(object sender, ChatThemeSelectedEventArgs e)
        {
            if (sender is ChatThemeDrawer drawer)
            {
                drawer.ThemeChanged -= ChatThemeDrawer_ThemeChanged;
                drawer.ThemeSelected -= ChatThemeDrawer_ThemeSelected;

                ShowHideChatThemeDrawer(false, drawer);

                if (e.Applied)
                {
                    return;
                }

                UpdateChatTheme(_viewModel.Chat);
            }
        }

        private async void ShowHideChatThemeDrawer(bool show, ChatThemeDrawer drawer)
        {
            if (TextRoot.Children.Count == 1)
            {
                return;
            }

            //if ((show && ComposerHeader.Visibility == Visibility.Visible) || (!show && (ComposerHeader.Visibility == Visibility.Collapsed || _composerHeaderCollapsed)))
            //{
            //    return;
            //}

            var composer = ElementComposition.GetElementVisual(drawer);
            var messages = ElementComposition.GetElementVisual(Messages);
            var textArea = ElementComposition.GetElementVisual(TextArea);
            var textMain = ElementComposition.GetElementVisual(TextMain);

            ElementCompositionPreview.SetIsTranslationEnabled(TextMain, true);

            if (show)
            {
                await TextArea.UpdateLayoutAsync();
            }

            var value = show ? TextArea.ActualSize.Y - TextMain.ActualSize.Y : 0;
            value = TextArea.ActualSize.Y - TextMain.ActualSize.Y;

            var value1 = TextArea.ActualSize.Y;

            var rect = textArea.Compositor.CreateRoundedRectangleGeometry();
            rect.CornerRadius = new Vector2(SettingsService.Current.Appearance.BubbleRadius);
            rect.Size = TextArea.ActualSize;
            rect.Offset = new Vector2(0, value);

            textArea.Clip = textArea.Compositor.CreateGeometricClip(rect);

            if (messages.Clip is InsetClip messagesClip)
            {
                messagesClip.TopInset = -44 + value;
                messagesClip.BottomInset = -96;
            }
            else
            {
                messages.Clip = textArea.Compositor.CreateInsetClip(0, -44 + value, 0, -96);
            }

            var batch = composer.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                textArea.Clip = null;
                composer.Clip = null;
                //messages.Clip = null;
                composer.Offset = new Vector3();
                messages.Offset = new Vector3();

                ContentPanel.Margin = new Thickness();

                if (show)
                {

                }
                else
                {
                    while (TextRoot.Children.Count > 1)
                    {
                        TextRoot.Children.RemoveAt(1);
                    }
                }

                UpdateTextAreaRadius();
            };

            var animClip2 = textArea.Compositor.CreateScalarKeyFrameAnimation();
            animClip2.InsertKeyFrame(0, show ? -44 : -44 + value);
            animClip2.InsertKeyFrame(1, show ? -44 + value : -44);
            animClip2.Duration = Constants.FastAnimation;

            var animClip3 = textArea.Compositor.CreateVector2KeyFrameAnimation();
            animClip3.InsertKeyFrame(0, new Vector2(0, show ? value : 0));
            animClip3.InsertKeyFrame(1, new Vector2(0, show ? 0 : value));
            animClip3.Duration = Constants.FastAnimation;

            var anim1 = textArea.Compositor.CreateVector3KeyFrameAnimation();
            anim1.InsertKeyFrame(0, new Vector3(0, show ? value : 0, 0));
            anim1.InsertKeyFrame(1, new Vector3(0, show ? 0 : value, 0));
            anim1.Duration = Constants.FastAnimation;

            var fade1 = textArea.Compositor.CreateScalarKeyFrameAnimation();
            fade1.InsertKeyFrame(0, show ? 1 : 0);
            fade1.InsertKeyFrame(1, show ? 0 : 1);
            fade1.Duration = Constants.FastAnimation;

            var fade2 = textArea.Compositor.CreateScalarKeyFrameAnimation();
            fade2.InsertKeyFrame(0, show ? 0 : 1);
            fade2.InsertKeyFrame(1, show ? 1 : 0);
            fade2.Duration = Constants.FastAnimation;

            rect.StartAnimation("Offset", animClip3);

            messages.Clip.StartAnimation("TopInset", animClip2);
            messages.StartAnimation("Offset", anim1);

            textMain.StartAnimation("Opacity", fade1);

            composer.StartAnimation("Offset", anim1);
            composer.StartAnimation("Opacity", fade2);

            batch.End();

            ContentPanel.Margin = new Thickness(0, -value, 0, 0);
        }

        #region Context menu

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            //var user = chat.Type is ChatTypePrivate privata ? ViewModel.ClientService.GetUser(privata.UserId) : null;
            var user = chat.Type is ChatTypePrivate or ChatTypeSecret ? ViewModel.ClientService.GetUser(chat) : null;
            var secret = chat.Type is ChatTypeSecret;
            var basicGroup = chat.Type is ChatTypeBasicGroup basicGroupType ? ViewModel.ClientService.GetBasicGroup(basicGroupType.BasicGroupId) : null;
            var supergroup = chat.Type is ChatTypeSupergroup supergroupType ? ViewModel.ClientService.GetSupergroup(supergroupType.SupergroupId) : null;

            if (user != null && user.Id == ViewModel.ClientService.Options.MyId && ViewModel.SavedMessagesTopicId == 0)
            {
                flyout.CreateFlyoutItem(ViewModel.ViewAsChats, Strings.SavedViewAsChats, Icons.AppsListDetails);
            }

            flyout.CreateFlyoutItem(Search, Strings.Search, Icons.Search, VirtualKey.F);

            if (ViewModel.Type is DialogType.SavedMessagesTopic)
            {
                flyout.CreateFlyoutItem(ViewModel.DeleteTopic, Strings.DeleteChatUser, Icons.Delete, destructive: true);

                flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
                return;
            }

            if (_compactCollapsed && user != null && user.Id != ViewModel.ClientService.Options.MyId && ViewModel.ClientService.TryGetUserFull(user.Id, out UserFullInfo userFull))
            {
                if (userFull.CanBeCalled)
                {
                    flyout.CreateFlyoutItem(ViewModel.VoiceCall, Strings.Call, Icons.Call);
                    flyout.CreateFlyoutItem(ViewModel.VideoCall, Strings.VideoCall, Icons.Video);
                }
            }
            else if (_compactCollapsed && chat.VideoChat?.GroupCallId != 0)
            {
                flyout.CreateFlyoutItem(ViewModel.VoiceCall, Strings.VoipGroupJoinCall, Icons.VideoChat);
            }

            if (ViewModel.TranslateService.CanTranslate(ViewModel.DetectedLanguage, true) && !chat.IsTranslatable)
            {
                flyout.CreateFlyoutItem(ViewModel.ShowTranslate, Strings.TranslateMessage, Icons.Translate);
            }

            if (user != null && user.Type is not UserTypeDeleted && !secret)
            {
                flyout.CreateFlyoutItem(ViewModel.ChangeTheme, Strings.SetWallpapers, Icons.PaintBrush);
            }

            if (supergroup != null && supergroup.Status is not ChatMemberStatusCreator && (supergroup.IsChannel || supergroup.HasActiveUsername()))
            {
                flyout.CreateFlyoutItem(ViewModel.Report, Strings.ReportChat, Icons.ErrorCircle);
            }
            if (user != null && user.Type is not UserTypeDeleted && user.Id != ViewModel.ClientService.Options.MyId)
            {
                if (!user.IsContact && !LastSeenConverter.IsServiceUser(user) && !LastSeenConverter.IsSupportUser(user))
                {
                    if (!string.IsNullOrEmpty(user.PhoneNumber))
                    {
                        flyout.CreateFlyoutItem(ViewModel.AddToContacts, Strings.AddToContacts, Icons.PersonAdd);
                    }
                    else
                    {
                        flyout.CreateFlyoutItem(ViewModel.ShareMyContact, Strings.ShareMyContactInfo, Icons.Share);
                    }
                }
            }
            if (ViewModel.IsSelectionEnabled is false)
            {
                if (user != null || basicGroup != null || (supergroup != null && !supergroup.IsChannel && !supergroup.HasActiveUsername()))
                {
                    flyout.CreateFlyoutItem(ViewModel.ClearHistory, Strings.ClearHistory, Icons.Broom);
                }
                if (user != null)
                {
                    flyout.CreateFlyoutItem(ViewModel.DeleteChat, Strings.DeleteChatUser, Icons.Delete, destructive: true);
                }
                if (basicGroup != null)
                {
                    flyout.CreateFlyoutItem(ViewModel.DeleteChat, Strings.DeleteAndExit, Icons.Delete, destructive: true);
                }
                if (supergroup != null && supergroup.Status is ChatMemberStatusMember or ChatMemberStatusRestricted)
                {
                    flyout.CreateFlyoutItem(ViewModel.DeleteChat, supergroup.IsChannel ? Strings.LeaveChannelMenu : Strings.LeaveMegaMenu, Icons.Delete, destructive: true);
                }
            }
            if ((user != null && user.Type is not UserTypeDeleted && user.Id != ViewModel.ClientService.Options.MyId) || basicGroup != null || (supergroup != null && !supergroup.IsChannel))
            {
                var muted = ViewModel.ClientService.Notifications.IsMuted(chat);
                var silent = chat.DefaultDisableNotification;

                var mute = new MenuFlyoutSubItem();
                mute.Text = Strings.Mute;
                mute.Icon = MenuFlyoutHelper.CreateIcon(muted ? Icons.Alert : Icons.AlertOff);

                if (muted is false)
                {
                    mute.CreateFlyoutItem(true, () => { },
                        silent ? Strings.SoundOn : Strings.SoundOff,
                        silent ? Icons.MusicNote2 : Icons.MusicNoteOff2);
                }

                mute.CreateFlyoutItem<int?>(ViewModel.MuteFor, 60 * 60, Strings.MuteFor1h, Icons.ClockAlarmHour);
                mute.CreateFlyoutItem<int?>(ViewModel.MuteFor, null, Strings.MuteForPopup, Icons.AlertSnooze);

                var toggle = mute.CreateFlyoutItem(
                    muted ? ViewModel.Unmute : ViewModel.Mute,
                    muted ? Strings.UnmuteNotifications : Strings.MuteNotifications,
                    muted ? Icons.Speaker3 : Icons.SpeakerOff);

                if (muted is false)
                {
                    toggle.Foreground = BootStrapper.Current.Resources["DangerButtonBackground"] as Brush;
                }

                flyout.Items.Add(mute);
            }

            //if (currentUser == null || !currentUser.IsSelf)
            //{
            //    this.muteItem = this.headerItem.addSubItem(18, null);
            //}
            //else if (currentUser.IsSelf)
            //{
            //    CreateFlyoutItem(ref flyout, null, Strings.AddShortcut);
            //}
            if (user != null && user.Type is UserTypeBot)
            {
                var fullInfo = ViewModel.ClientService.GetUserFull(user.Id);
                if (fullInfo?.BotInfo != null)
                {
                    if (fullInfo.BotInfo.Commands.Any(x => x.Command.Equals("settings", StringComparison.OrdinalIgnoreCase)))
                    {
                        flyout.CreateFlyoutItem(() => ViewModel.SendMessage("/settings"), Strings.BotSettings);
                    }

                    if (fullInfo.BotInfo.Commands.Any(x => x.Command.Equals("help", StringComparison.OrdinalIgnoreCase)))
                    {
                        flyout.CreateFlyoutItem(() => ViewModel.SendMessage("/help"), Strings.BotHelp);
                    }
                }
            }

            var hidden = ViewModel.Settings.GetChatPinnedMessage(chat.Id);
            if (hidden != 0)
            {
                flyout.CreateFlyoutItem(ViewModel.ShowPinnedMessage, Strings.PinnedMessages, Icons.Pin);
            }

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
            }
        }

        private void Send_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (ViewModel.Type is not DialogType.History and not DialogType.Thread)
            {
                return;
            }

            var self = ViewModel.ClientService.IsSavedMessages(chat);

            var flyout = new MenuFlyout();

            if (TextField.Effect != null)
            {
                flyout.CreateFlyoutItem(RemoveMessageEffect, Strings.RemoveEffect, Icons.Delete, destructive: true);
                flyout.CreateFlyoutSeparator();
            }

            flyout.CreateFlyoutItem(async () => await TextField.SendAsync(true), Strings.SendWithoutSound, Icons.AlertOff);

            if (ViewModel.ClientService.TryGetUser(chat, out Td.Api.User user) && user.Type is UserTypeRegular && user.Status is not UserStatusRecently && !self)
            {
                flyout.CreateFlyoutItem(async () => await TextField.ScheduleAsync(true), Strings.SendWhenOnline, Icons.PersonCircleOnline);
            }

            flyout.CreateFlyoutItem(async () => await TextField.ScheduleAsync(false), self ? Strings.SetReminder : Strings.ScheduleMessage, Icons.CalendarClock);

            if (chat.Type is ChatTypePrivate)
            {
                flyout.Opened += (s, args) =>
                {
                    var picker = ReactionsMenuFlyout.ShowAt(ViewModel.ClientService, ViewModel.ClientService.AvailableMessageEffects?.ReactionEffectIds, null, flyout);
                    picker.Selected += MessageEffectFlyout_Selected;
                };
            }

            flyout.ShowAt(sender, FlyoutPlacementMode.TopEdgeAlignedRight);

            // Supposedly cancels click by touch
            sender.ReleasePointerCaptures();
        }

        private void RemoveMessageEffect()
        {
            MessageEffectFlyout_Selected(null, null);
        }

        private void MessageEffectFlyout_Selected(object sender, MessageEffect e)
        {
            TextField.Effect = e;

            if (e == null)
            {
                SendEffectText.Text = string.Empty;
                SendEffect.Visibility = Visibility.Collapsed;

                SendEffect.Source = null;
            }
            else
            {
                if (e.StaticIcon != null)
                {
                    SendEffectText.Text = string.Empty;
                    SendEffect.Visibility = Visibility.Visible;

                    SendEffect.Source = new DelayedFileSource(ViewModel.ClientService, e.StaticIcon);
                }
                else
                {
                    SendEffectText.Text = e.Emoji;
                    SendEffect.Visibility = Visibility.Collapsed;

                    SendEffect.Source = null;
                }

                if (e.Type is MessageEffectTypeEmojiReaction emojiReaction)
                {
                    PlayInteraction(emojiReaction.EffectAnimation.StickerValue);
                }
                else if (e.Type is MessageEffectTypePremiumSticker premiumSticker && premiumSticker.Sticker.FullType is StickerFullTypeRegular regular)
                {
                    PlayInteraction(regular.PremiumAnimation);
                }
            }
        }

        public void PlayInteraction(File interaction)
        {
            var file = interaction;
            if (file.Local.IsDownloadingCompleted && SendEffectInteractions.Children.Count < 4)
            {
                var dispatcher = Windows.System.DispatcherQueue.GetForCurrentThread();

                var height = 180 * ViewModel.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
                var player = new AnimatedImage();
                player.Width = height * 3;
                player.Height = height * 3;
                //player.IsFlipped = !message.IsOutgoing;
                player.LoopCount = 1;
                player.IsHitTestVisible = false;
                player.FrameSize = new Size(512, 512);
                player.AutoPlay = true;
                player.Source = new LocalFileSource(file);
                player.LoopCompleted += (s, args) =>
                {
                    dispatcher.TryEnqueue(() =>
                    {
                        SendEffectInteractions.Children.Remove(player);

                        if (SendEffectInteractions.Children.Count > 0)
                        {
                            return;
                        }

                        SendEffectInteractionsPopup.IsOpen = false;
                    });
                };

                var random = new Random();
                var x = height * (0.08 - (0.16 * random.NextDouble()));
                var y = height * (0.08 - (0.16 * random.NextDouble()));
                var shift = height * 0.075;

                var left = height * 3 * 0.75;
                var right = 0;
                var top = height * 3 / 2 - 6;
                var bottom = height * 3 / 2 - 6;

                //if (message.IsOutgoing)
                //{
                player.Margin = new Thickness(-left, -top, -right, -bottom);
                //}
                //else
                //{
                //    player.Margin = new Thickness(-right, -top, -left, -bottom);
                //}

                SendEffectInteractions.Children.Add(player);
                SendEffectInteractionsPopup.IsOpen = true;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                //message.Interaction = interaction;
                ViewModel.ClientService.DownloadFile(file.Id, 16);

                //UpdateManager.Subscribe(this, message, file, ref _interactionToken, UpdateFile, true);
            }
        }

        private void Reply_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var header = ViewModel.ComposerHeader;
            if (header?.ReplyToMessage != null)
            {
                if (header.ReplyToQuote != null)
                {
                    var quote = new MessageQuote
                    {
                        Message = header.ReplyToMessage,
                        Quote = header.ReplyToQuote.Text,
                        Position = header.ReplyToQuote.Position
                    };

                    flyout.CreateFlyoutItem(ViewModel.QuoteToMessageInAnotherChat, quote, Strings.ReplyToAnotherChat, Icons.Replace);
                }
                else
                {
                    flyout.CreateFlyoutItem(ViewModel.ReplyToMessageInAnotherChat, header.ReplyToMessage, Strings.ReplyToAnotherChat, Icons.Replace);
                }

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.ClearReply, Strings.DoNotReply, Icons.DismissCircle, destructive: true);
            }
            else if (header?.LinkPreview != null)
            {
                static void ChangeShowAbove(MessageComposerHeader header)
                {
                    header.LinkPreviewOptions.ShowAboveText = !header.LinkPreviewOptions.ShowAboveText;
                }

                static void ChangeForceMedia(MessageComposerHeader header)
                {
                    header.LinkPreviewOptions.ForceSmallMedia = !header.LinkPreviewOptions.ForceSmallMedia;
                    header.LinkPreviewOptions.ForceLargeMedia = !header.LinkPreviewOptions.ForceSmallMedia;
                }

                flyout.CreateFlyoutItem(ChangeShowAbove, header, header.LinkPreviewOptions.ShowAboveText ? Strings.LinkBelow : Strings.LinkAbove, header.LinkPreviewOptions.ShowAboveText ? Icons.MoveDown : Icons.MoveUp);

                if (header.LinkPreview.HasLargeMedia)
                {
                    flyout.CreateFlyoutItem(ChangeForceMedia, header, header.LinkPreviewOptions.ForceSmallMedia ? Strings.LinkMediaLarger : Strings.LinkMediaSmaller, header.LinkPreviewOptions.ForceSmallMedia ? Icons.Enlarge : Icons.Shrink);
                }

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.ClearReply, Strings.DoNotLinkPreview, Icons.DismissCircle, destructive: true);
            }

            flyout.ShowAt(sender, args);
        }

        private async void Message_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();
            flyout.MenuFlyoutPresenterStyle = new Style(typeof(MenuFlyoutPresenter));
            flyout.MenuFlyoutPresenterStyle.Setters.Add(new Setter(MinWidthProperty, 180));

            var element = sender as FrameworkElement;
            var message = Messages.ItemFromContainer(element) as MessageViewModel;

            if (sender is SelectorItem container && container.ContentTemplateRoot is FrameworkElement content)
            {
                if (content is MessageSelector selector)
                {
                    element = selector.Content as MessageBubble;
                }
                else if (content is StackPanel panel)
                {
                    element = panel.FindName("Service") as FrameworkElement;
                }
                else
                {
                    element = content;
                }
            }

            var chat = message?.Chat;
            if (chat == null || message.Id == 0)
            {
                return;
            }

            var selectionStart = -1;
            var selectionEnd = -1;

            if (args.TryGetPosition(XamlRoot.Content, out Point point))
            {
                var children = VisualTreeHelper.FindElementsInHostCoordinates(point, element);

                var textBlock = children.FirstOrDefault() as RichTextBlock;
                if (textBlock?.SelectionStart != null && textBlock?.SelectionEnd != null)
                {
                    selectionStart = textBlock.SelectionStart.OffsetToIndex(message.Text);
                    selectionEnd = textBlock.SelectionEnd.OffsetToIndex(message.Text);

                    if (selectionEnd - selectionStart <= 0)
                    {
                        MessageHelper.Hyperlink_ContextRequested(ViewModel.TranslateService, textBlock, args);

                        if (args.Handled)
                        {
                            return;
                        }
                    }
                }

                var button = children.FirstOrDefault(x => x is Button inline && inline.Tag is InlineKeyboardButton) as Button;
                if (button != null && button.Tag is InlineKeyboardButton inlineButton && inlineButton.Type is InlineKeyboardButtonTypeUrl url)
                {
                    MessageHelper.Hyperlink_ContextRequested(button, url.Url, args);

                    if (args.Handled)
                    {
                        return;
                    }
                }

                var reaction = children.FirstOrDefault(x => x is ReactionAsTagButton) as ReactionAsTagButton;
                if (reaction != null)
                {
                    reaction.OnContextRequested();
                    return;
                }

                if (message.Content is MessageAlbum album)
                {
                    var child = children.FirstOrDefault(x => x is IContent) as IContent;
                    if (child?.Message != null)
                    {
                        message = child.Message;
                    }
                }
            }
            else if (message.Content is MessageAlbum album && args.OriginalSource is DependencyObject originaSource)
            {
                var ancestor = originaSource.GetParentOrSelf<IContent>();
                if (ancestor?.Message != null)
                {
                    message = ancestor.Message;
                }
            }
            else if (args.OriginalSource is RichTextBlock originalBlock && originalBlock.SelectionStart != null && originalBlock.SelectionEnd != null)
            {
                selectionStart = originalBlock.SelectionStart.OffsetToIndex(message.Text);
                selectionEnd = originalBlock.SelectionEnd.OffsetToIndex(message.Text);

                if (selectionEnd - selectionStart <= 0)
                {
                    MessageHelper.Hyperlink_ContextRequested(ViewModel.TranslateService, originalBlock, args);

                    if (args.Handled)
                    {
                        return;
                    }
                }
            }

            var properties = await message.ClientService.SendAsync(new GetMessageProperties(message.ChatId, message.Id)) as MessageProperties;
            if (properties == null)
            {
                if (ViewModel.Type == DialogType.BusinessReplies)
                {
                    properties = new MessageProperties
                    {
                        CanBeDeletedOnlyForSelf = true,
                        CanBeEdited = true,
                        CanBeReplied = true,
                        CanBeSaved = true
                    };
                }
                else if (ViewModel is DialogEventLogViewModel eventLog && message.Event is ChatEvent chatEvent)
                {
                    var senderId = chatEvent.Action switch
                    {
                        ChatEventMemberJoined => chatEvent.MemberId,
                        ChatEventMemberJoinedByInviteLink => chatEvent.MemberId,
                        ChatEventMemberJoinedByRequest => chatEvent.MemberId,
                        ChatEventMemberLeft => chatEvent.MemberId,
                        ChatEventMessageDeleted messageDeleted => messageDeleted.Message.SenderId,
                        ChatEventMessageEdited messageEdited => messageEdited.NewMessage.SenderId,
                        ChatEventMessagePinned messagePinned => messagePinned.Message.SenderId,
                        ChatEventMessageUnpinned messageUnpinned => messageUnpinned.Message.SenderId,
                        ChatEventPollStopped pollStopped => pollStopped.Message.SenderId,
                        _ => null
                    };

                    if (senderId != null && !ViewModel.IsAdministrator(senderId))
                    {
                        flyout.CreateFlyoutItem(MessageReportFalsePositive_Loaded, ViewModel.ReportFalsePositive, message, Strings.ReportFalsePositive, Icons.ShieldError);
                        flyout.CreateFlyoutSeparator();
                        flyout.CreateFlyoutItem(eventLog.RestrictMember, senderId, Strings.Restrict, Icons.HandRight);
                        flyout.CreateFlyoutItem(eventLog.BanMember, senderId, Strings.Ban, Icons.Block, destructive: true);
                    }

                    flyout.ShowAt(sender, args, FlyoutShowMode.Auto);
                    return;
                }
                else
                {
                    return;
                }
            }

            var selected = ViewModel.SelectedItems;
            if (selected.Count > 0)
            {
                if (selected.ContainsKey(message.Id))
                {
                    flyout.CreateFlyoutItem(ViewModel.ForwardSelectedMessages, Strings.ForwardSelected, Icons.Share);

                    if (selected.All(x => MessageDownload_Loaded(x.Value)))
                    {
                        flyout.CreateFlyoutItem(ViewModel.DownloadSelectedMessages, Strings.DownloadSelected, Icons.ArrowDownload);
                    }

                    if (chat.CanBeReported)
                    {
                        flyout.CreateFlyoutItem(ViewModel.ReportSelectedMessages, Strings.ReportSelectedMessages, Icons.ShieldError);
                    }

                    flyout.CreateFlyoutItem(ViewModel.DeleteSelectedMessages, Strings.DeleteSelected, Icons.Delete, destructive: true);
                    flyout.CreateFlyoutItem(ViewModel.UnselectMessages, Strings.ClearSelection);
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(ViewModel.CopySelectedMessages, Strings.CopySelectedMessages, Icons.DocumentCopy);
                }
                else
                {
                    flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.SelectMessage, message, Strings.Select, Icons.CheckmarkCircle);
                }
            }
            else if (message.SendingState is MessageSendingStateFailed or MessageSendingStatePending)
            {
                if (message.SendingState is MessageSendingStateFailed)
                {
                    flyout.CreateFlyoutItem(MessageRetry_Loaded, ViewModel.ResendMessage, message, Strings.Retry, Icons.ArrowClockwise);
                }

                flyout.CreateFlyoutItem(MessageCopy_Loaded, ViewModel.CopyMessage, message, Strings.Copy, Icons.DocumentCopy);

                if (MessageDelete_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.DeleteMessage, message, Strings.Delete, Icons.Delete, destructive: true);
                }

                if (message.SendingState is MessageSendingStateFailed sendingStateFailed)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.Items.Add(new MenuFlyoutLabel
                    {
                        Padding = new Thickness(12, 4, 12, 4),
                        MaxWidth = 178,
                        Text = sendingStateFailed.Error.Message
                    });
                }
            }
            else
            {
                // Scheduled
                flyout.CreateFlyoutItem(MessageSendNow_Loaded, ViewModel.SendNowMessage, message, Strings.MessageScheduleSend, Icons.Send);
                flyout.CreateFlyoutItem(MessageReschedule_Loaded, ViewModel.RescheduleMessage, message, Strings.MessageScheduleEditTime, Icons.CalendarClock);

                var bot = false;
                if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
                {
                    bot = senderUser.Type is UserTypeBot;
                }

                if (message.EditDate != 0 && message.ViaBotUserId == 0 && !bot && message.ReplyMarkup is not ReplyMarkupInlineKeyboard && !message.IsOutgoing)
                {
                    var placeholder = new MenuFlyoutItem();
                    placeholder.Text = Formatter.EditDate(message.EditDate);
                    placeholder.FontSize = 12;
                    placeholder.Icon = MenuFlyoutHelper.CreateIcon(Icons.ClockEdit);

                    flyout.Items.Add(placeholder);
                    flyout.CreateFlyoutSeparator();
                }
                else if (message.ForwardInfo != null && !message.IsSaved && !message.IsVerificationCode && !message.IsOutgoing)
                {
                    var placeholder = new MenuFlyoutItem();
                    placeholder.Text = Formatter.ForwardDate(message.ForwardInfo.Date);
                    placeholder.FontSize = 12;
                    placeholder.Icon = MenuFlyoutHelper.CreateIcon(Icons.ClockArrowForward);

                    flyout.Items.Add(placeholder);
                    flyout.CreateFlyoutSeparator();
                }

                if (CanGetMessageReadDate(message, properties))
                {
                    LoadMessageReadDate(message, properties, flyout);
                }
                else if (CanGetMessageViewers(message, properties))
                {
                    LoadMessageViewers(message, properties, flyout);
                }

                MessageQuote quote = null;
                if (selectionEnd - selectionStart > 0)
                {
                    var caption = message.GetCaption();
                    if (caption != null && caption.Text.Length >= selectionEnd && selectionEnd > 0 && selectionStart >= 0)
                    {
                        quote = new MessageQuote
                        {
                            Message = message,
                            Quote = caption.Substring(selectionStart, selectionEnd - selectionStart),
                            Position = selectionStart
                        };
                    }
                }

                // Generic
                if (quote != null && MessageQuote_Loaded(quote, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.QuoteToMessage, quote, Strings.QuoteSelectedPart, Icons.ArrowReply);
                }
                else if (MessageReply_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.ReplyToMessage, message, properties.CanBeReplied ? Strings.Reply : Strings.ReplyToAnotherChat, Icons.ArrowReply);
                }

                if (MessageEdit_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.EditMessage, message, Strings.Edit, Icons.Edit);
                }

                if (MessageThread_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.OpenMessageThread, message, message.InteractionInfo?.ReplyInfo?.ReplyCount > 0 ? Locale.Declension(Strings.R.ViewReplies, message.InteractionInfo.ReplyInfo.ReplyCount) : Strings.ViewThread, Icons.ChatMultiple);
                }

                flyout.CreateFlyoutSeparator();

                // Manage
                if (MessagePin_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.PinMessage, message, message.IsPinned ? Strings.UnpinMessage : Strings.PinMessage, message.IsPinned ? Icons.PinOff : Icons.Pin);
                }

                if (MessageStatistics_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.OpenMessageStatistics, message, Strings.Statistics, Icons.DataUsage);
                }

                if (MessageForward_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.ForwardMessage, message, Strings.Forward, Icons.Share);
                }

                flyout.CreateFlyoutItem(MessageReport_Loaded, ViewModel.ReportMessage, message, Strings.ReportChat, Icons.ErrorCircle);

                if (MessageFactCheck_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.FactCheckMessage, message, message.FactCheck == null ? Strings.AddFactCheck : Strings.EditFactCheck, Icons.CheckmarkStarburst);
                }

                if (MessageDelete_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.DeleteMessage, message, Strings.Delete, Icons.Delete, destructive: true);
                }

                flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.SelectMessage, message, Strings.Select, Icons.CheckmarkCircle);

                flyout.CreateFlyoutSeparator();

                // Copy
                if (quote != null)
                {
                    // TODO: copy selection
                    flyout.CreateFlyoutItem(MessageCopy_Loaded, ViewModel.CopyMessage, quote, Strings.Copy, Icons.DocumentCopy);
                }
                else
                {
                    flyout.CreateFlyoutItem(MessageCopy_Loaded, ViewModel.CopyMessage, message, Strings.Copy, Icons.DocumentCopy);
                }

                flyout.CreateFlyoutItem(MessageCopyLink_Loaded, ViewModel.CopyMessageLink, message, Strings.CopyLink, Icons.Link);
                flyout.CreateFlyoutItem(MessageCopyMedia_Loaded, ViewModel.CopyMessageMedia, message, Strings.CopyImage, Icons.Image);

                if (quote != null)
                {
                    flyout.CreateFlyoutItem(MessageTranslate_Loaded, ViewModel.TranslateMessage, quote, Strings.TranslateMessage, Icons.Translate);
                }
                else
                {
                    flyout.CreateFlyoutItem(MessageTranslate_Loaded, ViewModel.TranslateMessage, message, Strings.TranslateMessage, Icons.Translate);
                }

                flyout.CreateFlyoutSeparator();

                // Stickers
                flyout.CreateFlyoutItem(MessageAddEmoji_Loaded, ViewModel.ShowMessageEmoji, message, Strings.AddToEmoji, Icons.Emoji);
                flyout.CreateFlyoutItem(MessageAddSticker_Loaded, ViewModel.AddStickerFromMessage, message, Strings.AddToStickers, Icons.Sticker);
                flyout.CreateFlyoutItem(MessageFaveSticker_Loaded, ViewModel.AddFavoriteSticker, message, Strings.AddToFavorites, Icons.Star);
                flyout.CreateFlyoutItem(MessageUnfaveSticker_Loaded, ViewModel.RemoveFavoriteSticker, message, Strings.DeleteFromFavorites, Icons.StarOff);

                flyout.CreateFlyoutSeparator();

                // Files
                flyout.CreateFlyoutItem(MessageSaveAnimation_Loaded, ViewModel.SaveMessageAnimation, message, Strings.SaveToGIFs, Icons.Gif);
                flyout.CreateFlyoutItem(MessageSaveSound_Loaded, ViewModel.SaveMessageNotificationSound, message, Strings.SaveForNotifications, Icons.MusicNote2);
                flyout.CreateFlyoutItem(MessageSaveMedia_Loaded, ViewModel.SaveMessageMedia, message, Strings.SaveAs, Icons.SaveAs);
                flyout.CreateFlyoutItem(MessageOpenMedia_Loaded, ViewModel.OpenMessageWith, message, Strings.OpenWith, Icons.OpenIn);
                flyout.CreateFlyoutItem(MessageOpenFolder_Loaded, ViewModel.OpenMessageFolder, message, Strings.ShowInFolder, Icons.FolderOpen);

                // Contacts
                flyout.CreateFlyoutItem(MessageAddContact_Loaded, ViewModel.AddToContacts, message, Strings.AddContactTitle, Icons.Person);
                //CreateFlyoutItem(ref flyout, MessageSaveDownload_Loaded, ViewModel.MessageSaveDownloadCommand, messageCommon, Strings.SaveToDownloads);

                // Polls
                flyout.CreateFlyoutItem(MessageUnvotePoll_Loaded, ViewModel.UnvotePoll, message, Strings.Unvote, Icons.ArrowUndo);

                if (MessageStopPoll_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.StopPoll, message, Strings.StopPoll, Icons.LockClosed);
                }

                if (Constants.DEBUG)
                {
                    var file = message.GetFile();
                    if (file != null && (file.Local.IsDownloadingActive || file.Local.IsDownloadingCompleted))
                    {
                        flyout.CreateFlyoutItem(x =>
                        {
                            var file = x.GetFile();
                            if (file == null)
                            {
                                return;
                            }

                            ViewModel.ClientService.CancelDownloadFile(file);
                            ViewModel.ClientService.Send(new DeleteFileW(file.Id));
                        }, message, "Delete from disk", Icons.Delete);
                    }
                }

                if (message.CanBeSaved is false && flyout.Items.Count > 0 && ViewModel.Chat.HasProtectedContent)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.Items.Add(new MenuFlyoutLabel
                    {
                        Padding = new Thickness(12, 4, 12, 4),
                        MaxWidth = 178,
                        Text = message.IsChannelPost
                            ? Strings.ForwardsRestrictedInfoChannel
                            : Strings.ForwardsRestrictedInfoGroup
                    });
                }
            }

            //sender.ContextFlyout = menu;

            if (flyout.Items.Count > 0 && flyout.Items[flyout.Items.Count - 1] is MenuFlyoutSeparator and not MenuFlyoutLabel)
            {
                flyout.Items.RemoveAt(flyout.Items.Count - 1);
            }

            if (element is MessageBubble bubble && selected.Count == 0)
            {
                flyout.Opened += async (s, args) =>
                {
                    var response = await message.ClientService.SendAsync(new GetMessageAvailableReactions(message.ChatId, message.Id, 8));
                    if (response is AvailableReactions reactions && flyout.IsOpen)
                    {
                        if (reactions.TopReactions.Count > 0
                            || reactions.PopularReactions.Count > 0
                            || reactions.RecentReactions.Count > 0)
                        {
                            ReactionsMenuFlyout.ShowAt(reactions, message, bubble, flyout);
                        }
                    }
                };
            }

            flyout.ShowAt(sender, args, selectionEnd - selectionStart > 0 ? FlyoutShowMode.Transient : FlyoutShowMode.Auto);
        }

        private static bool CanGetMessageViewers(MessageViewModel message, MessageProperties properties, bool reactions = true)
        {
            if (reactions && message.InteractionInfo?.Reactions?.Reactions.Count > 0)
            {
                // Thread root message is reported as saved.
                if (message.IsSaved)
                {
                    return false;
                }

                return message.Chat.Type is ChatTypeBasicGroup || message.Chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel;
            }

            if (message.Chat.LastReadOutboxMessageId < message.Id || !properties.CanGetViewers)
            {
                return false;
            }

            var viewed = message.Content switch
            {
                MessageVoiceNote voiceNote => voiceNote.IsListened,
                MessageVideoNote videoNote => videoNote.IsViewed,
                _ => true
            };

            if (viewed)
            {
                var expirePeriod = message.ClientService.Config.GetNamedNumber("chat_read_mark_expire_period", 7 * 86400);
                if (expirePeriod + message.Date > DateTime.UtcNow.ToTimestamp())
                {
                    return true;
                }
            }

            return false;
        }

        private async void LoadMessageViewers(MessageViewModel message, MessageProperties properties, MenuFlyout flyout)
        {
            static async Task<IList<User>> GetMessageViewersAsync(MessageViewModel message, MessageProperties properties)
            {
                if (CanGetMessageViewers(message, properties, false))
                {
                    var response = await message.ClientService.SendAsync(new GetMessageViewers(message.ChatId, message.Id));
                    if (response is MessageViewers viewers && viewers.Viewers.Count > 0)
                    {
                        return message.ClientService.GetUsers(viewers.Viewers.Select(x => x.UserId));
                    }
                }

                return Array.Empty<User>();
            }

            var played = message.Content is MessageVoiceNote or MessageVideoNote;
            var reacted = message.InteractionInfo.TotalReactions();

            var placeholder = flyout.CreateFlyoutItem(ViewModel.ShowMessageInteractions, message, "...", reacted > 0 ? Icons.Heart : played ? Icons.Play : Icons.Seen);
            var separator = flyout.CreateFlyoutSeparator();

            // Width must be fixed because viewers are loaded asynchronously
            placeholder.Width = 200;
            placeholder.Style = BootStrapper.Current.Resources["MessageSeenMenuFlyoutItemStyle"] as Style;

            var viewers = await GetMessageViewersAsync(message, properties);
            if (viewers.Count > 0 || reacted > 0)
            {
                string text;
                if (reacted > 0)
                {
                    if (reacted < viewers.Count)
                    {
                        text = string.Format(Locale.Declension(Strings.R.Reacted, reacted, false), string.Format("{0}/{1}", message.InteractionInfo.Reactions.Reactions.Count, viewers.Count));
                    }
                    else
                    {
                        text = Locale.Declension(Strings.R.Reacted, reacted);
                    }
                }
                else if (viewers.Count > 1)
                {
                    text = Locale.Declension(played ? Strings.R.MessagePlayed : Strings.R.MessageSeen, viewers.Count);
                }
                else if (viewers.Count > 0)
                {
                    text = viewers[0].FullName();
                }
                else
                {
                    text = Strings.NobodyViewed;
                }

                var pictures = new StackPanel();
                pictures.Orientation = Orientation.Horizontal;

                var device = ElementComposition.GetSharedDevice();
                var rect1 = CanvasGeometry.CreateRectangle(device, 0, 0, 24, 24);
                var elli1 = CanvasGeometry.CreateEllipse(device, -2, 12, 14, 14);
                var group1 = CanvasGeometry.CreateGroup(device, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);

                var compositor = BootStrapper.Current.Compositor;
                var geometry = compositor.CreatePathGeometry(new CompositionPath(group1));
                var clip = compositor.CreateGeometricClip(geometry);

                for (int i = 0; i < Math.Min(3, viewers.Count); i++)
                {
                    var user = viewers[i];
                    var picture = new ProfilePicture();
                    picture.Width = 24;
                    picture.Height = 24;
                    picture.SetUser(message.ClientService, user, 24);
                    picture.Margin = new Thickness(pictures.Children.Count > 0 ? -10 : 0, -2, 0, -2);

                    if (pictures.Children.Count > 0)
                    {
                        var visual = ElementComposition.GetElementVisual(picture);
                        visual.Clip = clip;
                    }

                    Canvas.SetZIndex(picture, -pictures.Children.Count);
                    pictures.Children.Add(picture);
                }

                placeholder.Text = text;
                placeholder.Tag = pictures;
            }
            else
            {
                placeholder.Text = Strings.NobodyViewed;
            }
        }

        private static bool CanGetMessageReadDate(MessageViewModel message, MessageProperties properties, bool reactions = true)
        {
            if (message.Chat.LastReadOutboxMessageId < message.Id || !properties.CanGetReadDate)
            {
                return false;
            }

            var viewed = message.Content switch
            {
                MessageVoiceNote voiceNote => voiceNote.IsListened,
                MessageVideoNote videoNote => videoNote.IsViewed,
                _ => true
            };

            if (viewed)
            {
                return true;
            }

            return false;
        }

        private async void LoadMessageReadDate(MessageViewModel message, MessageProperties properties, MenuFlyout flyout)
        {
            static async Task<MessageReadDate> GetMessageReadDateAsync(MessageViewModel message, MessageProperties properties)
            {
                if (CanGetMessageReadDate(message, properties, false))
                {
                    var response = await message.ClientService.SendAsync(new GetMessageReadDate(message.ChatId, message.Id));
                    if (response is MessageReadDate readDate)
                    {
                        return readDate;
                    }
                }

                return null;
            }

            var played = message.Content is MessageVoiceNote or MessageVideoNote;
            var placeholder = new MenuFlyoutReadDateItem();
            placeholder.Text = "...";
            placeholder.FontSize = 12;
            placeholder.Icon = MenuFlyoutHelper.CreateIcon(played ? Icons.Play : Icons.Seen);

            // Width must be fixed because viewers are loaded asynchronously
            placeholder.Width = 200;

            flyout.Items.Add(placeholder);
            flyout.CreateFlyoutSeparator();


            var readDate = await GetMessageReadDateAsync(message, properties);
            if (readDate is MessageReadDateRead readDateRead)
            {
                placeholder.Text = Formatter.ReadDate(readDateRead.ReadDate);
            }
            else if (readDate is MessageReadDateMyPrivacyRestricted)
            {
                placeholder.Command = new RelayCommand(ViewModel.ShowReadDate);

                placeholder.Text = Strings.PmRead;
                placeholder.ShowWhenVisibility = Visibility.Visible;
            }
            else
            {
                // TooOld, Unread, UserPrivacyRestricted.
                // Should be hidden in this case, but hiding breaks the animation.
                placeholder.Text = Strings.PmReadUnknown;
            }
        }

        private bool MessageSendNow_Loaded(MessageViewModel message)
        {
            return message.SchedulingState != null;
        }

        private bool MessageReschedule_Loaded(MessageViewModel message)
        {
            return message.SchedulingState != null;
        }

        private bool MessageQuote_Loaded(MessageQuote quote, MessageProperties properties)
        {
            return MessageReply_Loaded(quote.Message, properties);
        }

        private bool MessageReply_Loaded(MessageViewModel message, MessageProperties properties)
        {
            if (message.SchedulingState != null || (ViewModel.Type != DialogType.History && ViewModel.Type != DialogType.Thread))
            {
                return false;
            }

            if (properties.CanBeRepliedInAnotherChat)
            {
                return message.ChatId != ViewModel.ClientService.Options.RepliesBotChatId && message.ChatId != ViewModel.ClientService.Options.VerificationCodesBotChatId;
            }

            var chat = message.Chat;
            if (chat != null && chat.Type is ChatTypeSupergroup supergroupType)
            {
                var supergroup = ViewModel.ClientService.GetSupergroup(supergroupType.SupergroupId);
                if (supergroup.IsChannel)
                {
                    return supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator;
                }
                else if (supergroup.Status is ChatMemberStatusRestricted restricted)
                {
                    return restricted.IsMember && restricted.Permissions.CanSendBasicMessages;
                }
                else if (supergroup.Status is ChatMemberStatusLeft)
                {
                    return ViewModel.Type == DialogType.Thread;
                }

                return supergroup.Status is not ChatMemberStatusLeft;
            }
            else if (message.ChatId == ViewModel.ClientService.Options.RepliesBotChatId && message.ChatId != ViewModel.ClientService.Options.VerificationCodesBotChatId)
            {
                return false;
            }

            return true;
        }

        private bool MessagePin_Loaded(MessageViewModel message, MessageProperties properties)
        {
            if (ViewModel.Type is not DialogType.History and not DialogType.Pinned)
            {
                if (ViewModel.Type is not DialogType.Thread || ViewModel.Topic == null)
                {
                    return false;
                }
            }

            return properties.CanBePinned;
        }

        private bool MessageEdit_Loaded(MessageViewModel message, MessageProperties properties)
        {
            if (message.Content is MessagePoll or MessageLocation)
            {
                return false;
            }
            else if (message is QuickReplyMessageViewModel quickReply)
            {
                return quickReply.CanBeEdited;
            }

            return properties.CanBeEdited;
        }

        private bool MessageThread_Loaded(MessageViewModel message, MessageProperties properties)
        {
            if (ViewModel.Type is not DialogType.History and not DialogType.Pinned)
            {
                return false;
            }

            if (message.InteractionInfo?.ReplyInfo == null || message.InteractionInfo?.ReplyInfo?.ReplyCount > 0)
            {
                return properties.CanGetMessageThread && !message.IsChannelPost;
            }

            return false;
        }

        private bool MessageDelete_Loaded(MessageViewModel message, MessageProperties properties)
        {
            if (message == null || properties == null)
            {
                return false;
            }

            return properties.CanBeDeletedOnlyForSelf || properties.CanBeDeletedForAllUsers;
        }

        private bool MessageForward_Loaded(MessageViewModel message, MessageProperties properties)
        {
            return properties.CanBeForwarded;
        }

        private bool MessageUnvotePoll_Loaded(MessageViewModel message)
        {
            if ((ViewModel.Type == DialogType.History || ViewModel.Type == DialogType.Thread) && message.Content is MessagePoll poll && poll.Poll.Type is PollTypeRegular)
            {
                return poll.Poll.Options.Any(x => x.IsChosen) && !poll.Poll.IsClosed;
            }

            return false;
        }

        private bool MessageStopPoll_Loaded(MessageViewModel message, MessageProperties properties)
        {
            if (message.Content is MessagePoll)
            {
                return properties.CanBeEdited;
            }

            return false;
        }

        private bool MessageReport_Loaded(MessageViewModel message)
        {
            var chat = ViewModel.Chat;
            if (chat == null || !chat.CanBeReported || message.Event != null || message.IsService)
            {
                return false;
            }

            if (message.SenderId is MessageSenderUser senderUser)
            {
                return senderUser.UserId != ViewModel.ClientService.Options.MyId;
            }

            return true;
        }

        private bool MessageFactCheck_Loaded(MessageViewModel message, MessageProperties properties)
        {
            var chat = ViewModel.Chat;
            if (chat == null || chat.Type is not ChatTypeSupergroup { IsChannel: true })
            {
                return false;
            }

            return properties.CanSetFactCheck;
        }

        private bool MessageReportFalsePositive_Loaded(MessageViewModel message)
        {
            var chat = ViewModel.Chat;
            if (chat == null || message.IsService)
            {
                return false;
            }

            if (message.Event?.Action is ChatEventMessageDeleted messageDeleted)
            {
                return messageDeleted.CanReportAntiSpamFalsePositive;
            }

            return false;
        }

        private bool MessageRetry_Loaded(MessageViewModel message)
        {
            if (message.SendingState is MessageSendingStateFailed failed)
            {
                return failed.CanRetry;
            }

            return false;
        }

        private bool MessageCopy_Loaded(MessageQuote quote)
        {
            return MessageCopy_Loaded(quote.Message);
        }

        private bool MessageCopy_Loaded(MessageViewModel message)
        {
            if (message.CanBeSaved is false)
            {
                return false;
            }

            if (message.Content is MessageText text)
            {
                return !string.IsNullOrEmpty(text.Text.Text);
            }
            else if (message.Content is MessageVoiceNote voiceNote
                && voiceNote.VoiceNote.SpeechRecognitionResult is SpeechRecognitionResultText speechVoiceText)
            {
                return !string.IsNullOrEmpty(speechVoiceText.Text);
            }
            else if (message.Content is MessageVideoNote videoNote
                && videoNote.VideoNote.SpeechRecognitionResult is SpeechRecognitionResultText speechVideoText)
            {
                return !string.IsNullOrEmpty(speechVideoText.Text);
            }
            else if (message.Content is MessageContact or MessageAnimatedEmoji)
            {
                return true;
            }

            return message.Content.HasCaption();
        }

        private bool MessageTranslate_Loaded(MessageQuote message)
        {
            return ViewModel.TranslateService.CanTranslateText(message.Quote.Text);
        }

        private bool MessageTranslate_Loaded(MessageViewModel message)
        {
            var caption = message.GetCaption();
            if (caption != null)
            {
                return ViewModel.TranslateService.CanTranslateText(caption.Text);
            }
            else if (message.Content is MessageVoiceNote voiceNote
                && voiceNote.VoiceNote.SpeechRecognitionResult is SpeechRecognitionResultText speechVoiceText)
            {
                return ViewModel.TranslateService.CanTranslateText(speechVoiceText.Text);
            }
            else if (message.Content is MessageVideoNote videoNote
                && videoNote.VideoNote.SpeechRecognitionResult is SpeechRecognitionResultText speechVideoText)
            {
                return ViewModel.TranslateService.CanTranslateText(speechVideoText.Text);
            }
            else if (message.Content is MessagePoll poll)
            {
                return ViewModel.TranslateService.CanTranslateText(poll.Poll.Question.Text);
            }

            return false;
        }

        private bool MessageCopyMedia_Loaded(MessageViewModel message)
        {
            if (message.SelfDestructType is not null || !message.CanBeSaved)
            {
                return false;
            }

            if (message.Content is MessagePhoto)
            {
                return true;
            }
            else if (message.Content is MessageInvoice invoice)
            {
                return invoice.ProductInfo.Photo != null;
            }
            else if (message.Content is MessageText text)
            {
                return text.LinkPreview != null && text.LinkPreview.HasPhoto();
            }

            return false;
        }

        private bool MessageCopyLink_Loaded(MessageViewModel message)
        {
            var chat = message.Chat;
            if (chat != null && chat.Type is ChatTypeSupergroup)
            {
                //var supergroup = ViewModel.ClientService.GetSupergroup(supergroupType.SupergroupId);
                //return !string.IsNullOrEmpty(supergroup.Username);
                return ViewModel.Type is DialogType.History or DialogType.Thread;
            }

            return false;
        }

        private bool MessageSelect_Loaded(MessageViewModel message)
        {
            if (ViewModel.Type == DialogType.EventLog || message.IsService)
            {
                return false;
            }

            return true;
        }

        private bool MessageStatistics_Loaded(MessageViewModel message, MessageProperties properties)
        {
            return properties.NeedShowStatistics;
        }

        private bool MessageAddSticker_Loaded(MessageViewModel message)
        {
            if (message.Content is MessageSticker sticker && sticker.Sticker.SetId != 0)
            {
                return !ViewModel.ClientService.IsStickerSetInstalled(sticker.Sticker.SetId);
            }
            else if (message.Content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeSticker previewSticker && previewSticker.Sticker.SetId != 0)
            {
                return !ViewModel.ClientService.IsStickerSetInstalled(previewSticker.Sticker.SetId);
            }

            return false;
        }

        private bool MessageAddEmoji_Loaded(MessageViewModel message)
        {
            var caption = message.GetCaption();
            if (caption?.Entities == null)
            {
                return false;
            }

            foreach (var item in caption.Entities)
            {
                if (item.Type is TextEntityTypeCustomEmoji customEmoji)
                {
                    return true;
                }
            }

            return false;
        }

        private bool MessageFaveSticker_Loaded(MessageViewModel message)
        {
            if (message.Content is MessageSticker sticker && sticker.Sticker.SetId != 0)
            {
                return !ViewModel.ClientService.IsStickerFavorite(sticker.Sticker.StickerValue.Id);
            }

            return false;
        }

        private bool MessageUnfaveSticker_Loaded(MessageViewModel message)
        {
            if (message.Content is MessageSticker sticker && sticker.Sticker.SetId != 0)
            {
                return ViewModel.ClientService.IsStickerFavorite(sticker.Sticker.StickerValue.Id);
            }

            return false;
        }

        private bool MessageSaveMedia_Loaded(MessageViewModel message)
        {
            if (message.SelfDestructType is not null || !message.CanBeSaved)
            {
                return false;
            }

            var file = message.GetFile();
            if (file != null)
            {
                return file.Local.IsDownloadingCompleted;
            }

            return false;

            return message.Content switch
            {
                MessagePhoto photo => photo.Photo.GetBig()?.Photo.Local.IsDownloadingCompleted ?? false,
                MessageAudio audio => audio.Audio.AudioValue.Local.IsDownloadingCompleted,
                MessageDocument document => document.Document.DocumentValue.Local.IsDownloadingCompleted,
                MessageVideo video => video.Video.VideoValue.Local.IsDownloadingCompleted,
                _ => false
            };
        }

        private bool MessageOpenMedia_Loaded(MessageViewModel message)
        {
            if (message.SelfDestructType is not null || !message.CanBeSaved)
            {
                return false;
            }

            return message.Content switch
            {
                MessageAudio audio => audio.Audio.AudioValue.Local.IsDownloadingCompleted,
                MessageDocument document => document.Document.DocumentValue.Local.IsDownloadingCompleted,
                MessageVideo video => video.Video.VideoValue.Local.IsDownloadingCompleted,
                _ => false
            };
        }

        private bool MessageDownload_Loaded(MessageViewModel message)
        {
            if (message.SelfDestructType is not null || !message.CanBeSaved)
            {
                return false;
            }

            return message.Content switch
            {
                MessageAudio audio => audio.Audio.AudioValue.Local.CanBeDownloaded && !audio.Audio.AudioValue.Local.IsDownloadingActive && !audio.Audio.AudioValue.Local.IsDownloadingCompleted,
                MessageDocument document => document.Document.DocumentValue.Local.CanBeDownloaded && !document.Document.DocumentValue.Local.IsDownloadingActive && !document.Document.DocumentValue.Local.IsDownloadingCompleted,
                MessageVideo video => video.Video.VideoValue.Local.CanBeDownloaded && !video.Video.VideoValue.Local.IsDownloadingActive && !video.Video.VideoValue.Local.IsDownloadingCompleted,
                _ => false
            };
        }

        private bool MessageOpenFolder_Loaded(MessageViewModel message)
        {
            if (message.SelfDestructType is not null || !message.CanBeSaved)
            {
                return false;
            }

            return message.Content switch
            {
                MessagePhoto photo => ViewModel.StorageService.CheckAccessToFolder(photo.Photo.GetBig()?.Photo),
                MessageAudio audio => ViewModel.StorageService.CheckAccessToFolder(audio.Audio.AudioValue),
                MessageDocument document => ViewModel.StorageService.CheckAccessToFolder(document.Document.DocumentValue),
                MessageVideo video => ViewModel.StorageService.CheckAccessToFolder(video.Video.VideoValue),
                _ => false
            };
        }

        private bool MessageSaveAnimation_Loaded(MessageViewModel message)
        {
            if (message.CanBeSaved is false)
            {
                return false;
            }

            if (message.Content is MessageText text)
            {
                return text.LinkPreview != null && text.LinkPreview.Type is LinkPreviewTypeAnimation;
            }
            else if (message.Content is MessageAnimation)
            {
                return true;
            }

            return false;
        }

        private bool MessageSaveSound_Loaded(MessageViewModel message)
        {
            if (message.CanBeSaved is false)
            {
                return false;
            }

            // TODO: max count
            if (message.Content is MessageText text)
            {
                if (text.LinkPreview?.Type is LinkPreviewTypeAudio previewAudio)
                {
                    return previewAudio.Audio.Duration <= ViewModel.ClientService.Options.NotificationSoundDurationMax
                        && previewAudio.Audio.AudioValue.Size <= ViewModel.ClientService.Options.NotificationSoundSizeMax;
                }
                else if (text.LinkPreview?.Type is LinkPreviewTypeVoiceNote previewVoiceNote)
                {
                    return previewVoiceNote.VoiceNote.Duration <= ViewModel.ClientService.Options.NotificationSoundDurationMax
                        && previewVoiceNote.VoiceNote.Voice.Size <= ViewModel.ClientService.Options.NotificationSoundSizeMax;
                }
            }
            else if (message.Content is MessageAudio audio)
            {
                return audio.Audio.Duration <= ViewModel.ClientService.Options.NotificationSoundDurationMax
                    && audio.Audio.AudioValue.Size <= ViewModel.ClientService.Options.NotificationSoundSizeMax;
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote.Duration <= ViewModel.ClientService.Options.NotificationSoundDurationMax
                    && voiceNote.VoiceNote.Voice.Size <= ViewModel.ClientService.Options.NotificationSoundSizeMax;
            }

            return false;
        }

        private bool MessageCallAgain_Loaded(MessageViewModel message)
        {
            if (message.Content is MessageCall)
            {
                return true;
            }

            return false;
        }

        private bool MessageAddContact_Loaded(MessageViewModel message)
        {
            if (message.Content is MessageContact contact)
            {
                var user = ViewModel.ClientService.GetUser(contact.Contact.UserId);
                if (user == null)
                {
                    return false;
                }

                if (user.IsContact)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        private void Emojis_ItemClick(object emoji)
        {
            if (emoji is string text)
            {
                TextField.InsertText(text);
            }
            else if (emoji is Sticker sticker)
            {
                TextField.InsertEmoji(sticker);
            }

            _focusState.Set(FocusState.Programmatic);
        }

        public void Stickers_ItemClick(Sticker sticker)
        {
            Stickers_ItemClick(null, new StickerDrawerItemClickEventArgs(sticker, false));
        }

        public void Stickers_ItemClick(object sender, StickerDrawerItemClickEventArgs e)
        {
            ViewModel.SendSticker(e.Sticker, null, null, null, e.FromStickerSet);
            ButtonStickers.Collapse();

            _focusState.Set(FocusState.Programmatic);
        }

        private void Stickers_ChoosingItem(object sender, EventArgs e)
        {
            ViewModel.ChatActionManager.SetTyping(new ChatActionChoosingSticker());
        }

        public void Animations_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.SendAnimation(e.ClickedItem as Animation);
            ButtonStickers.Collapse();

            _focusState.Set(FocusState.Programmatic);
        }

        private void InlinePanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _textShadowVisual.IsVisible = Math.Round(e.NewSize.Height) > ViewModel.Settings.Appearance.BubbleRadius
                || ReplyMarkupPanel.Visibility == Visibility.Visible;
        }

        private void TextArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _rootVisual.Size = e.NewSize.ToVector2();
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            var selection = TextField.Document.Selection.GetClone();
            var entity = AutocompleteEntityFinder.Search(selection, out string result, out int index);

            void InsertText(string insert)
            {
                var range = TextField.Document.GetRange(index, TextField.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                TextField.Document.Selection.StartPosition = index + insert.Length;
            }

            if (e.ClickedItem is User user && entity is AutocompleteEntity.Username)
            {
                // TODO: find username
                var username = user.ActiveUsername(result);

                string insert;
                if (string.IsNullOrEmpty(username))
                {
                    insert = string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;

                    if (FormattedTextBox.IsUnsafe(insert))
                    {
                        insert = Strings.Username;
                    }
                }
                else
                {
                    insert = $"@{username}";
                }

                var range = TextField.Document.GetRange(index, TextField.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                if (string.IsNullOrEmpty(username))
                {
                    range.Link = $"\"tg-user://{user.Id}\"";
                }

                TextField.Document.GetRange(range.EndPosition, range.EndPosition).SetText(TextSetOptions.None, " ");
                TextField.Document.Selection.StartPosition = range.EndPosition + 1;

                if (index == 0 && user.Type is UserTypeBot bot && bot.IsInline)
                {
                    ViewModel.ResolveInlineBot(username);
                }
            }
            else if (e.ClickedItem is UserCommand command)
            {
                var insert = $"/{command.Item.Command}";
                if (chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup)
                {
                    var bot = ViewModel.ClientService.GetUser(command.UserId);
                    if (bot != null && bot.HasActiveUsername(out string username))
                    {
                        insert += $"@{username}";
                    }
                }

                var complete = WindowContext.IsKeyDown(VirtualKey.Tab);
                if (complete && entity is AutocompleteEntity.Command)
                {
                    InsertText($"{insert} ");
                }
                else
                {
                    TextField.SetText(null, null);
                    ViewModel.SendMessage(insert);
                }

                TextField.IsMenuExpanded = false;
            }
            else if (e.ClickedItem is string hashtag && entity is AutocompleteEntity.Hashtag)
            {
                InsertText($"{hashtag} ");
            }
            else if (e.ClickedItem is EmojiData emoji)
            {
                InsertText($"{emoji.Value}");
            }
            else if (e.ClickedItem is Sticker sticker)
            {
                if (sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
                {
                    var range = TextField.Document.GetRange(index, TextField.Document.Selection.StartPosition);

                    TextField.InsertEmoji(range, sticker.Emoji, customEmoji.CustomEmojiId);
                    TextField.Document.Selection.StartPosition = range.EndPosition + 1;

                    var precedingRange = TextField.Document.GetRange(index, index);
                    var offset = index;

                    // Let's see if the current emoji is preceded by the same emoji and replace all the occurrences
                    while (AutocompleteEntityFinder.TrySearch(precedingRange, out AutocompleteEntity precedingEntity, out string precedingResult, out int precedingIndex))
                    {
                        if (precedingEntity != entity || precedingResult != result)
                        {
                            break;
                        }

                        precedingRange = TextField.Document.GetRange(precedingIndex, offset);
                        TextField.InsertEmoji(precedingRange, sticker.Emoji, customEmoji.CustomEmojiId);

                        precedingRange = TextField.Document.GetRange(precedingIndex, precedingIndex);
                        offset = precedingIndex;
                    }
                }
                else
                {
                    TextField.SetText(null, null);
                    ViewModel.SendSticker(sticker, null, null, result);

                    ButtonStickers.Collapse();
                }
            }
            else if (e.ClickedItem is QuickReplyShortcut shortcut)
            {
                TextField.SetText(null, null);
                ViewModel.ClientService.Send(new SendQuickReplyShortcutMessages(ViewModel.Chat.Id, shortcut.Id, 0));
            }
        }

        private void List_SelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            ShowHideManagePanel(ViewModel.IsSelectionEnabled);
        }

        private bool _manageCollapsed = true;

        private void ShowHideManagePanel(bool show)
        {
            if (_manageCollapsed != show)
            {
                return;
            }

            _manageCollapsed = !show;
            ManagePanel.Visibility = Visibility.Visible;

            TextArea.IsEnabled = !show;

            var manage = ElementComposition.GetElementVisual(ManagePanel);
            manage.StopAnimation("Opacity");

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (show)
                {
                    _manageCollapsed = false;
                    ManagePanel.Visibility = Visibility.Visible;
                }
                else
                {
                    ManagePanel.Visibility = Visibility.Collapsed;
                }
            };

            var opacity = manage.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(show ? 0 : 1, 0);
            opacity.InsertKeyFrame(show ? 1 : 0, 1);

            manage.StartAnimation("Opacity", opacity);

            batch.End();

            if (show)
            {
                if (ViewModel.IsReportingMessages != null)
                {
                    ManageCount.Visibility = Visibility.Collapsed;
                    ButtonForward.Visibility = Visibility.Collapsed;
                    ButtonDelete.Visibility = Visibility.Collapsed;
                    ButtonReport.Visibility = Visibility.Visible;
                }
                else
                {
                    ManageCount.Visibility = Visibility.Visible;
                    ButtonForward.Visibility = Visibility.Visible;
                    ButtonDelete.Visibility = Visibility.Visible;
                    ButtonReport.Visibility = Visibility.Collapsed;
                }

                ViewModel.RaisePropertyChanged(nameof(ViewModel.SelectedCount));

                ViewModel.SaveDraft(true);
                ShowHideComposerHeader(false);
            }
            else
            {
                ViewModel.ShowDraft();
                UpdateComposerHeader(ViewModel.Chat, ViewModel.ComposerHeader);
            }
        }

        #region Binding

        private string ConvertSelection(int count)
        {
            return Locale.Declension(Strings.R.messages, count);
        }

        private string ConvertReportSelection(int count)
        {
            if (count == 0)
            {
                return Strings.ReportMessages;
            }

            return string.Format(Strings.ReportMessagesCount, Locale.Declension(Strings.R.messages, count));
        }

        public Visibility ConvertIsEmpty(bool empty, bool self, bool bot, bool should)
        {
            if (should)
            {
                return empty && self ? Visibility.Visible : Visibility.Collapsed;
            }

            return empty && !self && !bot ? Visibility.Visible : Visibility.Collapsed;
        }

        public string ConvertEmptyText(long userId)
        {
            return userId != 777000 && userId != 429000 && userId != 4244000 && (userId / 1000 == 333 || userId % 1000 == 0) ? Strings.GotAQuestion : Strings.NoMessages;
        }

        #endregion

        private async void Date_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.Tag is int messageDate)
            {
                var date = Formatter.ToLocalTime(messageDate);

                var dialog = new CalendarPopup(date);
                dialog.MaxDate = DateTimeOffset.Now.Date;

                var confirm = await dialog.ShowQueuedAsync(XamlRoot);
                if (confirm == ContentDialogResult.Primary && dialog.SelectedDates.Count > 0)
                {
                    var first = dialog.SelectedDates.FirstOrDefault();
                    var offset = Common.Extensions.ToTimestamp(first.Date);

                    await ViewModel.LoadDateSliceAsync(offset);
                }
            }
        }

        private bool _compactCollapsed;

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_compactCollapsed == (e.NewSize.Width < 500))
            {
                return;
            }

            _compactCollapsed = e.NewSize.Width < 500;

            if (_compactCollapsed)
            {
                SecondaryOptions.Visibility = Visibility.Collapsed;

                ButtonForward.Padding =
                    ButtonDelete.Padding = new Thickness(0);

                ButtonForward.Content = null;
                ButtonDelete.Content = null;

                Automation.SetToolTip(ButtonForward, Strings.Forward);
                Automation.SetToolTip(ButtonDelete, Strings.Delete);
            }
            else
            {
                SecondaryOptions.Visibility = Visibility.Visible;

                ButtonForward.Padding =
                    ButtonDelete.Padding = new Thickness(2, -2, 12, 2);

                ButtonForward.Content = Strings.Forward;
                ButtonDelete.Content = Strings.Delete;

                Automation.SetToolTip(ButtonForward, null);
                Automation.SetToolTip(ButtonDelete, null);
            }
        }

        private void ContentPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ListInline != null)
            {
                ListInline.MaxHeight = Math.Min(320, Math.Max(e.NewSize.Height - 48, 0));
            }

            ListAutocomplete.MaxHeight = Math.Min(320, Math.Max(e.NewSize.Height - 48, 0));
        }

        private void DateHeaderPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ElementComposition.GetElementVisual(sender as UIElement).CenterPoint = new Vector3((float)e.NewSize.Width / 2f, (float)e.NewSize.Height / 2f, 0);
        }

        private void ItemsStackPanel_Loading(FrameworkElement sender, object args)
        {
            sender.MaxWidth = SettingsService.Current.IsAdaptiveWideEnabled ? 1024 : double.PositiveInfinity;
            Messages.SetScrollingMode();
        }

        private async void ServiceMessage_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as FrameworkElement;
            var message = button.Tag as MessageViewModel;

            if (message == null)
            {
                button = button.GetParent<MessageService>();
                message = button?.Tag as MessageViewModel;
            }

            if (message == null)
            {
                return;
            }

            if (message.Content is MessageAsyncStory asyncStory && asyncStory.State != MessageStoryState.Expired)
            {
                var segments = button.FindName("Segments") as ActiveStoriesSegments;
                if (segments != null)
                {
                    var transform = segments.TransformToVisual(null);
                    var point = transform.TransformPoint(new Windows.Foundation.Point());

                    var origin = new Rect(point.X + 4, point.Y + 4, 112, 112);

                    var item = asyncStory.Story;
                    item ??= await _viewModel.ClientService.SendAsync(new GetStory(asyncStory.StorySenderChatId, asyncStory.StoryId, true)) as Story;

                    if (item != null)
                    {
                        var story = new StoryViewModel(message.ClientService, item);
                        var activeStories = new ActiveStoriesViewModel(message.ClientService, message.Delegate.Settings, message.Delegate.Aggregator, story);

                        var viewModel = new StoryListViewModel(message.ClientService, message.Delegate.Settings, message.Delegate.Aggregator, activeStories);
                        viewModel.NavigationService = _viewModel.NavigationService;

                        var window = new StoriesWindow();
                        window.Update(viewModel, activeStories, StoryOpenOrigin.Mention, origin, _ =>
                        {
                            var transform = segments.TransformToVisual(null);
                            var point = transform.TransformPoint(new Windows.Foundation.Point());

                            return new Rect(point.X + 4, point.Y + 4, 112, 112);
                        });

                        _ = window.ShowAsync(XamlRoot);
                    }
                    else
                    {
                        ToastPopup.Show(XamlRoot, Strings.StoryNotFound, ToastPopupIcon.ExpiredStory);
                    }
                }
            }
            else
            {
                ViewModel.ExecuteServiceMessage(message);
            }
        }

        private void Autocomplete_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextGridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;

                _autocompleteZoomer.ElementPrepared(args.ItemContainer);
            }

            if (args.Item is EmojiData or Sticker)
            {
                var radius = SettingsService.Current.Appearance.BubbleRadius;
                var min = Math.Max(4, radius - 2);

                args.ItemContainer.Margin = new Thickness(4);
                args.ItemContainer.CornerRadius = new CornerRadius(args.ItemIndex == 0 ? min : 4, 4, 4, 4);
            }
            else
            {
                args.ItemContainer.Margin = new Thickness();
                args.ItemContainer.CornerRadius = new CornerRadius();
            }

            args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
            args.IsContainerPrepared = true;
        }

        private void Autocomplete_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Item is UserCommand userCommand)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                var photo = content.Children[0] as ProfilePicture;
                var title = content.Children[1] as TextBlock;

                var command = title.Inlines[0] as Run;
                var description = title.Inlines[1] as Run;

                command.Text = $"/{userCommand.Item.Command} ";
                description.Text = userCommand.Item.Description;

                if (ViewModel.ClientService.TryGetUser(userCommand.UserId, out User user))
                {
                    photo.SetUser(ViewModel.ClientService, user, 32);
                }
            }
            else if (args.Item is QuickReplyShortcut shortcut)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                var photo = content.Children[0] as ProfilePicture;
                var title = content.Children[1] as TextBlock;

                var command = title.Inlines[0] as Run;
                var description = title.Inlines[1] as Run;

                command.Text = $"/{shortcut.Name} ";
                description.Text = Locale.Declension(Strings.R.messages, shortcut.MessageCount);

                if (ViewModel.ClientService.TryGetUser(ViewModel.ClientService.Options.MyId, out User user))
                {
                    photo.SetUser(ViewModel.ClientService, user, 32);
                }
            }
            else if (args.Item is User user)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                var photo = content.Children[0] as ProfilePicture;
                var title = content.Children[1] as TextBlock;

                var name = title.Inlines[0] as Run;
                var username = title.Inlines[1] as Run;

                name.Text = user.FullName();

                if (user.HasActiveUsername(out string usernameValue))
                {
                    username.Text = $" @{usernameValue}";
                }
                else
                {
                    username.Text = string.Empty;
                }

                photo.SetUser(ViewModel.ClientService, user, 32);
            }
            else if (args.Item is string hashtag)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                var title = content.Children[0] as TextBlock;
                title.Text = hashtag;
            }
            else if (args.Item is Sticker sticker)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                var animated = content.Children[0] as AnimatedImage;
                animated.Source = new DelayedFileSource(_viewModel.ClientService, sticker);

                AutomationProperties.SetName(args.ItemContainer, sticker.Emoji);
            }
            else if (args.Item is EmojiData emoji)
            {
                AutomationProperties.SetName(args.ItemContainer, emoji.Value);
            }

            args.Handled = true;
        }

        private bool? _replyEnabled = null;
        private bool _actionCollapsed = true;

        private void ShowAction(string content, bool enabled, bool replyEnabled = false)
        {
            if (_fromPreview)
            {
                ButtonAction.Visibility = Visibility.Collapsed;
                ChatFooter.Visibility = Visibility.Collapsed;
                TextArea.Visibility = Visibility.Collapsed;
                return;
            }

            if (content != null && (ButtonAction.Content is not TextBlock || (ButtonAction.Content is TextBlock block && !string.Equals(block.Text, content))))
            {
                ButtonAction.Content = new TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    FontWeight = FontWeights.SemiBold
                };
            }

            _replyEnabled = replyEnabled;
            ButtonAction.IsEnabled = enabled;

            _actionCollapsed = false;
            ButtonAction.Visibility = Visibility.Visible;
            ChatFooter.Visibility = Visibility.Visible;
            TextArea.Visibility = Visibility.Collapsed;
        }

        private void ShowArea(bool permanent = true)
        {
            if (_fromPreview)
            {
                ButtonAction.Visibility = Visibility.Collapsed;
                ChatFooter.Visibility = Visibility.Collapsed;
                TextArea.Visibility = Visibility.Collapsed;
                return;
            }

            if (permanent)
            {
                _replyEnabled = null;
            }

            _actionCollapsed = true;
            TextArea.Visibility = Visibility.Visible;
            ButtonAction.Visibility = Visibility.Collapsed;
            ChatFooter.Visibility = Visibility.Collapsed;

            TrySetFocusState(FocusState.Programmatic, false);
        }

        private void ButtonAction_LosingFocus(UIElement sender, LosingFocusEventArgs args)
        {
            if (_actionCollapsed && !AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            {
                args.TrySetNewFocusedElement(TextField);
                args.Handled = true;
            }
        }

        private bool StillValid(Chat chat)
        {
            return chat?.Id == ViewModel?.Chat?.Id && !_fromPreview;
        }

        #region UI delegate

        public void UpdateChat(Chat chat)
        {
            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
            UpdateChatEmojiStatus(chat);

            UpdateChatActiveStories(chat);

            UpdateChatActionBar(chat);

            UpdateChatUnreadMentionCount(chat, chat.UnreadMentionCount);
            UpdateChatUnreadReactionCount(chat, chat.UnreadReactionCount);
            UpdateChatDefaultDisableNotification(chat, chat.DefaultDisableNotification);

            ButtonScheduled.Visibility = chat.HasScheduledMessages && ViewModel.Type == DialogType.History ? Visibility.Visible : Visibility.Collapsed;
            ButtonTimer.Visibility = chat.Type is ChatTypeSecret ? Visibility.Visible : Visibility.Collapsed;
            ButtonSilent.Visibility = chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            ButtonSilent.IsChecked = chat.DefaultDisableNotification;

            Call.Visibility = Visibility.Collapsed;
            VideoCall.Visibility = Visibility.Collapsed;

            SearchOption.Glyph = chat.Id == ViewModel.ClientService.Options.MyId
                ? Icons.TagSearch
                : Icons.Search;

            // We want to collapse the bar only of we know that there's no call at all
            if (chat.VideoChat.GroupCallId == 0)
            {
                GroupCall.ShowHide(false);
            }

            UpdateChatMessageSender(chat, chat.MessageSenderId);
            UpdateChatPendingJoinRequests(chat);
            UpdateChatIsTranslatable(chat, ViewModel.DetectedLanguage);
            UpdateChatPermissions(chat);
            UpdateChatTheme(chat);
            UpdateChatBusinessBotManageBar(chat, chat.BusinessBotManageBar);

            if (TextField.Effect != null)
            {
                RemoveMessageEffect();
            }
        }

        public void UpdateChatMessageSender(Chat chat, MessageSender defaultMessageSenderId)
        {
            if (defaultMessageSenderId == null)
            {
                if (chat.Type is not ChatTypePrivate)
                {
                    ShowHideSideButton(SideButton.None);
                }
            }
            else
            {
                PhotoAlias.SetMessageSender(ViewModel.ClientService, defaultMessageSenderId, 32);
                ShowHideSideButton(SideButton.Alias);
            }
        }

        public async void UpdateChatTheme(Chat chat)
        {
            if (_updateThemeTask != null)
            {
                await _updateThemeTask.Task;
            }

            if (!StillValid(chat))
            {
                return;
            }

            UpdateChatTheme(ViewModel.ClientService.GetChatTheme(chat.ThemeName));
        }

        public void UpdateChatBackground(Chat chat)
        {
            UpdateChatTheme(chat);

            foreach (var item in _messageIdToSelector)
            {
                if (_viewModel.Items.TryGetValue(item.Key, out MessageViewModel message) && message.Content is MessageChatSetBackground)
                {
                    if (item.Value.ContentTemplateRoot is MessageService service)
                    {
                        service.UpdateMessage(message);
                    }
                }
            }
        }

        private async void UpdateChatTheme(ChatTheme theme)
        {
            if (Theme.Current.Update(ActualTheme, theme, _viewModel.Chat.Background))
            {
                var current = _viewModel.Chat.Background?.Background;
                if (current?.Type is BackgroundTypeChatTheme typeChatTheme)
                {
                    theme ??= ViewModel.ClientService.GetChatTheme(typeChatTheme.ThemeName);
                }

                current ??= ActualTheme == ElementTheme.Light ? theme?.LightSettings.Background : theme?.DarkSettings.Background;
                current ??= ViewModel.ClientService.GetDefaultBackground(ActualTheme == ElementTheme.Dark);

                if (_loadedThemeTask != null)
                {
                    await _loadedThemeTask.Task;
                }

                _backgroundControl ??= FindBackgroundControl();
                _backgroundControl?.Update(current, ActualTheme == ElementTheme.Dark);
            }
        }

        public void UpdateChatPermissions(Chat chat)
        {
            ListInline?.UpdateChatPermissions(chat);

            StickersPanel.UpdateChatPermissions(ViewModel.ClientService, chat);
        }

        public void UpdateChatPendingJoinRequests(Chat chat)
        {
            JoinRequests.UpdateChat(chat);
        }

        public void UpdateChatIsTranslatable(Chat chat, string language)
        {
            TranslateHeader.UpdateChatIsTranslatable(chat, language);
        }

        public void UpdateChatTitle(Chat chat)
        {
            if (ViewModel.Type == DialogType.Thread)
            {
                if (ViewModel.Topic != null)
                {
                    ChatTitle = ViewModel.Topic.Info.Name;
                }
                else
                {
                    var message = ViewModel.Thread?.Messages.LastOrDefault();
                    if (message == null || message.InteractionInfo?.ReplyInfo == null)
                    {
                        return;
                    }

                    if (ViewModel.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
                    {
                        if (senderChat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                        {
                            ChatTitle = Locale.Declension(Strings.R.Comments, message.InteractionInfo.ReplyInfo.ReplyCount);
                        }
                        else
                        {
                            ChatTitle = Locale.Declension(Strings.R.Replies, message.InteractionInfo.ReplyInfo.ReplyCount);
                        }
                    }
                    else
                    {
                        ChatTitle = Locale.Declension(Strings.R.Replies, message.InteractionInfo.ReplyInfo.ReplyCount);
                    }
                }
            }
            else if (ViewModel.Type == DialogType.ScheduledMessages)
            {
                ChatTitle = ViewModel.ClientService.IsSavedMessages(chat) ? Strings.Reminders : Strings.ScheduledMessages;
            }
            else if (ViewModel.Type == DialogType.SavedMessagesTopic)
            {
                if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeMyNotes)
                {
                    ChatTitle = Strings.MyNotes;
                }
                else if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeAuthorHidden)
                {
                    ChatTitle = Strings.AnonymousForward;
                }
                else if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && ViewModel.ClientService.TryGetChat(savedFromChat.ChatId, out Chat savedChat))
                {
                    ChatTitle = ViewModel.ClientService.GetTitle(savedChat);
                }
            }
            else if (ViewModel.Type == DialogType.BusinessReplies && ViewModel.QuickReplyShortcut is QuickReplyShortcut shortcut)
            {
                ChatTitle = shortcut.Name switch
                {
                    "away" => Strings.BusinessAway,
                    "hello" => Strings.BusinessGreet,
                    _ => shortcut.Name
                };
            }
            else if (chat.Type is ChatTypeSecret)
            {
                ChatTitle = Icons.LockClosedFilled14 + "\u00A0" + ViewModel.ClientService.GetTitle(chat);
            }
            else
            {
                ChatTitle = ViewModel.ClientService.GetTitle(chat);
            }

            Title.Text = ChatTitle;

            if (!WindowContext.Current.IsInMainView)
            {
                // Would be cool to do this in MasterDetailView
                ApplicationView.GetForCurrentView().Title = ChatTitle;
            }
        }

        public string ChatTitle { get; private set; }

        public void UpdateChatPhoto(Chat chat)
        {
            if (ViewModel.Type == DialogType.Thread)
            {
                if (ViewModel.Topic != null)
                {
                    LoadObject(ref Icon, nameof(Icon));
                    Icon.Source = new CustomEmojiFileSource(ViewModel.ClientService, ViewModel.Topic.Info.Icon.CustomEmojiId);
                    Photo.Clear();
                }
                else
                {
                    UnloadObject(Icon);
                    Photo.Source = PlaceholderImage.GetGlyph(Icons.ArrowReplyFilled, 5);
                }
            }
            else if (ViewModel.Type == DialogType.SavedMessagesTopic)
            {
                UnloadObject(Icon);

                if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeMyNotes)
                {
                    Photo.Source = PlaceholderImage.GetGlyph(Icons.MyNotesFilled, 5);
                    Photo.Shape = ProfilePictureShape.Ellipse;
                }
                else if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeAuthorHidden)
                {
                    Photo.Source = PlaceholderImage.GetGlyph(Icons.AuthorHiddenFilled, 5);
                    Photo.Shape = ProfilePictureShape.Ellipse;
                }
                else if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && ViewModel.ClientService.TryGetChat(savedFromChat.ChatId, out Chat savedChat))
                {
                    Photo.SetChat(ViewModel.ClientService, savedChat, 36);
                }
            }
            else
            {
                UnloadObject(Icon);
                Photo.SetChat(ViewModel.ClientService, chat, 36);
            }
        }

        public void UpdateChatLastMessage(Chat chat)
        {
            // Used in ProfilePage
        }

        public void UpdateChatEmojiStatus(Chat chat)
        {
            if (ViewModel.Type == DialogType.SavedMessagesTopic)
            {
                if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeMyNotes)
                {
                    Identity.ClearStatus();
                }
                else if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeAuthorHidden)
                {
                    Identity.ClearStatus();
                }
                else if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && ViewModel.ClientService.TryGetChat(savedFromChat.ChatId, out Chat savedChat))
                {
                    Identity.SetStatus(_viewModel.ClientService, savedChat);
                }
            }
            else
            {
                Identity.SetStatus(_viewModel.ClientService, chat);
            }
        }

        public void UpdateChatAccentColors(Chat chat)
        {
            // Not needed in chat view
        }

        public void UpdateChatActiveStories(Chat chat)
        {
            Segments.SetChat(ViewModel.ClientService, chat, 36);
        }

        public void UpdateChatHasScheduledMessages(Chat chat)
        {
            ButtonScheduled.Visibility = chat.HasScheduledMessages && ViewModel.Type == DialogType.History ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateChatActionBar(Chat chat)
        {
            if (ViewModel.Type == DialogType.History)
            {
                ActionBar.UpdateChatActionBar(chat);
            }
            else
            {
                ActionBar.UpdateChatActionBar(null);
            }
        }

        public void UpdateChatDefaultDisableNotification(Chat chat, bool defaultDisableNotification)
        {
            if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                ButtonSilent.IsChecked = defaultDisableNotification;
                Automation.SetToolTip(ButtonSilent, defaultDisableNotification ? Strings.AccDescrChanSilentOn : Strings.AccDescrChanSilentOff);
            }

            TextField.PlaceholderText = GetPlaceholder(chat, out bool readOnly);

            if (_isTextReadOnly != readOnly)
            {
                _isTextReadOnly = readOnly;
                TextField.IsReadOnly = readOnly;
            }
        }

        public void UpdateChatActions(Chat chat, IDictionary<MessageSender, ChatAction> actions)
        {
            if (chat.Type is ChatTypePrivate privata && privata.UserId == ViewModel.ClientService.Options.MyId)
            {
                ChatActionIndicator.UpdateAction(null);
                ChatActionPanel.Visibility = Visibility.Collapsed;
                Subtitle.Opacity = 1;
                return;
            }

            if (actions != null && actions.Count > 0 && (ViewModel.Type == DialogType.History || ViewModel.Type == DialogType.Thread))
            {
                ChatActionLabel.Text = InputChatActionManager.GetTypingString(chat.Type, actions, ViewModel.ClientService, out ChatAction commonAction);
                ChatActionIndicator.UpdateAction(commonAction);
                ChatActionPanel.Visibility = Visibility.Visible;
                Subtitle.Opacity = 0;
            }
            else
            {
                ChatActionLabel.Text = string.Empty;
                ChatActionIndicator.UpdateAction(null);
                ChatActionPanel.Visibility = Visibility.Collapsed;
                Subtitle.Opacity = 1;
            }

            //var peer = FrameworkElementAutomationPeer.FromElement(ChatActionLabel);
            //peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }


        public void UpdateChatNotificationSettings(Chat chat)
        {
            if (chat.Type is ChatTypeSupergroup super && super.IsChannel)
            {
                var group = ViewModel.ClientService.GetSupergroup(super.SupergroupId);
                if (group == null)
                {
                    return;
                }

                if (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanPostMessages)
                {
                }
                else if (group.Status is ChatMemberStatusLeft)
                {
                }
                else
                {
                    ShowAction(ViewModel.ClientService.Notifications.IsMuted(chat) ? Strings.ChannelUnmute : Strings.ChannelMute, true);
                }
            }
        }



        public void UpdateChatUnreadMentionCount(Chat chat, int count)
        {
            if (ViewModel.Type == DialogType.History && count > 0)
            {
                Arrows.UnreadMentionCount = count;
            }
            else
            {
                Arrows.UnreadMentionCount = 0;
            }
        }

        public void UpdateChatUnreadReactionCount(Chat chat, int count)
        {
            if (ViewModel.Type == DialogType.History && count > 0)
            {
                Arrows.UnreadReactionsCount = count;
            }
            else
            {
                Arrows.UnreadReactionsCount = 0;
            }
        }

        private string GetPlaceholder(Chat chat, out bool readOnly)
        {
            readOnly = false;

            if (ViewModel.ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return GetPlaceholder(chat, supergroup, out readOnly);
            }
            else if (ViewModel.ClientService.TryGetBasicGroup(chat, out BasicGroup basicGroup))
            {
                return GetPlaceholder(chat, basicGroup, out readOnly);
            }

            return Strings.TypeMessage;
        }

        private string GetPlaceholder(Chat chat, Supergroup supergroup, out bool readOnly)
        {
            readOnly = false;

            if (supergroup.IsChannel)
            {
                return chat.DefaultDisableNotification
                    ? Strings.ChannelSilentBroadcast
                    : Strings.ChannelBroadcast;
            }
            else if (chat.Permissions.CanSendBasicMessages is false && supergroup.Status is not ChatMemberStatusCreator and not ChatMemberStatusAdministrator)
            {
                readOnly = true;
                return Strings.PlainTextRestrictedHint;
            }
            else if (supergroup.Status is ChatMemberStatusCreator { IsAnonymous: true} || supergroup.Status is ChatMemberStatusAdministrator { Rights.IsAnonymous: true })
            {
                return Strings.SendAnonymously;
            }

            return Strings.TypeMessage;
        }

        private string GetPlaceholder(Chat chat, BasicGroup basicGroup, out bool readOnly)
        {
            readOnly = false;

            if (chat.Permissions.CanSendBasicMessages is false && basicGroup.Status is not ChatMemberStatusCreator and not ChatMemberStatusAdministrator)
            {
                readOnly = true;
                return Strings.PlainTextRestrictedHint;
            }

            return Strings.TypeMessage;
        }

        public void UpdateChatReplyMarkup(Chat chat, MessageViewModel message)
        {
            void SetReadOnly(bool readOnly)
            {
                if (_isTextReadOnly != readOnly)
                {
                    _isTextReadOnly = readOnly;
                    TextField.IsReadOnly = readOnly;
                }
            }

            if (message?.ReplyMarkup is ReplyMarkupForceReply forceReply && forceReply.IsPersonal)
            {
                ViewModel.ReplyToMessage(message);

                if (forceReply.InputFieldPlaceholder.Length > 0)
                {
                    TextField.PlaceholderText = forceReply.InputFieldPlaceholder;
                    SetReadOnly(false);
                }
                else
                {
                    TextField.PlaceholderText = GetPlaceholder(chat, out bool readOnly);
                    SetReadOnly(readOnly);
                }

                ButtonMarkup.Visibility = Visibility.Collapsed;
                ShowHideMarkup(false, false);
            }
            else
            {
                var updated = ReplyMarkup.Update(message, message?.ReplyMarkup, false);
                if (updated)
                {
                    if (message.ReplyMarkup is ReplyMarkupShowKeyboard showKeyboard && showKeyboard.InputFieldPlaceholder.Length > 0)
                    {
                        TextField.PlaceholderText = showKeyboard.InputFieldPlaceholder;
                        SetReadOnly(false);
                    }
                    else
                    {
                        TextField.PlaceholderText = GetPlaceholder(chat, out bool readOnly);
                        SetReadOnly(readOnly);
                    }

                    ButtonMarkup.Visibility = Visibility.Visible;
                    ShowHideMarkup(true);
                }
                else
                {
                    TextField.PlaceholderText = GetPlaceholder(chat, out bool readOnly);
                    SetReadOnly(readOnly);

                    ButtonMarkup.Visibility = Visibility.Collapsed;
                    ShowHideMarkup(false, false);
                }
            }
        }

        public void UpdatePinnedMessage(Chat chat, bool known)
        {
            PinnedMessage.UpdateMessage(chat, null, known, 0, 1, false);
        }

        public void UpdateCallbackQueryAnswer(Chat chat, MessageViewModel message)
        {
            if (message == null)
            {
                CallbackQueryAnswerPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                CallbackQueryAnswerPanel.Visibility = Visibility.Visible;
                CallbackQueryAnswer.UpdateMessage(message, false, null);

                if (message.Id == 0 && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
                {
                    CallbackQueryAnswer.Focus(FocusState.Keyboard);
                }
                else
                {
                    var peer = FrameworkElementAutomationPeer.FromElement(CallbackQueryAnswer);
                    peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                }
            }
        }

        public void UpdateComposerHeader(Chat chat, MessageComposerHeader header)
        {
            CheckButtonsVisibility();

            if (header == null || (header.IsEmpty && header.LinkPreviewDisabled))
            {
                // Let's reset
                //ComposerHeader.Visibility = Visibility.Collapsed;
                ShowHideComposerHeader(false);
                ComposerHeaderReference.Message = null;

                ButtonAttach.Glyph = Icons.Attach24;
                ButtonAttach.IsEnabled = true;

                SecondaryButtonsPanel.Visibility = Visibility.Visible;
                //ButtonRecord.Visibility = Visibility.Visible;

                //CheckButtonsVisibility();
            }
            else
            {
                //ComposerHeader.Visibility = Visibility.Visible;
                ShowHideComposerHeader(true);
                ComposerHeaderReference.Message = header;

                TextField.Reply = header;

                var editing = header.EditingMessage;
                if (editing != null)
                {
                    switch (editing.Content)
                    {
                        case MessageAnimation:
                        case MessageAudio:
                        case MessageDocument:
                            ButtonAttach.Glyph = Icons.Replace;
                            ButtonAttach.IsEnabled = true;
                            break;
                        case MessagePhoto photo:
                            ButtonAttach.Glyph = !photo.IsSecret ? Icons.Replace : Icons.Attach24;
                            ButtonAttach.IsEnabled = !photo.IsSecret;
                            break;
                        case MessageVideo video:
                            ButtonAttach.Glyph = !video.IsSecret ? Icons.Replace : Icons.Attach24;
                            ButtonAttach.IsEnabled = !video.IsSecret;
                            break;
                        default:
                            ButtonAttach.Glyph = Icons.Attach24;
                            ButtonAttach.IsEnabled = false;
                            break;
                    }


                    ComposerHeaderGlyph.Glyph = Icons.Edit24;

                    Automation.SetToolTip(ComposerHeaderCancel, Strings.AccDescrCancelEdit);

                    SecondaryButtonsPanel.Visibility = Visibility.Collapsed;
                    //ButtonRecord.Visibility = Visibility.Collapsed;

                    //CheckButtonsVisibility();
                }
                else
                {
                    ButtonAttach.Glyph = Icons.Attach24;
                    ButtonAttach.IsEnabled = true;

                    if (header.LinkPreview != null)
                    {
                        ComposerHeaderGlyph.Glyph = Icons.Link24;
                    }
                    else if (header.ReplyToMessage != null)
                    {
                        ComposerHeaderGlyph.Glyph = Icons.ArrowReply24;
                    }
                    else
                    {
                        ComposerHeaderGlyph.Glyph = Icons.Loading;
                    }

                    Automation.SetToolTip(ComposerHeaderCancel, Strings.AccDescrCancelReply);

                    SecondaryButtonsPanel.Visibility = Visibility.Visible;
                    //ButtonRecord.Visibility = Visibility.Visible;

                    //CheckButtonsVisibility();
                }
            }
        }

        private bool _composerHeaderCollapsed = true;
        //private bool _botMenuButtonCollapsed = true;
        //private bool _aliasButtonCollapsed = true;
        private bool _autocompleteCollapsed = true;

        private void ShowHideComposerHeader(bool show, bool sendout = false)
        {
            if (ButtonAction.Visibility == Visibility.Visible)
            {
                if (_replyEnabled == true && show)
                {
                    ShowArea(false);
                }
                else
                {
                    _composerHeaderCollapsed = true;
                    ComposerHeader.Visibility = Visibility.Collapsed;

                    return;
                }
            }

            if (_composerHeaderCollapsed != show)
            {
                return;
            }

            var composer = ElementComposition.GetElementVisual(ComposerHeader);
            var messages = ElementComposition.GetElementVisual(Messages);
            var textArea = ElementComposition.GetElementVisual(TextArea);

            var value = show ? 48 : 0;

            var rect = textArea.Compositor.CreateRoundedRectangleGeometry();
            rect.CornerRadius = new Vector2(SettingsService.Current.Appearance.BubbleRadius);
            rect.Size = new Vector2(TextArea.ActualSize.X, 192 + 48);
            rect.Offset = new Vector2(0, value);

            textArea.Clip = textArea.Compositor.CreateGeometricClip(rect);

            if (messages.Clip is InsetClip messagesClip)
            {
                messagesClip.TopInset = -44 + value;
                messagesClip.BottomInset = -96;
            }
            else
            {
                messages.Clip = textArea.Compositor.CreateInsetClip(0, -44 + value, 0, -96);
            }

            composer.Clip = textArea.Compositor.CreateInsetClip(0, 0, 0, value);

            var batch = composer.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                textArea.Clip = null;
                composer.Clip = null;
                //messages.Clip = null;
                composer.Offset = new Vector3();
                messages.Offset = new Vector3();

                ContentPanel.Margin = new Thickness();

                if (_composerHeaderCollapsed)
                {
                    if (_replyEnabled.HasValue)
                    {
                        ShowAction(null, ButtonAction.IsEnabled, true);
                    }

                    ComposerHeader.Visibility = Visibility.Collapsed;
                }

                UpdateTextAreaRadius();
            };

            var animClip = textArea.Compositor.CreateScalarKeyFrameAnimation();
            animClip.InsertKeyFrame(0, show ? 48 : 0);
            animClip.InsertKeyFrame(1, show ? 0 : 48);
            animClip.Duration = Constants.FastAnimation;

            var animClip2 = textArea.Compositor.CreateScalarKeyFrameAnimation();
            animClip2.InsertKeyFrame(0, show ? -44 : -44 + 48);
            animClip2.InsertKeyFrame(1, show ? -44 + 48 : -44);
            animClip2.Duration = Constants.FastAnimation;

            var animClip3 = textArea.Compositor.CreateVector2KeyFrameAnimation();
            animClip3.InsertKeyFrame(0, new Vector2(0, show ? 48 : 0));
            animClip3.InsertKeyFrame(1, new Vector2(0, show ? 0 : 48));
            animClip3.Duration = Constants.FastAnimation;

            var anim1 = textArea.Compositor.CreateVector3KeyFrameAnimation();
            anim1.InsertKeyFrame(0, new Vector3(0, show ? 48 : 0, 0));
            anim1.InsertKeyFrame(1, new Vector3(0, show ? 0 : 48, 0));
            anim1.Duration = Constants.FastAnimation;

            rect.StartAnimation("Offset", animClip3);

            if (!sendout)
            {
                messages.Clip.StartAnimation("TopInset", animClip2);
                messages.StartAnimation("Offset", anim1);
            }

            composer.Clip.StartAnimation("BottomInset", animClip);
            composer.StartAnimation("Offset", anim1);

            batch.End();

            if (sendout)
            {
                ContentPanel.Margin = new Thickness(0, 0, 0, -48);
            }
            else
            {
                ContentPanel.Margin = new Thickness(0, -48, 0, 0);
            }

            if (show)
            {
                _composerHeaderCollapsed = false;
                ComposerHeader.Visibility = Visibility.Visible;
            }
            else
            {
                _composerHeaderCollapsed = true;
            }
        }

        enum SideButton
        {
            None,
            BotMenu,
            Alias
        }

        private SideButton _sideMenuCollapsed;

        private async void ShowHideSideButton(SideButton next)
        {
            if (_sideMenuCollapsed == next)
            {
                return;
            }

            var alias1 = false;
            var menu1 = false;
            var none1 = _sideMenuCollapsed == SideButton.None;
            var prev = _sideMenuCollapsed;

            if (next == SideButton.Alias || _sideMenuCollapsed == SideButton.Alias)
            {
                alias1 = true;
                ButtonAlias.Visibility = Visibility.Visible;
            }

            if (next == SideButton.BotMenu || _sideMenuCollapsed == SideButton.BotMenu)
            {
                menu1 = true;
                ButtonMore.Visibility = Visibility.Visible;
            }

            _sideMenuCollapsed = next;

            if (next == SideButton.BotMenu)
            {
                await ButtonMore.UpdateLayoutAsync();
            }
            else if (next == SideButton.Alias)
            {
                await ButtonAlias.UpdateLayoutAsync();
            }

            var more = ElementComposition.GetElementVisual(ButtonMore);
            var alias = ElementComposition.GetElementVisual(ButtonAlias);
            var field = ElementComposition.GetElementVisual(TextFieldPanel);
            var attach = ElementComposition.GetElementVisual(btnAttach);

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                field.Properties.InsertVector3("Translation", Vector3.Zero);
                attach.Properties.InsertVector3("Translation", Vector3.Zero);

                if (_sideMenuCollapsed != SideButton.BotMenu)
                {
                    TextField.IsMenuExpanded = false;
                    ButtonMore.Visibility = Visibility.Collapsed;
                }

                if (_sideMenuCollapsed != SideButton.Alias)
                {
                    ButtonAlias.Visibility = Visibility.Collapsed;
                }

                UpdateTextAreaRadius();
            };

            var offset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (next == SideButton.Alias)
            {
                if (prev == SideButton.BotMenu)
                {
                    offset.InsertKeyFrame(0, new Vector3(0, 0, 0));
                    offset.InsertKeyFrame(1, new Vector3(ButtonAlias.ActualSize.X - ButtonMore.ActualSize.X, 0, 0));
                }
                else
                {
                    offset.InsertKeyFrame(0, new Vector3(-ButtonAlias.ActualSize.X - 8, 0, 0));
                    offset.InsertKeyFrame(1, new Vector3());
                }
            }
            else if (next == SideButton.BotMenu)
            {
                if (prev == SideButton.Alias)
                {
                    offset.InsertKeyFrame(0, new Vector3(ButtonAlias.ActualSize.X - ButtonMore.ActualSize.X, 0, 0));
                    offset.InsertKeyFrame(1, new Vector3(0, 0, 0));
                }
                else
                {
                    offset.InsertKeyFrame(0, new Vector3(-ButtonMore.ActualSize.X - 8, 0, 0));
                    offset.InsertKeyFrame(1, new Vector3());
                }
            }
            else if (prev == SideButton.BotMenu)
            {
                offset.InsertKeyFrame(1, new Vector3(-ButtonMore.ActualSize.X - 8, 0, 0));
                offset.InsertKeyFrame(0, new Vector3());
            }
            else if (prev == SideButton.Alias)
            {
                offset.InsertKeyFrame(1, new Vector3(-ButtonAlias.ActualSize.X - 8, 0, 0));
                offset.InsertKeyFrame(0, new Vector3());
            }

            offset.Duration = Constants.FastAnimation;

            var scaleShow = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            scaleShow.InsertKeyFrame(0, Vector3.Zero);
            scaleShow.InsertKeyFrame(1, Vector3.One);
            scaleShow.Duration = Constants.FastAnimation;

            var opacityShow = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacityShow.InsertKeyFrame(0, 0);
            opacityShow.InsertKeyFrame(1, 1);
            opacityShow.Duration = Constants.FastAnimation;

            var scaleHide = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            scaleHide.InsertKeyFrame(0, Vector3.One);
            scaleHide.InsertKeyFrame(1, Vector3.Zero);
            scaleHide.Duration = Constants.FastAnimation;

            var opacityHide = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacityHide.InsertKeyFrame(0, 1);
            opacityHide.InsertKeyFrame(1, 0);
            opacityHide.Duration = Constants.FastAnimation * 10;

            more.CenterPoint = new Vector3(16, 16, 0);
            alias.CenterPoint = new Vector3(16, 16, 0);

            if (alias1)
            {
                alias.StartAnimation("Scale", next == SideButton.Alias ? scaleShow : scaleHide);
                alias.StartAnimation("Opacity", next == SideButton.Alias ? opacityShow : opacityHide);
            }

            if (menu1)
            {
                more.StartAnimation("Scale", next == SideButton.BotMenu ? scaleShow : scaleHide);
                more.StartAnimation("Opacity", next == SideButton.BotMenu ? opacityShow : opacityHide);
            }

            field.StartAnimation("Translation", offset);
            attach.StartAnimation("Translation", offset);

            batch.End();
        }

        private async void ShowHideAutocomplete(bool show)
        {
            if (_autocompleteCollapsed != show)
            {
                return;
            }

            _autocompleteCollapsed = !show;
            ListAutocomplete.Visibility = Visibility.Visible;

            var source = ListAutocomplete.ItemsSource;

            var list = ElementComposition.GetElementVisual(ListAutocomplete);
            list.StopAnimation("Translation");

            await ListAutocomplete.UpdateLayoutAsync();

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                list.Properties.InsertVector3("Translation", Vector3.Zero);

                if (show)
                {
                    _autocompleteCollapsed = false;
                }
                else if (ListAutocomplete.ItemsSource == source)
                {
                    ListAutocomplete.ItemsSource = null;
                    ListAutocomplete.Visibility = Visibility.Collapsed;
                }
            };

            var offset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, ListAutocomplete.ActualSize.Y, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            list.StartAnimation("Translation", offset);

            batch.End();
        }

        private double _textAreaRadius = double.NaN;

        private void UpdateTextAreaRadius(bool force = true)
        {
            var radius = SettingsService.Current.Appearance.BubbleRadius;
            if (radius == _textAreaRadius && !force)
            {
                return;
            }

            _textAreaRadius = radius;

            var min = Math.Max(4, radius - 2);
            var max = ComposerHeader.Visibility == Visibility.Visible ? 4 : min;

            ButtonAttach.CornerRadius = new CornerRadius(_sideMenuCollapsed == SideButton.None ? max : 4, 4, 4, _sideMenuCollapsed == SideButton.None ? min : 4);
            btnVoiceMessage.CornerRadius = new CornerRadius(4, max, min, 4);
            btnSendMessage.CornerRadius = new CornerRadius(4, max, min, 4);
            btnEdit.CornerRadius = new CornerRadius(4, max, min, 4);
            ButtonDelete.CornerRadius = new CornerRadius(4, min, min, 4);
            ButtonManage.CornerRadius = new CornerRadius(min, 4, 4, min);

            ComposerHeaderCancel.CornerRadius = new CornerRadius(4, min, 4, 4);
            TextRoot.CornerRadius =
                ChatFooter.CornerRadius =
                ChatRecord.CornerRadius =
                ManagePanel.CornerRadius =
                ButtonAction.CornerRadius = new CornerRadius(radius);

            // It would be cool to have shadow to respect text field corner radius
            //Separator.CornerRadius = new CornerRadius(radius);
            ListAutocomplete.CornerRadius = InlinePanel.CornerRadius = new CornerRadius(radius, radius, 0, 0);
            ListAutocomplete.Padding = new Thickness(0, 0, 0, radius);

            ListInline?.UpdateCornerRadius(radius);

            ReplyMarkupPanel.CornerRadius = new CornerRadius(0, 0, radius, radius);
            ReplyMarkupPanel.Padding = new Thickness(0, radius, 0, 0);

            if (radius > 0)
            {
                TextArea.MaxWidth = ChatRecord.MaxWidth = ChatFooter.MaxWidth = ManagePanel.MaxWidth = InlinePanel.MaxWidth = Separator.MaxWidth = ReplyMarkupPanel.MaxWidth =
                    SettingsService.Current.IsAdaptiveWideEnabled ? 1000 : double.PositiveInfinity;
                TextArea.Margin = ChatRecord.Margin = ChatFooter.Margin = ManagePanel.Margin = Separator.Margin = new Thickness(12, 0, 12, 8);
                InlinePanel.Margin = new Thickness(12, 0, 12, -radius);
                ReplyMarkupPanel.Margin = new Thickness(12, -8 - radius, 12, 8);
            }
            else
            {
                TextArea.MaxWidth = ChatRecord.MaxWidth = ChatFooter.MaxWidth = ManagePanel.MaxWidth = InlinePanel.MaxWidth = Separator.MaxWidth = ReplyMarkupPanel.MaxWidth =
                    SettingsService.Current.IsAdaptiveWideEnabled ? 1024 : double.PositiveInfinity;
                TextArea.Margin = ChatRecord.Margin = ChatFooter.Margin = ManagePanel.Margin = Separator.Margin = new Thickness();
                InlinePanel.Margin = new Thickness();
                ReplyMarkupPanel.Margin = new Thickness();
            }

            var messages = ElementComposition.GetElementVisual(Messages);
            if (messages.Clip is InsetClip messagesClip)
            {
                messagesClip.TopInset = -44;
                messagesClip.BottomInset = -8 - radius;
            }
            else
            {
                messages.Clip = messages.Compositor.CreateInsetClip(0, -44, 0, -8 - radius);
            }
        }

        public void UpdateAutocomplete(Chat chat, IAutocompleteCollection collection)
        {
            if (collection != null)
            {
                ListAutocomplete.ItemsSource = collection;
                ListAutocomplete.Orientation = collection.Orientation;
                ShowHideAutocomplete(true);
            }
            else
            {
                ShowHideAutocomplete(false);

                //var diff = (float)ListAutocomplete.ActualHeight;
                //var visual = ElementComposition.GetElementVisual(ListAutocomplete);

                //var anim = BootStrapper.Current.Compositor.CreateSpringVector3Animation();
                //anim.InitialValue = new Vector3();
                //anim.FinalValue = new Vector3(0, diff, 0);

                //visual.StartAnimation("Offset", anim);
            }
        }

        public void UpdateSearchMask(Chat chat, ChatSearchViewModel search)
        {

        }



        public void UpdateUser(Chat chat, User user, bool secret)
        {

        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            btnSendMessage.SlowModeDelay = 0;
            btnSendMessage.SlowModeDelayExpiresIn = 0;

            if (!secret && !user.RestrictsNewChats)
            {
                ShowArea();
            }

            TextField.PlaceholderText = GetPlaceholder(chat, out bool readOnly);

            if (_isTextReadOnly != readOnly)
            {
                _isTextReadOnly = readOnly;
                TextField.IsReadOnly = readOnly;
            }

            UpdateUserStatus(chat, user);

            if (fullInfo == null)
            {
                ButtonMore.Content = Strings.BotsMenuTitle;

                return;
            }

            if (ViewModel.Search?.SavedMessagesTag != null)
            {
                ShowAction(ViewModel.Search.FilterByTag ? Strings.SavedTagShowOtherMessages : Strings.SavedTagHideOtherMessages, true);
            }
            else if (ViewModel.Type == DialogType.SavedMessagesTopic)
            {
                if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeMyNotes)
                {
                    ShowArea();
                }
                else if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeAuthorHidden)
                {
                    ShowAction(Strings.AuthorHiddenDescription, false);
                }
                else if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && ViewModel.ClientService.TryGetChat(savedFromChat.ChatId, out Chat savedChat))
                {
                    if (savedChat.Type is ChatTypePrivate)
                    {
                        ShowAction(Strings.SavedOpenChat, true);
                    }
                    else if (savedChat.Type is ChatTypeSupergroup { IsChannel: true })
                    {
                        ShowAction(Strings.SavedOpenChannel, true);
                    }
                    else
                    {
                        ShowAction(Strings.SavedOpenGroup, true);
                    }
                }
            }
            else if (ViewModel.Type == DialogType.Pinned)
            {
                ShowAction(Strings.UnpinAllMessages, true);
            }
            else if (user.Type is UserTypeDeleted)
            {
                ShowAction(Strings.DeleteThisChat, true);
            }
            else if (chat.Id == ViewModel.ClientService.Options.RepliesBotChatId || chat.Id == ViewModel.ClientService.Options.VerificationCodesBotChatId)
            {
                ShowAction(ViewModel.ClientService.Notifications.IsMuted(chat) ? Strings.ChannelUnmute : Strings.ChannelMute, true);
            }
            else if (chat.BlockList is BlockListMain)
            {
                ShowAction(user.Type is UserTypeBot ? Strings.BotUnblock : Strings.Unblock, true);
            }
            else if (user.Type is UserTypeBot && (accessToken || chat?.LastMessage == null))
            {
                ShowAction(Strings.BotStart, true);
            }
            else if (!secret && !user.RestrictsNewChats)
            {
                ShowArea();
            }

            if (fullInfo.BotInfo?.MenuButton != null)
            {
                ButtonMore.Content = fullInfo.BotInfo.MenuButton.Text;

                ViewModel.BotCommands = null;
                ViewModel.HasBotCommands = false;
                ShowHideSideButton(SideButton.BotMenu);
            }
            else if (fullInfo.BotInfo?.Commands.Count > 0)
            {
                ButtonMore.Content = Strings.BotsMenuTitle;

                ViewModel.BotCommands = fullInfo.BotInfo.Commands.Select(x => new UserCommand(user.Id, x)).ToList();
                ViewModel.HasBotCommands = false;
                ShowHideSideButton(SideButton.BotMenu);
            }
            else
            {
                ViewModel.BotCommands = null;
                ViewModel.HasBotCommands = false;
                ShowHideSideButton(SideButton.None);
            }

            Automation.SetToolTip(Call, Strings.Call);

            btnVoiceMessage.IsRestricted = fullInfo.HasRestrictedVoiceAndVideoNoteMessages
                && user.Id != ViewModel.ClientService.Options.MyId;

            Call.Glyph = Icons.Call;
            Call.Visibility = /*!secret &&*/ fullInfo.CanBeCalled ? Visibility.Visible : Visibility.Collapsed;
            VideoCall.Visibility = /*!secret &&*/ fullInfo.CanBeCalled && fullInfo.SupportsVideoCalls ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            var options = ViewModel.ClientService.Options;
            if (chat.Id == options.MyId || chat.Id == options.RepliesBotChatId || chat.Id == options.VerificationCodesBotChatId)
            {
                ViewModel.LastSeen = null;
            }
            else if (ViewModel.Type == DialogType.ScheduledMessages)
            {
                ViewModel.LastSeen = null;
            }
            else
            {
                ViewModel.LastSeen = LastSeenConverter.GetLabel(user, true);
            }
        }

        public void UpdateUserRestrictsNewChats(Chat chat, User user, UserFullInfo fullInfo, CanSendMessageToUserResult result)
        {
            if (result is CanSendMessageToUserResultOk)
            {
                ShowArea();
            }
            else if (result is CanSendMessageToUserResultUserIsDeleted)
            {
                ShowAction(Strings.DeleteThisChat, true);
            }
            else if (result is CanSendMessageToUserResultUserRestrictsNewChats)
            {
                var text = string.Format(Strings.OnlyPremiumCanMessage, user.FirstName);
                var markdown = Extensions.ReplacePremiumLink(text, null);

                var textBlock = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Style = App.Current.Resources["InfoBodyTextBlockStyle"] as Style
                };

                TextBlockHelper.SetFormattedText(textBlock, markdown);

                _replyEnabled = false;
                ButtonAction.IsEnabled = true;
                ButtonAction.Content = textBlock;

                _actionCollapsed = false;
                ButtonAction.Visibility = Visibility.Visible;
                ChatFooter.Visibility = Visibility.Visible;
                TextArea.Visibility = Visibility.Collapsed;
            }

            if (ViewModel.Type == DialogType.History && chat.LastMessage == null && user?.Type is UserTypeRegular && user?.Id != ViewModel.ClientService.Options.MyId && fullInfo != null)
            {
                if (result is CanSendMessageToUserResultUserRestrictsNewChats)
                {
                    ShowHideRestrictsNewChats(true, user);
                    ShowHideEmptyChat(false, null, null);
                }
                else
                {
                    ShowHideRestrictsNewChats(false, null);
                    ShowHideEmptyChat(true, user, fullInfo);
                }
            }
            else
            {
                ShowHideRestrictsNewChats(false, null);
                ShowHideEmptyChat(false, null, null);
            }
        }

        public void UpdateSecretChat(Chat chat, SecretChat secretChat)
        {
            if (secretChat.State is SecretChatStateReady)
            {
                ShowArea();
            }
            else if (secretChat.State is SecretChatStatePending)
            {
                ShowAction(string.Format(Strings.AwaitingEncryption, ViewModel.ClientService.GetTitle(chat)), false);
            }
            else if (secretChat.State is SecretChatStateClosed)
            {
                ShowAction(Strings.EncryptionRejected, false);
            }
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {

        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            if (group.UpgradedToSupergroupId != 0)
            {
                ShowAction(Strings.OpenSupergroup, true);
            }
            else if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
            {
                ShowAction(Strings.DeleteThisGroup, true);

                ViewModel.LastSeen = Strings.YouLeft;
            }
            else if (group.Status is ChatMemberStatusCreator creator && !creator.IsMember)
            {
                ShowAction(Strings.ChannelJoin, true);
            }
            else
            {
                if (ViewModel.Type == DialogType.Pinned)
                {
                    if (group.CanPinMessages())
                    {
                        ShowAction(Strings.UnpinAllMessages, true);
                    }
                    else
                    {
                        ShowAction(Strings.HidePinnedMessages, true);
                    }
                }
                else
                {
                    ShowArea();
                }

                TextField.PlaceholderText = GetPlaceholder(chat, group, out bool readOnly);

                if (_isTextReadOnly != readOnly)
                {
                    _isTextReadOnly = readOnly;
                    TextField.IsReadOnly = readOnly;
                }

                ViewModel.LastSeen = Locale.Declension(Strings.R.Members, group.MemberCount);
            }

            if (fullInfo == null)
            {
                return;
            }

            ViewModel.LastSeen = Locale.Declension(Strings.R.Members, fullInfo.Members.Count);

            btnVoiceMessage.IsRestricted = false;

            btnSendMessage.SlowModeDelay = 0;
            btnSendMessage.SlowModeDelayExpiresIn = 0;

            var commands = new List<UserCommand>();

            foreach (var command in fullInfo.BotCommands)
            {
                commands.AddRange(command.Commands.Select(x => new UserCommand(command.BotUserId, x)));
            }

            ViewModel.BotCommands = commands;
            ViewModel.HasBotCommands = commands.Count > 0;
            //ShowHideBotCommands(false);
        }



        public void UpdateSupergroup(Chat chat, Supergroup group)
        {

        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            if (ViewModel.Type == DialogType.EventLog)
            {
                ShowAction(Strings.Settings, true);
                return;
            }

            if (ViewModel.Type == DialogType.Pinned)
            {
                if (group.CanPinMessages())
                {
                    ShowAction(Strings.UnpinAllMessages, true);
                }
                else
                {
                    ShowAction(Strings.HidePinnedMessages, true);
                }
            }
            else if (group.IsChannel || group.IsBroadcastGroup)
            {
                if ((group.Status is ChatMemberStatusLeft && (group.HasActiveUsername() || ViewModel.ClientService.IsChatAccessible(chat))) || group.Status is ChatMemberStatusCreator { IsMember: false })
                {
                    ShowAction(Strings.ChannelJoin, true);
                }
                else if (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanPostMessages)
                {
                    ShowArea();
                }
                else if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
                {
                    ShowAction(Strings.DeleteChat, true);
                }
                else
                {
                    ShowAction(ViewModel.ClientService.Notifications.IsMuted(chat) ? Strings.ChannelUnmute : Strings.ChannelMute, true);
                }
            }
            else
            {
                if ((group.Status is ChatMemberStatusLeft && (group.IsPublic() || ViewModel.ClientService.IsChatAccessible(chat))) || group.Status is ChatMemberStatusCreator { IsMember: false })
                {
                    if (group.JoinByRequest)
                    {
                        ShowAction(Strings.ChannelJoinRequest, true);
                    }
                    else if (ViewModel.Type == DialogType.Thread)
                    {
                        if (group.JoinToSendMessages)
                        {
                            ShowAction(Strings.JoinGroup, true);
                        }
                        else if (!chat.Permissions.CanSendBasicMessages)
                        {
                            ShowAction(Strings.GlobalSendMessageRestricted, false);
                        }
                        else
                        {
                            ShowArea();
                        }
                    }
                    else
                    {
                        ShowAction(Strings.ChannelJoin, true);
                    }
                }
                else if (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator administrator)
                {
                    if (ViewModel.Type != DialogType.Thread && group.IsForum)
                    {
                        ShowAction(Strings.ForumReplyToMessagesInTopic, false, true);
                    }
                    else
                    {
                        ShowArea();
                    }
                }
                else if (group.Status is ChatMemberStatusRestricted restrictedSend)
                {
                    if (!restrictedSend.IsMember && group.HasActiveUsername())
                    {
                        ShowAction(Strings.ChannelJoin, true);
                    }
                    else if (!restrictedSend.Permissions.CanSendBasicMessages)
                    {
                        if (restrictedSend.IsForever())
                        {
                            ShowAction(Strings.SendMessageRestrictedForever, false);
                        }
                        else
                        {
                            ShowAction(string.Format(Strings.SendMessageRestricted, Formatter.BannedUntil(restrictedSend.RestrictedUntilDate)), false);
                        }
                    }
                    else if (ViewModel.Type != DialogType.Thread && group.IsForum)
                    {
                        ShowAction(Strings.ForumReplyToMessagesInTopic, false, true);
                    }
                    else
                    {
                        ShowArea();
                    }
                }
                else if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
                {
                    ShowAction(Strings.DeleteChat, true);
                }
                else if (!chat.Permissions.CanSendBasicMessages && (fullInfo == null || fullInfo.MyBoostCount < fullInfo.UnrestrictBoostCount))
                {
                    ShowAction(Strings.GlobalSendMessageRestricted, fullInfo != null && fullInfo.UnrestrictBoostCount > 0);
                }
                else if (ViewModel.Type != DialogType.Thread && group.IsForum)
                {
                    ShowAction(Strings.ForumReplyToMessagesInTopic, false, true);
                }
                else
                {
                    ShowArea();
                }
            }

            TextField.PlaceholderText = GetPlaceholder(chat, group, out bool readOnly);

            if (_isTextReadOnly != readOnly)
            {
                _isTextReadOnly = readOnly;
                TextField.IsReadOnly = readOnly;
            }

            if (ViewModel.Type == DialogType.History)
            {
                ViewModel.LastSeen = Locale.Declension(group.IsChannel ? Strings.R.Subscribers : Strings.R.Members, group.MemberCount);
            }
            else if (ViewModel.Type == DialogType.Thread && ViewModel.Topic != null)
            {
                ViewModel.LastSeen = string.Format(Strings.TopicProfileStatus, chat.Title);
            }
            else
            {
                ViewModel.LastSeen = null;
            }

            if (group.IsChannel)
            {
                return;
            }

            UpdateComposerHeader(chat, ViewModel.ComposerHeader);
            UpdateChatPermissions(chat);

            if (fullInfo == null)
            {
                return;
            }

            if (ViewModel.Type == DialogType.History)
            {
                ViewModel.LastSeen = Locale.Declension(group.IsChannel ? Strings.R.Subscribers : Strings.R.Members, fullInfo.MemberCount);
            }
            else if (ViewModel.Type == DialogType.Thread && ViewModel.Topic != null)
            {
                ViewModel.LastSeen = string.Format(Strings.TopicProfileStatus, chat.Title);
            }
            else
            {
                ViewModel.LastSeen = null;
            }

            btnVoiceMessage.IsRestricted = false;

            btnSendMessage.SlowModeDelay = fullInfo.SlowModeDelay;
            btnSendMessage.SlowModeDelayExpiresIn = fullInfo.SlowModeDelayExpiresIn;

            if (fullInfo.SlowModeDelayExpiresIn > 0)
            {
                _slowModeTimer.Stop();
                _slowModeTimer.Start();
            }
            else
            {
                _slowModeTimer.Stop();
            }

            var commands = new List<UserCommand>();

            foreach (var command in fullInfo.BotCommands)
            {
                commands.AddRange(command.Commands.Select(x => new UserCommand(command.BotUserId, x)));
            }

            ViewModel.BotCommands = commands;
            ViewModel.HasBotCommands = commands.Count > 0;
            //ShowHideBotCommands(false);
        }

        public void UpdateChatVideoChat(Chat chat, VideoChat videoChat)
        {
            if (chat.Type is ChatTypeBasicGroup or ChatTypeSupergroup)
            {
                if (videoChat.GroupCallId == 0)
                {
                    GroupCall.ShowHide(false);
                    Call.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void UpdateChatBusinessBotManageBar(Chat chat, BusinessBotManageBar businessBotManageBar)
        {
            ConnectedBot.UpdateChatBusinessBotManageBar(chat, businessBotManageBar);
            ViewVisibleMessages(false);
        }

        public void UpdateGroupCall(Chat chat, GroupCall groupCall)
        {
            if (GroupCall.UpdateGroupCall(chat, groupCall))
            {
                Call.Visibility = Visibility.Collapsed;
            }
            else
            {
                Automation.SetToolTip(Call, Strings.VoipGroupJoinCall);

                Call.Glyph = Icons.VideoChat;
                Call.Visibility = Visibility.Visible;
            }
        }

        public void UpdateDeleteMessages(Chat chat, IList<MessageViewModel> messages)
        {
            if (messages.Count > 1)
            {
                return;
            }

            if (_messageIdToSelector.TryGetValue(messages[0].Id, out SelectorItem selector))
            {
                AnimateSizeChanged(messages[0], selector);
            }
        }

        private void AnimateSizeChanged(MessageViewModel message, SelectorItem selector)
        {
            var next = new Vector2(0, 0);
            var prev = selector.ActualSize;

            var diff = next.Y - prev.Y;

            var panel = Messages.ItemsPanelRoot as ItemsStackPanel;
            if (panel == null || prev.Y == next.Y || Math.Abs(diff) <= 2)
            {
                return;
            }

            var index = Messages.IndexFromContainer(selector);
            //if (index < panel.LastVisibleIndex)
            //{
            //    return;
            //}

            if (index >= panel.FirstVisibleIndex && index <= panel.LastVisibleIndex)
            {
                var direction = panel.ItemsUpdatingScrollMode == ItemsUpdatingScrollMode.KeepItemsInView ? -1 : 1;
                var edge = (index == panel.LastVisibleIndex && direction == 1) || index == panel.FirstVisibleIndex && direction == -1;

                if (edge && !Messages.VisualContains(selector))
                {
                    direction *= -1;
                }

                var first = direction == 1 ? panel.FirstCacheIndex : index + 1;
                var last = direction == 1 ? index : panel.LastCacheIndex;

                var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                var anim = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                anim.InsertKeyFrame(0, diff * direction);
                anim.InsertKeyFrame(1, 0);
                //anim.Duration = TimeSpan.FromSeconds(5);

                for (int i = first; i <= last; i++)
                {
                    var container = Messages.ContainerFromIndex(i) as SelectorItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var child = VisualTreeHelper.GetChild(container, 0) as UIElement;
                    if (child != null)
                    {
                        var visual = ElementComposition.GetElementVisual(child);
                        visual.StartAnimation("Offset.Y", anim);
                    }
                }

                batch.End();
            }
        }

        #endregion

        private void TextField_Sending(object sender, EventArgs e)
        {
            ButtonStickers.Collapse();
            RemoveMessageEffect();
        }

        private void ButtonMore_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.ClientService.TryGetUserFull(ViewModel.Chat, out UserFullInfo fullInfo))
            {
                if (fullInfo.BotInfo?.MenuButton != null)
                {
                    ViewModel.OpenMiniApp(fullInfo.BotInfo.MenuButton.Url);
                }
                else
                {
                    TextField.IsMenuExpanded = !TextField.IsMenuExpanded;
                }
            }
        }

        private async void ButtonAlias_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null || chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                return;
            }

            var flyout = new MenuFlyout();
            flyout.Items.Add(new MenuFlyoutLabel { Text = Strings.SendMessageAsTitle });
            flyout.Closing += (s, args) =>
            {
                _focusState.Set(FocusState.Programmatic);
            };

            var response = await ViewModel.ClientService.SendAsync(new GetChatAvailableMessageSenders(chat.Id));
            if (response is ChatMessageSenders senders)
            {
                void handler(object sender, RoutedEventArgs _)
                {
                    if (sender is MenuFlyoutItem item && item.CommandParameter is ChatMessageSender messageSender)
                    {
                        item.Click -= handler;
                        ViewModel.SetSender(messageSender);
                    }
                }

                foreach (var messageSender in senders.Senders)
                {
                    var picture = new ProfilePicture();
                    picture.Width = 36;
                    picture.Height = 36;
                    picture.Margin = new Thickness(-4, -2, 0, -2);

                    var item = new MenuFlyoutProfile();
                    item.Click += handler;
                    item.CommandParameter = messageSender;
                    item.Style = BootStrapper.Current.Resources["SendAsMenuFlyoutItemStyle"] as Style;
                    item.Icon = new FontIcon();
                    item.Tag = picture;

                    if (ViewModel.ClientService.TryGetUser(messageSender.Sender, out User senderUser))
                    {
                        picture.SetUser(ViewModel.ClientService, senderUser, 36);

                        item.Text = senderUser.FullName();
                        item.Info = Strings.VoipGroupPersonalAccount;
                    }
                    else if (ViewModel.ClientService.TryGetChat(messageSender.Sender, out Chat senderChat))
                    {
                        picture.SetChat(ViewModel.ClientService, senderChat, 36);

                        item.Text = senderChat.Title;

                        if (ViewModel.ClientService.TryGetSupergroup(senderChat, out Supergroup supergroup))
                        {
                            item.Info = Locale.Declension(Strings.R.Subscribers, supergroup.MemberCount);
                        }
                    }

                    flyout.Items.Add(item);
                }
            }

            flyout.ShowAt(ButtonAlias, FlyoutPlacementMode.TopEdgeAlignedLeft);
        }

        private void InlineBotResults_Loaded(object sender, RoutedEventArgs e)
        {
            ListInline.UpdateCornerRadius(SettingsService.Current.Appearance.BubbleRadius);
            ListInline.MaxHeight = Math.Min(320, Math.Max(ContentPanel.ActualHeight - 48, 0));

            if (ViewModel?.Chat is Chat chat)
            {
                ListInline.UpdateChatPermissions(chat);
            }
        }

        #region XamlMarkupHelper

        private void LoadObject<T>(ref T element, /*[CallerArgumentExpression("element")]*/string name)
            where T : DependencyObject
        {
            if (element == null)
            {
                FindName(name);
            }
        }

        #endregion

        private void Stickers_Redirect(object sender, EventArgs e)
        {
            _focusState.Set(FocusState.Programmatic);
        }

        private void ChatRecord_StartTyping(object sender, ChatAction e)
        {
            ViewModel.ChatActionManager.SetTyping(e);
        }

        private void ChatRecord_CancelTyping(object sender, EventArgs e)
        {
            _focusState.Set(FocusState.Programmatic);
            ViewModel.ChatActionManager.CancelTyping();
        }

        private void RestrictsNewChats_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.ShowPromo();
        }

        private bool _restrictsNewChatsCollapsed = true;

        private void ShowHideRestrictsNewChats(bool show, User user)
        {
            if (_restrictsNewChatsCollapsed != show)
            {
                return;
            }

            _restrictsNewChatsCollapsed = !show;
            RestrictsNewChats ??= FindName(nameof(RestrictsNewChats)) as MessageService;

            if (show)
            {
                if (user != null)
                {
                    TextBlockHelper.SetMarkdown(RestrictsNewChatsText, string.Format(Strings.MessageLockedPremium, user.FirstName));
                }

                RestrictsNewChats.Visibility = Visibility.Visible;
            }
            else
            {
                RestrictsNewChats.Visibility = Visibility.Collapsed;
            }
        }

        private bool _emptyChatCollapsed = true;

        private void ShowHideEmptyChat(bool show, User user, UserFullInfo fullInfo)
        {
            //if (_emptyChatCollapsed != show)
            //{
            //    return;
            //}

            _emptyChatCollapsed = !show;

            if (show)
            {
                FindName(nameof(EmptyChatRoot));

                var title = fullInfo?.BusinessInfo?.StartPage?.Title;
                var message = fullInfo?.BusinessInfo?.StartPage?.Message;

                EmptyChatTitle.Text = string.IsNullOrEmpty(title)
                    ? Strings.NoMessages
                    : title;
                EmptyChatMessage.Text = string.IsNullOrEmpty(message)
                    ? Strings.NoMessagesGreetingsDescription
                    : message;

                var sticker = fullInfo?.BusinessInfo?.StartPage?.Sticker ?? ViewModel.GreetingSticker;
                if (sticker != null)
                {
                    EmptyChatAnimated.Source = new DelayedFileSource(ViewModel.ClientService, sticker);
                }
                else
                {
                    EmptyChatAnimated.Source = null;
                }

                TextBlockHelper.SetMarkdown(EmptyChatHow, string.Format(Strings.GreetingHow, user.FirstName));

                EmptyChatRoot.Visibility = Visibility.Visible;
                EmptyChatHowRoot.Visibility = fullInfo?.BusinessInfo?.StartPage != null
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else if (EmptyChatRoot != null)
            {
                EmptyChatRoot.Visibility = Visibility.Collapsed;
            }
        }

        private void Options_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Profile.Padding = new Thickness(HeaderLeft.ActualWidth, 0, e.NewSize.Width, 0);
        }

        private void EmptyChat_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.ClientService.TryGetUserFull(ViewModel.Chat, out UserFullInfo fullInfo))
            {
                if (fullInfo.BusinessInfo?.StartPage?.Sticker != null)
                {
                    ViewModel.SendSticker(fullInfo.BusinessInfo.StartPage.Sticker, false, false);
                    return;
                }
            }

            if (ViewModel.GreetingSticker != null)
            {
                ViewModel.SendSticker(ViewModel.GreetingSticker, false, false);
            }
        }

        private void EmptyChatHow_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(BusinessPage));
        }
    }

    public enum StickersPanelMode
    {
        Collapsed,
        Overlay
    }

    public partial class ChatHeaderButton : Button
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatHeaderButtonAutomationPeer(this);
        }
    }

    public partial class ChatHeaderButtonAutomationPeer : ButtonAutomationPeer
    {
        private readonly ChatHeaderButton _owner;

        public ChatHeaderButtonAutomationPeer(ChatHeaderButton owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            // GetFullDescriptionCore doesn't seem to work :(
            //    return Strings.AccDescrChatInfo;
            //}

            //protected override string GetFullDescriptionCore()
            //{
            var view = _owner.GetParent<ChatView>();
            if (view != null)
            {
                return view.GetAutomationName();
            }

            return base.GetNameCore();
        }
    }
}
