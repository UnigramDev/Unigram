//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Services.ViewService;
using Telegram.ViewModels.Gallery;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Gallery
{
    public sealed partial class GalleryCompactWindow : UserControlEx
    {
        private GalleryViewModelBase _viewModel;

        public static ViewLifetimeControl Current { get; private set; }

        public GalleryCompactWindow(ViewLifetimeControl control, GalleryViewModelBase viewModel, GalleryMedia video, double position)
        {
            InitializeComponent();

            Current = control;

            _viewModel = viewModel;

            Controls.IsCompact = true;
            Controls.Attach(Video);

            Video.Play(video, position);

            var view = ApplicationView.GetForCurrentView();
            view.TitleBar.ButtonForegroundColor = Colors.White;

            var visual = ElementComposition.GetElementVisual(ControlsRoot);
            visual.Opacity = 0;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Current = null;
            Controls.Unload();
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
            var mainDispatcher = CoreApplication.MainView.Dispatcher;
            if (mainDispatcher == null)
            {
                return;
            }

            var position = (long)Video.Position;
            var prevId = ApplicationView.GetForCurrentView().Id;

            Video.Stop();

            await mainDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var nextId = ApplicationView.GetForCurrentView().Id;
                _ = ApplicationViewSwitcher.SwitchAsync(nextId, prevId, ApplicationViewSwitchingOptions.ConsolidateViews);
                _ = GalleryWindow.ShowAsync(null, _viewModel, null, position);
            });
        }

        public void Play(GalleryViewModelBase viewModel, GalleryMedia video, double time)
        {
            _viewModel = viewModel;
            Video.Play(video, time);
        }

        public void Pause()
        {
            Video.Pause();
        }
    }
}
