//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LibVLCSharp.Platforms.Windows;
using System;
using Telegram.Common;
using Telegram.Services.ViewService;
using Telegram.Streams;
using Telegram.ViewModels.Gallery;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Gallery
{
    public sealed partial class GalleryCompactWindow : UserControlEx
    {
        private GalleryViewModelBase _viewModel;
        private readonly RemoteFileStream _fileStream;
        private readonly long _initialTime;

        private AsyncMediaPlayer _player;

        public static ViewLifetimeControl Current { get; private set; }

        public GalleryCompactWindow(ViewLifetimeControl control, GalleryViewModelBase viewModel, RemoteFileStream stream, long time)
        {
            InitializeComponent();

            Current = control;

            _viewModel = viewModel;
            _fileStream = stream;
            _initialTime = time;

            Controls.IsCompact = true;

            var view = ApplicationView.GetForCurrentView();
            view.TitleBar.ButtonForegroundColor = Colors.White;

            var visual = ElementComposition.GetElementVisual(ControlsRoot);
            visual.Opacity = 0;
        }

        private void OnInitialized(object sender, InitializedEventArgs e)
        {
            _player = new AsyncMediaPlayer(e.SwapChainOptions);

            Controls.Attach(_player);

            _player.Play(_fileStream);
            _player.Time = _initialTime;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Current = null;
            Controls.Unload();

            _player?.Close();
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

            _transportCollapsed = !show;

            var visual = ElementComposition.GetElementVisual(ControlsRoot);
            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            visual.StartAnimation("Opacity", opacity);
        }

        private async void Controls_CompactClick(object sender, RoutedEventArgs e)
        {
            var mainDispatcher = CoreApplication.MainView.Dispatcher;
            if (mainDispatcher == null || _player == null)
            {
                return;
            }

            var position = _player.Time;
            var prevId = ApplicationView.GetForCurrentView().Id;

            _player.Stop();

            await mainDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var nextId = ApplicationView.GetForCurrentView().Id;
                _ = ApplicationViewSwitcher.SwitchAsync(nextId, prevId, ApplicationViewSwitchingOptions.ConsolidateViews);
                _ = GalleryWindow.ShowAsync(_viewModel, null, position);
            });
        }

        public void Play(GalleryViewModelBase viewModel, RemoteFileStream stream, long time)
        {
            _viewModel = viewModel;

            if (_player != null)
            {
                _player.Play(stream);
                _player.Time = time;
            }
        }

        public void Pause()
        {
            _player?.Pause(true);
        }
    }
}
