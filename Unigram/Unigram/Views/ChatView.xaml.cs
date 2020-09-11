using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
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
using Unigram.Views.Chats;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Views
{
    public sealed partial class ChatView : HostedPage, INavigablePage, ISearchablePage, IDialogDelegate, IActivablePage
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public BindConvert Convert => BindConvert.Current;

        private Func<IDialogDelegate, DialogViewModel> _getViewModel;
        private DialogViewModel _viewModel;

        private readonly TLWindowContext _windowContext;

        private bool _myPeople;

        private bool _selectionFromItemClick;

        private DispatcherTimer _slowModeTimer;

        private DispatcherTimer _stickersTimer;
        private Visual _stickersPanel;
        private StickersPanelMode _stickersMode = StickersPanelMode.Collapsed;
        private StickersPanelMode _stickersModeWide = StickersPanelMode.Sidebar;

        private const double SIDEBAR_MIN_WIDTH = 380 + 320;

        private DispatcherTimer _elapsedTimer;
        private Visual _messageVisual;
        private Visual _ellipseVisual;
        private Visual _elapsedVisual;
        private Visual _slideVisual;
        private Visual _recordVisual;
        private Visual _rootVisual;
        private Visual _textShadowVisual;

        private DispatcherTimer _dateHeaderTimer;
        private Visual _dateHeaderPanel;
        private Visual _dateHeader;

        private Compositor _compositor;

        private ZoomableListHandler _autocompleteZoomer;
        private AnimatedListHandler<Sticker> _autocompleteHandler;

        public ChatView(Func<IDialogDelegate, DialogViewModel> getViewModel)
        {
            InitializeComponent();
            DataContextChanged += (s, args) =>
            {
                _viewModel = ViewModel;
            };
            DataContext = getViewModel(this);
            ViewModel.Sticker_Click = Stickers_ItemClick;

            _autocompleteHandler = new AnimatedListHandler<Sticker>(ListAutocomplete);
            _autocompleteHandler.DownloadFile = (id, sticker) =>
            {
                _viewModel?.ProtoService.DownloadFile(id, 1);
            };

            _autocompleteZoomer = new ZoomableListHandler(ListAutocomplete);
            _autocompleteZoomer.Opening = _autocompleteHandler.UnloadVisibleItems;
            _autocompleteZoomer.Closing = _autocompleteHandler.ThrottleVisibleItems;
            _autocompleteZoomer.DownloadFile = fileId => ViewModel.ProtoService.DownloadFile(fileId, 32);
            _autocompleteZoomer.GetEmojisAsync = fileId => ViewModel.ProtoService.SendAsync(new GetStickerEmojis(new InputFileId(fileId)));

            TextField.IsFormattingVisible = ViewModel.Settings.IsTextFormattingVisible;

            _getViewModel = getViewModel;

            NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;

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

                Header.Visibility = Visibility.Collapsed;
                FindName("BackgroundPresenter");
                BackgroundPresenter.Update(ViewModel.SessionId, ViewModel.ProtoService, ViewModel.Aggregator);
            }
            else if (!Windows.ApplicationModel.Core.CoreApplication.GetCurrentView().IsMain)
            {
                FindName("BackgroundPresenter");
                BackgroundPresenter.Update(ViewModel.SessionId, ViewModel.ProtoService, ViewModel.Aggregator);
            }

            ViewModel.TextField = TextField;
            ViewModel.ListField = Messages;

            CheckMessageBoxEmpty();

            ViewModel.PropertyChanged += OnPropertyChanged;

            InitializeAutomation();
            InitializeStickers();

            Messages.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);

            _messageVisual = ElementCompositionPreview.GetElementVisual(TextField);
            _ellipseVisual = ElementCompositionPreview.GetElementVisual(Ellipse);
            _elapsedVisual = ElementCompositionPreview.GetElementVisual(ElapsedPanel);
            _slideVisual = ElementCompositionPreview.GetElementVisual(SlidePanel);
            _recordVisual = ElementCompositionPreview.GetElementVisual(ButtonRecord);
            _rootVisual = ElementCompositionPreview.GetElementVisual(TextArea);
            _compositor = _slideVisual.Compositor;

            _ellipseVisual.CenterPoint = new Vector3(48);
            _ellipseVisual.Scale = new Vector3(0);

            _rootVisual.Clip = _compositor.CreateInsetClip(0, -100, 0, 0);

            if (DateHeaderPanel != null)
            {
                _dateHeaderTimer = new DispatcherTimer();
                _dateHeaderTimer.Interval = TimeSpan.FromMilliseconds(2000);
                _dateHeaderTimer.Tick += (s, args) =>
                {
                    _dateHeaderTimer.Stop();

                    VisualUtilities.SetIsVisible(DateHeaderPanel, false);
                };

                _dateHeaderPanel = ElementCompositionPreview.GetElementVisual(DateHeaderRelative);
                _dateHeader = ElementCompositionPreview.GetElementVisual(DateHeader);

                _dateHeaderPanel.Clip = _compositor.CreateInsetClip();
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

            var visual = DropShadowEx.Attach(ArrowShadow, 2, 0.25f, null);
            visual.Size = new Vector2(36, 36);
            visual.Offset = new Vector3(0, 1, 0);

            visual = DropShadowEx.Attach(ArrowMentionsShadow, 2, 0.25f, null);
            visual.Size = new Vector2(36, 36);
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

            _textShadowVisual = DropShadowEx.Attach(Separator, 20, 0.25f);
            _textShadowVisual.IsVisible = false;

            //TextField.Language = Native.NativeUtils.GetCurrentCulture();

            return;

            if (ApiInformation.IsEventPresent("Windows.UI.Xaml.Input.FocusManager", "GettingFocus"))
            {
                FocusManager.GettingFocus += (s, args) =>
                {
                    // We want to apply this behavior when using mouse only
                    if (args.InputDevice != FocusInputDeviceKind.Mouse)
                    {
                        return;
                    }

                    // We don't want to steal focus from text areas/keyboard navigation
                    if (args.FocusState == FocusState.Keyboard || args.NewFocusedElement is TextBox || args.NewFocusedElement is RichEditBox)
                    {
                        return;
                    }

                    // We don't want to steal focus from popups
                    if (VisualTreeHelper.GetOpenPopups(Window.Current).Any())
                    {
                        return;
                    }

                    // If new focused element supports programmatic focus (so it's a control)
                    // then we can freely steal focus from it
                    if (args.NewFocusedElement is Control)
                    {
                        if (args.FocusState == FocusState.Programmatic && args.OldFocusedElement is ChatTextBox)
                        {
                            args.TryCancel();
                        }
                        else if (args.FocusState == FocusState.Programmatic)
                        {
                            args.TrySetNewFocusedElement(TextField);
                        }
                        else if (args.OldFocusedElement is ChatTextBox)
                        {
                            args.TryCancel();
                        }
                        else if (args.NewFocusedElement is ChatListViewItem)
                        {
                            args.TrySetNewFocusedElement(TextField);
                        }
                    }
                };
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

            StickersPanel.AnimationClick = Animations_ItemClick;
            StickersPanel.AnimationContextRequested += Animation_ContextRequested;

            _stickersModeWide = SettingsService.Current.IsSidebarOpen
                && SettingsService.Current.Stickers.IsSidebarEnabled
                    ? StickersPanelMode.Sidebar
                    : StickersPanelMode.Collapsed;

            _stickersPanel = ElementCompositionPreview.GetElementVisual(StickersPanel.Presenter);

            _stickersTimer = new DispatcherTimer();
            _stickersTimer.Interval = TimeSpan.FromMilliseconds(300);
            _stickersTimer.Tick += (s, args) =>
            {
                _stickersTimer.Stop();

                if (_stickersMode == StickersPanelMode.Sidebar)
                {
                    return;
                }

                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                Collapse_Click(StickersPanel, null);
                TextField.Focus(FocusState.Programmatic);
            };

            // Not working here
            VisualStateManager.GoToState(this, "FilledState", false);
            StickersPanel.SetView(StickersPanelMode.Overlay);

            ButtonStickers.PointerEntered += Stickers_PointerEntered;
            ButtonStickers.PointerExited += Stickers_PointerExited;

            StickersPanel.PointerEntered += Stickers_PointerEntered;
            StickersPanel.PointerExited += Stickers_PointerExited;

            StickersPanel.AllowFocusOnInteraction = true;

            switch (ViewModel.Settings.Stickers.SelectedTab)
            {
                case Services.Settings.StickersTab.Emoji:
                    ButtonStickers.Glyph = "\uE76E";
                    break;
                case Services.Settings.StickersTab.Animations:
                    ButtonStickers.Glyph = "\uF4A9";
                    break;
                case Services.Settings.StickersTab.Stickers:
                    ButtonStickers.Glyph = "\uF4AA";
                    break;
            }
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
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
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

            if (_stickersMode == StickersPanelMode.Sidebar || StickersPanel.Visibility == Visibility.Visible)
            {
                return;
            }

            _stickersMode = StickersPanelMode.Overlay;
            ButtonStickers.IsChecked = false;
            SettingsService.Current.IsSidebarOpen = false;

            VisualStateManager.GoToState(this, "FilledState", false);
            StickersPanel.SetView(StickersPanelMode.Overlay);

            Focus(FocusState.Programmatic);
            TextField.Focus(FocusState.Programmatic);

            InputPane.GetForCurrentView().TryHide();

            _stickersPanel.Opacity = 0;
            _stickersPanel.Clip = _compositor.CreateInsetClip(48, 48, 0, 0);

            StickersPanel.Visibility = Visibility.Visible;
            StickersPanel.Refresh();

            ViewModel.OpenStickersCommand.Execute(null);

            var opacity = _compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 1);

            var clip = _compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(0, 48);
            clip.InsertKeyFrame(1, 0);

            _stickersPanel.StopAnimation("Opacity");
            _stickersPanel.Clip.StopAnimation("LeftInset");
            _stickersPanel.Clip.StopAnimation("TopInset");

            _stickersPanel.StartAnimation("Opacity", opacity);
            _stickersPanel.Clip.StartAnimation("LeftInset", clip);
            _stickersPanel.Clip.StartAnimation("TopInset", clip);
        }

#pragma warning disable CA1063 // Implement IDisposable Correctly
        public void Dispose()
