﻿using LinqToVisualTree;
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
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Common.Chats;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Controls.Chats;
using Unigram.Controls.Gallery;
using Unigram.Controls.Messages;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Users;
using Unigram.Views.Popups;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.ViewManagement.Core;
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

namespace Unigram.Views
{
    public sealed partial class ChatView : HostedPage, INavigablePage, ISearchablePage, IDialogDelegate, IActivablePage
    {
        private DialogViewModel _viewModel;
        public DialogViewModel ViewModel => _viewModel ??= DataContext as DialogViewModel;

        private readonly Func<IDialogDelegate, int, DialogViewModel> _getViewModel;
        private readonly Action<string> _setTitle;

        private readonly TLWindowContext _windowContext;

        private readonly bool _myPeople;

        private bool _selectionFromItemClick;

        private readonly DispatcherTimer _slowModeTimer;

        private DispatcherTimer _stickersTimer;
        private Visual _stickersPanel;
        private Visual _stickersShadow;
        private StickersPanelMode _stickersMode = StickersPanelMode.Collapsed;

        private readonly DispatcherTimer _elapsedTimer;
        private readonly Visual _ellipseVisual;
        private readonly Visual _elapsedVisual;
        private readonly Visual _slideVisual;
        private readonly Visual _recordVisual;
        private readonly Visual _rootVisual;
        private readonly Visual _textShadowVisual;

        private readonly DispatcherTimer _dateHeaderTimer;
        private readonly Visual _dateHeaderPanel;
        private readonly Visual _dateHeader;

        private readonly ZoomableListHandler _autocompleteZoomer;
        private readonly AnimatedListHandler<Sticker> _autocompleteHandler;

        private TaskCompletionSource<bool> _updateThemeTask;
        private TaskCompletionSource<bool> _loadedThemeTask;

        private ChatBackgroundPresenter _backgroundPresenter;

        public ChatView(Func<IDialogDelegate, int, DialogViewModel> getViewModel, Action<string> setTitle)
        {
            InitializeComponent();

            _getViewModel = getViewModel;
            _setTitle = setTitle;

            _autocompleteHandler = new AnimatedListHandler<Sticker>(ListAutocomplete);

            _autocompleteZoomer = new ZoomableListHandler(ListAutocomplete);
            _autocompleteZoomer.Opening = _autocompleteHandler.UnloadVisibleItems;
            _autocompleteZoomer.Closing = _autocompleteHandler.ThrottleVisibleItems;
            _autocompleteZoomer.DownloadFile = fileId => ViewModel.ProtoService.DownloadFile(fileId, 32);
            _autocompleteZoomer.SessionId = () => ViewModel.ProtoService.SessionId;

            NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;

            _loadedThemeTask = new TaskCompletionSource<bool>();

            _typeToItemHashSetMapping.Add("UserMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ChatFriendMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("FriendMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ServiceMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ServiceMessagePhotoTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ServiceMessageUnreadTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("EmptyMessageTemplate", new HashSet<SelectorItem>());

            _typeToTemplateMapping.Add("UserMessageTemplate", Resources["UserMessageTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ChatFriendMessageTemplate", Resources["ChatFriendMessageTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("FriendMessageTemplate", Resources["FriendMessageTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ServiceMessageTemplate", Resources["ServiceMessageTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ServiceMessagePhotoTemplate", Resources["ServiceMessagePhotoTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ServiceMessageUnreadTemplate", Resources["ServiceMessageUnreadTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("EmptyMessageTemplate", Resources["EmptyMessageTemplate"] as DataTemplate);

            _windowContext = TLWindowContext.GetForCurrentView();

            if (_windowContext.ContactPanel != null)
            {
                _myPeople = true;
                _windowContext.ContactPanel.LaunchFullAppRequested += ContactPanel_LaunchFullAppRequested;
            }

            Messages.ViewVisibleMessages = ViewVisibleMessages;
            Messages.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);

            InitializeAutomation();
            InitializeStickers();

            //ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();
            ElementCompositionPreview.SetIsTranslationEnabled(Ellipse, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ButtonMore, true);
            ElementCompositionPreview.SetIsTranslationEnabled(TextFieldPanel, true);
            ElementCompositionPreview.SetIsTranslationEnabled(btnAttach, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ListAutocomplete, true);

            _ellipseVisual = ElementCompositionPreview.GetElementVisual(Ellipse);
            _elapsedVisual = ElementCompositionPreview.GetElementVisual(ElapsedPanel);
            _slideVisual = ElementCompositionPreview.GetElementVisual(SlidePanel);
            _recordVisual = ElementCompositionPreview.GetElementVisual(ChatRecord);
            _rootVisual = ElementCompositionPreview.GetElementVisual(TextArea);

            _ellipseVisual.CenterPoint = new Vector3(60);
            _ellipseVisual.Scale = new Vector3(0);

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

            _elapsedTimer = new DispatcherTimer();
            _elapsedTimer.Interval = TimeSpan.FromMilliseconds(100);
            _elapsedTimer.Tick += (s, args) =>
            {
                ElapsedLabel.Text = btnVoiceMessage.Elapsed.ToString("m\\:ss\\.ff");
            };

            _slowModeTimer = new DispatcherTimer();
            _slowModeTimer.Interval = TimeSpan.FromSeconds(1);
            _slowModeTimer.Tick += (s, args) =>
            {
                var fullInfo = ViewModel.CacheService.GetSupergroupFull(ViewModel.Chat);
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

            var visual = DropShadowEx.Attach(ArrowShadow, 2);
            visual.Offset = new Vector3(0, 1, 0);

            visual = DropShadowEx.Attach(ArrowMentionsShadow, 2);
            visual.Offset = new Vector3(0, 1, 0);

            visual = DropShadowEx.Attach(ArrowReactionsShadow, 2);
            visual.Offset = new Vector3(0, 1, 0);

            //if (ApiInformation.IsMethodPresent("Windows.UI.Xaml.Hosting.ElementCompositionPreview", "SetImplicitShowAnimation"))
            //{
            //    var showShowAnimation = Window.Current.Compositor.CreateSpringScalarAnimation();
            //    showShowAnimation.InitialValue = 0;
            //    showShowAnimation.FinalValue = 1;
            //    showShowAnimation.Target = nameof(Visual.Opacity);

            //    var hideHideAnimation = Window.Current.Compositor.CreateSpringScalarAnimation();
            //    hideHideAnimation.InitialValue = 1;
            //    hideHideAnimation.FinalValue = 0;
            //    hideHideAnimation.Target = nameof(Visual.Opacity);

            //    ElementCompositionPreview.SetImplicitShowAnimation(ManagePanel, showShowAnimation);
            //    ElementCompositionPreview.SetImplicitHideAnimation(ManagePanel, hideHideAnimation);
            //}

            _textShadowVisual = DropShadowEx.Attach(Separator);
            _textShadowVisual.IsVisible = false;

            //TextField.Language = Native.NativeUtils.GetCurrentCulture();
            _drawable ??= new AvatarWavesDrawable(true, true);
            _drawable.Update((Color)BootStrapper.Current.Resources["SystemAccentColor"], true);
        }

        private void OnNavigatedTo()
        {
            if (_windowContext.ContactPanel != null)
            {
                Header.Visibility = Visibility.Collapsed;
                FindName("BackgroundPresenter");
                BackgroundPresenter.Update(ViewModel.SessionId, ViewModel.ProtoService, ViewModel.Aggregator);
            }
            else if (!Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().IsMain)
            {
                FindName("BackgroundPresenter");
                BackgroundPresenter.Update(ViewModel.SessionId, ViewModel.ProtoService, ViewModel.Aggregator);
            }

            GroupCall.InitializeParent(ClipperOuter, ViewModel.ProtoService);
            JoinRequests.InitializeParent(ClipperJoinRequests, ViewModel.ProtoService);
            ActionBar.InitializeParent(ClipperActionBar);
            PinnedMessage.InitializeParent(Clipper);

            switch (ViewModel.Settings.Stickers.SelectedTab)
            {
                case Services.Settings.StickersTab.Emoji:
                    ButtonStickers.Glyph = Icons.Emoji;
                    break;
                case Services.Settings.StickersTab.Animations:
                    ButtonStickers.Glyph = Icons.Gif;
                    break;
                case Services.Settings.StickersTab.Stickers:
                    ButtonStickers.Glyph = Icons.Sticker;
                    break;
            }
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

            StickersPanel.AnimationClick = Animations_ItemClick;
            StickersPanel.AnimationContextRequested += Animation_ContextRequested;

            _stickersPanel = ElementCompositionPreview.GetElementVisual(StickersPanel.Presenter);
            _stickersShadow = ElementCompositionPreview.GetElementChildVisual(StickersPanel.Shadow);

            _stickersTimer = new DispatcherTimer();
            _stickersTimer.Interval = TimeSpan.FromMilliseconds(300);
            _stickersTimer.Tick += (s, args) =>
            {
                _stickersTimer.Stop();

                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                Collapse_Click(StickersPanel, null);
                TextField.Focus(FocusState.Programmatic);
            };

            ButtonStickers.PointerEntered += Stickers_PointerEntered;
            ButtonStickers.PointerExited += Stickers_PointerExited;

            StickersPanel.PointerEntered += Stickers_PointerEntered;
            StickersPanel.PointerExited += Stickers_PointerExited;

            StickersPanel.AllowFocusOnInteraction = true;
        }

        public void HideStickers()
        {
            if (_stickersMode == StickersPanelMode.Overlay)
            {
                Collapse_Click(StickersPanel, null);
            }

            TextField.Focus(FocusState.Programmatic);
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

        private void Stickers_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Touch)
            {
                _stickersTimer.Start();
            }
            else if (_stickersTimer.IsEnabled)
            {
                _stickersTimer.Stop();
            }
        }

        private void Stickers_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (_stickersTimer.IsEnabled)
            {
                _stickersTimer.Stop();
            }

            if (StickersPanel.Visibility == Visibility.Visible)
            {
                return;
            }

            _stickersMode = StickersPanelMode.Overlay;
            ButtonStickers.IsChecked = false;
            SettingsService.Current.IsSidebarOpen = false;

            Focus(FocusState.Programmatic);
            TextField.Focus(FocusState.Programmatic);

            CoreInputView.GetForCurrentView().TryHide();

            _stickersPanel.Opacity = 0;
            _stickersPanel.Clip = Window.Current.Compositor.CreateInsetClip(48, 48, 0, 0);

            _stickersShadow.Opacity = 0;
            _stickersShadow.Clip = Window.Current.Compositor.CreateInsetClip(48, 48, -48, -4);

            StickersPanel.Visibility = Visibility.Visible;
            StickersPanel.Activate();

            ViewModel.OpenStickersCommand.Execute(null);

            var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 1);

            var clip = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(0, 48);
            clip.InsertKeyFrame(1, 0);

            var clipShadow = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            clipShadow.InsertKeyFrame(0, 48);
            clipShadow.InsertKeyFrame(1, -48);

            _stickersPanel.StartAnimation("Opacity", opacity);
            _stickersPanel.Clip.StartAnimation("LeftInset", clip);
            _stickersPanel.Clip.StartAnimation("TopInset", clip);

            _stickersShadow.StartAnimation("Opacity", opacity);
            _stickersShadow.Clip.StartAnimation("LeftInset", clipShadow);
            _stickersShadow.Clip.StartAnimation("TopInset", clipShadow);
        }

        public void OnNavigatingFrom(Type sourcePageType)
        {
            var unallowed = sourcePageType != typeof(ChatPage)
                && sourcePageType != typeof(ChatEventLogPage)
                && sourcePageType != typeof(ChatPinnedPage)
                && sourcePageType != typeof(ChatScheduledPage)
                && sourcePageType != typeof(ChatThreadPage);

            if (unallowed && Theme.Current.Update(ActualTheme, null))
            {
                var background = ViewModel.ProtoService.GetSelectedBackground(ActualTheme == ElementTheme.Dark);

                _backgroundPresenter ??= FindBackgroundPresenter();
                _backgroundPresenter?.Update(background, ActualTheme == ElementTheme.Dark);
            }
        }

        private ChatBackgroundPresenter FindBackgroundPresenter()
        {
            if (BackgroundPresenter != null)
            {
                return BackgroundPresenter;
            }

            var masterDetailPanel = this.Ancestors<MasterDetailPanel>().FirstOrDefault();
            if (masterDetailPanel != null)
            {
                return masterDetailPanel.Descendants<ChatBackgroundPresenter>().FirstOrDefault();
            }

            return null;
        }

#pragma warning disable CA1063 // Implement IDisposable Correctly
        public void Dispose()
#pragma warning restore CA1063 // Implement IDisposable Correctly
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
                ViewModel.ListField = null;
                ViewModel.Sticker_Click = null;
            }
        }

        private void OnMessageSliceLoaded(object sender, EventArgs e)
        {
            if (sender is DialogViewModel viewModel)
            {
                viewModel.MessageSliceLoaded -= OnMessageSliceLoaded;
            }

            _updateThemeTask?.TrySetResult(true);
            Bindings.Update();
        }

        public void Activate(int sessionId)
        {
            DataContext = _viewModel = _getViewModel(this, sessionId);

            _updateThemeTask = new TaskCompletionSource<bool>();
            ViewModel.MessageSliceLoaded += OnMessageSliceLoaded;
            ViewModel.TextField = TextField;
            ViewModel.ListField = Messages;
            ViewModel.Sticker_Click = Stickers_ItemClick;

            ViewModel.SetText(null, false);

            Messages.SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);

