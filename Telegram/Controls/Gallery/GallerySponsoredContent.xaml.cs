//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Gallery
{
    public sealed partial class GallerySponsoredContent : HyperlinkButtonEx
    {
        public GalleryViewModelBase ViewModel => DataContext as GalleryViewModelBase;

        private VideoMessageAdvertisement _advertisement;

        private DispatcherTimer _maxDisplayTimer;
        private DispatcherTimer _minDisplayTimer;
        private int _minDisplayDuration;

        private CompositionSpriteShape _removeShape;
        private Visual _removeVisual;
        private Visual _countVisual;

        private long _thumbnailToken;

        public GallerySponsoredContent()
        {
            InitializeComponent();
            InitializeRemoveIcon();
        }

        protected override void OnLoaded()
        {
            _strokeBrush?.Register();
        }

        protected override void OnUnloaded()
        {
            _strokeBrush?.Unregister();

            _maxDisplayTimer?.Stop();
            _minDisplayTimer?.Stop();
        }

        private void InitializeRemoveIcon()
        {
            static CompositionPath GetIcon()
            {
                var stroke = 1.33f;
                var center = stroke / 2;

                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    builder.BeginFigure(new Vector2(0.5f, 0.5f));
                    builder.AddLine(new Vector2(11.5f, 11.5f));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    builder.BeginFigure(new Vector2(11.5f, 0.5f));
                    builder.AddLine(new Vector2(0.5f, 11.5f));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return new CompositionPath(result);
            }

            var path = BootStrapper.Current.Compositor.CreatePathGeometry(GetIcon());

            var shape = BootStrapper.Current.Compositor.CreateSpriteShape(path);
            shape.StrokeThickness = 1;
            shape.StrokeBrush = _strokeBrush ??= new CompositionColorSource(Stroke, IsConnected);
            shape.IsStrokeNonScaling = true;
            shape.StrokeStartCap = CompositionStrokeCap.Round;
            shape.StrokeEndCap = CompositionStrokeCap.Round;
            shape.Offset = new Vector2(10, 10);
            shape.CenterPoint = new Vector2(24, -12);
            shape.Scale = new Vector2(0.6f);

            var visual = BootStrapper.Current.Compositor.CreateShapeVisual();
            visual.Shapes.Add(shape);
            visual.Size = new Vector2(32, 32);
            visual.Offset = new Vector3(8, 8, 0);
            visual.Opacity = 0;

            _removeVisual = visual;
            _removeShape = shape;
            _countVisual = ElementComposition.GetElementVisual(MinDisplayText);
            ElementCompositionPreview.SetElementChildVisual(RemoveRoot, visual);
        }

        public void UpdateAdvertisement(VideoMessageAdvertisement advertisement)
        {
            _advertisement = advertisement;

            _maxDisplayTimer?.Stop();
            _minDisplayTimer?.Stop();

            if (advertisement == null)
            {
                ShowHide(false);
                return;
            }

            ShowHide(true);

            TitleText.Text = advertisement.Title;
            MessageText.Text = advertisement.Text;

            var small = advertisement.Sponsor.Photo?.GetSmall();
            if (small != null)
            {
                UpdateManager.Subscribe(this, ViewModel.ClientService, small.Photo, ref _thumbnailToken, UpdateFile, true);
                UpdateThumbnail(small.Photo);

                ThumbRoot.Visibility = Visibility.Visible;
            }
            else
            {
                ThumbRoot.Visibility = Visibility.Collapsed;
            }

            if (_maxDisplayTimer == null)
            {
                _maxDisplayTimer = new DispatcherTimer();
                _maxDisplayTimer.Tick += MaxDisplayTimer_Tick;
            }

            if (_minDisplayTimer == null)
            {
                _minDisplayTimer = new DispatcherTimer();
                _minDisplayTimer.Tick += MinDisplayTimer_Tick;
            }

            _minDisplayDuration = advertisement.MinDisplayDuration * 1;

            _maxDisplayTimer.Interval = TimeSpan.FromSeconds(_minDisplayDuration);
            _minDisplayTimer.Interval = TimeSpan.FromSeconds(1);

            //_displayTimer.Start();
            _minDisplayTimer.Start();

            _removeShape.Scale = new Vector2(0.6f);
            _removeVisual.Opacity = 0;
            _countVisual.Opacity = 1;

            _countCollapsed = true;
            _removeCollapsed = true;

            MinDisplayText.Text = _minDisplayDuration.ToString();

            MinDisplaySlice.Maximum = _minDisplayDuration;
            MinDisplaySlice.Value = DateTime.Now.Add(TimeSpan.FromSeconds(_minDisplayDuration));

            ShowHideCount(true, _minDisplayDuration);
            ShowHideRemove(true, _minDisplayDuration / 12d * 2.2);

            ViewModel.ClientService.Send(new ViewVideoMessageAdvertisement(advertisement.UniqueId));
        }

        private void MinDisplayTimer_Tick(object sender, object e)
        {
            _minDisplayDuration--;

            if (_minDisplayDuration == 0)
            {
                _minDisplayTimer.Stop();
            }
            else
            {
                MinDisplayText.Text = _minDisplayDuration.ToString();
            }
        }

        private void MaxDisplayTimer_Tick(object sender, object e)
        {
            _maxDisplayTimer.Stop();

            ShowHide(false);
            ViewModel.AdvertisementDisplayed();
        }

        private void UpdateFile(object target, File file)
        {
            UpdateThumbnail(file);
        }

        private void UpdateThumbnail(File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                ThumbImage.ImageSource = UriEx.ToBitmap(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                ViewModel.ClientService.DownloadFile(file.Id, 1);
            }
        }

        private void SponsoredMessage_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClientService.Send(new ClickVideoMessageAdvertisement(_advertisement.UniqueId));
            MessageHelper.OpenUrl(ViewModel.ClientService, ViewModel.NavigationService, _advertisement.Sponsor.Url);
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            //ViewModel.ShowPopup(new AboutAdsPopup(ViewModel, ViewModel.SponsoredMessage));
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_minDisplayDuration == 0)
            {
                _maxDisplayTimer.Stop();

                ShowHide(false);
                ViewModel.AdvertisementDisplayed();
            }
            else
            {
                ViewModel.HideAdvertisement();
            }
        }

        private bool _countCollapsed = true;

        private void ShowHideCount(bool show, double delay)
        {
            if (_countCollapsed != show)
            {
                return;
            }

            _countCollapsed = !show;

            var remove = _removeVisual;
            var count = ElementComposition.GetElementVisual(MinDisplayText);

            var scale = remove.Compositor.CreateVector2KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector2(show ? 0.6f : 1));
            scale.InsertKeyFrame(1, new Vector2(show ? 1 : 0.6f));
            scale.Duration = Constants.FastAnimation;
            scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scale.DelayTime = TimeSpan.FromSeconds(delay);

            var opacity = remove.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 1 : 0);
            opacity.InsertKeyFrame(1, show ? 0 : 1);
            opacity.Duration = Constants.FastAnimation;
            opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            opacity.DelayTime = TimeSpan.FromSeconds(delay);

            _removeShape.StartAnimation("Scale", scale);
            count.StartAnimation("Opacity", opacity);
        }

        private bool _removeCollapsed = true;

        private void ShowHideRemove(bool show, double delay)
        {
            if (_removeCollapsed != show)
            {
                return;
            }

            _removeCollapsed = !show;

            var scale = _removeVisual.Compositor.CreateVector2KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector2(show ? 0 : 0.6f));
            scale.InsertKeyFrame(1, new Vector2(show ? 0.6f : 0));
            scale.Duration = Constants.FastAnimation;
            scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scale.DelayTime = TimeSpan.FromSeconds(delay);

            var opacity = _removeVisual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);
            opacity.Duration = Constants.FastAnimation;
            opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            opacity.DelayTime = TimeSpan.FromSeconds(delay);

            //_removeShape.StartAnimation("Scale", scale);
            _removeVisual.StartAnimation("Opacity", opacity);
        }

        private bool _collapsed = true;

        private void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed = !show;
            Visibility = Visibility.Visible;

            var visual = ElementComposition.GetElementVisual(this);

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (_collapsed)
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(show ? 0 : 1, 0);
            opacity.InsertKeyFrame(show ? 1 : 0, 1);
            opacity.Duration = Constants.FastAnimation;

            visual.StartAnimation("Opacity", opacity);

            batch.End();
        }

        #region Stroke

        private CompositionColorSource _strokeBrush;

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(GallerySponsoredContent), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GallerySponsoredContent)d).OnStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            _strokeBrush?.PropertyChanged(newValue, IsConnected);
        }

        #endregion

    }
}