#pragma warning restore CA1063 // Implement IDisposable Correctly
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= OnPropertyChanged;
                ViewModel.Items.AttachChanged = null;
                //ViewModel.Items.CollectionChanged -= OnCollectionChanged;

                //ViewModel.Items.Dispose();
                //ViewModel.Items.Clear();

                ViewModel.Delegate = null;
                ViewModel.TextField = null;
                ViewModel.ListField = null;
                ViewModel.Sticker_Click = null;

                ViewModel.Dispose();
            }
        }

        public void Activate()
        {
            DataContext = _getViewModel(this);
            Bindings.Update();

            ViewModel.TextField = TextField;
            ViewModel.ListField = Messages;
            ViewModel.Sticker_Click = Stickers_ItemClick;

            ViewModel.SetText(null, false);

            Messages.SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);

            CheckMessageBoxEmpty();

            SearchMask.Update(ViewModel.Search);

            ViewModel.PropertyChanged += OnPropertyChanged;
            //ViewModel.Items.CollectionChanged += OnCollectionChanged;
            ViewModel.Items.AttachChanged = OnAttachChanged;

            //Playback.Update(ViewModel.CacheService, ViewModel.PlaybackService, ViewModel.NavigationService);

            UpdateTextAreaRadius();

            TextField.IsReplaceEmojiEnabled = ViewModel.Settings.IsReplaceEmojiEnabled;
            TextField.IsTextPredictionEnabled = SettingsService.Current.AutocorrectWords;
            TextField.IsSpellCheckEnabled = SettingsService.Current.HighlightWords;
            TextField.Focus(FocusState.Programmatic);

            Options.Visibility = ViewModel.Type == DialogType.Normal
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            var panel = Messages.ItemsStack;
            if (panel == null)
            {
                return;
            }

            if (args.Action == NotifyCollectionChangedAction.Remove && panel.FirstCacheIndex < args.OldStartingIndex && panel.LastCacheIndex >= args.OldStartingIndex)
            {
                //var container = ContainerFromItem(args.OldItems[0]);
                //if (container == null)
                //{
                //    return;
                //}

                var owner = panel.Descendants<SelectorItem>().FirstOrDefault(x => x is SelectorItem item && item.ContentTemplateRoot is FrameworkElement element && element.Tag == args.OldItems[0]) as SelectorItem;
                if (owner == null)
                {
                    return;
                }

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
            else if (args.Action == NotifyCollectionChangedAction.Add && panel.LastVisibleIndex >= args.NewStartingIndex - 1)
            {
                await Messages.ItemsStack.UpdateLayoutAsync();

                var owner = Messages.ContainerFromItem(args.NewItems[0]) as SelectorItem;
                if (owner == null)
                {
                    return;
                }

                owner.Measure(new Size(ActualWidth, ActualHeight));

                var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                var anim = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                anim.InsertKeyFrame(0, new Vector3(0, (float)owner.DesiredSize.Height, 0));
                anim.InsertKeyFrame(1, new Vector3());
                //anim.Duration = TimeSpan.FromSeconds(1);

                for (int i = panel.FirstCacheIndex; i <= args.NewStartingIndex; i++)
                {
                    var container = Messages.ContainerFromIndex(i) as SelectorItem;
                    var child = VisualTreeHelper.GetChild(container, 0) as UIElement;

                    var visual = ElementCompositionPreview.GetElementVisual(child);
                    visual.StartAnimation("Offset", anim);
                }

                batch.End();
            }
        }


        //private void DialogPage_LosingFocus(UIElement sender, LosingFocusEventArgs args)
        //{
        //    if (args.NewFocusedElement is DialogListViewItem && args.OldFocusedElement is BubbleTextBox)
        //    {
        //        args.Cancel = true;
        //        args.Handled = true;
        //    }
        //}

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

                if (content is MessageBubble == false)
                {
                    var photo = content.FindName("Photo") as ProfilePicture;
                    if (photo != null)
                    {
                        photo.Visibility = message.IsLast ? Visibility.Visible : Visibility.Collapsed;
                    }

                    content = content.FindName("Bubble") as FrameworkElement;
                }
                else if (content is StackPanel panel && !(content is MessageBubble))
                {
                    content = panel.FindName("Service") as FrameworkElement;
                }

                if (content is MessageBubble bubble)
                {
                    bubble.UpdateAttach(message);
                    bubble.UpdateMessageHeader(message);
                }
                else if (content is MessageService service)
                {
                    //service.UpdateAttach(item);
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

        private void ShowHideManagePanel(bool show)
        {
            var manage = ElementCompositionPreview.GetElementVisual(ManagePanel);
            var info = ElementCompositionPreview.GetElementVisual(InfoPanel);

            manage.StopAnimation("Offset");
            manage.StopAnimation("Opacity");
            info.StopAnimation("Offset");
            info.StopAnimation("Opacity");

            if ((show && InfoPanel.Visibility == Visibility.Collapsed) || (!show && ManagePanel.Visibility == Visibility.Collapsed))
            {
                return;
            }

            manage.Offset = new Vector3(show ? -32 : 0, 0, 0);
            manage.Opacity = show ? 0 : 1;

            info.Offset = new Vector3(show ? 0 : 32, 0, 0);
            info.Opacity = show ? 1 : 0;

            var batch = manage.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                manage.Offset = new Vector3(show ? 0 : -32, 0, 0);
                manage.Opacity = show ? 1 : 0;

                info.Offset = new Vector3(show ? 32 : 0, 0, 0);
                info.Opacity = show ? 0 : 1;

                if (show)
                {
                    InfoPanel.Visibility = Visibility.Collapsed;
                    ManagePanel.Visibility = Visibility.Visible;
                }
                else
                {
                    InfoPanel.Visibility = Visibility.Visible;
                    ManagePanel.Visibility = Visibility.Collapsed;

                    ViewModel.SelectedItems = new List<MessageViewModel>();
                }
            };

            var offset1 = manage.Compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, new Vector3(-32, 0, 0));
            offset1.InsertKeyFrame(show ? 1 : 0, new Vector3());

            var opacity1 = manage.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);

            var offset2 = manage.Compositor.CreateVector3KeyFrameAnimation();
            offset2.InsertKeyFrame(show ? 0 : 1, new Vector3());
            offset2.InsertKeyFrame(show ? 1 : 0, new Vector3(32, 0, 0));

            var opacity2 = manage.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(show ? 0 : 1, 1);
            opacity2.InsertKeyFrame(show ? 1 : 0, 0);

            manage.StartAnimation("Offset", offset1);
            manage.StartAnimation("Opacity", opacity1);
            info.StartAnimation("Offset", offset2);
            info.StartAnimation("Opacity", opacity2);

            batch.End();

            if (show)
            {
                ManagePanel.Visibility = Visibility.Visible;
            }
            else
            {
                InfoPanel.Visibility = Visibility.Visible;
            }
        }

        private void Manage_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
            }
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                var user = ViewModel.ProtoService.GetUser(chat);
                if (user == null || user.ProfilePhoto == null)
                {
                    return;
                }

                var userFull = ViewModel.ProtoService.GetUserFull(user.Id);
                if (userFull == null)
                {
                    return;
                }

                var viewModel = new UserPhotosViewModel(ViewModel.ProtoService, ViewModel.Aggregator, user, userFull);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
            }
            else if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup)
            {
                if (chat.Photo == null)
                {
                    return;
                }

                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, ViewModel.Aggregator, chat);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            //Bindings.StopTracking();
            //Bindings.Update();

            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;

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

            InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            InputPane.GetForCurrentView().Hiding -= InputPane_Hiding;

            Window.Current.Activated -= Window_Activated;
            Window.Current.VisibilityChanged -= Window_VisibilityChanged;

            Window.Current.CoreWindow.CharacterReceived -= OnCharacterReceived;
            WindowContext.GetForCurrentView().AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            args.EnsuredFocusedElementInView = true;
            KeyboardPlaceholder.Height = new GridLength(args.OccludedRect.Height);
            StickersPanel.Height = args.OccludedRect.Height;
            ReplyMarkupPanel.MaxHeight = args.OccludedRect.Height;
            //ReplyMarkupViewer.MaxHeight = args.OccludedRect.Height;

            Collapse_Click(null, null);
            CollapseMarkup(false);
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            args.EnsuredFocusedElementInView = true;
            KeyboardPlaceholder.Height = new GridLength(1, GridUnitType.Auto);
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (Window.Current.CoreWindow.ActivationMode == CoreWindowActivationMode.ActivatedInForeground)
            {
                ViewVisibleMessages(false);
                StickersPanel.LoadVisibleItems();

                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                TextField.Focus(FocusState.Programmatic);
            }
            else
            {
                UnloadVisibleMessages();
                StickersPanel.UnloadVisibleItems();
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
                    TextField.Document.Selection.Paste(0);
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
            if (args.EventType != CoreAcceleratorKeyEventType.KeyDown && args.EventType != CoreAcceleratorKeyEventType.SystemKeyDown)
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
            else if (args.VirtualKey == Windows.System.VirtualKey.R && ctrl && !alt && !shift)
            {
                btnVoiceMessage.ToggleRecording();
                args.Handled = true;
            }
            else if (args.VirtualKey == Windows.System.VirtualKey.D && ctrl && !alt && !shift)
            {
                btnVoiceMessage.CancelRecording();
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
                if (focused is Selector || focused is SelectorItem || focused is Microsoft.UI.Xaml.Controls.ItemsRepeater || focused is ChatCell)
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
            else if ((args.VirtualKey == Windows.System.VirtualKey.PageDown || args.VirtualKey == Windows.System.VirtualKey.Down) && !ctrl && !alt && !shift && TextField.Document.Selection.StartPosition == TextField.Text?.TrimEnd('\r', '\v').Length && ViewModel.Autocomplete == null)
            {
                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);
                if (popups.Count > 0)
                {
                    return;
                }

                var focused = FocusManager.GetFocusedElement();
                if (focused is Selector || focused is SelectorItem || focused is Microsoft.UI.Xaml.Controls.ItemsRepeater || focused is ChatCell)
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
            if (ReplyMarkupPanel.Visibility == Visibility.Visible && ButtonMarkup.Visibility == Visibility.Visible)
            {
                CollapseMarkup(false);
            }

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
                        if (embedded == null)
                        {
                            viewModel.ComposerHeader = new MessageComposerHeader { WebPagePreview = webPage, WebPageUrl = webPage.Url };
                        }
                        else
                        {
                            viewModel.ComposerHeader = new MessageComposerHeader { EditingMessage = embedded.EditingMessage, ReplyToMessage = embedded.ReplyToMessage, WebPagePreview = webPage, WebPageUrl = webPage.Url };
                        }
                    }
                    else if (embedded != null)
                    {
                        if (embedded.IsEmpty)
                        {
                            viewModel.ComposerHeader = null;
                        }
                        else if (embedded.WebPagePreview != null)
                        {
                            viewModel.ComposerHeader = new MessageComposerHeader { EditingMessage = embedded.EditingMessage, ReplyToMessage = embedded.ReplyToMessage, WebPagePreview = null };
                        }
                    }
                });
            });
        }

        private void TryGetWebPagePreview(IProtoService protoService, Chat chat, string text, Action<BaseObject> result)
        {
            if (chat == null)
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

            if (chat.Type is ChatTypePrivate privata && privata.UserId == ViewModel.CacheService.Options.MyId)
            {
                ViewModel.NavigationService.Navigate(typeof(ChatSharedMediaPage), chat.Id, infoOverride: ApiInfo.CanUseDirectComposition ? new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight } : null);
            }
            else
            {
                ViewModel.NavigationService.Navigate(typeof(ProfilePage), chat.Id, infoOverride: ApiInfo.CanUseDirectComposition ? new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight } : null);
            }
        }

        private async void Attach_Click(object sender, RoutedEventArgs e)
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

            var pane = InputPane.GetForCurrentView();
            if (pane.OccludedRect != Rect.Empty)
            {
                pane.TryHide();

                // TODO: Can't find any better solution
                await Task.Delay(200);
            }

            var flyout = FlyoutBase.GetAttachedFlyout(ButtonAttach) as MenuFlyout;
            if (flyout != null)
            {
                if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "BottomEdgeAlignedRight"))
                {
                    flyout.ShowAt(ButtonAttach, new FlyoutShowOptions { Placement = FlyoutPlacementMode.TopEdgeAlignedLeft });
                }
                else
                {
                    flyout.ShowAt(ButtonAttach);
                }
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
            var reference = sender as MessageReference;
            var message = reference.MessageId;

            if (message != 0)
            {
                await ViewModel.LoadMessageSliceAsync(null, message);
            }

            //if (message != null)
            //{
            //    if (message is TLMessagesContainter container)
            //    {
            //        if (container.EditMessage != null)
            //        {
            //            message = container.EditMessage;
            //        }
            //        else
            //        {
            //            return;
            //        }
            //    }

            //    if (message is TLMessageCommonBase messageCommon)
            //    {
            //        await ViewModel.LoadMessageSliceAsync(null, messageCommon.Id);
            //    }
            //}
        }

        private void ReplyMarkup_ButtonClick(object sender, ReplyMarkupButtonClickEventArgs e)
        {
            var panel = sender as ReplyMarkupPanel;
            if (panel != null)
            {
                ViewModel.KeyboardButtonExecute(panel.DataContext as MessageViewModel, e.Button);
            }
        }

        private StickersPanelMode CurrentPanelMode
        {
            get
            {
                return ActualWidth >= SIDEBAR_MIN_WIDTH
                    && SettingsService.Current.Stickers.IsSidebarEnabled
                        ? StickersPanelMode.Sidebar
                        : StickersPanelMode.Overlay;
            }
        }

        private void Stickers_Click(object sender, RoutedEventArgs e)
        {
            switch (CurrentPanelMode)
            {
                case StickersPanelMode.Overlay:
                    Stickers_PointerEntered(sender, null);
                    break;
                case StickersPanelMode.Sidebar:
                    ShowHideDockedStickersPanel();
                    break;
            }
        }

        private void ShowHideDockedStickersPanel(bool? show = null)
        {
            VisualStateManager.GoToState(this, "SidebarState", false);
            StickersPanel.SetView(StickersPanelMode.Sidebar);

            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (StickersPanel.Visibility == Visibility.Collapsed || _stickersMode == StickersPanelMode.Overlay)
            {
                if (show == false)
                {
                    return;
                }

                _stickersMode = StickersPanelMode.Sidebar;
                ButtonStickers.IsChecked = true;
                SettingsService.Current.IsSidebarOpen = true;

                Focus(FocusState.Programmatic);
                TextField.Focus(FocusState.Programmatic);

                InputPane.GetForCurrentView().TryHide();

                StickersPanel.Visibility = Visibility.Visible;
                StickersPanel.Refresh();

                ViewModel.OpenStickersCommand.Execute(null);

                _stickersPanel.StopAnimation("Opacity");
                _stickersPanel.Clip?.StopAnimation("LeftInset");
                _stickersPanel.Clip?.StopAnimation("TopInset");

                _stickersPanel.Opacity = 1;
                _stickersPanel.Clip = Window.Current.Compositor.CreateInsetClip();
            }
            else
            {
                if (show == true)
                {
                    return;
                }

                Focus(FocusState.Programmatic);
                TextField.Focus(FocusState.Keyboard);

                Collapse_Click(StickersPanel, null);
            }
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

            ButtonMarkup.Glyph = "\uE90F";
            Automation.SetToolTip(ButtonMarkup, Strings.Resources.AccDescrBotCommands);

            if (keyboard)
            {
                Focus(FocusState.Programmatic);
                TextField.Focus(FocusState.Keyboard);

                InputPane.GetForCurrentView().TryShow();
            }
        }

        public void ShowMarkup()
        {
            ReplyMarkupPanel.Visibility = Visibility.Visible;

            ButtonMarkup.Glyph = "\uE910";
            Automation.SetToolTip(ButtonMarkup, Strings.Resources.AccDescrShowKeyboard);

            Focus(FocusState.Programmatic);
            TextField.Focus(FocusState.Programmatic);

            InputPane.GetForCurrentView().TryHide();
        }

        private void TextField_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (ReplyMarkupPanel.Visibility == Visibility.Visible && ButtonMarkup.Visibility == Visibility.Visible)
            {
                CollapseMarkup(false);
            }

            InputPane.GetForCurrentView().TryShow();
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
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel post)
                {
                    // TODO: verify if this is sufficient
                    ViewModel.OpenChat(post.ChatId);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser)
                {
                    Window.Current.ShowTeachingTip(sender as FrameworkElement, Strings.Resources.HidAccount);
                    //await MessagePopup.ShowAsync(Strings.Resources.HidAccount, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
            else if (message.IsChannelPost)
            {
                ViewModel.OpenChat(message.ChatId);
            }
            else
            {
                ViewModel.OpenUser(message.SenderUserId);
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
                if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
                {
                    ViewModel.NavigationService.NavigateToChat(message.ForwardInfo.FromChatId, message.ForwardInfo.FromMessageId);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel post)
                {
                    ViewModel.NavigationService.NavigateToChat(post.ChatId, post.MessageId);
                }
            }
            else
            {
                ViewModel.MessageShareCommand.Execute(message);
            }
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            _selectionFromItemClick = true;
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.Multiple)
            {
                ViewModel.ExpandSelection(Messages.SelectedItems.Cast<MessageViewModel>());

                //if (ViewModel.SelectedItems.IsEmpty())
                //{
                //    ViewModel.SelectionMode = ListViewSelectionMode.None;
                //}
            }

            if (_selectionFromItemClick && Messages.SelectedItems.Count < 1)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
            }

            _selectionFromItemClick = false;
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

            if (supergroup != null && !(supergroup.Status is ChatMemberStatusCreator) && (supergroup.IsChannel || !string.IsNullOrEmpty(supergroup.Username)))
            {
                flyout.CreateFlyoutItem(ViewModel.ReportCommand, Strings.Resources.ReportChat, new FontIcon { Glyph = Icons.Report });
            }
            if (user != null && user.Id != ViewModel.CacheService.Options.MyId)
            {
                if (!user.IsContact)
                {
                    if (!string.IsNullOrEmpty(user.PhoneNumber))
                    {
                        flyout.CreateFlyoutItem(ViewModel.AddContactCommand, Strings.Resources.AddToContacts, new FontIcon { Glyph = Icons.AddUser });
                    }
                    else
                    {
                        flyout.CreateFlyoutItem(ViewModel.ShareContactCommand, Strings.Resources.ShareMyContactInfo, new FontIcon { Glyph = Icons.Share });
                    }
                }
            }
            if (secret)
            {
                flyout.CreateFlyoutItem(ViewModel.SetTimerCommand, Strings.Resources.SetTimer, new FontIcon { Glyph = Icons.Timer });
            }
            if (user != null || basicGroup != null || (supergroup != null && !supergroup.IsChannel && string.IsNullOrEmpty(supergroup.Username)))
            {
                flyout.CreateFlyoutItem(ViewModel.ChatClearCommand, Strings.Resources.ClearHistory, new FontIcon { Glyph = Icons.Clear });
            }
            if (user != null)
            {
                flyout.CreateFlyoutItem(ViewModel.ChatDeleteCommand, Strings.Resources.DeleteChatUser, new FontIcon { Glyph = Icons.Delete });
            }
            if (basicGroup != null)
            {
                flyout.CreateFlyoutItem(ViewModel.ChatDeleteCommand, Strings.Resources.DeleteAndExit, new FontIcon { Glyph = Icons.Delete });
            }
            if ((user != null && user.Id != ViewModel.CacheService.Options.MyId) || basicGroup != null || (supergroup != null && !supergroup.IsChannel))
            {
                var muted = ViewModel.CacheService.GetNotificationSettingsMuteFor(chat) > 0;
                flyout.CreateFlyoutItem(
                    muted ? ViewModel.UnmuteCommand : ViewModel.MuteCommand,
                    muted ? Strings.Resources.UnmuteNotifications : Strings.Resources.MuteNotifications,
                    new FontIcon { Glyph = muted ? Icons.Unmute : Icons.Mute });
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
                    if (fullInfo.BotInfo.Commands.Any(x => x.Command.Equals("Settings")))
                    {
                        flyout.CreateFlyoutItem(null, Strings.Resources.BotSettings);
                    }

                    if (fullInfo.BotInfo.Commands.Any(x => x.Command.Equals("help")))
                    {
                        flyout.CreateFlyoutItem(null, Strings.Resources.BotHelp);
                    }
                }
            }

            if (flyout.Items.Count > 0)
            {
                if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "BottomEdgeAlignedRight"))
                {
                    flyout.Placement = FlyoutPlacementMode.BottomEdgeAlignedRight;
                }

                flyout.ShowAt((Button)sender);
            }
        }

        private void Send_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (ViewModel.Type != DialogType.Normal)
            {
                return;
            }

            var self = ViewModel.CacheService.IsSavedMessages(chat);

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(new RelayCommand(async () => await TextField.SendAsync(true)), Strings.Resources.SendWithoutSound, new FontIcon { Glyph = Icons.Mute });
            flyout.CreateFlyoutItem(new RelayCommand(async () => await TextField.ScheduleAsync()), self ? Strings.Resources.SetReminder : Strings.Resources.ScheduleMessage, new FontIcon { Glyph = Icons.Schedule });

            if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "TopEdgeAlignedRight"))
            {
                flyout.ShowAt(sender, new FlyoutShowOptions { Placement = FlyoutPlacementMode.TopEdgeAlignedRight });
            }
            else
            {
                flyout.ShowAt(sender as FrameworkElement);
            }
        }

        private void Message_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var message = element.Tag as MessageViewModel;
            if (message == null && sender is SelectorItem selector && selector.ContentTemplateRoot is FrameworkElement content)
            {
                element = content;
                message = content.Tag as MessageViewModel;

                if (content is MessageBubble == false)
                {
                    element = content.FindName("Bubble") as FrameworkElement;
                }
                else if (content is StackPanel panel && !(content is MessageBubble))
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
                var textBlock = children.FirstOrDefault(x => x is RichTextBlock) as RichTextBlock;
                if (textBlock != null)
                {
                    MessageHelper.Hyperlink_ContextRequested(message, textBlock, args);

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

            if (message.SendingState is MessageSendingStateFailed || message.SendingState is MessageSendingStatePending)
            {
                if (message.SendingState is MessageSendingStateFailed)
                {
                    flyout.CreateFlyoutItem(MessageRetry_Loaded, ViewModel.MessageRetryCommand, message, Strings.Resources.Retry, new FontIcon { Glyph = Icons.Retry });
                }

                flyout.CreateFlyoutItem(MessageCopy_Loaded, ViewModel.MessageCopyCommand, message, Strings.Resources.Copy, new FontIcon { Glyph = Icons.Copy });
                flyout.CreateFlyoutItem(MessageDelete_Loaded, ViewModel.MessageDeleteCommand, message, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
            }
            else
            {
                // Scheduled
                flyout.CreateFlyoutItem(MessageSendNow_Loaded, ViewModel.MessageSendNowCommand, message, Strings.Resources.MessageScheduleSend, new FontIcon { Glyph = Icons.Send, FontFamily = new FontFamily("ms-appx:///Assets/Fonts/Telegram.ttf#Telegram") });
                flyout.CreateFlyoutItem(MessageReschedule_Loaded, ViewModel.MessageRescheduleCommand, message, Strings.Resources.MessageScheduleEditTime, new FontIcon { Glyph = Icons.Schedule });

                // Generic
                flyout.CreateFlyoutItem(MessageReply_Loaded, ViewModel.MessageReplyCommand, message, Strings.Resources.Reply, new FontIcon { Glyph = Icons.Reply });
                flyout.CreateFlyoutItem(MessageEdit_Loaded, ViewModel.MessageEditCommand, message, Strings.Resources.Edit, new FontIcon { Glyph = Icons.Edit });

                flyout.CreateFlyoutSeparator();

                // Manage
                flyout.CreateFlyoutItem(MessagePin_Loaded, ViewModel.MessagePinCommand, message, chat.PinnedMessageId == message.Id ? Strings.Resources.UnpinMessage : Strings.Resources.PinMessage, new FontIcon { Glyph = chat.PinnedMessageId == message.Id ? Icons.Unpin : Icons.Pin });

                flyout.CreateFlyoutItem(MessageForward_Loaded, ViewModel.MessageForwardCommand, message, Strings.Resources.Forward, new FontIcon { Glyph = Icons.Forward });
                flyout.CreateFlyoutItem(MessageReport_Loaded, ViewModel.MessageReportCommand, message, Strings.Resources.ReportChat, new FontIcon { Glyph = Icons.Report });
                flyout.CreateFlyoutItem(MessageDelete_Loaded, ViewModel.MessageDeleteCommand, message, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
                flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.MessageSelectCommand, message, Strings.Additional.Select, new FontIcon { Glyph = Icons.Select });

                flyout.CreateFlyoutSeparator();

                // Copy
                flyout.CreateFlyoutItem(MessageCopy_Loaded, ViewModel.MessageCopyCommand, message, Strings.Resources.Copy, new FontIcon { Glyph = Icons.Copy });
                flyout.CreateFlyoutItem(MessageCopyLink_Loaded, ViewModel.MessageCopyLinkCommand, message, Strings.Resources.CopyLink, new FontIcon { Glyph = Icons.CopyLink });
                flyout.CreateFlyoutItem(MessageCopyMedia_Loaded, ViewModel.MessageCopyMediaCommand, message, Strings.Additional.CopyImage, new FontIcon { Glyph = Icons.CopyImage });

                flyout.CreateFlyoutSeparator();

                // Stickers
                flyout.CreateFlyoutItem(MessageAddSticker_Loaded, ViewModel.MessageAddStickerCommand, message, Strings.Resources.AddToStickers, new FontIcon { Glyph = Icons.Stickers });
                flyout.CreateFlyoutItem(MessageFaveSticker_Loaded, ViewModel.MessageFaveStickerCommand, message, Strings.Resources.AddToFavorites, new FontIcon { Glyph = Icons.Favorite });
                flyout.CreateFlyoutItem(MessageUnfaveSticker_Loaded, ViewModel.MessageUnfaveStickerCommand, message, Strings.Resources.DeleteFromFavorites, new FontIcon { Glyph = Icons.Unfavorite });

                flyout.CreateFlyoutSeparator();

                // Files
                flyout.CreateFlyoutItem(MessageSaveAnimation_Loaded, ViewModel.MessageSaveAnimationCommand, message, Strings.Resources.SaveToGIFs, new FontIcon { Glyph = Icons.Animations });
                flyout.CreateFlyoutItem(MessageSaveMedia_Loaded, ViewModel.MessageSaveMediaCommand, message, Strings.Additional.SaveAs, new FontIcon { Glyph = Icons.SaveAs });
                flyout.CreateFlyoutItem(MessageSaveMedia_Loaded, ViewModel.MessageOpenWithCommand, message, Strings.Resources.OpenInExternalApp, new FontIcon { Glyph = Icons.OpenIn });
                flyout.CreateFlyoutItem(MessageSaveMedia_Loaded, ViewModel.MessageOpenFolderCommand, message, Strings.Additional.ShowInFolder, new FontIcon { Glyph = Icons.Folder });

                // Contacts
                flyout.CreateFlyoutItem(MessageAddContact_Loaded, ViewModel.MessageAddContactCommand, message, Strings.Resources.AddContactTitle, new FontIcon { Glyph = Icons.Contact });
                //CreateFlyoutItem(ref flyout, MessageSaveDownload_Loaded, ViewModel.MessageSaveDownloadCommand, messageCommon, Strings.Resources.SaveToDownloads);

                // Polls
                flyout.CreateFlyoutItem(MessageUnvotePoll_Loaded, ViewModel.MessageUnvotePollCommand, message, Strings.Resources.Unvote, new FontIcon { Glyph = Icons.Undo });
                flyout.CreateFlyoutItem(MessageStopPoll_Loaded, ViewModel.MessageStopPollCommand, message, Strings.Resources.StopPoll, new FontIcon { Glyph = Icons.Restricted });

#if DEBUG
                flyout.CreateFlyoutItem(x => true, new RelayCommand<MessageViewModel>(x =>
                {
                    var result = x.Get().GetFile();

                    var file = result;
                    if (file == null)
                    {
                        return;
                    }

                    ViewModel.ProtoService.Send(new DeleteFileW(file.Id));

                }), message, "Delete from disk", new FontIcon { Glyph = Icons.Delete });
#endif
            }

            //sender.ContextFlyout = menu;

            if (flyout.Items.Count > 0 && flyout.Items[flyout.Items.Count - 1] is MenuFlyoutSeparator)
            {
                flyout.Items.RemoveAt(flyout.Items.Count - 1);
            }

            args.ShowAt(flyout, sender as FrameworkElement);
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
            if (message.SchedulingState != null || ViewModel.Type != DialogType.Normal)
            {
                return false;
            }

            var chat = message.GetChat();
            if (chat != null && chat.Type is ChatTypeSupergroup supergroupType)
            {
                var supergroup = ViewModel.ProtoService.GetSupergroup(supergroupType.SupergroupId);
                if (supergroup.IsChannel)
                {
                    return supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator;
                }
                else if (supergroup.Status is ChatMemberStatusRestricted restricted)
                {
                    return restricted.Permissions.CanSendMessages;
                }
            }

            return true;
        }

        private bool MessagePin_Loaded(MessageViewModel message)
        {
            if (message.SchedulingState != null || ViewModel.Type != DialogType.Normal || message.IsService())
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

                return supergroup.Status is ChatMemberStatusCreator || (supergroup.Status is ChatMemberStatusAdministrator admin && (admin.CanPinMessages || supergroup.IsChannel && admin.CanEditMessages));
            }
            else if (chat != null && chat.Type is ChatTypeBasicGroup basicGroupType)
            {
                var basicGroup = ViewModel.ProtoService.GetBasicGroup(basicGroupType.BasicGroupId);
                if (basicGroup == null)
                {
                    return false;
                }

                return basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator admin;
            }
            else if (chat != null && chat.Type is ChatTypePrivate privata)
            {
                return privata.UserId == ViewModel.CacheService.Options.MyId;
            }

            return false;
        }

        private bool MessageEdit_Loaded(MessageViewModel message)
        {
            if (message.Content is MessagePoll || message.Content is MessageLocation)
            {
                return false;
            }

            return message.CanBeEdited;
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
            if (ViewModel.Type == DialogType.Normal && message.Content is MessagePoll poll && poll.Poll.Type is PollTypeRegular)
            {
                return poll.Poll.Options.Any(x => x.IsChosen) && !poll.Poll.IsClosed;
            }

            return false;
        }

        private bool MessageStopPoll_Loaded(MessageViewModel message)
        {
            if (message.Content is MessagePoll poll)
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
            return message.SenderUserId != myId;
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
            if (message.Content is MessageText text)
            {
                return !string.IsNullOrEmpty(text.Text.Text);
            }
            else if (message.Content is MessageContact)
            {
                return true;
            }

            return message.Content.HasCaption();
        }

        private bool MessageCopyMedia_Loaded(MessageViewModel message)
        {
            if (message.Ttl > 0)
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
            if (chat != null && chat.Type is ChatTypeSupergroup supergroupType)
            {
                //var supergroup = ViewModel.ProtoService.GetSupergroup(supergroupType.SupergroupId);
                //return !string.IsNullOrEmpty(supergroup.Username);
                return ViewModel.Type == DialogType.Normal;
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
            if (message.Ttl > 0)
            {
                return false;
            }

            var file = message.Get().GetFileAndName(true);
            if (file.File != null && file.File.Local.IsDownloadingCompleted)
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
            _textShadowVisual.IsVisible = e.NewSize.Height > 0;
        }

        private void TextArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _textShadowVisual.Size = e.NewSize.ToVector2();
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
            var slideWidth = (float)SlidePanel.ActualWidth;
            var elapsedWidth = (float)ElapsedPanel.ActualWidth;

            _slideVisual.Opacity = 1;

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var messageAnimation = _compositor.CreateScalarKeyFrameAnimation();
            messageAnimation.InsertKeyFrame(0, 0);
            messageAnimation.InsertKeyFrame(1, 48);
            messageAnimation.Duration = TimeSpan.FromMilliseconds(300);

            AttachTextAreaExpression();

            var slideAnimation = _compositor.CreateScalarKeyFrameAnimation();
            slideAnimation.InsertKeyFrame(0, slideWidth + 36);
            slideAnimation.InsertKeyFrame(1, 0);
            slideAnimation.Duration = TimeSpan.FromMilliseconds(300);

            var elapsedAnimation = _compositor.CreateScalarKeyFrameAnimation();
            elapsedAnimation.InsertKeyFrame(0, -elapsedWidth);
            elapsedAnimation.InsertKeyFrame(1, 0);
            elapsedAnimation.Duration = TimeSpan.FromMilliseconds(300);

            var ellipseAnimation = _compositor.CreateVector3KeyFrameAnimation();
            ellipseAnimation.InsertKeyFrame(0, new Vector3(56f / 96f));
            ellipseAnimation.InsertKeyFrame(1, new Vector3(1));
            ellipseAnimation.Duration = TimeSpan.FromMilliseconds(200);

            _messageVisual.StartAnimation("Offset.Y", messageAnimation);
            _slideVisual.StartAnimation("Offset.X", slideAnimation);
            _elapsedVisual.StartAnimation("Offset.X", elapsedAnimation);
            _ellipseVisual.StartAnimation("Scale", ellipseAnimation);

            batch.Completed += (s, args) =>
            {
                _elapsedTimer.Start();

                AttachExpression();
                //DetachTextAreaExpression();
            };
            batch.End();

            ViewModel.ChatActionManager.SetTyping(btnVoiceMessage.IsChecked.Value ? (ChatAction)new ChatActionRecordingVideoNote() : new ChatActionRecordingVoiceNote());
        }

        private void VoiceButton_RecordingStopped(object sender, EventArgs e)
        {
            AttachExpression();
            AttachTextAreaExpression();

            var slidePosition = (float)(LayoutRoot.ActualWidth - 48 - 36);
            var difference = (float)(slidePosition - ElapsedPanel.ActualWidth);

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var slideAnimation = _compositor.CreateScalarKeyFrameAnimation();
            slideAnimation.InsertKeyFrame(0, _slideVisual.Offset.X);
            slideAnimation.InsertKeyFrame(1, -slidePosition);
            slideAnimation.Duration = TimeSpan.FromMilliseconds(200);

            var messageAnimation = _compositor.CreateScalarKeyFrameAnimation();
            messageAnimation.InsertKeyFrame(0, 48);
            messageAnimation.InsertKeyFrame(1, 0);
            messageAnimation.Duration = TimeSpan.FromMilliseconds(200);

            _slideVisual.StartAnimation("Offset.X", slideAnimation);
            _messageVisual.StartAnimation("Offset.Y", messageAnimation);

            batch.Completed += (s, args) =>
            {
                _elapsedTimer.Stop();

                DetachExpression();
                //DetachTextAreaExpression();

                ButtonCancelRecording.Visibility = Visibility.Collapsed;
                ElapsedLabel.Text = "0:00,0";

                var point = _slideVisual.Offset;
                point.X = _slideVisual.Size.X + 36;

                _slideVisual.Opacity = 0;
                _slideVisual.Offset = point;

                point = _elapsedVisual.Offset;
                point.X = -_elapsedVisual.Size.X;

                _elapsedVisual.Offset = point;

                point = _recordVisual.Offset;
                point.Y = 0;

                _recordVisual.Offset = point;
            };
            batch.End();

            ViewModel.ChatActionManager.CancelTyping();

            TextField.Focus(FocusState.Programmatic);
        }

        private void VoiceButton_RecordingLocked(object sender, EventArgs e)
        {
            DetachExpression();

            var ellipseAnimation = _compositor.CreateScalarKeyFrameAnimation();
            ellipseAnimation.InsertKeyFrame(0, -57);
            ellipseAnimation.InsertKeyFrame(1, 0);

            _recordVisual.StartAnimation("Offset.Y", ellipseAnimation);

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

                point = _recordVisual.Offset;
                point.Y = 0;

                _recordVisual.Offset = point;

                return;
            }

            var cumulative = e.Cumulative.Translation.ToVector2();
            point = _slideVisual.Offset;
            point.X = Math.Min(0, cumulative.X);

            _slideVisual.Offset = point;

            if (point.X < -80)
            {
                e.Complete();
                btnVoiceMessage.CancelRecording();
                return;
            }

            point = _recordVisual.Offset;
            point.Y = Math.Min(0, cumulative.Y);

            _recordVisual.Offset = point;

            if (point.Y < -57)
            {
                e.Complete();
                btnVoiceMessage.LockRecording();
            }
        }

        private void ButtonCancelRecording_Click(object sender, RoutedEventArgs e)
        {
            btnVoiceMessage.CancelRecording();
        }

        private void AttachExpression()
        {
            var elapsedExpression = _compositor.CreateExpressionAnimation("min(0, slide.Offset.X + ((root.Size.X - 48 - 36 - slide.Size.X) - elapsed.Size.X))");
            elapsedExpression.SetReferenceParameter("slide", _slideVisual);
            elapsedExpression.SetReferenceParameter("elapsed", _elapsedVisual);
            elapsedExpression.SetReferenceParameter("root", _rootVisual);

            var ellipseExpression = _compositor.CreateExpressionAnimation("Vector3(max(0, 1 + slide.Offset.X / (root.Size.X - 48 - 36)), max(0, 1 + slide.Offset.X / (root.Size.X - 48 - 36)), 1)");
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

        private void AttachTextAreaExpression()
        {
            AttachTextAreaExpression(ButtonAttach);
            AttachTextAreaExpression(SecondaryButtonsPanel);
            AttachTextAreaExpression(ButtonStickers);
            AttachTextAreaExpression(btnSendMessage);
        }

        private void AttachTextAreaExpression(FrameworkElement element)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);

            var expression = _compositor.CreateExpressionAnimation("visual.Offset.Y");
            expression.SetReferenceParameter("visual", _messageVisual);

            visual.StopAnimation("Offset.Y");
            visual.StartAnimation("Offset.Y", expression);
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            TextField.Document.GetText(TextGetOptions.None, out string hidden);
            TextField.Document.GetText(TextGetOptions.NoHidden, out string text);

            if (e.ClickedItem is User user && ChatTextBox.SearchByUsername(text.Substring(0, Math.Min(TextField.Document.Selection.EndPosition, text.Length)), out string username, out int index))
            {
                var insert = string.Empty;
                var adjust = 0;

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
                var insert = $"/{command.Item.Command}";
                if (chat.Type is ChatTypeSupergroup || chat.Type is ChatTypeBasicGroup)
                {
                    var bot = ViewModel.ProtoService.GetUser(command.UserId);
                    if (bot != null && bot.Username.Length > 0)
                    {
                        insert += $"@{bot.Username}";
                    }
                }

                TextField.SetText(null, null);
                ViewModel.SendCommand.Execute(insert);
            }
            else if (e.ClickedItem is string hashtag && ChatTextBox.SearchByHashtag(text.Substring(0, Math.Min(TextField.Document.Selection.EndPosition, text.Length)), out string initial, out int index2))
            {
                var insert = $"{hashtag} ";
                var start = TextField.Document.Selection.StartPosition - 1 - initial.Length + insert.Length;
                var range = TextField.Document.GetRange(TextField.Document.Selection.StartPosition - 1 - initial.Length, TextField.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                //TextField.Document.GetRange(start, start).SetText(TextSetOptions.None, " ");
                //TextField.Document.Selection.StartPosition = start + 1;
                TextField.Document.Selection.StartPosition = start;
            }
            else if (e.ClickedItem is EmojiData emoji && ChatTextBox.SearchByEmoji(text.Substring(0, Math.Min(TextField.Document.Selection.EndPosition, text.Length)), out string replacement))
            {
                var insert = $"{emoji.Value} ";
                var start = TextField.Document.Selection.StartPosition - 1 - replacement.Length + insert.Length;
                var range = TextField.Document.GetRange(TextField.Document.Selection.StartPosition - 1 - replacement.Length, TextField.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                //TextField.Document.GetRange(start, start).SetText(TextSetOptions.None, " ");
                //TextField.Document.Selection.StartPosition = start + 1;
                TextField.Document.Selection.StartPosition = start;
            }
            else if (e.ClickedItem is Sticker sticker)
            {
                TextField.SetText(null, null);
                ViewModel.StickerSendCommand.Execute(sticker);

                if (_stickersMode == StickersPanelMode.Overlay)
                {
                    Collapse_Click(null, null);
                }
            }

            ViewModel.Autocomplete = null;
        }

        #region Binding

        //public Visibility ConvertBotInfo(TLBotInfo info, bool last)
        //{
        //    return info != null && !string.IsNullOrEmpty(info.Description) && last ? Visibility.Visible : Visibility.Collapsed;
        //}

        private Visibility ConvertShadowVisibility(Visibility inline, object stickers, object autocomplete)
        {
            _textShadowVisual.IsVisible = inline == Visibility.Visible || stickers != null || autocomplete != null;
            return Visibility.Visible;
        }

        public Visibility ConvertIsEmpty(bool empty, bool self, bool bot, bool should)
        {
            if (should)
            {
                return empty && self ? Visibility.Visible : Visibility.Collapsed;
            }

            return empty && !self && !bot ? Visibility.Visible : Visibility.Collapsed;
        }

        public string ConvertEmptyText(int userId)
        {
            return userId != 777000 && userId != 429000 && userId != 4244000 && (userId / 1000 == 333 || userId % 1000 == 0) ? Strings.Resources.GotAQuestion : Strings.Resources.NoMessages;
        }

        private bool ConvertClickEnabled(ListViewSelectionMode mode)
        {
            return mode == ListViewSelectionMode.Multiple;
        }

        #endregion

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as FrameworkElement;
            if (button.Tag is MessageViewModel message)
            {
                ViewModel.MessageShareCommand.Execute(message);
            }
        }

        private async void Date_Click(object sender, RoutedEventArgs e)
        {
            //var button = sender as FrameworkElement;
            //if (button.DataContext is TLMessageCommonBase message)
            //{
            //    var dialog = new Controls.Views.CalendarView();
            //    dialog.MaxDate = DateTimeOffset.Now.Date;
            //    dialog.SelectedDates.Add(BindConvert.Current.DateTime(message.Date));

            //    var confirm = await dialog.ShowQueuedAsync();
            //    if (confirm == ContentDialogResult.Primary && dialog.SelectedDates.Count > 0)
            //    {
            //        var offset = TLUtils.DateToUniversalTimeTLInt(dialog.SelectedDates.FirstOrDefault().Date);
            //        await ViewModel.LoadDateSliceAsync(offset);
            //    }
            //}
        }

        private void Collapse_Click(object sender, RoutedEventArgs e)
        {
            if (_stickersMode == StickersPanelMode.Overlay)
            {
                if (StickersPanel.Visibility == Visibility.Collapsed || _stickersMode == StickersPanelMode.Collapsed)
                {
                    return;
                }

                _stickersMode = StickersPanelMode.Collapsed;
                SettingsService.Current.IsSidebarOpen = false;

                var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    StickersPanel.Visibility = Visibility.Collapsed;
                    StickersPanel.Deactivate();
                };

                var opacity = _compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(0, 1);
                opacity.InsertKeyFrame(1, 0);

                var clip = _compositor.CreateScalarKeyFrameAnimation();
                clip.InsertKeyFrame(0, 0);
                clip.InsertKeyFrame(1, 48);

                _stickersPanel.StartAnimation("Opacity", opacity);
                _stickersPanel.Clip.StartAnimation("LeftInset", clip);
                _stickersPanel.Clip.StartAnimation("TopInset", clip);

                batch.End();
            }
            else
            {
                _stickersMode = StickersPanelMode.Collapsed;
                SettingsService.Current.IsSidebarOpen = false;

                StickersPanel.Deactivate();
                StickersPanel.Visibility = Visibility.Collapsed;
            }

            ButtonStickers.IsChecked = false;

            switch (ViewModel.Settings.Stickers.SelectedTab)
            {
                case Services.Settings.StickersTab.Emoji:
                    ButtonStickers.Glyph = "\uE76E";
                    break;
                case Services.Settings.StickersTab.Animations:
                    ButtonStickers.Glyph = "\uF4A9";
                    break;
                case Services.Settings.StickersTab.Stickers:
                    ButtonStickers.Glyph = "\uF4AA";
                    break;
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > e.PreviousSize.Width && e.NewSize.Width >= SIDEBAR_MIN_WIDTH && e.PreviousSize.Width < SIDEBAR_MIN_WIDTH)
            {
                if (_stickersModeWide == StickersPanelMode.Sidebar)
                {
                    ShowHideDockedStickersPanel(true);
                }
                else
                {
                    Collapse_Click(null, null);
                }
            }
            else if (e.NewSize.Width < e.PreviousSize.Width && e.NewSize.Width < SIDEBAR_MIN_WIDTH && e.PreviousSize.Width >= SIDEBAR_MIN_WIDTH)
            {
                if (_stickersMode == StickersPanelMode.Sidebar)
                {
                    _stickersModeWide = StickersPanelMode.Sidebar;
                    Collapse_Click(null, null);
                }
                else
                {
                    _stickersModeWide = StickersPanelMode.Collapsed;
                }
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
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;

                _autocompleteZoomer.ElementPrepared(args.ItemContainer);
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

                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
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

                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
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
                var photo = content.Children[0] as Image;

                while (content.Children.Count > 1)
                {
                    content.Children.RemoveAt(1);
                }

                photo.Opacity = 1;

                if (sticker == null || sticker.Thumbnail == null)
                {
                    photo.Source = null;
                    return;
                }

                var file = sticker.Thumbnail.File;
                if (file.Local.IsDownloadingCompleted)
                {
                    photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    //DownloadFile(file.Id, sticker);
                    photo.Source = null;
                    ViewModel.ProtoService.DownloadFile(file.Id, 1);
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
            UpdateChatDefaultDisableNotification(chat, chat.DefaultDisableNotification);

            Report.Visibility = chat.CanBeReported ? Visibility.Visible : Visibility.Collapsed;

            ButtonScheduled.Visibility = chat.HasScheduledMessages && ViewModel.Type == DialogType.Normal ? Visibility.Visible : Visibility.Collapsed;
            ButtonTimer.Visibility = chat.Type is ChatTypeSecret ? Visibility.Visible : Visibility.Collapsed;
            ButtonSilent.Visibility = chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            ButtonSilent.IsChecked = chat.DefaultDisableNotification;

            Call.Visibility = Visibility.Collapsed;
            VideoCall.Visibility = Visibility.Collapsed;

            UpdateChatPermissions(chat);
        }

        public void UpdateChatPermissions(Chat chat)
        {
            StickersPanel.UpdateChatPermissions(ViewModel.CacheService, chat);
            ListInline.UpdateChatPermissions(chat);
        }

        public void UpdateChatTitle(Chat chat)
        {
            if (ViewModel.Type == DialogType.ScheduledMessages)
            {
                Title.Text = ViewModel.CacheService.IsSavedMessages(chat) ? Strings.Resources.Reminders : Strings.Resources.ScheduledMessages;
            }
            else
            {
                Title.Text = ViewModel.CacheService.GetTitle(chat);
            }
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, (int)Photo.Width);
        }

        public void UpdateChatHasScheduledMessages(Chat chat)
        {
            ButtonScheduled.Visibility = chat.HasScheduledMessages && ViewModel.Type == DialogType.Normal ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateChatActionBar(Chat chat)
        {
            Button CreateButton(string text, ICommand command, object commandParameter = null, int column = 0)
            {
                var label = new TextBlock();
                label.Style = App.Current.Resources["CaptionTextBlockStyle"] as Style;
                label.Foreground = App.Current.Resources["SystemControlHighlightAccentBrush"] as Brush;
                label.Text = text;

                var button = new Button();
                button.Style = App.Current.Resources["EmptyButtonStyle"] as Style;
                button.HorizontalContentAlignment = HorizontalAlignment.Center;
                button.VerticalContentAlignment = VerticalAlignment.Center;
                button.Content = label;
                button.Command = command;
                button.CommandParameter = commandParameter;

                ActionBarRoot.ColumnDefinitions.Add(new ColumnDefinition());
                Grid.SetColumn(button, column);

                return button;
            }

            ActionBarRoot.ColumnDefinitions.Clear();
            ActionBarRoot.Children.Clear();

            if (chat.ActionBar is ChatActionBarAddContact)
            {
                var user = ViewModel.CacheService.GetUser(chat);
                if (user != null)
                {
                    ActionBarRoot.Children.Add(CreateButton(string.Format(Strings.Resources.AddContactFullChat, user.FirstName.ToUpper()), ViewModel.AddContactCommand));
                }
                else
                {
                    ActionBarRoot.Children.Add(CreateButton(Strings.Resources.AddContactChat, ViewModel.AddContactCommand));
                }
            }
            else if (chat.ActionBar is ChatActionBarReportAddBlock)
            {
                ActionBarRoot.Children.Add(CreateButton(Strings.Resources.ReportSpamUser, ViewModel.ReportSpamCommand));
                ActionBarRoot.Children.Add(CreateButton(Strings.Resources.AddContactChat, ViewModel.AddContactCommand, column: 1));
            }
            else if (chat.ActionBar is ChatActionBarReportSpam)
            {
                var user = ViewModel.CacheService.GetUser(chat);
                if (user != null)
                {
                    ActionBarRoot.Children.Add(CreateButton(Strings.Resources.ReportSpamUser, ViewModel.ReportSpamCommand));
                }
                else
                {
                    ActionBarRoot.Children.Add(CreateButton(Strings.Resources.ReportSpamAndLeave, ViewModel.ReportSpamCommand, new ChatReportReasonSpam()));
                }
            }
            else if (chat.ActionBar is ChatActionBarReportUnrelatedLocation)
            {
                ActionBarRoot.Children.Add(CreateButton(Strings.Resources.ReportSpamLocation, ViewModel.ReportSpamCommand, new ChatReportReasonUnrelatedLocation()));
            }
            else if (chat.ActionBar is ChatActionBarSharePhoneNumber)
            {
                ActionBarRoot.Children.Add(CreateButton(Strings.Resources.ShareMyPhone, ViewModel.ShareContactCommand));
            }

            ActionBar.Visibility = ActionBarRoot.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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

        public void UpdateChatActions(Chat chat, IDictionary<int, ChatAction> actions)
        {
            if (chat.Type is ChatTypePrivate privata && privata.UserId == ViewModel.CacheService.Options.MyId)
            {
                ChatActionIndicator.UpdateAction(null);
                ChatActionPanel.Visibility = Visibility.Collapsed;
                Subtitle.Opacity = 1;
                return;
            }

            if (actions != null && actions.Count > 0)
            {
                ChatActionLabel.Text = InputChatActionManager.GetTypingString(chat, actions, ViewModel.CacheService.GetUser, out ChatAction commonAction);
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
                    ShowAction(ViewModel.CacheService.GetNotificationSettingsMuteFor(chat) > 0 ? Strings.Resources.ChannelUnmute : Strings.Resources.ChannelMute, true);
                }
            }
        }

        public void UpdateChatOnlineMemberCount(Chat chat, int count)
        {
            if (count > 1)
            {
                ViewModel.OnlineCount = Locale.Declension("OnlineCount", count);
            }
            else
            {
                ViewModel.OnlineCount = null;
            }
        }



        public void UpdateChatUnreadMentionCount(Chat chat, int count)
        {
            if (ViewModel.Type == DialogType.Normal && count > 0)
            {
                MentionsPanel.Visibility = Visibility.Visible;
                Mentions.Text = count.ToString();
            }
            else
            {
                MentionsPanel.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateChatReplyMarkup(Chat chat, MessageViewModel message)
        {
            if (message?.ReplyMarkup is ReplyMarkupForceReply forceReply && forceReply.IsPersonal)
            {
                ViewModel.ReplyToMessage(message);

                ButtonMarkup.Visibility = Visibility.Collapsed;
                CollapseMarkup(false);
            }
            else
            {
                var updated = ReplyMarkup.Update(message, message?.ReplyMarkup, false);
                if (updated)
                {
                    ButtonMarkup.Visibility = Visibility.Visible;
                    ShowMarkup();
                }
                else
                {
                    ButtonMarkup.Visibility = Visibility.Collapsed;
                    CollapseMarkup(false);
                }
            }
        }

        public void UpdatePinnedMessage(Chat chat, MessageViewModel message, bool loading)
        {
            if (message == null && !loading)
            {
                PinnedMessagePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                PinnedMessagePanel.Visibility = Visibility.Visible;
                PinnedMessage.UpdateMessage(message, loading, Strings.Resources.PinnedMessage);
            }
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
            if (header == null)
            {
                // Let's reset
                //ComposerHeader.Visibility = Visibility.Collapsed;
                ShowHideComposerHeader(false);
                ComposerHeaderReference.Message = null;

                AttachMedia.Command = ViewModel.SendMediaCommand;
                AttachDocument.Command = ViewModel.SendDocumentCommand;

                var rights = ViewModel.VerifyRights(chat, x => x.CanSendMediaMessages, Strings.Resources.GlobalAttachMediaRestricted, Strings.Resources.AttachMediaRestrictedForever, Strings.Resources.AttachMediaRestricted, out string label);
                var pollsRights = ViewModel.VerifyRights(chat, x => x.CanSendPolls, Strings.Resources.GlobalAttachMediaRestricted, Strings.Resources.AttachMediaRestrictedForever, Strings.Resources.AttachMediaRestricted, out string pollsLabel);

                var pollsAllowed = chat.Type is ChatTypeSupergroup || chat.Type is ChatTypeBasicGroup;
                if (!pollsAllowed && ViewModel.CacheService.TryGetUser(chat, out User user))
                {
                    pollsAllowed = user.Type is UserTypeBot;
                }

                AttachRestriction.Tag = label ?? string.Empty;
                AttachRestriction.Visibility = rights ? Visibility.Visible : Visibility.Collapsed;
                AttachMedia.Visibility = rights ? Visibility.Collapsed : Visibility.Visible;
                AttachDocument.Visibility = rights ? Visibility.Collapsed : Visibility.Visible;
                AttachLocation.Visibility = Visibility.Visible;
                AttachPoll.Visibility = pollsAllowed && !pollsRights ? Visibility.Visible : Visibility.Collapsed;
                AttachContact.Visibility = Visibility.Visible;
                AttachCurrent.Visibility = Visibility.Collapsed;

                ButtonAttach.Glyph = ReplyInfoToGlyphConverter.AttachGlyph;
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
                            ButtonAttach.Glyph = ReplyInfoToGlyphConverter.AttachEditGlyph;
                            ButtonAttach.IsEnabled = true;
                            break;
                        case MessagePhoto photo:
                            ButtonAttach.Glyph = !photo.IsSecret ? ReplyInfoToGlyphConverter.AttachEditGlyph : ReplyInfoToGlyphConverter.AttachGlyph;
                            ButtonAttach.IsEnabled = !photo.IsSecret;
                            break;
                        case MessageVideo video:
                            ButtonAttach.Glyph = !video.IsSecret ? ReplyInfoToGlyphConverter.AttachEditGlyph : ReplyInfoToGlyphConverter.AttachGlyph;
                            ButtonAttach.IsEnabled = !video.IsSecret;
                            break;
                        default:
                            ButtonAttach.Glyph = ReplyInfoToGlyphConverter.AttachGlyph;
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
                    AttachCurrent.Visibility = editing.Content is MessagePhoto || editing.Content is MessageVideo ? Visibility.Visible : Visibility.Collapsed;

                    ComposerHeaderGlyph.Glyph = ReplyInfoToGlyphConverter.EditGlyph;

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

                    AttachRestriction.Tag = label ?? string.Empty;
                    AttachRestriction.Visibility = rights ? Visibility.Visible : Visibility.Collapsed;
                    AttachMedia.Visibility = rights ? Visibility.Collapsed : Visibility.Visible;
                    AttachDocument.Visibility = rights ? Visibility.Collapsed : Visibility.Visible;
                    AttachLocation.Visibility = Visibility.Visible;
                    AttachPoll.Visibility = (chat.Type is ChatTypeSupergroup || chat.Type is ChatTypeBasicGroup) && !pollsRights ? Visibility.Visible : Visibility.Collapsed;
                    AttachContact.Visibility = Visibility.Visible;
                    AttachCurrent.Visibility = Visibility.Collapsed;

                    ButtonAttach.Glyph = ReplyInfoToGlyphConverter.AttachGlyph;
                    ButtonAttach.IsEnabled = true;

                    if (header.WebPagePreview != null)
                    {
                        ComposerHeaderGlyph.Glyph = ReplyInfoToGlyphConverter.GlobeGlyph;
                    }
                    else if (header.ReplyToMessage != null)
                    {
                        ComposerHeaderGlyph.Glyph = ReplyInfoToGlyphConverter.ReplyGlyph;
                    }
                    else
                    {
                        ComposerHeaderGlyph.Glyph = ReplyInfoToGlyphConverter.LoadingGlyph;
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

        private void ShowHideComposerHeader(bool show)
        {
            if (ButtonAction.Visibility == Visibility.Visible)
            {
                _composerHeaderCollapsed = true;
                ComposerHeader.Visibility = Visibility.Collapsed;

                return;
            }

            var composer = ElementCompositionPreview.GetElementVisual(ComposerHeader);
            var messages = ElementCompositionPreview.GetElementVisual(Messages);
            var textArea = ElementCompositionPreview.GetElementVisual(TextArea);

            //textArea.Clip?.StopAnimation("TopInset");
            //messages.Clip?.StopAnimation("TopInset");
            //composer.Clip?.StopAnimation("BottomInset");
            //messages.StopAnimation("Offset");
            //composer.StopAnimation("Offset");

            if ((show && ComposerHeader.Visibility == Visibility.Visible) || (!show && (ComposerHeader.Visibility == Visibility.Collapsed || _composerHeaderCollapsed)))
            {
                return;
            }

            var value = show ? 48 : 0;

            if (ApiInformation.IsMethodPresent("Windows.UI.Composition.Compositor", "CreateGeometricClip"))
            {
                var rect = textArea.Compositor.CreateRoundedRectangleGeometry();
                rect.CornerRadius = new Vector2(SettingsService.Current.Appearance.BubbleRadius);
                rect.Size = new Vector2((float)TextArea.ActualWidth, 144);
                rect.Offset = new Vector2(0, value);

                textArea.Clip = textArea.Compositor.CreateGeometricClip(rect);

                var animClip3 = textArea.Compositor.CreateVector2KeyFrameAnimation();
                animClip3.InsertKeyFrame(0, new Vector2(0, show ? 48 : 0));
                animClip3.InsertKeyFrame(1, new Vector2(0, show ? 0 : 48));

                rect.StartAnimation("Offset", animClip3);
            }
            else
            {
                textArea.Clip = textArea.Compositor.CreateInsetClip(0, value, 0, 0);

                var animClip3 = textArea.Compositor.CreateScalarKeyFrameAnimation();
                animClip3.InsertKeyFrame(0, show ? 48 : 0);
                animClip3.InsertKeyFrame(1, show ? 0 : 48);
                animClip3.Duration = TimeSpan.FromMilliseconds(150);

                textArea.Clip.StartAnimation("TopInset", animClip3);
            }

            messages.Clip = textArea.Compositor.CreateInsetClip(0, value, 0, 0);
            composer.Clip = textArea.Compositor.CreateInsetClip(0, 0, 0, value);

            var batch = composer.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                textArea.Clip = null;
                composer.Clip = null;
                messages.Clip = null;
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

            var anim1 = textArea.Compositor.CreateVector3KeyFrameAnimation();
            anim1.InsertKeyFrame(0, new Vector3(0, show ? 48 : 0, 0));
            anim1.InsertKeyFrame(1, new Vector3(0, show ? 0 : 48, 0));
            anim1.Duration = TimeSpan.FromMilliseconds(150);

            //textArea.Clip.StartAnimation("TopInset", animClip);
            messages.Clip.StartAnimation("TopInset", animClip2);
            composer.Clip.StartAnimation("BottomInset", animClip);
            messages.StartAnimation("Offset", anim1);
            composer.StartAnimation("Offset", anim1);

            batch.End();



            ContentPanel.Margin = new Thickness(0, -48, 0, 0);

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

        private void ShowHideTextFormatting(bool show)
        {
            //if (ButtonAction.Visibility == Visibility.Visible)
            //{
            //    _composerHeaderCollapsed = true;
            //    ComposerHeader.Visibility = Visibility.Collapsed;

            //    return;
            //}

            var composer = ElementCompositionPreview.GetElementVisual(ComposerHeader);
            var messages = ElementCompositionPreview.GetElementVisual(Messages);
            var textArea = ElementCompositionPreview.GetElementVisual(TextArea);
            var textField = ElementCompositionPreview.GetElementVisual(TextField);
            var textFormatting = ElementCompositionPreview.GetElementVisual(TextFormatting);
            var textBackground = ElementCompositionPreview.GetElementVisual(TextBackground);

            ////textArea.Clip?.StopAnimation("TopInset");
            ////messages.Clip?.StopAnimation("TopInset");
            ////composer.Clip?.StopAnimation("BottomInset");
            ////messages.StopAnimation("Offset");
            ////composer.StopAnimation("Offset");

            if ((show && TextFormatting.Visibility == Visibility.Visible) || (!show && (TextFormatting.Visibility == Visibility.Collapsed || _textFormattingCollapsed)))
            {
                return;
            }

            var value = show ? 48 : 0;

            if (ApiInformation.IsMethodPresent("Windows.UI.Composition.Compositor", "CreateGeometricClip"))
            {
                var rect = textArea.Compositor.CreateRoundedRectangleGeometry();
                rect.CornerRadius = new Vector2(SettingsService.Current.Appearance.BubbleRadius);
                rect.Size = new Vector2((float)TextArea.ActualWidth, 144);
                rect.Offset = new Vector2(0, value);

                textArea.Clip = textArea.Compositor.CreateGeometricClip(rect);

                var animClip3 = textArea.Compositor.CreateVector2KeyFrameAnimation();
                animClip3.InsertKeyFrame(0, new Vector2(0, show ? 48 : 0));
                animClip3.InsertKeyFrame(1, new Vector2(0, show ? 0 : 48));

                rect.StartAnimation("Offset", animClip3);
            }
            else
            {
                textArea.Clip = textArea.Compositor.CreateInsetClip(0, value, 0, 0);

                var animClip3 = textArea.Compositor.CreateScalarKeyFrameAnimation();
                animClip3.InsertKeyFrame(0, show ? 48 : 0);
                animClip3.InsertKeyFrame(1, show ? 0 : 48);
                animClip3.Duration = TimeSpan.FromMilliseconds(150);

                textArea.Clip.StartAnimation("TopInset", animClip3);
            }

            messages.Clip = textArea.Compositor.CreateInsetClip(0, value, 0, 0);

            var batch = composer.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                textArea.Clip = null;
                messages.Clip = null;
                composer.Offset = new Vector3();
                messages.Offset = new Vector3();
                textField.Offset = new Vector3();
                textFormatting.Offset = new Vector3();
                textBackground.Offset = new Vector3();

                ContentPanel.Margin = new Thickness();

                if (show)
                {
                    _textFormattingCollapsed = false;
                }
                else
                {
                    TextFormatting.Visibility = Visibility.Collapsed;
                    TextBackground.Visibility = Visibility.Collapsed;

                    Grid.SetRow(btnAttach, show ? 2 : 1);
                    Grid.SetRow(ButtonsPanel, show ? 2 : 1);
                    //Grid.SetColumnSpan(TextFieldPanel, show ? 4 : 2);
                }

                UpdateTextAreaRadius();
            };

            var animClip2 = textArea.Compositor.CreateScalarKeyFrameAnimation();
            animClip2.InsertKeyFrame(0, show ? 0 : 48);
            animClip2.InsertKeyFrame(1, show ? 48 : 0);
            animClip2.Duration = TimeSpan.FromMilliseconds(150);

            var anim1 = textArea.Compositor.CreateVector3KeyFrameAnimation();
            anim1.InsertKeyFrame(0, new Vector3(0, show ? 48 : 0, 0));
            anim1.InsertKeyFrame(1, new Vector3(0, show ? 0 : 48, 0));
            anim1.Duration = TimeSpan.FromMilliseconds(150);

            //textArea.Clip.StartAnimation("TopInset", animClip);
            messages.Clip.StartAnimation("TopInset", animClip2);
            messages.StartAnimation("Offset", anim1);
            composer.StartAnimation("Offset", anim1);
            textField.StartAnimation("Offset", anim1);
            textFormatting.StartAnimation("Offset", anim1);
            textBackground.StartAnimation("Offset", anim1);

            batch.End();



            ContentPanel.Margin = new Thickness(0, -48, 0, 0);

            if (show)
            {
                _textFormattingCollapsed = false;
                TextFormatting.Visibility = Visibility.Visible;
                TextBackground.Visibility = Visibility.Visible;

                Grid.SetRow(btnAttach, show ? 2 : 1);
                Grid.SetRow(ButtonsPanel, show ? 2 : 1);
                //Grid.SetColumnSpan(TextFieldPanel, show ? 4 : 2);
            }
            else
            {
                _textFormattingCollapsed = true;
            }
        }

        private void UpdateTextAreaRadius()
        {
            var radius = SettingsService.Current.Appearance.BubbleRadius;
            var min = Math.Max(4, radius - 2);
            var max = ComposerHeader.Visibility == Visibility.Visible
                || TextFormatting.Visibility == Visibility.Visible ? 4 : min;

            ButtonAttach.Radius = new CornerRadius(max, 4, 4, min);
            btnVoiceMessage.Radius = new CornerRadius(4, max, min, 4);
            btnSendMessage.Radius = new CornerRadius(4, max, min, 4);
            btnEdit.Radius = new CornerRadius(4, max, min, 4);

            ComposerHeaderCancel.Radius = new CornerRadius(4, min, 4, 4);
            TextRoot.CornerRadius = ChatFooter.CornerRadius = new CornerRadius(radius);

            InlinePanel.CornerRadius = new CornerRadius(radius, radius, 0, 0);
            ListAutocomplete.Padding = new Thickness(0, 0, 0, radius);
            ListInline.UpdateCornerRadius(radius);

            if (radius > 0)
            {
                TextArea.MaxWidth = ChatFooter.MaxWidth = InlinePanel.MaxWidth = Separator.MaxWidth =
                    SettingsService.Current.IsAdaptiveWideEnabled ? 640 : double.PositiveInfinity;
                TextArea.Margin = ChatFooter.Margin = new Thickness(12, 0, 12, 8);
                InlinePanel.Margin = new Thickness(12, 0, 12, -radius);
            }
            else
            {
                TextArea.MaxWidth = ChatFooter.MaxWidth = InlinePanel.MaxWidth = Separator.MaxWidth =
                    SettingsService.Current.IsAdaptiveWideEnabled ? 664 : double.PositiveInfinity;
                TextArea.Margin = ChatFooter.Margin = new Thickness();
                InlinePanel.Margin = new Thickness();
            }

            Messages.Margin = new Thickness(0, 0, 0, -radius);
            Messages.Padding = new Thickness(0, 0, 0, radius + 6);
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

            DiscussColumn.Width = new GridLength(0, GridUnitType.Auto);
            DiscussButton.Visibility = Visibility.Collapsed;
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            if (fullInfo.IsBlocked)
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

            if (fullInfo.BotInfo != null)
            {
                ViewModel.BotCommands = fullInfo.BotInfo.Commands.Select(x => new UserCommand(user.Id, x)).ToList();
                ViewModel.HasBotCommands = fullInfo.BotInfo.Commands.Count > 0;
            }

            Call.Visibility = /*!secret &&*/ fullInfo.CanBeCalled ? Visibility.Visible : Visibility.Collapsed;
            VideoCall.Visibility = /*!secret &&*/ fullInfo.SupportsVideoCalls ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            if (ViewModel.CacheService.IsSavedMessages(user))
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

            DiscussColumn.Width = new GridLength(0, GridUnitType.Auto);
            DiscussButton.Visibility = Visibility.Collapsed;
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            if (group.Status is ChatMemberStatusLeft || group.Status is ChatMemberStatusBanned)
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
                ShowArea();

                TextField.PlaceholderText = Strings.Resources.TypeMessage;

                ViewModel.LastSeen = Locale.Declension("Members", group.MemberCount);
            }

            DiscussColumn.Width = new GridLength(0, GridUnitType.Auto);
            DiscussButton.Visibility = Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ViewModel.LastSeen = Locale.Declension("Members", fullInfo.Members.Count);

            btnSendMessage.SlowModeDelay = 0;
            btnSendMessage.SlowModeDelayExpiresIn = 0;

            var commands = new List<UserCommand>();

            foreach (var member in fullInfo.Members)
            {
                if (member.BotInfo != null)
                {
                    commands.AddRange(member.BotInfo.Commands.Select(x => new UserCommand(member.UserId, x)).ToList());
                }
            }

            ViewModel.BotCommands = commands;
            ViewModel.HasBotCommands = commands.Count > 0;
        }



        public async void UpdateSupergroup(Chat chat, Supergroup group)
        {
            if (ViewModel.Type == DialogType.EventLog)
            {
                ShowAction(Strings.Resources.Settings, true);

                DiscussColumn.Width = new GridLength(0, GridUnitType.Auto);
                DiscussButton.Visibility = Visibility.Collapsed;
                return;
            }

            if (group.IsChannel)
            {
                if ((group.Status is ChatMemberStatusLeft && group.Username.Length > 0) || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
                {
                    ShowAction(Strings.Resources.ChannelJoin, true);
                }
                else if (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator administrator && administrator.CanPostMessages)
                {
                    ShowArea();
                }
                else if (group.Status is ChatMemberStatusLeft || group.Status is ChatMemberStatusBanned)
                {
                    ShowAction(Strings.Resources.DeleteChat, true);
                }
                else
                {
                    ShowAction(ViewModel.CacheService.GetNotificationSettingsMuteFor(chat) > 0 ? Strings.Resources.ChannelUnmute : Strings.Resources.ChannelMute, true);
                }

                if (group.HasLinkedChat)
                {
                    DiscussColumn.Width = new GridLength(1, GridUnitType.Star);
                    DiscussButton.Visibility = Visibility.Visible;
                }
                else
                {
                    DiscussColumn.Width = new GridLength(0, GridUnitType.Auto);
                    DiscussButton.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                if ((group.Status is ChatMemberStatusLeft && (group.Username.Length > 0 || group.HasLocation || group.HasLinkedChat)) || (group.Status is ChatMemberStatusCreator creator && !creator.IsMember))
                {
                    ShowAction(Strings.Resources.ChannelJoin, true);
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
                            ShowAction(string.Format(Strings.Resources.SendMessageRestricted, BindConvert.Current.BannedUntil(restrictedSend.RestrictedUntilDate)), false);
                        }
                    }
                    else
                    {
                        ShowArea();
                    }
                }
                else if (group.Status is ChatMemberStatusLeft || group.Status is ChatMemberStatusBanned)
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

                DiscussColumn.Width = new GridLength(0, GridUnitType.Auto);
                DiscussButton.Visibility = Visibility.Collapsed;
            }

            TextField.PlaceholderText = group.IsChannel
                ? chat.DefaultDisableNotification
                ? Strings.Resources.ChannelSilentBroadcast
                : Strings.Resources.ChannelBroadcast
                : Strings.Resources.TypeMessage;
            ViewModel.LastSeen = Locale.Declension(group.IsChannel ? "Subscribers" : "Members", group.MemberCount);

            if (group.IsChannel)
            {
                return;
            }

            var response = await ViewModel.ProtoService.SendAsync(new GetSupergroupMembers(group.Id, new SupergroupMembersFilterBots(), 0, 200));
            if (response is ChatMembers members)
            {
                var commands = new List<UserCommand>();

                foreach (var member in members.Members)
                {
                    if (member.BotInfo != null)
                    {
                        commands.AddRange(member.BotInfo.Commands.Select(x => new UserCommand(member.UserId, x)).ToList());
                    }
                }

                if (StillValid(chat))
                {
                    ViewModel.BotCommands = commands;
                    ViewModel.HasBotCommands = commands.Count > 0;
                }
            }

            UpdateComposerHeader(chat, ViewModel.ComposerHeader);
            UpdateChatPermissions(chat);
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            if (ViewModel.Type == DialogType.EventLog)
            {
                return;
            }

            ViewModel.LastSeen = Locale.Declension(group.IsChannel ? "Subscribers" : "Members", fullInfo.MemberCount);

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

            if (group.IsChannel && fullInfo.LinkedChatId != 0)
            {
                var linkedChat = ViewModel.CacheService.GetChat(fullInfo.LinkedChatId);
                if (linkedChat == null)
                {
                    return;
                }

                var linkedSupergroup = ViewModel.CacheService.GetSupergroup(linkedChat);
                if (linkedSupergroup == null || !linkedSupergroup.IsMember())
                {
                    return;
                }

                if (linkedChat.UnreadCount > 0)
                {
                    DiscussBadgeLabel.Text = $"{linkedChat.UnreadCount}";
                    DiscussBadge.Visibility = Visibility.Visible;
                }
                else
                {
                    DiscussBadge.Visibility = Visibility.Collapsed;
                }
            }
        }



        public void UpdateFile(File file)
        {
            if (_viewModel.TryGetMessagesForFileId(file.Id, out IList<MessageViewModel> messages))
            {
                foreach (var message in messages)
                {
                    message.UpdateFile(file);

                    var container = Messages.ContainerFromItem(message) as ListViewItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var element = container.ContentTemplateRoot as FrameworkElement;
                    if (element is Grid)
                    {
                        element = element.FindName("Bubble") as FrameworkElement;
                    }

                    if (element is MessageBubble bubble)
                    {
                        bubble.UpdateFile(message, file);
                    }
                    else if (message.Content is MessageChatChangePhoto && file.Local.IsDownloadingCompleted)
                    {
                        var photo = element.FindName("Photo") as Image;
                        if (photo != null)
                        {
                            photo.Source = new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = 120, DecodePixelHeight = 120, DecodePixelType = DecodePixelType.Logical };
                        }
                    }

                    var content = message.GeneratedContent ?? message.Content;
                    if (content is MessageAnimation animation && animation.Animation.AnimationValue.Id == file.Id && file.Local.IsDownloadingCompleted)
                    {
                        ViewVisibleMessages(false);
                    }
                    else if (content is MessageVideoNote videoNote && videoNote.VideoNote.Video.Id == file.Id && file.Local.IsDownloadingCompleted)
                    {
                        ViewVisibleMessages(false);
                    }
                    else if (content is MessageSticker sticker && sticker.Sticker.IsAnimated && sticker.Sticker.StickerValue.Id == file.Id && file.Local.IsDownloadingCompleted)
                    {
                        ViewVisibleMessages(false);
                    }
                    else if (content is MessageText text && text.WebPage != null && file.Local.IsDownloadingCompleted)
                    {
                        if (text.WebPage.Animation?.AnimationValue.Id == file.Id || text.WebPage.VideoNote?.Video.Id == file.Id)
                        {
                            ViewVisibleMessages(false);
                        }
                        else if (text.WebPage.Sticker?.StickerValue.Id == file.Id && text.WebPage.Sticker.IsAnimated)
                        {
                            ViewVisibleMessages(false);
                        }
                    }
                }

                //if (file.Local.IsDownloadingCompleted && file.Remote.IsUploadingCompleted)
                //{
                //    messages.Clear();
                //}
            }

            if (file.Local.IsDownloadingCompleted && _viewModel.TryGetMessagesForPhotoId(file.Id, out IList<MessageViewModel> photos))
            {
                foreach (var message in photos)
                {
                    var container = Messages.ContainerFromItem(message) as ListViewItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var content = container.ContentTemplateRoot as FrameworkElement;
                    if (content is MessageBubble == false)
                    {
                        var photo = content.FindName("Photo") as ProfilePicture;
                        if (photo != null)
                        {
                            if (message.IsSaved())
                            {
                                if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
                                {
                                    var user = message.ProtoService.GetUser(fromUser.SenderUserId);
                                    if (user != null)
                                    {
                                        photo.Source = PlaceholderHelper.GetUser(null, user, 32);
                                    }
                                }
                                else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel post)
                                {
                                    var fromChat = message.ProtoService.GetChat(post.ChatId);
                                    if (fromChat != null)
                                    {
                                        photo.Source = PlaceholderHelper.GetChat(null, fromChat, 32);
                                    }
                                }
                            }
                            else if (message.SenderUserId != 0)
                            {
                                var user = message.GetSenderUser();
                                if (user != null)
                                {
                                    photo.Source = PlaceholderHelper.GetUser(null, user, 30);
                                }
                            }
                            else
                            {
                                var chat2 = message.GetChat();
                                if (chat2 != null)
                                {
                                    photo.Source = PlaceholderHelper.GetChat(null, chat2, 30);
                                }
                            }
                        }
                    }
                }

                photos.Clear();
            }

            var chat = ViewModel.Chat;
            if (chat != null && chat.UpdateFile(file))
            {
                if (chat.Type is ChatTypePrivate privata && privata.UserId == ViewModel.CacheService.Options.MyId)
                {
                    Photo.Source = PlaceholderHelper.GetSavedMessages(privata.UserId, (int)Photo.Width);
                }
                else
                {
                    Photo.Source = PlaceholderHelper.GetChat(null, chat, (int)Photo.Width);
                }
            }

            ListInline.UpdateFile(file);
            StickersPanel.UpdateFile(file);

            foreach (var item in ListAutocomplete.Items.ToArray())
            {
                if (item is UserCommand command)
                {
                    var user = ViewModel.ProtoService.GetUser(command.UserId);
                    if (user.UpdateFile(file))
                    {
                        var container = ListAutocomplete.ContainerFromItem(command) as ListViewItem;
                        if (container == null)
                        {
                            continue;
                        }

                        var content = container.ContentTemplateRoot as Grid;
                        if (content == null)
                        {
                            continue;
                        }

                        var photo = content.Children[0] as ProfilePicture;
                        photo.Source = PlaceholderHelper.GetUser(null, user, 36);
                    }
                }
                else if (item is User user)
                {
                    if (user.UpdateFile(file))
                    {
                        var container = ListAutocomplete.ContainerFromItem(user) as ListViewItem;
                        if (container == null)
                        {
                            continue;
                        }

                        var content = container.ContentTemplateRoot as Grid;
                        if (content == null)
                        {
                            continue;
                        }

                        var photo = content.Children[0] as ProfilePicture;
                        photo.Source = PlaceholderHelper.GetUser(null, user, 36);
                    }
                }
                else if (item is Sticker sticker)
                {
                    if (sticker.UpdateFile(file) && file.Local.IsDownloadingCompleted)
                    {
                        if (file.Id == sticker.Thumbnail?.File.Id)
                        {
                            var container = ListAutocomplete.ContainerFromItem(sticker) as SelectorItem;
                            if (container == null)
                            {
                                continue;
                            }

                            var content = container.ContentTemplateRoot as Grid;
                            var photo = content?.Children[0] as Image;

                            if (photo == null)
                            {
                                continue;
                            }

                            photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                        }
                        else if (file.Id == sticker.StickerValue.Id)
                        {
                            _autocompleteHandler.ThrottleVisibleItems();
                        }
                    }
                }
            }

            var header = ViewModel.ComposerHeader;
            if (header?.EditingMessageFileId == file.Id)
            {
                var size = Math.Max(file.Size, file.ExpectedSize);
                ComposerHeaderUpload.Value = (double)file.Remote.UploadedSize / size;
            }
        }

        #endregion

        private void TextField_Formatting(FormattedTextBox sender, EventArgs args)
        {
            ViewModel.Settings.IsTextFormattingVisible = sender.IsFormattingVisible;
            ShowHideTextFormatting(sender.IsFormattingVisible);
        }

        private void Autocomplete_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Height > e.PreviousSize.Height)
            {
                var diff = (float)e.NewSize.Height - (float)e.PreviousSize.Height;
                var visual = ElementCompositionPreview.GetElementVisual(ListAutocomplete);

                var anim = Window.Current.Compositor.CreateSpringVector3Animation();
                anim.InitialValue = new Vector3(0, diff, 0);
                anim.FinalValue = new Vector3();

                visual.StartAnimation("Offset", anim);
            }
        }

        private void TextField_Sending(object sender, EventArgs e)
        {
            if (_stickersMode == StickersPanelMode.Overlay)
            {
                Collapse_Click(StickersPanel, null);
            }
        }
    }

    public enum StickersPanelMode
    {
        Collapsed,
        Overlay,
        Sidebar
    }
}