            CheckMessageBoxEmpty();

            SearchMask?.Update(ViewModel.Search);

            ViewModel.PropertyChanged += OnPropertyChanged;
            ViewModel.Items.AttachChanged = OnAttachChanged;
            ViewModel.Items.CollectionChanged += OnCollectionChanged;

            //Playback.Update(ViewModel.CacheService, ViewModel.PlaybackService, ViewModel.NavigationService);

            UpdateTextAreaRadius();

            TextField.IsReplaceEmojiEnabled = ViewModel.Settings.IsReplaceEmojiEnabled;
            TextField.IsTextPredictionEnabled = !SettingsService.Current.DisableHighlightWords;
            TextField.IsSpellCheckEnabled = !SettingsService.Current.DisableHighlightWords;
            TextField.Focus(FocusState.Programmatic);

            StickersPanel.MaxWidth = SettingsService.Current.IsAdaptiveWideEnabled ? 664 : double.PositiveInfinity;

            //Header.BackVisibility = ViewModel.NavigationService.CanGoBack
            //    ? Visibility.Visible
            //    : Visibility.Collapsed;
            Options.Visibility = ViewModel.Type == DialogType.History
                ? Visibility.Visible
                : Visibility.Collapsed;

            OnNavigatedTo();
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
                if (owner == null)
                {
                    owner = _measurement = new MessageBubble();
                }

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
                var animateSendout = !message.IsChannelPost
                    && message.IsOutgoing
                    && message.SendingState is MessageSendingStatePending
                    && message.Content is MessageText or MessageDice or MessageAnimatedEmoji
                    && message.GeneratedContent is MessageBigEmoji or MessageSticker or null;

                await Messages.ItemsStack.UpdateLayoutAsync();

                if (message.IsOutgoing && message.SendingState is MessageSendingStatePending && !Messages.IsBottomReached)
                {
                    Messages.ScrollingHost.ChangeView(null, Messages.ScrollingHost.ScrollableHeight, null);
                }

                var withinViewport = panel.FirstVisibleIndex <= args.NewStartingIndex && panel.LastVisibleIndex >= args.NewStartingIndex;
                if (withinViewport is false)
                {
                    if (animateSendout && ViewModel.ComposerHeader == null)
                    {
                        ShowHideComposerHeader(false);
                    }

                    return;
                }

                if (animateSendout && ViewModel.ComposerHeader == null)
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

