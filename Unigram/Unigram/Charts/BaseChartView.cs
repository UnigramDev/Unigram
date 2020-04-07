using Unigram.Charts.Data;
using Unigram.Charts.DataView;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Unigram.Common;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using System.Threading;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Charts
{
    public abstract class BaseChartView : Grid
    {
        public abstract void SetDataPublic(ChartData data);

        public abstract List<LineViewData> GetLines();

        protected readonly List<Animator> _animators = new List<Animator>();
        protected readonly object _animatorsLock = new object();

        public void change(Animator animator, bool enable)
        {
            lock (_animatorsLock)
            {
                if (enable)
                {
                    _animators.Add(animator);
                }
                else
                {
                    _animators.Remove(animator);
                }

                change(_animators.Count < 1);
            }
        }

        protected abstract void change(bool pause);

        public static FastOutSlowInInterpolator INTERPOLATOR = new FastOutSlowInInterpolator();

        public abstract void onCheckChanged();
    }

    public abstract class BaseChartView<T, L> : BaseChartView, ChartPickerDelegate.Listener where T : ChartData where L : LineViewData
    {

        public SharedUiComponents sharedUiComponents;
        protected List<ChartHorizontalLinesData> horizontalLines = new List<ChartHorizontalLinesData>(10);
        List<ChartBottomSignatureData> bottomSignatureDate = new List<ChartBottomSignatureData>(25);

        public List<L> lines = new List<L>();

        private const int ANIM_DURATION = 400;
        public const int HORIZONTAL_PADDING = 16;
        private const float LINE_WIDTH = 1;
        private const float SELECTED_LINE_WIDTH = 1.5f;
        private const float SIGNATURE_TEXT_SIZE = 12;
        public const int SIGNATURE_TEXT_HEIGHT = 18;
        private const int BOTTOM_SIGNATURE_TEXT_HEIGHT = 14;
        public const int BOTTOM_SIGNATURE_START_ALPHA = 10;
        protected const int PICKER_PADDING = 16;
        private const int PICKER_CAPTURE_WIDTH = 24;
        private const int LANDSCAPE_END_PADDING = 16;
        private const int BOTTOM_SIGNATURE_OFFSET = 10;

        private const int DP_12 = 12;
        private const int DP_6 = 6;
        private const int DP_5 = 5;
        private const int DP_2 = 2;
        private const int DP_1 = 1;

        protected bool drawPointOnSelection = true;
        protected float signaturePaintAlpha;
        float bottomSignaturePaintAlpha;
        int hintLinePaintAlpha;
        protected int chartActiveLineAlpha;

        //public const bool USE_LINES = android.os.Build.VERSION.SDK_INT < Build.VERSION_CODES.P;
        public const bool USE_LINES = false;
        //protected const bool ANIMATE_PICKER_SIZES = android.os.Build.VERSION.SDK_INT > Build.VERSION_CODES.LOLLIPOP;
        protected const bool ANIMATE_PICKER_SIZES = true;

        protected int chartBottom;
        public float currentMaxHeight = 250;
        public float currentMinHeight = 0;

        float animateToMaxHeight = 0;
        float animateToMinHeight = 0;


        float thresholdMaxHeight = 0;

        protected int startXIndex;
        protected int endXIndex;
        protected bool invalidatePickerChart = true;

        bool landscape = false;

        public bool enabled = true;


        Color emptyPaint;

        protected Paint linePaint = new Paint();
        protected Paint selectedLinePaint = new Paint();
        protected Paint signaturePaint = new Paint();
        protected Paint signaturePaint2 = new Paint();
        Paint bottomSignaturePaint = new Paint();
        Paint pickerSelectorPaint = new Paint();
        Paint unactiveBottomChartPaint = new Paint();
        protected Paint selectionBackgroundPaint = new Paint();
        Paint ripplePaint = new Paint();
        Paint whiteLinePaint = new Paint();

        Rect pickerRect = new Rect();
        CanvasPathBuilder pathTmp;

        Animator maxValueAnimator;

        ValueAnimator alphaAnimator;
        ValueAnimator alphaBottomAnimator;
        protected Animator pickerAnimator;
        ValueAnimator selectionAnimator;
        protected bool postTransition = false;

        public ChartPickerDelegate pickerDelegate;
        protected T chartData;

        ChartBottomSignatureData currentBottomSignatures;
        protected float pickerMaxHeight;
        protected float pickerMinHeight;
        protected float animatedToPickerMaxHeight;
        protected float animatedToPickerMinHeight;
        //protected int tmpN;
        //protected int tmpI;
        protected int bottomSignatureOffset;

        //private Bitmap bottomChartBitmap;
        private CanvasRenderTarget bottomChartCanvas;

        protected bool chartCaptured = false;
        protected int selectedIndex = -1;
        protected float selectedCoordinate = -1;

        public LegendSignatureView legendSignatureView;
        public bool legendShowing = false;

        public float selectionA = 0f;

        protected bool superDraw = false;
        protected bool useAlphaSignature = false;

        public int transitionMode = TRANSITION_MODE_NONE;
        public TransitionParams transitionParams;

        public const int TRANSITION_MODE_CHILD = 1;
        public const int TRANSITION_MODE_PARENT = 2;
        public const int TRANSITION_MODE_ALPHA_ENTER = 3;
        public const int TRANSITION_MODE_NONE = 0;

        private int touchSlop;

        public int pikerHeight = 46;
        public int pickerWidth;
        public int chartStart;
        public int chartEnd;
        public int chartWidth;
        public float chartFullWidth;
        public Rect chartArea = new Rect();

        private AnimatorUpdateListener pickerHeightUpdateListener;
        private AnimatorUpdateListener pickerMinHeightUpdateListener;
        private AnimatorUpdateListener heightUpdateListener;
        private AnimatorUpdateListener minHeightUpdateListener;
        private AnimatorUpdateListener selectionAnimatorListener;
        private AnimatorUpdateListener selectorAnimatorEndListener;

        private void initListeners()
        {
            pickerHeightUpdateListener = new AnimatorUpdateListener(
            animation =>
            {
                pickerMaxHeight = (float)animation.getAnimatedValue();
                invalidatePickerChart = true;
                invalidate();
            });

            pickerMinHeightUpdateListener = new AnimatorUpdateListener(
                animation =>
                {
                    pickerMinHeight = (float)animation.getAnimatedValue();
                    invalidatePickerChart = true;
                    invalidate();
                });

            heightUpdateListener = new AnimatorUpdateListener(
                animation =>
                {
                    currentMaxHeight = ((float)animation.getAnimatedValue());
                    invalidate();
                });

            minHeightUpdateListener = new AnimatorUpdateListener(
                animation =>
                {
                    currentMinHeight = ((float)animation.getAnimatedValue());
                    invalidate();
                });

            selectionAnimatorListener = new AnimatorUpdateListener(
                animation =>
                {
                    selectionA = (float)animation.getAnimatedValue();
                    //legendSignatureView.setAlpha(selectionA);
                    invalidate();
                });

            selectorAnimatorEndListener = new AnimatorUpdateListener(
                end: animation =>
                {
                    if (!animateLegentTo)
                    {
                        legendShowing = false;
                        legendSignatureView.setVisibility(Visibility.Collapsed);
                        invalidate();
                    }

                    postTransition = false;
                });
        }

        protected bool useMinHeight = false;
        protected DateSelectionListener dateSelectionListener;
        private float startFromMax;
        private float startFromMin;
        private float startFromMaxH;
        private float startFromMinH;
        private float minMaxUpdateStep;

        public BaseChartView()
        {
            init();
            initListeners();
            //touchSlop = ViewConfiguration.get(context).getScaledTouchSlop();
        }

        public void invalidate()
        {
                canvas.Invalidate();
        }

        protected override void change(bool pause)
        {
            //if (pause)
            //{
            //    timer.Change(Timeout.Infinite, Timeout.Infinite);
            //}
            //else
            //{
            //    timer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / 60));
            //}
        }

        protected int getMeasuredHeight()
        {
            return (int)canvas.Size.Height;
            return (int)ActualHeight;
        }

        protected int getMeasuredWidth()
        {
            return (int)canvas.Size.Width;
            return (int)ActualWidth;
        }

        private CanvasControl canvas;
        private Timer timer;

        //protected override bool paused { get => canvas.Paused; set => canvas.Paused = value; }

        protected virtual void init()
        {
            pickerDelegate = new ChartPickerDelegate(this);

            canvas = new CanvasControl();
            canvas.Draw += (s, args) =>
            {
                if (s.Size.IsEmpty)
                {
                    return;
                }

                //lock (_animatorsLock)
                //{
                //    foreach (var animator in _animators.ToArray())
                //    {
                //        animator.onTick();
                //    }

                //    paused = _animators.Count < 1;
                //}

                onDraw(args.DrawingSession);
            };
            canvas.SizeChanged += (s, args) =>
            {
                onMeasure(0, 0);
            };

            timer = new Timer(new TimerCallback(state =>
            {
                lock (_animatorsLock)
                {
                    foreach (var animator in _animators.ToArray())
                    {
                        animator.tick();
                    }

                    change(_animators.Count < 1);
                }
            }), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1000 / 60));

            canvas.Background = new Windows.UI.Xaml.Media.SolidColorBrush() { Color = Windows.UI.Colors.Transparent };
            canvas.PointerPressed += OnPointerPressed;
            canvas.PointerMoved += OnPointerMoved;
            canvas.PointerReleased += OnPointerReleased;
            //canvas.PointerCanceled += OnPointerReleased;
            //canvas.PointerCaptureLost += OnPointerReleased;

            Children.Add(canvas);

            linePaint.StrokeWidth = LINE_WIDTH;
            selectedLinePaint.StrokeWidth = SELECTED_LINE_WIDTH;

            signaturePaint.TextSize = SIGNATURE_TEXT_SIZE;
            signaturePaint2.TextSize = SIGNATURE_TEXT_SIZE;
            signaturePaint2.TextAlignment = CanvasHorizontalAlignment.Right;
            bottomSignaturePaint.TextSize = SIGNATURE_TEXT_SIZE;
            bottomSignaturePaint.TextAlignment = CanvasHorizontalAlignment.Center;

            selectionBackgroundPaint.StrokeWidth = 6;
            selectionBackgroundPaint.StrokeCap = CanvasCapStyle.Round;

            //setLayerType(LAYER_TYPE_HARDWARE, null);
            //setWillNotDraw(false);

            legendSignatureView = createLegendView();


            legendSignatureView.setVisibility(Visibility.Collapsed);

            whiteLinePaint.Color = Colors.White;
            whiteLinePaint.StrokeWidth = 3;
            whiteLinePaint.StrokeCap = CanvasCapStyle.Round;

            updateColors();
        }

        protected virtual LegendSignatureView createLegendView()
        {
            return new LegendSignatureView();
        }

        protected static Dictionary<string, Color> _colors = new Dictionary<string, Color>
        {
            { "key_statisticChartSignatureAlpha", Color.FromArgb(0x7f, 0x25, 0x25, 0x29) },
            { "key_statisticChartSignature", Color.FromArgb(0x7f, 0x25, 0x25, 0x29) },
            { "key_statisticChartHintLine", Color.FromArgb(0x1a, 0x18, 0x2D, 0x3B) },
            { "key_statisticChartActiveLine", Color.FromArgb(0x33, 0x00, 0x00, 0x00) },
            { "key_statisticChartActivePickerChart", Color.FromArgb(0xd8, 0xba, 0xcc, 0xd9) },
            { "key_statisticChartInactivePickerChart", Color.FromArgb(0x99, 0xe2, 0xee, 0xf9) },
            { "key_windowBackgroundWhite", Colors.White },
            { "key_statisticChartRipple", Color.FromArgb(0x2c, 0x7e, 0x9d, 0xb7) },
        };

        public void updateColors()
        {
            if (useAlphaSignature)
            {
                signaturePaint.Color = _colors["key_statisticChartSignatureAlpha"];
            }
            else
            {
                signaturePaint.Color = _colors["key_statisticChartSignature"];
            }

            bottomSignaturePaint.Color = _colors["key_statisticChartSignature"];
            linePaint.Color = _colors["key_statisticChartHintLine"];
            selectedLinePaint.Color = _colors["key_statisticChartActiveLine"];
            pickerSelectorPaint.Color = _colors["key_statisticChartActivePickerChart"];
            unactiveBottomChartPaint.Color = _colors["key_statisticChartInactivePickerChart"];
            selectionBackgroundPaint.Color = _colors["key_windowBackgroundWhite"];
            ripplePaint.Color = _colors["key_statisticChartRipple"];
            legendSignatureView.recolor();

            hintLinePaintAlpha = linePaint.A;
            chartActiveLineAlpha = selectedLinePaint.A;
            signaturePaintAlpha = signaturePaint.A / 255f;
            bottomSignaturePaintAlpha = bottomSignaturePaint.A / 255f;


            foreach (LineViewData l in lines)
            {
                l.updateColors();
            }

            if (legendShowing && selectedIndex < chartData.x.Length)
            {
                legendSignatureView.setData(selectedIndex, chartData.x[selectedIndex], lines.Cast<LineViewData>().ToList(), false);
            }

            invalidatePickerChart = true;
        }

        int lastW = 0;
        int lastH = 0;

        //@Override
        protected virtual void onMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            //super.onMeasure(widthMeasureSpec, heightMeasureSpec);
            //if (!landscape)
            //{
            //    setMeasuredDimension(
            //            MeasureSpec.getSize(widthMeasureSpec),
            //            MeasureSpec.getSize(widthMeasureSpec)
            //    );
            //}
            //else
            //{
            //    setMeasuredDimension(
            //            MeasureSpec.getSize(widthMeasureSpec),
            //            AndroidUtilities.displaySize.y - AndroidUtilities.dp(56)
            //    );
            //}


            if (getMeasuredWidth() != lastW || getMeasuredHeight() != lastH)
            {
                lastW = getMeasuredWidth();
                lastH = getMeasuredHeight();
                //bottomChartBitmap = Bitmap.createBitmap(getMeasuredWidth() - (HORIZONTAL_PADDING << 1), pikerHeight, Bitmap.Config.ARGB_4444);
                //bottomChartCanvas = new Canvas(bottomChartBitmap);
                bottomChartCanvas = null;

                //sharedUiComponents.getPickerMaskBitmap(pikerHeight, getMeasuredWidth() - HORIZONTAL_PADDING * 2);
                measureSizes();

                if (legendShowing)
                    moveLegend(chartFullWidth * (pickerDelegate.pickerStart) - HORIZONTAL_PADDING);

                onPickerDataChanged(false, true, false);
            }
        }

        //@override
        private void measureSizes()
        {
            if (getMeasuredHeight() <= 0 || getMeasuredWidth() <= 0)
            {
                return;
            }
            pickerWidth = getMeasuredWidth() - (HORIZONTAL_PADDING * 2);
            chartStart = HORIZONTAL_PADDING;
            chartEnd = getMeasuredWidth() - (landscape ? LANDSCAPE_END_PADDING : HORIZONTAL_PADDING);
            chartWidth = chartEnd - chartStart;
            chartFullWidth = (chartWidth / (pickerDelegate.pickerEnd - pickerDelegate.pickerStart));

            updateLineSignature();
            chartBottom = 100;
            chartArea = createRect(chartStart - HORIZONTAL_PADDING, 0, chartEnd + HORIZONTAL_PADDING, getMeasuredHeight() - chartBottom);

            if (chartData != null)
            {
                bottomSignatureOffset = (int)(20 / ((float)pickerWidth / chartData.x.Length));
            }
            measureHeightThreshold();
        }

        private void measureHeightThreshold()
        {
            int chartHeight = getMeasuredHeight() - chartBottom;
            if (animateToMaxHeight == 0 || chartHeight == 0) return;
            thresholdMaxHeight = ((float)animateToMaxHeight / chartHeight) * SIGNATURE_TEXT_SIZE;
        }


        protected virtual void drawPickerChart(CanvasDrawingSession canvas)
        {

        }


        //@Override
        protected virtual void onDraw(CanvasDrawingSession canvas)
        {
            if (superDraw)
            {
                //super.onDraw(canvas);
                return;
            }
            tick();
            //int count = canvas.save();
            //canvas.clipRect(0, chartArea.Top, getMeasuredWidth(), chartArea.Bottom);
            var clip = canvas.CreateLayer(1, createRect(0, chartArea.Top, getMeasuredWidth(), chartArea.Bottom));

            drawBottomLine(canvas);
            int tmpN = horizontalLines.Count;
            for (int tmpI = 0; tmpI < tmpN; tmpI++)
            {
                drawHorizontalLines(canvas, horizontalLines[tmpI]);
            }

            drawChart(canvas);

            tmpN = horizontalLines.Count;
            for (int tmpI = 0; tmpI < tmpN; tmpI++)
            {
                drawSignaturesToHorizontalLines(canvas, horizontalLines[tmpI]);
            }

            //canvas.restoreToCount(count);
            clip.Dispose();
            drawBottomSignature(canvas);

            drawPicker(canvas);
            drawSelection(canvas);

            //super.onDraw(canvas);
        }

        protected void tick()
        {
            if (minMaxUpdateStep == 0)
            {
                return;
            }
            if (currentMaxHeight != animateToMaxHeight)
            {
                startFromMax += minMaxUpdateStep;
                if (startFromMax > 1)
                {
                    startFromMax = 1;
                    currentMaxHeight = animateToMaxHeight;
                }
                else
                {
                    currentMaxHeight = startFromMaxH + (animateToMaxHeight - startFromMaxH) * CubicBezierInterpolator.EASE_OUT.getInterpolation(startFromMax);
                }
                invalidate();
            }
            if (useMinHeight)
            {
                if (currentMinHeight != animateToMinHeight)
                {
                    startFromMin += minMaxUpdateStep;
                    if (startFromMin > 1)
                    {
                        startFromMin = 1;
                        currentMinHeight = animateToMinHeight;
                    }
                    else
                    {
                        currentMinHeight = startFromMinH + (animateToMinHeight - startFromMinH) * CubicBezierInterpolator.EASE_OUT.getInterpolation(startFromMin);
                    }
                    invalidate();
                }
            }
        }

        protected virtual void drawBottomSignature(CanvasDrawingSession canvas)
        {
            if (chartData == null) return;

            int tmpN = bottomSignatureDate.Count;

            float transitionAlpha = 1f;
            if (transitionMode == TRANSITION_MODE_PARENT)
            {
                transitionAlpha = 1f - transitionParams.progress;
            }
            else if (transitionMode == TRANSITION_MODE_CHILD)
            {
                transitionAlpha = transitionParams.progress;
            }
            else if (transitionMode == TRANSITION_MODE_ALPHA_ENTER)
            {
                transitionAlpha = transitionParams.progress;
            }

            for (int tmpI = 0; tmpI < tmpN; tmpI++)
            {
                int resultAlpha = bottomSignatureDate[tmpI].alpha;
                int step = bottomSignatureDate[tmpI].step;
                if (step == 0) step = 1;

                int start = startXIndex - bottomSignatureOffset;
                while (start % step != 0)
                {
                    start--;
                }

                int end = endXIndex - bottomSignatureOffset;
                while (end % step != 0 || end < chartData.x.Length - 1)
                {
                    end++;
                }

                start += bottomSignatureOffset;
                end += bottomSignatureOffset;


                float offset = chartFullWidth * (pickerDelegate.pickerStart) - HORIZONTAL_PADDING;
                float fullWidth = chartFullWidth;
                for (int i = start; i < end; i += step)
                {
                    if (i < 0 || i >= chartData.x.Length - 1) continue;
                    float xPercentage = (float)(chartData.x[i] - chartData.x[0]) /
                            (float)((chartData.x[chartData.x.Length - 1] - chartData.x[0]));
                    float xPoint = xPercentage * fullWidth - offset;
                    float xPointOffset = xPoint - BOTTOM_SIGNATURE_OFFSET;
                    if (xPointOffset > 0 &&
                            xPointOffset <= chartWidth + HORIZONTAL_PADDING)
                    {
                        if (xPointOffset < BOTTOM_SIGNATURE_START_ALPHA)
                        {
                            float a = 1f - (BOTTOM_SIGNATURE_START_ALPHA - xPointOffset) / BOTTOM_SIGNATURE_START_ALPHA;
                            bottomSignaturePaint.A = (byte)(resultAlpha * a * bottomSignaturePaintAlpha * transitionAlpha);
                        }
                        else if (xPointOffset > chartWidth)
                        {
                            float a = 1f - (xPointOffset - chartWidth) / HORIZONTAL_PADDING;
                            bottomSignaturePaint.A = (byte)(resultAlpha * a * bottomSignaturePaintAlpha * transitionAlpha);
                        }
                        else
                        {
                            bottomSignaturePaint.A = (byte)(resultAlpha * bottomSignaturePaintAlpha * transitionAlpha);
                        }
                        //canvas.DrawText(chartData.getDayString(i), xPoint, getMeasuredHeight() - chartBottom + BOTTOM_SIGNATURE_TEXT_HEIGHT + 3, bottomSignaturePaint);
                        canvas.DrawText(chartData.getDayString(i), xPoint, getMeasuredHeight() - chartBottom + 3, bottomSignaturePaint);
                    }
                }
            }
        }

        protected virtual void drawBottomLine(CanvasDrawingSession canvas)
        {
            if (chartData == null)
            {
                return;
            }
            float transitionAlpha = 1f;
            if (transitionMode == TRANSITION_MODE_PARENT)
            {
                transitionAlpha = 1f - transitionParams.progress;
            }
            else if (transitionMode == TRANSITION_MODE_CHILD)
            {
                transitionAlpha = transitionParams.progress;
            }
            else if (transitionMode == TRANSITION_MODE_ALPHA_ENTER)
            {
                transitionAlpha = transitionParams.progress;
            }

            linePaint.A = (byte)(hintLinePaintAlpha * transitionAlpha);
            signaturePaint.A = (byte)(255 * signaturePaintAlpha * transitionAlpha);

            var format = new CanvasTextFormat { FontSize = signaturePaint.TextSize ?? 0 };
            var layout = new CanvasTextLayout(canvas, "0", format, 0, 0);

            int textOffset = (int)(4 + layout.DrawBounds.Bottom);
            //int textOffset = (int)(SIGNATURE_TEXT_HEIGHT - signaturePaintFormat.FontSize);
            format.Dispose();
            layout.Dispose();

            int y = (getMeasuredHeight() - chartBottom - 1);
            canvas.DrawLine(
                    chartStart,
                    y,
                    chartEnd,
                    y,
                    linePaint);
            if (useMinHeight) return;

            canvas.DrawText("0", HORIZONTAL_PADDING, y - textOffset, signaturePaint);
        }

        protected virtual void drawSelection(CanvasDrawingSession canvas)
        {
            if (selectedIndex < 0 || !legendShowing || chartData == null) return;

            byte alpha = (byte)(chartActiveLineAlpha * selectionA);


            float fullWidth = (chartWidth / (pickerDelegate.pickerEnd - pickerDelegate.pickerStart));
            float offset = fullWidth * (pickerDelegate.pickerStart) - HORIZONTAL_PADDING;

            float xPoint;
            if (selectedIndex < chartData.xPercentage.Length)
            {
                xPoint = chartData.xPercentage[selectedIndex] * fullWidth - offset;
            }
            else
            {
                return;
            }

            selectedLinePaint.A = alpha;
            canvas.DrawLine(xPoint, 0, xPoint, (float)chartArea.Bottom, selectedLinePaint);

            if (drawPointOnSelection)
            {
                int tmpN = lines.Count;
                for (int tmpI = 0; tmpI < tmpN; tmpI++)
                {
                    LineViewData line = lines[tmpI];
                    if (!line.enabled && line.alpha == 0) continue;
                    float yPercentage = (line.line.y[selectedIndex] - currentMinHeight) / (currentMaxHeight - currentMinHeight);
                    float yPoint = getMeasuredHeight() - chartBottom - (yPercentage) * (getMeasuredHeight() - chartBottom - SIGNATURE_TEXT_HEIGHT);

                    line.selectionPaint.A = (byte)(255 * line.alpha * selectionA);
                    selectionBackgroundPaint.A = (byte)(255 * line.alpha * selectionA);

                    //canvas.DrawPoint(xPoint, yPoint, line.selectionPaint);
                    //canvas.DrawPoint(xPoint, yPoint, selectionBackgroundPaint);
                    canvas.FillCircle(xPoint, yPoint, line.selectionPaint);
                    canvas.FillCircle(xPoint, yPoint, selectionBackgroundPaint);
                }
            }
        }

        protected virtual void drawChart(CanvasDrawingSession canvas)
        {
        }

        protected virtual void drawHorizontalLines(CanvasDrawingSession canvas, ChartHorizontalLinesData a)
        {
            int n = a.values.Length;

            float additionalOutAlpha = 1f;
            if (n > 2)
            {
                float v = (a.values[1] - a.values[0]) / (float)(currentMaxHeight - currentMinHeight);
                if (v < 0.1)
                {
                    additionalOutAlpha = v / 0.1f;
                }
            }

            float transitionAlpha = 1f;
            if (transitionMode == TRANSITION_MODE_PARENT)
            {
                transitionAlpha = 1f - transitionParams.progress;
            }
            else if (transitionMode == TRANSITION_MODE_CHILD)
            {
                transitionAlpha = transitionParams.progress;
            }
            else if (transitionMode == TRANSITION_MODE_ALPHA_ENTER)
            {
                transitionAlpha = transitionParams.progress;
            }
            linePaint.A = (byte)(a.alpha * (hintLinePaintAlpha / 255f) * transitionAlpha * additionalOutAlpha);
            signaturePaint.A = (byte)(a.alpha * signaturePaintAlpha * transitionAlpha * additionalOutAlpha);
            int chartHeight = getMeasuredHeight() - chartBottom - SIGNATURE_TEXT_HEIGHT;
            for (int i = useMinHeight ? 0 : 1; i < n; i++)
            {
                int y = (int)((getMeasuredHeight() - chartBottom) - chartHeight * ((a.values[i] - currentMinHeight) / (currentMaxHeight - currentMinHeight)));
                //canvas.DrawRectangle(chartStart, y, chartEnd - chartStart, 2, linePaint, LINE_WIDTH);
                canvas.DrawLine(chartStart, y, chartEnd, y, linePaint);
            }
        }

        protected virtual void drawSignaturesToHorizontalLines(CanvasDrawingSession canvas, ChartHorizontalLinesData a)
        {
            int n = a.values.Length;

            float additionalOutAlpha = 1f;
            if (n > 2)
            {
                float v = (a.values[1] - a.values[0]) / (float)(currentMaxHeight - currentMinHeight);
                if (v < 0.1)
                {
                    additionalOutAlpha = v / 0.1f;
                }
            }

            float transitionAlpha = 1f;
            if (transitionMode == TRANSITION_MODE_PARENT)
            {
                transitionAlpha = 1f - transitionParams.progress;
            }
            else if (transitionMode == TRANSITION_MODE_CHILD)
            {
                transitionAlpha = transitionParams.progress;
            }
            else if (transitionMode == TRANSITION_MODE_ALPHA_ENTER)
            {
                transitionAlpha = transitionParams.progress;
            }
            linePaint.A = (byte)(a.alpha * (hintLinePaintAlpha / 255f) * transitionAlpha * additionalOutAlpha);
            signaturePaint.A = (byte)(a.alpha * signaturePaintAlpha * transitionAlpha * additionalOutAlpha);
            int chartHeight = getMeasuredHeight() - chartBottom - SIGNATURE_TEXT_HEIGHT;

            var format = new CanvasTextFormat { FontSize = signaturePaint.TextSize ?? 0 };
            var layout = new CanvasTextLayout(canvas, a.valuesStr[1], format, 0, 0);

            int textOffset = (int)(4 + layout.DrawBounds.Bottom);
            //int textOffset = (int)(SIGNATURE_TEXT_HEIGHT - signaturePaintFormat.FontSize);
            format.Dispose();
            layout.Dispose();
            for (int i = useMinHeight ? 0 : 1; i < n; i++)
            {
                int y = (int)((getMeasuredHeight() - chartBottom) - chartHeight * ((a.values[i] - currentMinHeight) / (currentMaxHeight - currentMinHeight)));
                canvas.DrawText(a.valuesStr[i], HORIZONTAL_PADDING, y - textOffset, signaturePaint);
            }
        }

        protected void drawPicker(CanvasDrawingSession canvas)
        {
            if (chartData == null)
            {
                return;
            }
            pickerDelegate.pickerWidth = pickerWidth;
            int bottom = getMeasuredHeight() - PICKER_PADDING;
            int top = getMeasuredHeight() - pikerHeight - PICKER_PADDING;

            int start = (int)(HORIZONTAL_PADDING + pickerWidth * pickerDelegate.pickerStart);
            int end = (int)(HORIZONTAL_PADDING + pickerWidth * pickerDelegate.pickerEnd);

            float transitionAlpha = 1f;
            if (transitionMode == TRANSITION_MODE_CHILD)
            {
                int startParent = (int)(HORIZONTAL_PADDING + pickerWidth * transitionParams.pickerStartOut);
                int endParent = (int)(HORIZONTAL_PADDING + pickerWidth * transitionParams.pickerEndOut);

                start += (int)((startParent - start) * (1f - transitionParams.progress));
                end += (int)((endParent - end) * (1f - transitionParams.progress));
            }
            else if (transitionMode == TRANSITION_MODE_ALPHA_ENTER)
            {
                transitionAlpha = transitionParams.progress;
            }

            if (chartData != null)
            {
                bool instantDraw = false;
                if (transitionMode == TRANSITION_MODE_NONE)
                {
                    for (int i = 0; i < lines.Count; i++)
                    {
                        L l = lines[i];
                        if ((l.animatorIn != null && l.animatorIn.isRunning()) || (l.animatorOut != null && l.animatorOut.isRunning()))
                        {
                            instantDraw = true;
                            break;
                        }
                    }
                }
                if (instantDraw)
                {
                    //canvas.save();
                    //canvas.clipRect(
                    //        HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING - pikerHeight,
                    //        getMeasuredWidth() - HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING
                    //);
                    var clip = canvas.CreateLayer(1, createRect(
                            HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING - pikerHeight,
                            getMeasuredWidth() - HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING
                        ));
                    //canvas.translate(HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING - pikerHeight);
                    canvas.Transform = Matrix3x2.CreateTranslation(HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING - pikerHeight);
                    drawPickerChart(canvas);
                    clip.Dispose();
                    canvas.Transform = Matrix3x2.Identity;
                    //canvas.restore();
                }
                else if (invalidatePickerChart || bottomChartCanvas == null)
                {
                    //bottomChartBitmap.eraseColor(0);
                    //drawPickerChart(bottomChartCanvas);
                    bottomChartCanvas = new CanvasRenderTarget(canvas, getMeasuredWidth() - (HORIZONTAL_PADDING << 1), pikerHeight);
                    using (var session = bottomChartCanvas.CreateDrawingSession())
                    {
                        drawPickerChart(session);
                    }
                    invalidatePickerChart = false;
                }
                if (!instantDraw)
                {
                    if (transitionMode == TRANSITION_MODE_PARENT)
                    {

                        float pY = top + (bottom - top) >> 1;
                        float pX = HORIZONTAL_PADDING + pickerWidth * transitionParams.xPercentage;

                        emptyPaint.A = (byte)((1f - transitionParams.progress) * 255);

                        //canvas.save();
                        //canvas.clipRect(HORIZONTAL_PADDING, top, getMeasuredWidth() - HORIZONTAL_PADDING, bottom);
                        var clip = canvas.CreateLayer(1, createRect(HORIZONTAL_PADDING, top, getMeasuredWidth() - HORIZONTAL_PADDING, bottom));
                        //canvas.scale(1 + 2 * transitionParams.progress, 1f, pX, pY);
                        canvas.Transform = Matrix3x2.CreateScale(new Vector2(1 + 2 * transitionParams.progress, 1f), new Vector2(pX, pY));
                        //canvas.drawBitmap(bottomChartBitmap, HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING - pikerHeight, emptyPaint);
                        canvas.DrawImage(bottomChartCanvas, HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING - pikerHeight);
                        //canvas.restore();
                        clip.Dispose();
                        canvas.Transform = Matrix3x2.Identity;


                    }
                    else if (transitionMode == TRANSITION_MODE_CHILD)
                    {
                        float pY = top + (bottom - top) >> 1;
                        float pX = HORIZONTAL_PADDING + pickerWidth * transitionParams.xPercentage;

                        float dX = (transitionParams.xPercentage > 0.5f ? pickerWidth * transitionParams.xPercentage : pickerWidth * (1f - transitionParams.xPercentage)) * transitionParams.progress;

                        //canvas.save();
                        //canvas.clipRect(pX - dX, top, pX + dX, bottom);
                        var clip = canvas.CreateLayer(1, createRect(pX - dX, top, pX + dX, bottom));

                        emptyPaint.A = (byte)(transitionParams.progress * 255);
                        //canvas.scale(transitionParams.progress, 1f, pX, pY);
                        canvas.Transform = Matrix3x2.CreateScale(new Vector2(transitionParams.progress, 1f), new Vector2(pX, pY));
                        //canvas.drawBitmap(bottomChartBitmap, HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING - pikerHeight, emptyPaint);
                        canvas.DrawImage(bottomChartCanvas, HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING - pikerHeight);
                        //canvas.restore();
                        clip.Dispose();
                        canvas.Transform = Matrix3x2.Identity;

                    }
                    else
                    {
                        emptyPaint.A = (byte)((transitionAlpha) * 255);
                        //canvas.drawBitmap(bottomChartBitmap, HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING - pikerHeight, emptyPaint);
                        canvas.DrawImage(bottomChartCanvas, HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING - pikerHeight);
                    }
                }


                if (transitionMode == TRANSITION_MODE_PARENT)
                {
                    return;
                }

                canvas.FillRectangle(createRect(HORIZONTAL_PADDING,
                        top,
                        start + DP_12,
                        bottom), unactiveBottomChartPaint.Color);

                canvas.FillRectangle(createRect(end - DP_12,
                        top,
                        getMeasuredWidth() - HORIZONTAL_PADDING,
                        bottom), unactiveBottomChartPaint.Color);
            }
            else
            {
                canvas.FillRectangle(createRect(HORIZONTAL_PADDING,
                        top,
                        getMeasuredWidth() - HORIZONTAL_PADDING,
                        bottom), unactiveBottomChartPaint.Color);
            }

            //canvas.drawBitmap(
            //        sharedUiComponents.getPickerMaskBitmap(pikerHeight, getMeasuredWidth() - HORIZONTAL_PADDING * 2),
            //        HORIZONTAL_PADDING, getMeasuredHeight() - PICKER_PADDING - pikerHeight, emptyPaint);

            if (chartData != null)
            {
                pickerRect = createRect(start,
                        top,
                        end,
                        bottom);


                pickerDelegate.middlePickerArea = new Rect(pickerRect.X, pickerRect.Y, pickerRect.Width, pickerRect.Height);


                canvas.FillGeometry(RoundedRect(pathTmp, canvas, (float)pickerRect.Left,
                        (float)pickerRect.Top - DP_1,
                        (float)pickerRect.Left + DP_12,
                        (float)pickerRect.Bottom + DP_1, DP_6, DP_6,
                        true, false, false, true), pickerSelectorPaint.Color);


                canvas.FillGeometry(RoundedRect(pathTmp, canvas, (float)pickerRect.Right - DP_12,
                        (float)pickerRect.Top - DP_1, (float)pickerRect.Right,
                        (float)pickerRect.Bottom + DP_1, DP_6, DP_6,
                        false, true, true, false), pickerSelectorPaint.Color);

                canvas.FillRectangle(createRect(pickerRect.Left + DP_12,
                        pickerRect.Bottom, pickerRect.Right - DP_12,
                        pickerRect.Bottom + DP_1), pickerSelectorPaint.Color);

                canvas.FillRectangle(createRect(pickerRect.Left + DP_12,
                        pickerRect.Top - DP_1, pickerRect.Right - DP_12,
                        pickerRect.Top), pickerSelectorPaint.Color);


                canvas.DrawLine((float)pickerRect.Left + DP_6, pickerRect.centerY() - DP_6,
                        (float)pickerRect.Left + DP_6, pickerRect.centerY() + DP_6, whiteLinePaint);

                canvas.DrawLine((float)pickerRect.Right - DP_6, pickerRect.centerY() - DP_6,
                        (float)pickerRect.Right - DP_6, pickerRect.centerY() + DP_6, whiteLinePaint);


                ChartPickerDelegate.CapturesData middleCap = pickerDelegate.getMiddleCaptured();

                int r = ((int)(pickerRect.Bottom - pickerRect.Top) >> 1);
                int cY = (int)pickerRect.Top + r;

                if (middleCap != null)
                {
                    // canvas.drawCircle(pickerRect.left + ((pickerRect.right - pickerRect.left) >> 1), cY, r * middleCap.aValue + HORIZONTAL_PADDING, ripplePaint);
                }
                else
                {
                    ChartPickerDelegate.CapturesData lCap = pickerDelegate.getLeftCaptured();
                    ChartPickerDelegate.CapturesData rCap = pickerDelegate.getRightCaptured();

                    if (lCap != null)
                        canvas.FillCircle((float)pickerRect.Left + DP_5, cY, r * lCap.aValue - DP_2, ripplePaint.Color);
                    if (rCap != null)
                        canvas.FillCircle((float)pickerRect.Right - DP_5, cY, r * rCap.aValue - DP_2, ripplePaint.Color);
                }

                int cX = start;
                pickerDelegate.leftPickerArea = createRect(
                        cX - PICKER_CAPTURE_WIDTH,
                        top,
                        cX + (PICKER_CAPTURE_WIDTH >> 1),
                        bottom
                );

                cX = end;
                pickerDelegate.rightPickerArea = createRect(
                        cX - (PICKER_CAPTURE_WIDTH >> 1),
                        top,
                        cX + PICKER_CAPTURE_WIDTH,
                        bottom
                );
            }
        }


        long lastTime = 0;

        private void setMaxMinValue(int newMaxHeight, int newMinHeight, bool animated)
        {
            setMaxMinValue(newMaxHeight, newMinHeight, animated, false, false);
        }

        protected void setMaxMinValue(int newMaxHeight, int newMinHeight, bool animated, bool force, bool useAnimator)
        {
            bool heightChanged = true;
            if ((Math.Abs(ChartHorizontalLinesData.lookupHeight(newMaxHeight) - animateToMaxHeight) < thresholdMaxHeight) || newMaxHeight == 0)
            {
                heightChanged = false;
            }

            if (!heightChanged && newMaxHeight == animateToMinHeight) return;
            ChartHorizontalLinesData newData = createHorizontalLinesData(newMaxHeight, newMinHeight);
            newMaxHeight = newData.values[newData.values.Length - 1];
            newMinHeight = newData.values[0];


            if (!useAnimator)
            {
                float k = (currentMaxHeight - currentMinHeight) / (newMaxHeight - newMinHeight);
                if (k > 1f)
                {
                    k = (newMaxHeight - newMinHeight) / (currentMaxHeight - currentMinHeight);
                }
                float s = 0.045f;
                if (k > 0.7)
                {
                    s = 0.1f;
                }
                else if (k < 0.1)
                {
                    s = 0.03f;
                }

                bool update = false;
                if (newMaxHeight != animateToMaxHeight)
                {
                    update = true;
                }
                if (useMinHeight && newMinHeight != animateToMinHeight)
                {
                    update = true;
                }
                if (update)
                {
                    if (maxValueAnimator != null)
                    {
                        maxValueAnimator.removeAllListeners();
                        maxValueAnimator.cancel();
                    }
                    startFromMaxH = currentMaxHeight;
                    startFromMinH = currentMinHeight;
                    startFromMax = 0;
                    startFromMin = 0;
                    minMaxUpdateStep = s;
                }
            }

            animateToMaxHeight = newMaxHeight;
            animateToMinHeight = newMinHeight;
            measureHeightThreshold();

            long t = DateTime.Now.ToTimestamp() * 1000;
            //  debounce
            if (t - lastTime < 320 && !force)
            {
                return;
            }
            lastTime = t;

            if (alphaAnimator != null)
            {
                alphaAnimator.removeAllListeners();
                alphaAnimator.cancel();
            }

            if (!animated)
            {
                currentMaxHeight = newMaxHeight;
                currentMinHeight = newMinHeight;
                horizontalLines.Clear();
                horizontalLines.Add(newData);
                newData.alpha = 255;
                return;
            }


            horizontalLines.Add(newData);

            if (useAnimator)
            {
                if (maxValueAnimator != null)
                {
                    maxValueAnimator.removeAllListeners();
                    maxValueAnimator.cancel();
                }
                minMaxUpdateStep = 0;

                AnimatorSet animatorSet = new AnimatorSet(this);
                animatorSet.playTogether(createAnimator(currentMaxHeight, newMaxHeight, heightUpdateListener));

                if (useMinHeight)
                {
                    animatorSet.playTogether(createAnimator(currentMinHeight, newMinHeight, minHeightUpdateListener));
                }

                maxValueAnimator = animatorSet;
                maxValueAnimator.start();
            }

            int n = horizontalLines.Count;
            for (int i = 0; i < n; i++)
            {
                ChartHorizontalLinesData a = horizontalLines[i];
                if (a != newData) a.fixedAlpha = a.alpha;
            }

            alphaAnimator = createAnimator(0, 255, new AnimatorUpdateListener(animation =>
            {
                newData.alpha = (int)((float)animation.getAnimatedValue());
                foreach (ChartHorizontalLinesData a in horizontalLines)
                {
                    if (a != newData)
                        a.alpha = (int)((a.fixedAlpha / 255f) * (255 - newData.alpha));
                }
                invalidate();
            }));
            alphaAnimator.addListener(new AnimatorUpdateListener(end: animation =>
            {
                horizontalLines.Clear();
                horizontalLines.Add(newData);
            }));

            alphaAnimator.start();
        }

        protected virtual ChartHorizontalLinesData createHorizontalLinesData(int newMaxHeight, int newMinHeight)
        {
            return new ChartHorizontalLinesData(newMaxHeight, newMinHeight, useMinHeight);
        }

        protected ValueAnimator createAnimator(float f1, float f2, AnimatorUpdateListener l)
        {
            ValueAnimator a = ValueAnimator.ofFloat(this, f1, f2);
            a.setDuration(ANIM_DURATION);
            a.setInterpolator(INTERPOLATOR);
            a.addUpdateListener(l);
            return a;
        }

        protected Rect createRect(double x1, double y1, double x2, double y2)
        {
            return new Rect(x1, y1, x2 - x1, y2 - y1);
        }

        int lastX;
        int lastY;
        int capturedX;
        int capturedY;
        long capturedTime;
        protected bool canCaptureChartSelection;

        private void OnPointerPressed(object sender, PointerRoutedEventArgs args)
        {
            // TODO: multi touch?
            // pickerDelegate.capture(x, y, @event.getActionIndex());

            var point = args.GetCurrentPoint(this);

            int x = (int)point.Position.X;
            int y = (int)point.Position.Y;

            capturedTime = DateTime.Now.ToTimestamp() * 1000;
            canvas.CapturePointer(args.Pointer);
            //getParent().requestDisallowInterceptTouchEvent(true);
            bool captured = pickerDelegate.capture(x, y, 0);
            if (captured)
            {
                return /*true*/;
            }

            capturedX = lastX = x;
            capturedY = lastY = y;

            if (chartArea.Contains(new Point(x, y)))
            {
                if (selectedIndex < 0 || !animateLegentTo)
                {
                    chartCaptured = true;
                    selectXOnChart(x, y);
                }
                return /*true*/;
            }
            return /*false*/;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs args)
        {
            var point = args.GetCurrentPoint(this);

            int x = (int)point.Position.X;
            int y = (int)point.Position.Y;

            int dx = x - lastX;
            int dy = y - lastY;

            if (pickerDelegate.captured())
            {
                bool rez = pickerDelegate.move(x, y, 0);
                //if (@event.getPointerCount() > 1)
                //{
                //    x = (int)@event.getX(1);
                //    y = (int)@event.getY(1);
                //    pickerDelegate.move(x, y, 1);
                //}

                //getParent().requestDisallowInterceptTouchEvent(rez);
                if (rez)
                {
                    canvas.CapturePointer(args.Pointer);
                }
                else
                {
                    canvas.ReleasePointerCapture(args.Pointer);
                }

                return /*true*/;
            }

            if (chartCaptured)
            {
                bool disable;
                if (canCaptureChartSelection && DateTime.Now.ToTimestamp() * 1000 - capturedTime > 200)
                {
                    disable = true;
                }
                else
                {
                    disable = Math.Abs(dx) > Math.Abs(dy) || Math.Abs(dy) < touchSlop;
                }
                lastX = x;
                lastY = y;

                //getParent().requestDisallowInterceptTouchEvent(disable);
                if (disable)
                {
                    canvas.CapturePointer(args.Pointer);
                }
                else
                {
                    canvas.ReleasePointerCapture(args.Pointer);
                }
                selectXOnChart(x, y);
            }
            else if (chartArea.Contains(new Point(capturedX, capturedY)))
            {
                int dxCaptured = capturedX - x;
                int dyCaptured = capturedY - y;
                if (Math.Sqrt(dxCaptured * dxCaptured + dyCaptured * dyCaptured) > touchSlop || DateTime.Now.ToTimestamp() * 1000 - capturedTime > 200)
                {
                    chartCaptured = true;
                    selectXOnChart(x, y);
                }
            }
            return /*true*/;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs args)
        {
            // TODO: multi-touch?
            //pickerDelegate.uncapture(@event, @event.getActionIndex());

            if (pickerDelegate.uncapture(args.GetCurrentPoint(this), 0))
            {
                return /*true*/;
            }
            if (chartArea.Contains(new Point(capturedX, capturedY)) && !chartCaptured)
            {
                animateLegend(false);
            }
            pickerDelegate.uncapture();
            updateLineSignature();
            //getParent().requestDisallowInterceptTouchEvent(false);
            canvas.ReleasePointerCapture(args.Pointer);
            chartCaptured = false;
            onActionUp();
            invalidate();
            int min = 0;
            if (useMinHeight) min = findMinValue(startXIndex, endXIndex);
            setMaxMinValue(findMaxValue(startXIndex, endXIndex), min, true, true, false);
            return /*true*/;
        }

        protected virtual void onActionUp()
        {

        }

        protected virtual void selectXOnChart(int x, int y)
        {
            int oldSelectedX = selectedIndex;
            if (chartData == null) return;
            float offset = chartFullWidth * (pickerDelegate.pickerStart) - HORIZONTAL_PADDING;
            float xP = (offset + x) / chartFullWidth;
            selectedCoordinate = xP;
            if (xP < 0)
            {
                selectedIndex = 0;
                selectedCoordinate = 0f;
            }
            else if (xP > 1)
            {
                selectedIndex = chartData.x.Length - 1;
                selectedCoordinate = 1f;
            }
            else
            {
                selectedIndex = chartData.findIndex(startXIndex, endXIndex, xP);
                if (selectedIndex + 1 < chartData.xPercentage.Length)
                {
                    float dx = Math.Abs(chartData.xPercentage[selectedIndex] - xP);
                    float dx2 = Math.Abs(chartData.xPercentage[selectedIndex + 1] - xP);
                    if (dx2 < dx)
                    {
                        selectedIndex++;
                    }
                }
            }

            if (selectedIndex > endXIndex) selectedIndex = endXIndex;
            if (selectedIndex < startXIndex) selectedIndex = startXIndex;

            legendShowing = true;
            animateLegend(true);
            moveLegend(offset);
            if (dateSelectionListener != null)
            {
                dateSelectionListener.onDateSelected(getSelectedDate());
            }
            invalidate();
        }

        public bool animateLegentTo = false;

        public void animateLegend(bool show)
        {
            moveLegend();
            if (animateLegentTo == show) return;
            animateLegentTo = show;
            if (selectionAnimator != null)
            {
                selectionAnimator.removeAllListeners();
                selectionAnimator.cancel();
            }
            selectionAnimator = createAnimator(selectionA, show ? 1f : 0f, selectionAnimatorListener)
                    .setDuration(200);

            selectionAnimator.addListener(selectorAnimatorEndListener);


            selectionAnimator.start();
        }

        public void moveLegend(float offset)
        {
#if LEGEND
            if (chartData == null || selectedIndex == -1 || !legendShowing) return;
            legendSignatureView.setData(selectedIndex, chartData.x[selectedIndex], (List<LineViewData>)lines, false);
            legendSignatureView.setVisibility(VISIBLE);
            legendSignatureView.measure(
                    MeasureSpec.makeMeasureSpec(getMeasuredWidth(), MeasureSpec.AT_MOST),
                    MeasureSpec.makeMeasureSpec(getMeasuredHeight(), MeasureSpec.AT_MOST)
            );
            float lXPoint = chartData.xPercentage[selectedIndex] * chartFullWidth - offset;
            if (lXPoint > (chartStart + chartWidth) >> 1)
            {
                lXPoint -= (legendSignatureView.getWidth() + DP_5);
            }
            else
            {
                lXPoint += DP_5;
            }
            if (lXPoint < 0)
            {
                lXPoint = 0;
            }
            else if (lXPoint + legendSignatureView.getMeasuredWidth() > getMeasuredWidth())
            {
                lXPoint = getMeasuredWidth() - legendSignatureView.getMeasuredWidth();
            }
            legendSignatureView.setTranslationX(
                    lXPoint
            );
#endif
        }

        public virtual int findMaxValue(int startXIndex, int endXIndex)
        {
            int linesSize = lines.Count;
            int maxValue = 0;
            for (int j = 0; j < linesSize; j++)
            {
                if (!lines[j].enabled) continue;
                int lineMax = lines[j].line.segmentTree.rMaxQ(startXIndex, endXIndex);
                if (lineMax > maxValue)
                    maxValue = lineMax;
            }
            return maxValue;
        }


        public virtual int findMinValue(int startXIndex, int endXIndex)
        {
            int linesSize = lines.Count;
            int minValue = int.MaxValue;
            for (int j = 0; j < linesSize; j++)
            {
                if (!lines[j].enabled) continue;
                int lineMin = lines[j].line.segmentTree.rMinQ(startXIndex, endXIndex);
                if (lineMin < minValue)
                    minValue = lineMin;
            }
            return minValue;
        }

        public override void SetDataPublic(ChartData data)
        {
            setData((T)data);
        }

        public override List<LineViewData> GetLines()
        {
            return lines.Cast<LineViewData>().ToList();
        }

        public virtual void setData(T chartData)
        {
            if (this.chartData != chartData)
            {
                invalidate();
                lines.Clear();
                if (chartData != null && chartData.lines != null)
                {
                    for (int i = 0; i < chartData.lines.Count; i++)
                    {
                        lines.Add(createLineViewData(chartData.lines[i]));
                    }
                }
                clearSelection();
                this.chartData = chartData;
                if (chartData != null)
                {
                    if (chartData.x[0] == 0)
                    {
                        pickerDelegate.pickerStart = 0f;
                        pickerDelegate.pickerEnd = 1f;
                    }
                    else
                    {
                        pickerDelegate.minDistance = getMinDistance();
                        if (pickerDelegate.pickerEnd - pickerDelegate.pickerStart < pickerDelegate.minDistance)
                        {
                            pickerDelegate.pickerStart = pickerDelegate.pickerEnd - pickerDelegate.minDistance;
                            if (pickerDelegate.pickerStart < 0)
                            {
                                pickerDelegate.pickerStart = 0f;
                                pickerDelegate.pickerEnd = 1f;
                            }
                        }
                    }
                }
            }
            measureSizes();

            if (chartData != null)
            {
                updateIndexes();
                int min = useMinHeight ? findMinValue(startXIndex, endXIndex) : 0;
                setMaxMinValue(findMaxValue(startXIndex, endXIndex), min, false);
                pickerMaxHeight = 0;
                pickerMinHeight = int.MaxValue;
                initPickerMaxHeight();
                legendSignatureView.setSize(lines.Count);

                invalidatePickerChart = true;
                updateLineSignature();
            }
            else
            {

                pickerDelegate.pickerStart = 0.7f;
                pickerDelegate.pickerEnd = 1f;

                pickerMaxHeight = pickerMinHeight = 0;
                horizontalLines.Clear();

                if (maxValueAnimator != null)
                {
                    maxValueAnimator.cancel();
                }

                if (alphaAnimator != null)
                {
                    alphaAnimator.removeAllListeners();
                    alphaAnimator.cancel();
                }
            }

            invalidate();
        }

        protected virtual float getMinDistance()
        {
            if (chartData == null)
            {
                return 0.1f;
            }

            int n = chartData.x.Length;
            if (n < 5)
            {
                return 1f;
            }
            float r = 5f / n;
            if (r < 0.1f)
            {
                return 0.1f;
            }
            return r;
        }

        protected virtual void initPickerMaxHeight()
        {
            foreach (LineViewData l in lines)
            {
                if (l.enabled && l.line.maxValue > pickerMaxHeight) pickerMaxHeight = l.line.maxValue;
                if (l.enabled && l.line.minValue < pickerMinHeight) pickerMinHeight = l.line.minValue;
                if (pickerMaxHeight == pickerMinHeight)
                {
                    pickerMaxHeight++;
                    pickerMinHeight--;
                }
            }
        }

        public abstract L createLineViewData(ChartData.Line line);

        public void onPickerDataChanged()
        {
            onPickerDataChanged(true, false, false);
        }

        public virtual void onPickerDataChanged(bool animated, bool force, bool useAniamtor)
        {
            if (chartData == null) return;
            chartFullWidth = (chartWidth / (pickerDelegate.pickerEnd - pickerDelegate.pickerStart));

            updateIndexes();
            int min = useMinHeight ? findMinValue(startXIndex, endXIndex) : 0;
            setMaxMinValue(findMaxValue(startXIndex, endXIndex), min, animated, force, useAniamtor);

            if (legendShowing && !force)
            {
                animateLegend(false);
                moveLegend(chartFullWidth * (pickerDelegate.pickerStart) - HORIZONTAL_PADDING);
            }
            invalidate();
        }

        public virtual void onPickerJumpTo(float start, float end, bool force)
        {
            if (chartData == null) return;
            if (force)
            {
                int startXIndex = chartData.findStartIndex(Math.Max(start, 0f));
                int endXIndex = chartData.findEndIndex(startXIndex, Math.Min(end, 1f));
                setMaxMinValue(findMaxValue(startXIndex, endXIndex), findMinValue(startXIndex, endXIndex), true, true, false);
                animateLegend(false);
            }
            else
            {
                updateIndexes();
                invalidate();
            }
        }

        protected void updateIndexes()
        {
            if (chartData == null) return;
            startXIndex = chartData.findStartIndex(Math.Max(pickerDelegate.pickerStart, 0f));
            endXIndex = chartData.findEndIndex(startXIndex, Math.Min(pickerDelegate.pickerEnd, 1f));
            //if (chartHeaderView != null)
            //{
            //    chartHeaderView.setDates(chartData.x[startXIndex], chartData.x[endXIndex]);
            //}
            updateLineSignature();
        }

        private const int BOTTOM_SIGNATURE_COUNT = 6;

        private void updateLineSignature()
        {
            if (chartData == null || chartWidth == 0) return;
            float d = chartFullWidth * chartData.oneDayPercentage;

            float k = chartWidth / d;
            int step = (int)(k / BOTTOM_SIGNATURE_COUNT);
            updateDates(step);
        }


        private void updateDates(int step)
        {
            if (currentBottomSignatures == null || step >= currentBottomSignatures.stepMax || step <= currentBottomSignatures.stepMin)
            {
                step = step.HighestOneBit() << 1;
                if (currentBottomSignatures != null && currentBottomSignatures.step == step)
                {
                    return;
                }

                if (alphaBottomAnimator != null)
                {
                    alphaBottomAnimator.removeAllListeners();
                    alphaBottomAnimator.cancel();
                }

                int stepMax = (int)(step + step * 0.2);
                int stepMin = (int)(step - step * 0.2);


                ChartBottomSignatureData data = new ChartBottomSignatureData(step, stepMax, stepMin);
                data.alpha = 255;

                if (currentBottomSignatures == null)
                {
                    currentBottomSignatures = data;
                    data.alpha = 255;
                    bottomSignatureDate.Add(data);
                    return;
                }

                currentBottomSignatures = data;


                int tmpN = bottomSignatureDate.Count;
                for (int i = 0; i < tmpN; i++)
                {
                    ChartBottomSignatureData a = bottomSignatureDate[i];
                    a.fixedAlpha = a.alpha;
                }

                bottomSignatureDate.Add(data);
                if (bottomSignatureDate.Count > 2)
                {
                    bottomSignatureDate.RemoveAt(0);
                }

                alphaBottomAnimator = createAnimator(0f, 1f, new AnimatorUpdateListener(
                    animation =>
                    {
                        float alpha = (float)animation.getAnimatedValue();
                        foreach (ChartBottomSignatureData a in bottomSignatureDate)
                        {
                            if (a == data)
                            {
                                data.alpha = (int)(255 * alpha);
                            }
                            else
                            {
                                a.alpha = (int)((1f - alpha) * (a.fixedAlpha));
                            }
                        }
                        invalidate();
                    })).setDuration(200);
                alphaBottomAnimator.addListener(new AnimatorUpdateListener(
                    end: animation =>
                    {
                        bottomSignatureDate.Clear();
                        bottomSignatureDate.Add(data);
                    }));

                alphaBottomAnimator.start();
            }
        }

        public override void onCheckChanged()
        {
            onPickerDataChanged(true, true, true);
            int tmpN = lines.Count;
            for (int tmpI = 0; tmpI < tmpN; tmpI++)
            {
                LineViewData lineViewData = lines[tmpI];

                if (lineViewData.enabled && lineViewData.animatorOut != null)
                {
                    lineViewData.animatorOut.cancel();
                }

                if (!lineViewData.enabled && lineViewData.animatorIn != null)
                {
                    lineViewData.animatorIn.cancel();
                }

                if (lineViewData.enabled && lineViewData.alpha != 1f)
                {
                    if (lineViewData.animatorIn != null && lineViewData.animatorIn.isRunning())
                    {
                        continue;
                    }
                    lineViewData.animatorIn = createAnimator(lineViewData.alpha, 1f, new AnimatorUpdateListener(animation =>
                    {
                        lineViewData.alpha = ((float)animation.getAnimatedValue());
                        invalidatePickerChart = true;
                        invalidate();
                    }));
                    lineViewData.animatorIn.start();
                }

                if (!lineViewData.enabled && lineViewData.alpha != 0)
                {
                    if (lineViewData.animatorOut != null && lineViewData.animatorOut.isRunning())
                    {
                        continue;
                    }
                    lineViewData.animatorOut = createAnimator(lineViewData.alpha, 0f, new AnimatorUpdateListener(animation =>
                    {
                        lineViewData.alpha = ((float)animation.getAnimatedValue());
                        invalidatePickerChart = true;
                        invalidate();
                    }));
                    lineViewData.animatorOut.start();
                }
            }

            updatePickerMinMaxHeight();
            if (legendShowing)
                legendSignatureView.setData(selectedIndex, chartData.x[selectedIndex], lines.Cast<LineViewData>().ToList(), true);
        }

        protected virtual void updatePickerMinMaxHeight()
        {
            if (!ANIMATE_PICKER_SIZES) return;
            int max = 0;
            int min = int.MaxValue;
            foreach (LineViewData l in lines)
            {
                if (l.enabled && l.line.maxValue > max) max = l.line.maxValue;
                if (l.enabled && l.line.minValue < min) min = l.line.minValue;
            }

            if ((min != int.MaxValue && min != animatedToPickerMinHeight) || (max > 0 && max != animatedToPickerMaxHeight))
            {
                animatedToPickerMaxHeight = max;
                if (pickerAnimator != null) pickerAnimator.cancel();
                AnimatorSet animatorSet = new AnimatorSet(this);
                animatorSet.playTogether(
                    createAnimator(pickerMaxHeight, animatedToPickerMaxHeight, pickerHeightUpdateListener),
                    createAnimator(pickerMinHeight, animatedToPickerMinHeight, pickerMinHeightUpdateListener)
                );
                pickerAnimator = animatorSet;
                pickerAnimator.start();
            }
        }

        public void setLandscape(bool b)
        {
            landscape = b;
        }

        //public void saveState(Bundle outState)
        //{
        //    if (outState == null) return;

        //    outState.putFloat("chart_start", pickerDelegate.pickerStart);
        //    outState.putFloat("chart_end", pickerDelegate.pickerEnd);


        //    if (lines != null)
        //    {
        //        int n = lines.Count;
        //        bool[] bArray = new bool[n];
        //        for (int i = 0; i < n; i++)
        //        {
        //            bArray[i] = lines[i].enabled;
        //        }
        //        outState.putBooleanArray("chart_line_enabled", bArray);

        //    }
        //}

        //ChartHeaderView chartHeaderView;

        //public void setHeader(ChartHeaderView chartHeaderView)
        //{
        //    this.chartHeaderView = chartHeaderView;
        //}

        public long getSelectedDate()
        {
            if (selectedIndex < 0)
            {
                return -1;
            }
            return chartData.x[selectedIndex];
        }

        public void clearSelection()
        {
            selectedIndex = -1;
            legendShowing = false;
            animateLegentTo = false;
            legendSignatureView.setVisibility(Visibility.Collapsed);
            selectionA = 0f;
        }

        public void selectDate(long activeZoom)
        {
            selectedIndex = Array.BinarySearch(chartData.x, activeZoom);
            legendShowing = true;
            legendSignatureView.setVisibility(Visibility.Visible);
            selectionA = 1f;
            moveLegend(chartFullWidth * (pickerDelegate.pickerStart) - HORIZONTAL_PADDING);
        }

        public long getStartDate()
        {
            return chartData.x[startXIndex];
        }

        public long getEndDate()
        {
            return chartData.x[endXIndex];
        }

        public virtual void updatePicker(ChartData chartData, long d)
        {
            int n = chartData.x.Length;
            long startOfDay = d - d % 86400000L;
            long endOfDay = startOfDay + 86400000L - 1;
            int startIndex = 0;
            int endIndex = 0;

            for (int i = 0; i < n; i++)
            {
                if (startOfDay > chartData.x[i]) startIndex = i;
                if (endOfDay > chartData.x[i]) endIndex = i;
            }
            pickerDelegate.pickerStart = chartData.xPercentage[startIndex];
            pickerDelegate.pickerEnd = chartData.xPercentage[endIndex];
        }

        public void moveLegend()
        {
            moveLegend(chartFullWidth * (pickerDelegate.pickerStart) - HORIZONTAL_PADDING);
        }

        //@Override
        public void requestLayout()
        {
            //super.requestLayout();
        }

        public static CanvasGeometry RoundedRect(
                CanvasPathBuilder path,
                ICanvasResourceCreator creator,
                float left, float top, float right, float bottom, float rx, float ry,
                bool tl, bool tr, bool br, bool bl
        )
        {
            path = new CanvasPathBuilder(creator);
            if (rx < 0) rx = 0;
            if (ry < 0) ry = 0;
            float width = right - left;
            float height = bottom - top;
            if (rx > width / 2) rx = width / 2;
            if (ry > height / 2) ry = height / 2;
            float widthMinusCorners = (width - (2 * rx));
            float heightMinusCorners = (height - (2 * ry));

            var last = new Vector2(right, top + ry);
            void AddRelativeLine(float x, float y)
            {
                last.X += x;
                last.Y += y;
                path.AddLine(last);
            }
            void AddRelativeBezier(float x1, float y1, float x2, float y2)
            {
                path.AddQuadraticBezier(new Vector2(last.X + x1, last.Y + y1), new Vector2(last.X + x2, last.Y + y2));
                last.X += x2;
                last.Y += y2;
            }

            path.BeginFigure(last);
            if (tr)
                AddRelativeBezier(0, -ry, -rx, -ry);
            else
            {
                AddRelativeLine(0, -ry);
                AddRelativeLine(-rx, 0);
            }
            AddRelativeLine(-widthMinusCorners, 0);
            if (tl)
                AddRelativeBezier(-rx, 0, -rx, ry);
            else
            {
                AddRelativeLine(-rx, 0);
                AddRelativeLine(0, ry);
            }
            AddRelativeLine(0, heightMinusCorners);

            if (bl)
                AddRelativeBezier(0, ry, rx, ry);
            else
            {
                AddRelativeLine(0, ry);
                AddRelativeLine(rx, 0);
            }

            AddRelativeLine(widthMinusCorners, 0);
            if (br)
                AddRelativeBezier(rx, 0, rx, -ry);
            else
            {
                AddRelativeLine(rx, 0);
                AddRelativeLine(0, -ry);
            }

            AddRelativeLine(0, -heightMinusCorners);

            path.EndFigure(CanvasFigureLoop.Closed);
            return CanvasGeometry.CreatePath(path);
        }

        public void setDateSelectionListener(DateSelectionListener dateSelectionListener)
        {
            this.dateSelectionListener = dateSelectionListener;
        }

        public interface DateSelectionListener
        {
            void onDateSelected(long date);
        }

        public class SharedUiComponents
        {

            //private Bitmap pickerRoundBitmap;
            //private Canvas canvas;


            //private Rect rectF = new Rect();
            //private Paint xRefP = new Paint(Paint.ANTI_ALIAS_FLAG);

            //public SharedUiComponents()
            //{
            //    xRefP.setColor(0);
            //    xRefP.setXfermode(new PorterDuffXfermode(PorterDuff.Mode.CLEAR));
            //}

            //int k = 0;
            //private bool _invalidate = true;

            //Bitmap getPickerMaskBitmap(int h, int w)
            //{
            //    if (h + w << 10 != k || _invalidate)
            //    {
            //        _invalidate = false;
            //        k = h + w << 10;
            //        pickerRoundBitmap = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888);
            //        canvas = new Canvas(pickerRoundBitmap);

            //        rectF.set(0, 0, w, h);
            //        canvas.drawColor(Theme.getColor(Theme.key_windowBackgroundWhite));
            //        canvas.drawRoundRect(rectF, AndroidUtilities.dp(4), AndroidUtilities.dp(4), xRefP);
            //    }


            //    return pickerRoundBitmap;
            //}

            //public void invalidate()
            //{
            //    _invalidate = true;
            //}
        }
    }
}
