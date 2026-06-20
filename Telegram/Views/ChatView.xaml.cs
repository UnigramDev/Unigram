//
// Copyright (c) Fela Ameghino 2015-2026
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
using System.Runtime.CompilerServices;
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
using Telegram.Controls.Messages.Content;
using Telegram.Controls.Stories;
using Telegram.Controls.Views;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
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
using Telegram.Views.Stars.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI;
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
using Windows.UI.Xaml.Media.Animation;

namespace Telegram.Views
{
    public interface IChatPage : INavigablePage, ISearchablePage, IActivablePage
    {
        DialogViewModel ViewModel { get; }

        void StartBannerAnimation(ScalarKeyFrameAnimation translate);
        void CompleteBannerAnimation();
    }

    public interface IProfileChatPage : IChatPage
    {
        double HeaderHeight { get; set; }
    }

    public sealed partial class ChatView : UserControlEx, INavigablePage, ISearchablePage, IDialogDelegate, IAutomationNameProvider
    {
        private DialogViewModel _viewModel;
        public DialogViewModel ViewModel => _viewModel ??= DataContext as DialogViewModel;

        private TopicListViewModel _forumViewModel;
        private int _forumTopicId;

        private readonly DispatcherTimer _slowModeTimer;

        private readonly Visual _rootVisual;

        private readonly DispatcherTimer _dateHeaderTimer;
        private readonly Visual _dateHeaderRelative;
        private readonly Visual _dateHeaderPanel;
        private readonly Visual _dateHeader;
        private SelectorItem _dateHeaderTracked;
        private ExpressionAnimation _dateHeaderTranslation;

        private readonly Visual _forumTopicHeaderPanel;
        private readonly Visual _forumTopicHeader;
        private SelectorItem _forumTopicHeaderTracked;
        private ExpressionAnimation _forumTopicHeaderTranslation;
        private ExpressionAnimation _forumTopicHeaderScale;

        // Optimize by holding these in two structs
        private Visual _stickySummaryAboveVisual;
        private MessageViewModel _stickySummaryAboveMessage;
        private SelectorItem _stickySummaryAboveTracked;
        private ExpressionAnimation _stickySummaryExpression;

        private Visual _stickyPhotoAboveVisual;
        private MessageViewModel _stickyPhotoAboveMessage;
        private SelectorItem _stickyPhotoAboveTracked;
        private Visual _stickyPhotoBelowVisual;
        private MessageViewModel _stickyPhotoBelowMessage;
        private SelectorItem _stickyPhotoBelowTracked;
        private ExpressionAnimation _stickyPhotoExpression;

        private readonly ZoomableListHandler _autocompleteZoomer;
        private readonly AnimatedListHandler _autocompleteHandler;

        private TaskCompletionSource<bool> _updateThemeTask;

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
            _autocompleteZoomer.Opening = _autocompleteHandler.Suspend;
            _autocompleteZoomer.Closing = _autocompleteHandler.Resume;

            void AddStrategy(ChatHistoryViewItemType type, DataTemplate template)
            {
                _typeToStrategy.Add(type, new(template));
            }

            AddStrategy(ChatHistoryViewItemType.Outgoing, OutgoingMessageTemplate);
            AddStrategy(ChatHistoryViewItemType.Incoming, IncomingMessageTemplate);
            AddStrategy(ChatHistoryViewItemType.Service, ServiceMessageTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceForumTopic, ServiceMessageForumTopicTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceUnread, ServiceMessageUnreadTemplate);
            AddStrategy(ChatHistoryViewItemType.ServicePhoto, ServiceMessagePhotoTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceBirthdate, ServiceMessageBirthdateTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceBackground, ServiceMessageBackgroundTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceGiftCode, ServiceMessageGiftCodeTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceGift, ServiceMessageGiftTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceUpgradedGift, ServiceMessageUpgradedGiftTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceUpgradedGiftPurchaseOffer, ServiceMessageUpgradedGiftPurchaseOfferTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceChatHasProtectedContentDisableRequested, ServiceMessageChatHasProtectedContentDisableRequestedTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceAccountInfo, ServiceMessageAccountInfoTemplate);
            AddStrategy(ChatHistoryViewItemType.ServiceNewThread, ServiceMessageNewThreadTemplate);

            _focusState = new DebouncedProperty<FocusState>(100, FocusText, CanFocusText);

            _loader = new BidirectionalIncrementalLoader(Messages);

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
            ElementCompositionPreview.SetIsTranslationEnabled(Messages, true);
            ElementCompositionPreview.SetIsTranslationEnabled(MessagesRoot, true);

            _rootVisual = ElementComposition.GetElementVisual(TextArea);

            _dateHeaderTimer = new DispatcherTimer();
            _dateHeaderTimer.Interval = TimeSpan.FromMilliseconds(2000);
            _dateHeaderTimer.Tick += (s, args) =>
            {
                _dateHeaderTimer.Stop();

                //var watch = Stopwatch.StartNew();
                //var point = DateHeaderRelative.TransformToPoint(XamlRoot.Content);
                //var x = point.X + (DateHeaderRelative.ActualWidth / 2) - Math.Max(DateHeader.ActualWidth, ForumTopicHeader.ActualWidth) / 2;
                //var y = point.Y + (DateHeaderRelative.ActualHeight / 2);

                //var rect = new Rect(x, y, Math.Max(DateHeader.ActualWidth, ForumTopicHeader.ActualWidth), DateHeaderRelative.ActualHeight);

                //var children = VisualTreeHelper.FindElementsInHostCoordinates(rect, Messages);
                //watch.Stop();
                //var test = children.ToList();

                //if (children.OfType<ChatHistoryViewItem>().Any(x => x.TypeName == ChatHistoryViewItemType.ServiceForumTopic))
                //{
                //    return;
                //}

                ShowHideDateHeader(false, true);
                ShowHideForumTopicHeader(false, true);
            };

            DateHeaderRelative.CreateInsetClip();

            _dateHeaderRelative = ElementComposition.GetElementVisual(DateHeaderRelative);
            _dateHeaderPanel = ElementComposition.GetElementVisual(DateHeaderPanel);
            _dateHeader = ElementComposition.GetElementVisual(DateHeader);

            _forumTopicHeaderPanel = ElementComposition.GetElementVisual(ForumTopicHeaderPanel);
            _forumTopicHeader = ElementComposition.GetElementVisual(ForumTopicHeader);

            _stickySummaryAboveVisual = ElementComposition.GetElementVisual(StickySummaryAbove);

            _stickyPhotoAboveVisual = ElementComposition.GetElementVisual(StickyPhotoRootAbove);
            _stickyPhotoBelowVisual = ElementComposition.GetElementVisual(StickyPhotoRootBelow);

            _messagesVisual = ElementComposition.GetElementVisual(Messages);
            _messagesPaddingSet = BootStrapper.Current.Compositor.CreatePropertySet();
            _messagesPaddingSet.InsertScalar("Padding", 0);
            _messagesPaddingSet.InsertScalar("TopPadding", 0);

            ElementCompositionPreview.SetIsTranslationEnabled(DateHeader, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ForumTopicHeader, true);
            ElementCompositionPreview.SetIsTranslationEnabled(StickySummaryAbove, true);
            ElementCompositionPreview.SetIsTranslationEnabled(StickyPhotoRootAbove, true);
            ElementCompositionPreview.SetIsTranslationEnabled(StickyPhotoRootBelow, true);

            _debouncer = new DispatcherTimer();
            _debouncer.Interval = TimeSpan.FromMilliseconds(Constants.AnimatedThrottle);
            _debouncer.Tick += (s, args) =>
            {
                _debouncer.Stop();

                _viewMessagesFirstVisibleId = 0;
                _viewMessagesLastVisibleId = 0;

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

            if (ApiInfo.CanCreateThemeShadow && PowerSavingPolicy.AreMaterialsEnabled)
            {
                _shadow = new ThemeShadow();

                ShadowCaster.Shadow = _shadow;
                ShadowCaster.Translation = new Vector3(0, 0, Constants.BubbleElevation);
            }
        }

        private ThemeShadow _shadow;

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
            GroupCall.InitializeParent(this);
            JoinRequests.InitializeParent(this);
            TranslateHeader.InitializeParent(this);
            ActionBar.InitializeParent(this);
            ConnectedBot.InitializeParent(this);
            PinnedMessage.InitializeParent(this);
            AccountInfoHeader.InitializeParent(this);
            Sponsored.InitializeParent(this, Clipper);
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

        public void StartBannerAnimation(ScalarKeyFrameAnimation translate)
        {
            var header = ElementComposition.GetElementVisual(Header);
            var clipper = ElementComposition.GetElementVisual(ClipperOuter);

            ElementCompositionPreview.SetIsTranslationEnabled(Header, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ClipperOuter, true);

            header.StartAnimation("Translation.Y", translate);
            clipper.StartAnimation("Translation.Y", translate);
        }

        public void CompleteBannerAnimation()
        {
            var header = ElementComposition.GetElementVisual(Header);
            var clipper = ElementComposition.GetElementVisual(ClipperOuter);

            header.Properties.InsertVector3("Translation", Vector3.Zero);
            clipper.Properties.InsertVector3("Translation", Vector3.Zero);
        }

        private void InitializeStickers()
        {
            StickersPanel.EmojiClick = Emojis_ItemClick;
            StickersPanel.EmojiContextRequested += Emoji_ContextRequested;

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
            var masterDetailPanel = XamlRoot?.Content?.GetChild<MasterDetailPanel>();
            if (masterDetailPanel != null)
            {
                return masterDetailPanel.GetChild<ChatBackgroundControl>();
            }

            return null;
        }

        private void ClearTrackedContainers()
        {
            _headerUnreadViewport = null;
            _headerUnreadNotReady = false;
            _headerUnreadRetry = false;

            _oldestItem = null;
            _oldestItemAsHeader = null;
            _oldestItemAsHeaderNeeded = null;

            _newestItem = null;
            _newestItemAsFooter = null;
            _newestItemAsFooterNeeded = null;

            _dateHeaderTracked = null;
            _forumTopicHeaderTracked = null;
            _forumTopicHeaderTopic = null;

            _stickySummaryAboveTracked = null;
            _stickySummaryAboveMessage = null;

            _stickyPhotoAboveTracked = null;
            _stickyPhotoAboveMessage = null;
            _stickyPhotoBelowTracked = null;
            _stickyPhotoBelowMessage = null;

            _viewMessagesFirstVisibleId = 0;
            _viewMessagesLastVisibleId = 0;
        }

        public void Deactivate(bool navigation)
        {
            if (ViewModel != null)
            {
                ViewModel.Dispose();

                ViewModel.Initialized -= OnInitialized;
                ViewModel.PropertyChanged -= OnPropertyChanged;
                ViewModel.Items.AttachChanged = null;
                ViewModel.Items.CollectionChanged -= OnCollectionChanged;

                //ViewModel.Items.Dispose();
                //ViewModel.Items.Clear();

                ViewModel.Delegate = null;
                ViewModel.TextField = null;
                ViewModel.HistoryField = null;
                ViewModel.Sticker_Click = null;

                if (navigation is false)
                {
                    _albumIdToSelector.Clear();
                    _messageIdToSelector.Clear();
                    _messageIdToMessageIds.Clear();
                    _messageTopicToSelectors.Clear();
                }
            }

            ClearTrackedContainers();

            ButtonStickers.Collapse();

            Messages.Suspend();

            if (navigation)
            {
                Messages.Disconnect();
            }
        }

        private readonly BidirectionalIncrementalLoader _loader;
        private readonly SynchronizedList<MessageViewModel> _messages = new();
        private readonly IndexShiftTracker _messagesShift = new();

        private void OnInitialized(object sender, EventArgs e)
        {
            _albumIdToSelector.Clear();
            _messageIdToSelector.Clear();
            _messageIdToMessageIds.Clear();
            _messageTopicToSelectors.Clear();

            ClearTrackedContainers();

            if (sender is DialogViewModel viewModel)
            {
                _headerUnreadNotReady = viewModel.HasUnreadMessages;

                _loader.Initialize(viewModel);
                _messages.UpdateSource(viewModel.Items, viewModel.IsSavedMessagesTab);

                viewModel.Initialized -= OnInitialized;

                if (!viewModel.HasProtectedContent)
                {
                    VisualUtilities.QueueCallbackForCompositionRendered(EnableScreenCapture);
                }
            }

            _updateThemeTask?.TrySetResult(true);

            Bindings.Update();
        }

        public void DisableScreenCapture()
        {
            if (ViewModel.HasProtectedContent)
            {
                ViewModel.NavigationService.Window.DisableScreenCapture(GetHashCode());
            }
        }

        public void EnableScreenCapture()
        {
            if (!ViewModel.HasProtectedContent)
            {
                ViewModel.NavigationService.Window.EnableScreenCapture(GetHashCode());
            }
        }

        public void Activate(DialogViewModel viewModel)
        {
            Logger.Info($"ItemsPanelRoot.Children.Count: {Messages.ItemsPanelRoot?.Children.Count}");
            Logger.Info($"Items.Count: {Messages.Items.Count}");

            DataContext = _viewModel = viewModel;
            Messages.ViewModel = viewModel;

            if (_needActivation)
            {
                _needActivation = false;
                OnNavigatedTo();
            }

            ClearTrackedContainers();

            _updateThemeTask = new TaskCompletionSource<bool>();
            ViewModel.Initialized += OnInitialized;
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

            Sponsored.UpdateSponsoredMessage(ViewModel.ClientService, ViewModel.Chat, ViewModel.SponsoredMessage);

            TextField.IsMenuExpanded = false;
            TextField.IsReplaceEmojiEnabled = ViewModel.Settings.IsReplaceEmojiEnabled;

            if (_useSystemSpellChecker != SettingsService.Current.UseSystemSpellChecker)
            {
                _useSystemSpellChecker = SettingsService.Current.UseSystemSpellChecker;
                TextField.IsTextPredictionEnabled = _useSystemSpellChecker;
                TextField.IsSpellCheckEnabled = _useSystemSpellChecker;
            }

            TrySetFocusState(FocusState.Programmatic, false);

            StickersPanel.MaxWidth = SettingsService.Current.IsAdaptiveWideEnabled ? 1024 : double.PositiveInfinity;

            Options.Visibility = ViewModel.Type is DialogType.History or DialogType.Thread
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (TextRoot.Children.Count > 1)
            {
                ShowHideChatThemeDrawer(false, TextRoot.Children[1] as ChatThemeDrawer);
            }

            if (ViewModel.IsInlineBotResultsVisible)
            {
                FindName(nameof(ListInline));
                InlineBotResults_Loaded(null, null);
            }
            else
            {
                UnloadObject(ListInline);
            }

            if (FromPreview)
            {
                BackButton.Visibility = Visibility.Collapsed;
                Options.Visibility = Visibility.Collapsed;

                ClipperOuter.Visibility = Visibility.Collapsed;
                HeaderBackground.Visibility = Visibility.Visible;

                HeaderLeft.SizeChanged += Options_SizeChanged;
                HeaderLeft.Padding = new Thickness(12, 0, 0, 0);

                Messages.Margin = new Thickness(0);

                ShadowCaster.Visibility = Visibility.Collapsed;
                LinearCaster.Visibility = Visibility.Collapsed;

                var background = new Border
                {
                    Style = Resources["HeaderBackgroundStyle"] as Style
                };

                Canvas.SetZIndex(background, 2);
                LayoutRoot.Children.Insert(0, background);
            }
            else if (ViewModel.IsSavedMessagesTab)
            {
                Header.Visibility = Visibility.Collapsed;
                Footer.Visibility = Visibility.Collapsed;
                ClipperOuter.Visibility = Visibility.Collapsed;

                Messages.Template = SavedMessagesTabTemplate;

                ShadowCaster.Visibility = Visibility.Collapsed;
                LinearCaster.Visibility = Visibility.Collapsed;
            }
        }

        public void AnimateEntrance()
        {
            var service = ConnectedAnimationService.GetForCurrentView();

            void Start(string key, UIElement element)
            {
                var animation = service.GetAnimation(key);
                if (animation != null)
                {
                    animation.Configuration = new BasicConnectedAnimationConfiguration();
                    animation.TryStart(element);
                }
            }

            try
            {
                Start("Photo", PhotoRoot);
                Start("Title", TitleRoot);
                Start("Subtitle", Subtitle);
            }
            catch
            {
                //
            }
        }

        public void PrepareExit()
        {
            try
            {
                var service = ConnectedAnimationService.GetForCurrentView();
                service.PrepareToAnimate("Photo", PhotoRoot);
                service.PrepareToAnimate("Title", TitleRoot);
                service.PrepareToAnimate("Subtitle", Subtitle);
            }
            catch
            {
                //
            }
        }

        public double HeaderHeight
        {
            get => SavedMessagesTabHeader.Height;
            set => SavedMessagesTabHeader.Height = value + 4;
        }

        public bool FromPreview { get; set; }

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
                // TODO: this happens in new bot topics
                //Messages.UpdateLayout();

                //panel = Messages.ItemsPanelRoot as ItemsStackPanel;

                //if (panel == null)
                //{
                //    return;
                //}

                return;
            }

            if (args.Action == NotifyCollectionChangedAction.Remove && panel.FirstCacheIndex < args.OldStartingIndex && panel.LastCacheIndex >= args.OldStartingIndex)
            {
                //UpdateDeleteMessages(args.OldItems[0] as MessageViewModel);
                var message = args.OldItems[0] as MessageViewModel;

                var index = _messagesShift.Translate(args.OldStartingIndex);
                if (index >= panel.FirstVisibleIndex && index <= panel.LastVisibleIndex && _messageIdToSelector.TryGetValue(message.Id, out ChatHistoryViewItem selector))
                {
                    var direction = panel.ItemsUpdatingScrollMode == ItemsUpdatingScrollMode.KeepItemsInView ? -1 : 1;
                    var edge = (index == panel.LastVisibleIndex && direction == 1) || (index == panel.FirstVisibleIndex && direction == -1);

                    var first = message.Delegate.IsSavedMessagesTab ? message.IsLast : message.IsFirst;
                    var last = message.Delegate.IsSavedMessagesTab ? message.IsFirst : message.IsLast;

                    var height = first && !last ? selector.ActualSize.Y - 6 : selector.ActualSize.Y;

                    _messagesShift.RegisterRemove(index, args.OldStartingIndex, height, edge && !Messages.ScrollingHost.ViewportContains(selector));
                }
                else
                {
                    _messagesShift.RegisterRemove(args.OldStartingIndex);
                }
            }
            else if (args.Action == NotifyCollectionChangedAction.Add)
            {
                _messagesShift.RegisterInsert(args.NewStartingIndex);

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
                    && message.Content is MessageText or MessageDice or MessageStakeDice or MessageAnimatedEmoji
                    && message.GeneratedContent is MessageBigEmoji or MessageSticker or null;

                await panel.UpdateLayoutAsync();

                if (message.IsOutgoing && message.SendingState is MessageSendingStatePending && !Messages.IsBottomReached)
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
                var diff = owner.ContentTemplateRoot.ActualSize.Y;

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

                        //if (messages.Clip is InsetClip messagesClip)
                        //{
                        //    messagesClip.BottomInset = -8 - SettingsService.Current.Appearance.CornerRadius;
                        //}
                    };

                    _collectionChanging++;
                    Canvas.SetZIndex(TextArea, -1);
                    Canvas.SetZIndex(InlinePanel, -2);
                    Canvas.SetZIndex(Separator, -3);

