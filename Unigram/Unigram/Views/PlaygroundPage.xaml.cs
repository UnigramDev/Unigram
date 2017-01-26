using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Unigram.Controls;
using Unigram.Core.Dependency;
using Unigram.Native;
using Unigram.ViewModels;
using Windows.ApplicationModel.Calls;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Media.Render;
using Windows.Phone.Media.Devices;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlaygroundPage : Page
    {
        private OpusRecorder recorder;

        public PlaygroundPage()
        {
            this.InitializeComponent();

            Loaded += PlaygroundPage_Loaded;
        }

        private void PlaygroundPage_Loaded(object sender, RoutedEventArgs e)
        {
            Storyboard1.Begin();
        }

        private void BackgroundCanvas_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            //var startAngle = DegreesToRadians(-15);
            //var sweepAngle = DegreesToRadians(30);

            //var progress = (float)Slide.Value / 100f;
            //var size = 1 + 4 * progress;

            //for (int i = 0; i < 4; i++)
            //{
            //    using (var builder = new CanvasPathBuilder(sender))
            //    {

            //        var centerPoint = new Vector2((1 + 4 * progress), 6);
            //        var startPoint = centerPoint + Vector2.Transform(Vector2.UnitX, Matrix3x2.CreateRotation(startAngle)) * size;

            //        builder.BeginFigure(startPoint);
            //        builder.AddArc(centerPoint, size, size, startAngle, sweepAngle);
            //        builder.EndFigure(CanvasFigureLoop.Open);

            //        using (var geometry = CanvasGeometry.CreatePath(builder))
            //        {
            //            var alpha = (i == 0) ? progress : (i == 4 - 1) ? (1.0f - progress) : 1.0f;
            //            args.DrawingSession.DrawGeometry(geometry, Color.FromArgb((byte)(alpha * 255), 0xFF, 0x00, 0x0), 2, new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round });
            //        }

            //        size += 4;
            //    }
            //}
        }

        public float DegreesToRadians(float angle)
        {
            return (float)((Math.PI / 180) * angle);
        }

        private void Slide_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            //BackgroundCanvas.Invalidate();
        }
    }

    public class Anubi : Windows.UI.Xaml.Shapes.Path
    {
        private bool _isUpdating;

        #region StartAngle
        /// <summary>
        /// The start angle property.
        /// </summary>
        public static readonly DependencyProperty StartAngleProperty =
            DependencyProperty.Register(
                "StartAngle",
                typeof(double),
                typeof(Anubi),
                new PropertyMetadata(
                    0d,
                    OnStartAngleChanged));

        /// <summary>
        /// Gets or sets the start angle.
        /// </summary>
        /// <value>
        /// The start angle.
        /// </value>
        public double StartAngle
        {
            get { return (double)GetValue(StartAngleProperty); }
            set { SetValue(StartAngleProperty, value); }
        }

        private static void OnStartAngleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = (Anubi)sender;
            var oldStartAngle = (double)e.OldValue;
            var newStartAngle = (double)e.NewValue;
            target.OnStartAngleChanged(oldStartAngle, newStartAngle);
        }

        private void OnStartAngleChanged(double oldStartAngle, double newStartAngle)
        {
            UpdatePath();
        }
        #endregion

        #region EndAngle
        /// <summary>
        /// The end angle property.
        /// </summary>
        public static readonly DependencyProperty EndAngleProperty =
            DependencyProperty.Register(
                "EndAngle",
                typeof(double),
                typeof(Anubi),
                new PropertyMetadata(
                    0d,
                    OnEndAngleChanged));

        /// <summary>
        /// Gets or sets the end angle.
        /// </summary>
        /// <value>
        /// The end angle.
        /// </value>
        public double EndAngle
        {
            get { return (double)GetValue(EndAngleProperty); }
            set { SetValue(EndAngleProperty, value); }
        }

        private static void OnEndAngleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = (Anubi)sender;
            var oldEndAngle = (double)e.OldValue;
            var newEndAngle = (double)e.NewValue;
            target.OnEndAngleChanged(oldEndAngle, newEndAngle);
        }

        private void OnEndAngleChanged(double oldEndAngle, double newEndAngle)
        {
            UpdatePath();
        }
        #endregion

        #region Radius
        /// <summary>
        /// The radius property
        /// </summary>
        public static readonly DependencyProperty RadiusProperty =
            DependencyProperty.Register(
                "Radius",
                typeof(double),
                typeof(Anubi),
                new PropertyMetadata(
                    0d,
                    OnRadiusChanged));

        /// <summary>
        /// Gets or sets the outer radius.
        /// </summary>
        /// <value>
        /// The outer radius.
        /// </value>
        public double Radius
        {
            get { return (double)GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        private static void OnRadiusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = (Anubi)sender;
            var oldRadius = (double)e.OldValue;
            var newRadius = (double)e.NewValue;
            target.OnRadiusChanged(oldRadius, newRadius);
        }

        private void OnRadiusChanged(double oldRadius, double newRadius)
        {
            Width = Height = 2 * Radius;
            UpdatePath();
        }
        #endregion

        #region InnerRadius
        /// <summary>
        /// The inner radius property
        /// </summary>
        public static readonly DependencyProperty InnerRadiusProperty =
            DependencyProperty.Register(
                "InnerRadius",
                typeof(double),
                typeof(Anubi),
                new PropertyMetadata(
                    0d,
                    OnInnerRadiusChanged));

        /// <summary>
        /// Gets or sets the inner radius.
        /// </summary>
        /// <value>
        /// The inner radius.
        /// </value>
        public double InnerRadius
        {
            get { return (double)GetValue(InnerRadiusProperty); }
            set { SetValue(InnerRadiusProperty, value); }
        }

        private static void OnInnerRadiusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var target = (Anubi)sender;
            var oldInnerRadius = (double)e.OldValue;
            var newInnerRadius = (double)e.NewValue;
            target.OnInnerRadiusChanged(oldInnerRadius, newInnerRadius);
        }

        private void OnInnerRadiusChanged(double oldInnerRadius, double newInnerRadius)
        {
            if (newInnerRadius < 0)
            {
                throw new ArgumentException("InnerRadius can't be a negative value.", "InnerRadius");
            }

            UpdatePath();
        }

        #endregion

        #region Center
        /// <summary>
        /// Center Dependency Property
        /// </summary>
        public static readonly DependencyProperty CenterProperty =
            DependencyProperty.Register(
                "Center",
                typeof(Point?),
                typeof(Anubi),
                new PropertyMetadata(null, OnCenterChanged));

        /// <summary>
        /// Gets or sets the Center property. This dependency property 
        /// indicates the center point.
        /// Center point is calculated based on Radius and StrokeThickness if not specified.    
        /// </summary>
        public Point? Center
        {
            get { return (Point?)GetValue(CenterProperty); }
            set { SetValue(CenterProperty, value); }
        }

        /// <summary>
        /// Handles changes to the Center property.
        /// </summary>
        /// <param name="d">
        /// The <see cref="DependencyObject"/> on which
        /// the property has changed value.
        /// </param>
        /// <param name="e">
        /// Event data that is issued by any event that
        /// tracks changes to the effective value of this property.
        /// </param>
        private static void OnCenterChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = (Anubi)d;
            Point? oldCenter = (Point?)e.OldValue;
            Point? newCenter = target.Center;
            target.OnCenterChanged(oldCenter, newCenter);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes
        /// to the Center property.
        /// </summary>
        /// <param name="oldCenter">The old Center value</param>
        /// <param name="newCenter">The new Center value</param>
        private void OnCenterChanged(
            Point? oldCenter, Point? newCenter)
        {
            UpdatePath();
        }
        #endregion

        #region Value



        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Value.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(Anubi), new PropertyMetadata(0.0, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = (Anubi)d;
            var oldInnerRadius = (double)e.OldValue;
            var newInnerRadius = (double)e.NewValue;
            target.OnValueChanged(oldInnerRadius, newInnerRadius);
        }

        private void OnValueChanged(double oldInnerRadius, double newInnerRadius)
        {
            if (newInnerRadius < 0)
            {
                throw new ArgumentException("InnerRadius can't be a negative value.", "InnerRadius");
            }

            UpdatePath();
        }


        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="Anubi" /> class.
        /// </summary>
        public Anubi()
        {
            SizeChanged += OnSizeChanged;
            //new PropertyChangeEventSource<double>(
            //    this, "StrokeThickness", BindingMode.OneWay).ValueChanged +=
            //    OnStrokeThicknessChanged;
        }

        private void OnStrokeThicknessChanged(object sender, double e)
        {
            UpdatePath();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            UpdatePath();
        }

        /// <summary>
        /// Suspends path updates until EndUpdate is called;
        /// </summary>
        public void BeginUpdate()
        {
            _isUpdating = true;
        }

        /// <summary>
        /// Resumes immediate path updates every time a component property value changes. Updates the path.
        /// </summary>
        public void EndUpdate()
        {
            _isUpdating = false;
            UpdatePath();
        }

        private TranslateTransform _transform;

        private void UpdatePath()
        {
            if (_transform == null)
            {
                _transform = new TranslateTransform();
                RenderTransform = _transform;
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            }

            var size = Radius + 1 + 4 * Value;
            var innerRadius = size - 2 + StrokeThickness / 2;
            var outerRadius = size - StrokeThickness / 2;

            //_transform.X = -size;
            //Width = Height = size * 2;
            //Margin = new Thickness(-size, 5 - size, 0, 5 - size);
            //Margin = new Thickness(-size, 0, 0, 0);

            if (_isUpdating ||
                ActualWidth == 0 ||
                innerRadius <= 0 ||
                outerRadius < innerRadius)
            {
                return;
            }

            var center =
                Center ??
                new Point(
                    outerRadius + StrokeThickness / 2,
                    outerRadius + StrokeThickness / 2);

            //center = new Point(Width / 2, Height / 2);

            if (EndAngle > 0 && EndAngle < 360)
            {
                var pathGeometry = new PathGeometry();
                var pathFigure = new PathFigure();
                pathFigure.IsClosed = false;

                // Starting Point
                pathFigure.StartPoint =
                    new Point(
                        center.X + Math.Sin(StartAngle * Math.PI / 180) * innerRadius,
                        center.Y - Math.Cos(StartAngle * Math.PI / 180) * innerRadius);

                // Inner Arc
                var innerArcSegment = new ArcSegment();
                innerArcSegment.IsLargeArc = (EndAngle - StartAngle) >= 180.0;
                innerArcSegment.Point =
                    new Point(
                        center.X + Math.Sin(EndAngle * Math.PI / 180) * innerRadius,
                        center.Y - Math.Cos(EndAngle * Math.PI / 180) * innerRadius);
                innerArcSegment.Size = new Size(innerRadius, innerRadius);
                innerArcSegment.SweepDirection = SweepDirection.Clockwise;

                pathFigure.Segments.Add(innerArcSegment);
                pathGeometry.Figures.Add(pathFigure);

                InvalidateArrange();
                Data = pathGeometry;
            }
        }

        //protected override Size MeasureOverride(Size availableSize)
        //{
        //    var size = (Radius + 1 + 4 * Value) * 2;
        //    return new Size(size, size);
        //}
    }

    public class VoiceButton : GlyphButton
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private OpusRecorder _recorder;
        private StorageFile _file;
        private bool _cancelOnRelease;
        private bool _isPressed;
        private DateTime _start;

        public VoiceButton()
        {
            DefaultStyleKey = typeof(VoiceButton);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            Start();

            CapturePointer(e.Pointer);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            Stop();

            ReleasePointerCapture(e.Pointer);
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);

            if (_isPressed)
            {
                _cancelOnRelease = false;
            }
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);

            if (_isPressed)
            {
                _cancelOnRelease = true;
            }
        }

        private async void Start()
        {
            if (_recorder?.IsRecording == true)
            {
                await _recorder.StopAsync();
            }

            _file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("recording.ogg", CreationCollisionOption.ReplaceExisting);
            _recorder = new OpusRecorder(_file);
            await _recorder.StartAsync();

            _isPressed = true;
            _cancelOnRelease = false;
            _start = DateTime.Now;
        }

        private async void Stop()
        {
            if (_recorder?.IsRecording == true)
            {
                await _recorder.StopAsync();
            }

            if (_cancelOnRelease)
            {
                await _file.DeleteAsync();
            }
            else
            {
                await ViewModel.SendAudioAsync(_file, (int)(DateTime.Now - _start).TotalSeconds, true, null, null, null);
            }
        }
    }

    internal sealed class OpusRecorder
    {
        #region fields

        private StorageFile m_file;
        private IMediaExtension m_opusSink;
        private MediaCapture m_mediaCapture;

        #endregion

        #region properties

        public StorageFile File
        {
            get { return m_file; }
        }

        public bool IsRecording
        {
            get { return m_mediaCapture != null; }
        }

        #endregion

        #region constructors

        public OpusRecorder(StorageFile file)
        {
            m_file = file;
        }

        #endregion

        #region methods

        public async Task StartAsync()
        {
            if (m_mediaCapture != null)
                throw new InvalidOperationException("Cannot start while recording");

            m_mediaCapture = new MediaCapture();
            await m_mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings()
            {
                MediaCategory = MediaCategory.Speech,
                AudioProcessing = AudioProcessing.Default,
                MemoryPreference = MediaCaptureMemoryPreference.Auto,
                SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                StreamingCaptureMode = StreamingCaptureMode.Audio,
            });

            m_opusSink = await OpusCodec.CreateMediaSinkAsync(m_file);

            var wawEncodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
            wawEncodingProfile.Audio.BitsPerSample = 16;
            wawEncodingProfile.Audio.SampleRate = 48000;
            wawEncodingProfile.Audio.ChannelCount = 1;
            await m_mediaCapture.StartRecordToCustomSinkAsync(wawEncodingProfile, m_opusSink);
        }

        public async Task StopAsync()
        {
            if (m_mediaCapture == null)
                throw new InvalidOperationException("Cannot stop while not recording");

            await m_mediaCapture.StopRecordAsync();

            m_mediaCapture.Dispose();
            m_mediaCapture = null;

            ((IDisposable)m_opusSink).Dispose();
            m_opusSink = null;
        }

        #endregion
    }
}
