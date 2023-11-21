//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
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
using Telegram.Controls.Gallery;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Controls.Stories;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Stories;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Point = Windows.Foundation.Point;
using VirtualKey = Windows.System.VirtualKey;

namespace Telegram.Views
{
    public sealed partial class ChatView : UserControl, INavigablePage, ISearchablePage, IDialogDelegate, IActivablePage
    {
        private DialogViewModel _viewModel;
        public DialogViewModel ViewModel => _viewModel ??= DataContext as DialogViewModel;

        private readonly Func<IDialogDelegate, int, DialogViewModel> _getViewModel;
        private readonly Action<string> _setTitle;

        private readonly bool _myPeople;

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

        public ChatView(Func<IDialogDelegate, int, DialogViewModel> getViewModel, Action<string> setTitle)
        {
            InitializeComponent();

            _getViewModel = getViewModel;
            _setTitle = setTitle;

            // TODO: this might need to change depending on context
            _autocompleteHandler = new AnimatedListHandler(ListAutocomplete, AnimatedListType.Stickers);

            _autocompleteZoomer = new ZoomableListHandler(ListAutocomplete);
            _autocompleteZoomer.Opening = _autocompleteHandler.UnloadVisibleItems;
            _autocompleteZoomer.Closing = _autocompleteHandler.ThrottleVisibleItems;
            _autocompleteZoomer.DownloadFile = fileId => ViewModel.ClientService.DownloadFile(fileId, 32);
            _autocompleteZoomer.SessionId = () => ViewModel.ClientService.SessionId;

            _loadedThemeTask = new TaskCompletionSource<bool>();

            _typeToItemHashSetMapping.Add("UserMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("FriendMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ServiceMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ServiceMessagePhotoTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ServiceMessageBackgroundTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ServiceMessageUnreadTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ServiceMessageGiftTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("EmptyMessageTemplate", new HashSet<SelectorItem>());

            _typeToTemplateMapping.Add("UserMessageTemplate", Resources["UserMessageTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("FriendMessageTemplate", Resources["FriendMessageTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ServiceMessageTemplate", Resources["ServiceMessageTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ServiceMessagePhotoTemplate", Resources["ServiceMessagePhotoTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ServiceMessageBackgroundTemplate", Resources["ServiceMessageBackgroundTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ServiceMessageUnreadTemplate", Resources["ServiceMessageUnreadTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ServiceMessageGiftTemplate", Resources["ServiceMessageGiftTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("EmptyMessageTemplate", Resources["EmptyMessageTemplate"] as DataTemplate);

            _focusState = new DebouncedProperty<FocusState>(100, FocusText, CanFocusText);

            if (WindowContext.Current.ContactPanel != null)
            {
                _myPeople = true;
                WindowContext.Current.ContactPanel.LaunchFullAppRequested += ContactPanel_LaunchFullAppRequested;
                WatchDog.TrackEvent("ContactPanel");
            }

            Messages.Delegate = this;
            Messages.ItemsSource = _messages;
            Messages.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);

            InitializeAutomation();
            InitializeStickers();

            //ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();
            ElementCompositionPreview.SetIsTranslationEnabled(ButtonMore, true);
            ElementCompositionPreview.SetIsTranslationEnabled(TextFieldPanel, true);
            ElementCompositionPreview.SetIsTranslationEnabled(btnAttach, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ListAutocomplete, true);

            _rootVisual = ElementCompositionPreview.GetElementVisual(TextArea);

            if (DateHeaderPanel != null)
            {
                _dateHeaderTimer = new DispatcherTimer();
                _dateHeaderTimer.Interval = TimeSpan.FromMilliseconds(2000);
                _dateHeaderTimer.Tick += (s, args) =>
                {
                    _dateHeaderTimer.Stop();
                    ShowHideDateHeader(false, true);
                };

                _dateHeaderPanel = ElementCompositionPreview.GetElementVisual(DateHeaderRelative);
                _dateHeader = ElementCompositionPreview.GetElementVisual(DateHeader);

                _dateHeaderPanel.Clip = Window.Current.Compositor.CreateInsetClip();
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

            _textShadowVisual = DropShadowEx.Attach(Separator);
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
            GroupCall.InitializeParent(ClipperOuter, ViewModel.ClientService);
            JoinRequests.InitializeParent(ClipperJoinRequests, ViewModel.ClientService);
            ActionBar.InitializeParent(ClipperActionBar);
            PinnedMessage.InitializeParent(Clipper);
        }

        private void InitializeAutomation()
        {
            Title.RegisterPropertyChangedCallback(TextBlock.TextProperty, OnHeaderContentChanged);
            Subtitle.RegisterPropertyChangedCallback(TextBlock.TextProperty, OnHeaderContentChanged);
            ChatActionLabel.RegisterPropertyChangedCallback(TextBlock.TextProperty, OnHeaderContentChanged);
        }

        private void OnHeaderContentChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Title == null || Subtitle == null || ChatActionLabel == null)
            {
                return;
            }

            var result = Title.Text.TrimEnd('.', ',');
            if (ChatActionLabel.Text.Length > 0)
            {
                result += ", " + ChatActionLabel.Text;
            }
            else if (Subtitle.Text.Length > 0)
            {
                result += ", " + Subtitle.Text;
            }

            AutomationProperties.SetName(Profile, result);
        }

        private void InitializeStickers()
        {
            StickersPanel.EmojiClick = Emojis_ItemClick;

            StickersPanel.StickerClick = Stickers_ItemClick;
            StickersPanel.StickerContextRequested += Sticker_ContextRequested;
            StickersPanel.ChoosingSticker += Stickers_ChoosingItem;
            StickersPanel.SettingsClick += StickersPanel_SettingsClick;

            StickersPanel.AnimationClick = Animations_ItemClick;
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

        private void ContactPanel_LaunchFullAppRequested(Windows.ApplicationModel.Contacts.ContactPanel sender, Windows.ApplicationModel.Contacts.ContactPanelLaunchFullAppRequestedEventArgs args)
        {
            sender.ClosePanel();
            args.Handled = true;

            this.BeginOnUIThread(async () =>
            {
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                if (chat.Type is ChatTypePrivate privata)
                {
                    var options = new Windows.System.LauncherOptions();
                    options.TargetApplicationPackageFamilyName = Package.Current.Id.FamilyName;

                    await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-contact-profile://meh?ContactRemoteIds=u" + privata.UserId), options);
                }
            });
        }

        public void OnNavigatingFrom(Type sourcePageType)
        {
            var unallowed = sourcePageType != typeof(ChatPage)
                && sourcePageType != typeof(ChatEventLogPage)
                && sourcePageType != typeof(ChatPinnedPage)
                && sourcePageType != typeof(ChatScheduledPage)
                && sourcePageType != typeof(ChatThreadPage);

            if (unallowed && Theme.Current.Update(ActualTheme, null, null))
            {
                var background = ViewModel.ClientService.GetSelectedBackground(ActualTheme == ElementTheme.Dark);

                _backgroundControl ??= FindBackgroundControl();
                _backgroundControl?.Update(background, ActualTheme == ElementTheme.Dark);
            }
        }

        private ChatBackgroundControl FindBackgroundControl()
        {
            if (BackgroundControl != null)
            {
                return BackgroundControl;
            }

            var masterDetailPanel = this.Ancestors<MasterDetailPanel>().FirstOrDefault();
            if (masterDetailPanel != null)
            {
                return masterDetailPanel.Descendants<ChatBackgroundControl>().FirstOrDefault();
            }

            return null;
        }

        private MessageCollection _cleanup;

        private void Cleanup(ref MessageCollection cache)
        {
            if (cache != null)
            {
                //foreach (var message in cache)
                //{
                //    message.Cleanup();
                //}

                cache.Clear();
                cache = null;
            }
        }

        public void Deactivate(bool navigation)
        {
            Cleanup(ref _cleanup);

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

                _cleanup = ViewModel.Items;

                if (navigation is false)
                {
                    Cleanup(ref _cleanup);

                    _albumIdToSelector.Clear();
                    _messageIdToSelector.Clear();
                    _messageIdToMessageIds.Clear();
                }
            }

            ButtonStickers.Collapse();
        }

        private readonly SynchronizedCollection<MessageViewModel> _messages = new();

        public class SynchronizedCollection<T> : MvxObservableCollection<T>
        {
            private System.Collections.ObjectModel.ObservableCollection<T> _source;

            public void UpdateSource(System.Collections.ObjectModel.ObservableCollection<T> source)
            {
                if (_source != null)
                {
                    _source.CollectionChanged -= OnCollectionChanged;
                }

                _source = source;

                if (_source != null)
                {
                    _source.CollectionChanged += OnCollectionChanged;
                    ReplaceWith(_source);
                }
                else
                {
                    Clear();
                }
            }

            private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        InsertRange(e.NewStartingIndex, e.NewItems);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        ReplaceWith(_source);
                        break;
                }
            }
        }

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

            Messages.ItemsSource ??= _messages;

