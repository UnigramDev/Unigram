using System;
using System.Collections.Generic;
using System.Globalization;
using Unigram.Common;
using Unigram.Converters;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Charts.DataView
{
    public sealed partial class LegendSignatureView : StackPanel
    {
        public LegendSignatureView()
        {
            this.InitializeComponent();
        }

        public bool isTopHourChart;
        //LinearLayout content;
        Holder[] holdes;
        //Drawable background;
        //public ImageView chevron;
        //private RadialProgressView progressView;

        //SimpleDateFormat format = new SimpleDateFormat("E, ");
        //SimpleDateFormat format2 = new SimpleDateFormat("MMM dd");
        //SimpleDateFormat hourFormat = new SimpleDateFormat(" HH:mm");

        public bool useHour;
        public bool showPercentage;
        public bool zoomEnabled;

        public bool canGoZoom = true;

        //Drawable shadowDrawable;
        //Drawable backgroundDrawable;

        //    Runnable showProgressRunnable = new Runnable() {
        //        @Override
        //        public void run()
        //    {
        //        chevron.animate().setDuration(120).alpha(0f);
        //        progressView.animate().setListener(null).start();
        //        if (progressView.getVisibility() != View.VISIBLE)
        //        {
        //            progressView.setVisibility(View.VISIBLE);
        //            progressView.setAlpha(0);
        //        }

        //        progressView.animate().setDuration(120).alpha(1f).start();
        //    }
        //};

        public void aLegendSignatureView()
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            //super(context);
            //setPadding(AndroidUtilities.dp(8), AndroidUtilities.dp(8), AndroidUtilities.dp(8), AndroidUtilities.dp(8));
            //content = new LinearLayout(getContext());
            //content.setOrientation(LinearLayout.VERTICAL);

            //time = new TextView(context);
            //time.setTextSize(14);
            //time.setTypeface(AndroidUtilities.getTypeface("fonts/rmedium.ttf"));
            //hourTime = new TextView(context);
            //hourTime.setTextSize(14);
            //hourTime.setTypeface(AndroidUtilities.getTypeface("fonts/rmedium.ttf"));

            //chevron = new ImageView(context);
            //chevron.setImageResource(R.drawable.ic_chevron_right_black_18dp);

            //progressView = new RadialProgressView(context);
            //progressView.setSize(AndroidUtilities.dp(12));
            //progressView.setStrokeWidth(AndroidUtilities.dp(0.5f));
            //progressView.setVisibility(View.GONE);

            //addView(content, LayoutHelper.createFrame(LayoutHelper.WRAP_CONTENT, LayoutHelper.WRAP_CONTENT, Gravity.NO_GRAVITY, 0, 22, 0, 0));
            //addView(time, LayoutHelper.createFrame(LayoutHelper.WRAP_CONTENT, LayoutHelper.WRAP_CONTENT, Gravity.START, 4, 0, 4, 0));
            //addView(hourTime, LayoutHelper.createFrame(LayoutHelper.WRAP_CONTENT, LayoutHelper.WRAP_CONTENT, Gravity.END, 4, 0, 4, 0));
            //addView(chevron, LayoutHelper.createFrame(18, 18, Gravity.END | Gravity.TOP, 0, 2, 0, 0));
            //addView(progressView, LayoutHelper.createFrame(18, 18, Gravity.END | Gravity.TOP, 0, 2, 0, 0));

            recolor();
        }

        public void recolor()
        {
            //time.setTextColor(Theme.getColor(Theme.key_dialogTextBlack));
            //hourTime.setTextColor(Theme.getColor(Theme.key_dialogTextBlack));
            //chevron.setColorFilter(Theme.getColor(Theme.key_statisticChartChevronColor));
            //progressView.setProgressColor(Theme.getColor(Theme.key_statisticChartChevronColor));

            //shadowDrawable = getContext().getResources().getDrawable(R.drawable.stats_tooltip).mutate();
            //backgroundDrawable = Theme.createSimpleSelectorRoundRectDrawable(AndroidUtilities.dp(4), Theme.getColor(Theme.key_dialogBackground), Theme.getColor(Theme.key_listSelector), 0xff000000);
            //CombinedDrawable drawable = new CombinedDrawable(shadowDrawable, backgroundDrawable, AndroidUtilities.dp(3), AndroidUtilities.dp(3));
            //drawable.setFullsize(true);
            //setBackground(drawable);
        }

        public void setSize(int n)
        {
            //content.removeAllViews();
            Lines.Children.Clear();
            holdes = new Holder[n];
            for (int i = 0; i < n; i++)
            {
                holdes[i] = new Holder(showPercentage);
                Lines.Children.Add(holdes[i]);
            }
        }


        public void setData(int index, long date, List<LineViewData> lines, bool animateChanges)
        {
            //int n = holdes.Length;
            //if (animateChanges)
            //{
            //    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.KITKAT)
            //    {
            //        TransitionSet transition = new TransitionSet();
            //        transition.
            //                addTransition(new Fade(Fade.OUT).setDuration(100)).
            //                addTransition(new ChangeBounds().setDuration(150)).
            //                addTransition(new Fade(Fade.IN).setDuration(100));
            //        transition.setOrdering(TransitionSet.ORDERING_SEQUENTIAL);
            //        TransitionManager.beginDelayedTransition(this, transition);
            //    }
            //}

            if (isTopHourChart)
            {
                Time.Text = string.Format(CultureInfo.InvariantCulture, "{0:00}:00", date);
            }
            else
            {
                Time.Text = formatData(Utils.UnixTimestampToDateTime(date / 1000));
                //if (useHour) hourTime.Text = hourFormat.format(date);
            }

            int sum = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].enabled) sum += lines[i].line.y[index];
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].enabled)
                {
                    holdes[i].Visibility = Visibility.Collapsed;
                }
                else
                {
                    var l = lines[i].line;

                    holdes[i].Visibility = Visibility.Visible;

                    holdes[i].Signature = l.name;
                    holdes[i].Value = formatWholeNumber(l.y[index]);
                    holdes[i].Foreground = new SolidColorBrush(lines[i].lineColor);

                    if (showPercentage)
                    {
                        float v = l.y[index] / (float)sum;
                        if (v < 0.1f && v != 0f)
                        {
                            holdes[i].Percentage = string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", (100f * v));
                        }
                        else
                        {
                            holdes[i].Percentage = string.Format(CultureInfo.InvariantCulture, "{0:0}%", Math.Round(100 * v));
                        }
                    }
                }
            }

            //if (zoomEnabled)
            //{
            //    canGoZoom = sum > 0;
            //    chevron.setVisibility(sum > 0 ? View.VISIBLE : View.GONE);
            //}
            //else
            //{
            //    canGoZoom = false;
            //    chevron.setVisibility(View.GONE);
            //}
        }

        private String formatData(DateTime date)
        {
            //if (useHour) return capitalize(format2.format(date));
            //return capitalize(format.format(date)) + capitalize(format2.format(date));
            return BindConvert.Current.DayMonthFullYear.Format(date);
        }

        private String capitalize(String s)
        {
            if (s.Length > 0)
                return char.ToUpper(s[0]) + s.Substring(1);
            return s;
        }


        public String formatWholeNumber(int v)
        {
            float num_ = v;
            int count = 0;
            if (v < 10_000)
            {
                return String.Format("{0:D}", v);
            }
            while (num_ >= 10_000 && count < ChartHorizontalLinesData.s.Length - 1)
            {
                num_ /= 1000;
                count++;
            }
            return String.Format("{0:F2}", num_) + ChartHorizontalLinesData.s[count];
        }


        public void showProgress(bool show, bool force)
        {
            //if (show)
            //{
            //    AndroidUtilities.runOnUIThread(showProgressRunnable, 300);
            //}
            //else
            //{
            //    AndroidUtilities.cancelRunOnUIThread(showProgressRunnable);
            //    if (force)
            //    {
            //        progressView.setVisibility(View.GONE);
            //    }
            //    else
            //    {
            //        chevron.animate().setDuration(80).alpha(1f).start();
            //        if (progressView.getVisibility() == View.VISIBLE)
            //        {
            //            //    progressView.animate().setDuration(80).alpha(0f).setListener(new AnimatorListenerAdapter() {
            //            //    @Override
            //            //    public void onAnimationEnd(Animator animation)
            //            //    {
            //            //        progressView.setVisibility(View.GONE);
            //            //    }
            //            //}).start();
            //        }
            //    }
            //}
        }

        class Holder : Grid
        {
            private readonly TextBlock _value;
            private readonly TextBlock signature;
            private readonly TextBlock percentage;
            //final TextView signature;
            //TextView percentage;
            //final LinearLayout root;

            public string Value
            {
                get => _value.Text;
                set => _value.Text = value;
            }

            public Brush Foreground
            {
                get => _value.Foreground;
                set => _value.Foreground = value;
            }

            public string Signature
            {
                get => signature.Text;
                set => signature.Text = value;
            }

            public string Percentage
            {
                get => percentage.Text;
                set => percentage.Text = value;
            }

            public Holder(bool showPercentage)
            {
                //root = new LinearLayout(getContext());
                //root.setPadding(AndroidUtilities.dp(4), AndroidUtilities.dp(2), AndroidUtilities.dp(4), AndroidUtilities.dp(2));

                if (showPercentage)
                {
                    ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36, GridUnitType.Pixel) });

                    percentage = new TextBlock();
                    percentage.Margin = new Thickness(0, 2, 8, 0);
                    //percentage.getLayoutParams().width = AndroidUtilities.dp(36);
                    //percentage.setVisibility(GONE);
                    //percentage.setTypeface(AndroidUtilities.getTypeface("fonts/rmedium.ttf"));
                    //percentage.setTextSize(13);
                    Children.Add(percentage);
                }
                else
                {
                    ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Pixel) });
                }

                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                signature = new TextBlock();
                signature.Margin = new Thickness(0, 2, 0, 0);
                SetColumn(signature, 1);
                Children.Add(signature);

                _value = new TextBlock();
                _value.Margin = new Thickness(8, 2, 0, 0);
                _value.Style = App.Current.Resources["BaseTextBlockStyle"] as Style;
                _value.HorizontalAlignment = HorizontalAlignment.Right;
                SetColumn(_value, 2);
                Children.Add(_value);

                //root.addView(signature = new TextView(getContext()));
                //signature.getLayoutParams().width = showPercentage ? AndroidUtilities.dp(80) : AndroidUtilities.dp(96);
                //root.addView(value = new TextView(getContext()), LayoutHelper.createLinear(LayoutHelper.MATCH_PARENT, LayoutHelper.WRAP_CONTENT));

                //signature.setGravity(Gravity.START);
                //value.setGravity(Gravity.END);

                //value.setTypeface(AndroidUtilities.getTypeface("fonts/rmedium.ttf"));
                //value.setTextSize(13);
                //value.setMinEms(4);
                //value.setMaxEms(4);
                //signature.setTextSize(13);
            }
        }

        internal void setVisibility(Visibility visibility)
        {
            //throw new NotImplementedException();
            if (Dispatcher.HasThreadAccess)
            {
                Visibility = visibility;
            }
            else
            {
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => Visibility = visibility);
            }
        }
    }
}
