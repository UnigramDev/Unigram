//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Telegram.Services
{
    public interface IViewService
    {
        ///<summary>
        /// Creates and opens new secondary view        
        /// </summary>
        /// <param name="page">Type of page to automatically navigate</param>
        /// <param name="parameter">Parameter that will be passed to NavigationService with the page</param>
        /// <param name="title">Title that will be displayed for new view. If <code>null</code> - current view's title will be used</param>
        /// <param name="size">Anchor size for newly created view</param>        
        /// <returns>The <see cref="WindowContext"/> of the newly created view, or <c>null</c> if it could not be created.</returns>
        Task<WindowContext> OpenAsync(ISession session, Type page, object parameter = null, string title = null, Size size = default, string id = "0");

        Task<WindowContext> OpenAsync(ViewServiceOptions options);
    }

    public enum ViewServiceMode
    {
        Default,
        CompactOverlay,
        FullScreen,
    }

    public partial class ViewServiceOptions
    {
        public ViewServiceMode ViewMode { get; set; } = ViewServiceMode.Default;

        public string Title { get; set; }

        public double Width { get; set; }
        public double Height { get; set; }

        public Func<WindowContext, UIElement> Content { get; set; }

        public string PersistedId { get; set; }
    }

    /// <summary>
    /// The host-agnostic half: what a view service is, and the one piece of state both hosts keep.
    /// Opening a window is entirely per host - UWP asks CoreApplication for a view, Win32 makes an
    /// HWND with an island in it - so those live in ViewService.Uwp.cs and ViewService.Win32.cs.
    /// </summary>
    public sealed partial class ViewService : IViewService
    {
        private static readonly TaskCompletionSource<bool> _mainWindowCreated = new();


        internal static void OnWindowLoaded()
        {
            _mainWindowCreated.TrySetResult(true);
        }

        public static Task WaitForMainWindowAsync()
        {
            return _mainWindowCreated.Task;
        }

    }
}