                    //if (messages.Clip is InsetClip messagesClip)
                    //{
                    //    messagesClip.BottomInset = -96;
                    //}

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
                            MessageSticker or MessageAnimatedEmoji or MessageDice or MessageStakeDice => 48 + more,
                            _ => 48 + more - 12f
                        };

                        var yOffset = content switch
                        {
                            MessageBigEmoji => 66,
                            MessageSticker or MessageAnimatedEmoji or MessageDice or MessageStakeDice => 36,
                            _ => reply ? 29 : 44f
                        };

                        float xScale;
                        float xTranslate;

                        // 432: maxMessageWidth
                        if (TextArea.ActualSize.X - xOffset > 432)
                        {
                            xScale = 432 / bubble.ActualSize.X;
                            xTranslate = (TextArea.ActualSize.X - xOffset) - 432;
                        }
                        else
                        {
                            xScale = (TextArea.ActualSize.X - xOffset) / bubble.ActualSize.X;
                            xTranslate = 0;
                        }

                        var yScale = content switch
                        {
                            MessageText => MathF.Min((float)TextField.MaxHeight, bubble.ActualSize.Y) / bubble.ActualSize.Y,
                            _ => 1
                        };

                        var fontScale = content switch
                        {
                            MessageBigEmoji => 14 / 32f,
                            MessageSticker or MessageAnimatedEmoji => 20 / (180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f)),
                            _ => 1
                        };

                        bubble.AnimateSendout(xTranslate, xScale, yScale, fontScale, outer, inner, delay, reply);

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
            else
            {
                _messagesShift.Invalidate();
            }
        }

        private void OnAttachChanged(IEnumerable<MessageViewModel> items)
        {
            foreach (var message in items)
            {
                if (message == null || !_messageIdToSelector.TryGetValue(message.Id, out ChatHistoryViewItem container))
                {
                    continue;
                }

                var content = container.ContentTemplateRoot as FrameworkElement;
                if (content == null)
                {
                    continue;
                }

                if (content is MessageSelector { Content: MessageBubble bubble })
                {
                    bubble.UpdateAttach(message);
                    bubble.UpdateMessageHeader(message);
                }

                if (_stickySummaryAboveTracked == container)
                {
                    _stickySummaryAboveTracked = null;
                }

                if (_stickyPhotoAboveTracked == container)
                {
                    _stickyPhotoAboveTracked = null;
                }

                if (_stickyPhotoBelowTracked == container)
                {
                    _stickyPhotoBelowTracked = null;
                }

                if (ViewModel.IsSavedMessagesTab)
                {
                    return;
                }

                void UpdateNewestOldest(bool main, bool? needed, bool? loaded, ref ChatHistoryViewItem item, ref ChatHistoryViewItem headerFooter, Index index)
                {
                    if (container == headerFooter && !main)
                    {
                        headerFooter.UpdatePadding(index.IsFromEnd ? -1 : 0, index.IsFromEnd ? 0 : -1);
                        headerFooter = null;

                        item = null;
                    }
                    else if (main && container != headerFooter && needed is true && loaded is true && ViewModel.Items[index] == message)
                    {
                        headerFooter?.UpdatePadding(index.IsFromEnd ? -1 : 0, index.IsFromEnd ? 0 : -1);

                        headerFooter = container;
                        headerFooter.UpdatePadding(index.IsFromEnd ? -1 : _messagesScrollBarPadding, index.IsFromEnd ? _messagesHeaderRootPadding : -1);

                        item = container;
                    }
                }

                UpdateNewestOldest(message.IsFirst, _oldestItemAsHeaderNeeded, ViewModel.IsOldestSliceLoaded, ref _oldestItem, ref _oldestItemAsHeader, 0);
                UpdateNewestOldest(message.IsLast, _newestItemAsFooterNeeded, ViewModel.IsNewestSliceLoaded, ref _newestItem, ref _newestItemAsFooter, ^1);
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("Reply") || e.PropertyName.Equals(nameof(ViewModel.CurrentInlineBot)))
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

                if (_forumCollapsed == ForumViewType.Horizontal && ViewModel.Search != null)
                {
                    ShowHideForumTopics(ForumViewType.List);
                }
                else if (_forumCollapsed == ForumViewType.List && ViewModel.Search == null)
                {
                    UpdateForumTopics(ViewModel.Chat);
                }
            }
            else if (e.PropertyName.Equals(nameof(ViewModel.IsNewestSliceLoaded)))
            {
                UpdateArrowVisibility();
                UpdateMessagesHeaderPadding();
            }
            else if (e.PropertyName.Equals(nameof(ViewModel.IsOldestSliceLoaded)))
            {
                UpdateMessagesHeaderPadding();
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
            else if (e.PropertyName.Equals(nameof(ViewModel.SponsoredMessage)))
            {
                Sponsored.UpdateSponsoredMessage(ViewModel.ClientService, ViewModel.Chat, ViewModel.SponsoredMessage);
            }
            else if (e.PropertyName.Equals(nameof(ViewModel.IsInlineBotResultsVisible)))
            {
                if (ViewModel.IsInlineBotResultsVisible)
                {
                    FindName(nameof(ListInline));
                    InlineBotResults_Loaded(null, null);
                }
                else
                {
                    UnloadObject(ListInline);
                }
            }
        }

        private void Segments_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null || (chat.Id == ViewModel.ClientService.Options.MyId && ViewModel.SavedMessagesTopic == null) || sender is not ActiveStoriesSegments segments || FromPreview)
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
                if (ViewModel.ClientService.TryGetChat(ViewModel.SavedMessagesTopic?.Type, out Chat savedMessagesTopicChat))
                {
                    GalleryWindow.ShowAsync(ViewModel, ViewModel.StorageService, savedMessagesTopicChat, Photo);
                }
                else if (ViewModel.ClientService.TryGetChat(ViewModel.DirectMessagesChatTopic?.ChatId ?? 0, out Chat directMessagesChat))
                {
                    GalleryWindow.ShowAsync(ViewModel, ViewModel.StorageService, directMessagesChat, Photo);
                }
                else
                {
                    GalleryWindow.ShowAsync(ViewModel, ViewModel.StorageService, chat, Photo);
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            //Bindings.StopTracking();
            //Bindings.Update();

            ViewModel.NavigationService.Window.Activated += Window_Activated;
            ViewModel.NavigationService.Window.VisibilityChanged += Window_VisibilityChanged;

            ViewModel.NavigationService.Window.CoreWindow.CharacterReceived += OnCharacterReceived;

            ViewVisibleMessages();

            TrySetFocusState(FocusState.Programmatic, true);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();

            UnloadVisibleMessages();

            ViewModel.NavigationService.Window.EnableScreenCapture(GetHashCode());

            ViewModel.NavigationService.Window.Activated -= Window_Activated;
            ViewModel.NavigationService.Window.VisibilityChanged -= Window_VisibilityChanged;

            ViewModel.NavigationService.Window.CoreWindow.CharacterReceived -= OnCharacterReceived;

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
            JoinRequests = null;
            ActionBar = null;
            TranslateHeader = null;
            Clipper = null;
            ClipperBackground = null;
            PinnedMessage = null;
            DateHeaderRelative = null;
            DateHeaderPanel = null;
            DateHeader = null;
            DateHeaderLabel = null;
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

                var element = FocusManagerEx.TryGetFocusedElement();
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
            var focused = FocusManagerEx.TryGetFocusedElement();
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

            var focused = FocusManagerEx.TryGetFocusedElement();
            if (focused is null or (not TextBox and not RichEditBox))
            {
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
                {
                    if (popup.Child is not ToolTip and not Grid { Name: "TeachingTipRootGrid" or "ReactionAnimation" } and not Grid { Children.Count: 0 })
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

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            var modifiers = WindowContext.KeyModifiers();

            if (args.Key == VirtualKey.Space && modifiers == VirtualKeyModifiers.None /*&& args.RepeatCount == 1*/)
            {
                if (btnVoiceMessage.IsLocked)
                {
                    ChatRecord.Pause();
                    args.Handled = true;
                }
            }
            else if (args.Key == VirtualKey.C && modifiers == VirtualKeyModifiers.Control)
            {
                if (ViewModel.IsSelectionEnabled && ViewModel.SelectedItems.Count > 0 && ViewModel.CanCopySelectedMessage)
                {
                    ViewModel.CopySelectedMessages();
                    args.Handled = true;
                }
                else
                {
                    var focused = FocusManagerEx.TryGetFocusedElement();
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
            else if (args.Key == VirtualKey.Delete && modifiers == VirtualKeyModifiers.None)
            {
                if (ViewModel.IsSelectionEnabled && ViewModel.SelectedItems.Count > 0 && ViewModel.CanDeleteSelectedMessages)
                {
                    ViewModel.DeleteSelectedMessages();
                    args.Handled = true;
                }
                else
                {
                    var focused = FocusManagerEx.TryGetFocusedElement();
                    if (focused is MessageSelector selector)
                    {
                        ViewModel.TryDeleteMessage(selector.Message);
                        args.Handled = true;
                    }
                }
            }
            else if (args.Key == VirtualKey.E && modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift))
            {
                ButtonStickers.Show(Services.Settings.StickersTab.Emoji);
            }
            else if (args.Key == VirtualKey.G && modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift))
            {
                ButtonStickers.Show(Services.Settings.StickersTab.Animations);
            }
            else if (args.Key == VirtualKey.S && modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift))
            {
                ButtonStickers.Show(Services.Settings.StickersTab.Stickers);
            }
            else if (args.Key == VirtualKey.R && /*args.RepeatCount == 1 &&*/ modifiers == VirtualKeyModifiers.Control)
            {
                btnVoiceMessage.ToggleRecording();
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.D && /*args.RepeatCount == 1 &&*/ modifiers == VirtualKeyModifiers.Control)
            {
                btnVoiceMessage.StopRecording(true);
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.P && /*args.RepeatCount == 1 &&*/ modifiers == VirtualKeyModifiers.Control)
            {
                if (btnVoiceMessage.IsLocked)
                {
                    ChatRecord.Pause();
                    args.Handled = true;
                }
            }
            if (args.Key == VirtualKey.O && /*args.RepeatCount == 1 &&*/ modifiers == VirtualKeyModifiers.Control)
            {
                ViewModel.SendDocument();
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.PageUp && modifiers == VirtualKeyModifiers.None && TextField.Document.Selection.StartPosition == 0 && ViewModel.Autocomplete == null)
            {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);
                if (popups.Count > 0)
                {
                    return;
                }

                var focused = FocusManagerEx.TryGetFocusedElement();
                if (focused is Selector or SelectorItem or MessageSelector or MessageService or ItemsRepeater or ChatCell or PlaybackSlider)
                {
                    return;
                }

                if (args.Key == VirtualKey.Up && (focused is TextBox or RichEditBox or ReactionButton))
                {
                    return;
                }

                var panel = Messages.ItemsPanelRoot as ItemsStackPanel;
                if (panel == null)
                {
                    return;
                }

                SelectorItem target;
                if (args.Key == VirtualKey.PageUp)
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
            else if (args.Key is VirtualKey.PageDown or VirtualKey.Down && modifiers == VirtualKeyModifiers.None && TextField.Document.Selection.StartPosition == TextField.Document.GetRange(int.MaxValue, int.MaxValue).EndPosition && ViewModel.Autocomplete == null)
            {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);
                if (popups.Count > 0)
                {
                    return;
                }

                var focused = FocusManagerEx.TryGetFocusedElement();
                if (focused is Selector or SelectorItem or MessageSelector or MessageService or ItemsRepeater or ChatCell or PlaybackSlider)
                {
                    return;
                }

                if (args.Key == VirtualKey.Down && (focused is TextBox or RichEditBox or ReactionButton))
                {
                    return;
                }

                var panel = Messages.ItemsPanelRoot as ItemsStackPanel;
                if (panel == null)
                {
                    return;
                }

                SelectorItem target;
                if (args.Key == VirtualKey.PageUp)
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

                if (ViewModel.TopicId != null && _forumCollapsed != ForumViewType.List)
                {
                    ViewModel.NavigationService.NavigateToChat(ViewModel.Chat, force: false);
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
            var empty = TextField.IsEmpty;
            var editing = ViewModel.ComposerHeader?.Editing != null || ViewModel.CurrentInlineBot != null;

            if (empty != _oldEmpty)
            {
                ButtonStickers.Source = empty
                    ? SettingsService.Current.Stickers.SelectedTab
                    : Services.Settings.StickersTab.Emoji;
            }

            if (editing != _oldEditing)
            {
                btnEdit.Glyph = ViewModel.CurrentInlineBot != null
                    ? Icons.DismissCircleFilled24
                    : Icons.CheckmarkCircleFilled24;
            }

            FrameworkElement elementHide = null;
            FrameworkElement elementShow = null;

            if (empty != _oldEmpty && !editing)
            {
                if (empty)
                {
                    if (_oldEditing)
                    {
                        elementHide = EditMessageButton;
                        SendMessageButton.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        elementHide = SendMessageButton;
                        EditMessageButton.Visibility = Visibility.Collapsed;
                    }

                    elementShow = ButtonRecord;
                }
                else
                {
                    if (_oldEditing)
                    {
                        elementHide = EditMessageButton;
                        ButtonRecord.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        elementHide = ButtonRecord;
                        EditMessageButton.Visibility = Visibility.Collapsed;
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

                    elementShow = EditMessageButton;
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

                    elementHide = EditMessageButton;
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
                if (_oldEditing)
                {
                    EditMessageButton.Visibility = Visibility.Visible;
                    SendMessageButton.Visibility = Visibility.Collapsed;
                    ButtonRecord.Visibility = Visibility.Collapsed;
                }
                else if (_oldEmpty)
                {
                    EditMessageButton.Visibility = Visibility.Collapsed;
                    SendMessageButton.Visibility = Visibility.Collapsed;
                    ButtonRecord.Visibility = Visibility.Visible;
                }
                else
                {
                    EditMessageButton.Visibility = Visibility.Collapsed;
                    SendMessageButton.Visibility = Visibility.Visible;
                    ButtonRecord.Visibility = Visibility.Collapsed;
                }
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
                var suggest = ElementComposition.GetElementVisual(btnSuggest);

                scheduled.CenterPoint = new Vector3(24);
                commands.CenterPoint = new Vector3(24);
                markup.CenterPoint = new Vector3(24);
                suggest.CenterPoint = new Vector3(24);

                var show = empty && !editing;

                btnScheduled.Visibility = Visibility.Visible;
                btnCommands.Visibility = Visibility.Visible;
                btnMarkup.Visibility = Visibility.Visible;
                btnSuggest.Visibility = Visibility.Visible;

                batch = commands.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    var visibility = _oldEmpty && !_oldEditing
                        ? Visibility.Visible
                        : Visibility.Collapsed;

                    btnScheduled.Visibility = visibility;
                    btnCommands.Visibility = visibility;
                    btnMarkup.Visibility = visibility;
                    btnSuggest.Visibility = visibility;

                    scheduled.Scale = commands.Scale = markup.Scale = new Vector3(1);
                    scheduled.Opacity = commands.Opacity = markup.Opacity = 1;
                };

                scheduled.StartAnimation("Scale", show ? show1 : hide1);
                scheduled.StartAnimation("Opacity", show ? show2 : hide2);

                commands.StartAnimation("Scale", show ? show1 : hide1);
                commands.StartAnimation("Opacity", show ? show2 : hide2);

                markup.StartAnimation("Scale", show ? show1 : hide1);
                markup.StartAnimation("Opacity", show ? show2 : hide2);

                suggest.StartAnimation("Scale", show ? show1 : hide1);
                suggest.StartAnimation("Opacity", show ? show2 : hide2);

                batch.End();
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
                            Editing = embedded.Editing,
                            ReplyTo = embedded.ReplyTo,
                            SuggestedPostInfo = embedded.SuggestedPostInfo,
                        };
                    }
                }

                return;
            }

            TryGetWebPagePreview(viewModel.ClientService, viewModel.Chat, text, embedded?.LinkPreviewOptions?.Url, result =>
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
                            Editing = embedded?.Editing,
                            ReplyTo = embedded?.ReplyTo,
                            SuggestedPostInfo = embedded?.SuggestedPostInfo,
                            LinkPreviewOptions = embedded?.LinkPreviewOptions,
                            LinkPreview = linkPreview,
                            LinkPreviewUrl = linkPreview.Url,
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
                                Editing = embedded.Editing,
                                ReplyTo = embedded.ReplyTo,
                                SuggestedPostInfo = embedded.SuggestedPostInfo,
                            };
                        }
                    }
                });
            });
        }

        private void TryGetWebPagePreview(IClientService clientService, Chat chat, string text, string url, Action<Object> result)
        {
            if (chat == null || string.IsNullOrWhiteSpace(text))
            {
                result(null);
                return;
            }

            if (url?.Length > 0)
            {
                clientService.Send(new GetLinkPreview(url.AsFormattedText(false), null), result);
            }
            else if (chat.Type is ChatTypeSecret)
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

                clientService.Send(new GetLinkPreview(urls.AsFormattedText(false), null), result);
            }
            else
            {
                clientService.Send(new GetLinkPreview(text.Format().AsFormattedText(false), null), result);
            }
        }

        private void TextField_TextChanging(RichEditBox sender, RichEditBoxTextChangingEventArgs args)
        {
            if (args.IsContentChanging)
            {
                CheckMessageBoxEmpty();
            }
        }

        private void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            TextField.Send();
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            INavigationService service = null;

            if (FromPreview)
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

            if (header == null || header.Editing == null || (header.IsEmpty && header.LinkPreviewDisabled))
            {
                var audioRights = !ViewModel.VerifyRights(chat, x => x.CanSendAudios);
                var messageRights = !ViewModel.VerifyRights(chat, x => x.CanSendBasicMessages);
                var pollRights = !ViewModel.VerifyRights(chat, x => x.CanSendPolls);

                var pollsAllowed = chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup;
                if (!pollsAllowed && ViewModel.ClientService.TryGetUser(chat, out User user))
                {
                    pollsAllowed = user.Type is UserTypeBot || user.Id == ViewModel.ClientService.Options.MyId;
                }

                var checklistsAllowed = chat.Type is not ChatTypeSecret and not ChatTypeSupergroup { IsChannel: true };

                if (photoRights || videoRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.SendMedia, Strings.PhotoOrVideo, Icons.Image);
                    flyout.CreateFlyoutItem(ViewModel.SendCamera, Strings.ChatCamera, Icons.Camera);
                }

                if (documentRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.SendDocument, Strings.ChatDocument, Icons.Document);
                }

                if (audioRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.SendAudio, Strings.AttachMusic, Icons.MusicNote2);
                }

                if (messageRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.SendLocation, Strings.ChatLocation, Icons.Location);
                }

                if (pollRights && pollsAllowed)
                {
                    flyout.CreateFlyoutItem(ViewModel.SendPoll, Strings.Poll, Icons.Poll);
                }

                if (pollRights && checklistsAllowed && (ViewModel.IsPremium || ViewModel.IsPremiumAvailable))
                {
                    flyout.CreateFlyoutItem(ViewModel.SendChecklist, Strings.Todo, Icons.CheckmarkSquare);
                }

                if (messageRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.SendContact, Strings.AttachContact, Icons.Person);
                }

                if (ViewModel.Type is DialogType.History or DialogType.Thread)
                {
                    if (ViewModel.IsPremium && ViewModel.ClientService.Options.GiftPremiumFromAttachmentMenu)
                    {
                        if (ViewModel.ClientService.TryGetUser(ViewModel.Chat, out User receiver))
                        {
                            flyout.CreateFlyoutItem(ViewModel.GiftPremium, Strings.SendAGift, Icons.GiftPremium);
                        }
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
            else if (header?.Editing != null)
            {
                if (photoRights || videoRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.EditMedia, Strings.PhotoOrVideo, Icons.Image);
                }

                if (documentRights)
                {
                    flyout.CreateFlyoutItem(ViewModel.EditDocument, Strings.ChatDocument, Icons.Document);
                }

                if (header.Editing.Message.Content is MessagePhoto or MessageVideo)
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
                var message = referenceBase.Message;
                if (message != null && message.ChatId == ViewModel.ChatId)
                {
                    if (sender is not ChatPinnedMessage)
                    {
                        ViewModel.PinnedMessages.SetLocked(0);
                    }

                    await ViewModel.LoadMessageSliceAsync(null, message.Id);

                    if (sender is ChatPinnedMessage)
                    {
                        ViewModel.PinnedMessages.SetLocked(message.Id);
                        ViewVisibleMessages();
                    }
                }
                else if (sender is not ChatPinnedMessage && ViewModel.ComposerHeader?.SuggestedPostInfo != null)
                {
                    ViewModel.SuggestPost();
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
                ReplyMarkupPanel.Visibility = Visibility.Visible;

                ButtonMarkup.Glyph = Icons.ChevronDown;
                Automation.SetToolTip(ButtonMarkup, Strings.AccDescrShowKeyboard);

                Focus(FocusState.Programmatic);
                _focusState.Set(FocusState.Programmatic);
            }
            else
            {
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
            UpdateChatTheme(ViewModel.Chat, e.Theme);
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
            rect.CornerRadius = new Vector2(SettingsService.Current.Appearance.CornerRadius);
            rect.Size = TextArea.ActualSize;
            rect.Offset = new Vector2(0, value);

            textArea.Clip = textArea.Compositor.CreateGeometricClip(rect);

            if (messages.Clip is InsetClip messagesClip)
            {
                messagesClip.LeftInset = -72;
                messagesClip.TopInset = -44 + value;
                messagesClip.BottomInset = int.MinValue;
            }
            else
            {
                messages.Clip = textArea.Compositor.CreateInsetClip(-72, -44 + value, 0, int.MinValue);
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
            var supergroupFull = supergroup != null ? ViewModel.ClientService.GetSupergroupFull(supergroup.Id) : null;

            if (user != null && user.Id == ViewModel.ClientService.Options.MyId && ViewModel.SavedMessagesTopic == null)
            {
                flyout.CreateFlyoutItem(ViewModel.ViewAsChats, Strings.SavedViewAsChats, Icons.AppsListDetails);
            }

            flyout.CreateFlyoutItem(Search, Strings.Search, Icons.Search, VirtualKey.F);

            if (supergroup != null && !supergroup.IsBroadcastGroup && !supergroup.IsDirectMessagesGroup && ((ViewModel.IsPremium || (supergroupFull?.MyBoostCount > 0) || supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)))
            {
                flyout.CreateFlyoutItem(ViewModel.Boost, supergroup.IsChannel ? Strings.BoostingBoostChannelMenu : Strings.BoostingBoostGroupMenu, Icons.Boosts);
            }

            if (ViewModel.SavedMessagesTopic != null || ViewModel.DirectMessagesChatTopic != null)
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

            if (user != null && user.Type is not UserTypeDeleted && !secret && ViewModel.SavedMessagesTopic == null)
            {
                flyout.CreateFlyoutItem(ViewModel.ChangeTheme, Strings.SetWallpapers, Icons.PaintBrush);
            }

            if (supergroup != null && supergroup.Status is not ChatMemberStatusCreator && (supergroup.IsChannel || supergroup.HasActiveUsername()))
            {
                flyout.CreateFlyoutItem(ViewModel.Report, Strings.ReportChat, Icons.ErrorCircle);
            }
            if (supergroup != null && supergroup.IsForum && !chat.ViewAsTopics && (ViewModel.Type == DialogType.History && !supergroup.HasForumTabs))
            {
                flyout.CreateFlyoutItem(ViewModel.ViewAsTopics, Strings.TopicViewAsTopics, Icons.ChatEmpty);

                if (ViewModel.Chat.CanCreateTopics(ViewModel.ClientService))
                {
                    flyout.CreateFlyoutItem(ViewModel.CreateTopic, Strings.CreateTopic, Icons.Compose);
                }
            }
            if (user != null && user.Type is not UserTypeDeleted and not UserTypeBot && user.Id != ViewModel.ClientService.Options.MyId)
            {
                if (!user.IsContact && !user.IsSupport)
                {
                    flyout.CreateFlyoutItem(ViewModel.AddToContacts, Strings.AddToContacts, Icons.PersonAdd);
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
                var silent = ViewModel.ClientService.Notifications.IsSilent(chat);

                var mute = new MenuFlyoutSubItem();
                mute.Text = Strings.Mute;
                mute.Icon = MenuFlyoutHelper.CreateIcon(muted ? Icons.Alert : Icons.AlertOff);

                if (muted is false)
                {
                    mute.CreateFlyoutItem(ViewModel.SetSound, !silent,
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

            var hidden = ViewModel.Settings.GetChatPinnedMessage(chat.Id);
            if (hidden != 0)
            {
                flyout.CreateFlyoutItem(ViewModel.ShowPinnedMessage, Strings.PinnedMessages, Icons.Pin);
            }

            if (ViewModel.ChatId == _forumViewModel?.ChatId && ViewModel.Chat.CanCreateTopics(ViewModel.ClientService))
            {
                flyout.CreateFlyoutItem(_forumViewModel.CreateTopic, Strings.CreateTopic, Icons.Compose);
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

            flyout.CreateFlyoutItem(() => TextField.Send(true), Strings.SendWithoutSound, Icons.AlertOff);

            if (!ViewModel.ClientService.IsPaid(chat) && !ViewModel.ClientService.IsDirectMessagesGroup(chat))
            {
                if (ViewModel.ClientService.TryGetUser(chat, out Td.Api.User user) && user.Type is UserTypeRegular && user.Status is not UserStatusRecently && !self)
                {
                    flyout.CreateFlyoutItem(() => TextField.Schedule(true), Strings.SendWhenOnline, Icons.PersonCircleOnline);
                }

                flyout.CreateFlyoutItem(() => TextField.Schedule(false), self ? Strings.SetReminder : Strings.ScheduleMessage, Icons.CalendarClock);
            }

            if (chat.Type is ChatTypePrivate)
            {
                flyout.Opened += (s, args) =>
                {
                    var picker = ReactionsMenuFlyout.ShowAt(ViewModel.ClientService, ViewModel.ClientService.AvailableMessageEffects?.ReactionEffectIds, null, flyout);
                    picker.Selected += MessageEffectFlyout_Selected;
                };
            }

            flyout.ShowAt(sender, FlyoutPlacementMode.TopEdgeAlignedRight);
        }

        private void Send_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
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
            if (SendEffectInteractions.Children.Count < 4)
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
                player.Source = new DelayedFileSource(ViewModel.ClientService, interaction);
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
        }

        private void Reply_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var header = ViewModel.ComposerHeader;
            if (header?.LinkPreview != null)
            {
                static void ChangeShowAbove(MessageComposerHeader header)
                {
                    header.LinkPreviewOptions.ShowAboveText = !header.LinkPreviewOptions.ShowAboveText;
                }

                static void ChangeForceMedia(MessageComposerHeader header)
                {
                    header.LinkPreviewOptions.ForceSmallMedia = !header.LinkPreviewOptions.ForceSmallMedia;
                    header.LinkPreviewOptions.ForceLargeMedia = !header.LinkPreviewOptions.ForceSmallMedia;
                    header.LinkPreviewOptions.Url = header.LinkPreviewUrl;
                }

                flyout.CreateFlyoutItem(ChangeShowAbove, header, header.LinkPreviewOptions.ShowAboveText ? Strings.LinkBelow : Strings.LinkAbove, header.LinkPreviewOptions.ShowAboveText ? Icons.MoveDown : Icons.MoveUp);

                if (header.LinkPreview.HasLargeMedia)
                {
                    flyout.CreateFlyoutItem(ChangeForceMedia, header, header.LinkPreviewOptions.ForceSmallMedia ? Strings.LinkMediaLarger : Strings.LinkMediaSmaller, header.LinkPreviewOptions.ForceSmallMedia ? Icons.Enlarge : Icons.Shrink);
                }

                var text = TextField.GetFormattedText();
                var entities = ClientEx.GetTextEntities(text.Text);

                var links = new List<string>();

                foreach (var entity in text.Entities.Union(entities).OrderBy(x => x.Offset))
                {
                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                    {
                        if (!links.Contains(textUrl.Url))
                        {
                            links.Add(textUrl.Url);
                        }
                    }
                    else if (entity.Type is TextEntityTypeUrl)
                    {
                        var url = text.Text.Substring(entity.Offset, entity.Length);
                        if (!links.Contains(url))
                        {
                            links.Add(url);
                        }
                    }
                }

                if (links.Count > 0)
                {
                    var item = new MenuFlyoutSubItem
                    {
                        Text = Strings.MessageOptionsLinkTitle,
                        Icon = MenuFlyoutHelper.CreateIcon(Icons.LinkDiagonal)
                    };

                    var target = header.LinkPreviewOptions.Url;
                    if (target.Length == 0)
                    {
                        target = header.LinkPreviewUrl;
                    }

                    void handler(object sender, RoutedEventArgs e)
                    {
                        if (sender is ToggleMenuFlyoutItem item && item.CommandParameter is string link)
                        {
                            item.Click -= handler;

                            var header = ViewModel.ComposerHeader;
                            if (header?.LinkPreviewOptions != null)
                            {
                                header.LinkPreviewOptions.Url = link;
                                CheckMessageBoxEmpty();
                            }
                        }
                    }

                    foreach (var link in links)
                    {
                        var toggle = new ToggleMenuFlyoutItem
                        {
                            Text = link,
                            CommandParameter = link,
                            IsChecked = target == link
                        };

                        toggle.Click += handler;
                        item.Items.Add(toggle);
                    }

                    flyout.Items.Add(item);
                }

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.ClearReply, Strings.DoNotLinkPreview, Icons.DismissCircle, destructive: true);
            }
            else if (header?.ReplyTo != null && header.ReplyTo.CanBeRepliedInAnotherChat && !ViewModel.IsDirectMessagesGroup)
            {
                if (header.ReplyTo.Quote != null)
                {
                    var quote = new MessageQuote(header.ReplyTo);

                    flyout.CreateFlyoutItem(ViewModel.QuoteToMessageInAnotherChat, quote, Strings.ReplyToAnotherChat, Icons.Replace);
                }
                else if (header.ReplyTo.ChecklistTaskId != 0)
                {
                    var checklist = new MessageChecklistTask(header.ReplyTo);

                    flyout.CreateFlyoutItem(ViewModel.ReplyToChecklistTaskInAnotherChat, checklist, Strings.ReplyToAnotherChat, Icons.Replace);
                }
                else if (!string.IsNullOrEmpty(header.ReplyTo.PollOptionId))
                {
                    var checklist = new MessagePollOption(header.ReplyTo);

                    flyout.CreateFlyoutItem(ViewModel.ReplyToPollOptionInAnotherChat, checklist, Strings.ReplyToAnotherChat, Icons.Replace);
                }
                else
                {
                    flyout.CreateFlyoutItem(ViewModel.ReplyToMessageInAnotherChat, header.ReplyTo.Message, Strings.ReplyToAnotherChat, Icons.Replace);
                }

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.ClearReply, Strings.DoNotReply, Icons.DismissCircle, destructive: true);
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

            ChecklistTask checklistTask = null;
            PollOption pollOption = null;
            PollOptionProperties pollOptionProperties = null;

            if (args.TryGetPosition(XamlRoot.Content, out Point point))
            {
                var children = VisualTreeHelper.FindElementsInHostCoordinates(point, element);

#if NET9_0_OR_GREATER
                children = children.ToList();
#endif

                var textBlock = children.FirstOrDefault() as RichTextBlock;
                if (textBlock?.SelectionStart != null && textBlock?.SelectionEnd != null)
                {
                    selectionStart = textBlock.SelectionStart.OffsetToIndex(message.Text);
                    selectionEnd = textBlock.SelectionEnd.OffsetToIndex(message.Text);

                    if (selectionEnd - selectionStart <= 0)
                    {
                        MessageHelper.Hyperlink_ContextRequested(ViewModel.TranslateService, textBlock, args, message);

                        if (args.Handled)
                        {
                            return;
                        }
                    }
                }
                else if (textBlock != null)
                {
                    MessageHelper.Hyperlink_ContextRequested(ViewModel.TranslateService, textBlock, args, message);

                    if (args.Handled)
                    {
                        return;
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

                var reaction = children.FirstOrDefault(x => x is ReactionButton) as ReactionButton;
                if (reaction != null)
                {
                    reaction.OnContextRequested(args);
                    return;
                }

                var profilePicture = children.FirstOrDefault(x => x is ProfilePicture) as ProfilePicture;
                if (profilePicture != null && profilePicture.Parent is HyperlinkButton)
                {
                    ProfilePhoto_ContextRequested(message, profilePicture, args);
                    return;
                }

                var checklistTaskControl = children.FirstOrDefault(x => x is ChecklistTaskContent) as ChecklistTaskContent;
                if (checklistTaskControl != null)
                {
                    checklistTask = checklistTaskControl.Task;
                }

                var pollOptionControl = children.FirstOrDefault(x => x is PollOptionContent) as PollOptionContent;
                if (pollOptionControl != null)
                {
                    pollOption = pollOptionControl.Option;
                    pollOptionProperties = await message.ClientService.SendAsync(new GetPollOptionProperties(message.ChatId, message.Id, pollOption.Id)) as PollOptionProperties;
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
                    MessageHelper.Hyperlink_ContextRequested(ViewModel.TranslateService, originalBlock, args, message);

                    if (args.Handled)
                    {
                        return;
                    }
                }
            }
            else if (args.OriginalSource is Hyperlink originalHyperlink)
            {
                MessageHelper.Hyperlink_ContextRequested(ViewModel.TranslateService, originalHyperlink, args, message);

                if (args.Handled)
                {
                    return;
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
                    flyout.CreateFlyoutItem(ViewModel.CopySelectedMessages, Strings.CopySelectedMessages, Icons.Copy);
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

                flyout.CreateFlyoutItem(MessageCopy_Loaded, ViewModel.CopyMessage, message, Strings.Copy, Icons.Copy);

                if (properties.CanBeDeletedOnlyForSelf || properties.CanBeDeletedForAllUsers)
                {
                    flyout.CreateFlyoutItem(ViewModel.DeleteMessage, message, message.SendingState is MessageSendingStatePending ? Strings.CancelSending : Strings.Delete, Icons.Delete, destructive: true);
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
            else if (message.Content is MessageSponsored sponsored)
            {
                if (sponsored.CanBeReported)
                {
                    // TODO: about
                    flyout.CreateFlyoutItem(() => { }, Strings.AboutRevenueSharingAds, Icons.Info);
                    flyout.CreateFlyoutItem(ViewModel.ReportMessage, message, Strings.ReportAd, Icons.HandRight);
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(ViewModel.HideSponsoredMessage, message, Strings.RemoveAds, Icons.DismissCircle);
                }
                else
                {
                    // TODO: about
                    flyout.CreateFlyoutItem(() => { }, Strings.SponsoredMessageInfo, Icons.Info);
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

                if (message.EditDate != 0 && message.ViaBotUserId == 0 && !bot && message.ReplyMarkup is not ReplyMarkupInlineKeyboard)
                {
                    var placeholder = new MenuFlyoutItem();
                    placeholder.Text = Formatter.EditDate(message.EditDate);
                    placeholder.FontSize = 12;
                    placeholder.Icon = MenuFlyoutHelper.CreateIcon(Icons.ClockEdit);

                    flyout.Items.Add(placeholder);
                    flyout.CreateFlyoutSeparator();
                }
                else if (message.ForwardInfo != null && !message.IsSaved && !message.IsVerificationCode)
                {
                    var placeholder = new MenuFlyoutItem();
                    placeholder.Text = Formatter.ForwardDate(message.ForwardInfo.Date);
                    placeholder.FontSize = 12;
                    placeholder.Icon = MenuFlyoutHelper.CreateIcon(Icons.ClockArrowForward);

                    flyout.Items.Add(placeholder);
                    flyout.CreateFlyoutSeparator();
                }

                if (CanGetMessageAuthor(message, properties))
                {
                    LoadMessageAuthor(message, properties, flyout);
                }
                else if (CanGetMessageReadDate(message, properties))
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

                if (message.Content is MessageGift gift && ViewModel.ClientService.TryGetUser(chat, out User user))
                {
                    flyout.CreateFlyoutItem(ViewModel.GiftPremium, message.IsOutgoing ? Strings.SendAnotherGift : string.Format(Strings.SendGiftTo, user.FirstName), Icons.GiftPremium);
                }

                // Generic
                if (quote != null && MessageQuote_Loaded(quote, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.QuoteToMessage, quote, Strings.QuoteSelectedPart, Icons.ArrowReply);
                }
                else if (MessageReply_Loaded(message, properties))
                {
                    if (properties.CanBeReplied)
                    {
                        flyout.CreateFlyoutItem(ViewModel.ReplyToMessage, message, Strings.Reply, Icons.ArrowReply);
                    }
                    else
                    {
                        flyout.CreateFlyoutItem(ViewModel.ReplyToMessageInAnotherChat, message, Strings.ReplyToAnotherChat, Icons.ArrowReply);
                    }
                }

                if (MessageEdit_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.EditMessage, message, message.Content is MessageChecklist ? Strings.EditToDo : Strings.Edit, Icons.Edit);
                }

                if (ViewModel.IsForum)
                {
                    flyout.CreateFlyoutItem(NavigateToMessageTopic, message, Strings.ViewTopic, Icons.ChatMultiple);
                }
                else if (MessageThread_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.OpenMessageThread, message, message.InteractionInfo?.ReplyInfo?.ReplyCount > 0 ? Locale.Declension(Strings.R.ViewReplies, message.InteractionInfo.ReplyInfo.ReplyCount) : Strings.ViewThread, Icons.ChatMultiple);
                }

                flyout.CreateFlyoutSeparator();

                // Manage
                if (MessagePin_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.PinMessage, message, message.IsPinned ? Strings.UnpinMessage : Strings.PinMessage, message.IsPinned ? Icons.PinOff : Icons.Pin);
                }

                if (ViewModel.Type == DialogType.Pinned || ViewModel.IsSavedPollsTab)
                {
                    flyout.CreateFlyoutItem(ViewModel.ViewMessageInChat, message, Strings.ShowInChat2, Icons.ChatEmpty);
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


                // Polls
                flyout.CreateFlyoutItem(MessageUnvotePoll_Loaded, ViewModel.UnvotePoll, message, Strings.Unvote, Icons.PollUndo);

                if (MessageStopPoll_Loaded(message, properties))
                {
                    flyout.CreateFlyoutItem(ViewModel.StopPoll, message, Strings.StopPoll, Icons.LockClosed);
                }

                // Checklists
                if (properties.CanAddTasks)
                {
                    flyout.CreateFlyoutItem(ViewModel.AddChecklistTask, message, Strings.AddTasks, Icons.AddCircle);
                }

                if (SettingsService.Current.Diagnostics.RichMessagesDebug && message.Content is MessageRichMessage richMessage)
                {
                    flyout.CreateFlyoutItem(() => {
                        ViewModel.ShowPopup(new TextEditorRichPopup(ViewModel.ClientService, ViewModel.NavigationService, richMessage.Message));
                        }, "Test");
                }

                if (checklistTask != null)
                {
                    var checklistTaskItem = new MenuFlyoutSubItem();
                    checklistTaskItem.Text = Strings.TodoMenuTabTask;
                    checklistTaskItem.Icon = MenuFlyoutHelper.CreateIcon(Icons.CheckmarkSquare);

                    if (checklistTask.CompletionDate != 0)
                    {
                        var textBlock = new TextBlock();
                        textBlock.Text = Formatter.CompletedDate(checklistTask.CompletionDate);
                        textBlock.FontSize = 12;

                        var placeholder = new MenuFlyoutContent();
                        placeholder.Content = textBlock;
                        placeholder.FontSize = 12;
                        placeholder.Padding = new Thickness(12, 4, 12, 4);
                        placeholder.HorizontalAlignment = HorizontalAlignment.Left;

                        checklistTaskItem.Items.Add(placeholder);
                        checklistTaskItem.CreateFlyoutSeparator();
                    }

                    var messageTask = new MessageChecklistTask(message, checklistTask);

                    if (properties.CanMarkTasksAsDone)
                    {
                        // TODO:
                        checklistTaskItem.CreateFlyoutItem(ViewModel.MarkChecklistTask, messageTask, checklistTask.CompletionDate != 0 ? Strings.TodoUncheck : Strings.TodoCheck, checklistTask.CompletionDate != 0 ? Icons.DismissCircle : Icons.CheckmarkCircle);
                    }

                    checklistTaskItem.CreateFlyoutItem(ViewModel.ReplyToChecklistTask, messageTask, Strings.TodoItemQuote, Icons.ArrowReply);

                    if (properties.CanGetLink)
                    {
                        checklistTaskItem.CreateFlyoutItem(ViewModel.CopyChecklistTask, messageTask, Strings.CopyLink, Icons.Link);
                    }

                    checklistTaskItem.CreateFlyoutItem(ViewModel.CopyText, checklistTask.Text, Strings.Copy, Icons.Copy);

                    if (properties.CanBeEdited)
                    {
                        checklistTaskItem.CreateFlyoutItem(ViewModel.EditChecklistTask, messageTask, Strings.TodoEditItem, Icons.Edit);
                        checklistTaskItem.CreateFlyoutItem(ViewModel.DeleteChecklistTask, messageTask, Strings.TodoDeleteItem, Icons.Delete, destructive: true);
                    }

                    flyout.Items.Add(checklistTaskItem);
                }
                else if (pollOption != null && pollOptionProperties != null && message.Content is MessagePoll poll)
                {
                    var pollOptionItem = new MenuFlyoutSubItem();
                    pollOptionItem.Text = Strings.PollMenuTabOption;
                    pollOptionItem.Icon = MenuFlyoutHelper.CreateIcon(Icons.CheckmarkSquare);

                    //if (pollOption.CompletionDate != 0)
                    //{
                    //    var textBlock = new TextBlock();
                    //    textBlock.Text = Formatter.CompletedDate(pollOption.CompletionDate);
                    //    textBlock.FontSize = 12;

                    //    var placeholder = new MenuFlyoutContent();
                    //    placeholder.Content = textBlock;
                    //    placeholder.FontSize = 12;
                    //    placeholder.Padding = new Thickness(12, 4, 12, 4);
                    //    placeholder.HorizontalAlignment = HorizontalAlignment.Left;

                    //    pollOptionItem.Items.Add(placeholder);
                    //    pollOptionItem.CreateFlyoutSeparator();
                    //}

                    var messageTask = new MessagePollOption(message, pollOption);

                    if (!poll.Poll.IsClosed)
                    {
                        // TODO:
                        pollOptionItem.CreateFlyoutItem(ViewModel.MarkPollOption, messageTask, pollOption.IsChosen ? Strings.Unvote : Strings.PollSubmitVotesNoCaps, pollOption.IsChosen ? Icons.PollUndo : Icons.CheckmarkCircle);
                    }

                    if (pollOptionProperties.CanBeReplied || pollOptionProperties.CanBeRepliedInAnotherChat)
                    {
                        pollOptionItem.CreateFlyoutItem(ViewModel.ReplyToPollOption, messageTask, Strings.PollItemQuote, Icons.ArrowReply);
                    }

                    if (pollOptionProperties.CanGetLink)
                    {
                        pollOptionItem.CreateFlyoutItem(ViewModel.CopyPollOption, messageTask, Strings.CopyLink, Icons.Link);
                    }

                    pollOptionItem.CreateFlyoutItem(ViewModel.CopyText, pollOption.Text, Strings.Copy, Icons.Copy);

                    //if (properties.CanBeEdited)
                    //{
                    //    pollOptionItem.CreateFlyoutItem(ViewModel.EditChecklistTask, messageTask, Strings.TodoEditItem, Icons.Edit);
                    //    pollOptionItem.CreateFlyoutItem(ViewModel.DeleteChecklistTask, messageTask, Strings.TodoDeleteItem, Icons.Delete, destructive: true);
                    //}

                    if (pollOptionProperties.CanBeDeleted)
                    {
                        pollOptionItem.CreateFlyoutItem(ViewModel.DeletePollOption, messageTask, Strings.TodoDeleteItem, Icons.Delete, destructive: true);
                    }

                    flyout.Items.Add(pollOptionItem);
                }

                if (properties.CanBeDeletedOnlyForSelf || properties.CanBeDeletedForAllUsers)
                {
                    if (message.IsPaidStarSuggestedPost || message.IsPaidTonSuggestedPost && DateTime.Now.ToTimestamp() < (int)message.ClientService.Options.SuggestedPostLifetimeMin + message.GetDate())
                    {
                        var delete = new MenuFlyoutInfoItem
                        {
                            Description = string.Format(Strings.SuggestedOfferPaidUntil, Formatter.DateAt((int)message.ClientService.Options.SuggestedPostLifetimeMin + message.GetDate())),
                            Text = Strings.Delete,
                            Icon = MenuFlyoutHelper.CreateIcon(Icons.Delete),
                            Foreground = BootStrapper.Current.Resources["DangerButtonBackground"] as Brush,
                            CommandParameter = message,
                            Command = new RelayCommand<MessageViewModel>(ViewModel.DeleteMessage)
                        };

                        flyout.Items.Add(delete);
                    }
                    else
                    {
                        flyout.CreateFlyoutItem(ViewModel.DeleteMessage, message, message.SendingState is MessageSendingStatePending ? Strings.CancelSending : Strings.Delete, Icons.Delete, destructive: true);
                    }
                }

                flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.SelectMessage, message, Strings.Select, Icons.CheckmarkCircle);

                flyout.CreateFlyoutSeparator();

                // Copy
                if (quote != null)
                {
                    // TODO: copy selection
                    flyout.CreateFlyoutItem(MessageCopy_Loaded, ViewModel.CopyMessage, quote, Strings.Copy, Icons.Copy);
                }
                else
                {
                    flyout.CreateFlyoutItem(MessageCopy_Loaded, ViewModel.CopyMessage, message, Strings.Copy, Icons.Copy);
                }

                if (properties.CanGetLink)
                {
                    flyout.CreateFlyoutItem(ViewModel.CopyMessageLink, message, Strings.CopyLink, Icons.Link);
                }

                flyout.CreateFlyoutItem(MessageCopyMedia_Loaded, ViewModel.CopyMessageMedia, message, Strings.CopyImage, Icons.Image);

                if (message.Content is not MessageAlbum)
                {
                    flyout.CreateFlyoutItem(MessageSaveMedia_Loaded, ViewModel.CopyMessagePath, message, Strings.CopyAsPath, Icons.CopyAsPath);
                }

                if (quote != null)
                {
                    flyout.CreateFlyoutItem(MessageTranslate_Loaded, ViewModel.TranslateMessage, quote, Strings.TranslateSelectedText, Icons.Translate);
                }
                else
                {
                    flyout.CreateFlyoutItem(MessageTranslate_Loaded, ViewModel.TranslateMessage, message, Strings.TranslateMessage, Icons.Translate);
                }

                flyout.CreateFlyoutSeparator();

                // Stickers
                flyout.CreateFlyoutItem(MessageAddSticker_Loaded, ViewModel.AddStickerFromMessage, message, Strings.AddToStickers, Icons.Sticker);
                flyout.CreateFlyoutItem(MessageFaveSticker_Loaded, ViewModel.AddFavoriteSticker, message, Strings.AddToFavorites, Icons.Star);
                flyout.CreateFlyoutItem(MessageUnfaveSticker_Loaded, ViewModel.RemoveFavoriteSticker, message, Strings.DeleteFromFavorites, Icons.StarOff);

                flyout.CreateFlyoutSeparator();

                // Files
                flyout.CreateFlyoutItem(MessageSaveAnimation_Loaded, ViewModel.SaveMessageAnimation, message, Strings.SaveToGIFs, Icons.Gif);
                flyout.CreateFlyoutItem(MessageSaveSound_Loaded, ViewModel.SaveMessageNotificationSound, message, Strings.SaveForNotifications, Icons.MusicNote2);
                flyout.CreateFlyoutItem(MessageSaveMedia_Loaded, ViewModel.SaveMessageMedia, message, Strings.SaveAs, Icons.SaveAs);
                flyout.CreateFlyoutItem(MessageOpenMedia_Loaded, ViewModel.OpenMessageWith, message, Strings.OpenWith, Icons.OpenWith);
                flyout.CreateFlyoutItem(MessageOpenFolder_Loaded, ViewModel.OpenMessageFolder, message, Strings.ShowInFolder, Icons.FolderOpen);

                // Contacts
                flyout.CreateFlyoutItem(MessageAddContact_Loaded, ViewModel.AddToContacts, message, Strings.AddContactTitle, Icons.Person);
                //CreateFlyoutItem(ref flyout, MessageSaveDownload_Loaded, ViewModel.MessageSaveDownloadCommand, messageCommon, Strings.SaveToDownloads);

                if (CanGetMessageEmojis(message, out var customEmojiIds))
                {
                    LoadMessageEmojis(message, flyout, customEmojiIds);
                }

                if (SettingsService.Current.Diagnostics.DeleteFilesDebug)
                {
                    var file = message.GetFile();
                    if (file != null && (file.Local.DownloadedSize > 0 || (message.Content is MessageVideo video && video.AlternativeVideos.Any(x => x.HlsFile.Local.DownloadedSize > 0 || x.Video.Local.DownloadedSize > 0))))
                    {
                        flyout.CreateFlyoutItem(x =>
                        {
                            var file = x.GetFile();
                            if (file == null)
                            {
                                return;
                            }

                            ViewModel.Settings.Video.RemovePosition(file);
                            ViewModel.Aggregator.Publish(new UpdateMessageContentOpened(message.ChatId, message.Id));

                            ViewModel.ClientService.CancelDownloadFile(file);
                            ViewModel.ClientService.Send(new DeleteFile(file.Id));

                            if (x.Content is MessageVideo video)
                            {
                                foreach (var vid in video.AlternativeVideos)
                                {
                                    ViewModel.ClientService.CancelDownloadFile(vid.HlsFile);
                                    ViewModel.ClientService.Send(new DeleteFile(vid.HlsFile.Id));

                                    ViewModel.ClientService.CancelDownloadFile(vid.Video);
                                    ViewModel.ClientService.Send(new DeleteFile(vid.Video.Id));
                                }
                            }

                        }, message, "Delete from disk", Icons.Delete);
                    }
                }

                if (message.CanBeSaved is false && message.Chat.HasProtectedContent && flyout.Items.Count > 0)
                {
                    string hasProtectedContent;
                    if (message.IsChannelPost)
                    {
                        hasProtectedContent = Strings.ForwardsRestrictedInfoChannel;
                    }
                    else if (properties.HasProtectedContentByCurrentUser)
                    {
                        hasProtectedContent = Strings.ForwardsRestrictedInfoUserBecauseYou;
                    }
                    else if (properties.HasProtectedContentByOtherUser)
                    {
                        hasProtectedContent = string.Format(Strings.ForwardsRestrictedInfoUserBecauseUser, message.Chat.Title);
                    }
                    else if (message.Chat.Type is ChatTypePrivate)
                    {
                        hasProtectedContent = Strings.ForwardsRestrictedInfoBot;
                    }
                    else
                    {
                        hasProtectedContent = Strings.ForwardsRestrictedInfoGroup;
                    }

                    flyout.CreateFlyoutSeparator();
                    flyout.Items.Add(new MenuFlyoutLabel
                    {
                        Padding = new Thickness(12, 4, 12, 4),
                        MaxWidth = 178,
                        Text = hasProtectedContent
                    });
                }
                else if (message.SchedulingState is MessageSchedulingStateSendWhenVideoProcessed && flyout.Items.Count > 0)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.Items.Add(new MenuFlyoutLabel
                    {
                        Padding = new Thickness(12, 4, 12, 4),
                        MaxWidth = 178,
                        Text = Strings.VideoConversionInfo
                    });
                }
            }

            //sender.ContextFlyout = menu;

            if (flyout.Items.Count > 0 && flyout.Items[flyout.Items.Count - 1] is MenuFlyoutSeparator and not MenuFlyoutLabel)
            {
                flyout.Items.RemoveAt(flyout.Items.Count - 1);
            }

            if (element is IReactionsDelegate bubble && selected.Count == 0)
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

        private void ProfilePhoto_ContextRequested(MessageViewModel message, ProfilePicture profilePicture, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            string mention = null;
            string mentionName = null;

            if (_viewModel.ClientService.TryGetUser(message.SenderId, out User user))
            {
                void OpenProfile()
                {
                    _viewModel.NavigationService.NavigateToUser(user.Id, false);
                }

                void SendMessage()
                {
                    _viewModel.NavigationService.NavigateToUser(user.Id, true);
                }

                flyout.CreateFlyoutItem(OpenProfile, Strings.OpenProfile, Icons.PersonCircle);
                flyout.CreateFlyoutItem(SendMessage, Strings.SendMessage, Icons.ChatEmpty);

                if (user.HasActiveUsername(out string username))
                {
                    mention = $"@{username}";
                    mentionName = null;
                }
                else
                {
                    mention = $"\"tg-user://{user.Id}\"";

                    if (FormattedTextBox.IsSafe(user.FirstName))
                    {
                        mentionName = user.FirstName;
                    }
                    else if (FormattedTextBox.IsSafe(user.LastName))
                    {
                        mentionName = user.LastName;
                    }
                    else
                    {
                        mentionName = Strings.Username;
                    }
                }
            }
            else if (_viewModel.ClientService.TryGetSupergroup(message.SenderId, out Supergroup supergroup))
            {
                var senderChat = message.SenderId as MessageSenderChat;

                void OpenProfile()
                {
                    _viewModel.NavigationService.Navigate(typeof(ProfilePage), senderChat.ChatId);
                }

                flyout.CreateFlyoutItem(OpenProfile, Strings.OpenProfile, Icons.PersonCircle);

                if (supergroup.IsChannel)
                {
                    void SendMessage()
                    {
                        _viewModel.NavigationService.NavigateToChat(senderChat.ChatId);
                    }

                    flyout.CreateFlyoutItem(SendMessage, Strings.OpenChannel2, Icons.Megaphone);
                }

                if (supergroup.HasActiveUsername(out string username))
                {
                    mention = $"@{username}";
                    mentionName = null;
                }
                else
                {
                    mention = null;
                    mentionName = null;
                }
            }

            void Mention()
            {
                var range = TextField.Document.Selection.GetClone();
                range.SetText(TextSetOptions.None, mentionName ?? mention);

                if (mentionName != null)
                {
                    range.Link = mention;
                }

                range = TextField.Document.GetRange(range.EndPosition, range.EndPosition);
                range.SetText(TextSetOptions.None, " ");

                TextField.Document.Selection.StartPosition = range.EndPosition;
                TextField.Focus(FocusState.Keyboard);
            }

            void SearchMessages(MessageSender sender)
            {
                _viewModel.SearchExecute(string.Empty, sender);
            }

            if (message.Chat.Type is not ChatTypeSupergroup { IsChannel: true })
            {
                if (mention != null)
                {
                    flyout.CreateFlyoutItem(Mention, Strings.Mention, Icons.Mention);
                }

                flyout.CreateFlyoutItem(SearchMessages, message.SenderId, Strings.AvatarPreviewSearchMessages, Icons.Search);
            }

            flyout.ShowAt(profilePicture, args);
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

            if (message.LastReadOutboxMessageId < message.Id || !properties.CanGetViewers)
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
            static async Task<IList<MessageViewer>> GetMessageViewersAsync(MessageViewModel message, MessageProperties properties)
            {
                if (CanGetMessageViewers(message, properties, false))
                {
                    var response = await message.ClientService.SendAsync(new GetMessageViewers(message.ChatId, message.Id));
                    if (response is MessageViewers viewers && viewers.Viewers.Count > 0)
                    {
                        return viewers.Viewers;
                    }
                }

                return Array.Empty<MessageViewer>();
            }

            var played = message.Content is MessageVoiceNote or MessageVideoNote;
            var reacted = message.InteractionInfo.TotalReactions();

            var placeholder = new MenuFlyoutSubItem
            {
                Text = "...",
                Icon = MenuFlyoutHelper.CreateIcon(reacted > 0 ? Icons.Heart : played ? Icons.Play : Icons.Seen)
            };

            flyout.Items.Add(placeholder);

            var separator = flyout.CreateFlyoutSeparator();

            // Width must be fixed because viewers are loaded asynchronously
            placeholder.Width = 200;
            //placeholder.Style = BootStrapper.Current.Resources["MessageSeenMenuFlyoutItemStyle"] as Style;

            var viewers = await GetMessageViewersAsync(message, properties);
            if (viewers.Count > 0 || reacted > 0)
            {
                var popup = new InteractionsView(message.ClientService, message.ChatId, message.Id, new MessageViewers(viewers))
                {
                    Width = 264,
                    Height = 48 * Math.Max(viewers.Count, reacted),
                    MinHeight = 48,
                    MaxHeight = 360
                };

                void handler(InteractionsView sender, ItemClickEventArgs e)
                {
                    sender.ItemClick -= handler;
                    flyout.Hide();

                    if (e.ClickedItem is AddedReaction addedReaction)
                    {
                        var state = new NavigationState();
                        if (properties.CanReportReactions)
                        {
                            state.Add("report_reactions", new ReportMessageReactions(message.ChatId, message.Id, addedReaction.SenderId));
                        }
                        if (properties.CanDeleteReactions)
                        {
                            state.Add("delete_reactions", new DeleteMessageReactionsFromSender(message.ChatId, message.Id, addedReaction.SenderId));
                        }

                        ViewModel.NavigationService.NavigateToSender(addedReaction.SenderId, state: state);
                    }
                    else if (e.ClickedItem is MessageViewer messageViewer)
                    {
                        ViewModel.NavigationService.NavigateToUser(messageViewer.UserId);
                    }
                }

                popup.ItemClick += handler;

                placeholder.Items.Add(new MenuFlyoutContent
                {
                    Content = popup,
                    Padding = new Thickness(0)
                });

                string text;
                if (reacted > 0)
                {
                    if (reacted < viewers.Count)
                    {
                        text = string.Format(Locale.Declension(Strings.R.Reacted, reacted, false), string.Format("{0}/{1}", reacted, viewers.Count));
                    }
                    else
                    {
                        text = Locale.Declension(Strings.R.Reacted, reacted);
                    }
                }
                else if (viewers.Count > 0)
                {
                    text = Locale.Declension(played ? Strings.R.MessagePlayed : Strings.R.MessageSeen, viewers.Count);
                }
                else
                {
                    text = Strings.NobodyViewed;
                }

                var pictures = new StackPanel();
                pictures.Orientation = Orientation.Horizontal;

                var rect1 = CanvasGeometry.CreateRectangle(null, 0, 0, 24, 24);
                var elli1 = CanvasGeometry.CreateEllipse(null, -2, 12, 14, 14);
                var group1 = CanvasGeometry.CreateGroup(null, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);

                var compositor = BootStrapper.Current.Compositor;
                var geometry = compositor.CreatePathGeometry(new CompositionPath(group1));
                var clip = compositor.CreateGeometricClip(geometry);

                for (int i = 0; i < Math.Min(3, viewers.Count); i++)
                {
                    var user = message.ClientService.GetUser(viewers[i].UserId);
                    var picture = new ProfilePicture();
                    picture.Size = 24;
                    picture.Source = ProfilePictureSource.User(message.ClientService, user);
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
                placeholder.IsEnabled = false;
            }
        }

        private bool CanGetMessageEmojis(MessageViewModel message, out IList<long> customEmojiIds)
        {
            var caption = message.GetCaption();
            if (caption?.Entities == null || caption.Entities.Empty())
            {
                customEmojiIds = null;
                return false;
            }

            HashSet<long> temp = null;

            foreach (var item in caption.Entities)
            {
                if (item.Type is TextEntityTypeCustomEmoji customEmoji)
                {
                    temp ??= new();
                    temp.Add(customEmoji.CustomEmojiId);
                }
            }

            if (temp != null)
            {
                customEmojiIds = temp.ToList();
                return true;
            }

            customEmojiIds = null;
            return false;
        }

        private async void LoadMessageEmojis(MessageViewModel message, MenuFlyout flyout, IList<long> customEmojiIds)
        {
            void ShowSkeleton(UIElement element)
            {
                var size = new Vector2(200, 48);
                var itemHeight = 6 + 36 + 6;

                var shapes = new List<CanvasGeometry>();

                shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 8, 6, 180, 14, 4, 4));
                shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 8, 6 + 16, 140, 14, 4, 4));

                var compositor = BootStrapper.Current.Compositor;

                var geometries = shapes.ToArray();
                var path = compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreateGroup(null, geometries, CanvasFilledRegionDetermination.Winding)));

                var transparent = Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF);
                var foregroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);
                var backgroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

                var lookup = ThemeService.GetLookup(ActualTheme);
                if (lookup.TryGet("MenuFlyoutItemBackgroundPointerOver", out Color color))
                {
                    foregroundColor = color;
                    backgroundColor = color;
                }

                var gradient = compositor.CreateLinearGradientBrush();
                gradient.StartPoint = new Vector2(0, 0);
                gradient.EndPoint = new Vector2(1, 0);
                gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, transparent));
                gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, foregroundColor));
                gradient.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, transparent));

                var background = compositor.CreateRectangleGeometry();
                background.Size = size;
                var backgroundShape = compositor.CreateSpriteShape(background);
                backgroundShape.FillBrush = compositor.CreateColorBrush(backgroundColor);

                var foreground = compositor.CreateRectangleGeometry();
                foreground.Size = size;
                var foregroundShape = compositor.CreateSpriteShape(foreground);
                foregroundShape.FillBrush = gradient;

                var clip = compositor.CreateGeometricClip(path);
                var visual = compositor.CreateShapeVisual();
                visual.Clip = clip;
                visual.Shapes.Add(backgroundShape);
                visual.Shapes.Add(foregroundShape);
                visual.RelativeSizeAdjustment = Vector2.One;

                var animation = compositor.CreateVector2KeyFrameAnimation();
                animation.InsertKeyFrame(0, new Vector2(-size.X, 0));
                animation.InsertKeyFrame(1, new Vector2(size.X, 0));
                animation.IterationBehavior = AnimationIterationBehavior.Forever;
                animation.Duration = TimeSpan.FromSeconds(1);

                foregroundShape.StartAnimation("Offset", animation);

                ElementCompositionPreview.SetElementChildVisual(element, visual);
            }

            var grid = new Grid
            {
                // Approximate height for two lines of text
                //Height = 46,
                Width = 200 - 4 - 4,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            ShowSkeleton(grid);

            var button = new Button
            {
                Content = grid,
                Style = BootStrapper.Current.Resources["ListEmptyButtonStyle"] as Style,
                CornerRadius = new CornerRadius(4),
                IsEnabled = false
            };

            var block = new RichTextBlock
            {
                // Needed due to reactions menu, as it can't be repositioned
                MaxLines = 2,
                FontSize = 12,
                IsTextSelectionEnabled = false,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(11, 3, 11, 5),
                VerticalAlignment = VerticalAlignment.Center
            };

            var paragraph = new Paragraph();
            paragraph.Inlines.Add("\n");
            block.Blocks.Add(paragraph);
            grid.Children.Add(block);

            void click(object sender, RoutedEventArgs e)
            {
                button.Click -= click;
                flyout.Hide();

                ViewModel.ShowMessageEmoji(message);
            }

            button.Click += click;

            var content = new MenuFlyoutContent
            {
                Content = button,
                Padding = new Thickness(4, 2, 4, 2)
            };

            flyout.CreateFlyoutSeparator();
            flyout.Items.Add(content);

            var function = message.ClientService.GetCustomEmojiStickerSets(customEmojiIds);

            // Currently unneeded because we fix size to two lines
            //await Task.WhenAll(function, Task.Delay(250));

            var response = await function;
            if (response is StickerSets stickerSets)
            {
                button.IsEnabled = true;

                if (stickerSets.Sets.Count != 1)
                {
                    TextBlockHelper.SetMarkdown(block, paragraph.Inlines, Locale.Declension(Strings.R.MessageContainsEmojiPacks, stickerSets.Sets.Count));
                }
                else
                {
                    var player = new CustomEmojiIcon();
                    player.LoopCount = 0;
                    player.Source = DelayedFileSource.FromStickerSetInfo(message.ClientService, stickerSets.Sets[0]);

                    player.HorizontalAlignment = HorizontalAlignment.Left;
                    player.FlowDirection = FlowDirection.LeftToRight;
                    player.Margin = new Thickness(0, -2, 0, -6);

                    var inline = new InlineUIContainer();
                    inline.Child = player;

                    var text = Strings.MessageContainsEmojiPack;
                    var index = text.IndexOf("{0}");

                    var prefix = text.Substring(0, index);
                    var suffix = text.Substring(index + 3);

                    paragraph.Inlines.Clear();
                    paragraph.Inlines.Add(prefix);
                    paragraph.Inlines.Add(inline);
                    paragraph.Inlines.Add($" {stickerSets.Sets[0].Title}", FontWeights.SemiBold);
                    paragraph.Inlines.Add(suffix);
                }

                var visual = ElementCompositionPreview.GetElementChildVisual(grid);
                var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(0, 1);
                animation.InsertKeyFrame(1, 0);

                visual.StartAnimation("Opacity", animation);
            }
        }

        private static bool CanGetMessageAuthor(MessageViewModel message, MessageProperties properties)
        {
            return properties.CanGetAuthor;
        }

        private async void LoadMessageAuthor(MessageViewModel message, MessageProperties properties, MenuFlyout flyout)
        {
            static async Task<User> GetMessageAuthorAsync(MessageViewModel message, MessageProperties properties)
            {
                if (CanGetMessageAuthor(message, properties))
                {
                    var response = await message.ClientService.SendAsync(new GetMessageAuthor(message.ChatId, message.Id));
                    if (response is User user)
                    {
                        return user;
                    }
                }

                return null;
            }

            var textBlock = new TextBlock();
            textBlock.Text = "...";
            textBlock.FontSize = 12;

            var placeholder = new MenuFlyoutContent();
            placeholder.Content = textBlock;
            placeholder.FontSize = 12;
            //placeholder.Icon = MenuFlyoutHelper.CreateIcon(Icons.Seen);
            placeholder.Padding = new Thickness(12, 4, 12, 4);
            placeholder.HorizontalAlignment = HorizontalAlignment.Left;

            // Width must be fixed because viewers are loaded asynchronously
            placeholder.Width = 200;

            flyout.Items.Add(placeholder);
            flyout.CreateFlyoutSeparator();


            var user = await GetMessageAuthorAsync(message, properties);
            if (user != null)
            {
                var markdown = ClientEx.ParseMarkdown(string.Format(Strings.MessageAuthorSentBy, user.FullName()));
                if (markdown.Entities.Count == 1)
                {
                    markdown.Entities[0].Type = new TextEntityTypeMentionName(user.Id);
                }

                TextBlockHelper.SetFormattedText(textBlock, markdown);
            }
        }

        private static bool CanGetMessageReadDate(MessageViewModel message, MessageProperties properties, bool reactions = true)
        {
            if (message.LastReadOutboxMessageId < message.Id || !properties.CanGetReadDate)
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
            placeholder.HorizontalAlignment = HorizontalAlignment.Left;

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
            return message.SchedulingState != null && !message.IsPaidStarSuggestedPost && !message.IsPaidTonSuggestedPost;
        }

        private bool MessageReschedule_Loaded(MessageViewModel message)
        {
            return message.SchedulingState is not null and not MessageSchedulingStateSendWhenVideoProcessed && !message.IsPaidStarSuggestedPost && !message.IsPaidTonSuggestedPost;
        }

        private bool MessageQuote_Loaded(MessageQuote quote, MessageProperties properties)
        {
            return MessageReply_Loaded(quote.Message, properties);
        }

        private bool MessageReply_Loaded(MessageViewModel message, MessageProperties properties)
        {
            if (message.SchedulingState != null || ViewModel.Type is not DialogType.History and not DialogType.Thread || ViewModel.IsSavedMessagesTab)
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
                if (ViewModel.Type is not DialogType.Thread || (ViewModel.ForumTopic == null && ViewModel.DirectMessagesChatTopic == null))
                {
                    return false;
                }
            }

            return properties.CanBePinned;
        }

        private bool MessageEdit_Loaded(MessageViewModel message, MessageProperties properties)
        {
            if (ViewModel.IsSavedMessagesTab)
            {
                return false;
            }

            if (message is QuickReplyMessageViewModel quickReply)
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
            if (chat == null || !chat.CanBeReported || message.Event != null || message.IsService || message.IsOutgoing)
            {
                return false;
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
            var caption = message.GetTranslatableText();
            if (caption != null)
            {
                return ViewModel.TranslateService.CanTranslateText(caption.Text);
            }
            else if (message.Content is MessagePoll poll)
            {
                return ViewModel.TranslateService.CanTranslateText(poll.Poll.Question.Text);
            }
            else if (message.Content is MessageChecklist checklist)
            {
                return ViewModel.TranslateService.CanTranslateText(checklist.List.Title.Text);
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

        private bool MessageSelect_Loaded(MessageViewModel message)
        {
            if (ViewModel.Type == DialogType.EventLog || ViewModel.IsSavedMessagesTab || message.IsService)
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

            if (message.Content is MessageAlbum album)
            {
                foreach (var item in album.Messages)
                {
                    var temp = item.GetFile();
                    if (temp != null && !temp.Local.IsDownloadingCompleted)
                    {
                        return false;
                    }
                }

                return true;
            }

            var file = message.GetFile();
            if (file != null)
            {
                return file.Local.IsDownloadingCompleted;
            }

            return false;
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

        public void Emojis_ItemClick(object emoji)
        {
            if (emoji is string text)
            {
                TextField.InsertText(text);
            }
            else if (emoji is Sticker sticker && sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                if (ViewModel.IsPremium || (ViewModel.ClientService.TryGetSupergroupFull(ViewModel.Chat, out SupergroupFullInfo fullInfo) && fullInfo.CustomEmojiStickerSetId == sticker.SetId))
                {
                    ViewModel.InsertedCustomEmojiIds.Add(customEmoji.CustomEmojiId);
                    TextField.InsertEmoji(sticker);
                }
                else
                {
                    ToastPopup.ShowFeaturePromo(ViewModel.NavigationService, new PremiumFeatureCustomEmoji());
                }
            }

            _focusState.Set(FocusState.Programmatic);
        }

        public void Stickers_ItemClick(Sticker sticker)
        {
            Stickers_ItemClick(null, new StickerDrawerItemClickEventArgs(sticker, false));
        }

        public void Stickers_ItemClick(object sender, StickerDrawerItemClickEventArgs e)
        {
            ViewModel.SendSticker(e.Sticker, SchedulingState.Auto, null, null, e.FromStickerSet);
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
                    TextField.ClearText();
                    ViewModel.SendMessage(insert);
                }

                TextField.IsMenuExpanded = false;
            }
            else if (e.ClickedItem is string hashtag && entity is AutocompleteEntity.Hashtag)
            {
                InsertText($"{hashtag} ");
            }
            else if (e.ClickedItem is Sticker sticker)
            {
                TextField.ClearText();
                ViewModel.SendSticker(sticker, SchedulingState.Auto, null, result);

                ButtonStickers.Collapse();
            }
            else if (e.ClickedItem is QuickReplyShortcut shortcut)
            {
                TextField.ClearText();

                if (ViewModel.IsPremium)
                {
                    ViewModel.ClientService.Send(new SendQuickReplyShortcutMessages(ViewModel.Chat.Id, shortcut.Id, 0));
                }
                else
                {
                    ViewModel.NavigationService.ShowPromo(new PremiumFeatureBusiness());
                }
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
            manage.Clip = null;
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
            if (button.Tag is DateTime date && ViewModel.Type is DialogType.History or DialogType.Thread)
            {
                var dialog = new CalendarPopup(ViewModel.ClientService, ViewModel.ChatId, ViewModel.TopicId, date);
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

        private void ForumTopic_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.Tag is MessageTopic messageTopic && ViewModel.Type is DialogType.History or DialogType.Thread)
            {
                NavigateToMessageTopic(ViewModel.Chat, messageTopic);
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
            ListInline?.MaxHeight = Math.Min(320, Math.Max(e.NewSize.Height - 48, 0));

            ListAutocomplete.MaxHeight = Math.Min(320, Math.Max(e.NewSize.Height - 48, 0));
        }

        private void DateHeaderPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ElementComposition.GetElementVisual(sender as UIElement).CenterPoint = new Vector3((float)e.NewSize.Width / 2f, (float)e.NewSize.Height / 2f, 0);
        }

        private void ItemsPanelRoot_Loading(FrameworkElement sender, object args)
        {
            sender.MaxWidth = SettingsService.Current.IsAdaptiveWideEnabled ? 1024 : double.PositiveInfinity;
            Messages.SetScrollingMode();
        }

        private async void ServiceMessage_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as MessageService;
            var message = button.Message;

            if (message == null)
            {
                button = button.GetParent<MessageService>();
                message = button?.Message as MessageViewModel;
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

                    var story = asyncStory.Story;
                    story ??= await _viewModel.ClientService.SendAsync(new GetStory(asyncStory.StoryPosterChatId, asyncStory.StoryId, true)) as Story;

                    if (story != null)
                    {
                        var activeStories = new ActiveStoriesViewModel(message.ClientService, message.Delegate.Settings, message.Delegate.Aggregator, story);
                        var viewModel = StoryListViewModel.Create(_viewModel.NavigationService, activeStories);

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
            else if (message.Content is MessageHeaderMessageTopic)
            {
                NavigateToMessageTopic(message.Chat, message.TopicId);
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
                args.ItemContainer = new TextGridViewItem
                {
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                };
                args.ItemContainer.Style = sender.ItemContainerStyle;

                _autocompleteZoomer.ElementPrepared(args.ItemContainer);
            }

            if (args.Item is EmojiData or Sticker)
            {
                var radius = SettingsService.Current.Appearance.CornerRadius;
                var min = Math.Max(4, radius - 4);

                args.ItemContainer.Margin = new Thickness(4);
                args.ItemContainer.CornerRadius = new CornerRadius(args.ItemIndex == 0 ? min : 4, 4, 4, 4);
            }
            else
            {
                args.ItemContainer.Margin = new Thickness();
                args.ItemContainer.CornerRadius = new CornerRadius();
            }

            args.ItemContainer.PointerEntered -= Autocomplete_PointerEntered;
            args.ItemContainer.PointerExited -= Autocomplete_PointerExited;

            if (args.Item is string)
            {
                args.ItemContainer.PointerEntered += Autocomplete_PointerEntered;
                args.ItemContainer.PointerExited += Autocomplete_PointerExited;
            }

            args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
            args.IsContainerPrepared = true;
        }

        private void Autocomplete_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is SelectorItem { ContentTemplateRoot: Grid content })
            {
                if (content.Children.Count > 1 && content.Children[1] is Button button)
                {
                    button.Visibility = Visibility.Visible;
                }
            }
        }

        private void Autocomplete_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is SelectorItem { ContentTemplateRoot: Grid content })
            {
                if (content.Children.Count > 1 && content.Children[1] is Button button)
                {
                    button.Visibility = Visibility.Collapsed;
                }
            }
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
                    photo.Source = ProfilePictureSource.User(ViewModel.ClientService, user);
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
                    photo.Source = ProfilePictureSource.User(ViewModel.ClientService, user);
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

                photo.Source = ProfilePictureSource.User(ViewModel.ClientService, user);
            }
            else if (args.Item is string hashtag)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                var title = content.Children[0] as TextBlock;
                title.Text = hashtag;

                var clear = content.Children[1] as Button;
                clear.Click -= RemoveHashtag_Click;
                clear.Click += RemoveHashtag_Click;
                clear.Tag = hashtag;
                clear.Visibility = Visibility.Collapsed;
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

        private async void RemoveHashtag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string hashtag })
            {
                _viewModel.ClientService.Send(new RemoveRecentHashtag(hashtag));

                await Task.Yield();

                if (ListAutocomplete.ItemsSource is AutocompleteCollection collection)
                {
                    collection.Remove(hashtag);
                }
            }
        }

        private bool? _replyEnabled = null;
        private bool _actionCollapsed = true;

        private void ShowAction(string content, bool enabled, bool replyEnabled = false)
        {
            if (FromPreview)
            {
                ButtonAction.Visibility = Visibility.Collapsed;
                ChatFooter.Visibility = Visibility.Collapsed;
                TextArea.Visibility = Visibility.Collapsed;
                return;
            }
            else if (ViewModel.ClientService.FreezeState.IsFrozen)
            {
                ShowFrozen();
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

        private void ShowFrozen()
        {
            var block = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Style = BootStrapper.Current.Resources["BodyTextBlockStyle"] as Style
            };

            block.Inlines.Add(new Run
            {
                Text = Strings.AccountFrozenBottomTitle,
                FontWeight = FontWeights.SemiBold,
                Foreground = BootStrapper.Current.Resources["SystemFillColorCriticalBrush"] as Brush
            });

            block.Inlines.Add(new LineBreak());
            block.Inlines.Add(Strings.AccountFrozenBottomSubtitle);

            ButtonAction.Content = block;

            _replyEnabled = false;
            ButtonAction.IsEnabled = true;

            _actionCollapsed = false;
            ButtonAction.Visibility = Visibility.Visible;
            ChatFooter.Visibility = Visibility.Visible;
            TextArea.Visibility = Visibility.Collapsed;
        }

        private void ShowArea(long paidMessageStarCount, bool permanent = true)
        {
            if (FromPreview)
            {
                ButtonAction.Visibility = Visibility.Collapsed;
                ChatFooter.Visibility = Visibility.Collapsed;
                TextArea.Visibility = Visibility.Collapsed;
                return;
            }
            else if (ViewModel.ClientService.FreezeState.IsFrozen)
            {
                ShowFrozen();
                return;
            }

            if (permanent)
            {
                _replyEnabled = null;

                btnSendMessage.Visibility = paidMessageStarCount > 0
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                btnPaidMessage.Visibility = paidMessageStarCount > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (paidMessageStarCount > 0)
                {
                    btnSendMessage.Visibility = Visibility.Collapsed;
                    btnPaidMessage.Visibility = Visibility.Visible;

                    btnPaidMessage.Content = Icons.Premium16 + Icons.Spacing + Formatter.ShortNumber(paidMessageStarCount);
                }
                else
                {
                    btnSendMessage.Visibility = Visibility.Visible;
                    btnPaidMessage.Visibility = Visibility.Collapsed;
                }
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
            return chat?.Id == ViewModel?.Chat?.Id && !FromPreview;
        }

        #region UI delegate

        public void UpdateChat(Chat chat)
        {
            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
            UpdateChatEmojiStatus(chat);

            UpdateChatActiveStories(chat);

            UpdateChatUnreadMentionCount(chat, chat.UnreadMentionCount);
            UpdateChatUnreadReactionCount(chat, chat.UnreadReactionCount);
            UpdateChatUnreadPollVoteCount(chat, chat.UnreadPollVoteCount);
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

            UpdateForumTopics(chat);
        }

        private void UpdateForumTopics(Chat chat)
        {
            if (ViewModel.Type is DialogType.History or DialogType.Thread && chat.HasForumTabs(ViewModel.ClientService, out bool forum))
            {
                if (_forumViewModel == null || _forumViewModel.IsForum != forum)
                {
                    _forumViewModel = new TopicListViewModel(ViewModel.ClientService, ViewModel.Settings, ViewModel.Aggregator, null, false, forum);
                    _forumViewModel.NavigationService = ViewModel.NavigationService;
                    _forumViewModel.Dispatcher = ViewModel.Dispatcher;

                    ForumNavigation.UpdateType(ForumViewType.Vertical);
                    ForumNavigationHorizontal.UpdateType(ForumViewType.Horizontal);

                    if (_forumCollapsed == ForumViewType.Vertical)
                    {
                        _forumViewModel.Delegate = ForumNavigation;
                        ForumNavigation.ViewModel = _forumViewModel;
                    }
                    else if (_forumCollapsed == ForumViewType.Horizontal)
                    {
                        _forumViewModel.Delegate = ForumNavigationHorizontal;
                        ForumNavigationHorizontal.ViewModel = _forumViewModel;
                    }
                }

                _forumViewModel.SetChat(chat);
                _forumViewModel.SelectedItem = ViewModel.TopicId;
                _forumViewModel.Delegate?.SetSelectedItem(_forumViewModel.Items.GetItem(ViewModel.TopicId));

                ShowHideForumTopics(ViewModel.Settings.UseLeftTabsForForums ? ForumViewType.Vertical : ForumViewType.Horizontal);
            }
            else
            {
                ShowHideForumTopics(ForumViewType.List);
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
                PhotoAlias.Source = ProfilePictureSource.MessageSender(ViewModel.ClientService, defaultMessageSenderId);
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

            if (ViewModel.SavedMessagesTopic != null && ViewModel.ClientService.TryGetChat(ViewModel.SavedMessagesTopic.Type, out Chat savedMessagesChat))
            {
                chat = savedMessagesChat;
            }

            UpdateChatTheme(chat, chat.Theme);
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

        private void UpdateChatTheme(Chat chat, ChatTheme theme)
        {
            ThemeSettings lightSettings = null;
            ThemeSettings darkSettings = null;

            if (ViewModel.ClientService.TryGetEmojiChatTheme(theme, out EmojiChatTheme emoji))
            {
                lightSettings = emoji.LightSettings;
                darkSettings = emoji.DarkSettings;
            }
            else if (theme is ChatThemeGift gift)
            {
                lightSettings = gift.GiftTheme.LightSettings;
                darkSettings = gift.GiftTheme.DarkSettings;
            }

            if (Theme.Current.Update(ActualTheme, theme, lightSettings, darkSettings, chat.Background))
            {
                var current = chat.Background?.Background;
                if (current?.Type is BackgroundTypeChatTheme typeChatTheme && ViewModel.ClientService.TryGetEmojiChatTheme(typeChatTheme.ThemeName, out emoji))
                {
                    lightSettings = emoji.LightSettings;
                    darkSettings = emoji.DarkSettings;
                }

                current ??= ActualTheme == ElementTheme.Light ? lightSettings?.Background : darkSettings?.Background;
                current ??= ViewModel.ClientService.GetDefaultBackground(ActualTheme == ElementTheme.Dark);

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
            if (ViewModel.ForumTopic != null)
            {
                ChatTitle = ViewModel.ForumTopic.Info.Name;
            }
            else if (ViewModel.DirectMessagesChatTopic != null)
            {
                ChatTitle = ViewModel.ClientService.GetTitle(ViewModel.DirectMessagesChatTopic.SenderId);
            }
            else if (ViewModel.Thread != null)
            {
                var message = ViewModel.Thread.Messages.LastOrDefault();
                if (message == null || message.InteractionInfo?.ReplyInfo == null)
                {
                    return;
                }

                // TODO: UpdateTopicMessageCount
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
            else if (ViewModel.SavedMessagesTopic != null)
            {
                if (ViewModel.SavedMessagesTopic.Type is SavedMessagesTopicTypeMyNotes)
                {
                    ChatTitle = Strings.MyNotes;
                }
                else if (ViewModel.SavedMessagesTopic.Type is SavedMessagesTopicTypeAuthorHidden)
                {
                    ChatTitle = Strings.AnonymousForward;
                }
                else if (ViewModel.SavedMessagesTopic.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && ViewModel.ClientService.TryGetChat(savedFromChat.ChatId, out Chat savedChat))
                {
                    ChatTitle = ViewModel.ClientService.GetTitle(savedChat);
                }
            }
            else if (ViewModel.Type == DialogType.ScheduledMessages)
            {
                ChatTitle = ViewModel.ClientService.IsSavedMessages(chat) ? Strings.Reminders : Strings.ScheduledMessages;
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
            if (ViewModel.ForumTopic is ForumTopic topic)
            {
                Photo.Source = null;

                if (topic.Info.Icon.CustomEmojiId != 0)
                {
                    Icon.Source = new CustomEmojiFileSource(ViewModel.ClientService, topic.Info.Icon.CustomEmojiId);
                    TopicIconRoot.Visibility = Visibility.Collapsed;
                    TopicIconGeneral.Visibility = Visibility.Collapsed;
                }
                else if (topic.Info.IsGeneral)
                {
                    Icon.Source = null;
                    TopicIconRoot.Visibility = Visibility.Collapsed;
                    TopicIconGeneral.Visibility = Visibility.Visible;
                }
                else
                {
                    Icon.Source = null;
                    TopicIconRoot.Visibility = Visibility.Visible;
                    TopicIconGeneral.Visibility = Visibility.Collapsed;

                    var brush = ForumTopicCell.GetIconGradient(topic.Info.Icon);

                    TopicIconPath.Fill = brush;
                    TopicIconPath.Stroke = new SolidColorBrush(brush.GradientStops[1].Color);
                    TopicIconText.Text = InitialNameStringConverter.Convert(topic.Info.Name);
                }
            }
            else if (ViewModel.DirectMessagesChatTopic != null)
            {
                TopicIconRoot.Visibility = Visibility.Collapsed;
                TopicIconGeneral.Visibility = Visibility.Collapsed;

                Icon.Source = null;
                Photo.Source = ProfilePictureSource.MessageSender(ViewModel.ClientService, ViewModel.DirectMessagesChatTopic.SenderId);
            }
            else if (ViewModel.Thread != null)
            {
                TopicIconRoot.Visibility = Visibility.Collapsed;
                TopicIconGeneral.Visibility = Visibility.Collapsed;

                Icon.Source = null;
                Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.ArrowReplyFilled, 5);
            }
            else if (ViewModel.SavedMessagesTopic != null)
            {
                TopicIconRoot.Visibility = Visibility.Collapsed;
                TopicIconGeneral.Visibility = Visibility.Collapsed;

                Icon.Source = null;

                if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeMyNotes)
                {
                    Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.MyNotesFilled, 5);
                }
                else if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeAuthorHidden)
                {
                    Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.AuthorHiddenFilled, 5);
                }
                else if (ViewModel.SavedMessagesTopic?.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && ViewModel.ClientService.TryGetChat(savedFromChat.ChatId, out Chat savedChat))
                {
                    Photo.Source = ProfilePictureSource.Chat(ViewModel.ClientService, savedChat);
                }
            }
            else
            {
                TopicIconRoot.Visibility = Visibility.Collapsed;
                TopicIconGeneral.Visibility = Visibility.Collapsed;

                Icon.Source = null;
                Photo.Source = ProfilePictureSource.Chat(ViewModel.ClientService, chat);
            }
        }

        public void UpdateChatLastMessage(Chat chat)
        {
            if (_forumTopicId != chat.LastMessage.ForumTopicId())
            {
                UpdateChatTextPlaceholder(chat);
            }
        }

        public void UpdateChatEmojiStatus(Chat chat)
        {
            if (ViewModel.SavedMessagesTopic != null)
            {
                if (ViewModel.SavedMessagesTopic.Type is SavedMessagesTopicTypeMyNotes)
                {
                    Identity.ClearStatus(BotVerified);
                }
                else if (ViewModel.SavedMessagesTopic.Type is SavedMessagesTopicTypeAuthorHidden)
                {
                    Identity.ClearStatus(BotVerified);
                }
                else if (ViewModel.SavedMessagesTopic.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && ViewModel.ClientService.TryGetChat(savedFromChat.ChatId, out Chat savedChat))
                {
                    Identity.SetStatus(_viewModel.ClientService, savedChat, BotVerified);
                }
            }
            else if (ViewModel.DirectMessagesChatTopic != null)
            {
                Identity.SetStatus(_viewModel.ClientService, ViewModel.DirectMessagesChatTopic.SenderId);
            }
            else
            {
                Identity.SetStatus(_viewModel.ClientService, chat, BotVerified);
            }
        }

        public void UpdateChatAccentColors(Chat chat)
        {
            // Not needed in chat view
        }

        public void UpdateChatGifts(Chat chat)
        {
            // Not needed in chat view
        }

        public void UpdateChatActiveStories(Chat chat)
        {
            if (ViewModel.Type == DialogType.History)
            {
                Segments.SetChat(ViewModel.ClientService, chat, 36);
            }
            else
            {
                Segments.Clear();
            }
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

            UpdateChatTextPlaceholder(chat);
        }

        private void UpdateChatTextPlaceholder(Chat chat)
        {
            TextField.PlaceholderText = GetPlaceholder(chat, out bool readOnly, out int forumTopicId);

            if (_isTextReadOnly != readOnly)
            {
                _isTextReadOnly = readOnly;
                TextField.IsReadOnly = readOnly;
            }

            _forumTopicId = forumTopicId;
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

            if (actions != null && actions.Count > 0 && (ViewModel.Type is DialogType.History or DialogType.Thread))
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
            if ((ViewModel.Type == DialogType.History || ViewModel.ForumTopic != null) && count > 0)
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
            if ((ViewModel.Type == DialogType.History || ViewModel.ForumTopic != null) && count > 0)
            {
                Arrows.UnreadReactionsCount = count;
            }
            else
            {
                Arrows.UnreadReactionsCount = 0;
            }
        }

        public void UpdateChatUnreadPollVoteCount(Chat chat, int count)
        {
            if ((ViewModel.Type == DialogType.History || ViewModel.ForumTopic != null) && count > 0)
            {
                Arrows.UnreadPollVoteCount = count;
            }
            else
            {
                Arrows.UnreadPollVoteCount = 0;
            }
        }

        private string GetPlaceholder(Chat chat, out bool readOnly, out int forumTopicId)
        {
            readOnly = false;
            forumTopicId = 0;

            if (ViewModel.ClientService.TryGetUserFull(chat, out UserFullInfo userFull))
            {
                if (userFull.OutgoingPaidMessageStarCount > 0)
                {
                    return string.Format(Strings.TypeMessageForStars.ReplaceStar(Icons.Premium), userFull.OutgoingPaidMessageStarCount.ToString("N0"));
                }
            }
            else if (ViewModel.ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                return GetPlaceholder(chat, supergroup, out readOnly, out forumTopicId);
            }
            else if (ViewModel.ClientService.TryGetBasicGroup(chat, out BasicGroup basicGroup))
            {
                return GetPlaceholder(chat, basicGroup, out readOnly);
            }

            return Strings.TypeMessage;
        }

        private string GetPlaceholder(Chat chat, Supergroup supergroup, out bool readOnly, out int forumTopicId)
        {
            readOnly = false;
            forumTopicId = 0;

            if (supergroup.IsChannel)
            {
                return chat.DefaultDisableNotification
                    ? Strings.ChannelSilentBroadcast
                    : Strings.ChannelBroadcast;
            }
            else if (chat.Permissions.CanSendBasicMessages is false && supergroup.Status is not ChatMemberStatusCreator and not ChatMemberStatusAdministrator)
            {
                if (ViewModel.ClientService.TryGetSupergroupFull(supergroup.Id, out SupergroupFullInfo fullInfo))
                {
                    if (fullInfo.UnrestrictBoostCount != 0 && fullInfo.MyBoostCount >= fullInfo.UnrestrictBoostCount)
                    {
                        return Strings.TypeMessage;
                    }
                }

                readOnly = true;
                return Strings.PlainTextRestrictedHint;
            }
            else if (supergroup.Status is ChatMemberStatusCreator { IsAnonymous: true } || supergroup.Status is ChatMemberStatusAdministrator { Rights.IsAnonymous: true })
            {
                return Strings.SendAnonymously;
            }
            else if (supergroup.IsDirectMessagesGroup)
            {
                if (supergroup.IsAdministeredDirectMessagesGroup)
                {
                    return Strings.TypeMessage;
                }
                else if (supergroup.PaidMessageStarCount > 0)
                {
                    return string.Format(Strings.SuggestPostForStars.ReplaceStar(Icons.Premium), supergroup.PaidMessageStarCount.ToString("N0"));
                }

                return Strings.SuggestPostForFree;
            }
            else if (supergroup.PaidMessageStarCount > 0 && supergroup.Status is not ChatMemberStatusCreator and not ChatMemberStatusAdministrator)
            {
                return string.Format(Strings.TypeMessageForStars.ReplaceStar(Icons.Premium), supergroup.PaidMessageStarCount.ToString("N0"));
            }
            else if (supergroup.IsForum && ViewModel.Type == DialogType.History && ViewModel.ClientService.TryGetForumTopic(chat.Id, chat.LastMessage?.TopicId, out ForumTopic forumTopic))
            {
                forumTopicId = forumTopic.Info.ForumTopicId;
                return string.Format(Strings.TypeMessageIn, forumTopic.Info.Name);
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
                    UpdateChatTextPlaceholder(chat);
                }

                ButtonMarkup.Visibility = Visibility.Collapsed;
                ShowHideMarkup(false, false);
            }
            else if (message?.ReplyMarkup is ReplyMarkupShowKeyboard { OneTime: true, IsPersonal: false })
            {
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
                        UpdateChatTextPlaceholder(chat);
                    }

                    ButtonMarkup.Visibility = Visibility.Visible;
                    ShowHideMarkup(true);
                }
                else
                {
                    UpdateChatTextPlaceholder(chat);

                    ButtonMarkup.Visibility = Visibility.Collapsed;
                    ShowHideMarkup(false, false);
                }
            }
        }

        public void UpdatePinnedMessage(Chat chat, bool known)
        {
            PinnedMessage.UpdateMessage(chat, null, known, -1, ViewModel.PinnedMessages.TotalCount, false);
        }

        public void UpdateComposerHeader(Chat chat, MessageComposerHeader header)
        {
            CheckButtonsVisibility();

            if (header == null || (header.IsEmpty && header.LinkPreviewDisabled))
            {
                // Let's reset
                //ComposerHeader.Visibility = Visibility.Collapsed;
                ShowHideComposerHeader(false);
                ComposerHeaderReference.UpdateComposerHeader(null);

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
                ComposerHeaderReference.UpdateComposerHeader(header);

                TextField.Reply = header;

                var editing = header.Editing?.Message;
                if (editing != null)
                {
                    switch (editing.Content)
                    {
                        case MessageAnimation:
                        case MessageAudio:
                        case MessageDocument:
                        case MessageText:
                            ButtonAttach.Glyph = Icons.Replace24;
                            ButtonAttach.IsEnabled = editing.SchedulingState is not MessageSchedulingStateSendWhenVideoProcessed;
                            break;
                        case MessagePhoto photo:
                            ButtonAttach.Glyph = !photo.IsSecret ? Icons.Replace24 : Icons.Attach24;
                            ButtonAttach.IsEnabled = !photo.IsSecret && editing.SchedulingState is not MessageSchedulingStateSendWhenVideoProcessed;
                            break;
                        case MessageVideo video:
                            ButtonAttach.Glyph = !video.IsSecret ? Icons.Replace24 : Icons.Attach24;
                            ButtonAttach.IsEnabled = !video.IsSecret && editing.SchedulingState is not MessageSchedulingStateSendWhenVideoProcessed;
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
                    else if (header.ReplyTo != null)
                    {
                        ComposerHeaderGlyph.Glyph = Icons.ArrowReply24;
                    }
                    else if (header.SuggestedPostInfo != null)
                    {
                        ComposerHeaderGlyph.Glyph = Icons.ChatDollar24;
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

        private void ShowHideComposerHeader(bool show, bool sendout = false)
        {
            if (ButtonAction.Visibility == Visibility.Visible)
            {
                if (_replyEnabled == true && show)
                {
                    ShowArea(0, false);
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

            _composerHeaderCollapsed = !show;
            ComposerHeader.Visibility = Visibility.Visible;

            var composer = ElementComposition.GetElementVisual(ComposerHeader);
            var messages = ElementComposition.GetElementVisual(Messages);
            var messagesRoot = ElementComposition.GetElementVisual(MessagesRoot);
            var textArea = ElementComposition.GetElementVisual(TextArea);

            var value = show ? 48 : 0;
            var width = Math.Max(0, _textAreaRadius > 0 ? ActualSize.X - 24 : ActualSize.X);

            var rect = textArea.Compositor.CreateRoundedRectangleGeometry();
            rect.CornerRadius = new Vector2(SettingsService.Current.Appearance.CornerRadius);
            rect.Size = new Vector2(width, 192 + 48);
            rect.Offset = new Vector2(0, value);

            textArea.Clip = textArea.Compositor.CreateGeometricClip(rect);

            if (messages.Clip is InsetClip messagesClip)
            {
                messagesClip.LeftInset = -72;
                messagesClip.TopInset = -44 + value;
                messagesClip.BottomInset = int.MinValue;
            }
            else
            {
                messages.Clip = textArea.Compositor.CreateInsetClip(-72, -44 + value, 0, int.MinValue);
            }

            composer.Clip = textArea.Compositor.CreateInsetClip(0, 0, 0, value);

            if (sendout)
            {
                ContentPanel.Margin = new Thickness(0, 0, 0, -48);
            }
            else
            {
                ContentPanel.Margin = new Thickness(0, -48, 0, 0);
            }

            void Completed()
            {
                textArea.Clip = null;
                composer.Clip = null;
                //messages.Clip = null;
                composer.Offset = new Vector3();
                messagesRoot.Properties.InsertVector3("Translation", Vector3.Zero);

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
            }

            var batch = composer.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                Completed();
            };

            var animClip = textArea.Compositor.CreateScalarKeyFrameAnimation();
            animClip.InsertKeyFrame(0, show ? 48 : 0);
            animClip.InsertKeyFrame(1, show ? 0 : 48);
            animClip.Duration = Constants.FastAnimation;

            var animClip2 = textArea.Compositor.CreateScalarKeyFrameAnimation();
            animClip2.InsertKeyFrame(0, show ? -44 : -44 + 48);
            animClip2.InsertKeyFrame(1, show ? -44 + 48 : -44);
            animClip2.Duration = Constants.FastAnimation;

            rect.StartAnimation("Offset.Y", animClip);

            if (!sendout)
            {
                messages.Clip.StartAnimation("TopInset", animClip2);
                messagesRoot.StartAnimation("Translation.Y", animClip);
            }

            composer.Clip.StartAnimation("BottomInset", animClip);
            composer.StartAnimation("Offset.Y", animClip);

            batch.End();
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

            if (next == SideButton.Alias || (prev == SideButton.Alias && ButtonAlias.ActualWidth > 0))
            {
                alias1 = true;
                ButtonAlias.Visibility = Visibility.Visible;
            }
            else if (prev == SideButton.Alias)
            {
                prev = SideButton.None;
            }

            if (next == SideButton.BotMenu || (prev == SideButton.BotMenu && ButtonMore.ActualWidth > 0))
            {
                menu1 = true;
                ButtonMore.Visibility = Visibility.Visible;
            }
            else if (prev == SideButton.BotMenu)
            {
                prev = SideButton.None;
            }

            _sideMenuCollapsed = next;

            if (next == SideButton.None && prev == SideButton.None)
            {
                TextField.IsMenuExpanded = false;
                ButtonMore.Visibility = Visibility.Collapsed;
                ButtonAlias.Visibility = Visibility.Collapsed;

                return;
            }
            else if (next == SideButton.BotMenu)
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
            opacityHide.Duration = Constants.FastAnimation;

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

        private double _textAreaRadius = double.NaN;

        private void UpdateTextAreaRadius(bool force = true)
        {
            var radius = SettingsService.Current.Appearance.CornerRadius;
            if (radius == _textAreaRadius && !force)
            {
                return;
            }

            _textAreaRadius = radius;

            var min = Math.Max(4, radius - 4);
            var max = ComposerHeader.Visibility == Visibility.Visible ? 4 : min;

            ShadowCaster.RadiusX = InlineShadow.RadiusX = radius;
            ShadowCaster.RadiusY = InlineShadow.RadiusY = radius;

            InlineShadow.Height = radius * 2;

            ButtonAttach.CornerRadius = new CornerRadius(_sideMenuCollapsed == SideButton.None ? max : 4, 4, 4, _sideMenuCollapsed == SideButton.None ? min : 4);
            ButtonFeedback.CornerRadius = new CornerRadius(min, 4, 4, min);
            ButtonGift.CornerRadius = new CornerRadius(4, min, min, 4);
            btnVoiceMessage.CornerRadius = new CornerRadius(4, max, min, 4);
            btnSendMessage.CornerRadius = new CornerRadius(4, max, min, 4);
            btnEdit.CornerRadius = new CornerRadius(4, max, min, 4);
            ButtonDelete.CornerRadius = new CornerRadius(4, min, min, 4);
            ButtonManage.CornerRadius = new CornerRadius(min, 4, 4, min);
            ComposerHeaderReference.CornerRadius = new CornerRadius(4, min, 4, 4);

            ComposerHeaderCancel.CornerRadius =
                ButtonMaximize.CornerRadius = new CornerRadius(4, min, 4, 4);
            ButtonEditor.CornerRadius = new CornerRadius(min, 4, 4, 4);
            TextRoot.CornerRadius =
                ChatFooter.CornerRadius =
                ChatRecord.CornerRadius =
                ManagePanel.CornerRadius =
                ButtonAction.CornerRadius = new CornerRadius(radius);

            ListAutocomplete.CornerRadius = InlineCaster.CornerRadius = new CornerRadius(radius, radius, 0, 0);
            ListAutocomplete.Padding = new Thickness(0, 0, 0, radius);

            ListInline?.UpdateCornerRadius(radius);

            ReplyMarkupPanel.CornerRadius = new CornerRadius(0, 0, radius, radius);
            ReplyMarkupPanel.Padding = new Thickness(0, radius, 0, 0);

            if (radius > 0)
            {
                Footer.MaxWidth = InlinePanel.MaxWidth = Separator.MaxWidth = ReplyMarkupPanel.MaxWidth =
                    SettingsService.Current.IsAdaptiveWideEnabled ? 1000 : double.PositiveInfinity;
                Footer.Margin = Separator.Margin = new Thickness(12, 0, 12, 8);
                InlinePanel.Margin = new Thickness(12, 0, 12, -radius);
                InlineShadow.Margin = new Thickness(0, 0, 0, -radius);
                ReplyMarkupPanel.Margin = new Thickness(12, -8 - radius, 12, 8);
            }
            else
            {
                Footer.MaxWidth = InlinePanel.MaxWidth = Separator.MaxWidth = ReplyMarkupPanel.MaxWidth =
                    SettingsService.Current.IsAdaptiveWideEnabled ? 1024 : double.PositiveInfinity;
                Footer.Margin = Separator.Margin = new Thickness();
                InlinePanel.Margin = new Thickness();
                InlineShadow.Margin = new Thickness();
                ReplyMarkupPanel.Margin = new Thickness();
            }

            MessagesStickyPhoto.MaxWidth =
                SettingsService.Current.IsAdaptiveWideEnabled ? 1024 : double.PositiveInfinity;

            var stickyPhoto = ElementComposition.GetElementVisual(MessagesStickyPhoto);
            if (stickyPhoto.Clip is InsetClip stickyPhotoClip)
            {
                stickyPhotoClip.BottomInset = -radius;
            }
            else
            {
                stickyPhoto.Clip = stickyPhoto.Compositor.CreateInsetClip(0, 0, 0, -radius);
            }

            var messages = ElementComposition.GetElementVisual(Messages);
            if (messages.Clip is InsetClip messagesClip)
            {
                messagesClip.LeftInset = -72;
                messagesClip.TopInset = -44;
                messagesClip.BottomInset = int.MinValue;
            }
            else
            {
                messages.Clip = messages.Compositor.CreateInsetClip(-72, -44, 0, int.MinValue);
            }
        }

        public void UpdateAutocomplete(Chat chat, IAutocompleteCollection collection)
        {
            if (collection != null)
            {
                ListAutocomplete.ItemsSource = collection;
                ListAutocomplete.Orientation = collection.Orientation;
                ListAutocomplete.Visibility = Visibility.Visible;
            }
            else
            {
                ListAutocomplete.Visibility = Visibility.Collapsed;
                ListAutocomplete.ItemsSource = null;
            }
        }

        public void UpdateSearchMask(Chat chat, ChatSearchViewModel search)
        {

        }



        public void UpdateUser(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            AccountInfoHeader.UpdateUser(_viewModel.ClientService, chat, user, fullInfo);

            btnSendMessage.SlowModeDelay = 0;
            btnSendMessage.SlowModeDelayExpiresIn = 0;

            if (!secret && !user.RestrictsNewChats)
            {
                ShowArea(fullInfo?.OutgoingPaidMessageStarCount ?? user.PaidMessageStarCount);
            }

            UpdateChatTextPlaceholder(chat);

            UpdateUserStatus(chat, user);

            ButtonFeedback.Visibility = Visibility.Collapsed;
            ButtonGift.Visibility = Visibility.Collapsed;
            ButtonSuggest.Visibility = Visibility.Collapsed;

            if (fullInfo == null)
            {
                ButtonMore.Content = Strings.BotsMenuTitle;

                return;
            }

            if (ViewModel.Search?.SavedMessagesTag != null)
            {
                ShowAction(ViewModel.Search.FilterByTag ? Strings.SavedTagShowOtherMessages : Strings.SavedTagHideOtherMessages, true);
            }
            else if (ViewModel.SavedMessagesTopic != null)
            {
                if (ViewModel.SavedMessagesTopic.Type is SavedMessagesTopicTypeMyNotes)
                {
                    ShowArea(fullInfo.OutgoingPaidMessageStarCount);
                }
                else if (ViewModel.SavedMessagesTopic.Type is SavedMessagesTopicTypeAuthorHidden)
                {
                    ShowAction(Strings.AuthorHiddenDescription, false);
                }
                else if (ViewModel.SavedMessagesTopic.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && ViewModel.ClientService.TryGetChat(savedFromChat.ChatId, out Chat savedChat))
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
                ShowArea(fullInfo.OutgoingPaidMessageStarCount);
            }

            if (fullInfo.BotInfo?.MenuButton != null)
            {
                ButtonMore.Content = fullInfo.BotInfo.MenuButton.Text;

                ViewModel.BotCommands = fullInfo.BotInfo.Commands.Count > 0 ? fullInfo.BotInfo.Commands.Select(x => new UserCommand(user.Id, x)).ToList() : null;
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
            Call.Visibility = fullInfo.CanBeCalled || user.CanBeCalled(ViewModel.ClientService) ? Visibility.Visible : Visibility.Collapsed;
            VideoCall.Visibility = fullInfo.SupportsVideoCalls ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            var options = ViewModel.ClientService.Options;
            if (chat.Id == options.MyId || chat.Id == options.RepliesBotChatId || chat.Id == options.VerificationCodesBotChatId)
            {
                ViewModel.UpdateLastSeen(null as string);
            }
            else if (ViewModel.Type == DialogType.ScheduledMessages)
            {
                ViewModel.UpdateLastSeen(null as string);
            }
            else
            {
                ViewModel.UpdateLastSeen(user);
            }
        }

        public void UpdateUserEmptyState(Chat chat, User user, UserFullInfo fullInfo, CanSendMessageToUserResult result)
        {
            if (result is CanSendMessageToUserResultOk)
            {
                ShowArea(0);
            }
            else if (result is CanSendMessageToUserResultUserHasPaidMessages userHasPaidMessages)
            {
                ShowArea(userHasPaidMessages.OutgoingPaidMessageStarCount);
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
            else if (ViewModel.Type == DialogType.History && chat.ActionBar is ChatActionBarReportAddBlock reportAddBlock && reportAddBlock.AccountInfo != null)
            {
                ShowHideRestrictsNewChats(false, null);
                ShowHideEmptyChat(false, null, null);
            }
            else
            {
                if (chat.Type is not ChatTypeSupergroup)
                {
                    ShowHideRestrictsNewChats(false, null);
                }

                ShowHideEmptyChat(false, null, null);
            }
        }

        public void UpdateSecretChat(Chat chat, SecretChat secretChat)
        {
            if (secretChat.State is SecretChatStateReady)
            {
                ShowArea(0);
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

        public void UpdateBasicGroup(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            AccountInfoHeader.UpdateUser(_viewModel.ClientService, null, null, null);

            if (group.UpgradedToSupergroupId != 0)
            {
                ShowAction(Strings.OpenSupergroup, true);
            }
            else if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
            {
                ShowAction(Strings.DeleteThisGroup, true);
                ViewModel.UpdateLastSeen(Strings.YouLeft);
            }
            else if (group.Status is ChatMemberStatusCreator creator && !creator.IsMember)
            {
                ShowAction(Strings.ChannelJoin, true);
            }
            else
            {
                if (ViewModel.Type == DialogType.Pinned)
                {
                    if (chat.CanPinMessages(_viewModel.ClientService))
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
                    ShowArea(0);
                }

                TextField.PlaceholderText = GetPlaceholder(chat, group, out bool readOnly);

                if (_isTextReadOnly != readOnly)
                {
                    _isTextReadOnly = readOnly;
                    TextField.IsReadOnly = readOnly;
                }

                ViewModel.UpdateLastSeen(Locale.Declension(Strings.R.Members, group.MemberCount));
            }

            ButtonFeedback.Visibility = Visibility.Collapsed;
            ButtonGift.Visibility = Visibility.Collapsed;
            ButtonSuggest.Visibility = Visibility.Collapsed;

            if (fullInfo == null)
            {
                return;
            }

            ViewModel.UpdateLastSeen(Locale.Declension(Strings.R.Members, fullInfo.Members.Count));

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

        public void UpdateSupergroup(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            AccountInfoHeader.UpdateUser(_viewModel.ClientService, null, null, null);

            if (ViewModel.Type == DialogType.EventLog)
            {
                ShowAction(Strings.Settings, true);
                return;
            }

            if (ViewModel.Type == DialogType.Pinned)
            {
                if (chat.CanPinMessages(_viewModel.ClientService))
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
                    ShowArea(0);
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
                    if (ViewModel.Type == DialogType.Thread)
                    {
                        if (group.JoinToSendMessages)
                        {
                            if (group.JoinByRequest)
                            {
                                ShowAction(Strings.ChannelJoinRequest, true);
                            }
                            else
                            {
                                ShowAction(Strings.JoinGroup, true);
                            }
                        }
                        else if (!chat.Permissions.CanSendBasicMessages)
                        {
                            ShowAction(Strings.GlobalSendMessageRestricted, false);
                        }
                        else
                        {
                            ShowArea(group.PaidMessageStarCount);
                        }
                    }
                    else if (group.IsDirectMessagesGroup)
                    {
                        ShowArea(group.PaidMessageStarCount);
                    }
                    else if (group.JoinByRequest)
                    {
                        ShowAction(Strings.ChannelJoinRequest, true);
                    }
                    else
                    {
                        ShowAction(Strings.ChannelJoin, true);
                    }
                }
                else if (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator administrator)
                {
                    if (ViewModel.Type != DialogType.Thread && group.IsDirectMessagesGroup && group.IsAdministeredDirectMessagesGroup)
                    {
                        ShowAction(Strings.ForumReplyToMessagesInTopic, false, true);
                    }
                    else
                    {
                        ShowArea(0);
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
                    else
                    {
                        ShowArea(group.PaidMessageStarCount);
                    }
                }
                else if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
                {
                    if (group.IsDirectMessagesGroup)
                    {
                        ShowArea(group.PaidMessageStarCount);
                    }
                    else
                    {
                        ShowAction(Strings.DeleteChat, true);
                    }
                }
                else if (!chat.Permissions.CanSendBasicMessages && (fullInfo == null || fullInfo.MyBoostCount < fullInfo.UnrestrictBoostCount))
                {
                    if (fullInfo != null && fullInfo.MyBoostCount < fullInfo.UnrestrictBoostCount)
                    {
                        ShowAction(Strings.BoostingBoostToSendMessages, true);
                    }
                    else
                    {
                        ShowAction(Strings.GlobalSendMessageRestricted, false);
                    }
                }
                else if (ViewModel.Type != DialogType.Thread && group.IsDirectMessagesGroup && group.IsAdministeredDirectMessagesGroup)
                {
                    ShowAction(Strings.ForumReplyToMessagesInTopic, false, true);
                }
                else if (ViewModel.ForumTopic is ForumTopic { Info.IsClosed: true })
                {
                    ShowAction(Strings.TopicClosedByAdmin, false);
                }
                else
                {
                    ShowArea(group.IsAdministeredDirectMessagesGroup ? 0 : group.PaidMessageStarCount);
                }
            }

            UpdateChatTextPlaceholder(chat);

            if (ViewModel.Type == DialogType.History)
            {
                if (fullInfo != null)
                {
                    ViewModel.UpdateLastSeen(Locale.Declension(group.IsChannel ? Strings.R.Subscribers : Strings.R.Members, fullInfo.MemberCount));
                }
                else
                {
                    ViewModel.UpdateLastSeen(Locale.Declension(group.IsChannel ? Strings.R.Subscribers : Strings.R.Members, group.MemberCount));
                }
            }
            else if (ViewModel.Type == DialogType.Thread && ViewModel.ForumTopic != null)
            {
                ViewModel.UpdateLastSeen(string.Format(Strings.TopicProfileStatus, chat.Title));
            }
            else
            {
                ViewModel.UpdateLastSeen(null as string);
            }

            ButtonFeedback.Visibility = group.HasDirectMessagesGroup
                ? Visibility.Visible
                : Visibility.Collapsed;

            ButtonSuggest.Visibility = group.IsDirectMessagesGroup
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (group.IsChannel)
            {
                if (fullInfo == null)
                {
                    ButtonGift.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ButtonGift.Visibility = fullInfo.CanSendGift
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }

                return;
            }
            else
            {
                ButtonGift.Visibility = Visibility.Collapsed;
            }

            UpdateComposerHeader(chat, ViewModel.ComposerHeader);
            UpdateChatPermissions(chat);

            if (fullInfo == null)
            {
                return;
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

        public void UpdateSupergroupEmptyState(Chat chat, Supergroup supergroup)
        {
            var show = supergroup.IsDirectMessagesGroup && chat.LastMessage == null;

            if (_restrictsNewChatsCollapsed != show)
            {
                return;
            }

            _restrictsNewChatsCollapsed = !show;
            RestrictsNewChats ??= FindName(nameof(RestrictsNewChats)) as MessageService;

            if (show)
            {
                if (supergroup.PaidMessageStarCount > 0)
                {
                    TextBlockHelper.SetMarkdown(RestrictsNewChatsText, string.Format(Strings.SuggestionLockedStars.ReplaceStar(Icons.Premium), chat.Title, supergroup.PaidMessageStarCount.ToString("N0")));
                    RestrictsNewChatsButton.Visibility = Visibility.Visible;
                }
                else
                {
                    TextBlockHelper.SetMarkdown(RestrictsNewChatsText, string.Format(Strings.SuggestionUnlockedStars, chat.Title));
                    RestrictsNewChatsButton.Visibility = Visibility.Collapsed;
                }

                RestrictsNewChats.Visibility = Visibility.Visible;
                RestrictsNewChatsButtonText.Text = Strings.MessageStarsUnlock;
            }
            else
            {
                RestrictsNewChats.Visibility = Visibility.Collapsed;
            }
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

        public void UpdateDeleteMessages(MessageViewModel message)
        {
            if (_messageIdToSelector.TryGetValue(message.Id, out ChatHistoryViewItem selector))
            {
                var first = message.Delegate.IsSavedMessagesTab ? message.IsLast : message.IsFirst;

                var next = new Vector2(0, first ? 6 : 0);
                var prev = selector.ActualSize;

                var panel = Messages.ItemsPanelRoot as ItemsStackPanel;
                if (panel == null)
                {
                    return;
                }

                var index = Messages.IndexFromContainer(selector);
                AnimateSizeChanged(panel, selector, index, prev, next);
            }
        }

        #endregion

        private void TextField_Sending(object sender, EventArgs e)
        {
            ButtonStickers.Collapse();
            RemoveMessageEffect();

            if (TextField.IsEmpty)
            {
                Messages.ScrollToBottom();
            }
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
                    picture.Size = 36;
                    picture.Margin = new Thickness(-4, -2, 0, -2);

                    var item = new MenuFlyoutProfile();
                    item.Click += handler;
                    item.CommandParameter = messageSender;
                    item.Style = BootStrapper.Current.Resources["SendAsMenuFlyoutItemStyle"] as Style;
                    item.Icon = new FontIcon();
                    item.Tag = picture;

                    if (ViewModel.ClientService.TryGetUser(messageSender.Sender, out User senderUser))
                    {
                        picture.Source = ProfilePictureSource.User(ViewModel.ClientService, senderUser);

                        item.Text = senderUser.FullName();
                        item.Info = Strings.VoipGroupPersonalAccount;
                    }
                    else if (ViewModel.ClientService.TryGetChat(messageSender.Sender, out Chat senderChat))
                    {
                        picture.Source = ProfilePictureSource.Chat(ViewModel.ClientService, senderChat);

                        item.Text = senderChat.Title;

                        if (ViewModel.ClientService.TryGetSupergroup(senderChat, out Supergroup supergroup))
                        {
                            item.Info = Locale.Declension(supergroup.IsChannel ? Strings.R.Subscribers : Strings.R.Members, supergroup.MemberCount);
                        }
                    }

                    flyout.Items.Add(item);
                }
            }

            flyout.ShowAt(ButtonAlias, FlyoutPlacementMode.TopEdgeAlignedLeft);
        }

        private void InlineBotResults_Loaded(object sender, RoutedEventArgs e)
        {
            ListInline.UpdateCornerRadius(SettingsService.Current.Appearance.CornerRadius);
            ListInline.MaxHeight = Math.Min(320, Math.Max(ContentPanel.ActualHeight - 48, 0));

            if (ViewModel?.Chat is Chat chat)
            {
                ListInline.UpdateChatPermissions(chat);
            }
        }

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
            if (ViewModel.Chat.Type is ChatTypePrivate)
            {
                ViewModel.NavigationService.ShowPromo();
            }
            else if (ViewModel.ClientService.TryGetSupergroup(ViewModel.Chat, out Supergroup supergroup))
            {
                ViewModel.NavigationService.ShowPopup(new Views.Stars.Popups.BuyPopup(), BuyStarsArgs.ForChannel(supergroup.PaidMessageStarCount, 0));
            }
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
                RestrictsNewChatsButton.Visibility = Visibility.Visible;
                RestrictsNewChatsButtonText.Text = Strings.MessagePremiumUnlock;
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
            else
            {
                EmptyChatRoot?.Visibility = Visibility.Collapsed;
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
                    ViewModel.SendSticker(fullInfo.BusinessInfo.StartPage.Sticker, SchedulingState.None, false);
                    return;
                }
            }

            if (ViewModel.GreetingSticker != null)
            {
                ViewModel.SendSticker(ViewModel.GreetingSticker, SchedulingState.None, false);
            }
        }

        private void EmptyChatHow_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(BusinessPage));
        }

        private void ClipperOuter_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var width = ForumNavigation.ActualWidth;
            var height = e.NewSize.Height - DateHeaderRelative.ActualHeight;

            ClipperBackground.Margin = new Thickness(-width, -height - 48, -72, 0);
            //UpdateMessagesHeaderPadding();
        }

        private readonly Visual _messagesVisual;
        private readonly CompositionPropertySet _messagesPaddingSet;
        private float _messagesHeaderRootPadding;
        private float _messagesScrollBarPadding;
        private bool _messagesScrollBarPaddingBottom;

        private ChatHistoryViewItem _oldestItemAsHeader;
        private ChatHistoryViewItem _oldestItem;
        private bool? _oldestItemAsHeaderNeeded = false;

        private ChatHistoryViewItem _newestItemAsFooter;
        private ChatHistoryViewItem _newestItem;
        private bool? _newestItemAsFooterNeeded = false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateNewestOldestItemAsFooterHeader(bool? needed, bool? loaded, ref ChatHistoryViewItem item, ref ChatHistoryViewItem headerFooter, Index index, bool clear = true)
        {
            if (needed is true && loaded is true)
            {
                if (item == null && ViewModel.Items.Count > 0)
                {
                    item = Messages.ContainerFromIndex(index.IsFromEnd ? Messages.Items.Count - index.Value : index.Value) as ChatHistoryViewItem;
                }

                headerFooter?.UpdatePadding(index.IsFromEnd ? -1 : 0, index.IsFromEnd ? 0 : -1);

                headerFooter = item;
                headerFooter?.UpdatePadding(index.IsFromEnd ? -1 : _messagesScrollBarPadding, index.IsFromEnd ? _messagesHeaderRootPadding : -1);
            }
            else if (headerFooter != null && clear)
            {
                headerFooter.UpdatePadding(index.IsFromEnd ? -1 : 0, index.IsFromEnd ? 0 : -1);
                headerFooter = null;
            }
        }

        private void UpdateOldestItemAsHeader(bool clear = true)
        {
            UpdateNewestOldestItemAsFooterHeader(_oldestItemAsHeaderNeeded, ViewModel.IsOldestSliceLoaded, ref _oldestItem, ref _oldestItemAsHeader, 0, clear);
        }

        private void UpdateNewestItemAsFooter(bool clear = true)
        {
            UpdateNewestOldestItemAsFooterHeader(_newestItemAsFooterNeeded, ViewModel.IsNewestSliceLoaded, ref _newestItem, ref _newestItemAsFooter, ^1, clear);
        }

        public float AnimatedHeight => ClipperOuter.Visibility == Visibility.Visible
            ? GroupCall.AnimatedHeight
            + JoinRequests.AnimatedHeight
            + TranslateHeader.AnimatedHeight
            + ActionBar.AnimatedHeight
            + ConnectedBot.AnimatedHeight
            + PinnedMessage.AnimatedHeight
            + AccountInfoHeader.AnimatedHeight
            + Sponsored.AnimatedHeight
            + (_forumCollapsed == ForumViewType.Horizontal ? 40 : 0)
            : 0;

        public bool HasMessagesPadding => _messagesHeaderRootPadding > 0;

        private EffectiveViewportChangedEventArgs _headerUnreadViewport;
        private bool _headerUnreadNotReady = true;
        private bool _headerUnreadRetry = false;

        private void HeaderUnread_EffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            var padding = AnimatedHeight;

            if (args.EffectiveViewport.Top < 4 && Math.Truncate(args.EffectiveViewport.Top) >= -padding && args.BringIntoViewDistanceY <= 28 && !Messages.HasBeenScrolled)
            {
                _headerUnreadViewport = args;
                UpdateMessagesHeaderPadding(padding, true, padding);
            }
            else
            {
                _headerUnreadViewport = null;
            }
        }

        public void UpdateMessagesHeaderPadding()
        {
            if (_headerUnreadNotReady)
            {
                _headerUnreadRetry = true;
                return;
            }

            _headerUnreadRetry = false;

            var padding = AnimatedHeight;

            var args = _headerUnreadViewport;
            if (args?.EffectiveViewport.Top < 4 && Math.Truncate(args.EffectiveViewport.Top) >= -padding && args.BringIntoViewDistanceY <= 28 && !Messages.HasBeenScrolled)
            {
                UpdateMessagesHeaderPadding(padding, true, padding);
            }
            else
            {
                UpdateMessagesHeaderPadding(0, false, padding);
            }
        }

        public void UpdateMessagesHeaderPadding(float padding, bool animate, float scrollBar)
        {
            if (ViewModel.IsSavedMessagesTab)
            {
                return;
            }

            var scrollBarChanged = _messagesScrollBarPadding != scrollBar;
            if (scrollBarChanged || _messagesScrollBarPaddingBottom != animate)
            {
                _messagesScrollBarPadding = scrollBar;
                _messagesScrollBarPaddingBottom = animate;
                _messagesPaddingSet.InsertScalar("TopPadding", scrollBar);

                Messages.ScrollingHost.SetVerticalPadding(animate ? 0 : scrollBar, animate ? scrollBar : 0);
            }

            var changed = _messagesHeaderRootPadding != padding;
            if (changed)
            {
                var diff = _messagesHeaderRootPadding - padding;

                _messagesHeaderRootPadding = padding;
                _messagesPaddingSet.InsertScalar("Padding", padding);

                MessagesRoot.Margin = new Thickness(0, padding, 0, -padding);
                MessagesOverlay.Margin = new Thickness(0, padding, 0, 0);
                MessagesStickyPhoto.Margin = new Thickness(0, 0, 0, padding);

                if (animate)
                {
                    var visual = ElementComposition.GetElementVisual(MessagesRoot);
                    visual.Clip = visual.Compositor.CreateInsetClip(0, -padding, 0, int.MinValue);

                    var offset = visual.Compositor.CreateScalarKeyFrameAnimation();
                    offset.InsertKeyFrame(0, diff);
                    offset.InsertKeyFrame(1, 0);
                    offset.Duration = Constants.FastAnimation;

                    //var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
                    //clip.InsertKeyFrame(0, -32);
                    //clip.InsertKeyFrame(1, -32 + padding);
                    //clip.Duration = Constants.FastAnimation;

                    visual.StartAnimation("Translation.Y", offset);
                    //visual.Clip.StartAnimation("BottomInset", clip);
                }
                else
                {
                    ElementCompositionPreview.SetIsTranslationEnabled(MessagesRoot, true);
                    var visual = ElementComposition.GetElementVisual(MessagesRoot);
                    visual.Clip = visual.Compositor.CreateInsetClip(0, -padding, 0, int.MinValue);
                    visual.Properties.InsertVector3("Translation", Vector3.Zero);
                }
            }

            if (_oldestItemAsHeaderNeeded != !animate || scrollBarChanged || (_oldestItemAsHeader == null && _oldestItemAsHeaderNeeded is true))
            {
                _oldestItemAsHeaderNeeded = !animate;
                UpdateOldestItemAsHeader();
            }

            if (_newestItemAsFooterNeeded != animate || changed || (_newestItemAsFooter == null && _newestItemAsFooterNeeded is true))
            {
                _newestItemAsFooterNeeded = animate;
                UpdateNewestItemAsFooter();
            }
        }

        private void ForumNavigation_ItemClick(object sender, ForumViewItemClickEventArgs e)
        {
            if (e.ClickedItem is ForumTopic forumTopic && ViewModel.ClientService.TryGetChat(forumTopic.Info.ChatId, out Chat chat))
            {
                if (forumTopic.Info.ForumTopicId != 0)
                {
                    NavigateToMessageTopic(chat, new MessageTopicForum(forumTopic.Info.ForumTopicId));
                }
                else
                {
                    ViewModel.NavigationService.NavigateToChat(chat, force: false);
                }
            }
            else if (e.ClickedItem is DirectMessagesChatTopic directMessagesChatTopic && ViewModel.ClientService.TryGetChat(directMessagesChatTopic.ChatId, out chat))
            {
                if (directMessagesChatTopic.Id != 0)
                {
                    NavigateToMessageTopic(chat, new MessageTopicDirectMessages(directMessagesChatTopic.Id));
                }
                else
                {
                    ViewModel.NavigationService.NavigateToChat(chat, force: false);
                }
            }
        }

        private void NavigateToMessageTopic(Chat chat, MessageTopic topic)
        {
            ViewModel.NavigationService.NavigateToChat(chat, topic: topic, force: false);
        }

        private void NavigateToMessageTopic(MessageViewModel message)
        {
            ViewModel.NavigationService.NavigateToChat(message.Chat, topic: message.TopicId, force: false);
        }

        private void ForumMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button)
            {
                if (button.IsChecked == false)
                {
                    ViewModel.Settings.UseLeftTabsForForums = false;
                    ShowHideForumTopics(ForumViewType.Horizontal);
                }
                else if (button.IsChecked == true)
                {
                    ViewModel.Settings.UseLeftTabsForForums = true;
                    ShowHideForumTopics(ForumViewType.Vertical);
                }
            }
        }

        private ForumViewType _forumCollapsed = ForumViewType.List;

        private void ShowHideForumTopics(ForumViewType type)
        {
            if (_forumCollapsed == type)
            {
                return;
            }

            ElementCompositionPreview.SetIsTranslationEnabled(ForumNavigationHorizontal, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ForumNavigation, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ClipperOuter, true);
            ElementCompositionPreview.SetIsTranslationEnabled(DateHeaderRelative, true);

            var hori = type == ForumViewType.Horizontal;
            var vert = type == ForumViewType.Vertical;

            var collaped = _forumCollapsed == ForumViewType.List;

            var changingHori = hori || _forumCollapsed == ForumViewType.Horizontal;
            var changingVert = vert || _forumCollapsed == ForumViewType.Vertical;

            ForumNavigationHorizontal.Visibility = changingHori ? Visibility.Visible : Visibility.Collapsed;
            ForumNavigation.Visibility = changingVert ? Visibility.Visible : Visibility.Collapsed;
            ForumModeButton.Visibility = type != ForumViewType.List ? Visibility.Visible : Visibility.Collapsed;
            ForumModeButton.IsChecked = type == ForumViewType.Vertical;

            if (hori)
            {
                _forumViewModel.Delegate = ForumNavigationHorizontal;
                ForumNavigationHorizontal.ViewModel = _forumViewModel;
            }
            else if (vert)
            {
                _forumViewModel.Delegate = ForumNavigation;
                ForumNavigation.ViewModel = _forumViewModel;
            }
            else
            {
                _forumViewModel.Delegate = null;
            }

            ClipperOuterPadding.Height = changingHori ? 40 : 0;

            Header.Children[0].Visibility = type != ForumViewType.Horizontal
                ? Visibility.Visible
                : Visibility.Collapsed;

            var compositor = BootStrapper.Current.Compositor;

            var horizont = ElementComposition.GetElementVisual(ForumNavigationHorizontal);
            var vertical = ElementComposition.GetElementVisual(ForumNavigation);
            var header = ElementComposition.GetElementVisual(ClipperOuter);
            var dateHeader = ElementComposition.GetElementVisual(DateHeaderRelative);

            var textArea = ElementComposition.GetElementVisual(TextArea);
            var buttons = ElementComposition.GetElementVisual(ButtonsRoot);
            var delete = ElementComposition.GetElementVisual(ButtonDelete);
            var forward = ElementComposition.GetElementVisual(ButtonForward);
            var footer = ElementComposition.GetElementVisual(ChatFooter);

            horizont.Clip ??= compositor.CreateInsetClip();
            vertical.Clip ??= compositor.CreateInsetClip();

            var hheight = 40;
            var vwidth = 72;

            void Complete()
            {
                if (_forumCollapsed == ForumViewType.List)
                {
                    _forumViewModel.SetChat(null);
                }

                if (_forumCollapsed != ForumViewType.Vertical)
                {
                    ForumNavigation.ViewModel = null;
                    ForumNavigation.Visibility = Visibility.Collapsed;
                }

                if (_forumCollapsed != ForumViewType.Horizontal)
                {
                    ForumNavigationHorizontal.ViewModel = null;
                    ForumNavigationHorizontal.Visibility = Visibility.Collapsed;
                }

                ClipperOuterPadding.Height = _forumCollapsed == ForumViewType.Horizontal ? 40 : 0;

                buttons.Properties.InsertVector3("Translation", Vector3.Zero);
                header.Properties.InsertVector3("Translation", Vector3.Zero);
                delete.Properties.InsertVector3("Translation", Vector3.Zero);
                forward.Properties.InsertVector3("Translation", Vector3.Zero);
                footer.Clip = null;
                textArea.Clip = null;

                UpdateTextAreaRadius();

                Grid.SetColumn(RootGrid, 1);
            }

            if (IsLoaded is false)
            {
                _forumCollapsed = type;
                UpdateMessagesHeaderPadding();

                Complete();
                return;
            }

            var duration = TimeSpan.FromSeconds(.25);
            //var anim = compositor.CreateScalarKeyFrameAnimation();
            //duration = anim.Duration;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                Complete();
            };

            Grid.SetColumn(RootGrid, vert ? 1 : 0);

            var hoffset = compositor.CreateScalarKeyFrameAnimation();
            hoffset.InsertKeyFrame(hori ? 0 : 1, -hheight);
            hoffset.InsertKeyFrame(hori ? 1 : 0, 0);
            hoffset.Duration = duration;

            var hclip = compositor.CreateScalarKeyFrameAnimation();
            hclip.InsertKeyFrame(hori ? 0 : 1, hheight);
            hclip.InsertKeyFrame(hori ? 1 : 0, 0);
            hclip.Duration = duration;

            var voffset = compositor.CreateScalarKeyFrameAnimation();
            voffset.InsertKeyFrame(vert ? 0 : 1, -vwidth);
            voffset.InsertKeyFrame(vert ? 1 : 0, 0);
            voffset.Duration = duration;

            var vclip = compositor.CreateScalarKeyFrameAnimation();
            vclip.InsertKeyFrame(vert ? 0 : 1, vwidth);
            vclip.InsertKeyFrame(vert ? 1 : 0, 0);
            vclip.Duration = duration;

            var headerOffsetFrom = new Vector3();
            var headerOffsetTo = new Vector3();

            if (vert || _forumCollapsed == ForumViewType.Vertical)
            {
                headerOffsetFrom.X = vert ? -vwidth : vwidth;
            }

            if (hori || _forumCollapsed == ForumViewType.Horizontal)
            {
                headerOffsetFrom.Y = hori ? -hheight : 0;
                headerOffsetTo.Y = hori ? 0 : -hheight;
            }

            var headerOffset = compositor.CreateVector3KeyFrameAnimation();
            //headerOffset.InsertKeyFrame(hori ? 0 : 1, new Vector3(0, -hheight, 0));
            //headerOffset.InsertKeyFrame(hori ? 1 : 0, new Vector3(-vwidth, 0, 0));
            headerOffset.InsertKeyFrame(0, headerOffsetFrom);
            headerOffset.InsertKeyFrame(1, headerOffsetTo);
            headerOffset.Duration = duration;

            var dateHeaderOffset = compositor.CreateScalarKeyFrameAnimation();
            dateHeaderOffset.InsertKeyFrame(0, vert ? 36 : -36);
            dateHeaderOffset.InsertKeyFrame(1, 0);
            dateHeaderOffset.Duration = duration;

            horizont.StartAnimation("Translation.Y", hoffset);
            horizont.Clip.StartAnimation("TopInset", hclip);

            vertical.StartAnimation("Translation.X", voffset);
            vertical.Clip.StartAnimation("LeftInset", vclip);

            header.StartAnimation("Translation", headerOffset);
            dateHeader.StartAnimation("Translation.X", dateHeaderOffset);

            if (changingVert)
            {
                Messages.ForEach(container =>
                {
                    if (container is ChatHistoryViewItem item && item.TypeName != ChatHistoryViewItemType.Outgoing)
                    {
                        ElementCompositionPreview.SetIsTranslationEnabled(container, true);

                        var visual = ElementComposition.GetElementVisual(container);

                        var offset = item.TypeName == ChatHistoryViewItemType.Incoming ? 72 : 36;
                        var translation = compositor.CreateScalarKeyFrameAnimation();
                        translation.InsertKeyFrame(0, vert ? -offset : offset);
                        translation.InsertKeyFrame(1, 0);
                        translation.Duration = duration;

                        visual.StartAnimation("Translation.X", translation);
                    }
                });

                var translation = compositor.CreateScalarKeyFrameAnimation();
                translation.InsertKeyFrame(0, vert ? 72 : -72);
                translation.InsertKeyFrame(1, 0);
                translation.Duration = duration;

                foreach (var element in GetAnimatableVisuals())
                {
                    ElementCompositionPreview.SetIsTranslationEnabled(element, true);

                    var visual = ElementComposition.GetElementVisual(element);
                    visual.StartAnimation("Translation.X", translation);
                }

                ChatFooterAnimateWidth(vert, duration);
                ManagePanelAnimateWidth(vert, duration);
                TextAreaAnimateWidth(vert, duration);
            }

            if (!collaped)
            {
                ForumNavigation.AnimateWidth(hori, duration);
                ForumNavigationHorizontal.AnimateWidth(vert, duration);
            }

            batch.End();

            _forumCollapsed = type;
            UpdateMessagesHeaderPadding();
        }

        private IEnumerable<UIElement> GetAnimatableVisuals()
        {
            foreach (var visual in GroupCall.GetAnimatableVisuals())
            {
                yield return visual;
            }

            //foreach (var visual in JoinRequests.GetAnimatableVisuals())
            //{
            //    yield return visual;
            //}

            foreach (var visual in ActionBar.GetAnimatableVisuals())
            {
                yield return visual;
            }

            foreach (var visual in TranslateHeader.GetAnimatableVisuals())
            {
                yield return visual;
            }

            foreach (var visual in ConnectedBot.GetAnimatableVisuals())
            {
                yield return visual;
            }

            foreach (var visual in PinnedMessage.GetAnimatableVisuals())
            {
                yield return visual;
            }

            //foreach (var visual in AccountInfoHeader.GetAnimatableVisuals())
            //{
            //    yield return visual;
            //}

            foreach (var visual in Sponsored.GetAnimatableVisuals())
            {
                yield return visual;
            }
        }

        private void ChatFooterAnimateWidth(bool collapse, TimeSpan duration)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(ChatFooter, true);

            var radius = new Vector2((float)_textAreaRadius);
            var width = collapse ? ActualSize.X - 24 - 72 : ActualSize.X - 24;
            var margin = (width - (ActualSize.X - 24)) / 2;

            // 12,0,12,8
            Footer.Margin = new Thickness(12 + margin, 0, 12 + margin, 8);

            var visual = ElementComposition.GetElementVisual(ChatFooter);

            if (ApiInfo.CanCreateRectangleClip)
            {
                var clipLeft = visual.Compositor.CreateScalarKeyFrameAnimation();
                clipLeft.InsertKeyFrame(0, collapse ? 0 : 36);
                clipLeft.InsertKeyFrame(1, collapse ? 36 : 0);
                clipLeft.Duration = duration;

                var clipRight = visual.Compositor.CreateScalarKeyFrameAnimation();
                clipRight.InsertKeyFrame(0, collapse ? ActualSize.X - 24 : ActualSize.X - 24 - 36);
                clipRight.InsertKeyFrame(1, collapse ? ActualSize.X - 24 - 36 : ActualSize.X - 24);
                clipRight.Duration = duration;

                visual.Clip = visual.Compositor.CreateRectangleClip(0, 0, ActualSize.X - 24, ChatFooter.ActualSize.Y, radius, radius, radius, radius);
                visual.Clip.StartAnimation("Left", clipLeft);
                visual.Clip.StartAnimation("Right", clipRight);
            }
            else
            {
                // TODO: alternative animation
            }

            var translation = visual.Compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, collapse ? -36 : 36);
            translation.InsertKeyFrame(1, 0);
            translation.Duration = duration;

            visual.StartAnimation("Translation.X", translation);
        }

        private void ManagePanelAnimateWidth(bool collapse, TimeSpan duration)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(ManagePanel, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ButtonDelete, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ButtonForward, true);

            var radius = new Vector2((float)_textAreaRadius);
            var width = collapse ? ActualSize.X - 24 - 72 : ActualSize.X - 24;
            var margin = (width - (ActualSize.X - 24)) / 2;

            // 12,0,12,8
            Footer.Margin = new Thickness(12 + 0, 0, 12 + margin + margin, 8);

            var delete = ElementComposition.GetElementVisual(ButtonDelete);
            var forward = ElementComposition.GetElementVisual(ButtonForward);
            var visual = ElementComposition.GetElementVisual(ManagePanel);

            if (ApiInfo.CanCreateRectangleClip)
            {
                var clipRight = visual.Compositor.CreateScalarKeyFrameAnimation();
                clipRight.InsertKeyFrame(0, collapse ? ActualSize.X - 24 : ActualSize.X - 24 - 72);
                clipRight.InsertKeyFrame(1, collapse ? ActualSize.X - 24 - 72 : ActualSize.X - 24);
                clipRight.Duration = duration;

                visual.Clip = visual.Compositor.CreateRectangleClip(0, 0, ActualSize.X - 24, ManagePanel.ActualSize.Y, radius, radius, radius, radius);
                visual.Clip.StartAnimation("Right", clipRight);
            }
            else
            {
                // TODO: alternative animation
            }

            var translation = visual.Compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, collapse ? -72 : 72);
            translation.InsertKeyFrame(1, 0);
            translation.Duration = duration;

            var button = visual.Compositor.CreateScalarKeyFrameAnimation();
            button.InsertKeyFrame(0, collapse ? 0 : -72);
            button.InsertKeyFrame(1, collapse ? -72 : 0);
            button.Duration = duration;

            delete.StartAnimation("Translation.X", button);
            forward.StartAnimation("Translation.X", button);
            visual.StartAnimation("Translation.X", translation);
        }

        private void TextAreaAnimateWidth(bool collapse, TimeSpan duration)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(TextArea, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ButtonsRoot, true);
            //ElementCompositionPreview.SetIsTranslationEnabled(ButtonDelete, true);
            //ElementCompositionPreview.SetIsTranslationEnabled(ButtonForward, true);

            var radius = new Vector2((float)_textAreaRadius);
            var width = collapse ? ActualSize.X - 24 - 72 : ActualSize.X - 24;
            var margin = (width - (ActualSize.X - 24)) / 2;

            // 12,0,12,8
            Footer.Margin = new Thickness(12 + 0, 0, 12 + margin + margin, 8);

            var delete = ElementComposition.GetElementVisual(ButtonsRoot);
            var visual = ElementComposition.GetElementVisual(TextArea);

            var translation = visual.Compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, collapse ? -72 : 72);
            translation.InsertKeyFrame(1, 0);
            translation.Duration = duration;

            var button = visual.Compositor.CreateScalarKeyFrameAnimation();
            button.InsertKeyFrame(0, collapse ? 0 : -72);
            button.InsertKeyFrame(1, collapse ? -72 : 0);
            button.Duration = duration;

            if (ApiInfo.CanCreateRectangleClip)
            {
                var clipRight = visual.Compositor.CreateScalarKeyFrameAnimation();
                clipRight.InsertKeyFrame(0, collapse ? ActualSize.X - 24 : ActualSize.X - 24 - 72);
                clipRight.InsertKeyFrame(1, collapse ? ActualSize.X - 24 - 72 : ActualSize.X - 24);
                clipRight.Duration = duration;

                visual.Clip = visual.Compositor.CreateRectangleClip(0, 0, ActualSize.X - 24, TextArea.ActualSize.Y, radius, radius, radius, radius);
                visual.Clip.StartAnimation("Right", clipRight);
            }
            else
            {
                // TODO: alternative animation
            }

            delete.StartAnimation("Translation.X", button);
            visual.StartAnimation("Translation.X", translation);
        }

        private void ForumTopic_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _forumTopicHeader.CenterPoint = new Vector3(ForumTopicHeader.ActualSize / 2, 0);
        }

        private void ButtonFeedback_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.ClientService.TryGetSupergroupFull(ViewModel.Chat, out SupergroupFullInfo fullInfo))
            {
                ViewModel.NavigationService.NavigateToChat(fullInfo.DirectMessagesChatId);
            }
        }

        private void ButtonGift_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.GiftPremium();
        }

        private void Suggest_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SuggestPost();
        }

        private void ContinueLastThread_Click(object sender, RoutedEventArgs e)
        {
            var items = _forumViewModel.Items as TopicListViewModel.ForumTopicsCollection;
            var topic = items?.FirstOrDefault(x => x.Info.ForumTopicId != 0);
            if (topic != null)
            {
                NavigateToMessageTopic(_forumViewModel.Chat, new MessageTopicForum(topic.Info.ForumTopicId));
            }
        }

        private void NewThread_Click(object sender, RoutedEventArgs e)
        {
            TextField.Focus(FocusState.Keyboard);
        }

        private void StickySummaryAbove_Click(object sender, RoutedEventArgs e)
        {
            _stickySummaryAboveMessage?.Delegate.SummarizeMessage(_stickySummaryAboveMessage);
        }

        private void StickyPhotoAbove_Click(object sender, RoutedEventArgs e)
        {
            _stickyPhotoAboveMessage?.Delegate.OpenSender(_stickyPhotoAboveMessage);
        }

        private void StickyPhotoBelow_Click(object sender, RoutedEventArgs e)
        {
            _stickyPhotoBelowMessage?.Delegate.OpenSender(_stickyPhotoBelowMessage);
        }

        private void StickyPhotoAbove_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (_stickyPhotoAboveMessage != null)
            {
                ProfilePhoto_ContextRequested(_stickyPhotoAboveMessage, StickyPhotoAbove, args);
            }
        }

        private void StickyPhotoBelow_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (_stickyPhotoBelowMessage != null)
            {
                ProfilePhoto_ContextRequested(_stickyPhotoBelowMessage, StickyPhotoBelow, args);
            }
        }

        private bool _inlinePanelCollapsed = true;
        private int _inlinePanelTracker;

        private void InlinePanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var show = e.NewSize.Height > _textAreaRadius;
            var tracker = ++_inlinePanelTracker;

            var prev = e.PreviousSize.ToVector2();
            var next = e.NewSize.ToVector2();

            ElementCompositionPreview.SetIsTranslationEnabled(InlinePanel, true);
            ElementCompositionPreview.SetIsTranslationEnabled(InlineShadow, true);

            var visual = ElementComposition.GetElementVisual(InlinePanel);
            var caster = ElementComposition.GetElementVisual(InlineShadow);
            var grid = ElementComposition.GetElementVisual(InlineGrid);
            var background = ElementComposition.GetElementVisual(InlineBackground);

            var rectangle = visual.Compositor.CreateRoundedRectangleGeometry();
            rectangle.CornerRadius = new Vector2((float)_textAreaRadius);

            grid.Clip = visual.Compositor.CreateGeometricClip(rectangle);

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            if (_inlinePanelCollapsed == show)
            {
                InlineBackground.Visibility = Visibility.Visible;

                batch.Completed += (s, args) =>
                {
                    if (_inlinePanelTracker != tracker)
                    {
                        return;
                    }

                    // Theme shadow for the inline panel must be added/removed manually when "visibility" changes
                    // Otherwise it's going to generate GPU artifacts on text box size changes.
                    if (show)
                    {
                        InlineShadow.Shadow = _shadow;
                        InlineShadow.Translation = new Vector3(0, 0, 64);

                        InlineCaster.Shadow = _shadow;
                        InlineCaster.Translation = new Vector3(0, 0, Constants.BubbleElevation);

                        InlineBackground.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        InlineShadow.Shadow = null;
                        InlineShadow.Translation = Vector3.Zero;

                        InlineCaster.Shadow = null;
                        InlineCaster.Translation = Vector3.Zero;

                        InlineBackground.Visibility = Visibility.Collapsed;
                    }
                };
            }

            _inlinePanelCollapsed = !show;

            var translation = visual.Compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, next.Y - prev.Y);
            translation.InsertKeyFrame(1, 0);
            translation.Duration = Constants.FastAnimation;

            var translation2 = visual.Compositor.CreateScalarKeyFrameAnimation();
            translation2.InsertKeyFrame(0, prev.Y - next.Y);
            translation2.InsertKeyFrame(1, 0);
            translation2.Duration = Constants.FastAnimation;

            var scale = visual.Compositor.CreateScalarKeyFrameAnimation();
            scale.InsertKeyFrame(0, prev.Y / next.Y);
            scale.InsertKeyFrame(1, 1);
            scale.Duration = Constants.FastAnimation;

            var size = visual.Compositor.CreateVector2KeyFrameAnimation();
            size.InsertKeyFrame(0, new Vector2(prev.X, prev.Y + rectangle.CornerRadius.Y));
            size.InsertKeyFrame(1, new Vector2(next.X, next.Y + rectangle.CornerRadius.Y));
            size.Duration = Constants.FastAnimation;

            visual.StartAnimation("Translation.Y", translation);
            caster.StartAnimation("Translation.Y", translation2);
            background.StartAnimation("Scale.Y", scale);
            rectangle.StartAnimation("Size", size);

            batch.End();
        }

        private void TextField_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ButtonEditor.Visibility = e.NewSize.Height >= 84
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (SettingsService.Current.Diagnostics.RichMessagesDebug)
            {
                ButtonMaximize.Visibility = e.NewSize.Height >= 84
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void ButtonEditor_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenTextEditor();
        }

        private void ButtonMaximize_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenRichTextEditor();
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
            var view = _owner.GetParent<IAutomationNameProvider>();
            if (view != null)
            {
                return view.GetAutomationName();
            }

            return base.GetNameCore();
        }
    }

    public interface IAutomationNameProvider
    {
        string GetAutomationName();
    }
}
