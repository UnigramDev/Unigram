//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Authorization;
using Telegram.Views.Calls;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Navigation
{
    public class PopupActivatedEventArgs : EventArgs
    {
        public bool IsActive { get; }

        public PopupActivatedEventArgs(bool isActive)
        {
            IsActive = isActive;
        }
    }

    /// <summary>
    /// Raised by <see cref="WindowContext.Activated"/>. Deliberately not the UWP
    /// <c>Windows.UI.Core.WindowActivatedEventArgs</c>: that one carries a
    /// <c>CoreWindowActivationState</c>, and an island host has no CoreWindow. Every consumer
    /// only ever asked whether the state was Deactivated, so the args carry the answer.
    /// </summary>
    public class WindowActivatedEventArgs : EventArgs
    {
        public bool IsActive { get; }

        public WindowActivatedEventArgs(bool isActive)
        {
            IsActive = isActive;
        }
    }

    public class WindowSizeChangedEventArgs : EventArgs
    {
        public Size Size { get; }

        public WindowSizeChangedEventArgs(Size size)
        {
            Size = size;
        }
    }

    public class WindowVisibilityEventArgs : EventArgs
    {
        public bool IsVisible { get; }

        public WindowVisibilityEventArgs(bool isVisible)
        {
            IsVisible = isVisible;
        }
    }

    /// <summary>
    /// The cursors the app actually uses, so call sites do not name <c>CoreCursorType</c>.
    /// <see cref="Hidden"/> is the gallery's chrome-less mode.
    /// </summary>
    public enum PointerCursorType
    {
        Arrow,
        Hand,
        IBeam,
        SizeWestEast,
        SizeNorthSouth,
        SizeNorthwestSoutheast,
        SizeNortheastSouthwest,
        Hidden
    }

    /// <summary>
    /// Which caption buttons the window draws for itself. Only None, Close and All mean anything
    /// on both hosts: UWP can close a view but has no way to minimize or maximize one, so a
    /// combination naming either of those is Win32 only.
    ///
    /// All is the default and means "whatever this host does normally" - on UWP that is the
    /// shell's own caption, which is better than anything the app could draw: it minimizes, it
    /// maximizes, it has the system menu. Anything less than All is the app taking it over.
    /// </summary>
    [Flags]
    public enum CaptionButtons
    {
        None = 0,
        Minimize = 1,
        Maximize = 2,
        Close = 4,

        All = Minimize | Maximize | Close
    }

    public partial class WindowPresenter : ContentControl, IPopupHost, IToastHost
    {
        private readonly WindowContext _context;

        private StackPanel CaptionPanel;
        private Button MinimizeButton;
        private Button MaximizeButton;
        private Button CloseButton;

        public WindowPresenter(WindowContext window)
        {
            _context = window;
            DefaultStyleKey = typeof(WindowPresenter);
        }

        private CaptionButtons _buttons = CaptionButtons.All;
        public CaptionButtons Buttons
        {
            get => _buttons;
            set
            {
                if (_buttons != value)
                {
                    _buttons = value;
                    ApplyCaptionButtons();
                }
            }
        }

        private bool _isMaximized;
        /// <summary>
        /// Drives the maximize button's glyph, which is a restore glyph while the window is
        /// zoomed. Set by the host, because only the host knows.
        /// </summary>
        public bool IsMaximized
        {
            get => _isMaximized;
            set
            {
                if (_isMaximized != value)
                {
                    _isMaximized = value;

                    ApplyMaximizeGlyph();
                }
            }
        }

        private ElementTheme _captionTheme = ElementTheme.Default;
        /// <summary>
        /// The caption buttons follow the window's theme, except where something paints its own
        /// colour behind them - a web app whose bot picked the header colour. Only the buttons,
        /// not the content, which is why it is not just RequestedTheme.
        /// </summary>
        public ElementTheme CaptionTheme
        {
            get => _captionTheme;
            set
            {
                _captionTheme = value;

                if (CaptionPanel != null)
                {
                    CaptionPanel.RequestedTheme = value;
                }
            }
        }

        private CaptionButtons _hotButton;
        private bool _hotPressed;

        /// <summary>
        /// Which caption button is lit, and whether it is held. Pushed in by the host rather than
        /// read from the pointer: on Win32 the drag bar of gate 1.7 is the window under the
        /// pointer, so the buttons themselves never see one. On UWP nothing calls this - the
        /// buttons are ordinary buttons there.
        /// </summary>
        public void SetCaptionButtonState(CaptionButtons button, bool pressed)
        {
            if (_hotButton == button && _hotPressed == pressed)
            {
                return;
            }

            _hotButton = button;
            _hotPressed = pressed;

            GoToCaptionButtonState(MinimizeButton, CaptionButtons.Minimize);
            GoToCaptionButtonState(MaximizeButton, CaptionButtons.Maximize);
            GoToCaptionButtonState(CloseButton, CaptionButtons.Close);
        }

        /// <summary>
        /// The window's own caption buttons dim while it is not the active one, the way the shell's
        /// do. Pushed in by the host: neither the buttons nor the presenter can see activation, and
        /// on Win32 the pointer is over the drag bar rather than over them.
        /// </summary>
        private void GoToCaptionButtonState(Button button, CaptionButtons which)
        {
            if (button == null)
            {
                return;
            }

            var state = _hotButton != which
                ? _isActive ? "Normal" : "Unfocused"
                : _hotPressed ? "Pressed" : "PointerOver";

            // Transitions, so leaving a button fades the way the system's do rather than snapping.
            VisualStateManager.GoToState(button, state, true);
        }

        private bool _isActive = true;
        /// <summary>
        /// Whether the window is the active one. The system dims its caption glyphs when it is not,
        /// and nothing about a button's own pointer state says so - the host has to tell us.
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;

                    GoToCaptionButtonState(MinimizeButton, CaptionButtons.Minimize);
                    GoToCaptionButtonState(MaximizeButton, CaptionButtons.Maximize);
                    GoToCaptionButtonState(CloseButton, CaptionButtons.Close);
                }
            }
        }

        private void ApplyMaximizeGlyph()
        {
            if (MaximizeButton != null)
            {
                VisualStateManager.GoToState(MaximizeButton,
                    _isMaximized ? "WindowStateMaximized" : "WindowStateNormal", false);
            }
        }

        protected override void OnApplyTemplate()
        {
            if (CloseButton != null)
            {
                CloseButton.Click -= OnCloseButtonClick;
            }

            CaptionPanel = GetTemplateChild(nameof(CaptionPanel)) as StackPanel;
            MinimizeButton = GetTemplateChild(nameof(MinimizeButton)) as Button;
            MaximizeButton = GetTemplateChild(nameof(MaximizeButton)) as Button;
            CloseButton = GetTemplateChild(nameof(CloseButton)) as Button;

            // Only Close is wired: on UWP it is the only one that can be drawn at all, and on
            // Win32 none of the three ever sees a click - the drag bar of gate 1.7 covers the
            // strip and turns the hit test into a WM_SYSCOMMAND, which is what earns the snap
            // layouts flyout. The buttons are what the user sees; the drag bar is what answers.
            if (CloseButton != null)
            {
                CloseButton.Click += OnCloseButtonClick;
            }

            if (CaptionPanel != null)
            {
                CaptionPanel.RequestedTheme = _captionTheme;
            }

            // The window can already be maximized by the time the template arrives.
            ApplyMaximizeGlyph();

            ApplyCaptionButtons();

            base.OnApplyTemplate();
        }

        private void ApplyCaptionButtons()
        {
            if (CaptionPanel == null)
            {
                return;
            }

            // The rule that lets one setting serve both hosts: asking for every button is asking
            // for an ordinary window, and an ordinary UWP window already has all three, drawn by
            // the shell. Any subset is the app taking over, and then it draws them itself.
            var mine = !WindowContext.HasSystemCaptionButtons || _buttons != CaptionButtons.All;

            CaptionPanel.Visibility = mine && _buttons != CaptionButtons.None
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (MinimizeButton != null)
            {
                MinimizeButton.Visibility = mine && _buttons.HasFlag(CaptionButtons.Minimize)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (MaximizeButton != null)
            {
                MaximizeButton.Visibility = mine && _buttons.HasFlag(CaptionButtons.Maximize)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (CloseButton != null)
            {
                CloseButton.Visibility = mine && _buttons.HasFlag(CaptionButtons.Close)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Through the root's close request rather than straight to WindowContext.Close: a root
        /// can have something to ask first - the web app window does - and that is the same
        /// question the system asks it when the window is closed any other way.
        /// </summary>
        private async void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            if (Content is WindowContent root && !await root.RequestCloseAsync())
            {
                return;
            }

            _context.Close();
        }

        public void PopupOpened()
        {
            _context.RaisePopupActivated(true);

            if (OverlayWindow.PopupOpened(XamlRoot))
            {
                return;
            }

            if (Content is IPopupHost content)
            {
                content.PopupOpened();
            }
        }

        public void PopupClosed()
        {
            _context.RaisePopupActivated(false);

            if (OverlayWindow.PopupClosed(XamlRoot))
            {
                return;
            }

            if (Content is IPopupHost content)
            {
                content.PopupClosed();
            }
        }

        private ConditionalWeakTable<TeachingTip, string> _toasts = new();

        public void ToastOpened(TeachingTip toast)
        {
            var key = "Toast" + Guid.NewGuid().ToString("N");

            _toasts.Add(toast, key);
            Resources.Add(key, toast);
            toast.Closed += Toast_Closed;
        }

        public void ToastClosed(TeachingTip toast)
        {
            if (_toasts.Remove(toast, out string key))
            {
                Resources.Remove(key);
            }

            toast.Closed -= Toast_Closed;
        }

        private void Toast_Closed(TeachingTip sender, TeachingTipClosedEventArgs args)
        {
            if (_toasts.Remove(sender, out string key))
            {
                Resources.Remove(key);
            }

            sender.Closed -= Toast_Closed;
        }

        public void HideAllToasts()
        {
            foreach (var toast in _toasts)
            {
                toast.Key.IsOpen = false;
            }
        }
    }

    /// <summary>
    /// A control that re-renders when the window's rasterization scale changes. Raised by
    /// <see cref="WindowContext"/> rather than subscribed per control - see the registration there.
    /// </summary>
    public interface IRasterizationScaleAware
    {
        void RasterizationScaleChanged(double rasterizationScale);
    }

    public partial class WindowContext
    {
        private readonly InputListener _inputListener;
        public InputListener InputListener => _inputListener;

        #region Pointer cursor

        // One CoreCursor per type, shared. Every call site used to allocate a fresh one, and
        // FormattedTextBlock does this at pointer sample rate; CoreCursor is immutable and its
        // id argument only means anything for CoreCursorType.Custom, so sharing is safe.
        // Indexed by PointerCursorType, which is why Hidden - the null cursor - is last.
        private static readonly CoreCursor[] _cursors = new CoreCursor[(int)PointerCursorType.Hidden];

        /// <summary>
        /// Shared, and it took a fork and back to establish that it could be. A Win32 host cannot
        /// do this with SetCursor: the island is the window under the pointer and answers
        /// WM_SETCURSOR itself, so a cursor set on the top-level window never survives a mouse
        /// move. What it can do is exactly what UWP does - a thread hosting islands still gets a
        /// CoreWindow, and that stub is what XAML's input site reads.
        ///
        /// GetForCurrentThread rather than Window.Current.CoreWindow, which is the same object on
        /// UWP and one fewer Window.Current.
        /// </summary>
        public static void SetPointerCursor(PointerCursorType cursor)
        {
            CoreWindow.GetForCurrentThread().PointerCursor = GetPointerCursor(cursor);
        }

        private static CoreCursor GetPointerCursor(PointerCursorType cursor)
        {
            if (cursor == PointerCursorType.Hidden)
            {
                return null;
            }

            // UI thread only, so a lost race would just allocate one extra cursor.
            return _cursors[(int)cursor] ??= new CoreCursor(cursor switch
            {
                PointerCursorType.Hand => CoreCursorType.Hand,
                PointerCursorType.IBeam => CoreCursorType.IBeam,
                PointerCursorType.SizeWestEast => CoreCursorType.SizeWestEast,
                PointerCursorType.SizeNorthSouth => CoreCursorType.SizeNorthSouth,
                PointerCursorType.SizeNorthwestSoutheast => CoreCursorType.SizeNorthwestSoutheast,
                PointerCursorType.SizeNortheastSouthwest => CoreCursorType.SizeNortheastSouthwest,
                _ => CoreCursorType.Arrow
            }, 0);
        }

        #endregion

        public int Id { get; }

        public Theme Theme => Theme.Current;

        public ThemeParameters ThemeParameters => Theme.GetParameters(ActualTheme);

        #region Chat theme

        // The app theme is global and lives on Theme, merged app-wide so that popups resolve it.
        // A chat override is not: two windows can show two chats, so each window recolours its
        // own brushes, and Theme's pair stays as the default nothing has overridden.
        public MessageBrushes Outgoing { get; } = new("Outgoing", ThemeOutgoing.DefaultLight, ThemeOutgoing.DefaultDark);

        public MessageBrushes Incoming { get; } = new("Incoming", ThemeIncoming.DefaultLight, ThemeIncoming.DefaultDark);

        private Background _lastBackground;

        private ThemeSettings _lastLightSettings;
        private ThemeSettings _lastDarkSettings;

        private ChatBackground _lastChatBackground;
        private ChatTheme _lastChatTheme;

        public ThemeSettings LightSettings => _lastLightSettings;
        public ThemeSettings DarkSettings => _lastDarkSettings;

        public ChatBackground ChatBackground => _lastChatBackground;

        public ChatTheme ChatTheme => _lastChatTheme;

        /// <summary>
        /// Re-applies this window's override after the app theme has been recomputed underneath
        /// it, falling back to the app-wide chat theme when the window has none of its own.
        /// </summary>
        public void ReapplyChatTheme()
        {
            UpdateMessages(_lastLightSettings ?? Theme.GetAppChatSettings(TelegramTheme.Light),
                _lastDarkSettings ?? Theme.GetAppChatSettings(TelegramTheme.Dark));
        }

        public bool UpdateChatTheme(ElementTheme elementTheme, ChatTheme theme, ThemeSettings lightSettings, ThemeSettings darkSettings, ChatBackground background)
        {
            var requested = elementTheme == ElementTheme.Dark ? TelegramTheme.Dark : TelegramTheme.Light;
            var settings = requested == TelegramTheme.Light ? lightSettings : darkSettings;

            // Both sides, because both have to be right before the base flips - the switch itself
            // recolours nothing. Comparing both accents rather than the current one: two themes
            // can share a light accent and differ in dark.
            var changed = _lastLightSettings?.AccentColor != lightSettings?.AccentColor
                || _lastDarkSettings?.AccentColor != darkSettings?.AccentColor;

            _lastLightSettings = lightSettings;
            _lastDarkSettings = darkSettings;
            _lastChatTheme = settings != null ? theme : null;

            if (changed)
            {
                UpdateMessages(lightSettings, darkSettings);
            }

            var nextBackground = background?.Background;
            if (settings != null)
            {
                nextBackground ??= settings.Background;
            }

            var updated = !_lastBackground.AreTheSame(nextBackground);

            _lastBackground = nextBackground;
            _lastChatBackground = background;

            return updated;
        }

        private void UpdateMessages(ThemeSettings lightSettings, ThemeSettings darkSettings)
        {
            Apply(TelegramTheme.Light, lightSettings);
            Apply(TelegramTheme.Dark, darkSettings);

            void Apply(TelegramTheme requested, ThemeSettings settings)
            {
                // Parent, not requested: a chat theme is tinted by the appearance, and that can
                // resolve a light request to the dark family.
                var info = Theme.Resolve(requested, settings);

                Outgoing.Update(info?.Parent ?? requested, info?.Values);
                Incoming.Update(info?.Parent ?? requested, info?.Values);
            }
        }

        #endregion

#if NET9_0_OR_GREATER
        // A stuck finalizer must not keep a closed window alive, so the drain is bounded.
        private static readonly TimeSpan ShutdownDrainTimeout = TimeSpan.FromSeconds(2);

        private Task _drain;
        private Deferral _deferral;

        private void OnShutdownStarting(DispatcherQueue sender, Windows.System.DispatcherQueueShutdownStartingEventArgs args)
        {
            sender.ShutdownStarting -= OnShutdownStarting;

            // This view's RCWs are context bound. Released from the finalizer thread they marshal
            // back here through IContextCallback, and once the XAML core is gone unparenting one
            // faults on a null CCoreServices - the access violation seen a moment after closing a
            // window that held a FormattedTextBlock, whose runs are XamlDirect objects.
            //
            // So collect while this thread still pumps and XAML is still up. The deferral is what
            // keeps it pumping; the wait has to run off-thread, because blocking here would block
            // the very apartment the finalizer needs to call into.
            _deferral = args.GetDeferral();

            // ShutdownStarting is raised before the Unloaded cascade that OnConsolidated queued
            // when it dropped the content, so releasing here would hand disposed handles to
            // handlers that have not run yet. Queue behind them instead - the deferral is what
            // keeps the thread pumping long enough for that to be dispatched. If the queue will
            // not take the work, release now anyway: a live handle past teardown is the crash
            // this exists to prevent, and _released covers the handlers that follow.
            if (!sender.TryEnqueue(Windows.System.DispatcherQueuePriority.Low, OnShutdownDrain))
            {
                Logger.Info("queue refused, releasing inline");
                OnShutdownDrain();
            }
        }

        private void OnShutdownDrain()
        {
            FormattedTextBlock.ReleaseNative(_xamlRoot);

            _drain = Task.Run(Drain);

            Task.WhenAny(_drain, Task.Delay(ShutdownDrainTimeout))
                .ContinueWith(OnDrained, _deferral, TaskScheduler.Default);
        }

        // DIAGNOSTIC: the releases still fault after this drain, and the two explanations want
        // opposite fixes - either the drain lost its race against a few hundred blocking
        // cross-apartment releases, or the peers were still reference-tracked by the native tree
        // and there was nothing here to collect. A pass that finishes in milliseconds means the
        // second; one that hits the timeout means the first. Logger's tail ships with the crash.
        private static void Drain()
        {
            var started = Logger.TickCount;
            var before = GC.GetTotalMemory(false);

            GC.Collect();
            GC.WaitForPendingFinalizers();

            var first = Logger.TickCount - started;

            // A release freed by the first pass can unroot the next wrapper along.
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Logger.Info($"first pass {first}ms, second {Logger.TickCount - started - first}ms, heap {before >> 10}K -> {GC.GetTotalMemory(false) >> 10}K");
        }

        private void OnDrained(Task task, object state)
        {
            Logger.Info(_drain.IsCompleted ? "drained" : $"timed out after {ShutdownDrainTimeout.TotalMilliseconds}ms");

            (state as Deferral)?.Complete();
        }
#endif

        public bool IsInMainView { get; }

        public bool IsCallInProgress { get; private set; }

        public XamlRoot XamlRoot => _content?.XamlRoot;

        private bool _contentMaterial;

        private WindowPresenter _content;
        /// <summary>
        /// Characters typed into this window that nothing in the tree consumed. Raised from the
        /// window's root element, so it replaces <c>CoreWindow.CharacterReceived</c> without
        /// needing a CoreWindow - which a XAML island does not have.
        /// </summary>
        public event TypedEventHandler<WindowContext, CharacterReceivedRoutedEventArgs> CharacterReceived;

        private void OnContentCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
        {
            if (args.OriginalSource is TextBox or RichEditBox)
            {
                return;
            }

            CharacterReceived?.Invoke(this, args);
        }

        private CaptionButtons _captionButtons = CaptionButtons.All;

        /// <summary>
        /// Which caption buttons this window draws for itself. Kept here rather than on the
        /// presenter because a root sets it from its constructor, which is before there is a
        /// presenter to set it on.
        /// </summary>
        public CaptionButtons CaptionButtons
        {
            get => _captionButtons;
            set
            {
                _captionButtons = value;

                if (_content != null)
                {
                    _content.Buttons = value;
                }

                SetHostCaptionButtons(value);
            }
        }

        /// <summary>
        /// What the host has to do about it: take the shell's own caption away on UWP, drop the
        /// resize border on Win32. Only called when a window asks for something other than the
        /// default, so an ordinary window touches neither.
        /// </summary>
        partial void SetHostCaptionButtons(CaptionButtons buttons);

        private ElementTheme _captionTheme = ElementTheme.Default;

        /// <summary>
        /// The theme the caption buttons are drawn against, for a window that paints its own
        /// colour behind them.
        /// </summary>
        public ElementTheme CaptionTheme
        {
            get => _captionTheme;
            set
            {
                _captionTheme = value;

                if (_content != null)
                {
                    _content.CaptionTheme = value;
                }
            }
        }

        public UIElement Content
        {
            get => _locked != null ? _lockedContent : _content?.Content as UIElement;
            set
            {
                if (_locked != null)
                {
                    _lockedContent = value;
                }
                else
                {
                    SetContent(value);
                }

                IsCallInProgress = value is VoipWindow or GroupCallWindow or LiveStreamWindow;
            }
        }

        /// <summary>
        /// Hands the root element to whatever this host shows it in - a <c>Window</c> on UWP, a
        /// <c>DesktopWindowXamlSource</c> on Win32.
        /// </summary>
        partial void SetHostContent(UIElement content);

        partial void SetScreenCaptureEnabled(bool enabled);

        /// <summary>
        /// The window's own backdrop. WinUI 2's BackdropMaterial on UWP; the Win32 host asks DWM
        /// for it against the HWND instead - see gate 1.10 - so the two are alternatives rather
        /// than layers.
        /// </summary>
        partial void SetBackdropMaterial(WindowPresenter content);

        private void SetContent(UIElement content)
        {
            if (_content != null)
            {
                _content.Content = content;
            }
            else
            {
                // Before the content exists, so ActualTheme comes from GetCalculatedElementTheme
                // rather than from an element that is not in a tree yet and has nothing to
                // inherit from.
                ReapplyChatTheme();

                _content = new WindowPresenter(this)
                {
                    RequestedTheme = NightModeService.Current.GetCalculatedElementTheme(),
                    Buttons = _captionButtons,
                    CaptionTheme = _captionTheme,
                    Content = content,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch
                };

                _content.Loaded += OnLoaded;
                _content.Unloaded += OnUnloaded;

                // Deliberately +=, not AddHandler(handledEventsToo): a focused TextBox or
                // RichEditBox marks the character handled, so it never reaches here - which is
                // what the type-to-search sites used to approximate with a FocusManager check.
                _content.CharacterReceived += OnContentCharacterReceived;

                _content.Resources.MergedDictionaries.Add(Incoming.CreateDictionary());

                SetHostContent(_content);

                // XamlRoot becomes available instantly as the content is set to the Window
                lock (_allLock)
                {
                    _xamlRoot = _content.XamlRoot;
                    _mapping.AddOrUpdate(_content.XamlRoot, this);
                }

                _rasterizationScale = _xamlRoot.RasterizationScale;
                _xamlRoot.Changed += OnXamlRootChanged;
            }

            if (!_contentMaterial && content is RootWindow or StandaloneWindow or TabbedWindow or WebAppWindow)
            {
                _contentMaterial = true;
                SetBackdropMaterial(_content);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                control.Loaded -= OnLoaded;
            }

            ViewService.OnWindowLoaded();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                control.Unloaded -= OnUnloaded;
            }

#if NET9_0_OR_GREATER
            FormattedTextBlock.ReleaseNative(_xamlRoot);
#endif
        }

        /// <summary>
        /// Drops everything the window owns that outlives its host: the XamlRoot mapping, the
        /// navigation services and the content. Each host calls this from its own close path.
        /// </summary>
        private void Detach()
        {
            lock (_allLock)
            {
                if (_xamlRoot != null)
                {
                    _xamlRoot.Changed -= OnXamlRootChanged;
                    _mapping.Remove(_xamlRoot);
                }

                All.Remove(this);
            }

            NavigationServices.ForEach(x => x.Suspend());
            NavigationServices.Clear();

            _content = null;
        }

        public ElementTheme ActualTheme => _content?.ActualTheme ?? NightModeService.Current.GetCalculatedElementTheme();

        public ElementTheme RequestedTheme
        {
            get => _content?.RequestedTheme ?? ElementTheme.Default;
            set => _content?.RequestedTheme = value;
        }

        public double RasterizationScale => _content?.XamlRoot?.RasterizationScale ?? 1;

        #region Rasterization scale

        // One subscription per window rather than one per control, and the controls are held
        // weakly, so nothing that outlives a control ever holds a handle to it.
        //
        // A control over a Telegram.Native base must NOT subscribe XamlRoot.Changed itself. The
        // XamlRoot outlives it, and a handle to a composed control handed to a source that outlives
        // it either dangles - a raw `this` is revoked when the CCW is finalized, which is off the
        // UI thread - or, with get_weak(), pins the managed object for the life of the window under
        // NativeAOT, where revoking does not release it either.
        //
        // A ConditionalWeakTable rather than a list of WeakReference: registering twice is a no-op,
        // dead entries need no pruning, and unload has nothing to undo.
        private readonly ConditionalWeakTable<IRasterizationScaleAware, object> _rasterizationScaleAware = new();

        private double _rasterizationScale;

        public static void RegisterRasterizationScale(XamlRoot xamlRoot, IRasterizationScaleAware element)
        {
            var context = ForXamlRoot(xamlRoot);
            context?._rasterizationScaleAware.AddOrUpdate(element, null);
        }

        private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            if (_rasterizationScale == sender.RasterizationScale)
            {
                return;
            }

            _rasterizationScale = sender.RasterizationScale;

            foreach (var pair in _rasterizationScaleAware)
            {
                pair.Key.RasterizationScaleChanged(_rasterizationScale);
            }
        }

        #endregion

        public bool IsPopupOpened { get; private set; }

        public event EventHandler<PopupActivatedEventArgs> PopupActivated;

        public void RaisePopupActivated(bool opened)
        {
            IsPopupOpened = opened;
            PopupActivated?.Invoke(this, new PopupActivatedEventArgs(opened));
        }

        public event EventHandler<WindowActivatedEventArgs> Activated;

        public event EventHandler<WindowVisibilityEventArgs> VisibilityChanged;

        public event EventHandler<WindowSizeChangedEventArgs> SizeChanged;

        public event EventHandler<object> VisibleBoundsChanged;

        public IDispatcherContext Dispatcher { get; }
        public NavigationServiceList NavigationServices { get; } = new NavigationServiceList();

        public static INavigationService GetNavigationService(UIElement element)
        {
            var context = ForXamlRoot(element);
            if (context != null)
            {
                return context.GetNavigationService();
            }

            return null;
        }

        public static INavigationService GetNavigationService(XamlRoot xamlRoot)
        {
            var context = ForXamlRoot(xamlRoot);
            if (context != null)
            {
                return context.GetNavigationService();
            }

            return null;
        }

        #region Screen capture

        private readonly HashSet<int> _screenCaptureDisabled = new();
        private bool _screenCaptureEnabled = true;

        public void DisableScreenCapture(int hash)
        {
            if (Constants.DEBUG)
            {
                return;
            }

            _screenCaptureDisabled.Add(hash);

            if (_screenCaptureDisabled.Count == 1 && _screenCaptureEnabled)
            {
                _screenCaptureEnabled = false;
                SetScreenCaptureEnabled(false);
            }
        }

        public void EnableScreenCapture(int hash)
        {
            if (Constants.DEBUG)
            {
                return;
            }

            _screenCaptureDisabled.Remove(hash);

            if (_screenCaptureDisabled.Count == 0 && !_screenCaptureEnabled)
            {
                _screenCaptureEnabled = true;
                SetScreenCaptureEnabled(true);
            }
        }

        #endregion

        #region Lock

        private UIElement _lockedContent;
        private PasscodeWindow _locked;

        public void Lock(bool biometrics)
        {
            if (_locked != null)
            {
                return;
            }

            if (_content?.Content is IPopupHost popupHost)
            {
                popupHost.PopupOpened();
            }

            Logger.Info("Showing passcode lock");

            // TODO: Transition from splash screen to passcode
            _locked = new PasscodeWindow(this, biometrics && IsInMainView);
            _lockedContent = _content?.Content as UIElement;

            SetContent(_locked);
        }

        public void Unlock()
        {
            if (_locked == null)
            {
                return;
            }

            Logger.Info("Hiding passcode lock");

            SetContent(_lockedContent);

            _locked = null;
            _lockedContent = null;

            if (_content.Content is IPopupHost popupHost)
            {
                popupHost.PopupClosed();
            }

            if (_content.Content is Control control)
            {
                control.Focus(FocusState.Programmatic);
            }
        }

        #endregion

        // Activation, and it is app logic rather than host plumbing: it switches on the
        // authorization state and navigates. IActivatedEventArgs is a projection both hosts
        // have - a packaged Win32 app is activated with the same args - so this is shared,
        // and only the title bar it used to sit beside is per host.
        #region Legacy code

        public async void Activate(IActivatedEventArgs args, INavigationService service, AuthorizationState state)
        {
            try
            {
                switch (state)
                {
                    case AuthorizationStateReady:
                        //App.Current.NavigationService.Navigate(typeof(Views.MainPage));
                        Activate(args, service);
                        break;
                    case AuthorizationStateWaitPhoneNumber:
                    case AuthorizationStateWaitOtherDeviceConfirmation:
                        service.Navigate(typeof(AuthorizationPage));
                        break;
                    case AuthorizationStateWaitCode:
                        service.Navigate(typeof(AuthorizationCodePage), navigationStackEnabled: false);
                        break;
                    case AuthorizationStateWaitEmailAddress:
                        service.Navigate(typeof(AuthorizationEmailAddressPage), navigationStackEnabled: false);
                        break;
                    case AuthorizationStateWaitEmailCode:
                        service.Navigate(typeof(AuthorizationEmailCodePage), navigationStackEnabled: false);
                        break;
                    case AuthorizationStateWaitRegistration:
                        service.Navigate(typeof(AuthorizationRegistrationPage), navigationStackEnabled: false);
                        break;
                    case AuthorizationStateWaitPassword waitPassword:
                        if (!string.IsNullOrEmpty(waitPassword.RecoveryEmailAddressPattern))
                        {
                            await service.ShowPopupAsync(string.Format(Strings.RestoreEmailSent, waitPassword.RecoveryEmailAddressPattern), Strings.AppName, Strings.OK);
                        }

                        service.Navigate(typeof(AuthorizationPasswordPage), navigationStackEnabled: false);
                        break;
                }
            }
            catch { }
        }

        private void Activate(IActivatedEventArgs args, INavigationService service)
        {
            service ??= Current.NavigationServices.FirstOrDefault();

            if (service == null || args == null)
            {
                return;
            }

            if (args is ShareTargetActivatedEventArgs share)
            {
            }
            else if (args is ProtocolActivatedEventArgs protocol)
            {
                if (service?.Frame?.Content is MainPage page)
                {
                    page.Activate(protocol.Uri.ToString());
                }
                else
                {
                    service.NavigateToMain(protocol.Uri.ToString());
                }
            }
            else if (args is FileActivatedEventArgs file)
            {
                if (service?.Frame?.Content is MainPage page)
                {
                    //page.Activate(launch);
                }
                else
                {
                    service.NavigateToMain(string.Empty);
                }

                if (file.Files[0] is StorageFile item)
                {
                    // TODO: WinUI - most likely XamlRoot is going to be null at this stage.
                    // As well, Content may be null too.

                    _ = new ThemePreviewPopup(item).ShowQueuedAsync(XamlRoot);
                }
            }
            else if (args is CommandLineActivatedEventArgs commandLine)
            {
                Activate(commandLine.Operation.Arguments, service);
            }
            else if (args is ToastNotificationActivatedEventArgs toastNotificationActivated)
            {
                Activate(toastNotificationActivated.Argument, service);
            }
            else
            {
                var launch = args as LaunchActivatedEventArgs;
                Activate(launch?.Arguments, service);
            }
        }

        private void Activate(string arguments, INavigationService service)
        {
            if (service?.Frame?.Content is MainPage page)
            {
                page.Activate(arguments);
            }
            else
            {
                service.NavigateToMain(arguments);
            }
        }

        #endregion
        #region Static code

        public static void ForEach(Action<WindowContext> action)
        {
            lock (_allLock)
            {
                foreach (var window in All)
                {
                    window.Dispatcher.Dispatch(() => action(window));
                }
            }
        }

        public static Task ForEachAsync(Func<WindowContext, Task> action)
        {
            var tasks = new List<Task>();

            lock (_allLock)
            {
                foreach (var window in All)
                {
                    tasks.Add(window.Dispatcher.DispatchAsync(() => action(window)));
                }
            }

            return Task.WhenAll(tasks);
        }

        public static Task ForEachAsync(Action<WindowContext> action)
        {
            var tasks = new List<Task>();

            lock (_allLock)
            {
                foreach (var window in All)
                {
                    tasks.Add(window.Dispatcher.DispatchAsync(() => action(window)));
                }
            }

            return Task.WhenAll(tasks);
        }

        // Weak on the key, so a window that skips OnClosed cannot pin its own tree, and the
        // entry a context holds back to its XamlRoot is what dependent handles exist for. Reads
        // need no lock, which matters because every MessageBubble resolves its window through
        // here as it loads.
        private static readonly ConditionalWeakTable<XamlRoot, WindowContext> _mapping = new();
        private XamlRoot _xamlRoot;

        public static bool TryGetForXamlRoot(XamlRoot xamlRoot, out WindowContext context)
        {
            context = ForXamlRoot(xamlRoot);
            return context != null;
        }

        public static WindowContext ForXamlRoot(XamlRoot xamlRoot)
        {
            if (xamlRoot != null && _mapping.TryGetValue(xamlRoot, out WindowContext context))
            {
                return context;
            }

            return null;
        }

        /// <summary>
        /// The window handle behind a XamlRoot - a CoreWindow's on UWP, the island's HWND on Win32.
        /// <c>IntPtr.Zero</c> if the root belongs to no window this app knows about.
        /// </summary>
        public static IntPtr GetWindowHandle(XamlRoot xamlRoot)
        {
            var context = ForXamlRoot(xamlRoot);
            return context != null ? (IntPtr)context.Handle : IntPtr.Zero;
        }

        public static WindowContext ForXamlRoot(UIElement element)
        {
            return ForXamlRoot(element?.XamlRoot);
        }

        private static readonly object _allLock = new();
        public static readonly List<WindowContext> All = new();

        private static readonly object _activeLock = new();
        public static WindowContext Active;

        public static WindowContext Main;

        #endregion

        #region Navigation

        public bool RaiseShortcutInvoked(InvokedShortcut shortcut, VirtualKeyModifiers modifiers)
        {
            var args = new ShortcutInvokedEventArgs(shortcut, modifiers);

            foreach (var frame in NavigationServices.Select(x => x.FrameFacade).Reverse())
            {
                frame.RaiseShortcutInvoked(args);

                if (args.Handled)
                {
                    return true;
                }
            }

            return false;
        }

        public bool RaiseBackRequested(VirtualKey key = VirtualKey.GoBack)
        {
            var handled = false;
            RaiseBackRequested(key, ref handled);
            return handled;
        }

        /// <summary>
        /// Default Hardware/Shell Back handler overrides standard Back behavior 
        /// that navigates to previous app in the app stack to instead cause a backward page navigation.
        /// Views or Viewodels can override this behavior by handling the BackRequested 
        /// event and setting the Handled property of the BackRequestedEventArgs to true.
        /// </summary>
        private void RaiseBackRequested(VirtualKey key, ref bool handled)
        {
            Logger.Info();

            var args = new BackRequestedRoutedEventArgs(key);
            var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);

            foreach (var popup in popups)
            {
                if (popup.Child is INavigablePage page)
                {
                    page.OnBackRequested(args);

                    if (handled = args.Handled)
                    {
                        return;
                    }
                }
                else if (popup.Child is ContentDialog dialog)
                {
                    dialog.Hide();
                    return;
                }
                else if (popup.Child is ToolTip toolTip)
                {
                    toolTip.IsOpen = false;
                }
                else if (popup.Child is TeachingTip teachingTip)
                {
                    if (teachingTip.IsLightDismissEnabled)
                    {
                        teachingTip.IsOpen = false;
                    }
                }
                else if (key == VirtualKey.Escape)
                {
                    // TODO: what is this for? I have no clue anymore
                    if (popup.Child is not Grid)
                    {
                        //handled = args.Handled = true;
                        return;
                    }
                }
            }

            foreach (var frame in NavigationServices.Select(x => x.FrameFacade).Reverse())
            {
                frame.RaiseBackRequested(args);

                if (handled = args.Handled)
                {
                    return;
                }
            }

            var navigationService = NavigationServices.FirstOrDefault();
            if (navigationService?.CanGoBack ?? false)
            {
                navigationService?.GoBack();
                handled = true;
            }
        }

        public bool RaiseForwardRequested()
        {
            Logger.Info();

            var args = new HandledEventArgs();

            foreach (var frame in NavigationServices.Select(x => x.FrameFacade))
            {
                frame.RaiseForwardRequested(args);
                if (args.Handled)
                {
                    return true;
                }
            }

            var navigationService = NavigationServices.FirstOrDefault();
            if (navigationService?.CanGoForward ?? false)
            {
                navigationService?.GoForward();
                return true;
            }

            return false;
        }

        #endregion
    }
}
