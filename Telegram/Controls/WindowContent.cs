//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Views.Host;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    /// <summary>
    /// Base for anything assigned to <see cref="WindowContext.Content"/> - the root of a window,
    /// though not a window itself. Was WindowEx, declared at the bottom of VoipWindow.xaml.cs.
    ///
    /// The point of collecting these here is that every window-level event a root cares about -
    /// activation, close requests, visibility - is wired once, in one place, behind overridable
    /// methods. What raises them is then an implementation detail of this class rather than of
    /// the twelve roots above it.
    /// </summary>
    public partial class WindowContent : UserControl, IPopupHost
    {
        /// <summary>
        /// The window this is the root of. Required at construction rather than resolved from
        /// the XamlRoot: every root is created with `new` and never reparented, and XamlRoot is
        /// null until the content is in a tree - which is later than some roots need it.
        /// </summary>
        public WindowContext Window { get; private set; }

        protected WindowContent(WindowContext context)
        {
            _counter = s_counters.GetOrAdd(GetType().Name, static name => new LifetimeCounter(name));
            Logger.Info(_counter.TypeName + ": " + Interlocked.Increment(ref _counter.Constructed));

            Window = context;
            Loaded += OnChanged;
            Unloaded += OnChanged;
        }

        #region Lifetime tracking

        /// <summary>
        /// A window root is the object most likely to keep an entire view alive, and whether one
        /// ever dies is a question only a finalizer can answer - reachability tells you nothing
        /// while the view is still up. There are at most a dozen of these alive at once, so the
        /// cost of putting every one on the finalization queue is not worth measuring.
        /// </summary>
        public sealed class LifetimeCounter
        {
            public LifetimeCounter(string typeName)
            {
                TypeName = typeName;
            }

            public string TypeName { get; }

            public int Constructed;
            public int Consolidated;
            public int Finalized;

            public int Pending => Consolidated - Finalized;
        }

        private static readonly ConcurrentDictionary<string, LifetimeCounter> s_counters = new();

        // Held per instance so the finalizer touches nothing but this one object, which is rooted
        // for the process: no dictionary lookup, no lock, no allocation, and no GetType() call on
        // the finalizer thread. Null only if a base constructor threw, and an exception escaping a
        // finalizer takes the process with it.
        private readonly LifetimeCounter _counter;

        ~WindowContent()
        {
            if (_counter != null)
            {
                Logger.Info(_counter.TypeName + ": " + Interlocked.Increment(ref _counter.Finalized));
            }
        }

        /// <summary>
        /// One line per root type. Finalization is lazy, so read it after a forced collection -
        /// see MainPage.CollectAndAnalyze.
        /// </summary>
        public static string DebugCounters()
        {
            var builder = new StringBuilder();

            foreach (var counter in s_counters.Values)
            {
                builder.AppendLine($"{counter.TypeName}: {counter.Constructed} constructed, {counter.Finalized} finalized, {counter.Pending} pending");
            }

            return builder.ToString();
        }

        public static string DebugInline()
        {
            var count = 0;

            foreach (var counter in s_counters.Values)
            {
                count += counter.Pending;
            }

            if (count > 0)
            {
                return $", {count} pending";
            }

            return string.Empty;
        }

        #endregion

        private bool _loaded;
        private bool _unloaded;
        private bool _closed;

        public bool IsConnected => _loaded;
        public bool IsDisconnected => _unloaded;

        private void OnChanged(object sender, RoutedEventArgs e)
        {
            // TODO: unfortunately FrameworkElement.Parent returns null
            // whenever the control is a DataTemplate root or similar,
            // hence we're forced to use VisualTreeHelper here, but I'm quite sure it's slower.

            var parent = this.GetParent();
            if (parent != null && !_loaded)
            {
                _loaded = true;
                _unloaded = false;
                OnLoadedCore();
            }
            else if (parent == null && _loaded)
            {
                _loaded = false;
                _unloaded = true;
                OnUnloadedCore();
            }
        }

        /// <summary>
        /// The element acting as the draggable caption, if this root has one. Declared here so
        /// the base can hide it while a popup is open - which six of the seven roots were each
        /// doing by hand, differing only in which element they named.
        /// </summary>
        protected virtual UIElement TitleBarElement => null;

        void IPopupHost.PopupOpened() => OnPopupOpened();

        void IPopupHost.PopupClosed() => OnPopupClosed();

        protected virtual void OnPopupOpened()
        {
            if (TitleBarElement != null)
            {
                Window?.SetTitleBar(null);
            }
        }

        protected virtual void OnPopupClosed()
        {
            if (TitleBarElement != null)
            {
                Window?.SetTitleBar(TitleBarElement);
            }
        }

        /// <summary>
        /// Wired once, here, rather than in each root: what raises these is the part that differs
        /// between a CoreWindow and an island host, and no root should have to know.
        ///
        /// OnLoaded rather than the constructor or the raw Loaded event: roots used to subscribe
        /// from their constructors and never unsubscribe, keeping themselves reachable from the
        /// view for as long as it lived. UserControlEx guarantees OnLoaded and OnUnloaded
        /// alternate, which Loaded and Unloaded do not - those fire again on every reparenting,
        /// and subscribing there would attach a second handler each time. CloseRequested and
        /// Consolidated both take deferrals, so a duplicate handler is a duplicate confirmation
        /// dialog rather than merely wasted work.
        /// </summary>
        private void OnLoadedCore()
        {
            if (Window is WindowContext window)
            {
                window.Activated += OnWindowActivatedCore;
                window.VisibilityChanged += OnWindowVisibilityChangedCore;
                window.VisibleBoundsChanged += OnVisibleBoundsChangedCore;
                window.CloseRequested += OnCloseRequestedCore;
                window.Closed += OnClosedCore;
            }

            OnLoaded();
        }

        protected virtual void OnLoaded()
        {

        }

        private void OnUnloadedCore()
        {
            if (Window is WindowContext window)
            {
                window.Activated -= OnWindowActivatedCore;
                window.VisibilityChanged -= OnWindowVisibilityChangedCore;
                window.VisibleBoundsChanged -= OnVisibleBoundsChangedCore;
                window.CloseRequested -= OnCloseRequestedCore;
                window.Closed -= OnClosedCore;
                window.SetTitleBar(null);
            }

            OnUnloaded();

            if (_closed)
            {
                Window = null;
            }
        }

        protected virtual void OnUnloaded()
        {

        }

        private void OnWindowActivatedCore(object sender, WindowActivatedEventArgs args)
        {
            OnWindowActivated(args.IsActive);
        }

        private void OnWindowVisibilityChangedCore(object sender, WindowVisibilityEventArgs args)
        {
            OnWindowVisibilityChanged(args.IsVisible);
        }

        private void OnCloseRequestedCore(object sender, WindowCloseRequestedEventArgs args)
        {
            OnWindowCloseRequested(args);
        }

        // Neither carries a payload worth forwarding: the one caller of Consolidated ignored its
        // args, and the one caller of VisibleBoundsChanged wanted IsFullScreenMode, which the
        // WindowContext already exposes. So ApplicationViewConsolidatedEventArgs - another type
        // an island host could not construct - stops here.
        private void OnClosedCore(object sender, EventArgs args)
        {
            _closed = true;
            OnWindowClosed();

            if (_unloaded)
            {
                Window = null;
            }

            if (_counter != null)
            {
                Logger.Info(_counter.TypeName + ": " + Interlocked.Increment(ref _counter.Consolidated));
            }
        }

        private void OnVisibleBoundsChangedCore(object sender, object args)
        {
            OnWindowVisibleBoundsChanged();
        }

        protected virtual void OnWindowActivated(bool active)
        {
        }

        protected virtual void OnWindowVisibilityChanged(bool visible)
        {
        }

        protected virtual void OnWindowCloseRequested(WindowCloseRequestedEventArgs args)
        {
        }

        protected virtual void OnWindowClosed()
        {
        }

        protected virtual void OnWindowVisibleBoundsChanged()
        {
        }

        /// <summary>
        /// Caption buttons drawn white on transparent, for a root that paints its own dark
        /// chrome behind them. Opt-in: it is wrong for anything following the app theme.
        /// </summary>
        protected static void UseDarkCaptionButtons()
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.White;
            titleBar.ButtonHoverBackgroundColor = ColorEx.FromHex(0x19FFFFFF);
            titleBar.ButtonHoverForegroundColor = ColorEx.FromHex(0xCCFFFFFF);
            titleBar.ButtonPressedBackgroundColor = ColorEx.FromHex(0x33FFFFFF);
            titleBar.ButtonPressedForegroundColor = ColorEx.FromHex(0x99FFFFFF);
        }
    }
}
