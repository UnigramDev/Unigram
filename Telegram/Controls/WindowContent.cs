//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Views.Host;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

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
    public partial class WindowContent : UserControlEx, IPopupHost
    {
        private WindowContext _window;

        /// <summary>
        /// The window this is the root of. Resolved from the XamlRoot rather than read off a
        /// thread-static, so it stays correct once several windows share a thread. Null until
        /// the content is in a tree, hence the retry rather than a cached null.
        /// </summary>
        public WindowContext Window => _window ??= WindowContext.ForXamlRoot(this);

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

        public async void Close()
        {
            try
            {
                if (XamlRoot.Content is WindowControl { Content: RootWindow root })
                {
                    root.PresentContent(null);
                    return;
                }
            }
            catch
            {
                // XamlRoot.Content seems to throw a NullReferenceException
                // whenever corresponding window has been already closed.
            }

            await WindowContext.Current.ConsolidateAsync();
        }

        protected async void RestoreWindow()
        {
            var applicationView = ApplicationView.GetForCurrentView();
            if (applicationView.ViewMode != ApplicationViewMode.CompactOverlay)
            {
                return;
            }

            var restored = await applicationView.TryEnterViewModeAsync(ApplicationViewMode.Default);
            if (restored)
            {
                applicationView.TryResizeView(new Size(720, 540));
            }
        }
    }
}
