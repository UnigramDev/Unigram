//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Gallery;
using Telegram.ViewModels.Users;
using Telegram.Views.Popups;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Controls.Gallery
{
    public sealed partial class GalleryWindow : OverlayWindow, IGalleryDelegate
    //, IHandle<UpdateDeleteMessages>
    //, IHandle<UpdateMessageContent>
    {
        public GalleryViewModelBase ViewModel => DataContext as GalleryViewModelBase;

        public IClientService ClientService => ViewModel?.ClientService;

        private WeakReference<FrameworkElement> _closing;

        private readonly DispatcherTimer _inactivityTimer;

        private readonly Visual _layout;

        private readonly Visual _layer;
        private readonly Visual _layerFullScreen;
        private readonly Visual _bottom;

        private bool _wasFullScreen;
        private bool _lastFullScreen;

        private bool _unloaded;

        private double? _initialPosition;
        public double InitialPosition
        {
            get => _initialPosition ?? 0;
            set => _initialPosition = value > 0 ? value : null;
        }

        private GalleryWindow()
        {
            InitializeComponent();

            _layout = ElementComposition.GetElementVisual(LayoutRoot);

            _layer = ElementComposition.GetElementVisual(Layer);
            _layerFullScreen = ElementComposition.GetElementVisual(LayerFullScreen);
            _bottom = ElementComposition.GetElementVisual(BottomPanel);

            _layer.Opacity = 0;
            _layerFullScreen.Opacity = 0;
            _bottom.Opacity = 0;

            _inactivityTimer = new DispatcherTimer();
            _inactivityTimer.Tick += OnTick;
            _inactivityTimer.Interval = TimeSpan.FromSeconds(1.5);
            _inactivityTimer.Start();

            ScrollingHost.AddHandler(TappedEvent, new TappedEventHandler(OnTapped), true);
        }

        private void OnTick(object sender, object e)
        {
            _inactivityTimer.Stop();
            ShowHideTransport(false);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && IsLoaded && !SafeArea.Contains(e))
            {
                _inactivityTimer.Stop();
                ShowHideTransport(false);
            }

            try
            {
                base.OnPointerExited(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            _inactivityTimer.Stop();
            ShowHideTransport(true);

            try
            {
                base.OnPointerPressed(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            _inactivityTimer.Stop();

            try
            {
                base.OnPointerMoved(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }

            var point = e.GetCurrentPoint(this);
            if (ActualWidth - point.Position.X < 1)
            {
                ShowHideTransport(false);
                return;
            }

            ShowHideTransport(true);

            if (_transportEntered)
            {
                return;
            }
            else if (e.OriginalSource is FrameworkElement element)
            {
                var button = element.GetParentOrSelf<ButtonBase>();
                if (button is not null and not FileButton)
                {
                    return;
                }
            }

            _inactivityTimer.Start();

            //var point = e.GetCurrentPoint(Controls);
            //if (point.Position.X < 0
            //    || point.Position.Y < 0
            //    || point.Position.X > Controls.ActualWidth
            //    || point.Position.Y > Controls.ActualHeight)
            //{
            //    _inactivityTimer.Start();
            //}
        }

        private void OnTapped(object sender, TappedRoutedEventArgs e)
        {
            if (!_areInteractionsEnabled)
            {
                return;
            }

            if (e.PointerDeviceType != PointerDeviceType.Mouse)
            {
                _inactivityTimer.Stop();
                ShowHideTransport(true);
            }
            else
            {
                var container = GetElement(CarouselDirection.None);
                var point = e.GetPosition(container);

                if ((_current != null || container.IsTextSelectionEnabled)
                    && point.X >= 0
                    && point.Y >= 0
                    && point.X <= container.ActualWidth
                    && point.Y <= container.ActualHeight)
                {
                    if (_current != null)
                    {
                        Controls.TogglePlaybackState();
                    }

                    return;
                }

                if (e.OriginalSource is FrameworkElement element)
                {
                    var button = element.GetParentOrSelf<ButtonBase>();
                    if (button is FileButton)
                    {
                        return;
                    }
                }

                OnBackRequested(new BackRequestedRoutedEventArgs());
            }
        }

        private void Controls_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            _transportEntered = true;
        }

        private void Controls_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _transportEntered = false;
        }

        private bool _transportCollapsed = false;
        private bool _transportFocused = false;
        private bool _transportEntered = false;

        private void ShowHideTransport(bool show)
        {
            if (show != _transportCollapsed || _unloaded || (_transportFocused && !show))
            {
                return;
            }

            if (show is false && XamlRoot != null)
            {
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
                {
                    if (popup.Child is not GalleryWindow)
                    {
                        return;
                    }
                }
            }

            _transportCollapsed = !show;
            BottomPanel.IsHitTestVisible = false;
            BottomPanel.Visibility = Visibility.Visible;

            if (show)
            {
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
            }
            else
            {
                Window.Current.CoreWindow.PointerCursor = null;
            }

            var parent = ElementComposition.GetElementVisual(BottomPanel);
            var next = ElementComposition.GetElementVisual(NextButton);
            var prev = ElementComposition.GetElementVisual(PrevButton);
            var back = ElementComposition.GetElementVisual(BackButton);

            parent.Opacity = next.Opacity = prev.Opacity = back.Opacity = 1;

            var batch = parent.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                BottomPanel.IsHitTestVisible = !_transportCollapsed;
            };

            var opacity = parent.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);
            parent.StartAnimation("Opacity", opacity);
            next.StartAnimation("Opacity", opacity);
            prev.StartAnimation("Opacity", opacity);
            back.StartAnimation("Opacity", opacity);

            batch.End();
        }

        private void Transport_GotFocus(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Control control && control.FocusState is FocusState.Keyboard or FocusState.Programmatic)
            {
                _transportFocused = true;
                ShowHideTransport(true);
            }
        }

        private void Transport_LostFocus(object sender, RoutedEventArgs e)
        {
            _transportFocused = false;
            _inactivityTimer.Start();
        }

        public void Handle(UpdateDeleteMessages update)
        {
            if (update.FromCache || !update.IsPermanent)
            {
                return;
            }

            this.BeginOnUIThread(() =>
            {
                var viewModel = ViewModel;
                if (viewModel != null && viewModel.SelectedItem is GalleryMessage message && message.ChatId == update.ChatId && update.MessageIds.Any(x => x == message.Id))
                {
                    OnBackRequestedOverride(this, new BackRequestedRoutedEventArgs());
                }
            });
        }

        public void Handle(UpdateMessageContent update)
        {
            this.BeginOnUIThread(() =>
            {
                var viewModel = ViewModel;
                if (viewModel != null && viewModel.SelectedItem is GalleryMessage message && message.ChatId == update.ChatId && message.Id == update.MessageId && message.SelfDestructType is MessageSelfDestructTypeTimer)
                {
                    OnBackRequestedOverride(this, new BackRequestedRoutedEventArgs());
                }
            });
        }

        public void OpenFile(GalleryMedia item, File file, bool force = false)
        {
            Play(CurrentElement, item, force: force);
        }

        public void OpenItem(GalleryMedia item)
        {
            OnBackRequested(new BackRequestedRoutedEventArgs());
        }

        public static Task<ContentDialogResult> ShowAsync(ViewModelBase viewModelBase, IStorageService storageService, Chat chat, FrameworkElement closing = null)
        {
            var clientService = viewModelBase.ClientService;
            var aggregator = viewModelBase.Aggregator;
            var navigationService = viewModelBase.NavigationService;

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var user = clientService.GetUser(chat);
                if (user == null)
                {
                    return Task.FromResult(ContentDialogResult.None);
                }

                var userFull = clientService.GetUserFull(user.Id);
                if (userFull?.Photo == null && userFull?.PublicPhoto == null && userFull?.PersonalPhoto == null)
                {
                    return Task.FromResult(ContentDialogResult.None);
                }

                var viewModel = new UserPhotosViewModel(clientService, storageService, aggregator, user, userFull);
                viewModel.NavigationService = navigationService;
                return ShowAsync(navigationService.XamlRoot, viewModel, closing);
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                var basicGroupFull = clientService.GetBasicGroupFull(chat);
                if (basicGroupFull?.Photo == null)
                {
                    return Task.FromResult(ContentDialogResult.None);
                }

                var viewModel = new ChatPhotosViewModel(clientService, storageService, aggregator, chat, basicGroupFull.Photo);
                viewModel.NavigationService = navigationService;
                return ShowAsync(navigationService.XamlRoot, viewModel, closing);
            }
            else if (chat.Type is ChatTypeSupergroup)
            {
                var supergroupFull = clientService.GetSupergroupFull(chat);
                if (supergroupFull?.Photo == null)
                {
                    return Task.FromResult(ContentDialogResult.None);
                }

                var viewModel = new ChatPhotosViewModel(clientService, storageService, aggregator, chat, supergroupFull.Photo);
                viewModel.NavigationService = navigationService;
                return ShowAsync(navigationService.XamlRoot, viewModel, closing);
            }

            return Task.FromResult(ContentDialogResult.None);
        }

        public static Task<ContentDialogResult> ShowAsync(ViewModelBase viewModelBase, IStorageService storageService, User user, FrameworkElement closing = null)
        {
            var clientService = viewModelBase.ClientService;
            var aggregator = viewModelBase.Aggregator;
            var navigationService = viewModelBase.NavigationService;

            var userFull = clientService.GetUserFull(user.Id);
            if (userFull?.Photo == null && userFull?.PublicPhoto == null && userFull?.PersonalPhoto == null)
            {
                return Task.FromResult(ContentDialogResult.None);
            }

            var viewModel = new UserPhotosViewModel(clientService, storageService, aggregator, user, userFull);
            viewModel.NavigationService = navigationService;
            return ShowAsync(navigationService.XamlRoot, viewModel, closing);
        }

        public static Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot, GalleryViewModelBase parameter, FrameworkElement closing = null, double timestamp = 0, VideoPlayerBase player = null)
        {
            var popup = new GalleryWindow
            {
                InitialPosition = timestamp
            };

            return popup.ShowAsyncInternal(xamlRoot, parameter, closing, player);
        }

        private Task<ContentDialogResult> ShowAsyncInternal(XamlRoot xamlRoot, GalleryViewModelBase parameter, FrameworkElement closing = null, VideoPlayerBase player = null)
        {
            if (closing != null && closing.IsConnected() && !SettingsService.Current.FullScreenGallery)
            {
                _closing = new WeakReference<FrameworkElement>(closing);
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", closing);
            }

            Load(parameter);

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                if (_unloaded)
                {
                    return;
                }

                parameter.Items.CollectionChanged -= OnCollectionChanged;
                parameter.Items.CollectionChanged += OnCollectionChanged;

                PrepareNext(0, true, false, player);

                var applicationView = ApplicationView.GetForCurrentView();

                _wasFullScreen = applicationView.IsFullScreenMode || ApiInfo.IsXbox;

                if (SettingsService.Current.FullScreenGallery && !_wasFullScreen)
                {
                    applicationView.TryEnterFullScreenMode();
                }

                applicationView.VisibleBoundsChanged += OnVisibleBoundsChanged;
                OnVisibleBoundsChanged(applicationView, null);

                InitializeBackButton();
                Controls.Focus(FocusState.Programmatic);

                Loaded -= handler;

                var viewModel = ViewModel;
                if (viewModel != null)
                {
                    await viewModel.NavigatedToAsync(parameter, NavigationMode.New, null);
                }
            });

            Loaded += handler;
            return ShowAsync(xamlRoot);
        }

        protected override void MaskTitleAndStatusBar(WindowContext window)
        {
            base.MaskTitleAndStatusBar(window);
            window.SetTitleBar(TitleBar);
        }

        private void InitializeBackButton()
        {
            BackButton.Glyph = "\uE72B";
            BackButton.Margin = new Thickness(0, -40, 0, 0);
            BackButton.HorizontalAlignment = HorizontalAlignment.Left;
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            var fullScreen = sender.IsFullScreenMode || ApiInfo.IsXbox;

            if (_lastFullScreen != fullScreen)
            {
                _lastFullScreen = fullScreen;

                //Window.Current.Content.Visibility = sender.IsFullScreenMode
                //    ? Visibility.Collapsed
                //    : Visibility.Visible;

                Controls.IsFullScreen = fullScreen;
                Padding = new Thickness(0, fullScreen ? 0 : 40, 0, 0);

                var stretch = fullScreen
                    ? Stretch.UniformToFill
                    : Stretch.Uniform;

                Element2.Stretch = stretch;
                Element0.Stretch = stretch;
                Element1.Stretch = stretch;

                var anim = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                anim.InsertKeyFrame(0, fullScreen ? 0 : 1);
                anim.InsertKeyFrame(1, fullScreen ? 1 : 0);

                _layerFullScreen.StartAnimation("Opacity", anim);
            }
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            PrepareNext(0, true);
        }

        protected override void OnBackRequestedOverride(object sender, BackRequestedRoutedEventArgs e)
        {
            e.Handled = true;

            var applicationView = ApplicationView.GetForCurrentView();
            if (applicationView.IsFullScreenMode && !_wasFullScreen)
            {
                applicationView.ExitFullScreenMode();

                if (e.Key == VirtualKey.Escape && !SettingsService.Current.FullScreenGallery)
                {
                    Cancel();
                    return;
                }
            }

            applicationView.VisibleBoundsChanged -= OnVisibleBoundsChanged;

            var translate = true;

            if (ViewModel != null)
            {
                ViewModel.NavigationService.Window.EnableScreenCapture(ViewModel.GetHashCode());

                ViewModel.Aggregator.Unsubscribe(this);
                ViewModel.Items.CollectionChanged -= OnCollectionChanged;

                Bindings.StopTracking();

                if (ViewModel.SelectedItem == ViewModel.FirstItem && _closing != null)
                {
                    var root = LayoutRoot.CurrentElement;
                    if (root != null && root.IsLoaded && !SettingsService.Current.FullScreenGallery && !_lastFullScreen)
                    {
                        if (_closing.TryGetTarget(out FrameworkElement element) && element.IsConnected())
                        {
                            var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", root);
                            if (animation != null)
                            {
                                void handler(ConnectedAnimation s, object e)
                                {
                                    animation.Completed -= handler;
                                    Hide();
                                }

                                animation.Completed += handler;
                                animation.Configuration = new DirectConnectedAnimationConfiguration();
                                translate = animation.TryStart(element);
                            }
                        }
                    }
                }
            }

            var batch = _layer.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            _layer.StartAnimation("Opacity", CreateScalarAnimation(1, 0, false));
            _bottom.StartAnimation("Opacity", CreateScalarAnimation(1, 0, false));

            if (translate)
            {
                _layout.StartAnimation("Offset.Y", CreateScalarAnimation(_layout.Offset.Y, ActualSize.Y, false));
                batch.Completed += (s, args) =>
                {
                    Hide();
                };
            }

            batch.End();

            Dispose();
            Unload();
        }

        private void Preview_ImageOpened(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var image = sender as GalleryContent;
            if (image.Item != viewModel.FirstItem)
            {
                return;
            }

            var item = image.Item;
            if (item == null)
            {
                return;
            }

            _layer.StartAnimation("Opacity", CreateScalarAnimation(0, 1, true));
            _bottom.StartAnimation("Opacity", CreateScalarAnimation(0, 1, true));

            var container = LayoutRoot.CurrentElement as GalleryContent;

            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
            if (animation != null)
            {
                if (animation.TryStart(image))
                {
                    void handler(ConnectedAnimation s, object args)
                    {
                        animation.Completed -= handler;

                        //Transport.Show();
                        InitializeVideo(container, item);
                    }

                    animation.Completed += handler;
                    return;
                }
            }

            //Transport.Show();
            InitializeVideo(container, item);
        }

        private void InitializeVideo(GalleryContent content, GalleryMedia item)
        {
            if (item.IsVideo)
            {
                Play(content, item);
                GalleryCompactOverlay.Pause();
            }
        }

        private CompositionAnimation CreateScalarAnimation(float from, float to, bool showing)
        {
            var scalar = _layer.Compositor.CreateScalarKeyFrameAnimation();
            scalar.InsertKeyFrame(0, from);
            scalar.InsertKeyFrame(1, to, ConnectedAnimationService.GetForCurrentView().DefaultEasingFunction);
            scalar.Duration = TimeSpan.FromMilliseconds(showing ? 250 : 150);

            return scalar;
        }

        #region Binding

        private string ConvertFrom(object with)
        {
            if (with is User user)
            {
                return user.FullName();
            }
            else if (with is Chat chat)
            {
                return chat.Title;
            }

            return null;
        }

        private string ConvertDate(int value)
        {
            if (value == 0)
            {
                return string.Empty;
            }

            return Formatter.DateAt(value);
        }

        private string ConvertOf(GalleryMedia item, int index, int count)
        {
            if (item.IsPersonal)
            {
                return Strings.CustomAvatarTooltip;
            }
            else if (item.IsPublic)
            {
                return Strings.FallbackTooltip;
            }

            return string.Format(Strings.Of, index, count);
        }

        private Visibility ConvertCaption(FormattedText text)
        {
            if (string.IsNullOrEmpty(text?.Text))
            {
                return Visibility.Collapsed;
            }

            Caption.SetText(ViewModel.ClientService, text);
            return Visibility.Visible;
        }

        #endregion

        private GalleryContent _current;

        private void Play(GalleryContent content, GalleryMedia item, VideoPlayerBase player = null, bool force = false)
        {
            if (_unloaded)
            {
                return;
            }

            var position = 0d;
            var file = item.File;

            if (_initialPosition is double initialPosition)
            {
                _initialPosition = null;
                position = initialPosition;
            }
            else if (ViewModel.Settings.Video.TryGetPosition(file, out double knownPosition))
            {
                position = knownPosition;
            }

            if (player != null)
            {
                _current = content;
                _current.Play(player, item, Controls);
            }
            else
            {
                _current = content;
                _current.Play(item, position, Controls, force);
            }

            ViewModel.PlaybackStarted(item);
        }

        private void Dispose()
        {
            ScrollingHost.Zoom(1, true);
            Controls.Stop();

            if (_current != null)
            {
                _current.Stop(out var item, out double position);

                if (item is GalleryMessage message)
                {
                    ViewModel?.Settings.Video.SetPosition(item.File, position);
                    ViewModel?.Aggregator.Publish(new UpdateMessageContentOpened(message.ChatId, message.Id));
                }

                _current = null;
            }

            ViewModel?.PlaybackStopped();
        }

        public void UpdateAdvertisement(VideoMessageAdvertisement advertisement)
        {
            Sponsored.UpdateAdvertisement(advertisement);
        }

        private void Load(GalleryViewModelBase parameter)
        {
            parameter.Delegate = this;
            DataContext = parameter;
            Bindings.Update();

            ViewModel?.Aggregator.Subscribe<UpdateDeleteMessages>(this, Handle)
                    .Subscribe<UpdateMessageContent>(Handle);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unload();
        }

        private void Unload()
        {
            if (_unloaded)
            {
                return;
            }

            _unloaded = true;

            if (ViewModel != null)
            {
                ViewModel.Delegate = null;
                ViewModel.Aggregator.Unsubscribe(this);
                ViewModel.PlaybackStopped();
            }

            DataContext = null;
            Bindings.StopTracking();

            Controls.Unload();

            Element0.Unload();
            Element1.Unload();
            Element2.Unload();

            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            var focused = FocusManagerEx.TryGetFocusedElement();
            if (focused is Slider or TextBox or RichEditBox)
            {
                return;
            }

            var modifiers = WindowContext.KeyModifiers();
            var keyCode = (int)args.Key;

            if (args.Key is VirtualKey.Space && modifiers == VirtualKeyModifiers.None)
            {
                Controls.TogglePlaybackState();
                args.Handled = true;
            }
            else if (args.Key is VirtualKey.Left or VirtualKey.GamepadLeftShoulder && modifiers == VirtualKeyModifiers.None)
            {
                ChangeView(CarouselDirection.Previous);
                args.Handled = true;
            }
            else if (args.Key is VirtualKey.Right or VirtualKey.GamepadRightShoulder && modifiers == VirtualKeyModifiers.None)
            {
                ChangeView(CarouselDirection.Next);
                args.Handled = true;
            }
            else if (args.Key is VirtualKey.R && modifiers == VirtualKeyModifiers.Control)
            {
                args.Handled = true;
                Rotate_Click(null, null);
            }
            else if (args.Key is VirtualKey.C && modifiers == VirtualKeyModifiers.Control)
            {
                var container = GetElement(CarouselDirection.None);
                if (container.IsTextSelectionEnabled && container.IsTextSelected)
                {
                    container.CopySelectedText();
                }
                else
                {
                    ViewModel?.Copy();
                }

                args.Handled = true;
            }
            else if (args.Key is VirtualKey.A && modifiers == VirtualKeyModifiers.Control)
            {
                var container = GetElement(CarouselDirection.None);
                if (container.IsTextSelectionEnabled)
                {
                    container.SelectAllText();
                }

                args.Handled = true;
            }
            else if (args.Key is VirtualKey.S && modifiers == VirtualKeyModifiers.Control)
            {
                ViewModel?.Save();
                args.Handled = true;
            }
            else if (args.Key is VirtualKey.F11 || (args.Key is VirtualKey.F && modifiers == VirtualKeyModifiers.Control))
            {
                FullScreen_Click(null, null);
                args.Handled = true;
            }
            else if (keyCode is 187 or 189 || args.Key is VirtualKey.Add or VirtualKey.Subtract)
            {
                ScrollingHost.Zoom(keyCode is 187 || args.Key is VirtualKey.Add);
                args.Handled = true;
            }
            else
            {
                Controls.ProcessKeyboardAccelerators(args);
            }
        }

        private void LayoutRoot_ViewChanging(object sender, CarouselViewChangingEventArgs e)
        {
            TryChangeView(e.Direction);
        }

        private void LayoutRoot_ViewChanged(object sender, CarouselViewChangedEventArgs e)
        {
            if (ViewModel?.SelectedItem is GalleryMedia item && item.IsVideo && (item.IsLoopingEnabled || SettingsService.Current.IsStreamingEnabled))
            {
                Play(CurrentElement, item);
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeView(CarouselDirection.Previous);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeView(CarouselDirection.Next);
        }

        public GalleryContent CurrentElement => LayoutRoot.CurrentElement as GalleryContent;

        #region Flippitiflip

        protected override Size ArrangeOverride(Size finalSize)
        {
            LayoutRoot.Width = finalSize.Width;
            LayoutRoot.Height = Math.Max(0, finalSize.Height - Padding.Top);

            // TODO: setting a max width/height to the element causes a weird clipping effect if
            // the final calculated size is actually larger than that to accommodate for rotation
            //static void Apply(AspectView element, Size size)
            //{
            //    if (element.Constraint is not Size)
            //    {
            //        element.MaxWidth = size.Width;
            //        element.MaxHeight = size.Height;
            //    }
            //}

            //Apply(Element2, finalSize);
            //Apply(Element0, finalSize);
            //Apply(Element1, finalSize);

            _layout?.Offset = new Vector3(0, 0, 0);

            return base.ArrangeOverride(finalSize);
        }

        private bool ChangeView(CarouselDirection direction)
        {
            if (TryChangeView(direction))
            {
                LayoutRoot.ChangeView(direction);
                return true;
            }

            return false;
        }

        private bool TryChangeView(CarouselDirection direction)
        {
            var viewModel = ViewModel;
            if (viewModel == null || LayoutRoot.IsScrolling)
            {
                return false;
            }

            var selected = viewModel.SelectedIndex;
            var previous = selected > 0;
            var next = selected < viewModel.Items.Count - 1;

            if (previous && direction == CarouselDirection.Previous)
            {
                viewModel.SelectedItem = viewModel.Items[selected - 1];
                PrepareNext(direction, dispose: true);

                viewModel.LoadMore();
                return true;
            }
            else if (next && direction == CarouselDirection.Next)
            {
                viewModel.SelectedItem = viewModel.Items[selected + 1];
                PrepareNext(direction, dispose: true);

                viewModel.LoadMore();
                return true;
            }

            return false;
        }

        private void PrepareNext(CarouselDirection direction, bool initialize = false, bool dispose = false, VideoPlayerBase player = null)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            LayoutRoot.PrepareElements(direction,
                out GalleryContent previous,
                out GalleryContent target,
                out GalleryContent next);

            target.IsEnabled = true;
            previous.IsEnabled = false;
            next.IsEnabled = false;

            var index = viewModel.SelectedIndex;
            if (index < 0 || viewModel.Items.Empty())
            {
                return;
            }

            var set = TrySet(target, viewModel.Items[index]);
            TrySet(previous, index > 0 ? viewModel.Items[index - 1] : null);
            TrySet(next, index < viewModel.Items.Count - 1 ? viewModel.Items[index + 1] : null);

            LayoutRoot.HasPrevious = index > 0;
            LayoutRoot.HasNext = index < viewModel.Items.Count - 1;

            if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
            {
                PrevButton.IsEnabled = index > 0;
                NextButton.IsEnabled = index < viewModel.Items.Count - 1;

                PrevButton.Visibility = Visibility.Visible;
                NextButton.Visibility = Visibility.Visible;
            }
            else
            {
                PrevButton.Visibility = Visibility.Collapsed;
                NextButton.Visibility = Visibility.Collapsed;
            }

            if (set)
            {
                Dispose();

                if (viewModel.Items[index].HasProtectedContent is false)
                {
                    viewModel.OpenMessage(viewModel.Items[index]);
                }
            }
            else if (dispose)
            {
                Dispose();
            }

            var item = viewModel.Items[index];
            if (item.IsVideo /*&& initialize*/)
            {
                Play(target, item, player);
            }
        }

        private bool TrySet(GalleryContent element, GalleryMedia content)
        {
            if (Equals(element.Item, content))
            {
                return false;
            }

            element.UpdateItem(this, content);
            return true;
        }

        #endregion

        #region Context menu

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            Menu_ContextRequested(sender as UIElement, null);
        }

        private void Menu_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null || sender is not FrameworkElement element)
            {
                return;
            }

            var item = viewModel.SelectedItem;
            if (item == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var container = GetElement(CarouselDirection.None);
            if (container.IsTextSelectionEnabled && args != null)
            {
                if (container.IsTextSelected)
                {
                    flyout.CreateFlyoutItem(container.CopySelectedText, Strings.Copy, Icons.Copy, VirtualKey.C, VirtualKeyModifiers.Control);

                    var translate = ViewModel.ClientService.Session.Resolve<ITranslateService>();
                    if (translate.CanTranslateText(container.SelectedText))
                    {
                        void handler()
                        {
                            var language = LanguageIdentification.IdentifyLanguage(container.SelectedText);
                            var popup = new TranslatePopup(translate, container.SelectedText, language, SettingsService.Current.Translate.To, true);

                            ViewModel.ShowPopup(popup, requestedTheme: ElementTheme.Dark);
                        }

                        flyout.CreateFlyoutItem(handler, Strings.TranslateMessage, Icons.Translate);
                    }
                }
                else
                {
                    var translate = ViewModel.ClientService.Session.Resolve<ITranslateService>();
                    if (translate.CanTranslateText(container.RecognizedText))
                    {
                        void handler()
                        {
                            var language = LanguageIdentification.IdentifyLanguage(container.RecognizedText);
                            var popup = new TranslatePopup(translate, container.RecognizedText, language, SettingsService.Current.Translate.To, true);

                            ViewModel.ShowPopup(popup, requestedTheme: ElementTheme.Dark);
                        }

                        flyout.CreateFlyoutItem(handler, Strings.TranslateMessage, Icons.Translate);
                    }
                }

                flyout.CreateFlyoutItem(container.SelectAllText, Strings.SelectAll, key: VirtualKey.A, modifiers: VirtualKeyModifiers.Control);

                flyout.ShowAt(element, args, FlyoutShowMode.Transient);
                return;
            }

            PopulateContextRequested(flyout, viewModel, item);

            if (args != null)
            {
                flyout.ShowAt(element, args);
            }
            else
            {
                flyout.ShowAt(element, FlyoutPlacementMode.TopEdgeAlignedRight);
            }
        }

        private void PopulateContextRequested(MenuFlyout flyout, GalleryViewModelBase viewModel, GalleryMedia item)
        {
            flyout.CreateFlyoutItem(() => item.CanBeViewed, viewModel.View, Strings.ShowInChat, Icons.ChatEmpty);
            flyout.CreateFlyoutItem(() => item.CanBeShared, viewModel.Forward, Strings.Forward, Icons.Share);
            flyout.CreateFlyoutItem(() => item.CanBeCopied, viewModel.Copy, Strings.Copy, Icons.Copy, VirtualKey.C);
            flyout.CreateFlyoutItem(() => item.CanBeSaved, viewModel.Save, Strings.SaveAs, Icons.SaveAs, VirtualKey.S);

            if (viewModel is UserPhotosViewModel userPhotos && userPhotos.CanDelete && userPhotos.SelectedIndex > 0)
            {
                flyout.CreateFlyoutItem(() => viewModel.CanDelete, userPhotos.SetAsMain, Strings.SetAsMain, Icons.PersonCircle);
            }
            else if (viewModel is ChatPhotosViewModel chatPhotos && chatPhotos.CanDelete && chatPhotos.SelectedIndex > 0)
            {
                flyout.CreateFlyoutItem(() => viewModel.CanDelete, chatPhotos.SetAsMain, Strings.SetAsMain, Icons.PersonCircle);
            }

            flyout.CreateFlyoutItem(() => viewModel.CanOpenWith, viewModel.OpenWith, Strings.OpenWith, Icons.OpenWith);
            flyout.CreateFlyoutItem(() => viewModel.CanDelete, viewModel.Delete, Strings.Delete, Icons.Delete, destructive: true);
        }

        #endregion

        #region Compact overlay

        private void Compact_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            var item = viewModel?.SelectedItem;

            if (item == null)
            {
                return;
            }

            //if (_mediaPlayer == null || _mediaPlayer.Source == null)
            {
                Play(CurrentElement, item);
            }

            var player = _current.Video;
            player.IsUnloadedExpected = true;

            _current.Video = null;
            OnBackRequestedOverride(this, new BackRequestedRoutedEventArgs());

            GalleryCompactOverlay.CreateOrUpdate(viewModel, item, player);
        }

        #endregion

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            OnBackRequestedOverride(this, new BackRequestedRoutedEventArgs());
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ScrollingHost.Zoom(true);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ScrollingHost.Zoom(false);
        }

        private void Rotate_Click(object sender, RoutedEventArgs e)
        {
            if (LayoutRoot.CurrentElement is AspectView view)
            {
                view.RotationAngle = view.RotationAngle switch
                {
                    RotationAngle.Angle0 => RotationAngle.Angle270,
                    RotationAngle.Angle270 => RotationAngle.Angle180,
                    RotationAngle.Angle180 => RotationAngle.Angle90,
                    _ => RotationAngle.Angle0
                };

                if (ViewModel?.SelectedItem != null)
                {
                    ViewModel.SelectedItem.RotationAngle = view.RotationAngle;
                }
            }
        }

        private bool _areInteractionsEnabled = true;

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var neutral = ScrollingHost.ZoomFactor.AlmostEquals(1);
            if (neutral == _areInteractionsEnabled)
            {
                Element2.Opacity = 1;
                Element0.Opacity = 1;
                Element1.Opacity = 1;
                return;
            }

            _areInteractionsEnabled = neutral;
            LayoutRoot.IsManipulationEnabled = neutral;

            ZoomIn.IsEnabled = ScrollingHost.CanZoomIn;
            ZoomOut.IsEnabled = ScrollingHost.CanZoomOut;

            var container = GetElement(CarouselDirection.None);
            container.IsEnabled = neutral;
            container.ManipulationMode = neutral ? ManipulationModes.TranslateY | ManipulationModes.TranslateRailsY | ManipulationModes.System : ManipulationModes.System;

            var container2 = GetElement(CarouselDirection.Previous);
            container2.Opacity = neutral ? 1 : 0;

            var container1 = GetElement(CarouselDirection.Next);
            container1.Opacity = neutral ? 1 : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private GalleryContent GetElement(CarouselDirection direction)
        {
            return LayoutRoot.GetElement(direction) as GalleryContent;
        }

        private void FullScreen_Click(object sender, RoutedEventArgs e)
        {
            var applicationView = ApplicationView.GetForCurrentView();
            if (applicationView.IsFullScreenMode)
            {
                applicationView.ExitFullScreenMode();
            }
            else
            {
                applicationView.TryEnterFullScreenMode();
            }
        }

        private void Recognize_Click(object sender, RoutedEventArgs e)
        {
            SettingsService.Current.ToolTip.Complete("TextRecognizer");

            var container = GetElement(CarouselDirection.None);
            container.RecognizeText();
        }

        private bool _recognizeLoaded;

        private void Recognize_Loaded(object sender, RoutedEventArgs e)
        {
            if (_recognizeLoaded)
            {
                return;
            }

            _recognizeLoaded = true;

            if (SettingsService.Current.ToolTip.Increment("TextRecognizer"))
            {
                ToastPopup.Show(Recognize, Strings.ScanTextFirstTime, Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.Top, dismissAfter: TimeSpan.FromSeconds(3));
            }
        }

        private void ScrollingHost_PanStarting(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var container = GetElement(CarouselDirection.None);
            e.Cancel = container.IsTextSelectionEnabled;
        }
    }
}
