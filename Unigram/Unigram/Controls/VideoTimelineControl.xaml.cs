using System;
using System.IO;
using System.Threading;
using System.Windows;
using Telegram.Api.Helpers;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls
{
    public partial class VideoTimelineControl
    {
        public static readonly DependencyProperty TrimRightProperty = DependencyProperty.Register(
            "TrimRight", typeof(TimeSpan?), typeof(VideoTimelineControl), new PropertyMetadata(default(TimeSpan?)));

        public TimeSpan? TrimRight
        {
            get { return (TimeSpan?)GetValue(TrimRightProperty); }
            set { SetValue(TrimRightProperty, value); }
        }

        public static readonly DependencyProperty TrimLeftProperty = DependencyProperty.Register(
            "TrimLeft", typeof(TimeSpan?), typeof(VideoTimelineControl), new PropertyMetadata(default(TimeSpan?)));

        public TimeSpan? TrimLeft
        {
            get { return (TimeSpan?)GetValue(TrimLeftProperty); }
            set { SetValue(TrimLeftProperty, value); }
        }

        public event EventHandler<ThumbnailChangedEventArgs> ThumbnailChanged;
        protected virtual void RaiseThumbnailChanged(ThumbnailChangedEventArgs e)
        {
            ThumbnailChanged?.Invoke(this, e);
        }

        public static readonly DependencyProperty FileProperty = DependencyProperty.Register(
            "File", typeof(StorageFile), typeof(VideoTimelineControl), new PropertyMetadata(default(StorageFile), OnFileChanged));

        private static void OnFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as VideoTimelineControl;
            if (control != null)
            {
                control.OnFileChangedInternal(e.NewValue as StorageFile);
            }
        }

        private VideoProperties _videoProperties;

        private void OnFileChangedInternal(StorageFile storageFile)
        {
            Photos.Children.Clear();
            LeftTransform.X = -MaxTranslateX;
            LeftOpacityBorderTransform.X = -468;
            Left.IsHitTestVisible = false;
            RightTransform.X = MaxTranslateX;
            RightOpacityBorderTransform.X = 468;
            Right.IsHitTestVisible = false;
            _videoProperties = null;
            _composition = null;
            TrimRight = null;
            TrimLeft = null;
            _lastPosition = null;
            _isManipulating = false;

            if (storageFile != null)
            {
                Execute.BeginOnThreadPool(TimeSpan.FromSeconds(1.0), async () =>
                {
                    _videoProperties = storageFile.Properties.GetVideoPropertiesAsync().AsTask().Result;
                    if (_videoProperties == null) return;

                    Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Left.IsHitTestVisible = true;
                        Right.IsHitTestVisible = true;
                    });

                    _composition = new MediaComposition();
                    var clip = await MediaClip.CreateFromFileAsync(storageFile);
                    _composition.Clips.Add(clip);

                    var scaleFactor = 100.0 / Math.Min(_videoProperties.Width, _videoProperties.Height);
                    var thumbnailWidth = _videoProperties.Orientation == VideoOrientation.Normal || _videoProperties.Orientation == VideoOrientation.Rotate180 ? (int)(_videoProperties.Width * scaleFactor) : (int)(_videoProperties.Height * scaleFactor);
                    var thumbnailHeight = _videoProperties.Orientation == VideoOrientation.Normal || _videoProperties.Orientation == VideoOrientation.Rotate180 ? (int)(_videoProperties.Height * scaleFactor) : (int)(_videoProperties.Width * scaleFactor);
                    for (var i = 0; i < 9; i++)
                    {
                        var timeStamp = new TimeSpan(_videoProperties.Duration.Ticks / 9 * i);

                        var photo = await _composition.GetThumbnailAsync(timeStamp, thumbnailWidth, thumbnailHeight, VideoFramePrecision.NearestKeyFrame);
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            var bitmapImage = new BitmapImage();
                            bitmapImage.SetSource(photo);

                            var image = new Image
                            {
                                Source = bitmapImage,
                                Stretch = Stretch.UniformToFill,
                                Width = 50,
                                Height = 48
                            };

                            Photos.Children.Add(image);
                        });
                    }
                });
            }
        }

        public StorageFile File
        {
            get { return (StorageFile)GetValue(FileProperty); }
            set { SetValue(FileProperty, value); }
        }

        public VideoTimelineControl()
        {
            InitializeComponent();

            SizeChanged += OnSizeChanged;

            Unloaded += (o, e) =>
            {
                _isManipulating = false;
            };
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            foreach (Image element in Photos.Children)
            {
                element.Width = LayoutRoot.ActualWidth / 9;
            }
        }

        private MediaComposition _composition;

        private const double MaxTranslateX = 234;

        private void Left_OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            LeftTransform.X += e.Delta.Translation.X;
            if (LeftTransform.X <= -MaxTranslateX) LeftTransform.X = -MaxTranslateX;
            if (LeftTransform.X >= RightTransform.X - (Left.ActualWidth + Right.ActualWidth) / 2) LeftTransform.X = RightTransform.X - (Left.ActualWidth + Right.ActualWidth) / 2;

            LeftOpacityBorderTransform.X = LeftTransform.X - (468 - MaxTranslateX);

            var trimLeft = new TimeSpan((long)((LeftTransform.X + MaxTranslateX) / (2 * MaxTranslateX) * _videoProperties.Duration.Ticks));
            if (trimLeft.Ticks < 0)
            {
                trimLeft = TimeSpan.Zero;
            }

            SetThumbnailPosition(trimLeft);

            TrimLeft = trimLeft;
        }

        private void Right_OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            RightTransform.X += e.Delta.Translation.X;
            if (RightTransform.X >= MaxTranslateX) RightTransform.X = MaxTranslateX;
            if (RightTransform.X <= LeftTransform.X + (Left.ActualWidth + Right.ActualWidth) / 2) RightTransform.X = LeftTransform.X + (Left.ActualWidth + Right.ActualWidth) / 2;

            RightOpacityBorderTransform.X = RightTransform.X + (468 - MaxTranslateX);

            var trimRight = new TimeSpan((long)((RightTransform.X + MaxTranslateX) / (2 * MaxTranslateX) * _videoProperties.Duration.Ticks));
            if (trimRight.Ticks >= _videoProperties.Duration.Ticks)
            {
                trimRight = new TimeSpan(_videoProperties.Duration.Ticks - 1);
            }
            SetThumbnailPosition(trimRight);

            TrimRight = trimRight;
        }

        private TimeSpan? _lastPosition;

        private bool _isManipulating;

        private void SetThumbnailPosition(TimeSpan position)
        {
            _lastPosition = position;
        }

        private void Slider_OnManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            _isManipulating = true;

            Execute.BeginOnThreadPool(async () =>
            {
                TimeSpan processedPosition;
                while (_isManipulating)
                {
                    var position = _lastPosition;
                    if (position != null && processedPosition != position)
                    {
                        var thumbnailWidth = _videoProperties.Orientation == VideoOrientation.Normal || _videoProperties.Orientation == VideoOrientation.Rotate180 ? (int)_videoProperties.Width : (int)_videoProperties.Height;
                        var thumbnailHeight = _videoProperties.Orientation == VideoOrientation.Normal || _videoProperties.Orientation == VideoOrientation.Rotate180 ? (int)_videoProperties.Height : (int)_videoProperties.Width;
                        var photo = await _composition.GetThumbnailAsync(position.Value,
                            thumbnailWidth,
                            thumbnailHeight,
                            VideoFramePrecision.NearestFrame);

                        processedPosition = position.Value;
                        Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            RaiseThumbnailChanged(new ThumbnailChangedEventArgs { Thumbnail = photo });
                        });
                    }

//#if DEBUG
//                    VibrateController.Default.Start(TimeSpan.FromMilliseconds(50));
//#endif
                }
            });
        }

        private void Slider_OnManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            _isManipulating = false;
        }
    }

    public class ThumbnailChangedEventArgs : System.EventArgs
    {
        public ImageStream Thumbnail { get; set; }
    }
}
