//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Messages.Content;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public sealed partial class PlaybackOverlay : UserControlEx
    {
        private bool _presenterPressed;
        private Vector2 _presenterDelta;
        private Vector2 _presenterOffset = Vector2.Zero;
        private readonly Visual _presenter;

        public PlaybackOverlay()
        {
            InitializeComponent();

            _presenter = ElementComposition.GetElementVisual(Presenter);

            Presenter.PointerPressed += Presenter_PointerPressed;
            Presenter.PointerMoved += Presenter_PointerMoved;
            Presenter.PointerReleased += Presenter_PointerReleased;

            Connected += OnConnected;
            Disconnected += OnDisconnected;
        }

        private void OnConnected(object sender, RoutedEventArgs e)
        {
            VideoNoteContent.VisibleMessagesChanged += OnVisibleMessagesChanged;
            LifetimeService.Current.Playback.SourceChanged += OnSourceChanged;
        }

        private void OnDisconnected(object sender, RoutedEventArgs e)
        {
            VideoNoteContent.VisibleMessagesChanged -= OnVisibleMessagesChanged;
            LifetimeService.Current.Playback.SourceChanged -= OnSourceChanged;
        }

        private void OnVisibleMessagesChanged(object sender, EventArgs e)
        {
            this.BeginOnUIThread(OnSourceChanged);
        }

        private void OnSourceChanged(IPlaybackService sender, object args)
        {
            this.BeginOnUIThread(OnSourceChanged);
        }

        private void OnSourceChanged()
        {
            if (LifetimeService.Current.Playback.CurrentItem is PlaybackItemMessage message && message.Message.Content is MessageVideoNote)
            {
                if (message.XamlRoot != XamlRoot || VideoNoteContent.IsMessageVisible(XamlRoot, message.Message))
                {
                    ShowHide(false);
                }
                else
                {
                    ShowHide(true);
                }
            }
            else
            {
                ShowHide(false);
            }
        }

        private bool _collapsed = true;

        private void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            Grid.SetRow(this, 0);
            Grid.SetRowSpan(this, 3);

            _collapsed = !show;
            Visibility = Visibility.Visible;

            ElementCompositionPreview.SetIsTranslationEnabled(Presenter, true);

            var visual = ElementComposition.GetElementVisual(Presenter);
            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                Visibility = _collapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, show ? Vector3.Zero : Vector3.One);
            scale.InsertKeyFrame(1, show ? Vector3.One : Vector3.Zero);

            visual.CenterPoint = new Vector3(60);
            visual.Properties.InsertVector3("Translation", new Vector3());
            visual.StartAnimation("Opacity", opacity);
            visual.StartAnimation("Scale", scale);
            batch.End();

            if (show)
            {
                _panel = new SwapChainPanel();

                Presenter.Child = _panel;
                LifetimeService.Current.Playback.Attach(_panel);
            }
            else if (_panel != null)
            {
                Presenter.Child = null;
                LifetimeService.Current.Playback.Detach(_panel);

                _panel = null;
            }
        }

        private SwapChainPanel _panel;

        #region Interactions

        private void Presenter_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _presenterPressed = true;
            Presenter.CapturePointer(e.Pointer);

            var pointer = e.GetCurrentPoint(this);
            var point = pointer.Position.ToVector2();
            _presenterDelta = new Vector2(_presenter.Offset.X - point.X, _presenter.Offset.Y - point.Y);
        }

        private void Presenter_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_presenterPressed)
            {
                return;
            }

            var pointer = e.GetCurrentPoint(this);
            var delta = _presenterDelta + pointer.Position.ToVector2();

            _presenter.Offset = new Vector3(delta, 0);
        }

        private void Presenter_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _presenterPressed = false;
            Presenter.ReleasePointerCapture(e.Pointer);

            var pointer = e.GetCurrentPoint(this);
            var offset = _presenterDelta + pointer.Position.ToVector2();

            // Padding maybe
            var p = 8;

            var w = (float)ActualWidth - p * 2;
            var h = (float)ActualHeight - p * 2;

            _presenterOffset = new Vector2((offset.X - p) / w, (offset.Y - p) / h);

            CheckConstraints();
        }

        private void CheckConstraints()
        {
            if (Presenter.Visibility == Visibility.Collapsed)
            {
                return;
            }

            var w = (float)(RootGrid.ActualWidth - Presenter.ActualWidth);
            var h = (float)(RootGrid.ActualHeight - Presenter.ActualHeight);

            if (w == 0 || h == 0)
            {
                return;
            }

            var x = MathF.Round(_presenterOffset.X + (Presenter.ActualSize.X / 2 / w));
            var y = MathF.Round(_presenterOffset.Y + (Presenter.ActualSize.Y / 2 / h));

            var x1 = Math.Max(0, Math.Min(w, x * w));
            var y1 = Math.Max(0, Math.Min(h, y * h));

            if (x1 != _presenter.Offset.X || y1 != _presenter.Offset.Y)
            {
                var anim = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                anim.InsertKeyFrame(0, _presenter.Offset);
                anim.InsertKeyFrame(1, new Vector3(x1, y1, 0));

                var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    _presenter.Offset = new Vector3(x1, y1, 0);
                    _presenterOffset = new Vector2(x1 / w, y1 / h);
                };

                _presenter.StartAnimation("Offset", anim);
                batch.End();
            }
        }

        #endregion

    }
}
