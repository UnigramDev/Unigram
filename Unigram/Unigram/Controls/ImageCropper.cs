using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unigram.Core.Helpers;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls
{
    public enum ImageCroppingProportions
    {
        Custom,
        Original,
        Square,
        TwoOverThree,
        ThreeOverFive,
        ThreeOverFour,
        FourOverFive,
        FiveOverSeven,
        NineOverSixteen,
        ThreeOverTwo,
        FiveOverThree,
        FourOverThree,
        FiveOverFour,
        SevenOverFive,
        SixteenOverNine,
    }

    public sealed class ImageCropperThumb : Control
    {
        #region constructors
        public ImageCropperThumb()
        {
            DefaultStyleKey = typeof(ImageCropperThumb);
        }
        #endregion
    }

    public class ImageCropper : ContentControl
    {
        #region fields
        private static readonly DependencyProperty s_proportionsProperty = DependencyProperty.Register("Proportions", typeof(ImageCroppingProportions), typeof(ImageCropper),
            new PropertyMetadata(ImageCroppingProportions.Original, new PropertyChangedCallback(ProportionsProperty_Changed)));
        private static readonly DependencyProperty s_cropRectangleProperty = DependencyProperty.Register("CropRectangle", typeof(Rect), typeof(ImageCropper),
            new PropertyMetadata(new Rect(0.0, 0.0, 100.0, 100.0), new PropertyChangedCallback(CropRectangleProperty_Changed)));
        //private static readonly DependencyProperty s_rotationAngleProperty = DependencyProperty.Register("RotationAngle", typeof(double), typeof(ImageCropper),
        //    new PropertyMetadata(0.0, new PropertyChangedCallback(RotationAngleProperty_Changed)));
        private static readonly DependencyProperty s_isCropEnabledProperty = DependencyProperty.Register("IsCropEnabled", typeof(bool), typeof(ImageCropper),
            new PropertyMetadata(true, new PropertyChangedCallback(IsCropEnabled_Changed)));

        private static Size s_minimumSize = new Size(100, 100);

        private StorageFile m_imageSource;
        private ImageSource m_imagePreview;
        private bool m_imageWaiting;

        private Geometry m_outerClip;
        private Geometry m_innerClip;

        private Grid m_layoutRoot;
        private Image m_imageViewer;
        private FrameworkElement m_imageThumb;
        private CompositeTransform m_imageThumbTransform;
        private Grid m_thumbsContainer; 
        private Dictionary<uint, Point> m_pointerPositions;
        private Size m_imageSize;
        private Rect m_thumbsRectangle;
        private Rect m_currentThumbsRectangle;
        private Rect m_imageRectangle;
        private Rect m_cropRectangle;
        #endregion

        #region properties
        public static DependencyProperty ProportionsProperty
        {
            get { return s_proportionsProperty; }
        }

        public ImageCroppingProportions Proportions
        {
            get { return (ImageCroppingProportions)GetValue(s_proportionsProperty); }
            set { SetValue(s_proportionsProperty, value); }
        }

        public int MaxZoomFactor
        {
            get { return (int)GetValue(MaxZoomFactorProperty); }
            set { SetValue(MaxZoomFactorProperty, value); }
        }
        
        public static readonly DependencyProperty MaxZoomFactorProperty =
            DependencyProperty.Register("MaxZoomFactor", typeof(int), typeof(ImageCropper), new PropertyMetadata(3));

        public int CurrentZoomFactor { get; private set; }

        //public static DependencyProperty RotationAngleProperty
        //{
        //    get { return s_rotationAngleProperty; }
        //}

        //public double RotationAngle
        //{
        //    get { return (double)GetValue(s_rotationAngleProperty); }
        //    set { SetValue(s_rotationAngleProperty, value); }
        //}

        public static DependencyProperty CropRectangleProperty
        {
            get { return s_cropRectangleProperty; }
        }

        public Rect CropRectangle
        {
            get { return (Rect)GetValue(s_cropRectangleProperty); }
            set { SetValue(s_cropRectangleProperty, value); }
        }

        public static DependencyProperty IsCropEnabledProperty
        {
            get { return s_isCropEnabledProperty; }
        }

        public bool IsCropEnabled
        {
            get { return (bool)GetValue(s_isCropEnabledProperty); }
            set { SetValue(s_isCropEnabledProperty, value); }
        }
        #endregion

        #region constructors
        public ImageCropper()
        {
            DefaultStyleKey = typeof(ImageCropper);

            m_imageSize = new Size(100, 00);
            m_cropRectangle = new Rect(0.0, 0.0, 100.0, 100.0);
            m_pointerPositions = new Dictionary<uint, Point>();

            SizeChanged += ImageCropper_SizeChanged;
        }
        #endregion

        #region methods
        protected override void OnApplyTemplate()
        {
            m_layoutRoot = (Grid)GetTemplateChild("LayoutRoot");
            m_imageViewer = (Image)GetTemplateChild("ImageViewer");
            m_imageThumb = (FrameworkElement)GetTemplateChild("ImageThumb");

            m_imageThumb.ManipulationDelta += ImageThumb_ManipulationDelta;
            m_imageThumb.PointerWheelChanged += ImageThumb_PointerWheelChanged;

            m_outerClip = (Geometry)GetTemplateChild("OuterClip");
            m_innerClip = (Geometry)GetTemplateChild("InnerClip");

            m_thumbsContainer = (Grid)GetTemplateChild("ThumbsContainer");
            m_imageThumbTransform = (CompositeTransform)GetTemplateChild("ImageThumbTransform");

            var leftThumb = (ImageCropperThumb)GetTemplateChild("LeftThumb");
            if (leftThumb != null)
            {
                leftThumb.PointerEntered += WEThumb_PointerEntered;
                leftThumb.PointerExited += Thumb_PointerExited;
                leftThumb.PointerPressed += Thumb_PointerPressed;
                leftThumb.PointerReleased += Thumb_PointerReleased;
                leftThumb.PointerMoved += LeftThumb_PointerMoved;
            }

            var topThumb = (ImageCropperThumb)GetTemplateChild("TopThumb");
            if (topThumb != null)
            {
                topThumb.PointerEntered += NSThumb_PointerEntered;
                topThumb.PointerExited += Thumb_PointerExited;
                topThumb.PointerPressed += Thumb_PointerPressed;
                topThumb.PointerReleased += Thumb_PointerReleased;
                topThumb.PointerMoved += TopThumb_PointerMoved;
            }

            var rightThumb = (ImageCropperThumb)GetTemplateChild("RightThumb");
            if (rightThumb != null)
            {
                rightThumb.PointerEntered += WEThumb_PointerEntered;
                rightThumb.PointerExited += Thumb_PointerExited;
                rightThumb.PointerPressed += Thumb_PointerPressed;
                rightThumb.PointerReleased += Thumb_PointerReleased;
                rightThumb.PointerMoved += RightThumb_PointerMoved;
            }

            var bottomThumb = (ImageCropperThumb)GetTemplateChild("BottomThumb");
            if (bottomThumb != null)
            {
                bottomThumb.PointerEntered += NSThumb_PointerEntered;
                bottomThumb.PointerExited += Thumb_PointerExited;
                bottomThumb.PointerPressed += Thumb_PointerPressed;
                bottomThumb.PointerReleased += Thumb_PointerReleased;
                bottomThumb.PointerMoved += BottomThumb_PointerMoved;
            }

            var topLeftThumb = (ImageCropperThumb)GetTemplateChild("TopLeftThumb");
            if (topLeftThumb != null)
            {
                topLeftThumb.PointerEntered += NWSEThumb_PointerEntered;
                topLeftThumb.PointerExited += Thumb_PointerExited;
                topLeftThumb.PointerPressed += Thumb_PointerPressed;
                topLeftThumb.PointerReleased += Thumb_PointerReleased;
                topLeftThumb.PointerMoved += TopLeftThumb_PointerMoved;
            }

            var topRightThumb = (ImageCropperThumb)GetTemplateChild("TopRightThumb");
            if (topLeftThumb != null)
            {
                topRightThumb.PointerEntered += NESWThumb_PointerEntered;
                topRightThumb.PointerExited += Thumb_PointerExited;
                topRightThumb.PointerPressed += Thumb_PointerPressed;
                topRightThumb.PointerReleased += Thumb_PointerReleased;
                topRightThumb.PointerMoved += TopRightThumb_PointerMoved;
            }

            var bottomLeftThumb = (ImageCropperThumb)GetTemplateChild("BottomLeftThumb");
            if (bottomLeftThumb != null)
            {
                bottomLeftThumb.PointerEntered += NESWThumb_PointerEntered;
                bottomLeftThumb.PointerExited += Thumb_PointerExited;
                bottomLeftThumb.PointerPressed += Thumb_PointerPressed;
                bottomLeftThumb.PointerReleased += Thumb_PointerReleased;
                bottomLeftThumb.PointerMoved += BottomLeftThumb_PointerMoved;
            }

            var bottomRightThumb = (ImageCropperThumb)GetTemplateChild("BottomRightThumb");
            if (bottomRightThumb != null)
            {
                bottomRightThumb.PointerEntered += NWSEThumb_PointerEntered;
                bottomRightThumb.PointerExited += Thumb_PointerExited;
                bottomRightThumb.PointerPressed += Thumb_PointerPressed;
                bottomRightThumb.PointerReleased += Thumb_PointerReleased;
                bottomRightThumb.PointerMoved += BottomRightThumb_PointerMoved;
            }

            if (m_imageWaiting)
            {
                m_imageWaiting = false;
                SetSource(m_imageSource, m_imagePreview, m_imageSize.Width, m_imageSize.Height);
            }
            else
            {
                UpdateCropRectangle(CropRectangle, false);
            }
        }

        private void UpdateThumbs(Rect thumbsRectangle)
        {
            Canvas.SetLeft(m_thumbsContainer, thumbsRectangle.Left);
            Canvas.SetTop(m_thumbsContainer, thumbsRectangle.Top);

            m_thumbsContainer.Width = thumbsRectangle.Width;
            m_thumbsContainer.Height = thumbsRectangle.Height;

            switch (m_innerClip)
            {
                case RectangleGeometry rectangle:
                    rectangle.Rect = thumbsRectangle;
                    break;
                case EllipseGeometry ellipse:
                    ellipse.Center = new Point(thumbsRectangle.Left + thumbsRectangle.Width / 2, thumbsRectangle.Top + thumbsRectangle.Height / 2);
                    ellipse.RadiusX = thumbsRectangle.Width / 2;
                    ellipse.RadiusY = thumbsRectangle.Height / 2;
                    break;
            }
        }

        private void UpdateCropRectangle(bool animate)
        {
            var cropScaleX = m_imageRectangle.Width / m_imageSize.Width;
            var cropScaleY = m_imageRectangle.Height / m_imageSize.Height;
            m_cropRectangle = new Rect((m_currentThumbsRectangle.X - m_imageRectangle.X) / cropScaleX,
                (m_currentThumbsRectangle.Y - m_imageRectangle.Y) / cropScaleY,
                m_currentThumbsRectangle.Width / cropScaleX, m_currentThumbsRectangle.Height / cropScaleY);

            UpdateCropRectangle(m_cropRectangle, animate);
        }

        private void UpdateCropRectangle(Rect cropRectangle, bool animate)
        {
            if (m_layoutRoot != null)
            {
                Size thumbsRectangleSize;
                var cropScale = cropRectangle.Width / cropRectangle.Height;
                if (m_layoutRoot.ActualWidth / m_layoutRoot.ActualHeight < cropScale)
                {
                    thumbsRectangleSize = new Size(m_layoutRoot.ActualWidth, m_layoutRoot.ActualWidth / cropScale);
                }
                else
                {
                    thumbsRectangleSize = new Size(m_layoutRoot.ActualHeight * cropScale, m_layoutRoot.ActualHeight);
                }

                var finalThumbsRectangle = new Rect((m_layoutRoot.ActualWidth - thumbsRectangleSize.Width) / 2.0,
                    (m_layoutRoot.ActualHeight - thumbsRectangleSize.Height) / 2.0, thumbsRectangleSize.Width, thumbsRectangleSize.Height);

                m_thumbsRectangle = m_currentThumbsRectangle = finalThumbsRectangle;

                var imageScaleX = finalThumbsRectangle.Width / cropRectangle.Width;
                var imageScaleY = finalThumbsRectangle.Height / cropRectangle.Height;
                var imageRectangleWidth = m_imageSize.Width * imageScaleX;
                var imageRectangleHeight = m_imageSize.Height * imageScaleY;

                m_imageRectangle = new Rect(finalThumbsRectangle.X - cropRectangle.X * imageScaleX,
                    finalThumbsRectangle.Y - cropRectangle.Y * imageScaleY,
                    imageRectangleWidth, imageRectangleHeight);

                if (animate)
                {
                    AnimateToCropRectangle(m_thumbsRectangle, m_imageRectangle);
                }
                else
                {
                    UpdateThumbs(m_thumbsRectangle);

                    m_imageThumbTransform.ScaleX = m_imageRectangle.Width / m_imageSize.Width;
                    m_imageThumbTransform.ScaleY = m_imageRectangle.Height / m_imageSize.Height;
                    m_imageThumbTransform.TranslateX = (m_imageRectangle.Width - m_thumbsRectangle.Width) / 2.0 - m_thumbsRectangle.X + m_imageRectangle.X;
                    m_imageThumbTransform.TranslateY = (m_imageRectangle.Height - m_thumbsRectangle.Height) / 2.0 - m_thumbsRectangle.Y + m_imageRectangle.Y;
                }
            }

            CropRectangle = cropRectangle;
        }

        private void AnimateToCropRectangle(Rect thumbsRectangle, Rect imageRectangle)
        {
            var storyboard = new Storyboard();
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            storyboard.Children.Add(CreateAnimation(thumbsRectangle.Width, m_thumbsContainer, "FrameworkElement.Width", ease, true));
            storyboard.Children.Add(CreateAnimation(thumbsRectangle.Height, m_thumbsContainer, "FrameworkElement.Height", ease, true));
            storyboard.Children.Add(CreateAnimation(thumbsRectangle.Left, m_thumbsContainer, "(Canvas.Left)", ease, false));
            storyboard.Children.Add(CreateAnimation(thumbsRectangle.Top, m_thumbsContainer, "(Canvas.Top)", ease, false));

            storyboard.Children.Add(CreateAnimation(imageRectangle.Width / m_imageSize.Width, m_imageThumbTransform, "CompositeTransform.ScaleX", ease, false));
            storyboard.Children.Add(CreateAnimation(imageRectangle.Height / m_imageSize.Height, m_imageThumbTransform, "CompositeTransform.ScaleY", ease, false));
            storyboard.Children.Add(CreateAnimation((imageRectangle.Width - thumbsRectangle.Width) / 2.0 - thumbsRectangle.X + imageRectangle.X, m_imageThumbTransform, "CompositeTransform.TranslateX", ease, false));
            storyboard.Children.Add(CreateAnimation((imageRectangle.Height - thumbsRectangle.Height) / 2.0 - thumbsRectangle.Y + imageRectangle.Y, m_imageThumbTransform, "CompositeTransform.TranslateY", ease, false));

            switch (m_innerClip)
            {
                case RectangleGeometry rectangle:
                    rectangle.Rect = thumbsRectangle;
                    break;
                case EllipseGeometry ellipse:
                    var centerAnimation = new PointAnimation();
                    centerAnimation.To = new Point(thumbsRectangle.Left + thumbsRectangle.Width / 2, thumbsRectangle.Top + thumbsRectangle.Height / 2);
                    centerAnimation.EasingFunction = ease;
                    centerAnimation.Duration = TimeSpan.FromMilliseconds(300);
                    centerAnimation.EnableDependentAnimation = true;

                    Storyboard.SetTarget(centerAnimation, ellipse);
                    Storyboard.SetTargetProperty(centerAnimation, "EllipseGeometry.Center");

                    storyboard.Children.Add(centerAnimation);

                    storyboard.Children.Add(CreateAnimation(thumbsRectangle.Width / 2.0, ellipse, "EllipseGeometry.RadiusX", ease, true));
                    storyboard.Children.Add(CreateAnimation(thumbsRectangle.Height / 2.0, ellipse, "EllipseGeometry.RadiusY", ease, true));
                    break;
            }

            storyboard.Begin();
        }

        private double GetProportionsFactor(ImageCroppingProportions proportions, double defaultValue)
        {
            switch (proportions)
            {
                case ImageCroppingProportions.Original:
                    return m_imageSize.Width / m_imageSize.Height;
                case ImageCroppingProportions.Square:
                    return 1.0;
                // Portrait
                case ImageCroppingProportions.TwoOverThree:
                    return 2.0 / 3.0;
                case ImageCroppingProportions.ThreeOverFive:
                    return 3.0 / 5.0;
                case ImageCroppingProportions.ThreeOverFour:
                    return 3.0 / 4.0;
                case ImageCroppingProportions.FourOverFive:
                    return 4.0 / 5.0;
                case ImageCroppingProportions.FiveOverSeven:
                    return 5.0 / 7.0;
                case ImageCroppingProportions.NineOverSixteen:
                    return 9.0 / 16.0;
                // Landscape
                case ImageCroppingProportions.ThreeOverTwo:
                    return 3.0 / 2.0;
                case ImageCroppingProportions.FiveOverThree:
                    return 5.0 / 3.0;
                case ImageCroppingProportions.FourOverThree:
                    return 4.0 / 3.0;
                case ImageCroppingProportions.FiveOverFour:
                    return 5.0 / 4.0;
                case ImageCroppingProportions.SevenOverFive:
                    return 7.0 / 5.0;
                case ImageCroppingProportions.SixteenOverNine:
                    return 16.0 / 9.0;
                default:
                    return defaultValue;
            }
        }

        public static IReadOnlyList<ImageCroppingProportions> GetProportionsFor(double width, double height)
        {
            var items = new List<ImageCroppingProportions>();
            items.Add(ImageCroppingProportions.Original);
            items.Add(ImageCroppingProportions.Square);

            if (width > height)
            {
                items.Add(ImageCroppingProportions.ThreeOverTwo);
                items.Add(ImageCroppingProportions.FiveOverThree);
                items.Add(ImageCroppingProportions.FourOverThree);
                items.Add(ImageCroppingProportions.FiveOverFour);
                items.Add(ImageCroppingProportions.SevenOverFive);
                items.Add(ImageCroppingProportions.SixteenOverNine);
            }
            else
            {
                items.Add(ImageCroppingProportions.TwoOverThree);
                items.Add(ImageCroppingProportions.ThreeOverFive);
                items.Add(ImageCroppingProportions.ThreeOverFour);
                items.Add(ImageCroppingProportions.FourOverFive);
                items.Add(ImageCroppingProportions.FiveOverSeven);
                items.Add(ImageCroppingProportions.NineOverSixteen);
            }

            return items;
        }

        public async void SetSource(StorageFile file)
        {
            await SetSourceAsync(file);
        }

        public async Task<StorageFile> CropAsync(int min = 1280, int max = 0)
        {
            var croppedFile = await ImageHelper.CropAsync(m_imageSource, CropRectangle, min, max);

            return croppedFile;
        }

        public async Task SetSourceAsync(StorageFile file)
        {
            SoftwareBitmapSource source;
            using (var fileStream = await ImageHelper.OpenReadAsync(file))
            {
                var decoder = await BitmapDecoder.CreateAsync(fileStream);
                var transform = ImageHelper.ComputeScalingTransformForSourceImage(decoder);

                var software = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
                source = new SoftwareBitmapSource();
                await source.SetBitmapAsync(software);

                SetSource(file, source, software.PixelWidth, software.PixelHeight);
            }

            Canvas.SetLeft(m_imageThumb, (m_layoutRoot.ActualWidth - m_imageSize.Width) / 2.0);
            Canvas.SetTop(m_imageThumb, (m_layoutRoot.ActualHeight - m_imageSize.Height) / 2.0);

            var imageScale = m_imageSize.Width / m_imageSize.Height;
            var cropScale = GetProportionsFactor(Proportions, imageScale);
            if (imageScale < cropScale)
            {
                var cropHeight = m_imageSize.Width / cropScale;
                m_cropRectangle = new Rect(0.0, (m_imageSize.Height - cropHeight) / 2.0, m_imageSize.Width, cropHeight);
            }
            else
            {
                var cropWidth = m_imageSize.Height * cropScale;
                m_cropRectangle = new Rect((m_imageSize.Width - cropWidth) / 2.0, 0.0, cropWidth, m_imageSize.Height);
            }

            UpdateCropRectangle(m_cropRectangle, false);
        }

        public void SetSource(StorageFile file, ImageSource source, double width, double height)
        {
            m_imagePreview = source;
            m_imageSource = file;
            m_imageSize = new Size(width, height);

            if (m_imageViewer != null)
            {
                m_imageViewer.Source = m_imagePreview;

                Canvas.SetLeft(m_imageThumb, (m_layoutRoot.ActualWidth - m_imageSize.Width) / 2.0);
                Canvas.SetTop(m_imageThumb, (m_layoutRoot.ActualHeight - m_imageSize.Height) / 2.0);

                var imageScale = m_imageSize.Width / m_imageSize.Height;
                var cropScale = GetProportionsFactor(Proportions, imageScale);
                if (imageScale < cropScale)
                {
                    var cropHeight = m_imageSize.Width / cropScale;
                    m_cropRectangle = new Rect(0.0, (m_imageSize.Height - cropHeight) / 2.0, m_imageSize.Width, cropHeight);
                }
                else
                {
                    var cropWidth = m_imageSize.Height * cropScale;
                    m_cropRectangle = new Rect((m_imageSize.Width - cropWidth) / 2.0, 0.0, cropWidth, m_imageSize.Height);
                }

                UpdateCropRectangle(m_cropRectangle, false);
            }
            else
            {
                m_imageWaiting = true;
            }
        }

        public void Reset(ImageCroppingProportions? proportions = null)
        {
            var imageScale = m_imageSize.Width / m_imageSize.Height;
            var cropScale = GetProportionsFactor(proportions ?? Proportions, imageScale);
            if (imageScale < cropScale)
            {
                var cropHeight = m_imageSize.Width / cropScale;
                m_cropRectangle = new Rect(0.0, (m_imageSize.Height - cropHeight) / 2.0, m_imageSize.Width, cropHeight);
            }
            else
            {
                var cropWidth = m_imageSize.Height * cropScale;
                m_cropRectangle = new Rect((m_imageSize.Width - cropWidth) / 2.0, 0.0, cropWidth, m_imageSize.Height);
            }

            UpdateCropRectangle(m_cropRectangle, true);
        }

        private void OnProportionsChanged(ImageCroppingProportions oldValue, ImageCroppingProportions newValue)
        {
            var cropScale = m_cropRectangle.Width / m_cropRectangle.Height;
            var proportionalCropScale = GetProportionsFactor(Proportions, cropScale);

            if (cropScale < proportionalCropScale)
            {
                var cropHeight = m_cropRectangle.Width / proportionalCropScale;

                m_cropRectangle.Y = Clamp(m_cropRectangle.Y + (m_cropRectangle.Height - cropHeight) / 2.0, 0.0, m_imageSize.Height - cropHeight);
                m_cropRectangle.Height = cropHeight;
            }
            else
            {
                var cropWidth = m_cropRectangle.Height * proportionalCropScale;

                m_cropRectangle.X = Clamp(m_cropRectangle.X + (m_cropRectangle.Width - cropWidth) / 2.0, 0.0, m_imageSize.Width - cropWidth);
                m_cropRectangle.Width = cropWidth;
            }

            UpdateCropRectangle(m_cropRectangle, true);
        }

        //protected virtual void OnRotationAngleChanged(double oldValue, double newValue)
        //{
        //}

        protected virtual void OnCropRectangleChanged(Rect oldValue, Rect newValue)
        {
            if (newValue.IsEmpty)
            {
                Reset();
            }
            else if (newValue != m_cropRectangle)
            {
                m_cropRectangle = newValue;
                UpdateCropRectangle(m_cropRectangle, false);
            }
        }

        protected virtual void OnIsCropEnabledChanged(bool oldValue, bool newValue)
        {
            VisualStateManager.GoToState(this, newValue ? "Normal" : "Disabled", true);
        }

        private static Size GetImageSourceSize(ImageSource imageSource)
        {
            switch (imageSource)
            {
                case BitmapImage bitmapImage:
                    return new Size(bitmapImage.PixelWidth, bitmapImage.PixelHeight);
                case RenderTargetBitmap renderTargetBitmap:
                    return new Size(renderTargetBitmap.PixelWidth, renderTargetBitmap.PixelHeight);
                case null:
                    return default(Size);
                default:
                    return default(Size);
            }
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum)
                value = minimum;

            if (value > maximum)
                value = maximum;

            return value;
        }

        private static DoubleAnimation CreateAnimation(double to, DependencyObject target, string propertyName, EasingFunctionBase ease, bool enableDependentAnimation)
        {
            var animation = new DoubleAnimation()
            {
                To = to,
                Duration = TimeSpan.FromMilliseconds(300),
                EnableDependentAnimation = enableDependentAnimation,
                EasingFunction = ease
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, propertyName);

            return animation;
        }
        #endregion

        #region event methods
        private void NWSEThumb_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.SizeNorthwestSoutheast, 1);
        }

        private void NESWThumb_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.SizeNortheastSouthwest, 1);
        }

        private void WEThumb_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.SizeWestEast, 1);
        }

        private void NSThumb_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.SizeNorthSouth, 1);
        }

        private void Thumb_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 1);
        }

        private void Thumb_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).CapturePointer(e.Pointer);

            var pointer = e.GetCurrentPoint(m_layoutRoot);
            m_pointerPositions[pointer.PointerId] = pointer.Position;

            e.Handled = true;
        }

        private void Thumb_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            m_pointerPositions.Remove(e.Pointer.PointerId);

            ((UIElement)sender).ReleasePointerCapture(e.Pointer);

            UpdateCropRectangle(true);

            e.Handled = true;
        }

        private void ImageThumb_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (e.Delta.Scale != 1.0)
            {
                double width;
                double height;
                var imageScale = m_imageRectangle.Width / m_imageRectangle.Height;

                if (m_thumbsRectangle.Width / m_thumbsRectangle.Height < imageScale)
                {
                    height = Math.Max(m_imageRectangle.Height * e.Delta.Scale, m_thumbsRectangle.Height);
                    width = height * imageScale;
                }
                else
                {
                    width = Math.Max(m_imageRectangle.Width * e.Delta.Scale, m_thumbsRectangle.Width);
                    height = width / imageScale;
                }

                m_imageRectangle.X = Clamp(m_imageRectangle.Left + (m_imageRectangle.Width - width) / 2.0, m_thumbsRectangle.Right - width, m_thumbsRectangle.Left);
                m_imageRectangle.Y = Clamp(m_imageRectangle.Top + (m_imageRectangle.Height - height) / 2.0, m_thumbsRectangle.Bottom - height, m_thumbsRectangle.Top);
                m_imageRectangle.Width = width;
                m_imageRectangle.Height = height;

                UpdateCropRectangle(false);

                e.Handled = true;
            }
            else if (e.Delta.Translation.X != 0.0 || e.Delta.Translation.Y != 0.0)
            {
                m_imageRectangle.X = Clamp(m_imageRectangle.Left + e.Delta.Translation.X, m_thumbsRectangle.Right - m_imageRectangle.Width, m_thumbsRectangle.Left);
                m_imageRectangle.Y = Clamp(m_imageRectangle.Top + e.Delta.Translation.Y, m_thumbsRectangle.Bottom - m_imageRectangle.Height, m_thumbsRectangle.Top);

                UpdateCropRectangle(false);

                e.Handled = true;
            }
        }

        private void ImageThumb_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (!IsCropEnabled)
            {
                return;
            }

            var mouseWheelDelta = e.GetCurrentPoint(sender as UIElement).Properties.MouseWheelDelta / 100;
            if (mouseWheelDelta == 0)
            {
                return;
            }
            
            if (mouseWheelDelta > 0)
            {
                ZoomIn();
            }
            else
            {
                ZoomOut();
            }

            e.Handled = true;
        }

        private void ZoomIn(double zoomInFactor = 1.1)
        {
            if (CurrentZoomFactor >= MaxZoomFactor)
            {
                return;
            }

            CurrentZoomFactor++;

            double width;
            double height;
            var imageScale = m_imageRectangle.Width / m_imageRectangle.Height;

            if (m_thumbsRectangle.Width / m_thumbsRectangle.Height < imageScale)
            {
                height = Math.Max(m_imageRectangle.Height * zoomInFactor, m_thumbsRectangle.Height);
                width = height * imageScale;
            }
            else
            {
                width = Math.Max(m_imageRectangle.Width * zoomInFactor, m_thumbsRectangle.Width);
                height = width / imageScale;
            }

            m_imageRectangle.X = Clamp(m_imageRectangle.Left + (m_imageRectangle.Width - width) / 2.0, m_thumbsRectangle.Right - width, m_thumbsRectangle.Left);
            m_imageRectangle.Y = Clamp(m_imageRectangle.Top + (m_imageRectangle.Height - height) / 2.0, m_thumbsRectangle.Bottom - height, m_thumbsRectangle.Top);
            m_imageRectangle.Width = width;
            m_imageRectangle.Height = height;

            UpdateCropRectangle(false);
        }

        private void ZoomOut(double zoomOutFactor = 0.9)
        {
            if (CurrentZoomFactor <= 0)
            {
                return;
            }

            CurrentZoomFactor--;

            double width;
            double height;
            var imageScale = m_imageRectangle.Width / m_imageRectangle.Height;

            if (m_thumbsRectangle.Width / m_thumbsRectangle.Height < imageScale)
            {
                height = Math.Max(m_imageRectangle.Height * zoomOutFactor, m_thumbsRectangle.Height);
                width = height * imageScale;
            }
            else
            {
                width = Math.Max(m_imageRectangle.Width * zoomOutFactor, m_thumbsRectangle.Width);
                height = width / imageScale;
            }

            m_imageRectangle.X = Clamp(m_imageRectangle.Left + (m_imageRectangle.Width - width) / 2.0, m_thumbsRectangle.Right - width, m_thumbsRectangle.Left);
            m_imageRectangle.Y = Clamp(m_imageRectangle.Top + (m_imageRectangle.Height - height) / 2.0, m_thumbsRectangle.Bottom - height, m_thumbsRectangle.Top);
            m_imageRectangle.Width = width;
            m_imageRectangle.Height = height;

            UpdateCropRectangle(false);
        }

        private void TopLeftThumb_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact && m_pointerPositions.TryGetValue(e.Pointer.PointerId, out Point startPosition))
            {
                var position = e.GetCurrentPoint(m_layoutRoot).Position;
                var offsetX = position.X - startPosition.X;
                var offsetY = position.Y - startPosition.Y;

                var left = Clamp(m_thumbsRectangle.Left + offsetX, m_imageRectangle.Left, m_thumbsRectangle.Right - s_minimumSize.Width);
                var top = Clamp(m_thumbsRectangle.Top + offsetY, m_imageRectangle.Top, m_thumbsRectangle.Bottom - s_minimumSize.Height);
                var width = m_currentThumbsRectangle.Right - left;
                var height = m_currentThumbsRectangle.Bottom - top;

                var cropScale = width / height;
                var proportionalCropScale = GetProportionsFactor(Proportions, cropScale);

                if (cropScale < proportionalCropScale)
                {
                    var cropHeight = width / proportionalCropScale;

                    m_currentThumbsRectangle.X = left;
                    m_currentThumbsRectangle.Y = top + (height - cropHeight);
                    m_currentThumbsRectangle.Width = width;
                    m_currentThumbsRectangle.Height = cropHeight;
                }
                else
                {
                    var cropWidth = height * proportionalCropScale;

                    m_currentThumbsRectangle.X = left + (width - cropWidth);
                    m_currentThumbsRectangle.Y = top;
                    m_currentThumbsRectangle.Width = cropWidth;
                    m_currentThumbsRectangle.Height = height;
                }

                if (m_currentThumbsRectangle.Left < m_thumbsRectangle.Left || m_currentThumbsRectangle.Top < m_thumbsRectangle.Top)
                {
                    UpdateCropRectangle(false);
                }
                else
                {
                    UpdateThumbs(m_currentThumbsRectangle);
                }

                e.Handled = true;
            }
        }

        private void TopRightThumb_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact && m_pointerPositions.TryGetValue(e.Pointer.PointerId, out Point startPosition))
            {
                var position = e.GetCurrentPoint(m_layoutRoot).Position;
                var offsetX = position.X - startPosition.X;
                var offsetY = position.Y - startPosition.Y;

                var top = Clamp(m_thumbsRectangle.Top + offsetY, m_imageRectangle.Top, m_thumbsRectangle.Bottom - s_minimumSize.Height);
                var right = Clamp(m_thumbsRectangle.Right + offsetX, m_thumbsRectangle.Left + s_minimumSize.Width, m_imageRectangle.Right);
                var width = right - m_currentThumbsRectangle.Left;
                var height = m_currentThumbsRectangle.Bottom - top;

                var cropScale = width / height;
                var proportionalCropScale = GetProportionsFactor(Proportions, cropScale);

                if (cropScale < proportionalCropScale)
                {
                    var cropHeight = width / proportionalCropScale;

                    m_currentThumbsRectangle.Y = top + (height - cropHeight);
                    m_currentThumbsRectangle.Width = width;
                    m_currentThumbsRectangle.Height = cropHeight;
                }
                else
                {
                    var cropWidth = height * proportionalCropScale;

                    m_currentThumbsRectangle.Y = top;
                    m_currentThumbsRectangle.Width = cropWidth;
                    m_currentThumbsRectangle.Height = height;
                }

                if (m_currentThumbsRectangle.Left < m_thumbsRectangle.Left || m_currentThumbsRectangle.Right > m_thumbsRectangle.Right)
                {
                    UpdateCropRectangle(false);
                }
                else
                {
                    UpdateThumbs(m_currentThumbsRectangle);
                }

                e.Handled = true;
            }
        }

        private void BottomLeftThumb_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact && m_pointerPositions.TryGetValue(e.Pointer.PointerId, out Point startPosition))
            {
                var position = e.GetCurrentPoint(m_layoutRoot).Position;
                var offsetX = position.X - startPosition.X;
                var offsetY = position.Y - startPosition.Y;

                var left = Clamp(m_thumbsRectangle.Left + offsetX, m_imageRectangle.Left, m_thumbsRectangle.Right - s_minimumSize.Width);
                var bottom = Clamp(m_thumbsRectangle.Bottom + offsetY, m_thumbsRectangle.Top + s_minimumSize.Height, m_imageRectangle.Bottom);
                var width = m_currentThumbsRectangle.Right - left;
                var height = bottom - m_currentThumbsRectangle.Top;

                var cropScale = width / height;
                var proportionalCropScale = GetProportionsFactor(Proportions, cropScale);

                if (cropScale < proportionalCropScale)
                {
                    var cropHeight = width / proportionalCropScale;

                    m_currentThumbsRectangle.X = left;
                    m_currentThumbsRectangle.Width = width;
                    m_currentThumbsRectangle.Height = cropHeight;
                }
                else
                {
                    var cropWidth = height * proportionalCropScale;

                    m_currentThumbsRectangle.X = left + (width - cropWidth);
                    m_currentThumbsRectangle.Width = cropWidth;
                    m_currentThumbsRectangle.Height = height;
                }

                if (m_currentThumbsRectangle.Bottom > m_thumbsRectangle.Bottom || m_currentThumbsRectangle.Left < m_thumbsRectangle.Left)
                {
                    UpdateCropRectangle(false);
                }
                else
                {
                    UpdateThumbs(m_currentThumbsRectangle);
                }

                e.Handled = true;
            }
        }

        private void BottomRightThumb_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact && m_pointerPositions.TryGetValue(e.Pointer.PointerId, out Point startPosition))
            {
                var position = e.GetCurrentPoint(m_layoutRoot).Position;
                var offsetX = position.X - startPosition.X;
                var offsetY = position.Y - startPosition.Y;

                var right = Clamp(m_thumbsRectangle.Right + offsetX, m_thumbsRectangle.Left + s_minimumSize.Width, m_imageRectangle.Right);
                var bottom = Clamp(m_thumbsRectangle.Bottom + offsetY, m_thumbsRectangle.Top + s_minimumSize.Height, m_imageRectangle.Bottom);
                var width = right - m_currentThumbsRectangle.Left;
                var height = bottom - m_currentThumbsRectangle.Top;

                var cropScale = width / height;
                var proportionalCropScale = GetProportionsFactor(Proportions, cropScale);

                if (cropScale < proportionalCropScale)
                {
                    var cropHeight = width / proportionalCropScale;

                    m_currentThumbsRectangle.Width = width;
                    m_currentThumbsRectangle.Height = cropHeight;
                }
                else
                {
                    var cropWidth = height * proportionalCropScale;

                    m_currentThumbsRectangle.Width = cropWidth;
                    m_currentThumbsRectangle.Height = height;
                }

                if (m_currentThumbsRectangle.Bottom > m_thumbsRectangle.Bottom || m_currentThumbsRectangle.Right > m_thumbsRectangle.Right)
                {
                    UpdateCropRectangle(false);
                }
                else
                {
                    UpdateThumbs(m_currentThumbsRectangle);
                }

                e.Handled = true;
            }
        }

        private void BottomThumb_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact && m_pointerPositions.TryGetValue(e.Pointer.PointerId, out Point startPosition))
            {
                var position = e.GetCurrentPoint(m_layoutRoot).Position;
                var offsetY = position.Y - startPosition.Y;

                var bottom = Clamp(m_thumbsRectangle.Bottom + offsetY, m_thumbsRectangle.Top + s_minimumSize.Height, m_imageRectangle.Bottom);
                var height = bottom - m_currentThumbsRectangle.Top;

                var proportionalCropScale = GetProportionsFactor(Proportions, m_currentThumbsRectangle.Width / height);
                var cropWidth = Math.Min(m_imageRectangle.Width, height * proportionalCropScale);

                m_currentThumbsRectangle.X = Clamp(m_currentThumbsRectangle.X + (m_currentThumbsRectangle.Width - cropWidth) / 2.0, m_imageRectangle.X, m_imageRectangle.Right - cropWidth);
                m_currentThumbsRectangle.Width = cropWidth;
                m_currentThumbsRectangle.Height = cropWidth / proportionalCropScale;

                if (m_currentThumbsRectangle.Bottom > m_thumbsRectangle.Bottom)
                {
                    UpdateCropRectangle(false);
                }
                else
                {
                    UpdateThumbs(m_currentThumbsRectangle);
                }

                e.Handled = true;
            }
        }

        private void RightThumb_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact && m_pointerPositions.TryGetValue(e.Pointer.PointerId, out Point startPosition))
            {
                var position = e.GetCurrentPoint(m_layoutRoot).Position;
                var offsetX = position.X - startPosition.X;

                var right = Clamp(m_thumbsRectangle.Right + offsetX, m_thumbsRectangle.Left + s_minimumSize.Width, m_imageRectangle.Right);
                var width = right - m_currentThumbsRectangle.Left;

                var proportionalCropScale = GetProportionsFactor(Proportions, width / m_currentThumbsRectangle.Height);
                var cropHeight = Math.Min(m_imageRectangle.Height, width / proportionalCropScale);

                m_currentThumbsRectangle.Y = Clamp(m_currentThumbsRectangle.Y + (m_currentThumbsRectangle.Height - cropHeight) / 2.0, m_imageRectangle.Y, m_imageRectangle.Bottom - cropHeight);
                m_currentThumbsRectangle.Width = cropHeight * proportionalCropScale;
                m_currentThumbsRectangle.Height = cropHeight;

                if (m_currentThumbsRectangle.Right > m_thumbsRectangle.Right)
                {
                    UpdateCropRectangle(false);
                }
                else
                {
                    UpdateThumbs(m_currentThumbsRectangle);
                }

                e.Handled = true;
            }
        }

        private void TopThumb_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact && m_pointerPositions.TryGetValue(e.Pointer.PointerId, out Point startPosition))
            {
                var position = e.GetCurrentPoint(m_layoutRoot).Position;
                var offsetY = position.Y - startPosition.Y;

                var top = Clamp(m_thumbsRectangle.Top + offsetY, m_imageRectangle.Top, m_thumbsRectangle.Bottom - s_minimumSize.Height);
                var height = m_currentThumbsRectangle.Bottom - top;

                var proportionalCropScale = GetProportionsFactor(Proportions, m_currentThumbsRectangle.Width / height);
                var cropWidth = Math.Min(m_imageRectangle.Width, height * proportionalCropScale);

                m_currentThumbsRectangle.Y = top;
                m_currentThumbsRectangle.X = Clamp(m_currentThumbsRectangle.X + (m_currentThumbsRectangle.Width - cropWidth) / 2.0, m_imageRectangle.X, m_imageRectangle.Right - cropWidth);
                m_currentThumbsRectangle.Width = cropWidth;
                m_currentThumbsRectangle.Height = cropWidth / proportionalCropScale;

                if (m_currentThumbsRectangle.Top < m_thumbsRectangle.Top)
                {
                    UpdateCropRectangle(false);
                }
                else
                {
                    UpdateThumbs(m_currentThumbsRectangle);
                }

                e.Handled = true;
            }
        }

        private void LeftThumb_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact && m_pointerPositions.TryGetValue(e.Pointer.PointerId, out Point startPosition))
            {
                var position = e.GetCurrentPoint(m_layoutRoot).Position;
                var offsetX = position.X - startPosition.X;

                var left = Clamp(m_thumbsRectangle.Left + offsetX, m_imageRectangle.Left, m_thumbsRectangle.Right - s_minimumSize.Width);
                var width = m_currentThumbsRectangle.Right - left;

                var proportionalCropScale = GetProportionsFactor(Proportions, width / m_currentThumbsRectangle.Height);
                var cropHeight = Math.Min(m_imageRectangle.Height, width / proportionalCropScale);

                m_currentThumbsRectangle.X = left;
                m_currentThumbsRectangle.Y = Clamp(m_currentThumbsRectangle.Y + (m_currentThumbsRectangle.Height - cropHeight) / 2.0, m_imageRectangle.Y, m_imageRectangle.Bottom - cropHeight);
                m_currentThumbsRectangle.Width = cropHeight * proportionalCropScale;
                m_currentThumbsRectangle.Height = cropHeight;

                if (m_currentThumbsRectangle.Left < m_thumbsRectangle.Left)
                {
                    UpdateCropRectangle(false);
                }
                else
                {
                    UpdateThumbs(m_currentThumbsRectangle);
                }

                e.Handled = true;
            }
        }

        private void ImageCropper_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //Clip = new RectangleGeometry() { Rect = new Rect(default(Point), e.NewSize) };

            switch (m_outerClip)
            {
                case RectangleGeometry rectangle:
                    rectangle.Rect = new Rect(-Padding.Left, -Padding.Top, ActualWidth, ActualHeight);
                    break;
            }

            Canvas.SetLeft(m_imageThumb, (m_layoutRoot.ActualWidth - m_imageSize.Width) / 2.0);
            Canvas.SetTop(m_imageThumb, (m_layoutRoot.ActualHeight - m_imageSize.Height) / 2.0);

            UpdateCropRectangle(m_cropRectangle, false);
        }

        private static void ProportionsProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ImageCropper)d).OnProportionsChanged((ImageCroppingProportions)e.OldValue, (ImageCroppingProportions)e.NewValue);
        }

        //private static void RotationAngleProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{
        //    ((ImageCropper)d).OnRotationAngleChanged((double)e.OldValue, (double)e.NewValue);
        //}

        private static void CropRectangleProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ImageCropper)d).OnCropRectangleChanged((Rect)e.OldValue, (Rect)e.NewValue);
        }

        private static void IsCropEnabled_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ImageCropper)d).OnIsCropEnabledChanged((bool)e.OldValue, (bool)e.NewValue);
        }

        #endregion
    }

}