                        if (messages.Clip is InsetClip messagesClip)
                        {
                            messagesClip.BottomInset = -SettingsService.Current.Appearance.BubbleRadius;
                        }
                    };

                    _collectionChanging++;
                    Canvas.SetZIndex(TextArea, -1);

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
                    var visual = ElementCompositionPreview.GetElementVisual(child);

                    if (i == args.NewStartingIndex && animateSendout)
                    {
                        var bubble = owner.Descendants<MessageBubble>().FirstOrDefault();
                        var reply = message.ReplyToMessageState != ReplyToMessageState.Hidden && message.ReplyToMessageId != 0;
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
                            MessageSticker => 20 / (200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f)),
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
                    _backgroundPresenter ??= FindBackgroundPresenter();
                    _backgroundPresenter?.UpdateBackground();
                }
            }
        }

        private void OnAttachChanged(IEnumerable<MessageViewModel> items)
        {
            foreach (var message in items)
            {
                if (message == null)
                {
                    continue;
                }

                var container = Messages.ContainerFromItem(message) as SelectorItem;
                if (container == null)
                {
                    continue;
                }

                var content = container.ContentTemplateRoot as FrameworkElement;
                if (content == null)
                {
                    continue;
                }

                if (content is not MessageBubble)
                {
                    var photo = content.FindName("Photo") as ProfilePicture;
                    if (photo != null)
                    {
                        photo.Visibility = message.IsLast ? Visibility.Visible : Visibility.Collapsed;
                    }

                    content = content.FindName("Bubble") as FrameworkElement;
                }
                else if (content is StackPanel panel)
                {
                    content = panel.FindName("Service") as FrameworkElement;
                }

                if (content is MessageBubble bubble)
                {
                    bubble.UpdateAttach(message);
                    bubble.UpdateMessageHeader(message);
                }
                else if (content is MessageService && container.ContentTemplateRoot is FrameworkElement)
                {
                    //root.Margin = new Thickness(0, message.IsFirst ? 8 : 4, 0, 0);
                }
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("Reply"))
            {
                CheckMessageBoxEmpty();
            }
            else if (e.PropertyName.Equals("Search"))
            {
                SearchMask.Update(ViewModel.Search);
            }
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var user = ViewModel.ProtoService.GetUser(chat);
                if (user == null || user.ProfilePhoto == null)
                {
                    return;
                }

                var userFull = ViewModel.ProtoService.GetUserFull(user.Id);
                if (userFull?.Photo == null)
                {
                    return;
                }

                var viewModel = new UserPhotosViewModel(ViewModel.ProtoService, ViewModel.StorageService, ViewModel.Aggregator, user, userFull);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                var basicGroupFull = ViewModel.ProtoService.GetBasicGroupFull(chat);
                if (basicGroupFull?.Photo == null)
                {
                    return;
                }

                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, ViewModel.StorageService, ViewModel.Aggregator, chat, basicGroupFull.Photo);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
            }
            else if (chat.Type is ChatTypeSupergroup)
            {
                var supergroupFull = ViewModel.ProtoService.GetSupergroupFull(chat);
                if (supergroupFull?.Photo == null)
                {
                    return;
                }

                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, ViewModel.StorageService, ViewModel.Aggregator, chat, supergroupFull.Photo);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
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
            WindowContext.GetForCurrentView().AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;

            UnloadVisibleMessages();
            ViewVisibleMessages(false);



            TextField.Focus(FocusState.Programmatic);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            //Bindings.StopTracking();

            UnloadVisibleMessages();

            Window.Current.Activated -= Window_Activated;
            Window.Current.VisibilityChanged -= Window_VisibilityChanged;

            Window.Current.CoreWindow.CharacterReceived -= OnCharacterReceived;
            WindowContext.GetForCurrentView().AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (Window.Current.CoreWindow.ActivationMode == CoreWindowActivationMode.ActivatedInForeground)
            {
                ViewVisibleMessages(true);

                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                TextField.Focus(FocusState.Programmatic);
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

                TextField.Focus(FocusState.Programmatic);
            }
        }

        public void Search()
        {
            ViewModel.SearchCommand.Execute();
        }

        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            var character = Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));
            if (character.Length == 0 || (char.IsControl(character[0]) && character != "\u0016") || char.IsWhiteSpace(character[0]))
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement();
            if (focused == null || (focused is TextBox == false && focused is RichEditBox == false))
            {
                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
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

        private void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (args.EventType is not CoreAcceleratorKeyEventType.KeyDown and not CoreAcceleratorKeyEventType.SystemKeyDown)
            {
                return;
            }

            var alt = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            var ctrl = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            var shift = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            if (args.VirtualKey == Windows.System.VirtualKey.Delete && ViewModel.SelectionMode != ListViewSelectionMode.None && ViewModel.SelectedItems != null && ViewModel.SelectedItems.Count > 0)
            {
                ViewModel.MessagesDeleteCommand.Execute();
                args.Handled = true;
            }
            else if (args.VirtualKey == Windows.System.VirtualKey.C && ctrl && !alt && !shift && ViewModel.SelectionMode != ListViewSelectionMode.None && ViewModel.SelectedItems != null && ViewModel.SelectedItems.Count > 0)
            {
                ViewModel.MessagesCopyCommand.Execute();
                args.Handled = true;
            }
            else if (args.VirtualKey == Windows.System.VirtualKey.R && args.KeyStatus.RepeatCount == 1 && ctrl && !alt && !shift)
            {
                btnVoiceMessage.ToggleRecording();
                args.Handled = true;
            }
            else if (args.VirtualKey == Windows.System.VirtualKey.D && args.KeyStatus.RepeatCount == 1 && ctrl && !alt && !shift)
            {
                btnVoiceMessage.StopRecording(true);
                args.Handled = true;
            }
            else if (args.VirtualKey == Windows.System.VirtualKey.Escape && !ctrl && !alt && !shift)
            {
                if (ViewModel.ComposerHeader != null && ViewModel.ComposerHeader.ReplyToMessage != null)
                {
                    ViewModel.ClearReplyCommand.Execute(null);
                    args.Handled = true;
                }
            }
            else if ((args.VirtualKey == Windows.System.VirtualKey.PageUp) && !ctrl && !alt && !shift && TextField.Document.Selection.StartPosition == 0 && ViewModel.Autocomplete == null)
            {
                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                var focused = FocusManager.GetFocusedElement();
                if (focused is Selector or SelectorItem or Microsoft.UI.Xaml.Controls.ItemsRepeater or ChatCell or PlaybackSlider)
                {
                    return;
                }

                if (args.VirtualKey == Windows.System.VirtualKey.Up && (focused is TextBox || focused is RichEditBox))
                {
                    return;
                }

                var panel = Messages.ItemsPanelRoot as ItemsStackPanel;
                if (panel == null)
                {
                    return;
                }

                SelectorItem target;
                if (args.VirtualKey == Windows.System.VirtualKey.PageUp)
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
            else if ((args.VirtualKey == Windows.System.VirtualKey.PageDown || args.VirtualKey == Windows.System.VirtualKey.Down) && !ctrl && !alt && !shift && TextField.Document.Selection.StartPosition == TextField.Document.GetRange(int.MaxValue, int.MaxValue).EndPosition && ViewModel.Autocomplete == null)
            {
                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                var focused = FocusManager.GetFocusedElement();
                if (focused is Selector or SelectorItem or Microsoft.UI.Xaml.Controls.ItemsRepeater or ChatCell or PlaybackSlider)
                {
                    return;
                }

                if (args.VirtualKey == Windows.System.VirtualKey.Down && (focused is TextBox || focused is RichEditBox))
                {
                    return;
                }

                var panel = Messages.ItemsPanelRoot as ItemsStackPanel;
                if (panel == null)
                {
                    return;
                }

                SelectorItem target;
                if (args.VirtualKey == Windows.System.VirtualKey.PageUp)
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

        public void OnBackRequested(HandledEventArgs args)
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

            if (ViewModel.SelectionMode != ListViewSelectionMode.None)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
                args.Handled = true;
            }

            if (ViewModel.ComposerHeader != null && ViewModel.ComposerHeader.EditingMessage != null)
            {
                ViewModel.ClearReplyCommand.Execute(null);
                args.Handled = true;
            }

            if (ViewModel.Autocomplete != null)
            {
                ViewModel.Autocomplete = null;
                args.Handled = true;
            }

            if (args.Handled)
            {
                Focus(FocusState.Programmatic);
                TextField.Focus(FocusState.Programmatic);
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

            TryGetWebPagePreview(viewModel.ProtoService, viewModel.Chat, text, result =>
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

                        viewModel.ComposerHeader = new MessageComposerHeader
                        {
                            EditingMessage = embedded?.EditingMessage,
                            ReplyToMessage = embedded?.ReplyToMessage,
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
                            viewModel.ComposerHeader = new MessageComposerHeader
                            {
                                EditingMessage = embedded.EditingMessage,
                                ReplyToMessage = embedded.ReplyToMessage
                            };
                        }
                    }
                });
            });
        }

        private void TryGetWebPagePreview(IProtoService protoService, Chat chat, string text, Action<BaseObject> result)
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

                    protoService.Send(new GetWebPagePreview(new FormattedText(urls, new TextEntity[0])), result);
                }
                else
                {
                    result(null);
                }
            }
            else
            {
                protoService.Send(new GetWebPagePreview(new FormattedText(text.Format(), new TextEntity[0])), result);
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

            if (ViewModel.CacheService.IsRepliesChat(chat))
            {
                return;
            }

            ViewModel.NavigationService.Navigate(typeof(ProfilePage), chat.Id, infoOverride: new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            //var restricted = await ViewModel.VerifyRightsAsync(chat, x => x.CanSendMediaMessages, Strings.Resources.AttachMediaRestrictedForever, Strings.Resources.AttachMediaRestricted);
            //if (restricted)
            //{
            //    return;
            //}

            var flyout = FlyoutBase.GetAttachedFlyout(ButtonAttach) as MenuFlyout;
            if (flyout != null)
            {
                flyout.ShowAt(ButtonAttach, new FlyoutShowOptions { Placement = FlyoutPlacementMode.TopEdgeAlignedLeft });
            }
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
            if (sender is MessageReferenceBase referenceBase)
            {
                var message = referenceBase.MessageId;
                if (message != 0)
                {
                    await ViewModel.LoadMessageSliceAsync(null, message);

                    ViewModel.LockedPinnedMessageId = message;
                    UpdatePinnedMessage();
                }
            }
            else
            {
                var reference = sender as MessageReference;
                var message = reference.MessageId;

                if (message != 0)
                {
                    await ViewModel.LoadMessageSliceAsync(null, message);
                }
            }
        }

        private void ReplyMarkup_ButtonClick(object sender, ReplyMarkupButtonClickEventArgs e)
        {
            if (sender is ReplyMarkupPanel panel)
            {
                ViewModel.KeyboardButtonExecute(panel.DataContext as MessageViewModel, e.Button);
            }
        }

        private void Stickers_Click(object sender, RoutedEventArgs e)
        {
            Stickers_PointerEntered(sender, null);
        }

        private void Commands_Click(object sender, RoutedEventArgs e)
        {
            TextField.SetText("/", null);
            TextField.Focus(FocusState.Keyboard);
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
            ReplyMarkupPanel.Visibility = Visibility.Collapsed;

            ButtonMarkup.Glyph = Icons.AppFolder;
            Automation.SetToolTip(ButtonMarkup, Strings.Resources.AccDescrBotCommands);

            if (keyboard)
            {
                Focus(FocusState.Programmatic);
                TextField.Focus(FocusState.Keyboard);

                CoreInputView.GetForCurrentView().TryShow();
            }
        }

        public void ShowMarkup()
        {
            ReplyMarkupPanel.Visibility = Visibility.Visible;

            ButtonMarkup.Glyph = Icons.ChevronDown;
            Automation.SetToolTip(ButtonMarkup, Strings.Resources.AccDescrShowKeyboard);

            Focus(FocusState.Programmatic);
            TextField.Focus(FocusState.Programmatic);

            CoreInputView.GetForCurrentView().TryHide();
        }

        private void TextField_Tapped(object sender, TappedRoutedEventArgs e)
        {
            CoreInputView.GetForCurrentView().TryShow();
        }

        private void Participant_Click(object sender, RoutedEventArgs e)
        {
            var control = sender as FrameworkElement;

            var message = control.Tag as MessageViewModel;
            if (message == null)
            {
                return;
            }

            if (message.IsSaved())
            {
                if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
                {
                    ViewModel.OpenUser(fromUser.SenderUserId);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChat fromChat)
                {
                    ViewModel.OpenChat(fromChat.SenderChatId, true);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel)
                {
                    // TODO: verify if this is sufficient
                    ViewModel.OpenChat(fromChannel.ChatId);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser)
                {
                    Window.Current.ShowTeachingTip(sender as FrameworkElement, Strings.Resources.HidAccount);
                    //await MessagePopup.ShowAsync(Strings.Resources.HidAccount, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
            else if (ViewModel.CacheService.TryGetChat(message.SenderId, out Chat senderChat))
            {
                if (senderChat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                {
                    ViewModel.OpenChat(senderChat.Id);
                }
                else
                {
                    ViewModel.OpenChat(senderChat.Id, true);
                }
            }
            else if (message.SenderId is MessageSenderUser senderUser)
            {
                ViewModel.OpenUser(senderUser.UserId);
            }
        }

        private void Action_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var message = button.Tag as MessageViewModel;

            if (message == null)
            {
                return;
            }

            if (message.IsSaved())
            {
                if (message.ForwardInfo?.Origin is MessageForwardOriginUser or MessageForwardOriginChat)
                {
                    ViewModel.NavigationService.NavigateToChat(message.ForwardInfo.FromChatId, message.ForwardInfo.FromMessageId);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel)
                {
                    ViewModel.NavigationService.NavigateToChat(fromChannel.ChatId, fromChannel.MessageId);
                }
            }
            else
            {
                ViewModel.MessageForwardCommand.Execute(message);
            }
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            _selectionFromItemClick = true;
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_selectionFromItemClick && Messages.SelectedItems.Count < 1)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
            }

            _selectionFromItemClick = false;
            ViewModel.ExpandSelection(Messages.SelectedItems.Cast<MessageViewModel>());
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

            //var user = chat.Type is ChatTypePrivate privata ? ViewModel.ProtoService.GetUser(privata.UserId) : null;
            var user = ViewModel.CacheService.GetUser(chat);
            var secret = chat.Type is ChatTypeSecret;
            var basicGroup = chat.Type is ChatTypeBasicGroup basicGroupType ? ViewModel.ProtoService.GetBasicGroup(basicGroupType.BasicGroupId) : null;
            var supergroup = chat.Type is ChatTypeSupergroup supergroupType ? ViewModel.ProtoService.GetSupergroup(supergroupType.SupergroupId) : null;

            flyout.CreateFlyoutItem(ViewModel.SearchCommand, Strings.Resources.Search, new FontIcon { Glyph = Icons.Search }, Windows.System.VirtualKey.F);

            if (user != null && !secret)
            {
                flyout.CreateFlyoutItem(ViewModel.SetThemeCommand, Strings.Resources.ChangeColors, new FontIcon { Glyph = Icons.PaintBrush });
            }

            if (supergroup != null && supergroup.Status is not ChatMemberStatusCreator && (supergroup.IsChannel || !string.IsNullOrEmpty(supergroup.Username)))
            {
                flyout.CreateFlyoutItem(ViewModel.ReportCommand, Strings.Resources.ReportChat, new FontIcon { Glyph = Icons.ShieldError });
            }
            if (user != null && user.Id != ViewModel.CacheService.Options.MyId)
            {
                if (!user.IsContact && !LastSeenConverter.IsServiceUser(user) && !LastSeenConverter.IsSupportUser(user))
                {
                    if (!string.IsNullOrEmpty(user.PhoneNumber))
                    {
                        flyout.CreateFlyoutItem(ViewModel.AddContactCommand, Strings.Resources.AddToContacts, new FontIcon { Glyph = Icons.PersonAdd });
                    }
                    else
                    {
                        flyout.CreateFlyoutItem(ViewModel.ShareContactCommand, Strings.Resources.ShareMyContactInfo, new FontIcon { Glyph = Icons.Share });
                    }
                }
            }
            if (user != null || (basicGroup != null && basicGroup.CanDeleteMessages()) || (supergroup != null && supergroup.CanDeleteMessages()))
            {
                flyout.CreateFlyoutItem(ViewModel.SetTimerCommand, Strings.Resources.SetTimer, new FontIcon { Glyph = Icons.Timer });
            }
            if (ViewModel.SelectionMode != ListViewSelectionMode.Multiple)
            {
                if (user != null || basicGroup != null || (supergroup != null && !supergroup.IsChannel && string.IsNullOrEmpty(supergroup.Username)))
                {
                    flyout.CreateFlyoutItem(ViewModel.ChatClearCommand, Strings.Resources.ClearHistory, new FontIcon { Glyph = Icons.Broom });
                }
                if (user != null)
                {
                    flyout.CreateFlyoutItem(ViewModel.ChatDeleteCommand, Strings.Resources.DeleteChatUser, new FontIcon { Glyph = Icons.Delete });
                }
                if (basicGroup != null)
                {
                    flyout.CreateFlyoutItem(ViewModel.ChatDeleteCommand, Strings.Resources.DeleteAndExit, new FontIcon { Glyph = Icons.Delete });
                }
            }
            if ((user != null && user.Id != ViewModel.CacheService.Options.MyId) || basicGroup != null || (supergroup != null && !supergroup.IsChannel))
            {
                var muted = ViewModel.CacheService.Notifications.GetMutedFor(chat) > 0;
                flyout.CreateFlyoutItem(
                    muted ? ViewModel.UnmuteCommand : ViewModel.MuteCommand,
                    muted ? Strings.Resources.UnmuteNotifications : Strings.Resources.MuteNotifications,
                    new FontIcon { Glyph = muted ? Icons.Alert : Icons.AlertOff });
            }

            //if (currentUser == null || !currentUser.IsSelf)
            //{
            //    this.muteItem = this.headerItem.addSubItem(18, null);
            //}
            //else if (currentUser.IsSelf)
            //{
            //    CreateFlyoutItem(ref flyout, null, Strings.Resources.AddShortcut);
            //}
            if (user != null && user.Type is UserTypeBot)
            {
                var fullInfo = ViewModel.ProtoService.GetUserFull(user.Id);
                if (fullInfo != null)
                {
                    if (fullInfo.Commands.Any(x => x.Command.Equals("Settings")))
                    {
                        flyout.CreateFlyoutItem(() => { }, Strings.Resources.BotSettings);
                    }

                    if (fullInfo.Commands.Any(x => x.Command.Equals("help")))
                    {
                        flyout.CreateFlyoutItem(() => { }, Strings.Resources.BotHelp);
                    }
                }
            }

            var hidden = ViewModel.Settings.GetChatPinnedMessage(chat.Id);
            if (hidden != 0)
            {
                flyout.CreateFlyoutItem(ViewModel.PinnedShowCommand, Strings.Resources.PinnedMessages, new FontIcon { Glyph = Icons.Pin });
            }

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(sender as Button, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
            }
        }

        private void Send_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (ViewModel.Type != DialogType.History)
            {
                return;
            }

            var self = ViewModel.CacheService.IsSavedMessages(chat);

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(new RelayCommand(async () => await TextField.SendAsync(true)), Strings.Resources.SendWithoutSound, new FontIcon { Glyph = Icons.AlertOff });
            flyout.CreateFlyoutItem(new RelayCommand(async () => await TextField.ScheduleAsync()), self ? Strings.Resources.SetReminder : Strings.Resources.ScheduleMessage, new FontIcon { Glyph = Icons.CalendarClock });

            flyout.ShowAt(sender, new FlyoutShowOptions { Placement = FlyoutPlacementMode.TopEdgeAlignedRight });
        }

        private async void Message_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var message = element.Tag as MessageViewModel;
            if (message == null && sender is SelectorItem selector && selector.ContentTemplateRoot is FrameworkElement content)
            {
                element = content;
                message = content.Tag as MessageViewModel;

                if (content is not MessageBubble)
                {
                    element = content.FindName("Bubble") as FrameworkElement;
                }
                else if (content is StackPanel panel)
                {
                    element = panel.FindName("Service") as FrameworkElement;
                }
            }

            if (message == null || message.Id == 0)
            {
                return;
            }

            var chat = message.GetChat();
            if (chat == null)
            {
                return;
            }

            if (args.TryGetPosition(Window.Current.Content as FrameworkElement, out Point point))
            {
                var children = VisualTreeHelper.FindElementsInHostCoordinates(point, element);

                var textBlock = children.FirstOrDefault() as RichTextBlock;
                if (textBlock != null)
                {
                    MessageHelper.Hyperlink_ContextRequested(ViewModel.TranslateService, textBlock, args);

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

                if (message.Content is MessageAlbum album)
                {
                    var child = children.FirstOrDefault(x => x is IContent) as IContent;
                    if (child != null)
                    {
                        message = child.Message;
                    }
                    else
                    {
                        message = album.Messages.FirstOrDefault() ?? message;
                    }
                }
            }
            else if (message.Content is MessageAlbum album && args.OriginalSource is DependencyObject originaSource)
            {
                var ancestor = originaSource.AncestorsAndSelf<IContent>().FirstOrDefault();
                if (ancestor != null)
                {
                    message = ancestor.Message;
                }
            }

            var response = await ViewModel.ProtoService.SendAsync(new GetMessage(message.ChatId, message.Id));
            if (response is Message)
            {
                message.UpdateWith(response as Message);
            }

            var selected = ViewModel.SelectedItems;
            if (selected.Count > 0)
            {
                if (selected.Contains(message))
                {
                    flyout.CreateFlyoutItem(ViewModel.MessagesForwardCommand, "Forward Selected", new FontIcon { Glyph = Icons.Share });

                    if (chat.CanBeReported)
                    {
                        flyout.CreateFlyoutItem(ViewModel.MessagesReportCommand, "Report Selected", new FontIcon { Glyph = Icons.ShieldError });
                    }

                    flyout.CreateFlyoutItem(ViewModel.MessagesDeleteCommand, "Delete Selected", new FontIcon { Glyph = Icons.Delete });
                    flyout.CreateFlyoutItem(ViewModel.MessagesUnselectCommand, "Clear Selection");
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(ViewModel.MessagesCopyCommand, "Copy Selected as Text", new FontIcon { Glyph = Icons.DocumentCopy });
                }
                else
                {
                    flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.MessageSelectCommand, message, Strings.Resources.lng_context_select_msg, new FontIcon { Glyph = Icons.CheckmarkCircle });
                }
            }
            else if (message.SendingState is MessageSendingStateFailed or MessageSendingStatePending)
            {
                if (message.SendingState is MessageSendingStateFailed)
                {
                    flyout.CreateFlyoutItem(MessageRetry_Loaded, ViewModel.MessageRetryCommand, message, Strings.Resources.Retry, new FontIcon { Glyph = Icons.Retry });
                }

                flyout.CreateFlyoutItem(MessageCopy_Loaded, ViewModel.MessageCopyCommand, message, Strings.Resources.Copy, new FontIcon { Glyph = Icons.DocumentCopy });
                flyout.CreateFlyoutItem(MessageDelete_Loaded, ViewModel.MessageDeleteCommand, message, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
            }
            else
            {
                // Scheduled
                flyout.CreateFlyoutItem(MessageSendNow_Loaded, ViewModel.MessageSendNowCommand, message, Strings.Resources.MessageScheduleSend, new FontIcon { Glyph = Icons.Send, FontFamily = new FontFamily("ms-appx:///Assets/Fonts/Telegram.ttf#Telegram") });
                flyout.CreateFlyoutItem(MessageReschedule_Loaded, ViewModel.MessageRescheduleCommand, message, Strings.Resources.MessageScheduleEditTime, new FontIcon { Glyph = Icons.CalendarClock });

                if (message.CanGetViewers && CanGetMessageViewers(chat, message))
                {
                    LoadMessageViewers(message, flyout);
                }

                // Generic
                flyout.CreateFlyoutItem(MessageReply_Loaded, ViewModel.MessageReplyCommand, message, Strings.Resources.Reply, new FontIcon { Glyph = Icons.ArrowReply });

                if (true /*message.Content is MessageText*/)
                {
                    flyout.CreateFlyoutItem(MessageEdit_Loaded, ViewModel.MessageEditCommand, message, Strings.Resources.Edit, new FontIcon { Glyph = Icons.Edit });
                }
                else if (MessageEdit_Loaded(message))
                {
                    var edit = new MenuFlyoutSubItem();
                    edit.Text = Strings.Resources.Edit;
                    edit.Icon = new FontIcon { Glyph = Icons.Edit };

                    var caption = message.Content.GetCaption();
                    if (string.IsNullOrEmpty(caption?.Text))
                    {
                        edit.CreateFlyoutItem(ViewModel.MessageEditCommand, message, "Add a Caption", new FontIcon { Glyph = Icons.Compose });
                    }
                    else
                    {
                        edit.CreateFlyoutItem(ViewModel.MessageEditCommand, message, "Edit Caption", new FontIcon { Glyph = Icons.Compose });
                    }

                    if (message.Content is MessagePhoto)
                    {
                        edit.CreateFlyoutItem(ViewModel.MessageEditCommand, message, "Edit This Photo", new FontIcon { Glyph = Icons.Signature });
                        edit.CreateFlyoutItem(ViewModel.MessageEditCommand, message, "Replace Photo", new FontIcon { Glyph = Icons.Document });
                    }
                    else
                    {
                        edit.CreateFlyoutItem(ViewModel.MessageEditCommand, message, "Replace File", new FontIcon { Glyph = Icons.Document });
                    }

                    flyout.Items.Add(edit);
                }

                flyout.CreateFlyoutItem(MessageThread_Loaded, ViewModel.MessageThreadCommand, message, message.InteractionInfo?.ReplyInfo?.ReplyCount > 0 ? Locale.Declension("ViewReplies", message.InteractionInfo.ReplyInfo.ReplyCount) : Strings.Resources.ViewThread, new FontIcon { Glyph = Icons.Thread, FontFamily = new FontFamily("ms-appx:///Assets/Fonts/Telegram.ttf#Telegram") });

                flyout.CreateFlyoutSeparator();

                // Manage
                flyout.CreateFlyoutItem(MessagePin_Loaded, ViewModel.MessagePinCommand, message, message.IsPinned ? Strings.Resources.UnpinMessage : Strings.Resources.PinMessage, new FontIcon { Glyph = message.IsPinned ? Icons.PinOff : Icons.Pin });
                flyout.CreateFlyoutItem(MessageStatistics_Loaded, ViewModel.MessageStatisticsCommand, message, Strings.Resources.Statistics, new FontIcon { Glyph = Icons.DataUsage });

                flyout.CreateFlyoutItem(MessageForward_Loaded, ViewModel.MessageForwardCommand, message, Strings.Resources.Forward, new FontIcon { Glyph = Icons.Share });
                flyout.CreateFlyoutItem(MessageReport_Loaded, ViewModel.MessageReportCommand, message, Strings.Resources.ReportChat, new FontIcon { Glyph = Icons.ShieldError });
                flyout.CreateFlyoutItem(MessageDelete_Loaded, ViewModel.MessageDeleteCommand, message, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
                flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.MessageSelectCommand, message, Strings.Resources.lng_context_select_msg, new FontIcon { Glyph = Icons.CheckmarkCircle });

                flyout.CreateFlyoutSeparator();

                // Copy
                flyout.CreateFlyoutItem(MessageCopy_Loaded, ViewModel.MessageCopyCommand, message, Strings.Resources.Copy, new FontIcon { Glyph = Icons.DocumentCopy });
                flyout.CreateFlyoutItem(MessageCopyLink_Loaded, ViewModel.MessageCopyLinkCommand, message, Strings.Resources.CopyLink, new FontIcon { Glyph = Icons.Link });
                flyout.CreateFlyoutItem(MessageCopyMedia_Loaded, ViewModel.MessageCopyMediaCommand, message, Strings.Resources.lng_context_copy_image, new FontIcon { Glyph = Icons.Image });

                flyout.CreateFlyoutItem(MessageTranslate_Loaded, ViewModel.MessageTranslateCommand, message, Strings.Resources.TranslateMessage, new FontIcon { Glyph = Icons.Translate });

                flyout.CreateFlyoutSeparator();

                // Stickers
                flyout.CreateFlyoutItem(MessageAddSticker_Loaded, ViewModel.MessageAddStickerCommand, message, Strings.Resources.AddToStickers, new FontIcon { Glyph = Icons.Sticker });
                flyout.CreateFlyoutItem(MessageFaveSticker_Loaded, ViewModel.MessageFaveStickerCommand, message, Strings.Resources.AddToFavorites, new FontIcon { Glyph = Icons.Star });
                flyout.CreateFlyoutItem(MessageUnfaveSticker_Loaded, ViewModel.MessageUnfaveStickerCommand, message, Strings.Resources.DeleteFromFavorites, new FontIcon { Glyph = Icons.StarOff });

                flyout.CreateFlyoutSeparator();

                // Files
                flyout.CreateFlyoutItem(MessageSaveAnimation_Loaded, ViewModel.MessageSaveAnimationCommand, message, Strings.Resources.SaveToGIFs, new FontIcon { Glyph = Icons.Gif });
                flyout.CreateFlyoutItem(MessageSaveMedia_Loaded, ViewModel.MessageSaveMediaCommand, message, Strings.Resources.lng_mediaview_save_as, new FontIcon { Glyph = Icons.SaveAs });
                flyout.CreateFlyoutItem(MessageSaveMedia_Loaded, ViewModel.MessageOpenWithCommand, message, Strings.Resources.OpenInExternalApp, new FontIcon { Glyph = Icons.OpenIn });
                flyout.CreateFlyoutItem(MessageSaveMedia_Loaded, ViewModel.MessageOpenFolderCommand, message, Strings.Resources.lng_context_show_in_folder, new FontIcon { Glyph = Icons.FolderOpen });

                // Contacts
                flyout.CreateFlyoutItem(MessageAddContact_Loaded, ViewModel.MessageAddContactCommand, message, Strings.Resources.AddContactTitle, new FontIcon { Glyph = Icons.Person });
                //CreateFlyoutItem(ref flyout, MessageSaveDownload_Loaded, ViewModel.MessageSaveDownloadCommand, messageCommon, Strings.Resources.SaveToDownloads);

                // Polls
                flyout.CreateFlyoutItem(MessageUnvotePoll_Loaded, ViewModel.MessageUnvotePollCommand, message, Strings.Resources.Unvote, new FontIcon { Glyph = Icons.ArrowUndo });
                flyout.CreateFlyoutItem(MessageStopPoll_Loaded, ViewModel.MessageStopPollCommand, message, Strings.Resources.StopPoll, new FontIcon { Glyph = Icons.LockClosed });

#if DEBUG
                var file = message.GetFile();
                if (file != null)
                {
                    flyout.CreateFlyoutItem(x => true, new RelayCommand<MessageViewModel>(x =>
                    {
                        var file = x.GetFile();
                        if (file == null)
                        {
                            return;
                        }

                        ViewModel.ProtoService.CancelDownloadFile(file.Id);
                        ViewModel.ProtoService.Send(new DeleteFileW(file.Id));
                    }), message, "Delete from disk", new FontIcon { Glyph = Icons.Delete });
                }
#endif

                if (message.CanBeSaved is false && flyout.Items.Count > 0)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.Items.Add(new MenuFlyoutLabel
                    {
                        Padding = new Thickness(12, 4, 12, 4),
                        MaxWidth = 180,
                        Text = message.IsChannelPost
                            ? Strings.Resources.ForwardsRestrictedInfoChannel
                            : Strings.Resources.ForwardsRestrictedInfoGroup
                    });
                }
            }

            //sender.ContextFlyout = menu;

            if (flyout.Items.Count > 0 && flyout.Items[flyout.Items.Count - 1] is MenuFlyoutSeparator and not MenuFlyoutLabel)
            {
                flyout.Items.RemoveAt(flyout.Items.Count - 1);
            }

            if (element is MessageBubble bubble && selected.Count == 0 && args.TryGetPosition(Window.Current.Content, out Point absolute))
            {
                flyout.Opened += async (s, args) =>
                {
                    var response = await message.ProtoService.GetAvailableReactionsAsync(message.Get());
                    if (response.Count > 0 && flyout.IsOpen)
                    {
                        MenuFlyoutReactions.ShowAt(response, message, bubble, flyout, absolute);
                    }
                };
            }

            args.ShowAt(flyout, sender as FrameworkElement);
        }

        private bool CanGetMessageViewers(Chat chat, MessageViewModel message)
        {
            if (chat.LastReadOutboxMessageId < message.Id)
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
                var expirePeriod = ViewModel.ProtoService.Config.GetNamedNumber("chat_read_mark_expire_period", 7 * 86400);
                if (expirePeriod + message.Date > DateTime.UtcNow.ToTimestamp())
                {
                    return true;
                }
            }

            return false;
        }

        private async void LoadMessageViewers(MessageViewModel message, MenuFlyout flyout)
        {
            var played = message.Content is MessageVoiceNote or MessageVideoNote;

            var placeholder = flyout.CreateFlyoutItem(() => { }, "...", new FontIcon { Glyph = played ? Icons.Play : Icons.Seen });
            var separator = flyout.CreateFlyoutSeparator();

            // Width must be fixed because viewers are loaded asynchronously
            placeholder.Width = 240;

            var final = new MenuFlyoutSubItem();
            final.Visibility = Visibility.Collapsed;
            flyout.Items.Insert(0, final);

            var response = await message.ProtoService.SendAsync(new GetMessageViewers(message.ChatId, message.Id));
            if (response is Telegram.Td.Api.Users users && users.UserIds.Count > 0)
            {
                var profiles = message.ProtoService.GetUsers(users.UserIds);

                var pictures = new StackPanel();
                pictures.Orientation = Orientation.Horizontal;

                foreach (var user in profiles.Take(Math.Min(3, profiles.Count)))
                {
                    var picture = new ProfilePicture();
                    picture.Width = 24;
                    picture.Height = 24;
                    picture.IsEnabled = false;
                    picture.SetUser(message.ProtoService, user, 24);
                    picture.Margin = new Thickness(pictures.Children.Count > 0 ? -10 : 0, -2, 0, -2);

                    Canvas.SetZIndex(picture, -pictures.Children.Count);
                    pictures.Children.Add(picture);
                }

                if (profiles.Count > 1)
                {
                    //var final = new MenuFlyoutSubItem();
                    final.Style = App.Current.Resources["MessageSeenMenuFlyoutSubItemStyle"] as Style;
                    final.Text = Locale.Declension(played ? "MessagePlayed" : "MessageSeen", users.UserIds.Count);
                    final.Icon = new FontIcon { Glyph = played ? Icons.Play : Icons.Seen, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily };
                    final.Tag = pictures;

                    // Width must be fixed because viewers are loaded asynchronously
                    final.Width = 240;

                    foreach (var user in message.ProtoService.GetUsers(users.UserIds))
                    {
                        var picture = new ProfilePicture();
                        picture.Width = 24;
                        picture.Height = 24;
                        picture.IsEnabled = false;
                        picture.SetUser(message.ProtoService, user, 24);
                        picture.Margin = new Thickness(-4, -2, 0, -2);

                        var item = final.CreateFlyoutItem(ViewModel.OpenUserCommand, user.Id, user.GetFullName());
                        item.Style = App.Current.Resources["ProfilePictureMenuFlyoutItemStyle"] as Style;
                        item.Icon = new FontIcon();
                        item.Tag = picture;
                    }

                    //flyout.Items.Remove(placeholder);
                    //flyout.Items.Insert(0, final);
                    placeholder.Visibility = Visibility.Collapsed;
                    final.Visibility = Visibility.Visible;
                }
                else if (profiles.Count > 0)
                {
                    placeholder.Style = App.Current.Resources["MessageSeenMenuFlyoutItemStyle"] as Style;
                    placeholder.Text = profiles[0].GetFullName();
                    placeholder.Tag = pictures;
                    placeholder.CommandParameter = profiles[0].Id;
                    placeholder.Command = ViewModel.OpenUserCommand;
                }
            }
            else
            {
                placeholder.Text = Strings.Resources.NobodyViewed;
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

        private bool MessageReply_Loaded(MessageViewModel message)
        {
            if (message.SchedulingState != null || (ViewModel.Type != DialogType.History && ViewModel.Type != DialogType.Thread))
            {
                return false;
            }

            var chat = message.GetChat();
            if (chat != null && chat.Type is ChatTypeSupergroup supergroupType)
            {
                var supergroup = ViewModel.ProtoService.GetSupergroup(supergroupType.SupergroupId);
                if (supergroup.IsChannel)
                {
                    return supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator;
                }
                else if (supergroup.Status is ChatMemberStatusRestricted restricted)
                {
                    return restricted.IsMember && restricted.Permissions.CanSendMessages;
                }
                else if (supergroup.Status is ChatMemberStatusLeft)
                {
                    return ViewModel.Type == DialogType.Thread;
                }

                return supergroup.Status is not ChatMemberStatusLeft;
            }
            else if (chat != null && chat.Id == ViewModel.CacheService.Options.RepliesBotChatId)
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

            if (message.SchedulingState != null || message.IsService())
            {
                return false;
            }

            var chat = message.GetChat();
            if (chat != null && chat.Type is ChatTypeSupergroup supergroupType)
            {
                var supergroup = ViewModel.ProtoService.GetSupergroup(supergroupType.SupergroupId);
                if (supergroup == null)
                {
                    return false;
                }

                if (supergroup.Status is ChatMemberStatusCreator || (supergroup.Status is ChatMemberStatusAdministrator admin && (admin.CanPinMessages || supergroup.IsChannel && admin.CanEditMessages)))
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
                var basicGroup = ViewModel.ProtoService.GetBasicGroup(basicGroupType.BasicGroupId);
                if (basicGroup == null)
                {
                    return false;
                }

                if (basicGroup.Status is ChatMemberStatusCreator || (basicGroup.Status is ChatMemberStatusAdministrator admin && admin.CanPinMessages))
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
            if (chat == null || !chat.CanBeReported)
            {
                return false;
            }

            if (message.IsService())
            {
                return false;
            }

            var myId = ViewModel.CacheService.Options.MyId;
            if (message.SenderId is MessageSenderUser senderUser)
            {
                return senderUser.UserId != myId;
            }

            return true;
        }

        private bool MessageRetry_Loaded(MessageViewModel message)
        {
            if (message.SendingState is MessageSendingStateFailed failed)
            {
                return failed.CanRetry;
            }

            return false;
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

            return false;
        }

        private bool MessageCopyMedia_Loaded(MessageViewModel message)
        {
            if (message.Ttl > 0 || !message.CanBeSaved)
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
                return text.WebPage != null && text.WebPage.IsPhoto();
            }

            return false;
        }

        private bool MessageCopyLink_Loaded(MessageViewModel message)
        {
            var chat = message.GetChat();
            if (chat != null && chat.Type is ChatTypeSupergroup)
            {
                //var supergroup = ViewModel.ProtoService.GetSupergroup(supergroupType.SupergroupId);
                //return !string.IsNullOrEmpty(supergroup.Username);
                return ViewModel.Type is DialogType.History or DialogType.Thread;
            }

            return false;
        }

        private bool MessageSelect_Loaded(MessageViewModel message)
        {
            if (_myPeople || ViewModel.Type == DialogType.EventLog || message.IsService())
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
                return !ViewModel.ProtoService.IsStickerSetInstalled(sticker.Sticker.SetId);
            }
            else if (message.Content is MessageText text && text.WebPage?.Sticker != null && text.WebPage.Sticker.SetId != 0)
            {
                return !ViewModel.ProtoService.IsStickerSetInstalled(text.WebPage.Sticker.SetId);
            }

            return false;
        }

        private bool MessageFaveSticker_Loaded(MessageViewModel message)
        {
            if (message.Content is MessageSticker sticker && sticker.Sticker.SetId != 0)
            {
                return !ViewModel.ProtoService.IsStickerFavorite(sticker.Sticker.StickerValue.Id);
            }

            return false;
        }

        private bool MessageUnfaveSticker_Loaded(MessageViewModel message)
        {
            if (message.Content is MessageSticker sticker && sticker.Sticker.SetId != 0)
            {
                return ViewModel.ProtoService.IsStickerFavorite(sticker.Sticker.StickerValue.Id);
            }

            return false;
        }

        private bool MessageSaveMedia_Loaded(MessageViewModel message)
        {
            if (message.Ttl > 0 || !message.CanBeSaved)
            {
                return false;
            }

            var file = message.GetFile();
            if (file != null && file.Local.IsDownloadingCompleted)
            {
                return true;
            }

            return false;
        }

        private bool MessageSaveDownload_Loaded(MessageViewModel messageCommon)
        {
            return false;
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
                var user = ViewModel.ProtoService.GetUser(contact.Contact.UserId);
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

        private async void Emojis_ItemClick(string emoji)
        {
            TextField.InsertText(emoji);

            await Task.Delay(100);
            TextField.Focus(FocusState.Programmatic);
        }

        private void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Sticker sticker)
            {
                Stickers_ItemClick(sticker);
            }
        }

        public async void Stickers_ItemClick(Sticker sticker)
        {
            ViewModel.StickerSendCommand.Execute(sticker);

            if (_stickersMode == StickersPanelMode.Overlay)
            {
                Collapse_Click(null, null);
            }

            await Task.Delay(100);
            TextField.Focus(FocusState.Programmatic);
        }

        private void Stickers_ChoosingItem(object sender, EventArgs e)
        {
            ViewModel.ChatActionManager.SetTyping(new ChatActionChoosingSticker());
        }

        public async void Animations_ItemClick(Animation animation)
        {
            ViewModel.AnimationSendCommand.Execute(animation);

            if (_stickersMode == StickersPanelMode.Overlay)
            {
                Collapse_Click(null, null);
            }

            await Task.Delay(100);
            TextField.Focus(FocusState.Programmatic);
        }

        private void InlinePanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _textShadowVisual.IsVisible = Math.Round(e.NewSize.Height) > ViewModel.Settings.Appearance.BubbleRadius;
        }

        private void TextArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _rootVisual.Size = e.NewSize.ToVector2();
        }

        private void ElapsedPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var point = _elapsedVisual.Offset;
            point.X = (float)-e.NewSize.Width;

            _elapsedVisual.Offset = point;
            _elapsedVisual.Size = e.NewSize.ToVector2();
        }

        private void SlidePanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var point = _slideVisual.Offset;
            point.X = (float)e.NewSize.Width + 36;

            _slideVisual.Opacity = 0;
            _slideVisual.Offset = point;
            _slideVisual.Size = e.NewSize.ToVector2();
        }

        private void VoiceButton_RecordingStarted(object sender, EventArgs e)
        {
            // TODO: video message
            ChatRecord.Visibility = Visibility.Visible;

            ChatRecordPopup.IsOpen = true;
            ChatRecordGlyph.Text = btnVoiceMessage.Mode == ChatRecordMode.Video
                ? Icons.VideoFilled
                : Icons.MicOnFilled;

            var slideWidth = SlidePanel.ActualSize.X;
            var elapsedWidth = ElapsedPanel.ActualSize.X;

            _slideVisual.Opacity = 1;

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                _elapsedTimer.Start();
                AttachExpression();
            };

            var slideAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            slideAnimation.InsertKeyFrame(0, slideWidth + 36);
            slideAnimation.InsertKeyFrame(1, 0);
            slideAnimation.Duration = TimeSpan.FromMilliseconds(300);

            var elapsedAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            elapsedAnimation.InsertKeyFrame(0, -elapsedWidth);
            elapsedAnimation.InsertKeyFrame(1, 0);
            elapsedAnimation.Duration = TimeSpan.FromMilliseconds(300);

            var visibleAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            visibleAnimation.InsertKeyFrame(0, 0);
            visibleAnimation.InsertKeyFrame(1, 1);

            var ellipseAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            ellipseAnimation.InsertKeyFrame(0, new Vector3(56f / 96f));
            ellipseAnimation.InsertKeyFrame(1, new Vector3(1));
            ellipseAnimation.Duration = TimeSpan.FromMilliseconds(200);

            _slideVisual.StartAnimation("Offset.X", slideAnimation);
            _elapsedVisual.StartAnimation("Offset.X", elapsedAnimation);
            _recordVisual.StartAnimation("Opacity", visibleAnimation);
            _ellipseVisual.StartAnimation("Scale", ellipseAnimation);

            batch.End();

            ViewModel.ChatActionManager.SetTyping(btnVoiceMessage.IsChecked.Value ? new ChatActionRecordingVideoNote() : new ChatActionRecordingVoiceNote());
        }

        private void VoiceButton_RecordingStopped(object sender, EventArgs e)
        {
            //if (btnVoiceMessage.IsLocked)
            //{
            //    Poggers.Visibility = Visibility.Visible;
            //    Poggers.UpdateWaveform(btnVoiceMessage.GetWaveform());
            //    return;
            //}

            AttachExpression();

            var slidePosition = LayoutRoot.ActualSize.X - 48 - 36;
            var difference = slidePosition - ElapsedPanel.ActualSize.X;

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                _elapsedTimer.Stop();

                DetachExpression();

                ChatRecordPopup.IsOpen = false;

                ChatRecord.Visibility = Visibility.Collapsed;
                ButtonCancelRecording.Visibility = Visibility.Collapsed;
                ElapsedLabel.Text = "0:00,0";

                var point = _slideVisual.Offset;
                point.X = _slideVisual.Size.X + 36;

                _slideVisual.Opacity = 0;
                _slideVisual.Offset = point;

                point = _elapsedVisual.Offset;
                point.X = -_elapsedVisual.Size.X;

                _elapsedVisual.Offset = point;

                _ellipseVisual.Properties.TryGetVector3("Translation", out point);
                point.Y = 0;

                _ellipseVisual.Properties.InsertVector3("Translation", point);
            };

            var slideAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            slideAnimation.InsertKeyFrame(0, _slideVisual.Offset.X);
            slideAnimation.InsertKeyFrame(1, -slidePosition);
            slideAnimation.Duration = TimeSpan.FromMilliseconds(200);

            var visibleAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            visibleAnimation.InsertKeyFrame(0, 1);
            visibleAnimation.InsertKeyFrame(1, 0);

            _slideVisual.StartAnimation("Offset.X", slideAnimation);
            _recordVisual.StartAnimation("Opacity", visibleAnimation);

            batch.End();

            ViewModel.ChatActionManager.CancelTyping();

            TextField.Focus(FocusState.Programmatic);
        }

        private void VoiceButton_RecordingLocked(object sender, EventArgs e)
        {
            ChatRecordGlyph.Text = Icons.SendFilled;

            DetachExpression();

            var ellipseAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            ellipseAnimation.InsertKeyFrame(0, -57);
            ellipseAnimation.InsertKeyFrame(1, 0);

            _ellipseVisual.StartAnimation("Translation.Y", ellipseAnimation);

            ButtonCancelRecording.Visibility = Visibility.Visible;
            btnVoiceMessage.Focus(FocusState.Programmatic);

            var point = _slideVisual.Offset;
            point.X = _slideVisual.Size.X + 36;

            _slideVisual.Opacity = 0;
            _slideVisual.Offset = point;
        }

        private void VoiceButton_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            Vector3 point;
            if (btnVoiceMessage.IsLocked || !btnVoiceMessage.IsRecording)
            {
                point = _slideVisual.Offset;
                point.X = 0;

                _slideVisual.Offset = point;

                _ellipseVisual.Properties.TryGetVector3("Translation", out point);
                point.Y = 0;

                _ellipseVisual.Properties.InsertVector3("Translation", point);

                return;
            }

            var cumulative = e.Cumulative.Translation.ToVector2();
            point = _slideVisual.Offset;
            point.X = Math.Min(0, cumulative.X);

            _slideVisual.Offset = point;

            if (point.X < -80)
            {
                e.Complete();
                btnVoiceMessage.StopRecording(true);
                return;
            }

            _ellipseVisual.Properties.TryGetVector3("Translation", out point);
            point.Y = Math.Min(0, cumulative.Y);

            _ellipseVisual.Properties.InsertVector3("Translation", point);

            if (point.Y < -120)
            {
                e.Complete();
                btnVoiceMessage.LockRecording();
            }
        }

        private void ButtonCancelRecording_Click(object sender, RoutedEventArgs e)
        {
            btnVoiceMessage.StopRecording(true);
        }

        private void AttachExpression()
        {
            var elapsedExpression = Window.Current.Compositor.CreateExpressionAnimation("min(0, slide.Offset.X + ((root.Size.X - 48 - 36 - slide.Size.X) - elapsed.Size.X))");
            elapsedExpression.SetReferenceParameter("slide", _slideVisual);
            elapsedExpression.SetReferenceParameter("elapsed", _elapsedVisual);
            elapsedExpression.SetReferenceParameter("root", _rootVisual);

            var ellipseExpression = Window.Current.Compositor.CreateExpressionAnimation("Vector3(max(0, min(1, 1 + slide.Offset.X / (root.Size.X - 48 - 36))), max(0, min(1, 1 + slide.Offset.X / (root.Size.X - 48 - 36))), 1)");
            ellipseExpression.SetReferenceParameter("slide", _slideVisual);
            ellipseExpression.SetReferenceParameter("elapsed", _elapsedVisual);
            ellipseExpression.SetReferenceParameter("root", _rootVisual);

            _elapsedVisual.StopAnimation("Offset.X");
            _elapsedVisual.StartAnimation("Offset.X", elapsedExpression);

            _ellipseVisual.StopAnimation("Scale");
            _ellipseVisual.StartAnimation("Scale", ellipseExpression);
        }

        private void DetachExpression()
        {
            _elapsedVisual.StopAnimation("Offset.X");
            _ellipseVisual.StopAnimation("Scale");
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            TextField.Document.GetText(TextGetOptions.None, out string text);

            if (e.ClickedItem is User user && ChatTextBox.SearchByUsername(text.Substring(0, Math.Min(TextField.Document.Selection.EndPosition, text.Length)), out string username, out int index))
            {
                var adjust = 0;

                string insert;
                if (string.IsNullOrEmpty(user.Username))
                {
                    insert = string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                    adjust = 1;
                }
                else
                {
                    insert = user.Username;
                }

                var range = TextField.Document.GetRange(TextField.Document.Selection.StartPosition - username.Length - adjust, TextField.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                if (string.IsNullOrEmpty(user.Username))
                {
                    range.Link = $"\"tg-user://{user.Id}\"";
                }

                TextField.Document.GetRange(range.EndPosition, range.EndPosition).SetText(TextSetOptions.None, " ");
                TextField.Document.Selection.StartPosition = range.EndPosition + 1;

                if (index == 0 && user.Type is UserTypeBot bot && bot.IsInline)
                {
                    ViewModel.ResolveInlineBot(user.Username);
                }
            }
            else if (e.ClickedItem is UserCommand command)
            {
                var input = text.Substring(0, Math.Min(TextField.Document.Selection.EndPosition, text.Length));
                if (string.IsNullOrEmpty(input))
                {
                    input = "/";
                }

                if (ChatTextBox.SearchByCommand(input, out string initialCommand))
                {
                    var insert = $"/{command.Item.Command}";
                    if (chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup)
                    {
                        var bot = ViewModel.ProtoService.GetUser(command.UserId);
                        if (bot != null && bot.Username.Length > 0)
                        {
                            insert += $"@{bot.Username}";
                        }
                    }

                    var complete = Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Tab).HasFlag(CoreVirtualKeyStates.Down);
                    if (complete)
                    {
                        insert += " ";

                        var start = TextField.Document.Selection.StartPosition - 1 - initialCommand.Length + insert.Length;
                        var range = TextField.Document.GetRange(TextField.Document.Selection.StartPosition - 1 - initialCommand.Length, TextField.Document.Selection.StartPosition);
                        range.SetText(TextSetOptions.None, insert);

                        TextField.Document.Selection.StartPosition = start;
                    }
                    else
                    {
                        TextField.SetText(null, null);
                        ViewModel.SendCommand.Execute(insert);
                    }
                }

                ButtonMore.IsChecked = false;
            }
            else if (e.ClickedItem is string hashtag && ChatTextBox.SearchByHashtag(text.Substring(0, Math.Min(TextField.Document.Selection.EndPosition, text.Length)), out string initial, out _))
            {
                var insert = $"{hashtag} ";
                var start = TextField.Document.Selection.StartPosition - 1 - initial.Length + insert.Length;
                var range = TextField.Document.GetRange(TextField.Document.Selection.StartPosition - 1 - initial.Length, TextField.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                TextField.Document.Selection.StartPosition = start;
            }
            else if (e.ClickedItem is EmojiData emoji && ChatTextBox.SearchByEmoji(text.Substring(0, Math.Min(TextField.Document.Selection.EndPosition, text.Length)), out string replacement, out _))
            {
                var insert = $"{emoji.Value} ";
                var start = TextField.Document.Selection.StartPosition - 1 - replacement.Length + insert.Length;
                var range = TextField.Document.GetRange(TextField.Document.Selection.StartPosition - 1 - replacement.Length, TextField.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                TextField.Document.Selection.StartPosition = start;
            }
            else if (e.ClickedItem is Sticker sticker)
            {
                TextField.SetText(null, null);
                ViewModel.StickerSendExecute(sticker, null, null, text);

                if (_stickersMode == StickersPanelMode.Overlay)
                {
                    Collapse_Click(null, null);
                }
            }
        }

        private void List_SelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                ShowHideManagePanel(false);
            }
            else
            {
                ShowHideManagePanel(true);
            }
        }

        private bool _manageCollapsed;

        private void ShowHideManagePanel(bool show)
        {
            if ((show && ManagePanel.Visibility == Visibility.Visible && !_manageCollapsed) || (!show && (ManagePanel.Visibility == Visibility.Collapsed || _manageCollapsed)))
            {
                return;
            }

            if (show)
            {
                _manageCollapsed = false;
            }
            else
            {
                _manageCollapsed = true;
            }

            var manage = ElementCompositionPreview.GetElementVisual(ManagePanel);
            manage.StopAnimation("Opacity");

            ManagePanel.Visibility = Visibility.Visible;

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
            if (Messages.SelectionMode == ListViewSelectionMode.None)
            {
                return ManageCount.Text;
            }

            return Locale.Declension("messages", count);
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
            return userId != 777000 && userId != 429000 && userId != 4244000 && (userId / 1000 == 333 || userId % 1000 == 0) ? Strings.Resources.GotAQuestion : Strings.Resources.NoMessages;
        }

        private bool ConvertClickEnabled(ListViewSelectionMode mode)
        {
            return mode == ListViewSelectionMode.Multiple;
        }

        #endregion

        private async void Date_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.CommandParameter is int messageDate)
            {
                var date = Converter.DateTime(messageDate);

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

        private void Collapse_Click(object sender, RoutedEventArgs e)
        {
            if (StickersPanel.Visibility == Visibility.Collapsed || _stickersMode == StickersPanelMode.Collapsed)
            {
                return;
            }

            _stickersMode = StickersPanelMode.Collapsed;
            SettingsService.Current.IsSidebarOpen = false;

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                StickersPanel.Visibility = Visibility.Collapsed;
                StickersPanel.Deactivate();
            };

            var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 1);
            opacity.InsertKeyFrame(1, 0);

            var clip = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(0, 0);
            clip.InsertKeyFrame(1, 48);

            var clipShadow = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            clipShadow.InsertKeyFrame(0, -48);
            clipShadow.InsertKeyFrame(1, 48);

            _stickersPanel.StartAnimation("Opacity", opacity);
            _stickersPanel.Clip.StartAnimation("LeftInset", clip);
            _stickersPanel.Clip.StartAnimation("TopInset", clip);

            _stickersShadow.StartAnimation("Opacity", opacity);
            _stickersShadow.Clip.StartAnimation("LeftInset", clip);
            _stickersShadow.Clip.StartAnimation("TopInset", clip);

            batch.End();

            ButtonStickers.IsChecked = false;

            switch (ViewModel.Settings.Stickers.SelectedTab)
            {
                case Services.Settings.StickersTab.Emoji:
                    ButtonStickers.Glyph = Icons.Emoji;
                    break;
                case Services.Settings.StickersTab.Animations:
                    ButtonStickers.Glyph = Icons.Gif;
                    break;
                case Services.Settings.StickersTab.Stickers:
                    ButtonStickers.Glyph = Icons.Sticker;
                    break;
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width >= 500 && ManageCount.OverflowVisibility == Visibility.Collapsed)
            {
                ManageCount.OverflowVisibility = Visibility.Collapsed;
            }
            else if (e.NewSize.Width < 500 && ManageCount.OverflowVisibility == Visibility.Visible)
            {
                ManageCount.OverflowVisibility = Visibility.Collapsed;
            }
        }

        private void ContentPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ListInline.MaxHeight = Math.Min(320, Math.Max(e.NewSize.Height - 48, 0));
            ListAutocomplete.MaxHeight = Math.Min(320, Math.Max(e.NewSize.Height - 48, 0));
        }

        private void Arrow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ElementCompositionPreview.GetElementVisual(sender as UIElement).CenterPoint = new Vector3((float)e.NewSize.Width / 2f, (float)e.NewSize.Height - (float)e.NewSize.Width / 2f, 0);
        }

        private void DateHeaderPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ElementCompositionPreview.GetElementVisual(sender as UIElement).CenterPoint = new Vector3((float)e.NewSize.Width / 2f, (float)e.NewSize.Height / 2f, 0);
        }

        private void Mentions_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            ViewModel.ReadMentionsCommand.Execute();
        }

        private void Reactions_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            ViewModel.ReadMentionsCommand.Execute();
        }

        private void Arrow_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            ViewModel.RepliesStack.Clear();
            ViewModel.PreviousSliceCommand.Execute();
        }

        private void ItemsStackPanel_Loading(FrameworkElement sender, object args)
        {
            sender.MaxWidth = SettingsService.Current.IsAdaptiveWideEnabled ? 664 : double.PositiveInfinity;
            Messages.SetScrollMode();
        }

        private void ServiceMessage_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as FrameworkElement;
            var message = button.Tag as MessageViewModel;

            if (message == null)
            {
                return;
            }

            ViewModel.MessageServiceCommand.Execute(message);
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

                var user = ViewModel.ProtoService.GetUser(userCommand.UserId);
                if (user == null)
                {
                    return;
                }

                photo.SetUser(ViewModel.ProtoService, user, 36);
            }
            else if (args.Item is User user)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                var photo = content.Children[0] as ProfilePicture;
                var title = content.Children[1] as TextBlock;

                var name = title.Inlines[0] as Run;
                var username = title.Inlines[1] as Run;

                name.Text = user.GetFullName();
                username.Text = string.IsNullOrEmpty(user.Username) ? string.Empty : $" @{user.Username}";

                photo.SetUser(ViewModel.ProtoService, user, 36);
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
                var file = sticker?.StickerValue;

                if (file == null)
                {
                    if (content.Children[0] is Image photo)
                    {
                        photo.Source = null;
                    }
                    else if (content.Children[0] is LottieView lottie)
                    {
                        lottie.Source = null;
                    }
                    else if (content.Children[0] is AnimationView video)
                    {
                        video.Source = null;
                    }

                    return;
                }

                if (file.Local.IsDownloadingCompleted)
                {
                    if (content.Children[0] is Border border && border.Child is Image photo)
                    {
                        photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path, 68);
                        ElementCompositionPreview.SetElementChildVisual(content.Children[0], null);
                    }
                    else if (content.Children[0] is LottieView lottie)
                    {
                        lottie.Source = UriEx.ToLocal(file.Local.Path);
                    }
                    else if (content.Children[0] is AnimationView video)
                    {
                        video.Source = new LocalVideoSource(file);
                    }
                }
                else
                {
                    if (content.Children[0] is Image photo)
                    {
                        photo.Source = null;
                    }
                    else if (content.Children[0] is LottieView lottie)
                    {
                        lottie.Source = null;
                    }
                    else if (content.Children[0] is AnimationView video)
                    {
                        video.Source = null;
                    }

                    CompositionPathParser.ParseThumbnail(sticker, out ShapeVisual visual, false);
                    ElementCompositionPreview.SetElementChildVisual(content.Children[0], visual);

                    UpdateManager.Subscribe(content, ViewModel.ProtoService, file, UpdateSticker, true);

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        ViewModel.ProtoService.DownloadFile(file.Id, 1);
                    }
                }
            }
        }

        private void ShowAction(string content, bool enabled)
        {
            LabelAction.Text = content;
            ButtonAction.IsEnabled = enabled;
            ButtonAction.Visibility = Visibility.Visible;
            ChatFooter.Visibility = Visibility.Visible;
            TextArea.Visibility = Visibility.Collapsed;

            ButtonAction.Focus(FocusState.Programmatic);
        }

        private void ShowArea()
        {
            ButtonAction.IsEnabled = false;
            ButtonAction.Visibility = Visibility.Collapsed;
            ChatFooter.Visibility = Visibility.Collapsed;
            TextArea.Visibility = Visibility.Visible;

            TextField.Focus(FocusState.Programmatic);
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

            UpdateChatActionBar(chat);

            UpdateChatUnreadMentionCount(chat, chat.UnreadMentionCount);
            UpdateChatUnreadReactionCount(chat, chat.UnreadReactionCount);
            UpdateChatDefaultDisableNotification(chat, chat.DefaultDisableNotification);

            TypeIcon.Text = chat.Type is ChatTypeSecret ? Icons.LockClosed : string.Empty;
            TypeIcon.Visibility = chat.Type is ChatTypeSecret ? Visibility.Visible : Visibility.Collapsed;

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
                PhotoMore.SetMessageSender(ViewModel.ProtoService, defaultMessageSenderId, 32);
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

            var theme = ViewModel.CacheService.GetChatTheme(chat.ThemeName);
            if (Theme.Current.Update(ActualTheme, theme))
            {
                var background = ActualTheme == ElementTheme.Light ? theme?.LightSettings.Background : theme?.DarkSettings.Background;
                if (background == null)
                {
                    background = ViewModel.ProtoService.GetSelectedBackground(ActualTheme == ElementTheme.Dark);
                }

                if (_loadedThemeTask != null)
                {
                    await _loadedThemeTask.Task;
                }

                _backgroundPresenter ??= FindBackgroundPresenter();
                _backgroundPresenter?.Update(background, ActualTheme == ElementTheme.Dark);
            }
        }

        public void UpdateChatPermissions(Chat chat)
        {
            StickersPanel.UpdateChatPermissions(ViewModel.CacheService, chat);
            ListInline.UpdateChatPermissions(chat);
        }

        public void UpdateChatPendingJoinRequests(Chat chat)
        {
            JoinRequests.UpdateChat(chat);
        }

        public void UpdateChatTitle(Chat chat)
        {
            if (ViewModel.Type == DialogType.Thread)
            {
                var message = ViewModel.Thread?.Messages.LastOrDefault();
                if (message == null || message.InteractionInfo?.ReplyInfo == null)
                {
                    return;
                }

                if (message.SenderId is MessageSenderUser)
                {
                    Title.Text = Locale.Declension("Replies", message.InteractionInfo.ReplyInfo.ReplyCount);
                }
                else if (ViewModel.CacheService.TryGetChat(message.SenderId, out Chat senderChat))
                {
                    if (senderChat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                    {
                        Title.Text = Locale.Declension("Comments", message.InteractionInfo.ReplyInfo.ReplyCount);
                    }
                    else
                    {
                        Title.Text = Locale.Declension("Replies", message.InteractionInfo.ReplyInfo.ReplyCount);
                    }
                }
            }
            else if (ViewModel.Type == DialogType.ScheduledMessages)
            {
                Title.Text = ViewModel.CacheService.IsSavedMessages(chat) ? Strings.Resources.Reminders : Strings.Resources.ScheduledMessages;
            }
            else
            {
                Title.Text = ViewModel.CacheService.GetTitle(chat);
            }

            _setTitle(Title.Text);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            if (ViewModel.Type == DialogType.Thread)
            {
                Photo.Source = PlaceholderHelper.GetGlyph(Icons.ArrowReply, 5, (int)Photo.Width);
            }
            else
            {
                Photo.SetChat(ViewModel.ProtoService, chat, (int)Photo.Width);
            }
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
                Automation.SetToolTip(ButtonSilent, defaultDisableNotification ? Strings.Resources.AccDescrChanSilentOn : Strings.Resources.AccDescrChanSilentOff);

                TextField.PlaceholderText = chat.DefaultDisableNotification
                    ? Strings.Resources.ChannelSilentBroadcast
                    : Strings.Resources.ChannelBroadcast;
            }
        }

        public void UpdateChatActions(Chat chat, IDictionary<MessageSender, ChatAction> actions)
        {
            if (chat.Type is ChatTypePrivate privata && privata.UserId == ViewModel.CacheService.Options.MyId)
            {
                ChatActionIndicator.UpdateAction(null);
                ChatActionPanel.Visibility = Visibility.Collapsed;
                Subtitle.Opacity = 1;
                return;
            }

            if (actions != null && actions.Count > 0 && (ViewModel.Type == DialogType.History || ViewModel.Type == DialogType.Thread))
            {
                ChatActionLabel.Text = InputChatActionManager.GetTypingString(chat, actions, ViewModel.CacheService.GetUser, ViewModel.CacheService.GetChat, out ChatAction commonAction);
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
                var group = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                if (group == null)
                {
                    return;
                }

                if (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator administrator && administrator.CanPostMessages)
                {
                }
                else if (group.Status is ChatMemberStatusLeft)
                {
                }
                else
                {
                    ShowAction(ViewModel.CacheService.Notifications.GetMutedFor(chat) > 0 ? Strings.Resources.ChannelUnmute : Strings.Resources.ChannelMute, true);
                }
            }
        }



        public void UpdateChatUnreadMentionCount(Chat chat, int count)
        {
            if (ViewModel.Type == DialogType.History && count > 0)
            {
                MentionsPanel.Visibility = Visibility.Visible;
                Mentions.Text = count.ToString();
            }
            else
            {
                MentionsPanel.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateChatUnreadReactionCount(Chat chat, int count)
        {
            if (ViewModel.Type == DialogType.History && count > 0)
            {
                ReactionsPanel.Visibility = Visibility.Visible;
                Reactions.Text = count.ToString();
            }
            else
            {
                ReactionsPanel.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateChatReplyMarkup(Chat chat, MessageViewModel message)
        {
            string GetPlaceholder()
            {
                if (ViewModel.CacheService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    if (supergroup.IsChannel)
                    {
                        return chat.DefaultDisableNotification
                            ? Strings.Resources.ChannelSilentBroadcast
                            : Strings.Resources.ChannelBroadcast;
                    }
                    else if (supergroup.Status is ChatMemberStatusCreator creator && creator.IsAnonymous || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.IsAnonymous)
                    {
                        return Strings.Resources.SendAnonymously;
                    }
                }

                return Strings.Resources.TypeMessage;
            }

            if (message?.ReplyMarkup is ReplyMarkupForceReply forceReply && forceReply.IsPersonal)
            {
                ViewModel.ReplyToMessage(message);

                if (forceReply.InputFieldPlaceholder.Length > 0)
                {
                    TextField.PlaceholderText = forceReply.InputFieldPlaceholder;
                }
                else
                {
                    TextField.PlaceholderText = GetPlaceholder();
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
                    }
                    else
                    {
                        TextField.PlaceholderText = GetPlaceholder();
                    }

                    ButtonMarkup.Visibility = Visibility.Visible;
                    ShowMarkup();
                }
                else
                {
                    TextField.PlaceholderText = GetPlaceholder();

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

                AttachMedia.Command = ViewModel.SendMediaCommand;
                AttachDocument.Command = ViewModel.SendDocumentCommand;

                var rights = ViewModel.VerifyRights(chat, x => x.CanSendMediaMessages, Strings.Resources.GlobalAttachMediaRestricted, Strings.Resources.AttachMediaRestrictedForever, Strings.Resources.AttachMediaRestricted, out string label);
                var pollsRights = ViewModel.VerifyRights(chat, x => x.CanSendPolls, Strings.Resources.GlobalAttachMediaRestricted, Strings.Resources.AttachMediaRestrictedForever, Strings.Resources.AttachMediaRestricted, out string pollsLabel);

                var pollsAllowed = chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup;
                if (!pollsAllowed && ViewModel.CacheService.TryGetUser(chat, out User user))
                {
                    pollsAllowed = user.Type is UserTypeBot;
                }

                AttachRestriction.Text = label ?? string.Empty;
                AttachRestriction.Visibility = rights ? Visibility.Visible : Visibility.Collapsed;
                AttachMedia.Visibility = rights ? Visibility.Collapsed : Visibility.Visible;
                AttachDocument.Visibility = rights ? Visibility.Collapsed : Visibility.Visible;
                AttachLocation.Visibility = Visibility.Visible;
                AttachPoll.Visibility = pollsAllowed && !pollsRights ? Visibility.Visible : Visibility.Collapsed;
                AttachContact.Visibility = Visibility.Visible;
                AttachCurrent.Visibility = Visibility.Collapsed;

                ButtonAttach.Glyph = Icons.Attach;
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
                            ButtonAttach.Glyph = Icons.AttachArrowRight;
                            ButtonAttach.IsEnabled = true;
                            break;
                        case MessagePhoto photo:
                            ButtonAttach.Glyph = !photo.IsSecret ? Icons.AttachArrowRight : Icons.Attach;
                            ButtonAttach.IsEnabled = !photo.IsSecret;
                            break;
                        case MessageVideo video:
                            ButtonAttach.Glyph = !video.IsSecret ? Icons.AttachArrowRight : Icons.Attach;
                            ButtonAttach.IsEnabled = !video.IsSecret;
                            break;
                        default:
                            ButtonAttach.Glyph = Icons.Attach;
                            ButtonAttach.IsEnabled = false;
                            break;
                    }

                    AttachMedia.Command = ViewModel.EditMediaCommand;
                    AttachDocument.Command = ViewModel.EditDocumentCommand;

                    AttachRestriction.Visibility = Visibility.Collapsed;
                    AttachMedia.Visibility = Visibility.Visible;
                    AttachDocument.Visibility = Visibility.Visible;
                    AttachLocation.Visibility = Visibility.Collapsed;
                    AttachPoll.Visibility = Visibility.Collapsed;
                    AttachContact.Visibility = Visibility.Collapsed;
                    AttachCurrent.Visibility = editing.Content is MessagePhoto or MessageVideo ? Visibility.Visible : Visibility.Collapsed;

                    ComposerHeaderGlyph.Glyph = Icons.Edit;

                    Automation.SetToolTip(ComposerHeaderCancel, Strings.Resources.AccDescrCancelEdit);

                    SecondaryButtonsPanel.Visibility = Visibility.Collapsed;
                    //ButtonRecord.Visibility = Visibility.Collapsed;

                    //CheckButtonsVisibility();
                }
                else
                {
                    AttachMedia.Command = ViewModel.SendMediaCommand;
                    AttachDocument.Command = ViewModel.SendDocumentCommand;

                    var rights = ViewModel.VerifyRights(chat, x => x.CanSendMediaMessages, Strings.Resources.GlobalAttachMediaRestricted, Strings.Resources.AttachMediaRestrictedForever, Strings.Resources.AttachMediaRestricted, out string label);
                    var pollsRights = ViewModel.VerifyRights(chat, x => x.CanSendPolls, Strings.Resources.GlobalAttachMediaRestricted, Strings.Resources.AttachMediaRestrictedForever, Strings.Resources.AttachMediaRestricted, out string pollsLabel);

                    AttachRestriction.Text = label ?? string.Empty;
                    AttachRestriction.Visibility = rights ? Visibility.Visible : Visibility.Collapsed;
                    AttachMedia.Visibility = rights ? Visibility.Collapsed : Visibility.Visible;
                    AttachDocument.Visibility = rights ? Visibility.Collapsed : Visibility.Visible;
                    AttachLocation.Visibility = Visibility.Visible;
                    AttachPoll.Visibility = (chat.Type is ChatTypeSupergroup || chat.Type is ChatTypeBasicGroup) && !pollsRights ? Visibility.Visible : Visibility.Collapsed;
                    AttachContact.Visibility = Visibility.Visible;
                    AttachCurrent.Visibility = Visibility.Collapsed;

                    ButtonAttach.Glyph = Icons.Attach;
                    ButtonAttach.IsEnabled = true;

                    if (header.WebPagePreview != null)
                    {
                        ComposerHeaderGlyph.Glyph = Icons.Globe;
                    }
                    else if (header.ReplyToMessage != null)
                    {
                        ComposerHeaderGlyph.Glyph = Icons.ArrowReply;
                    }
                    else
                    {
                        ComposerHeaderGlyph.Glyph = Icons.Loading;
                    }

                    Automation.SetToolTip(ComposerHeaderCancel, Strings.Resources.AccDescrCancelReply);

                    SecondaryButtonsPanel.Visibility = Visibility.Visible;
                    //ButtonRecord.Visibility = Visibility.Visible;

                    //CheckButtonsVisibility();
                }
            }
        }

        private bool _composerHeaderCollapsed = false;
        private bool _textFormattingCollapsed = false;
        private bool _botCommandsCollapsed = true;
        private bool _autocompleteCollapsed = true;

        private void ShowHideComposerHeader(bool show, bool sendout = false)
        {
            if (ButtonAction.Visibility == Visibility.Visible)
            {
                _composerHeaderCollapsed = true;
                ComposerHeader.Visibility = Visibility.Collapsed;

                return;
            }

            if ((show && ComposerHeader.Visibility == Visibility.Visible) || (!show && (ComposerHeader.Visibility == Visibility.Collapsed || _composerHeaderCollapsed)))
            {
                return;
            }

            var composer = ElementCompositionPreview.GetElementVisual(ComposerHeader);
            var messages = ElementCompositionPreview.GetElementVisual(Messages);
            var textArea = ElementCompositionPreview.GetElementVisual(TextArea);

            var value = show ? 48 : 0;

            var rect = textArea.Compositor.CreateRoundedRectangleGeometry();
            rect.CornerRadius = new Vector2(SettingsService.Current.Appearance.BubbleRadius);
            rect.Size = new Vector2(TextArea.ActualSize.X, 144);
            rect.Offset = new Vector2(0, value);

            textArea.Clip = textArea.Compositor.CreateGeometricClip(rect);

            if (messages.Clip is InsetClip messagesClip)
            {
                messagesClip.TopInset = value;
                messagesClip.BottomInset = -96;
            }
            else
            {
                messages.Clip = textArea.Compositor.CreateInsetClip(0, value, 0, -96);
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

                if (show)
                {
                    _composerHeaderCollapsed = false;
                }
                else
                {
                    ComposerHeader.Visibility = Visibility.Collapsed;
                }

                UpdateTextAreaRadius();
            };

            var animClip = textArea.Compositor.CreateScalarKeyFrameAnimation();
            animClip.InsertKeyFrame(0, show ? 48 : 0);
            animClip.InsertKeyFrame(1, show ? 0 : 48);
            animClip.Duration = TimeSpan.FromMilliseconds(150);

            var animClip2 = textArea.Compositor.CreateScalarKeyFrameAnimation();
            animClip2.InsertKeyFrame(0, show ? 0 : 48);
            animClip2.InsertKeyFrame(1, show ? 48 : 0);
            animClip2.Duration = TimeSpan.FromMilliseconds(150);

            var animClip3 = textArea.Compositor.CreateVector2KeyFrameAnimation();
            animClip3.InsertKeyFrame(0, new Vector2(0, show ? 48 : 0));
            animClip3.InsertKeyFrame(1, new Vector2(0, show ? 0 : 48));
            animClip3.Duration = TimeSpan.FromMilliseconds(150);

            var anim1 = textArea.Compositor.CreateVector3KeyFrameAnimation();
            anim1.InsertKeyFrame(0, new Vector3(0, show ? 48 : 0, 0));
            anim1.InsertKeyFrame(1, new Vector3(0, show ? 0 : 48, 0));
            anim1.Duration = TimeSpan.FromMilliseconds(150);

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
            if ((show && ButtonMore.Visibility == Visibility.Visible) || (!show && (ButtonMore.Visibility == Visibility.Collapsed || _botCommandsCollapsed)))
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
            offset.Duration = TimeSpan.FromMilliseconds(150);

            var scale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale.InsertKeyFrame(show ? 1 : 0, Vector3.One);
            scale.Duration = TimeSpan.FromMilliseconds(150);

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
            if ((show && ListAutocomplete.Visibility == Visibility.Visible && !_autocompleteCollapsed) || (!show && (ListAutocomplete.Visibility == Visibility.Collapsed || _autocompleteCollapsed)))
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
            offset.Duration = TimeSpan.FromMilliseconds(150);

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
            ButtonManage.CornerRadius = new CornerRadius(4, min, min, 4);
            ButtonForward.CornerRadius = new CornerRadius(min, 4, 4, min);

            ComposerHeaderCancel.CornerRadius = new CornerRadius(4, min, 4, 4);
            TextRoot.CornerRadius = ChatFooter.CornerRadius = ChatRecord.CornerRadius = ManagePanel.CornerRadius = new CornerRadius(radius, radius, 0, 0);

            // It would be cool to have shadow to respect text field corner radius
            //Separator.CornerRadius = new CornerRadius(radius);
            ListAutocomplete.CornerRadius = ListInline.CornerRadius = InlinePanel.CornerRadius = new CornerRadius(radius, radius, 0, 0);
            ListAutocomplete.Padding = new Thickness(0, 0, 0, radius);
            ListInline.UpdateCornerRadius(radius);

            if (radius > 0)
            {
                TextArea.MaxWidth = ChatRecord.MaxWidth = ChatFooter.MaxWidth = ManagePanel.MaxWidth = InlinePanel.MaxWidth = Separator.MaxWidth =
                    SettingsService.Current.IsAdaptiveWideEnabled ? 640 : double.PositiveInfinity;
                TextArea.Margin = ChatRecord.Margin = ChatFooter.Margin = ManagePanel.Margin = Separator.Margin = new Thickness(12, 0, 12, 0);
                InlinePanel.Margin = new Thickness(12, 0, 12, -radius);
            }
            else
            {
                TextArea.MaxWidth = ChatRecord.MaxWidth = ChatFooter.MaxWidth = ManagePanel.MaxWidth = InlinePanel.MaxWidth = Separator.MaxWidth =
                    SettingsService.Current.IsAdaptiveWideEnabled ? 664 : double.PositiveInfinity;
                TextArea.Margin = ChatRecord.Margin = ChatFooter.Margin = ManagePanel.Margin = Separator.Margin = new Thickness();
                InlinePanel.Margin = new Thickness();
            }

            var messages = ElementCompositionPreview.GetElementVisual(Messages);
            if (messages.Clip is InsetClip messagesClip)
            {
                messagesClip.TopInset = -48;
                messagesClip.BottomInset = -48;
            }
            else
            {
                messages.Clip = Window.Current.Compositor.CreateInsetClip(0, -48, 0, -48);
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

            if (!secret)
            {
                ShowArea();
            }

            TextField.PlaceholderText = Strings.Resources.TypeMessage;
            UpdateUserStatus(chat, user);
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            if (ViewModel.Type == DialogType.Pinned)
            {
                ShowAction(Strings.Resources.UnpinAllMessages, true);
            }
            else if (ViewModel.ProtoService.IsRepliesChat(chat))
            {
                ShowAction(ViewModel.CacheService.Notifications.GetMutedFor(chat) > 0 ? Strings.Resources.ChannelUnmute : Strings.Resources.ChannelMute, true);
            }
            else if (chat.IsBlocked)
            {
                ShowAction(user.Type is UserTypeBot ? Strings.Resources.BotUnblock : Strings.Resources.Unblock, true);
            }
            else if (user.Type is UserTypeBot && (accessToken || chat?.LastMessage == null))
            {
                ShowAction(Strings.Resources.BotStart, true);
            }
            else if (!secret)
            {
                ShowArea();
            }

            if (fullInfo.Commands.Count > 0)
            {
                ViewModel.BotCommands = fullInfo.Commands.Select(x => new UserCommand(user.Id, x)).ToList();
                ViewModel.HasBotCommands = false;
                ShowHideBotCommands(true);
            }
            else
            {
                ViewModel.BotCommands = null;
                ViewModel.HasBotCommands = false;
                ShowHideBotCommands(false);
            }

            Automation.SetToolTip(Call, Strings.Resources.Call);

            Call.Glyph = Icons.Phone;
            Call.Visibility = /*!secret &&*/ fullInfo.CanBeCalled ? Visibility.Visible : Visibility.Collapsed;
            VideoCall.Visibility = /*!secret &&*/ fullInfo.CanBeCalled && fullInfo.SupportsVideoCalls ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            if (ViewModel.CacheService.IsSavedMessages(user))
            {
                ViewModel.LastSeen = null;
            }
            else if (ViewModel.CacheService.IsRepliesChat(chat))
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
                ShowAction(string.Format(Strings.Resources.AwaitingEncryption, ViewModel.ProtoService.GetTitle(chat)), false);
            }
            else if (secretChat.State is SecretChatStateClosed)
            {
                ShowAction(Strings.Resources.EncryptionRejected, false);
            }
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
            {
                ShowAction(Strings.Resources.DeleteThisGroup, true);

                ViewModel.LastSeen = Strings.Resources.YouLeft;
            }
            else if (group.Status is ChatMemberStatusCreator creator && !creator.IsMember)
            {
                ShowAction(Strings.Resources.ChannelJoin, true);
            }
            else
            {
                if (ViewModel.Type == DialogType.Pinned)
                {
                    if (group.CanPinMessages())
                    {
                        ShowAction(Strings.Resources.UnpinAllMessages, true);
                    }
                    else
                    {
                        ShowAction(Strings.Resources.HidePinnedMessages, true);
                    }
                }
                else
                {
                    ShowArea();
                }

                TextField.PlaceholderText = Strings.Resources.TypeMessage;

                ViewModel.LastSeen = Locale.Declension("Members", group.MemberCount);
            }
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ViewModel.LastSeen = Locale.Declension("Members", fullInfo.Members.Count);

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
            if (ViewModel.Type == DialogType.EventLog)
            {
                ShowAction(Strings.Resources.Settings, true);
                return;
            }

            if (ViewModel.Type == DialogType.Pinned)
            {
                if (group.CanPinMessages())
                {
                    ShowAction(Strings.Resources.UnpinAllMessages, true);
                }
                else
                {
                    ShowAction(Strings.Resources.HidePinnedMessages, true);
                }
            }
            else if (group.IsChannel || group.IsBroadcastGroup)
            {
                if ((group.Status is ChatMemberStatusLeft && (group.Username.Length > 0 || ViewModel.CacheService.IsChatAccessible(chat))) || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
                {
                    ShowAction(Strings.Resources.ChannelJoin, true);
                }
                else if (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator administrator && administrator.CanPostMessages)
                {
                    ShowArea();
                }
                else if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
                {
                    ShowAction(Strings.Resources.DeleteChat, true);
                }
                else
                {
                    ShowAction(ViewModel.CacheService.Notifications.GetMutedFor(chat) > 0 ? Strings.Resources.ChannelUnmute : Strings.Resources.ChannelMute, true);
                }
            }
            else
            {
                if ((group.Status is ChatMemberStatusLeft && (group.Username.Length > 0 || group.HasLocation || group.HasLinkedChat || ViewModel.CacheService.IsChatAccessible(chat))) || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
                {
                    if (ViewModel.Type == DialogType.Thread)
                    {
                        if (!chat.Permissions.CanSendMessages)
                        {
                            ShowAction(Strings.Resources.GlobalSendMessageRestricted, false);
                        }
                        else
                        {
                            ShowArea();
                        }
                    }
                    else
                    {
                        ShowAction(Strings.Resources.ChannelJoin, true);
                    }
                }
                else if (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator administrator)
                {
                    ShowArea();
                }
                else if (group.Status is ChatMemberStatusRestricted restrictedSend)
                {
                    if (!restrictedSend.IsMember && group.Username.Length > 0)
                    {
                        ShowAction(Strings.Resources.ChannelJoin, true);
                    }
                    else if (!restrictedSend.Permissions.CanSendMessages)
                    {
                        if (restrictedSend.IsForever())
                        {
                            ShowAction(Strings.Resources.SendMessageRestrictedForever, false);
                        }
                        else
                        {
                            ShowAction(string.Format(Strings.Resources.SendMessageRestricted, Converter.BannedUntil(restrictedSend.RestrictedUntilDate)), false);
                        }
                    }
                    else
                    {
                        ShowArea();
                    }
                }
                else if (group.Status is ChatMemberStatusLeft or ChatMemberStatusBanned)
                {
                    ShowAction(Strings.Resources.DeleteChat, true);
                }
                else if (!chat.Permissions.CanSendMessages)
                {
                    ShowAction(Strings.Resources.GlobalSendMessageRestricted, false);
                }
                else
                {
                    ShowArea();
                }
            }

            if (group.IsChannel)
            {
                TextField.PlaceholderText = chat.DefaultDisableNotification
                    ? Strings.Resources.ChannelSilentBroadcast
                    : Strings.Resources.ChannelBroadcast;
            }
            else if (group.Status is ChatMemberStatusCreator creator && creator.IsAnonymous || group.Status is ChatMemberStatusAdministrator administrator && administrator.IsAnonymous)
            {
                TextField.PlaceholderText = Strings.Resources.SendAnonymously;
            }
            else
            {
                TextField.PlaceholderText = Strings.Resources.TypeMessage;
            }

            if (ViewModel.Type == DialogType.History)
            {
                ViewModel.LastSeen = Locale.Declension(group.IsChannel ? "Subscribers" : "Members", group.MemberCount);
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
                ViewModel.LastSeen = Locale.Declension(group.IsChannel ? "Subscribers" : "Members", fullInfo.MemberCount);
            }
            else
            {
                ViewModel.LastSeen = null;
            }

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
                Automation.SetToolTip(Call, Strings.Resources.VoipGroupJoinCall);

                Call.Glyph = Icons.VideoChat;
                Call.Visibility = Visibility.Visible;
            }
        }



        public void UpdateFile(File file)
        {
            var header = ViewModel.ComposerHeader;
            if (header?.EditingMessageFileId == file.Id)
            {
                var size = Math.Max(file.Size, file.ExpectedSize);
                ComposerHeaderUpload.Value = (double)file.Remote.UploadedSize / size;
            }
        }

        private void UpdateSticker(object target, File file)
        {
            var content = target as Grid;
            if (content == null)
            {
                return;
            }

            if (content.Children[0] is Border border && border.Child is Image photo)
            {
                photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path, 68);
                ElementCompositionPreview.SetElementChildVisual(content.Children[0], null);
            }
            else if (content.Children[0] is LottieView lottie)
            {
                lottie.Source = UriEx.ToLocal(file.Local.Path);
                _autocompleteHandler.ThrottleVisibleItems();
            }
            else if (content.Children[0] is AnimationView video)
            {
                video.Source = new LocalVideoSource(file);
                _autocompleteHandler.ThrottleVisibleItems();
            }
        }

        #endregion

        private void TextField_Sending(object sender, EventArgs e)
        {
            if (_stickersMode == StickersPanelMode.Overlay)
            {
                Collapse_Click(StickersPanel, null);
            }
        }

        private AvatarWavesDrawable _drawable;

        private void VoiceButton_QuantumProcessed(object sender, float amplitude)
        {
            _drawable ??= new AvatarWavesDrawable(true, true);
            _drawable.SetAmplitude(amplitude * 100, ChatRecordCanvas);
        }

        private void ChatRecordCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            _drawable.Draw(args.DrawingSession, 60, 60, sender);
        }

        private void ChatRecordLocked_Click(object sender, RoutedEventArgs e)
        {
            btnVoiceMessage.Release();
        }

        private async void ButtonMore_Checked(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null || chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                return;
            }

            var flyout = new MenuFlyout();
            flyout.Items.Add(new MenuFlyoutLabel { Text = Strings.Resources.SendMessageAsTitle });
            flyout.Closing += (s, args) =>
            {
                ButtonMore.IsChecked = false;
                TextField.Focus(FocusState.Programmatic);
            };

            var response = await ViewModel.ProtoService.SendAsync(new GetChatAvailableMessageSenders(chat.Id));
            if (response is MessageSenders senders)
            {
                foreach (var messageSender in senders.Senders)
                {
                    var picture = new ProfilePicture();
                    picture.Width = 36;
                    picture.Height = 36;
                    picture.IsEnabled = false;
                    picture.Margin = new Thickness(-4, -2, 0, -2);

                    var item = new MenuFlyoutProfile();
                    item.CommandParameter = messageSender;
                    item.Command = ViewModel.SetSenderCommand;
                    item.Style = App.Current.Resources["SendAsMenuFlyoutItemStyle"] as Style;
                    item.Icon = new FontIcon();
                    item.Tag = picture;

                    if (ViewModel.ProtoService.TryGetUser(messageSender, out User senderUser))
                    {
                        picture.SetUser(ViewModel.ProtoService, senderUser, 36);

                        item.Text = senderUser.GetFullName();
                        item.Info = Strings.Resources.VoipGroupPersonalAccount;
                    }
                    else if (ViewModel.ProtoService.TryGetChat(messageSender, out Chat senderChat))
                    {
                        picture.SetChat(ViewModel.ProtoService, senderChat, 36);

                        item.Text = senderChat.Title;

                        if (ViewModel.CacheService.TryGetSupergroup(senderChat, out Supergroup supergroup))
                        {
                            item.Info = Locale.Declension("Subscribers", supergroup.MemberCount);
                        }
                    }

                    flyout.Items.Add(item);
                }
            }

            flyout.ShowAt(ButtonMore, new FlyoutShowOptions { Placement = FlyoutPlacementMode.TopEdgeAlignedLeft });
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
                view.Invalidate();
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
                        float interpolation = f7 * Unigram.Charts.CubicBezierInterpolator.EASE_OUT.getInterpolation(wavesEnter);
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
                        view.Invalidate();
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
                float interpolation2 = f7 * Unigram.Charts.CubicBezierInterpolator.EASE_OUT.getInterpolation(wavesEnter);
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
                view.Invalidate();
                //canvas.restore();
            }
        }

        public float AvatarScale
        {
            get
            {
                float interpolation = Unigram.Charts.CubicBezierInterpolator.EASE_OUT.getInterpolation(wavesEnter);
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
            view.Invalidate();
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
            var path = new CanvasPathBuilder(canvas);
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
            return Matrix3x2.CreateRotation(Unigram.Charts.MathFEx.ToRadians(degree), new Vector2(px, py));
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
