using LinqToVisualTree;
using System;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Composition;
using Windows.UI.Composition.Interactions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Views
{
    public sealed partial class ChatListPage : ChatListListView, IInteractionTrackerOwner
    {
        public MainViewModel Main { get; set; }

        private bool _canGoNext;
        private bool _canGoPrev;

        public ChatListPage()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        }

        private void ChatsList_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ChatListListViewItem(this);
                //args.ItemContainer.Style = ChatsList.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = ChatsList.ItemTemplate;
                args.ItemContainer.ContextRequested += Chat_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        #region Context menu

        private async void Chat_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.Tag as Chat;

            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (position == null)
            {
                return;
            }

            var muted = ViewModel.CacheService.Notifications.GetMutedFor(chat) > 0;
            flyout.CreateFlyoutItem(DialogArchive_Loaded, viewModel.ChatArchiveCommand, chat, chat.Positions.Any(x => x.List is ChatListArchive) ? Strings.Resources.Unarchive : Strings.Resources.Archive, new FontIcon { Glyph = Icons.Archive });
            flyout.CreateFlyoutItem(DialogPin_Loaded, viewModel.ChatPinCommand, chat, position.IsPinned ? Strings.Resources.UnpinFromTop : Strings.Resources.PinToTop, new FontIcon { Glyph = position.IsPinned ? Icons.PinOff : Icons.Pin });

            if (viewModel.Items.ChatList is ChatListFilter chatListFilter)
            {
                flyout.CreateFlyoutItem(viewModel.FolderRemoveCommand, (chatListFilter.ChatFilterId, chat), Strings.Resources.FilterRemoveFrom, new FontIcon { Glyph = Icons.FolderMove });
            }
            else
            {
                var response = await ViewModel.ProtoService.SendAsync(new GetChatListsToAddChat(chat.Id)) as ChatLists;
                if (response != null && response.ChatListsValue.Count > 0)
                {
                    var filters = ViewModel.CacheService.ChatFilters;

                    var item = new MenuFlyoutSubItem();
                    item.Text = Strings.Resources.FilterAddTo;
                    item.Icon = new FontIcon { Glyph = Icons.FolderAdd, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily };

                    foreach (var chatList in response.ChatListsValue.OfType<ChatListFilter>())
                    {
                        var filter = filters.FirstOrDefault(x => x.Id == chatList.ChatFilterId);
                        if (filter != null)
                        {
                            var icon = Icons.ParseFilter(filter.IconName);
                            var glyph = Icons.FilterToGlyph(icon);

                            item.CreateFlyoutItem(ViewModel.FolderAddCommand, (filter.Id, chat), filter.Title, new FontIcon { Glyph = glyph.Item1 });
                        }
                    }

                    if (filters.Count < 10 && item.Items.Count > 0)
                    {
                        item.CreateFlyoutSeparator();
                        item.CreateFlyoutItem(ViewModel.FolderCreateCommand, chat, Strings.Resources.CreateNewFilter, new FontIcon { Glyph = Icons.Add });
                    }

                    if (item.Items.Count > 0)
                    {
                        flyout.Items.Add(item);
                    }
                }
            }

            flyout.CreateFlyoutItem(DialogNotify_Loaded, viewModel.ChatNotifyCommand, chat, muted ? Strings.Resources.UnmuteNotifications : Strings.Resources.MuteNotifications, new FontIcon { Glyph = muted ? Icons.Alert : Icons.AlertOff });
            flyout.CreateFlyoutItem(DialogMark_Loaded, viewModel.ChatMarkCommand, chat, chat.IsUnread() ? Strings.Resources.MarkAsRead : Strings.Resources.MarkAsUnread, new FontIcon { Glyph = chat.IsUnread() ? Icons.MarkAsRead : Icons.MarkAsUnread, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily });
            flyout.CreateFlyoutItem(DialogClear_Loaded, viewModel.ChatClearCommand, chat, Strings.Resources.ClearHistory, new FontIcon { Glyph = Icons.Broom });
            flyout.CreateFlyoutItem(DialogDelete_Loaded, viewModel.ChatDeleteCommand, chat, DialogDelete_Text(chat), new FontIcon { Glyph = Icons.Delete });

            if (viewModel.SelectionMode != ListViewSelectionMode.Multiple)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(viewModel.ChatOpenCommand, chat, "Open in new window", new FontIcon { Glyph = Icons.WindowNew });
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(viewModel.ChatSelectCommand, chat, Strings.Resources.lng_context_select_msg, new FontIcon { Glyph = Icons.CheckmarkCircle });
            }

            args.ShowAt(flyout, element);
        }

        private bool DialogMark_Loaded(Chat chat)
        {
            return true;
        }

        private bool DialogPin_Loaded(Chat chat)
        {
            //if (!chat.IsPinned)
            //{
            //    var count = ViewModel.Dialogs.LegacyItems.Where(x => x.IsPinned).Count();
            //    var max = ViewModel.CacheService.Config.PinnedDialogsCountMax;

            //    return count < max ? Visibility.Visible : Visibility.Collapsed;
            //}

            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (position?.Source != null)
            {
                return false;
            }

            return true;
        }

        private bool DialogArchive_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (ViewModel.CacheService.IsSavedMessages(chat) || position?.Source != null || chat.Id == 777000)
            {
                return false;
            }

            return true;
        }

        private bool DialogNotify_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (ViewModel.CacheService.IsSavedMessages(chat) || position?.Source is ChatSourcePublicServiceAnnouncement)
            {
                return false;
            }

            return true;
        }

        public bool DialogClear_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (position?.Source != null)
            {
                return false;
            }

            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup != null)
                {
                    return string.IsNullOrEmpty(supergroup.Username) && !super.IsChannel;
                }
            }

            return true;
        }

        private bool DialogDelete_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (position?.Source is ChatSourceMtprotoProxy)
            {
                return false;
            }

            //if (dialog.With is TLChannel channel)
            //{
            //    return Visibility.Visible;
            //}
            //else if (dialog.Peer is TLPeerUser userPeer)
            //{
            //    return Visibility.Visible;
            //}
            //else if (dialog.Peer is TLPeerChat chatPeer)
            //{
            //    return dialog.With is TLChatForbidden || dialog.With is TLChatEmpty ? Visibility.Visible : Visibility.Collapsed;
            //}

            //return Visibility.Collapsed;

            return true;
        }

        private string DialogDelete_Text(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Items.ChatList);
            if (position?.Source is ChatSourcePublicServiceAnnouncement)
            {
                return Strings.Resources.PsaHide;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                return super.IsChannel ? Strings.Resources.LeaveChannelMenu : Strings.Resources.LeaveMegaMenu;
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                return Strings.Resources.DeleteAndExit;
            }

            return Strings.Resources.Delete;
        }

        #endregion

        #region Reorder

        private void Chats_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items[0] is Chat chat)
            {
                var position = chat.GetPosition(ViewModel.Items.ChatList);
                if (position == null || !position.IsPinned || e.Items.Count > 1 || ChatsList.SelectionMode == ListViewSelectionMode.Multiple)
                {
                    ChatsList.CanReorderItems = false;
                    e.Cancel = true;
                }
                else
                {
                    ChatsList.CanReorderItems = true;
                }
            }
        }

        private void Chats_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            ChatsList.CanReorderItems = false;

            var chatList = ViewModel?.Items.ChatList;
            if (chatList == null)
            {
                return;
            }

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is Chat chat)
            {
                var items = ViewModel.Items;
                var index = items.IndexOf(chat);

                var compare = items[index > 0 ? index - 1 : index + 1];

                var position = compare.GetPosition(items.ChatList);
                if (position == null)
                {
                    return;
                }

                if (position.Source != null && index > 0)
                {
                    position = items[index + 1].GetPosition(items.ChatList);
                }

                if (position.IsPinned)
                {
                    var pinned = items.Where(x =>
                    {
                        var position = x.GetPosition(items.ChatList);
                        if (position == null)
                        {
                            return false;
                        }

                        return position.IsPinned;
                    }).Select(x => x.Id).ToArray();

                    ViewModel.ProtoService.Send(new SetPinnedChats(chatList, pinned));
                }
                else
                {
                    var real = chat.GetPosition(items.ChatList);
                    if (real != null)
                    {
                        items.Handle(chat.Id, real.Order);
                    }
                }
            }
        }

        #endregion

        private string ConvertCount(int count)
        {
            return Locale.Declension("Chats", count);
        }

        private SpriteVisual _hitTest;
        private ContainerVisual _container;
        private Visual _visual;
        private ContainerVisual _indicator;

        private bool _hasInitialLoadedEventFired;
        private InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_hasInitialLoadedEventFired)
            {
                var root = VisualTreeHelper.GetChild(this, 0) as UIElement;

                _visual = ElementCompositionPreview.GetElementVisual(root);

                _hitTest = _visual.Compositor.CreateSpriteVisual();
                _hitTest.Brush = _visual.Compositor.CreateColorBrush(Windows.UI.Colors.Transparent);
                _hitTest.RelativeSizeAdjustment = Vector2.One;

                _container = _visual.Compositor.CreateContainerVisual();
                _container.Children.InsertAtBottom(_hitTest);
                _container.RelativeSizeAdjustment = Vector2.One;

                ElementCompositionPreview.SetElementChildVisual(this, _container);

                ConfigureInteractionTracker();
            }

            _hasInitialLoadedEventFired = true;
        }

        private void ConfigureInteractionTracker()
        {
            _interactionSource = VisualInteractionSource.Create(_hitTest);

            //Configure for x-direction panning
            _interactionSource.ManipulationRedirectionMode = VisualInteractionSourceRedirectionMode.CapableTouchpadOnly;
            _interactionSource.PositionXSourceMode = InteractionSourceMode.EnabledWithInertia;
            _interactionSource.PositionXChainingMode = InteractionChainingMode.Never;
            _interactionSource.IsPositionXRailsEnabled = true;

            //Create tracker and associate interaction source
            _tracker = InteractionTracker.CreateWithOwner(_visual.Compositor, this);
            _tracker.InteractionSources.Add(_interactionSource);

            _tracker.MaxPosition = new Vector3(72);
            _tracker.MinPosition = new Vector3(-72);

            _tracker.Properties.InsertBoolean("CanGoNext", _canGoNext);
            _tracker.Properties.InsertBoolean("CanGoPrev", _canGoPrev);

            //ConfigureAnimations(_visual, null);
            ConfigureRestingPoints();
        }

        private void ConfigureRestingPoints()
        {
            var neutralX = InteractionTrackerInertiaRestingValue.Create(_visual.Compositor);
            neutralX.Condition = _visual.Compositor.CreateExpressionAnimation("true");
            neutralX.RestingValue = _visual.Compositor.CreateExpressionAnimation("0");

            _tracker.ConfigurePositionXInertiaModifiers(new InteractionTrackerInertiaModifier[] { neutralX });
        }

        private void ConfigureAnimations(Visual visual)
        {
            var viewModel = Main;
            if (viewModel != null)
            {
                var already = viewModel.SelectedFilter;
                if (already == null)
                {
                    return;
                }

                var index = viewModel.Filters.IndexOf(already);

                _canGoNext = index < viewModel.Filters.Count - 1;
                _canGoPrev = index > 0;

                _tracker.Properties.InsertBoolean("CanGoNext", _canGoNext);
                _tracker.Properties.InsertBoolean("CanGoPrev", _canGoPrev);
                _tracker.MaxPosition = new Vector3(_canGoNext ? 72 : 0);
                _tracker.MinPosition = new Vector3(_canGoPrev ? -72 : 0);
            }

            var offsetExp = _visual.Compositor.CreateExpressionAnimation("(tracker.Position.X > 0 && !tracker.CanGoNext) || (tracker.Position.X <= 0 && !tracker.CanGoPrev) ? 0 : -tracker.Position.X");
            offsetExp.SetReferenceParameter("tracker", _tracker);
            visual.StartAnimation("Offset.X", offsetExp);
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType is Windows.Devices.Input.PointerDeviceType.Touch or Windows.Devices.Input.PointerDeviceType.Pen)
            {
                try
                {
                    _interactionSource.TryRedirectForManipulation(e.GetCurrentPoint(this));
                }
                catch { }
            }
        }

        public void ValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {
            if (_indicator == null && (_tracker.Position.X > 0.0001f || _tracker.Position.X < -0.0001f) /*&& Math.Abs(e.Cumulative.Translation.X) >= 45*/)
            {
                var sprite = _visual.Compositor.CreateSpriteVisual();
                sprite.Size = new Vector2(30, 30);
                sprite.CenterPoint = new Vector3(15);

                var surface = LoadedImageSurface.StartLoadFromUri(new Uri("ms-appx:///Assets/Images/Reply.png"));
                surface.LoadCompleted += (s, e) =>
                {
                    sprite.Brush = _visual.Compositor.CreateSurfaceBrush(s);
                };

                var ellipse = _visual.Compositor.CreateEllipseGeometry();
                ellipse.Radius = new Vector2(15);

                var ellipseShape = _visual.Compositor.CreateSpriteShape(ellipse);
                ellipseShape.FillBrush = _visual.Compositor.CreateColorBrush((Windows.UI.Color)Navigation.BootStrapper.Current.Resources["MessageServiceBackgroundColor"]);
                ellipseShape.Offset = new Vector2(15);

                var shape = _visual.Compositor.CreateShapeVisual();
                shape.Shapes.Add(ellipseShape);
                shape.Size = new Vector2(30, 30);

                _indicator = _visual.Compositor.CreateContainerVisual();
                _indicator.Children.InsertAtBottom(shape);
                _indicator.Children.InsertAtTop(sprite);
                _indicator.Size = new Vector2(30, 30);
                _indicator.CenterPoint = new Vector3(15);
                _indicator.Scale = new Vector3();

                _container.Children.InsertAtTop(_indicator);
            }

            var offset = (_tracker.Position.X > 0 && !_canGoNext) || (_tracker.Position.X <= 0 && !_canGoPrev) ? 0 : Math.Max(0, Math.Min(72, Math.Abs(_tracker.Position.X)));

            var abs = Math.Abs(offset);
            var percent = abs / 72f;

            var width = ActualSize.X;
            var height = ActualSize.Y;

            if (_indicator != null)
            {
                _indicator.Offset = new Vector3(_tracker.Position.X > 0 ? width - percent * 60 : -30 + percent * 55, (height - 30) / 2, 0);
                _indicator.Scale = new Vector3(_tracker.Position.X > 0 ? 0.8f + percent * 0.2f : -(0.8f + percent * 0.2f), 0.8f + percent * 0.2f, 1);
                _indicator.Opacity = percent;
            }
        }

        public void InertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
        {
            var position = _tracker.Position;
            if (position.X >= 72 && _canGoNext || position.X <= -72 && _canGoPrev)
            {
                var main = this.Ancestors<MainPage>().FirstOrDefault();
                if (main == null)
                {
                    return;
                }

                var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();

                if (position.X >= 72 && _canGoNext)
                {
                    offset.InsertKeyFrame(0, new Vector3(-72, 0, 0));
                    offset.InsertKeyFrame(1, new Vector3(0));

                    main.ScrollFolder(+1, true);
                }
                else if (position.X <= -72 && _canGoPrev)
                {
                    offset.InsertKeyFrame(0, new Vector3(72, 0, 0));
                    offset.InsertKeyFrame(1, new Vector3(0));

                    main.ScrollFolder(-1, true);
                }

                offset.Duration = TimeSpan.FromMilliseconds(250);
                sender.TryUpdatePositionWithAnimation(offset);

                opacity.InsertKeyFrame(0, 0);
                opacity.InsertKeyFrame(1, 1);
                opacity.Duration = TimeSpan.FromMilliseconds(250);

                _visual.StartAnimation("Opacity", opacity);
            }
        }

        public void IdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            ConfigureAnimations(_visual);
        }

        public void InteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            ConfigureAnimations(_visual);
        }

        public void RequestIgnored(InteractionTracker sender, InteractionTrackerRequestIgnoredArgs args)
        {

        }

        public void CustomAnimationStateEntered(InteractionTracker sender, InteractionTrackerCustomAnimationStateEnteredArgs args)
        {

        }
    }
}
