//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.ViewModels.Gallery;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Core.Preview;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Gallery
{
    public sealed partial class GalleryCompactOverlay : UserControlEx
    {
        private AppWindow _window;

        private GalleryViewModelBase _viewModel;
        private VideoPlayerBase _player;

        private static GalleryCompactOverlay _current;

        public GalleryCompactOverlay(AppWindow window, GalleryViewModelBase viewModel, GalleryMedia media, VideoPlayerBase player)
        {
            InitializeComponent();

            _window = window;

            _viewModel = viewModel;
            _player = player;
            _player.SizeChanged += OnVideoSizeChanged;

            Presenter.Constraint = media.Constraint;
            Presenter.Children.Insert(0, player);

            Controls.Attach(player);
            Controls.IsCompact = true;

            _current = this;

            //var view = ApplicationView.GetForCurrentView();
            //view.TitleBar.ButtonForegroundColor = Colors.White;

            var visual = ElementComposition.GetElementVisual(ControlsRoot);
            visual.Opacity = 0;
        }

        private void OnVideoSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is VideoPlayerBase player && e.NewSize.Width != 0 && e.NewSize.Height != 0)
            {
                player.IsUnloadedExpected = false;
                player.SizeChanged -= OnVideoSizeChanged;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Controls.Unload();

            _player = null;
            _window = null;
            _current = null;
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            ShowHideTransport(true);
            base.OnPointerEntered(e);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            ShowHideTransport(false);
            base.OnPointerExited(e);
        }

        private bool _transportCollapsed = true;

        private void ShowHideTransport(bool show)
        {
            if (show != _transportCollapsed)
            {
                return;
            }

            if (show is false && XamlRoot != null)
            {
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
                {
                    return;
                }
            }

            _transportCollapsed = !show;

            var visual = ElementComposition.GetElementVisual(ControlsRoot);
            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            visual.StartAnimation("Opacity", opacity);
        }

        private async void Controls_CompactClick(object sender, RoutedEventArgs e)
        {
            // TODO: WinUI - Rewrite
            if (_current == null)
            {
                // Button was already pressed
                return;
            }

            _current = null;

            _player.IsUnloadedExpected = true;
            Presenter.Children.RemoveAt(0);

            if (FeatureTokenGenerator.TryUnlockFeature("com.microsoft.windows.applicationwindow"))
            {
                var prevId = CoreAppWindowPreview.GetIdFromWindow(_window);
                var nextId = ApplicationView.GetForCurrentView().Id;
                await ApplicationViewSwitcher.SwitchAsync(nextId, prevId);
            }

            _ = GalleryWindow.ShowAsync(WindowContext.Current.Content.XamlRoot, _viewModel, null, 0, _player);
            _ = _window.CloseAsync();
        }

        private void Play(GalleryViewModelBase viewModel, VideoPlayerBase player)
        {
            _viewModel = viewModel;
            _player = player;
            _player.SizeChanged += OnVideoSizeChanged;

            Presenter.Children.RemoveAt(0);
            Presenter.Children.Insert(0, player);

            Controls.Attach(player);
        }

        private void PauseImpl()
        {
            _player?.Pause();
        }

        public static void Pause()
        {
            if (_current != null && _current.Dispatcher.HasThreadAccess)
            {
                _current.PauseImpl();
            }
            else if (_current != null)
            {
                try
                {
                    _ = _current.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, Pause);
                }
                catch
                {
                    // May throw
                }
            }
        }

        public static async void CreateOrUpdate(GalleryViewModelBase viewModel, GalleryMedia item, VideoPlayerBase player)
        {
            // TODO: there is a problem when creating the overlay from a secondary window
            // as the AppWindow will be destroyed as soon as the secondary window gets closed.
            if (_current is GalleryCompactOverlay window)
            {
                window.Play(viewModel, player);
            }
            else
            {
                var appWindow = await AppWindow.TryCreateAsync();
                appWindow.PersistedStateId = "Gallery";
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                appWindow.TitleBar.ButtonForegroundColor = Colors.White;

                // Is CompactOverlay supported for this AppWindow? If not, then stop.
                if (appWindow.Presenter.IsPresentationSupported(AppWindowPresentationKind.CompactOverlay))
                {
                    var appWindowContent = new GalleryCompactOverlay(appWindow, viewModel, item, player);

                    // Create a new frame for the window
                    // Navigate the frame to the CompactOverlay page inside it.
                    //appWindowFrame.Navigate(typeof(SecondaryAppWindowPage));
                    // Attach the frame to the window
                    ElementCompositionPreview.SetAppWindowContent(appWindow, appWindowContent);
                    // Let's set the title so that we can tell the windows apart

                    var switched = appWindow.Presenter.RequestPresentation(AppWindowPresentationKind.CompactOverlay);
                    if (switched)
                    {
                        // Double call because at times it fails
                        appWindow.Presenter.RequestPresentation(AppWindowPresentationKind.CompactOverlay);

                        await appWindow.TryShowAsync();
                    }
                }
            }
        }
    }
}
