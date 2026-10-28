//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Common.Widgets;
using Telegram.Native.Widgets;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.ApplicationModel;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Content
{
    // A widget in a bubble: the client's side of widget-sandbox's docs/sdk-v1.md §5 and docs/integration.md.
    // The chat starts and pauses it as it does looping media, through IPlayerView.
    public sealed partial class WidgetContent : Control, IContent, IPlayerView, IWidgetCarrier
    {
        private static readonly object _resourcesLock = new();
        private static bool _resourcesLoaded;

        // One link dialog at a time, across widgets; links that arrive meanwhile are dropped (§5.2).
        private static bool _dialogOpen;

        // The one widget whose text may be selected (§5.10): a press in another widget, or outside every widget, drops it.
        private static WidgetContent _selected;

        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private WidgetSession _session;
        // The HTML the bubble was last updated with. It never changes after sending: the bot edits only the state.
        private string _html;
        // The shared widget whose state is polled for this bubble, 0 for none, and the poller doing it.
        private long _polledWidgetId;
        private WidgetPublicDataPoller _poller;
        // The widest the bubble lets the media be, from the last measure.
        private double _available;
        private double _width;
        // 0 when the content decides.
        private double _fixedHeight;
        private double _height = WidgetSizing.PlaceholderHeight;
        private bool _crashCounted;
        // Set by the chat (ViewportChanged). A widget is built only once it is on screen: the list realizes
        // messages well beyond it, and each one booting costs a sandbox, a validator and their threads.
        private bool _visible;

        // The pointer the widget has, and what it claimed of it: bit0 horizontal drags, bit1 vertical, bit2 holds.
        private uint? _pointer;
        private bool _pointerMouse;
        private int _claim = -1;
        // The last press's menu is the widget's contextmenu, not the message's; and what was selected before that press.
        private bool _menuTaken;
        private string _menuSelection;

        // The last change of the auto height on screen, until its layout: the difference, the widget's top in the window
        // before it, and the anchor.
        private double _resizeDelta;
        private double _resizeFrom;
        private bool _resizeKeepsBottom;

        private WindowContext _window;
        // Not minimized or hidden; a widget in a window in the background is paused as one off screen (§5.8).
        private bool _windowVisible = true;

        public WidgetContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(WidgetContent);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
            ActualThemeChanged += OnActualThemeChanged;
        }

        #region InitializeComponent

        private Border Placeholder;
        private TextBlock Stopped;
        private Grid Host;
        private FrameworkElement NetworkMarker;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Placeholder = GetTemplateChild(nameof(Placeholder)) as Border;
            Stopped = GetTemplateChild(nameof(Stopped)) as TextBlock;
            Host = GetTemplateChild(nameof(Host)) as Grid;
            NetworkMarker = GetTemplateChild(nameof(NetworkMarker)) as FrameworkElement;

            ToolTipService.SetToolTip(NetworkMarker, Strings.WidgetNetworkHint);
            AutomationProperties.SetName(NetworkMarker, Strings.WidgetNetworkHint);

            Host.PointerPressed += OnPointerPressed;
            Host.PointerMoved += OnPointerMoved;
            Host.PointerReleased += OnPointerReleased;
            Host.PointerCanceled += OnPointerCanceled;
            Host.PointerCaptureLost += OnPointerCanceled;
            Host.PointerExited += OnPointerExited;
            Host.Holding += OnHolding;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        // The message menu stays closed: the widget had the press as its contextmenu (§5.5).
        public bool TakesContextMenu => _menuTaken;

        // What the message menu copies: the widget's selection as it was before the press that opened the menu, which dropped it.
        public string MenuSelection => _message?.CanBeSaved is false ? null : _menuSelection;

        public void UpdateMessage(MessageViewModel message)
        {
            if (_message != message)
            {
                Close();
                _html = null;
                _visible = false;
            }

            _message = message;

            if (message.Content is not MessageWidget widget || !_templateApplied)
            {
                return;
            }

            if (_html != widget.Html)
            {
                // Different HTML restarts the widget on the same storage, and a stopped one gets another try.
                if (_html != null)
                {
                    Close(false);
                    Stopped.Visibility = Visibility.Collapsed;
                }

                _html = widget.Html;
            }

            // The bot's edits of the state reach the running widget, which is told what changed; a chat re-rendered
            // after a text size change brings the size.
            if (_session != null)
            {
                _session.SetData(widget.ChatData, widget.PublicData);
                UpdateTheme(_session);
            }

            // A tap may send data to the bot's server: the server's flag decides, so the sign is exactly as honest as it.
            NetworkMarker.Visibility = CanQuery(message, widget) ? Visibility.Visible : Visibility.Collapsed;

            _fixedHeight = WidgetSizing.FixedHeight(widget.Height);
            _height = _fixedHeight > 0 ? _fixedHeight : AutoHeight();

            if (_available > 0)
            {
                _width = WidgetSizing.Width(widget.Width, _available);
            }

            InvalidateMeasure();
            TryOpen();
        }

        public void Recycle()
        {
            Close();
            _message = null;
            _html = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageWidget;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_message?.Content is not MessageWidget widget)
            {
                return base.MeasureOverride(availableSize);
            }

            if (!double.IsInfinity(availableSize.Width))
            {
                _available = availableSize.Width;
            }

            var width = WidgetSizing.Width(widget.Width, _available > 0 ? _available : WidgetSizing.MinWidth);
            if (width != _width)
            {
                _width = width;

                if (_session != null)
                {
                    _session.Width = width;
                    _session.View.SetWidth(width);
                }
            }

            base.MeasureOverride(new Size(_width, _height));
            return new Size(_width, _height);
        }

        private double AutoHeight()
        {
            if (_session != null && _session.View.IsLoaded && _session.View.ContentHeight > 0)
            {
                return WidgetSizing.ClampHeight(_session.View.ContentHeight);
            }

            // The height it had last time, so the list does not jump when the message scrolls back in.
            if (Store.TryGetHeight(Key(_message), out double cached))
            {
                return cached;
            }

            return WidgetSizing.PlaceholderHeight;
        }

        private WidgetStore Store => WidgetStore.For(_message.ClientService.SessionId);

        private static WidgetKey Key(MessageViewModel message)
        {
            return new WidgetKey(message.ChatId, message.Id);
        }

        // A scheduled message cannot send queries.
        private static bool CanQuery(MessageViewModel message, MessageWidget widget)
        {
            return widget.CanSendQueries && message.SchedulingState == null;
        }

        // The kind of chat as the Bot API names it. A copy gets none: the bot must not learn where its widget was forwarded.
        private static string ChatType(MessageViewModel message, MessageWidget widget)
        {
            if (widget.IsCopy)
            {
                return string.Empty;
            }

            return message.Chat?.Type switch
            {
                ChatTypePrivate or ChatTypeSecret => "private",
                ChatTypeBasicGroup => "group",
                ChatTypeSupergroup { IsChannel: true } => "channel",
                ChatTypeSupergroup => "supergroup",
                _ => string.Empty
            };
        }

        private static void SetViewer(WidgetOptions options, IClientService clientService)
        {
            if (clientService.TryGetUser(clientService.Options.MyId, out User user))
            {
                options.ViewerId = user.Id;
                options.ViewerFirstName = user.FirstName;
                options.ViewerLastName = user.LastName;
                options.ViewerUsername = user.ActiveUsername();
                options.ViewerIsPremium = user.IsPremium;
            }
        }

        private static void LoadResources()
        {
            lock (_resourcesLock)
            {
                if (!_resourcesLoaded)
                {
                    WidgetView.LoadResources(Path.Combine(Package.Current.InstalledLocation.Path, "WidgetJS"));
                    _resourcesLoaded = true;
                }
            }
        }

        private WidgetTheme CreateTheme()
        {
            var outgoing = _message.IsOutgoing && !_message.IsChannelPost;
            var brushes = WindowContext.TryGetForXamlRoot(XamlRoot, out WindowContext window)
                ? outgoing ? window.Outgoing : window.Incoming
                : outgoing ? Theme.Current.Outgoing : Theme.Current.Incoming;

            return WidgetThemes.Create(brushes, ActualTheme != ElementTheme.Light);
        }

        private void UpdateTheme(WidgetSession session)
        {
            var key = WidgetThemes.Key(ActualTheme != ElementTheme.Light);
            if (session.Theme != key && _message != null)
            {
                session.Theme = key;
                session.View.SetTheme(CreateTheme());
            }
        }

        // A control taken back from the pool already knows how wide it is; a new one waits for its first size.
        private void TryOpen()
        {
            if (_session == null && _visible && _width > 0 && _templateApplied && IsLoaded && _message?.Content is MessageWidget && Stopped.Visibility == Visibility.Collapsed)
            {
                Open();
            }
        }

        private void Open()
        {
            var widget = (MessageWidget)_message.Content;
            var store = Store;
            var key = Key(_message);

            var html = Encoding.UTF8.GetBytes(widget.Html ?? string.Empty);

            // Stopped after 2 crashes in a row, until the app restarts or the message gets new HTML (§5.7).
            var hash = WidgetSession.HashOf(html);
            if (store.Crashes(key, hash) >= 2)
            {
                ShowStopped();
                return;
            }

            // Its own widget, if it went away a moment ago: still running, with its last frame and height.
            var parked = WidgetSessionPool.Take(key, hash, BootStrapper.Current.Compositor);
            if (parked != null)
            {
                Attach(parked);

                if (parked.Width != _width)
                {
                    parked.Width = _width;
                    parked.View.SetWidth(_width);
                }

                UpdateTheme(parked);
                parked.SetData(widget.ChatData, widget.PublicData);
                parked.View.SetVisible(_windowVisible);
                UpdateFromWidget();
                return;
            }

            var culture = LocaleService.Current.CurrentCulture.Name;

            var options = new WidgetOptions();
            options.Width = _width;
            options.DevicePixelRatio = XamlRoot?.RasterizationScale ?? 1;
            options.Locale = string.IsNullOrEmpty(culture) ? "en-US" : culture;
            options.StoppedLabel = Strings.WidgetStopped;
            store.Load(key, options.LocalStorage, options.SessionStorage);

            options.ChatData = widget.ChatData ?? string.Empty;
            options.PublicData = widget.PublicData ?? string.Empty;
            options.IsCopy = widget.IsCopy;
            options.CanQuery = CanQuery(_message, widget);
            options.BackgroundQuery = widget.CanSendBackgroundQueries && options.CanQuery;
            options.ChatType = ChatType(_message, widget);
            SetViewer(options, _message.ClientService);

            var themeKey = WidgetThemes.Key(ActualTheme != ElementTheme.Light);

            WidgetView view;
            try
            {
                LoadResources();

                var compositor = BootStrapper.Current.Compositor;
                view = new WidgetView(compositor, html, CreateTheme(), options);
            }
            catch (Exception)
            {
                ShowStopped();
                return;
            }

            // Before Start: a view with no request handler when it starts refuses every request.
            var session = new WidgetSession(_message.ClientService, view, key, hash, store);
            session.Width = _width;
            session.Theme = themeKey;
            session.ChatData = widget.ChatData;
            session.PublicData = widget.PublicData;

            Attach(session);
            view.SetVisible(_windowVisible);
            view.Start();
        }

        private void Attach(WidgetSession session)
        {
            _session = session;
            _crashCounted = session.View.IsDead;

            session.View.Changed += OnWidgetChanged;
            session.View.ActionRequested += OnActionRequested;
            session.View.CopyTextRequested += OnCopyTextRequested;
            ElementCompositionPreview.SetElementChildVisual(Host, session.View.Visual);

            Stopped.Visibility = Visibility.Collapsed;
            Placeholder.Visibility = Visibility.Visible;
        }

        // park: keep it paused for a while in case its message comes back (WidgetSessionPool); not for a
        // widget being replaced by new HTML, nor one that died.
        private void Close(bool park = true)
        {
            CancelPointer();

            var session = _session;
            if (session == null)
            {
                if (_templateApplied)
                {
                    Stopped.Visibility = Visibility.Collapsed;
                    Placeholder.Visibility = Visibility.Visible;
                }

                return;
            }

            _session = null;
            _resizeDelta = 0;
            UpdatePolling();

            if (_selected == this)
            {
                _selected = null;
            }

            var view = session.View;
            view.Changed -= OnWidgetChanged;
            view.ActionRequested -= OnActionRequested;
            view.CopyTextRequested -= OnCopyTextRequested;
            ElementCompositionPreview.SetElementChildVisual(Host, null);

            // A widget that ran and was put away normally breaks a row of crashes.
            if (view.IsLoaded && !view.IsDead)
            {
                Store.ResetCrashes(session.Key);
            }

            if (park && !view.IsDead)
            {
                WidgetSessionPool.Park(session);
                _ = Task.Run(Store.Flush);
            }
            else
            {
                session.CloseInBackground();
            }

            Placeholder.Visibility = Visibility.Visible;
        }

        private void ShowStopped()
        {
            Stopped.Text = Strings.WidgetStopped;
            Stopped.Visibility = Visibility.Visible;
            Placeholder.Visibility = Visibility.Visible;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_window == null && WindowContext.TryGetForXamlRoot(XamlRoot, out _window))
            {
                _window.ChatThemeChanged += OnChatThemeChanged;
                _window.VisibilityChanged += OnWindowVisibilityChanged;
            }

            TryOpen();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_window != null)
            {
                _window.ChatThemeChanged -= OnChatThemeChanged;
                _window.VisibilityChanged -= OnWindowVisibilityChanged;
                _window = null;
                _windowVisible = true;
            }

            Close();
        }

        private void OnChatThemeChanged(object sender, EventArgs e)
        {
            WidgetThemes.Invalidate();

            if (_session != null)
            {
                UpdateTheme(_session);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_resizeDelta != 0)
            {
                KeepAnchoredEdge();
            }

            TryOpen();
        }

        // The edge the widget anchors stays where it was on screen (docs/sdk-v1.md §5.6). Whichever edge the list's own
        // anchoring kept still, the list scrolls by how far the anchored one moved; a bubble off screen is left to it.
        private void KeepAnchoredEdge()
        {
            var delta = _resizeDelta;
            _resizeDelta = 0;

            var scroll = this.GetParent<ScrollViewer>();
            if (scroll == null)
            {
                return;
            }

            var top = TransformToVisual(null).TransformPoint(default).Y;
            var shift = top - _resizeFrom + (_resizeKeepsBottom ? delta : 0);

            if (Math.Abs(shift) >= 0.5)
            {
                scroll.ChangeView(null, scroll.VerticalOffset + shift, null, true);
            }
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_session != null)
            {
                UpdateTheme(_session);
            }
        }

        #region IPlayerView

        // Live as long as it is on screen, as looping media is: the chat pauses it when it scrolls away.
        public int LoopCount => 0;

        public void ViewportChanged(bool within)
        {
            if (_visible != within)
            {
                _visible = within;
                UpdateVisible();
            }
        }

        #endregion

        private void OnWindowVisibilityChanged(object sender, WindowVisibilityEventArgs e)
        {
            if (_windowVisible != e.IsVisible)
            {
                _windowVisible = e.IsVisible;
                UpdateVisible();
            }
        }

        // Off screen or in the background: no frames, timers at 1 Hz, and the storage goes to disk now (§5.4).
        private void UpdateVisible()
        {
            if (_session == null)
            {
                TryOpen();
                return;
            }

            var visible = _visible && _windowVisible;
            _session.View.SetVisible(visible);
            UpdatePolling();

            if (!visible)
            {
                _ = Task.Run(Store.Flush);
            }
        }

        #region Widget

        // A new height arrives on the UI thread with its frame, and the bubble takes it in the same layout pass, so that the two
        // reach the screen together (WidgetView.ContentHeight). The rest comes from the widget's threads.
        private void OnWidgetChanged(WidgetView sender, object args)
        {
            if (Dispatcher.HasThreadAccess)
            {
                UpdateFromWidget();
            }
            else
            {
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, UpdateFromWidget);
            }
        }

        private void UpdateFromWidget()
        {
            var session = _session;
            if (session == null)
            {
                return;
            }

            var view = session.View;
            UpdatePolling();

            if (view.ContentHeight > 0 && view.IsLoaded)
            {
                Placeholder.Visibility = Visibility.Collapsed;

                if (_fixedHeight == 0)
                {
                    var height = WidgetSizing.ClampHeight(view.ContentHeight);
                    Store.SetHeight(session.Key, height);

                    if (height != _height)
                    {
                        // Read before the layout that moves it; heights that come faster than layouts add up.
                        if (_visible)
                        {
                            if (_resizeDelta == 0)
                            {
                                _resizeFrom = TransformToVisual(null).TransformPoint(default).Y;
                            }

                            _resizeDelta += height - _height;
                            _resizeKeepsBottom = view.KeepsBottom;
                        }

                        _height = height;
                        InvalidateMeasure();
                    }
                }
            }

            // The renderer keeps the last frame with the badge; the bubble keeps its size.
            if (view.IsDead && !_crashCounted)
            {
                _crashCounted = true;
                Store.AddCrash(session.Key, session.Hash);
                Logger.Warning($"Widget {session.Key.ChatId}/{session.Key.MessageId} stopped: {view.Error}");

                // One that died before drawing anything, or was never built, has no frame to keep.
                if (view.ContentHeight > 0)
                {
                    Placeholder.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ShowStopped();
                }
            }
        }

        // The widget's host thread.
        private void OnActionRequested(WidgetView sender, WidgetAction args)
        {
            // Haptics: desktop ignores them. Ready: nothing to do.
            if (args.Kind != WidgetActionKind.OpenLink && args.Kind != WidgetActionKind.OpenTelegramLink)
            {
                return;
            }

            var url = args.Url;
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_session?.View == sender)
                {
                    OpenLink(url);
                }
            });
        }

        // The widget's host thread.
        private void OnCopyTextRequested(WidgetView sender, string args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_session?.View == sender && XamlRoot != null)
                {
                    MessageHelper.CopyText(XamlRoot, args);
                }
            });
        }

        // Only a shared widget that watches its state, on screen, is polled.
        private void UpdatePolling()
        {
            var widgetId = 0L;
            if (_session != null && _visible && _windowVisible && _session.View.WatchesPublicData && _message?.Content is MessageWidget widget)
            {
                widgetId = widget.WidgetId;
            }

            if (widgetId == _polledWidgetId)
            {
                return;
            }

            if (_poller != null)
            {
                _poller.Unwatch(_polledWidgetId, this);
                _poller = null;
            }

            _polledWidgetId = widgetId;

            if (widgetId != 0)
            {
                _poller = WidgetPublicDataPoller.For(_message.ClientService);
                _poller.Watch(widgetId, this, _message.ChatId, _message.Id);
            }
        }

        public void ApplyPublicData(long widgetId, string publicData)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Stored with the message too, so that the widget opens with it next time.
                if (_polledWidgetId == widgetId && _message?.Content is MessageWidget widget && widget.WidgetId == widgetId)
                {
                    widget.PublicData = publicData;
                    _session?.SetData(widget.ChatData, publicData);
                }
            });
        }

        // Both kinds ask first with the full URL (§5.2); OpenUrl then sends t.me links to the app's own
        // handling and the rest to the browser. The framework has already checked which host each takes.
        private async void OpenLink(string url)
        {
            var message = _message;
            if (_dialogOpen || message?.Delegate == null)
            {
                return;
            }

            _dialogOpen = true;
            try
            {
                var navigation = message.Delegate.NavigationService;
                var confirm = await navigation.ShowPopupAsync(string.Format(Strings.OpenUrlAlert, string.Format("[{0}]({0})", url)), Strings.OpenUrlTitle, Strings.Open, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    MessageHelper.OpenUrl(message.ClientService, navigation, url);
                }
            }
            finally
            {
                _dialogOpen = false;
            }
        }

        #endregion

        #region Input

        // Clicks, drags and hover reach the widget; keys, wheel and focus never do (profile 1). Double clicks are the
        // widget's: the renderer gives it dblclick, and the quick reaction only fires on the bubble's own surface
        // (MessageSelector.OnDoubleTapped). The renderer arbitrates the rest from the claim (docs/sdk-v1.md §5.5):
        // vertical drags are the chat's, horizontal ones the bubble's swipe unless claimed, and the right button and a
        // long press the message menu unless the widget claimed holds, when they are its contextmenu.

        // DOM modifier bits: alt, ctrl, meta, shift.
        private static int Modifiers(VirtualKeyModifiers keys)
        {
            return ((keys & VirtualKeyModifiers.Menu) != 0 ? 1 : 0)
                | ((keys & VirtualKeyModifiers.Control) != 0 ? 2 : 0)
                | ((keys & VirtualKeyModifiers.Windows) != 0 ? 4 : 0)
                | ((keys & VirtualKeyModifiers.Shift) != 0 ? 8 : 0);
        }

        private int Send(int action, PointerRoutedEventArgs e, int button = 0)
        {
            var point = e.GetCurrentPoint(Host);
            var touch = point.PointerDevice.PointerDeviceType == PointerDeviceType.Touch;
            var mouse = point.PointerDevice.PointerDeviceType == PointerDeviceType.Mouse;
            e.Handled = true;

            return _session.View.Pointer(action, point.Position.X, point.Position.Y, (int)point.PointerId, point.Properties.IsPrimary || !touch, mouse, Modifiers(e.KeyModifiers), button);
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_session == null || _pointer != null)
            {
                return;
            }

            var point = e.GetCurrentPoint(Host);
            var mouse = e.Pointer.PointerDeviceType == PointerDeviceType.Mouse;
            var secondary = mouse && point.Properties.IsRightButtonPressed;

            if (!point.Properties.IsLeftButtonPressed && !secondary)
            {
                return;
            }

            // A press in a widget drops the selection of any other; its own renderer drops or starts its own. The press
            // that opens the menu drops it too, so the menu copies what was selected before.
            if (_selected != this)
            {
                ClearSelection(null);
                _selected = this;
            }

            _menuSelection = secondary ? _session.View.SelectedText : null;

            if (!Host.CapturePointer(e.Pointer))
            {
                return;
            }

            _pointer = e.Pointer.PointerId;
            _pointerMouse = mouse;
            _claim = Send(0, e, secondary ? 2 : 0);
            _menuTaken = (_claim & 4) != 0 && (secondary || !mouse);

            if (secondary && !_menuTaken)
            {
                // The message menu opens on the release, which the widget does not see.
                CancelPointer();
                e.Handled = false;
            }
            else if (!mouse && (_claim & 1) == 0)
            {
                // A horizontal drag is the bubble's swipe. The widget keeps the press for a tap, and its touch ends with
                // pointercancel if the swipe takes it. A mouse never swipes.
                this.GetParent<MessageSelector>()?.RedirectForManipulation(e);
            }
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_session == null)
            {
                return;
            }

            if (_pointer == e.Pointer.PointerId)
            {
                Send(1, e);
            }
            else if (_pointer == null && e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                var position = e.GetCurrentPoint(Host).Position;
                _session.View.Hover(0, position.X, position.Y, Modifiers(e.KeyModifiers));
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_session == null || _pointer != e.Pointer.PointerId)
            {
                return;
            }

            Send(2, e);
            _pointer = null;
            _claim = -1;
            Host.ReleasePointerCapture(e.Pointer);
        }

        private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (_session == null || _pointer != e.Pointer.PointerId)
            {
                return;
            }

            Send(3, e);
            _pointer = null;
            _claim = -1;
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_session != null && _pointer == null && e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                var position = e.GetCurrentPoint(Host).Position;
                _session.View.Hover(1, position.X, position.Y, Modifiers(e.KeyModifiers));
            }
        }

        // A long press is the message menu unless the widget claimed holds, when the renderer has already given it the
        // contextmenu: otherwise the widget's touch ends with pointercancel and no click.
        private void OnHolding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started && !_menuTaken)
            {
                CancelPointer();
            }
        }

        private void CancelPointer()
        {
            if (_pointer is not uint pointer)
            {
                return;
            }

            _pointer = null;
            _claim = -1;
            _session?.View.Pointer(3, 0, 0, (int)pointer, true, _pointerMouse, 0, 0);
            Host.ReleasePointerCaptures();
        }

        #endregion

        #region Selection

        // A press outside `source`'s widget, or anywhere when null. UI thread.
        public static void ClearSelection(DependencyObject source)
        {
            var selected = _selected;
            if (selected == null)
            {
                return;
            }

            for (var element = source; element != null; element = VisualTreeHelper.GetParent(element))
            {
                if (element == selected)
                {
                    return;
                }
            }

            _selected = null;
            selected._session?.View.ClearSelection();
        }

        // The copy command: the selected text of the widget pressed last, if it has some and its message may be copied.
        public static bool TryCopySelection(XamlRoot xamlRoot)
        {
            var selected = _selected;
            if (selected?._session == null || selected._message?.CanBeSaved is false)
            {
                return false;
            }

            var text = selected._session.View.SelectedText;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            MessageHelper.CopyText(xamlRoot, text);
            return true;
        }

        #endregion
    }
}