            Bindings.Update();
            Cleanup(ref _cleanup);
        }

        public void Activate(int sessionId)
        {
            Logger.Info($"ItemsPanelRoot.Children.Count: {Messages.ItemsStack?.Children.Count}");
            Logger.Info($"Items.Count: {Messages.Items.Count}");

            DataContext = _viewModel = _getViewModel(this, sessionId);

            _updateThemeTask = new TaskCompletionSource<bool>();
            ViewModel.MessageSliceLoaded += OnMessageSliceLoaded;
            ViewModel.TextField = TextField;
            ViewModel.HistoryField = Messages;
            ViewModel.Sticker_Click = Stickers_ItemClick;

            ViewModel.SetText(null, false);

            Messages.SetScrollingMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);

            CheckMessageBoxEmpty();

            SearchMask?.Update(ViewModel.Search);

            ViewModel.PropertyChanged += OnPropertyChanged;
            ViewModel.Items.AttachChanged = OnAttachChanged;
            ViewModel.Items.CollectionChanged += OnCollectionChanged;

            //Playback.Update(ViewModel.ClientService, ViewModel.PlaybackService, ViewModel.NavigationService);

            UpdateTextAreaRadius();

            TextField.IsReplaceEmojiEnabled = ViewModel.Settings.IsReplaceEmojiEnabled;

            if (_useSystemSpellChecker != SettingsService.Current.UseSystemSpellChecker)
            {
                _useSystemSpellChecker = SettingsService.Current.UseSystemSpellChecker;
                TextField.IsTextPredictionEnabled = _useSystemSpellChecker;
                TextField.IsSpellCheckEnabled = _useSystemSpellChecker;
            }

            _focusState.Set(FocusState.Programmatic);

            StickersPanel.MaxWidth = SettingsService.Current.IsAdaptiveWideEnabled ? 1024 : double.PositiveInfinity;

            Options.Visibility = ViewModel.Type == DialogType.History
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
        }

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
            var panel = Messages.ItemsStack;
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

                var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                var anim = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                anim.InsertKeyFrame(0, new Vector3(0, -(float)owner.DesiredSize.Height, 0));
                anim.InsertKeyFrame(1, new Vector3());
                //anim.Duration = TimeSpan.FromSeconds(1);

                for (int i = panel.FirstCacheIndex; i < args.OldStartingIndex; i++)
                {
                    var container = Messages.ContainerFromIndex(i) as SelectorItem;
                    var child = VisualTreeHelper.GetChild(container, 0) as UIElement;

                    var visual = ElementCompositionPreview.GetElementVisual(child);
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

                await Messages.ItemsStack.UpdateLayoutAsync();

                if (message.IsOutgoing && message.SendingState is MessageSendingStatePending && !Messages.IsBottomReached)
                {
                    await Messages.ScrollToItem(message, VerticalAlignment.Bottom, new MessageBubbleHighlightOptions(false, false));
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

                var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                var diff = owner.ActualSize.Y;

                if (animateSendout)
                {
                    var messages = ElementCompositionPreview.GetElementVisual(Messages);

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

                var anim = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
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

                    var visual = ElementCompositionPreview.GetElementVisual(child);

                    if (i == args.NewStartingIndex && animateSendout)
                    {
                        var bubble = owner.Descendants<MessageBubble>().FirstOrDefault();
                        var reply = message.ReplyToState != MessageReplyToState.Hidden && message.ReplyTo != null;
                        var more = ButtonMore.Visibility == Visibility.Visible ? 40 : 0;

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

                        anim = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
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
        }

        private void Segments_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null || sender is not ActiveStoriesSegments segments)
            {
                return;
            }

            if (segments.HasActiveStories)
            {
                segments.Open(ViewModel.NavigationService, ViewModel.ClientService, chat, 36, story =>
                {
                    var transform = Segments.TransformToVisual(Window.Current.Content);
                    var point = transform.TransformPoint(new Point());

                    return new Rect(point.X + 4, point.Y + 4, 28, 28);
                });
            }
            else
            {
                GalleryWindow.ShowAsync(ViewModel, ViewModel.StorageService, chat, () => Photo);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _loadedThemeTask?.TrySetResult(true);

            //Bindings.StopTracking();
            //Bindings.Update();

            Window.Current.Activated += Window_Activated;
            Window.Current.VisibilityChanged += Window_VisibilityChanged;

            Window.Current.CoreWindow.CharacterReceived += OnCharacterReceived;
            WindowContext.Current.InputListener.KeyDown += OnAcceleratorKeyActivated;

            ViewVisibleMessages();



            _focusState.Set(FocusState.Programmatic);

            if (WindowContext.Current.ContactPanel != null)
            {
                Header.Visibility = Visibility.Collapsed;
                FindName(nameof(BackgroundControl));
                BackgroundControl.Update(ViewModel.ClientService, ViewModel.Aggregator);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            //Bindings.StopTracking();

            UnloadVisibleMessages();

            Window.Current.Activated -= Window_Activated;
            Window.Current.VisibilityChanged -= Window_VisibilityChanged;

            Window.Current.CoreWindow.CharacterReceived -= OnCharacterReceived;
            WindowContext.Current.InputListener.KeyDown -= OnAcceleratorKeyActivated;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            var mode = Window.Current.CoreWindow.ActivationMode;
            if (mode == CoreWindowActivationMode.ActivatedInForeground)
            {
                ViewVisibleMessages(true);

                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                var element = FocusManager.GetFocusedElement();
                if (element is not TextBox and not RichEditBox)
                {
                    _focusState.Set(FocusState.Programmatic);
                }
            }
            else if (mode == CoreWindowActivationMode.Deactivated)
            {
                ViewModel.SaveDraft();
            }
        }

        private void Window_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            if (e.Visible)
            {
                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                _focusState.Set(FocusState.Programmatic);
            }
        }

        public void Search()
        {
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
                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                _focusState.Set(FocusState.Keyboard);

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
                    if (focused is MessageSelector selector && MessageDelete_Loaded(selector.Message))
                    {
                        ViewModel.DeleteMessage(selector.Message);
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
                    if (focused is MessageSelector selector && MessageCopy_Loaded(selector.Message))
                    {
                        ViewModel.CopyMessage(selector.Message);
                        args.Handled = true;
                    }
                    else if (focused is RichTextBlock textBlock)
                    {
                        var message = textBlock.Ancestors<MessageSelector>().FirstOrDefault()?.Message;
                        if (message != null)
                        {
                            var selectionStart = textBlock.SelectionStart.Shift();
                            var selectionEnd = textBlock.SelectionEnd.Shift();

                            if (selectionEnd - selectionStart > 0)
                            {
                                var caption = message.GetCaption();
                                if (caption != null && caption.Text.Length >= selectionEnd && selectionEnd > 0 && selectionStart >= 0)
                                {
                                    var quote = new MessageQuote
                                    {
                                        Message = message,
                                        Quote = caption.Substring(selectionStart, selectionEnd - selectionStart)
                                    };

                                    if (MessageCopy_Loaded(quote))
                                    {
                                        ViewModel.CopyMessage(quote);
                                        args.Handled = true;
                                    }
                                }
                            }
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
            else if (args.VirtualKey == VirtualKey.O && args.RepeatCount == 1 && args.OnlyControl)
            {
                ViewModel.SendDocument();
                args.Handled = true;
            }
            else if (args.VirtualKey == VirtualKey.PageUp && args.OnlyKey && TextField.Document.Selection.StartPosition == 0 && ViewModel.Autocomplete == null)
            {
                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                var focused = FocusManager.GetFocusedElement();
                if (focused is Selector or SelectorItem or MessageSelector or Microsoft.UI.Xaml.Controls.ItemsRepeater or ChatCell or PlaybackSlider)
                {
                    return;
                }

                if (args.VirtualKey == VirtualKey.Up && (focused is TextBox || focused is RichEditBox))
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
                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                var focused = FocusManager.GetFocusedElement();
                if (focused is Selector or SelectorItem or MessageSelector or Microsoft.UI.Xaml.Controls.ItemsRepeater or ChatCell or PlaybackSlider)
                {
                    return;
                }

                if (args.VirtualKey == VirtualKey.Down && (focused is TextBox || focused is RichEditBox))
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
                    CollapseMarkup(false);
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

            if (args.Handled)
            {
                Focus(FocusState.Programmatic);
                _focusState.Set(FocusState.Programmatic);
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
                        btnSendMessage.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        elementHide = btnSendMessage;
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

                    elementShow = btnSendMessage;
                }
            }
            else if (editing != _oldEditing)
            {
                if (editing)
                {
                    if (_oldEmpty)
                    {
                        elementHide = ButtonRecord;
                        btnSendMessage.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        elementHide = btnSendMessage;
                        ButtonRecord.Visibility = Visibility.Collapsed;
                    }

                    elementShow = btnEdit;
                }
                else
                {
                    if (empty)
                    {
                        elementShow = ButtonRecord;
                        btnSendMessage.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        elementShow = btnSendMessage;
                        ButtonRecord.Visibility = Visibility.Collapsed;
                    }

                    elementHide = btnEdit;
                }
            }
            //else
            //{
            //    btnSendMessage.Visibility = empty || editing ? Visibility.Collapsed : Visibility.Visible;
            //    btnEdit.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
            //    ButtonRecord.Visibility = empty && !editing ? Visibility.Visible : Visibility.Collapsed;
            //}

            if (elementHide == null || elementShow == null)
            {
                return;
            }

            //elementShow.Visibility = Visibility.Visible;
            //elementHide.Visibility = Visibility.Collapsed;

            var visualHide = ElementCompositionPreview.GetElementVisual(elementHide);
            var visualShow = ElementCompositionPreview.GetElementVisual(elementShow);

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
                var scheduled = ElementCompositionPreview.GetElementVisual(btnScheduled);
                var commands = ElementCompositionPreview.GetElementVisual(btnCommands);
                var markup = ElementCompositionPreview.GetElementVisual(btnMarkup);

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

            var text = viewModel.GetText(TextGetOptions.None);
            var embedded = viewModel.ComposerHeader;

            TryGetWebPagePreview(viewModel.ClientService, viewModel.Chat, text, result =>
            {
                this.BeginOnUIThread(() =>
                {
                    if (!string.Equals(text, viewModel.GetText(TextGetOptions.None)))
                    {
                        return;
                    }

                    if (result is WebPage webPage)
                    {
                        if (embedded != null && embedded.WebPageDisabled && string.Equals(embedded.WebPageUrl, webPage.Url, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        viewModel.ComposerHeader = new MessageComposerHeader(viewModel.ClientService)
                        {
                            EditingMessage = embedded?.EditingMessage,
                            ReplyToMessage = embedded?.ReplyToMessage,
                            ReplyToQuote = embedded?.ReplyToQuote,
                            LinkPreviewOptions = embedded?.LinkPreviewOptions,
                            WebPagePreview = webPage,
                            WebPageUrl = webPage.Url
                        };
                    }
                    else if (embedded != null)
                    {
                        if (embedded.IsEmpty)
                        {
                            viewModel.ComposerHeader = null;
                        }
                        else if (embedded.WebPagePreview != null)
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
                var response = Client.Execute(new GetTextEntities(text));
                if (response is TextEntities entities)
                {
                    var urls = string.Empty;

                    foreach (var entity in entities.Entities)
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

                    clientService.Send(new GetWebPagePreview(new FormattedText(urls, Array.Empty<TextEntity>()), null), result);
                }
                else
                {
                    result(null);
                }
            }
            else
            {
                clientService.Send(new GetWebPagePreview(new FormattedText(text.Format(), Array.Empty<TextEntity>()), null), result);
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
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (ViewModel.ClientService.IsRepliesChat(chat))
            {
                return;
            }

            if (ViewModel.Topic != null)
            {
                ViewModel.NavigationService.Navigate(typeof(ProfilePage), new ChatNavigationArgs(chat.Id, ViewModel.ThreadId), infoOverride: new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
            }
            else
            {
                ViewModel.NavigationService.Navigate(typeof(ProfilePage), chat.Id, infoOverride: new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
            }
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

            if (header == null || header.EditingMessage == null || (header.IsEmpty && header.WebPageDisabled))
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

                if (ViewModel.IsPremium
                    && ViewModel.ClientService.Options.GiftPremiumFromAttachmentMenu
                    && ViewModel.ClientService.TryGetUserFull(chat, out UserFullInfo fullInfo) && fullInfo.PremiumGiftOptions.Count > 0)
                {
                    flyout.CreateFlyoutItem(ViewModel.GiftPremium, Strings.GiftPremium, Icons.GiftPremium);
                }

                var bots = ViewModel.ClientService.GetBotsForChat(chat.Id);
                if (bots.Count > 0)
                {
                    flyout.CreateFlyoutSeparator();

                    foreach (var bot in bots)
                    {
                        var item = flyout.CreateFlyoutItem(ViewModel.OpenMiniApp, bot, bot.Name, Icons.Bot);
                        item.ContextRequested += AttachmentMenuBot_ContextRequested;
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

            var flyout = new MenuFlyout();
            var bot = item.CommandParameter as AttachmentMenuBot;

            flyout.CreateFlyoutItem(ViewModel.RemoveMiniApp, bot, Strings.BotWebViewDeleteBot, Icons.Delete);
            args.ShowAt(flyout, item);
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
            e.AcceptedOperation = DataPackageOperation.Copy;
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
                CollapseMarkup(false);
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
                CollapseMarkup(true);
            }
            else
            {
                ShowMarkup();
            }
        }

        private void CollapseMarkup(bool keyboard)
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

        public void ShowMarkup()
        {
            _textShadowVisual.IsVisible = true;
            ReplyMarkupPanel.Visibility = Visibility.Visible;

            ButtonMarkup.Glyph = Icons.ChevronDown;
            Automation.SetToolTip(ButtonMarkup, Strings.AccDescrShowKeyboard);

            Focus(FocusState.Programmatic);
            _focusState.Set(FocusState.Programmatic);
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

            var composer = ElementCompositionPreview.GetElementVisual(drawer);
            var messages = ElementCompositionPreview.GetElementVisual(Messages);
            var textArea = ElementCompositionPreview.GetElementVisual(TextArea);
            var textMain = ElementCompositionPreview.GetElementVisual(TextMain);

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

            flyout.CreateFlyoutItem(Search, Strings.Search, Icons.Search, VirtualKey.F);

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
            }
            if ((user != null && user.Type is not UserTypeDeleted && user.Id != ViewModel.ClientService.Options.MyId) || basicGroup != null || (supergroup != null && !supergroup.IsChannel))
            {
                var muted = ViewModel.ClientService.Notifications.GetMutedFor(chat) > 0;
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
            flyout.CreateFlyoutItem(async () => await TextField.SendAsync(true), Strings.SendWithoutSound, Icons.AlertOff);

            if (ViewModel.ClientService.TryGetUser(chat, out Td.Api.User user) && user.Type is UserTypeRegular && user.Status is not UserStatusRecently && !self)
            {
                flyout.CreateFlyoutItem(async () => await TextField.ScheduleAsync(true), Strings.SendWhenOnline, Icons.PersonCircleOnline);
            }

            flyout.CreateFlyoutItem(async () => await TextField.ScheduleAsync(false), self ? Strings.SetReminder : Strings.ScheduleMessage, Icons.CalendarClock);

            flyout.ShowAt(sender, FlyoutPlacementMode.TopEdgeAlignedRight);
        }

        private void Reply_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            return;

            var flyout = new MenuFlyout();

            var header = ViewModel.ComposerHeader;
            if (header?.WebPagePreview != null)
            {
                flyout.CreateFlyoutItem((web) => { }, header, true ? Strings.LinkBelow : Strings.LinkAbove);

                if (header.WebPagePreview.HasLargeMedia)
                {
                    flyout.CreateFlyoutItem((web) => { }, header, header.LinkPreviewOptions.ForceSmallMedia ? Strings.LinkMediaLarger : Strings.LinkMediaSmaller);
                }

                flyout.CreateFlyoutSeparator();

                flyout.CreateFlyoutItem(ViewModel.ClearReply, Strings.DoNotLinkPreview, Icons.Delete, destructive: true);
            }

            args.ShowAt(flyout, sender as FrameworkElement);
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

            if (args.TryGetPosition(Window.Current.Content as FrameworkElement, out Point point))
            {
                var children = VisualTreeHelper.FindElementsInHostCoordinates(point, element);

                var textBlock = children.FirstOrDefault() as RichTextBlock;
                if (textBlock?.SelectionStart != null && textBlock?.SelectionEnd != null)
                {
                    selectionStart = textBlock.SelectionStart.Shift();
                    selectionEnd = textBlock.SelectionEnd.Shift();

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
                var ancestor = originaSource.AncestorsAndSelf<IContent>().FirstOrDefault();
                if (ancestor?.Message != null)
                {
                    message = ancestor.Message;
                }
            }

            var response = await ViewModel.ClientService.SendAsync(new GetMessage(message.ChatId, message.Id));
            if (response is Message)
            {
                message.UpdateWith(response as Message);
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
                flyout.CreateFlyoutItem(MessageDelete_Loaded, ViewModel.DeleteMessage, message, Strings.Delete, Icons.Delete, destructive: true);

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

                if (CanGetMessageViewers(message))
                {
                    LoadMessageViewers(message, flyout);
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
                            Quote = caption.Substring(selectionStart, selectionEnd - selectionStart)
                        };
                    }
                }

                // Generic
                if (quote != null)
                {
                    flyout.CreateFlyoutItem(MessageQuote_Loaded, ViewModel.QuoteToMessage, quote, Strings.QuoteSelectedPart, Icons.ArrowReply);
                }
                else
                {
                    flyout.CreateFlyoutItem(MessageReply_Loaded, ViewModel.ReplyToMessage, message, Strings.Reply, Icons.ArrowReply);
                }

                flyout.CreateFlyoutItem(MessageEdit_Loaded, ViewModel.EditMessage, message, Strings.Edit, Icons.Edit);
                flyout.CreateFlyoutItem(MessageThread_Loaded, ViewModel.OpenMessageThread, message, message.InteractionInfo?.ReplyInfo?.ReplyCount > 0 ? Locale.Declension(Strings.R.ViewReplies, message.InteractionInfo.ReplyInfo.ReplyCount) : Strings.ViewThread, Icons.ChatMultiple);

                flyout.CreateFlyoutSeparator();

                // Manage
                flyout.CreateFlyoutItem(MessagePin_Loaded, ViewModel.PinMessage, message, message.IsPinned ? Strings.UnpinMessage : Strings.PinMessage, message.IsPinned ? Icons.PinOff : Icons.Pin);
                flyout.CreateFlyoutItem(MessageStatistics_Loaded, ViewModel.OpenMessageStatistics, message, Strings.Statistics, Icons.DataUsage);

                flyout.CreateFlyoutItem(MessageForward_Loaded, ViewModel.ForwardMessage, message, Strings.Forward, Icons.Share);
                flyout.CreateFlyoutItem(MessageReport_Loaded, ViewModel.ReportMessage, message, Strings.ReportChat, Icons.ErrorCircle);
                flyout.CreateFlyoutItem(MessageReportFalsePositive_Loaded, ViewModel.ReportFalsePositive, message, Strings.ReportFalsePositive, Icons.ShieldError);
                flyout.CreateFlyoutItem(MessageDelete_Loaded, ViewModel.DeleteMessage, message, Strings.Delete, Icons.Delete, destructive: true);
                flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.SelectMessage, message, Strings.Select, Icons.CheckmarkCircle);

                flyout.CreateFlyoutSeparator();

                // Copy
                if (quote != null)
                {
                    // TODO: copy selection
                    flyout.CreateFlyoutItem(MessageCopy_Loaded, ViewModel.CopyMessage, quote, Strings.CopySelectedText, Icons.DocumentCopy);
                }
                else
                {
                    flyout.CreateFlyoutItem(MessageCopy_Loaded, ViewModel.CopyMessage, message, Strings.Copy, Icons.DocumentCopy);
                }

                flyout.CreateFlyoutItem(MessageCopyLink_Loaded, ViewModel.CopyMessageLink, message, Strings.CopyLink, Icons.Link);
                flyout.CreateFlyoutItem(MessageCopyMedia_Loaded, ViewModel.CopyMessageMedia, message, Strings.CopyImage, Icons.Image);

                flyout.CreateFlyoutItem(MessageTranslate_Loaded, ViewModel.TranslateMessage, message, Strings.TranslateMessage, Icons.Translate);

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
                flyout.CreateFlyoutItem(MessageOpenMedia_Loaded, ViewModel.OpenMessageWith, message, Strings.OpenWith, Icons.OpenIn);
                flyout.CreateFlyoutItem(MessageOpenFolder_Loaded, ViewModel.OpenMessageFolder, message, Strings.ShowInFolder, Icons.FolderOpen);

                // Contacts
                flyout.CreateFlyoutItem(MessageAddContact_Loaded, ViewModel.AddToContacts, message, Strings.AddContactTitle, Icons.Person);
                //CreateFlyoutItem(ref flyout, MessageSaveDownload_Loaded, ViewModel.MessageSaveDownloadCommand, messageCommon, Strings.SaveToDownloads);

                // Polls
                flyout.CreateFlyoutItem(MessageUnvotePoll_Loaded, ViewModel.UnvotePoll, message, Strings.Unvote, Icons.ArrowUndo);
                flyout.CreateFlyoutItem(MessageStopPoll_Loaded, ViewModel.StopPoll, message, Strings.StopPoll, Icons.LockClosed);

#if DEBUG
                var file = message.GetFile();
                if (file != null)
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
#endif

                if (CanGetMessageEmojis(chat, message, out var emoji))
                {
                    LoadMessageEmojis(message, emoji, flyout);
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

            args.ShowAt(flyout, sender as FrameworkElement, selectionEnd - selectionStart > 0 ? FlyoutShowMode.Transient : FlyoutShowMode.Auto);
        }

        private bool CanGetMessageEmojis(Chat chat, MessageViewModel message, out HashSet<long> emoji)
        {
            var caption = message.GetCaption();
            if (caption?.Entities == null)
            {
                emoji = null;
                return false;
            }

            emoji = new HashSet<long>();

            foreach (var item in caption.Entities)
            {
                if (item.Type is TextEntityTypeCustomEmoji customEmoji)
                {
                    emoji.Add(customEmoji.CustomEmojiId);
                }
            }

            return emoji.Count > 0;
        }

        private async void LoadMessageEmojis(MessageViewModel message, HashSet<long> emoji, MenuFlyout flyout)
        {
            var separator = flyout.CreateFlyoutSeparator();
            var placeholder = flyout.CreateFlyoutItem(ViewModel.ShowMessageEmoji, message, "...");

            // Width must be fixed because emojis are loaded asynchronously
            placeholder.Width = 240;

            var response = await message.ClientService.SendAsync(new GetCustomEmojiStickers(emoji.ToArray()));
            if (response is Stickers stickers && stickers.StickersValue.Count > 0)
            {
                var sets = new HashSet<long>();

                foreach (var sticker in stickers.StickersValue)
                {
                    sets.Add(sticker.SetId);
                }

                if (sets.Count > 1)
                {
                    placeholder.Text = Locale.Declension(Strings.R.MessageContainsEmojiPacks, sets.Count);
                }
                else if (sets.Count > 0)
                {
                    var response2 = await message.ClientService.SendAsync(new GetStickerSet(sets.First()));
                    if (response2 is StickerSet set)
                    {
                        placeholder.Text = string.Format(Strings.MessageContainsEmojiPack, set.Title);
                    }
                }
            }
            else
            {
                placeholder.Text = Strings.NobodyViewed;
            }
        }

        private static bool CanGetMessageViewers(MessageViewModel message, bool reactions = true)
        {
            if (reactions && message.InteractionInfo?.Reactions.Count > 0)
            {
                return message.Chat.Type is ChatTypeBasicGroup || message.Chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel;
            }

            if (message.Chat.LastReadOutboxMessageId < message.Id || !message.CanGetViewers)
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

        private async void LoadMessageViewers(MessageViewModel message, MenuFlyout flyout)
        {
            static async Task<IList<User>> GetMessageViewersAsync(MessageViewModel message)
            {
                if (CanGetMessageViewers(message, false))
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

            var final = new MenuFlyoutSubItem();
            final.Visibility = Visibility.Collapsed;
            flyout.Items.Insert(0, final);

            var viewers = await GetMessageViewersAsync(message);
            if (viewers.Count > 0 || reacted > 0)
            {
                string text;
                if (reacted > 0)
                {
                    if (reacted < viewers.Count)
                    {
                        text = string.Format(Locale.Declension(Strings.R.Reacted, reacted, false), string.Format("{0}/{1}", message.InteractionInfo.Reactions.Count, viewers.Count));
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

                var device = CanvasDevice.GetSharedDevice();
                var rect1 = CanvasGeometry.CreateRectangle(device, 0, 0, 24, 24);
                var elli1 = CanvasGeometry.CreateEllipse(device, -2, 12, 14, 14);
                var group1 = CanvasGeometry.CreateGroup(device, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);

                var compositor = Window.Current.Compositor;
                var geometry = compositor.CreatePathGeometry(new CompositionPath(group1));
                var clip = compositor.CreateGeometricClip(geometry);

                for (int i = 0; i < Math.Min(3, viewers.Count); i++)
                {
                    var user = viewers[i];
                    var picture = new ProfilePicture();
                    picture.Width = 24;
                    picture.Height = 24;
                    picture.IsEnabled = false;
                    picture.SetUser(message.ClientService, user, 24);
                    picture.Margin = new Thickness(pictures.Children.Count > 0 ? -10 : 0, -2, 0, -2);

                    if (pictures.Children.Count > 0)
                    {
                        var visual = ElementCompositionPreview.GetElementVisual(picture);
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

        private bool MessageSendNow_Loaded(MessageViewModel message)
        {
            return message.SchedulingState != null;
        }

        private bool MessageReschedule_Loaded(MessageViewModel message)
        {
            return message.SchedulingState != null;
        }

        private bool MessageQuote_Loaded(MessageQuote quote)
        {
            return MessageReply_Loaded(quote.Message);
        }

        private bool MessageReply_Loaded(MessageViewModel message)
        {
            if (message.SchedulingState != null || (ViewModel.Type != DialogType.History && ViewModel.Type != DialogType.Thread))
            {
                return false;
            }

            if (message.CanBeRepliedInAnotherChat)
            {
                return true;
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
            else if (chat != null && chat.Id == ViewModel.ClientService.Options.RepliesBotChatId)
            {
                return false;
            }

            return true;
        }

        private bool MessagePin_Loaded(MessageViewModel message)
        {
            if (ViewModel.Type is not DialogType.History and not DialogType.Pinned)
            {
                return false;
            }

            if (message.SchedulingState != null || message.IsService)
            {
                return false;
            }

            var chat = message.Chat;
            if (chat != null && chat.Type is ChatTypeSupergroup supergroupType)
            {
                var supergroup = ViewModel.ClientService.GetSupergroup(supergroupType.SupergroupId);
                if (supergroup == null)
                {
                    return false;
                }

                if (supergroup.Status is ChatMemberStatusCreator || (supergroup.Status is ChatMemberStatusAdministrator admin && (admin.Rights.CanPinMessages || supergroup.IsChannel && admin.Rights.CanEditMessages)))
                {
                    return true;
                }
                else if (supergroup.Status is ChatMemberStatusRestricted restricted)
                {
                    return restricted.Permissions.CanPinMessages;
                }
            }
            else if (chat != null && chat.Type is ChatTypeBasicGroup basicGroupType)
            {
                var basicGroup = ViewModel.ClientService.GetBasicGroup(basicGroupType.BasicGroupId);
                if (basicGroup == null)
                {
                    return false;
                }

                if (basicGroup.Status is ChatMemberStatusCreator || (basicGroup.Status is ChatMemberStatusAdministrator admin && admin.Rights.CanPinMessages))
                {
                    return true;
                }
            }
            else if (chat != null && chat.Type is ChatTypePrivate)
            {
                return true;
            }

            if (chat != null)
            {
                return chat.Permissions.CanPinMessages;
            }

            return false;
        }

        private bool MessageEdit_Loaded(MessageViewModel message)
        {
            if (message.Content is MessagePoll or MessageLocation)
            {
                return false;
            }

            return message.CanBeEdited;
        }

        private bool MessageThread_Loaded(MessageViewModel message)
        {
            if (ViewModel.Type is not DialogType.History and not DialogType.Pinned)
            {
                return false;
            }

            if (message.InteractionInfo?.ReplyInfo == null || message.InteractionInfo?.ReplyInfo?.ReplyCount > 0)
            {
                return message.CanGetMessageThread && !message.IsChannelPost;
            }

            return false;
        }

        private bool MessageDelete_Loaded(MessageViewModel message)
        {
            return message.CanBeDeletedOnlyForSelf || message.CanBeDeletedForAllUsers;
        }

        private bool MessageForward_Loaded(MessageViewModel message)
        {
            return message.CanBeForwarded;
        }

        private bool MessageUnvotePoll_Loaded(MessageViewModel message)
        {
            if ((ViewModel.Type == DialogType.History || ViewModel.Type == DialogType.Thread) && message.Content is MessagePoll poll && poll.Poll.Type is PollTypeRegular)
            {
                return poll.Poll.Options.Any(x => x.IsChosen) && !poll.Poll.IsClosed;
            }

            return false;
        }

        private bool MessageStopPoll_Loaded(MessageViewModel message)
        {
            if (message.Content is MessagePoll)
            {
                return message.CanBeEdited;
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

        private bool MessageTranslate_Loaded(MessageViewModel message)
        {
            var caption = message.GetCaption();
            if (caption != null)
            {
                return ViewModel.TranslateService.CanTranslate(caption.Text);
            }
            else if (message.Content is MessageVoiceNote voiceNote
                && voiceNote.VoiceNote.SpeechRecognitionResult is SpeechRecognitionResultText speechVoiceText)
            {
                return ViewModel.TranslateService.CanTranslate(speechVoiceText.Text);
            }
            else if (message.Content is MessageVideoNote videoNote
                && videoNote.VideoNote.SpeechRecognitionResult is SpeechRecognitionResultText speechVideoText)
            {
                return ViewModel.TranslateService.CanTranslate(speechVideoText.Text);
            }
            else if (message.Content is MessagePoll poll)
            {
                return ViewModel.TranslateService.CanTranslate(poll.Poll.Question);
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
                return invoice.Photo != null;
            }
            else if (message.Content is MessageText text)
            {
                return text.WebPage != null && text.WebPage.HasPhoto();
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
            if (_myPeople || ViewModel.Type == DialogType.EventLog || message.IsService)
            {
                return false;
            }

            return true;
        }

        private bool MessageStatistics_Loaded(MessageViewModel message)
        {
            return message.CanGetStatistics;
        }

        private bool MessageAddSticker_Loaded(MessageViewModel message)
        {
            if (message.Content is MessageSticker sticker && sticker.Sticker.SetId != 0)
            {
                return !ViewModel.ClientService.IsStickerSetInstalled(sticker.Sticker.SetId);
            }
            else if (message.Content is MessageText text && text.WebPage?.Sticker != null && text.WebPage.Sticker.SetId != 0)
            {
                return !ViewModel.ClientService.IsStickerSetInstalled(text.WebPage.Sticker.SetId);
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
                return text.WebPage != null && text.WebPage.Animation != null;
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
                if (text.WebPage?.Audio != null)
                {
                    return text.WebPage.Audio.Duration <= ViewModel.ClientService.Options.NotificationSoundDurationMax
                        && text.WebPage.Audio.AudioValue.Size <= ViewModel.ClientService.Options.NotificationSoundSizeMax;
                }
                else if (text.WebPage?.VoiceNote != null)
                {
                    return text.WebPage.VoiceNote.Duration <= ViewModel.ClientService.Options.NotificationSoundDurationMax
                        && text.WebPage.VoiceNote.Voice.Size <= ViewModel.ClientService.Options.NotificationSoundSizeMax;
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
            Stickers_ItemClick(sticker, false);
        }

        public void Stickers_ItemClick(Sticker sticker, bool fromStickerSet)
        {
            ViewModel.SendSticker(sticker, null, null, null, fromStickerSet);
            ButtonStickers.Collapse();

            _focusState.Set(FocusState.Programmatic);
        }

        private void Stickers_ChoosingItem(object sender, EventArgs e)
        {
            ViewModel.ChatActionManager.SetTyping(new ChatActionChoosingSticker());
        }

        public void Animations_ItemClick(Animation animation)
        {
            ViewModel.SendAnimation(animation);
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

        private async void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
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

                ButtonMore.IsChecked = false;
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
                    range.SetText(TextSetOptions.None, string.Empty);

                    await TextField.InsertEmojiAsync(range, sticker.Emoji, customEmoji.CustomEmojiId);
                    TextField.Document.Selection.StartPosition = index + 1;
                }
                else
                {
                    TextField.SetText(null, null);
                    ViewModel.SendSticker(sticker, null, null, result);

                    ButtonStickers.Collapse();
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

            var manage = ElementCompositionPreview.GetElementVisual(ManagePanel);
            manage.StopAnimation("Opacity");

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
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

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary && dialog.SelectedDates.Count > 0)
                {
                    var first = dialog.SelectedDates.FirstOrDefault();
                    var offset = Common.Extensions.ToTimestamp(first.Date);

                    await ViewModel.LoadDateSliceAsync(offset);
                }
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
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
            ElementCompositionPreview.GetElementVisual(sender as UIElement).CenterPoint = new Vector3((float)e.NewSize.Width / 2f, (float)e.NewSize.Height / 2f, 0);
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
                return;
            }

            if (message.Content is MessageAsyncStory asyncStory)
            {
                var segments = button.FindName("Segments") as ActiveStoriesSegments;
                if (segments != null)
                {
                    var transform = segments.TransformToVisual(Window.Current.Content);
                    var point = transform.TransformPoint(new Windows.Foundation.Point());

                    var origin = new Rect(point.X + 4, point.Y + 4, 112, 112);

                    var item = asyncStory.Story;
                    item ??= await _viewModel.ClientService.SendAsync(new GetStory(asyncStory.StorySenderChatId, asyncStory.StoryId, true)) as Story;

                    var story = new StoryViewModel(message.ClientService, item);
                    var activeStories = new ActiveStoriesViewModel(message.ClientService, message.Delegate.Settings, message.Delegate.Aggregator, story);

                    var viewModel = new StoryListViewModel(message.ClientService, message.Delegate.Settings, message.Delegate.Aggregator, activeStories);
                    viewModel.NavigationService = _viewModel.NavigationService;

                    var window = new StoriesWindow();
                    window.Update(viewModel, activeStories, StoryOrigin.Mention, origin, _ =>
                    {
                        var transform = segments.TransformToVisual(Window.Current.Content);
                        var point = transform.TransformPoint(new Windows.Foundation.Point());

                        return new Rect(point.X + 4, point.Y + 4, 112, 112);
                    });

                    _ = window.ShowAsync();

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

                var user = ViewModel.ClientService.GetUser(userCommand.UserId);
                if (user == null)
                {
                    return;
                }

                photo.SetUser(ViewModel.ClientService, user, 32);
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

                var file = sticker?.StickerValue;
                if (file.Local.IsDownloadingCompleted)
                {
                }
                else
                {
                    CompositionPathParser.ParseThumbnail(sticker, out ShapeVisual visual, false);
                    ElementCompositionPreview.SetElementChildVisual(content.Children[0], visual);
                }

                animated.Source = new DelayedFileSource(_viewModel.ClientService, file);
            }
        }

        private bool? _replyEnabled = null;

        private void ShowAction(string content, bool enabled, bool replyEnabled = false)
        {
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

            //LabelAction.Text = content;
            _replyEnabled = replyEnabled;
            ButtonAction.IsEnabled = enabled;
            ButtonAction.Visibility = Visibility.Visible;
            ChatFooter.Visibility = Visibility.Visible;
            TextArea.Visibility = Visibility.Collapsed;
        }

        private void ShowArea(bool permanent = true)
        {
            if (permanent)
            {
                _replyEnabled = null;
            }

            ButtonAction.IsEnabled = false;
            ButtonAction.Visibility = Visibility.Collapsed;
            ChatFooter.Visibility = Visibility.Collapsed;
            TextArea.Visibility = Visibility.Visible;

            _focusState.Set(FocusState.Programmatic);
        }

        private bool StillValid(Chat chat)
        {
            return chat?.Id == ViewModel?.Chat?.Id;
        }

        #region UI delegate

        public void UpdateChat(Chat chat)
        {
            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);

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

            // We want to collapse the bar only of we know that there's no call at all
            if (chat.VideoChat.GroupCallId == 0)
            {
                GroupCall.ShowHide(false);
            }

            UpdateChatMessageSender(chat, chat.MessageSenderId);
            UpdateChatPendingJoinRequests(chat);
            UpdateChatPermissions(chat);
            UpdateChatTheme(chat);
        }

        public void UpdateChatMessageSender(Chat chat, MessageSender defaultMessageSenderId)
        {
            if (defaultMessageSenderId == null)
            {
                //PhotoMore.Source = null;
                ShowHideBotCommands(false);
            }
            else
            {
                PhotoMore.SetMessageSender(ViewModel.ClientService, defaultMessageSenderId, 32);
                ShowHideBotCommands(true);
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

        private async void UpdateChatTheme(ChatTheme theme)
        {
            if (Theme.Current.Update(ActualTheme, theme, _viewModel.Chat.Background))
            {
                var current = _viewModel.Chat.Background?.Background;
                current ??= ActualTheme == ElementTheme.Light ? theme?.LightSettings.Background : theme?.DarkSettings.Background;
                current ??= ViewModel.ClientService.GetSelectedBackground(ActualTheme == ElementTheme.Dark);

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

        public void UpdateChatTitle(Chat chat)
        {
            if (ViewModel.Type == DialogType.Thread)
            {
                if (ViewModel.Topic != null)
                {
                    Title.Text = ViewModel.Topic.Info.Name;
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
                            Title.Text = Locale.Declension(Strings.R.Comments, message.InteractionInfo.ReplyInfo.ReplyCount);
                        }
                        else
                        {
                            Title.Text = Locale.Declension(Strings.R.Replies, message.InteractionInfo.ReplyInfo.ReplyCount);
                        }
                    }
                    else
                    {
                        Title.Text = Locale.Declension(Strings.R.Replies, message.InteractionInfo.ReplyInfo.ReplyCount);
                    }
                }
            }
            else if (ViewModel.Type == DialogType.ScheduledMessages)
            {
                Title.Text = ViewModel.ClientService.IsSavedMessages(chat) ? Strings.Reminders : Strings.ScheduledMessages;
            }
            else if (chat.Type is ChatTypeSecret)
            {
                Title.Text = Icons.LockClosedFilled14 + "\u00A0" + ViewModel.ClientService.GetTitle(chat);
            }
            else
            {
                Title.Text = ViewModel.ClientService.GetTitle(chat);
            }

            _setTitle(Title.Text);
        }

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
                    Photo.Source = PlaceholderImage.GetGlyph(Icons.ChatMultiple, 5);
                    Photo.IsEnabled = false;
                }
            }
            else
            {
                UnloadObject(Icon);
                Photo.SetChat(ViewModel.ClientService, chat, 36);
                Photo.IsEnabled = true;
            }
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
            ActionBar.UpdateChatActionBar(chat);
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
                    ShowAction(ViewModel.ClientService.Notifications.GetMutedFor(chat) > 0 ? Strings.ChannelUnmute : Strings.ChannelMute, true);
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
            else if (supergroup.Status is ChatMemberStatusCreator creator && creator.IsAnonymous || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.IsAnonymous)
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
            if (message?.ReplyMarkup is ReplyMarkupForceReply forceReply && forceReply.IsPersonal)
            {
                ViewModel.ReplyToMessage(message);

                if (forceReply.InputFieldPlaceholder.Length > 0)
                {
                    TextField.PlaceholderText = forceReply.InputFieldPlaceholder;

                    if (_isTextReadOnly != false)
                    {
                        _isTextReadOnly = false;
                        TextField.IsReadOnly = false;
                    }
                }
                else
                {
                    TextField.PlaceholderText = GetPlaceholder(chat, out bool readOnly);

                    if (_isTextReadOnly != readOnly)
                    {
                        _isTextReadOnly = readOnly;
                        TextField.IsReadOnly = readOnly;
                    }
                }

                ButtonMarkup.Visibility = Visibility.Collapsed;
                CollapseMarkup(false);
            }
            else
            {
                var updated = ReplyMarkup.Update(message, message?.ReplyMarkup, false);
                if (updated)
                {
                    if (message.ReplyMarkup is ReplyMarkupShowKeyboard showKeyboard && showKeyboard.InputFieldPlaceholder.Length > 0)
                    {
                        TextField.PlaceholderText = showKeyboard.InputFieldPlaceholder;

                        if (_isTextReadOnly != false)
                        {
                            _isTextReadOnly = false;
                            TextField.IsReadOnly = false;
                        }
                    }
                    else
                    {
                        TextField.PlaceholderText = GetPlaceholder(chat, out bool readOnly);

                        if (_isTextReadOnly != readOnly)
                        {
                            _isTextReadOnly = readOnly;
                            TextField.IsReadOnly = readOnly;
                        }
                    }

                    ButtonMarkup.Visibility = Visibility.Visible;
                    ShowMarkup();
                }
                else
                {
                    TextField.PlaceholderText = GetPlaceholder(chat, out bool readOnly);
                    TextField.IsReadOnly = readOnly;

                    ButtonMarkup.Visibility = Visibility.Collapsed;
                    CollapseMarkup(false);
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
            }
        }

        public void UpdateComposerHeader(Chat chat, MessageComposerHeader header)
        {
            CheckButtonsVisibility();

            if (header == null || (header.IsEmpty && header.WebPageDisabled))
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
                        case MessageAnimation animation:
                        case MessageAudio audio:
                        case MessageDocument document:
                            ButtonAttach.Glyph = Icons.AttachArrowRight24;
                            ButtonAttach.IsEnabled = true;
                            break;
                        case MessagePhoto photo:
                            ButtonAttach.Glyph = !photo.IsSecret ? Icons.AttachArrowRight24 : Icons.Attach24;
                            ButtonAttach.IsEnabled = !photo.IsSecret;
                            break;
                        case MessageVideo video:
                            ButtonAttach.Glyph = !video.IsSecret ? Icons.AttachArrowRight24 : Icons.Attach24;
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

                    if (header.WebPagePreview != null)
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
        private bool _botCommandsCollapsed = true;
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

            var composer = ElementCompositionPreview.GetElementVisual(ComposerHeader);
            var messages = ElementCompositionPreview.GetElementVisual(Messages);
            var textArea = ElementCompositionPreview.GetElementVisual(TextArea);

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

        private void ShowHideBotCommands(bool show)
        {
            if (_botCommandsCollapsed != show)
            {
                return;
            }

            _botCommandsCollapsed = !show;
            ButtonMore.Visibility = Visibility.Visible;

            var more = ElementCompositionPreview.GetElementVisual(ButtonMore);
            var field = ElementCompositionPreview.GetElementVisual(TextFieldPanel);
            var attach = ElementCompositionPreview.GetElementVisual(btnAttach);

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                field.Properties.InsertVector3("Translation", Vector3.Zero);
                attach.Properties.InsertVector3("Translation", Vector3.Zero);

                if (show)
                {
                    _botCommandsCollapsed = false;
                }
                else
                {
                    ButtonMore.IsChecked = false;
                    ButtonMore.Visibility = Visibility.Collapsed;
                }

                UpdateTextAreaRadius();
            };

            var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(-40, 0, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            var scale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale.InsertKeyFrame(show ? 1 : 0, Vector3.One);
            scale.Duration = Constants.FastAnimation;

            var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(show ? 0 : 1, 0);
            opacity.InsertKeyFrame(show ? 1 : 0, 1);

            more.CenterPoint = new Vector3(20, 16, 0);
            more.StartAnimation("Scale", scale);
            more.StartAnimation("Opacity", opacity);
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

            var list = ElementCompositionPreview.GetElementVisual(ListAutocomplete);
            list.StopAnimation("Translation");

            await ListAutocomplete.UpdateLayoutAsync();

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
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

            var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, ListAutocomplete.ActualSize.Y, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            list.StartAnimation("Translation", offset);

            batch.End();
        }


        private void UpdateTextAreaRadius()
        {
            var radius = SettingsService.Current.Appearance.BubbleRadius;
            var min = Math.Max(4, radius - 2);
            var max = ComposerHeader.Visibility == Visibility.Visible ? 4 : min;

            ButtonAttach.CornerRadius = new CornerRadius(_botCommandsCollapsed ? max : 4, 4, 4, _botCommandsCollapsed ? min : 4);
            btnVoiceMessage.CornerRadius = new CornerRadius(4, max, min, 4);
            btnSendMessage.CornerRadius = new CornerRadius(4, max, min, 4);
            btnEdit.CornerRadius = new CornerRadius(4, max, min, 4);
            ButtonDelete.CornerRadius = new CornerRadius(4, min, min, 4);
            ButtonManage.CornerRadius = new CornerRadius(min, 4, 4, min);

            ComposerHeaderCancel.CornerRadius = new CornerRadius(4, min, 4, 4);
            TextRoot.CornerRadius = ChatFooter.CornerRadius = ChatRecord.CornerRadius = ManagePanel.CornerRadius = new CornerRadius(radius);

            // It would be cool to have shadow to respect text field corner radius
            //Separator.CornerRadius = new CornerRadius(radius);
            ListAutocomplete.CornerRadius = InlinePanel.CornerRadius = new CornerRadius(radius, radius, 0, 0);
            ListAutocomplete.Padding = new Thickness(0, 0, 0, radius);

            ListInline?.UpdateCornerRadius(radius);

            ReplyMarkupPanel.CornerRadius = new CornerRadius(0, 0, radius, radius);
            ReplyMarkupPanel.Padding = new Thickness(0, radius, 0, 0);

            if (radius > 0)
            {
                TextArea.MaxWidth = ChatRecord.MaxWidth = ChatFooter.MaxWidth = ManagePanel.MaxWidth = InlinePanel.MaxWidth = Separator.MaxWidth =
                    SettingsService.Current.IsAdaptiveWideEnabled ? 1000 : double.PositiveInfinity;
                TextArea.Margin = ChatRecord.Margin = ChatFooter.Margin = ManagePanel.Margin = Separator.Margin = new Thickness(12, 0, 12, 8);
                InlinePanel.Margin = new Thickness(12, 0, 12, -radius);
                ReplyMarkupPanel.Margin = new Thickness(12, -8 - radius, 12, 8);
            }
            else
            {
                TextArea.MaxWidth = ChatRecord.MaxWidth = ChatFooter.MaxWidth = ManagePanel.MaxWidth = InlinePanel.MaxWidth = Separator.MaxWidth =
                    SettingsService.Current.IsAdaptiveWideEnabled ? 1024 : double.PositiveInfinity;
                TextArea.Margin = ChatRecord.Margin = ChatFooter.Margin = ManagePanel.Margin = Separator.Margin = new Thickness();
                InlinePanel.Margin = new Thickness();
                ReplyMarkupPanel.Margin = new Thickness();
            }

            var messages = ElementCompositionPreview.GetElementVisual(Messages);
            if (messages.Clip is InsetClip messagesClip)
            {
                messagesClip.TopInset = -44;
                messagesClip.BottomInset = -8 - radius;
            }
            else
            {
                messages.Clip = Window.Current.Compositor.CreateInsetClip(0, -44, 0, -8 - radius);
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
                //var visual = ElementCompositionPreview.GetElementVisual(ListAutocomplete);

                //var anim = Window.Current.Compositor.CreateSpringVector3Animation();
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
            btnSendMessage.SlowModeDelay = 0;
            btnSendMessage.SlowModeDelayExpiresIn = 0;

            if (user.Id != ViewModel.ClientService.Options.MyId)
            {
                Identity.SetStatus(ViewModel.ClientService, user);
            }
            else
            {
                Identity.ClearStatus();
            }

            if (!secret)
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
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            if (ViewModel.Type == DialogType.Pinned)
            {
                ShowAction(Strings.UnpinAllMessages, true);
            }
            else if (user.Type is UserTypeDeleted)
            {
                ShowAction(Strings.DeleteThisChat, true);
            }
            else if (ViewModel.ClientService.IsRepliesChat(chat))
            {
                ShowAction(ViewModel.ClientService.Notifications.GetMutedFor(chat) > 0 ? Strings.ChannelUnmute : Strings.ChannelMute, true);
            }
            else if (chat.BlockList is BlockListMain)
            {
                ShowAction(user.Type is UserTypeBot ? Strings.BotUnblock : Strings.Unblock, true);
            }
            else if (user.Type is UserTypeBot && (accessToken || chat?.LastMessage == null))
            {
                ShowAction(Strings.BotStart, true);
            }
            else if (!secret)
            {
                ShowArea();
            }

            if (fullInfo.BotInfo?.Commands.Count > 0)
            {
                ViewModel.BotCommands = fullInfo.BotInfo.Commands.Select(x => new UserCommand(user.Id, x)).ToList();
                ViewModel.HasBotCommands = false;
                ShowHideBotCommands(true);
            }
            else
            {
                ViewModel.BotCommands = null;
                ViewModel.HasBotCommands = false;
                ShowHideBotCommands(false);
            }

            Automation.SetToolTip(Call, Strings.Call);

            btnVoiceMessage.IsRestricted = fullInfo.HasRestrictedVoiceAndVideoNoteMessages
                && user.Id != ViewModel.ClientService.Options.MyId;

            Call.Glyph = Icons.Phone;
            Call.Visibility = /*!secret &&*/ fullInfo.CanBeCalled ? Visibility.Visible : Visibility.Collapsed;
            VideoCall.Visibility = /*!secret &&*/ fullInfo.CanBeCalled && fullInfo.SupportsVideoCalls ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            if (ViewModel.ClientService.IsSavedMessages(user))
            {
                ViewModel.LastSeen = null;
            }
            else if (ViewModel.ClientService.IsRepliesChat(chat))
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
            Identity.ClearStatus();

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
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
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
            Identity.SetStatus(group);

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
                if ((group.Status is ChatMemberStatusLeft && (group.HasActiveUsername() || ViewModel.ClientService.IsChatAccessible(chat))) || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
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
                    ShowAction(ViewModel.ClientService.Notifications.GetMutedFor(chat) > 0 ? Strings.ChannelUnmute : Strings.ChannelMute, true);
                }
            }
            else
            {
                if ((group.Status is ChatMemberStatusLeft && (group.IsPublic() || ViewModel.ClientService.IsChatAccessible(chat))) || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
                {
                    if (group.JoinByRequest)
                    {
                        ShowAction(Strings.ChannelJoinRequest, true);
                    }
                    else if (ViewModel.Type == DialogType.Thread)
                    {
                        if (!chat.Permissions.CanSendBasicMessages)
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
                else if (!chat.Permissions.CanSendBasicMessages)
                {
                    ShowAction(Strings.GlobalSendMessageRestricted, false);
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
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
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

        #endregion

        private void TextField_Sending(object sender, EventArgs e)
        {
            ButtonStickers.Collapse();
        }

        private async void ButtonMore_Checked(object sender, RoutedEventArgs e)
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
                ButtonMore.IsChecked = false;
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
                    picture.IsEnabled = false;
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

            flyout.ShowAt(ButtonMore, FlyoutPlacementMode.TopEdgeAlignedLeft);
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
    }

    public enum StickersPanelMode
    {
        Collapsed,
        Overlay
    }











    public class AvatarWavesDrawable
    {
        float amplitude;
        float animateAmplitudeDiff;
        float animateToAmplitude;
        private readonly BlobDrawable buttonDrawable = new BlobDrawable(4);
        private readonly BlobDrawable blobDrawable = new BlobDrawable(6);
        private readonly BlobDrawable blobDrawable2 = new BlobDrawable(8);
        bool showWaves = true;
        float wavesEnter = 0.0f;

        public AvatarWavesDrawable(bool large, bool button)
        {
            if (button)
            {
                buttonDrawable.minRadius = large ? 36 : 20; // 22.0f;
                buttonDrawable.maxRadius = large ? 40 : 24; //28.0f;
                buttonDrawable.GenerateBlob();
            }

            blobDrawable.minRadius = large ? 56 : 32; // 22.0f;
            blobDrawable.maxRadius = large ? 64 : 36; //28.0f;
            blobDrawable2.minRadius = large ? 52 : 30; // 22.0f;
            blobDrawable2.maxRadius = large ? 60 : 34; // 28.0f;
            blobDrawable.GenerateBlob();
            blobDrawable2.GenerateBlob();
            //this.blobDrawable.paint.setColor(ColorUtils.setAlphaComponent(Theme.getColor("voipgroup_speakingText"), 38));
            //this.blobDrawable2.paint.setColor(ColorUtils.setAlphaComponent(Theme.getColor("voipgroup_speakingText"), 38));
            blobDrawable.paint.A = large ? (byte)38 : (byte)61;
            blobDrawable2.paint.A = large ? (byte)38 : (byte)61;
        }

        public void Update(Color color, bool large)
        {
            if (buttonDrawable != null)
            {
                buttonDrawable.paint = color;
            }

            blobDrawable.paint = color;
            blobDrawable2.paint = color;

            blobDrawable.paint.A = large ? (byte)38 : (byte)61;
            blobDrawable2.paint.A = large ? (byte)38 : (byte)61;
        }

        public void Draw(CanvasDrawingSession canvas, float x, float y, CanvasControl view)
        {
            float f3 = animateToAmplitude;
            float f4 = amplitude;
            if (f3 != f4)
            {
                float f5 = animateAmplitudeDiff;
                float f6 = f4 + (16.0f * f5);
                amplitude = f6;
                if (f5 > 0.0f)
                {
                    if (f6 > f3)
                    {
                        amplitude = f3;
                    }
                }
                else if (f6 < f3)
                {
                    amplitude = f3;
                }

                if (PowerSavingPolicy.AreCallsAnimated)
                {
                    view.Invalidate();
                }
            }
            float f7 = (amplitude * 0.2f) + 0.8f;
            if (showWaves || wavesEnter != 0.0f)
            {
                //canvas.save();
                bool z = showWaves;
                if (z)
                {
                    float f8 = wavesEnter;
                    if (f8 != 1.0f)
                    {
                        float f9 = f8 + 0.064f;
                        wavesEnter = f9;
                        if (f9 > 1.0f)
                        {
                            wavesEnter = 1.0f;
                        }
                        float interpolation = f7 * Charts.CubicBezierInterpolator.EASE_OUT.getInterpolation(wavesEnter);
                        //canvas.scale(interpolation, interpolation, f, f2);
                        canvas.Transform = Matrix3x2.CreateScale(interpolation, interpolation, new Vector2(x, y));
                        blobDrawable.Update(amplitude, 1.0f);
                        blobDrawable.Draw(canvas, x, y);
                        blobDrawable2.Update(amplitude, 1.0f);
                        blobDrawable2.Draw(canvas, x, y);
                        canvas.Transform = Matrix3x2.Identity;
                        if (buttonDrawable != null)
                        {
                            buttonDrawable.Update(amplitude, 1.0f);
                            buttonDrawable.Draw(canvas, x, y);
                        }

                        if (PowerSavingPolicy.AreCallsAnimated)
                        {
                            view.Invalidate();
                        }
                        //canvas.restore();
                    }
                }
                if (!z)
                {
                    float f10 = wavesEnter;
                    if (f10 != 0.0f)
                    {
                        float f11 = f10 - 0.064f;
                        wavesEnter = f11;
                        if (f11 < 0.0f)
                        {
                            wavesEnter = 0.0f;
                        }
                    }
                }
                float interpolation2 = f7 * Charts.CubicBezierInterpolator.EASE_OUT.getInterpolation(wavesEnter);
                //canvas.scale(interpolation2, interpolation2, f, f2);
                canvas.Transform = Matrix3x2.CreateScale(interpolation2, interpolation2, new Vector2(x, y));
                blobDrawable.Update(amplitude, 1.0f);
                blobDrawable.Draw(canvas, x, y);
                blobDrawable2.Update(amplitude, 1.0f);
                blobDrawable2.Draw(canvas, x, y);
                canvas.Transform = Matrix3x2.Identity;
                if (buttonDrawable != null)
                {
                    buttonDrawable.Update(amplitude, 1.0f);
                    buttonDrawable.Draw(canvas, x, y);
                }

                if (PowerSavingPolicy.AreCallsAnimated)
                {
                    view.Invalidate();
                }
                //canvas.restore();
            }
        }

        public float AvatarScale
        {
            get
            {
                float interpolation = Charts.CubicBezierInterpolator.EASE_OUT.getInterpolation(wavesEnter);
                return (((amplitude * 0.2f) + 0.8f) * interpolation) + ((1.0f - interpolation) * 1.0f);
            }
        }

        public void SetShowWaves(bool z)
        {
            showWaves = z;
        }

        public void SetAmplitude(double d, CanvasControl view)
        {
            float f = ((float)d) / 100.0f;
            float f2 = 0.0f;
            if (!showWaves)
            {
                f = 0.0f;
            }
            if (f > 1.0f)
            {
                f2 = 1.0f;
            }
            else if (f >= 0.0f)
            {
                f2 = f;
            }
            animateToAmplitude = f2;
            animateAmplitudeDiff = (f2 - amplitude) / 150.0f;

            if (PowerSavingPolicy.AreCallsAnimated)
            {
                view.Invalidate();
            }
        }
    }

    public class BlobDrawable
    {
        public static float AMPLITUDE_SPEED = 0.33f;
        public static float FORM_BIG_MAX = 0.6f;
        public static float FORM_BUTTON_MAX = 0.0f;
        public static float FORM_SMALL_MAX = 0.6f;
        public static float GLOBAL_SCALE = 1.0f;
        public static float GRADIENT_SPEED_MAX = 0.01f;
        public static float GRADIENT_SPEED_MIN = 0.5f;
        public static float LIGHT_GRADIENT_SIZE = 0.5f;
        public static float MAX_SPEED = 8.2f;
        public static float MIN_SPEED = 0.8f;
        public static float SCALE_BIG = 0.807f;
        public static float SCALE_BIG_MIN = 0.878f;
        public static float SCALE_SMALL = 0.704f;
        public static float SCALE_SMALL_MIN = 0.926f;
        private readonly float L;
        private readonly float N;
        private readonly float[] angle;
        private readonly float[] angleNext;
        public float cubicBezierK = 1.0f;
        private Matrix3x2 m;
        public float maxRadius;
        public float minRadius;
        public Color paint = Colors.Red;
        private readonly Vector2[] pointEnd = new Vector2[2];
        private readonly Vector2[] pointStart = new Vector2[2];
        private readonly float[] progress;
        private readonly float[] radius;
        private readonly float[] radiusNext;
        readonly Random random = new Random();
        private readonly float[] speed;

        public BlobDrawable(int i)
        {
            float f = i;
            N = f;
            float d = (float)(f * 2.0f);
            //Double.isNaN(d);
            L = MathF.Tan(3.141592653589793f / d) * 1.3333333333333333f;
            radius = new float[i];
            angle = new float[i];
            radiusNext = new float[i];
            angleNext = new float[i];
            progress = new float[i];
            speed = new float[i];
            for (int i2 = 0; i2 < N; i2++)
            {
                generateBlob(radius, angle, i2);
                generateBlob(radiusNext, angleNext, i2);
                progress[i2] = 0.0f;
            }
        }

        private void generateBlob(float[] fArr, float[] fArr2, int i)
        {
            fArr[i] = minRadius + (MathF.Abs((random.Next() % 100.0f) / 100.0f) * (maxRadius - minRadius));
            fArr2[i] = ((360.0f / N) * i) + (((random.Next() % 100.0f) / 100.0f) * (360.0f / N) * 0.05f);
            double abs = MathF.Abs(random.Next() % 100.0f) / 100.0f;
            Double.IsNaN(abs);
            speed[i] = (float)((abs * 0.003d) + 0.017d);
        }

        public void Update(float f, float f2)
        {
            for (int i = 0; i < N; i++)
            {
                float f3 = progress[i];
                progress[i] = f3 + (speed[i] * MIN_SPEED) + (speed[i] * f * MAX_SPEED * f2);
                if (progress[i] >= 1.0f)
                {
                    progress[i] = 0.0f;
                    radius[i] = radiusNext[i];
                    angle[i] = angleNext[i];
                    generateBlob(radiusNext, angleNext, i);
                }
            }
        }

        public void Draw(CanvasDrawingSession canvas, float x, float y)
        {
            using var path = new CanvasPathBuilder(canvas);
            int i = 0;
            while (true)
            {
                float f5 = N;
                if (i < f5)
                {
                    float[] fArr = progress;
                    float f6 = fArr[i];
                    int i2 = i + 1;
                    int i3 = i2 < f5 ? i2 : 0;
                    float f7 = fArr[i3];
                    float f8 = 1.0f - f6;
                    float f9 = (radius[i] * f8) + (radiusNext[i] * f6);
                    float f10 = 1.0f - f7;
                    float f11 = (radius[i3] * f10) + (radiusNext[i3] * f7);
                    float f12 = angle[i] * f8;
                    float f13 = (angle[i3] * f10) + (angleNext[i3] * f7);
                    float min = L * (MathF.Min(f9, f11) + ((Math.Max(f9, f11) - Math.Min(f9, f11)) / 2.0f)) * cubicBezierK;
                    pointStart[0].X = x;
                    pointStart[0].Y = y - f9;
                    pointStart[1].X = x + min;
                    pointStart[1].Y = y - f9;
                    m = SetRotate(f12 + (angleNext[i] * f6), x, y);
                    pointStart[0] = Vector2.Transform(pointStart[0], m);
                    pointStart[1] = Vector2.Transform(pointStart[1], m);
                    pointEnd[0].X = x;
                    pointEnd[0].Y = y - f11;
                    pointEnd[1].X = x - min;
                    pointEnd[1].Y = y - f11;
                    m = SetRotate(f13, x, y);
                    pointEnd[0] = Vector2.Transform(pointEnd[0], m);
                    pointEnd[1] = Vector2.Transform(pointEnd[1], m);
                    if (i == 0)
                    {
                        path.BeginFigure(pointStart[0]);
                    }
                    path.AddCubicBezier(pointStart[1], pointEnd[1], pointEnd[0]);
                    i = i2;
                }
                else
                {
                    //canvas.save();
                    //canvas.drawPath(this.path, paint2);
                    //canvas.restore();
                    path.EndFigure(CanvasFigureLoop.Closed);
                    canvas.FillGeometry(CanvasGeometry.CreatePath(path), paint);
                    return;
                }
            }
        }

        private Matrix3x2 SetRotate(float degree, float px, float py)
        {
            return Matrix3x2.CreateRotation(MathFEx.ToRadians(degree), new Vector2(px, py));
        }

        public void GenerateBlob()
        {
            for (int i = 0; i < N; i++)
            {
                generateBlob(radius, angle, i);
                generateBlob(radiusNext, angleNext, i);
                progress[i] = 0.0f;
            }
        }
    }

}
