//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Views.Host;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core.Preview;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Telegram.Controls
{
    /// <summary>
    /// Raised by <see cref="WindowContent.OnWindowCloseRequested"/>. Wraps the UWP args rather
    /// than exposing them, so a root only ever sees Handled and a deferral - the two things
    /// every caller actually used, and the two an island host can supply by other means.
    /// </summary>
    public class WindowCloseRequestedEventArgs : EventArgs
    {
        private readonly SystemNavigationCloseRequestedPreviewEventArgs _args;
        private TaskCompletionSource<bool> _deferral;
        private bool _handled;

        internal WindowCloseRequestedEventArgs(SystemNavigationCloseRequestedPreviewEventArgs args)
        {
            _args = args;
        }

        /// <summary>
        /// Raised by the app rather than by the system - the window's own close button. Handled is
        /// then just a bool, and the deferral has to be honoured by whoever raised it.
        /// </summary>
        internal WindowCloseRequestedEventArgs()
        {
        }

        public bool Handled
        {
            get => _args?.Handled ?? _handled;
            set
            {
                if (_args != null)
                {
                    _args.Handled = value;
                }
                else
                {
                    _handled = value;
                }
            }
        }

        public Deferral GetDeferral()
        {
            if (_args != null)
            {
                return _args.GetDeferral();
            }

            // Allocated only when a handler actually asks to defer, which is the uncommon case.
            _deferral ??= new TaskCompletionSource<bool>();
            return new Deferral(CompleteDeferral);
        }

        private void CompleteDeferral()
        {
            _deferral.TrySetResult(true);
        }

        /// <summary>
        /// Completes once every handler that took a deferral has released it. Only meaningful for
        /// an app-raised request: the system waits on its own.
        /// </summary>
        internal Task WaitAsync()
        {
            return _deferral?.Task ?? Task.CompletedTask;
        }
    }

    /// <summary>
    /// Base for anything assigned to <see cref="WindowContext.Content"/> - the root of a window,
    /// though not a window itself. Was WindowEx, declared at the bottom of VoipWindow.xaml.cs.
    ///
    /// The point of collecting these here is that every window-level event a root cares about -
    /// activation, close requests, visibility - is wired once, in one place, behind overridable
    /// methods. What raises them is then an implementation detail of this class rather than of
    /// the twelve roots above it.
    /// </summary>
    public partial class WindowContent : UserControlEx, IPopupHost
    {
        /// <summary>
        /// The window this is the root of. Required at construction rather than resolved from
        /// the XamlRoot: every root is created with `new` and never reparented, and XamlRoot is
        /// null until the content is in a tree - which is later than some roots need it.
        /// </summary>
        public WindowContext Window { get; }

        protected WindowContent(WindowContext context)
        {
            Window = context;
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
        /// between a CoreWindow and an island host, and this is the only place that should know.
        ///
        /// OnLoaded rather than the constructor or the raw Loaded event: roots used to subscribe
        /// from their constructors and never unsubscribe, keeping themselves reachable from the
        /// view for as long as it lived. UserControlEx guarantees OnLoaded and OnUnloaded
        /// alternate, which Loaded and Unloaded do not - those fire again on every reparenting,
        /// and subscribing there would attach a second handler each time. CloseRequested and
        /// Consolidated both take deferrals, so a duplicate handler is a duplicate confirmation
        /// dialog rather than merely wasted work.
        /// </summary>
        protected override void OnLoaded()
        {
            if (Window is WindowContext window)
            {
                window.Activated += OnWindowActivatedCore;
                window.VisibilityChanged += OnWindowVisibilityChangedCore;
            }

            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += OnCloseRequestedCore;

            var view = ApplicationView.GetForCurrentView();
            view.Consolidated += OnConsolidatedCore;
            view.VisibleBoundsChanged += OnVisibleBoundsChangedCore;
        }

        protected override void OnUnloaded()
        {
            if (Window is WindowContext window)
            {
                window.Activated -= OnWindowActivatedCore;
                window.VisibilityChanged -= OnWindowVisibilityChangedCore;
            }

            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested -= OnCloseRequestedCore;

            var view = ApplicationView.GetForCurrentView();
            view.Consolidated -= OnConsolidatedCore;
            view.VisibleBoundsChanged -= OnVisibleBoundsChangedCore;
        }

        private void OnWindowActivatedCore(object sender, WindowActivatedEventArgs args)
        {
            OnWindowActivated(args.IsActive);
        }

        private void OnWindowVisibilityChangedCore(object sender, WindowVisibilityEventArgs args)
        {
            OnWindowVisibilityChanged(args.IsVisible);
        }

        private void OnCloseRequestedCore(object sender, SystemNavigationCloseRequestedPreviewEventArgs args)
        {
            OnWindowCloseRequested(new WindowCloseRequestedEventArgs(args));
        }

        // Neither carries a payload worth forwarding: the one caller of Consolidated ignored its
        // args, and the one caller of VisibleBoundsChanged wanted IsFullScreenMode, which the
        // WindowContext already exposes. So ApplicationViewConsolidatedEventArgs - another type
        // an island host could not construct - stops here.
        private void OnConsolidatedCore(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
        {
            OnWindowConsolidated();
        }

        private void OnVisibleBoundsChangedCore(ApplicationView sender, object args)
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

        /// <summary>
        /// The window's own close button, routed into the same override the system's close request
        /// uses: a root has one place to intercept a close, whatever asked for it. Returns false
        /// when the root refused.
        ///
        /// Awaited, because the answer can be a question: the web app window takes a deferral and
        /// asks the user whether to discard the bot's changes.
        /// </summary>
        internal async Task<bool> RequestCloseAsync()
        {
            var args = new WindowCloseRequestedEventArgs();
            OnWindowCloseRequested(args);

            await args.WaitAsync();

            return !args.Handled;
        }

        protected virtual void OnWindowConsolidated()
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
