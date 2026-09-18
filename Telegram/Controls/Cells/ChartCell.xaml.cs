//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using Telegram.Charts;
using Telegram.Charts.Data;
using Telegram.Charts.DataView;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class ChartCell : StackPanel
    {
        private ChartViewData data;

        private BaseChartView chartView;
        private BaseChartView zoomedChartView;

        public ChartCell()
        {
            InitializeComponent();

            // Once, here, rather than per UpdateData: the header is a XAML child and outlives every
            // chart the cell shows, so subscribing alongside them would stack a handler per recycle.
            chartHeaderView.Click += OnHeaderClick;
        }

        private void OnHeaderClick(object sender, RoutedEventArgs e)
        {
            if (zoomedChartView != null)
            {
                ZoomOut(true);
            }
        }

        private void OnChartTapped(object sender, TappedRoutedEventArgs e)
        {
            OnZoomed();
        }

        public void PrepareData(ChartViewData data)
        {
            chartHeaderView.SetTitle(data?.title ?? string.Empty);

            LayoutRoot.Children.Clear();
            LayoutRoot.Constraint = data;
        }

        private static BaseChartView CreateZoomedChartView(int graphType)
        {
            BaseChartView view = graphType switch
            {
                1 or 6 => new DoubleLinearChartView(),
                2 or 7 or 8 => new StackBarChartView(),
                4 => new PieChartView(),
                _ => new LinearChartView()
            };

            // A zoomed graph covers one day, so its legend reads hours rather than dates. The pie
            // is the exception: it shows a share of a whole, and has no time axis to label.
            view.legendSignatureView.useHour = graphType != 4;

            return view;
        }

        public void UpdateData(ChartViewData data)
        {
            if (data?.chartData == null)
            {
                LayoutRoot.Children.Clear();
                CheckPanel.Children.Clear();

                Visibility = Visibility.Collapsed;
                return;
            }

            Visibility = Visibility.Visible;

            //if (args.ItemIndex != _loadIndex)
            //{
            //    root.Header = data.title;
            //    return;
            //}

            // Zoom needs somewhere to go: a token to fetch the day's own graph, or a languages
            // chart, which builds its child locally. Where there is neither, the zoomed view is
            // never shown - and it is a whole chart, surface and legend included, so it is not
            // built at all rather than built and left hidden.
            var canZoom = !string.IsNullOrEmpty(data.zoomToken) || data.graphType == 4;

            BaseChartView chartView = null;
            BaseChartView zoomedChartView = canZoom ? CreateZoomedChartView(data.graphType) : null;

            switch (data.graphType)
            {
                case 1:
                    chartView = new DoubleLinearChartView();
                    break;
                case 2:
                    chartView = new StackBarChartView();
                    break;
                case 3:
                    chartView = new BarChartView();
                    break;
                case 4:
                    chartView = new StackLinearChartView();
                    chartView.legendSignatureView.showPercentage = true;
                    break;
                case 5:
                    chartView = new StepChartView();
                    chartView.legendSignatureView.isTopHourChart = true;
                    break;
                case 6:
                    chartView = new DoubleStepChartView();
                    break;
                case 7:
                case 8:
                    chartView = new StackBarChartView();
                    break;
                default:
                    chartView = new LinearChartView();
                    break;
            }

            LayoutRoot.Children.Clear();
            LayoutRoot.Children.Add(chartView);

            AutomationProperties.SetName(chartView, data.title);

            this.data = data;
            this.chartView = chartView;
            this.zoomedChartView = zoomedChartView;

            if (zoomedChartView != null)
            {
                chartView.Tapped += OnChartTapped;

                // There is a zoomed view precisely when the chart can zoom, so the chevron follows
                // from its existence. The zoomed chart is the bottom of the stack: nothing further.
                chartView.legendSignatureView.zoomEnabled = true;
                zoomedChartView.legendSignatureView.zoomEnabled = false;

                LayoutRoot.Children.Add(zoomedChartView);
                zoomedChartView.Visibility = Visibility.Collapsed;
            }

            CheckPanel.Children.Clear();

            chartView.SetHeader(chartView.legendSignatureView.isTopHourChart ? null : chartHeaderView);
            chartView.Loaded += (s, args) =>
            {
                chartView.SetDataPublic(data.chartData);

                var lines = chartView.GetLines();
                if (lines.Count > 1)
                {
                    foreach (var line in lines)
                    {
                        var check = new FauxCheckBox();
                        check.Resources = new CheckBoxResources(line.lineColor);
                        check.Style = BootStrapper.Current.Resources["DefaultCheckBoxStyle"] as Style;
                        check.Content = line.line.name;
                        check.IsFaux = true;
                        check.IsChecked = line.enabled;
                        check.Background = new SolidColorBrush(line.lineColor);
                        check.Margin = new Thickness(12, 0, 0, 12);
                        check.MinWidth = 0;
                        check.DataContext = line;
                        check.Click += CheckBox_Checked;

                        CheckPanel.Children.Add(check);
                    }

                    CheckPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    CheckPanel.Visibility = Visibility.Collapsed;
                }

                if (zoomedChartView == null)
                {
                    return;
                }

                if (data.activeZoom > 0)
                {
                    chartView.SelectDate(data.activeZoom);
                    ZoomChart(true);
                }
                else
                {
                    ZoomOut(false);
                    //chartView.invalidate();
                }
            };
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox check && check.DataContext is LineViewData line)
            {
                var lines = chartView.GetLines();
                if (line.enabled && lines.Except(new[] { line }).Any(x => x.enabled))
                {
                    line.enabled = false;
                    check.IsChecked = false;

                    chartView.OnCheckChanged();
                }
                else if (!line.enabled)
                {
                    line.enabled = true;
                    check.IsChecked = true;

                    chartView.OnCheckChanged();
                }
                else
                {
                    VisualUtilities.ShakeView(check);
                    return;
                }

                if (data.activeZoom > 0 && zoomedChartView != null)
                {
                    var zoomedLines = zoomedChartView.GetLines();

                    var position = lines.IndexOf(line);
                    if (position < zoomedLines.Count)
                    {
                        zoomedLines[position].enabled = line.enabled;
                        zoomedChartView.OnCheckChanged();
                    }
                }

                //var border =

                //test.onCheckChanged();
            }

        }

        public async void OnZoomed()
        {
            if (data.activeZoom > 0)
            {
                return;
            }

            if (!chartView.legendSignatureView.canGoZoom)
            {
                return;
            }

            long x = chartView.GetSelectedDate();
            if (data.graphType == 4)
            {
                data.childChartData = new StackLinearChartData(data.chartData, x);
                ZoomChart(false);
                return;
            }

            // Empty rather than null where a graph has no zoom: TDLib strings cross the ABI as
            // HSTRING, which cannot carry one.
            if (string.IsNullOrEmpty(data.zoomToken))
            {
                return;
            }

            // The request is per date and can outlive the cell: a list recycles its rows, so what
            // comes back is only applied if this cell is still showing the graph that asked.
            var requested = data;

            chartView.legendSignatureView.showProgress(true, false);

            var loaded = await data.LoadZoomAsync(x);

            if (requested != data)
            {
                return;
            }

            chartView.legendSignatureView.showProgress(false, false);

            if (loaded)
            {
                ZoomChart(false);
            }
        }

        private void ZoomChart(bool skipTransition)
        {
            chartView.Visibility = Visibility.Visible;

            long d = chartView.GetSelectedDate();
            var childData = data.childChartData;
            if (childData == null)
            {
                return;
            }

            if (!skipTransition || zoomedChartView.Visibility != Visibility.Visible)
            {
                zoomedChartView.UpdatePicker(childData, d);
            }
            zoomedChartView.SetDataPublic(childData);

            if (data.chartData.lines.Count > 1)
            {
                var lines = chartView.GetLines();
                var zoomedLines = zoomedChartView.GetLines();
                int enabledCount = 0;
                for (int i = 0; i < data.chartData.lines.Count; i++)
                {
                    bool found = false;
                    for (int j = 0; j < childData.lines.Count; j++)
                    {
                        var line = childData.lines[j];
                        if (line.id.Equals(data.chartData.lines[i].id))
                        {
                            bool check = lines[j].enabled;
                            zoomedLines[j].enabled = check;
                            zoomedLines[j].alpha = check ? 1f : 0f;
                            //checkBoxes.get(i).checkBox.enabled = true;
                            //checkBoxes.get(i).checkBox.animate().alpha(1).start();
                            if (check)
                            {
                                enabledCount++;
                            }

                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        //checkBoxes.get(i).checkBox.enabled = false;
                        //checkBoxes.get(i).checkBox.animate().alpha(0).start();
                    }
                }

                if (enabledCount == 0)
                {
                    for (int i = 0; i < data.chartData.lines.Count; i++)
                    {
                        //checkBoxes.get(i).checkBox.enabled = true;
                        //checkBoxes.get(i).checkBox.animate().alpha(1).start();
                    }
                    return;
                }
            }

            data.activeZoom = d;

            //chartView.legendSignatureView.setAlpha(0f);
            chartView.selectionA = 0;
            chartView.legendShowing = false;
            chartView.animateLegentTo = false;

            zoomedChartView.UpdateColors();

            if (!skipTransition)
            {
                zoomedChartView.ClearSelection();
                chartHeaderView.ZoomTo(zoomedChartView, d, true);
            }

            zoomedChartView.SetHeader(chartHeaderView);
            chartView.SetHeader(null);

            if (skipTransition)
            {
                chartView.Visibility = Visibility.Collapsed;
                zoomedChartView.Visibility = Visibility.Visible;

                chartView.transitionMode = BaseChartView.TRANSITION_MODE_NONE;
                zoomedChartView.transitionMode = BaseChartView.TRANSITION_MODE_NONE;
                //chartView.enabled = false;
                //zoomedChartView.enabled = true;
                chartHeaderView.ZoomTo(zoomedChartView, d, false);
            }
            else
            {
                ValueAnimator animator = CreateTransitionAnimator(d, true);
                animator.AddListener(new AnimatorUpdateListener(null, animation =>
                {
                    chartView.BeginOnUIThread(() =>
                    {
                        chartView.Visibility = Visibility.Collapsed;
                    });

                    //chartView.enabled = false;
                    //zoomedChartView.enabled = true;
                    chartView.transitionMode = BaseChartView.TRANSITION_MODE_NONE;
                    zoomedChartView.transitionMode = BaseChartView.TRANSITION_MODE_NONE;
                    //((Activity)getContext()).getWindow().clearFlags(WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE);
                }));
                animator.Start();

            }
        }

        private void ZoomOut(bool animated)
        {
            if (data.chartData.x == null)
            {
                return;
            }
            chartHeaderView.ZoomOut(chartView, animated);
            //chartView.legendSignatureView.chevron.setAlpha(1f);
            zoomedChartView.SetHeader(null);

            // The date the chart was zoomed into, not whatever is selected now. Android reads the
            // live selection here and can, because it is touch driven and nothing clears it; we
            // clear on pointer exit, and moving the mouse to the header to click back does exactly
            // that. GetSelectedDate then returns -1, the transition's binary search misses, and it
            // falls back to the last date - anchoring the whole animation to the right edge.
            long d = data.activeZoom;
            data.activeZoom = 0;

            chartView.Visibility = Visibility.Visible;
            zoomedChartView.ClearSelection();

            zoomedChartView.SetHeader(null);
            chartView.SetHeader(chartHeaderView);

            if (!animated)
            {
                zoomedChartView.Visibility = Visibility.Collapsed;
                //chartView.enabled = true;
                //zoomedChartView.enabled = false;
                chartView.Invalidate();
                //((Activity)getContext()).getWindow().clearFlags(WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE);

                //for (CheckBoxHolder checkbox : checkBoxes)
                //{
                //    checkbox.checkBox.setAlpha(1);
                //    checkbox.checkBox.enabled = true;
                //}
            }
            else
            {
                ValueAnimator animator = CreateTransitionAnimator(d, false);
                animator.AddListener(new AnimatorUpdateListener(animator =>
                {
                    zoomedChartView.BeginOnUIThread(() =>
                    {
                        zoomedChartView.Visibility = Visibility.Collapsed;
                    });

                    chartView.transitionMode = BaseChartView.TRANSITION_MODE_NONE;
                    zoomedChartView.transitionMode = BaseChartView.TRANSITION_MODE_NONE;

                    //chartView.enabled = true;
                    //zoomedChartView.enabled = false;

                    if (chartView is not StackLinearChartView)
                    {
                        chartView.legendShowing = true;
                        chartView.MoveLegend();
                        chartView.AnimateLegend(true);
                        chartView.Invalidate();
                    }
                    else
                    {
                        chartView.legendShowing = false;
                        chartView.ClearSelection();
                    }
                    //((Activity)getContext()).getWindow().clearFlags(WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE);
                }));
                //for (CheckBoxHolder checkbox : checkBoxes)
                //{
                //    checkbox.checkBox.animate().alpha(1f).start();
                //    checkbox.checkBox.enabled = true;
                //}
                animator.Start();
            }
        }

        private ValueAnimator CreateTransitionAnimator(long d, bool inz)
        {
            //((Activity)getContext()).getWindow().setFlags(WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE,
            //        WindowManager.LayoutParams.FLAG_NOT_TOUCHABLE);

            //chartView.enabled = false;
            //zoomedChartView.enabled = false;
            chartView.transitionMode = BaseChartView.TRANSITION_MODE_PARENT;
            zoomedChartView.transitionMode = BaseChartView.TRANSITION_MODE_CHILD;

            var param = new TransitionParams();
            param.pickerEndOut = chartView.pickerDelegate.pickerEnd;
            param.pickerStartOut = chartView.pickerDelegate.pickerStart;

            param.date = d;

            int dateIndex = Array.BinarySearch(data.chartData.x, d);
            if (dateIndex < 0)
            {
                dateIndex = data.chartData.x.Length - 1;
            }
            param.xPercentage = data.chartData.xPercentage[dateIndex];


            zoomedChartView.Visibility = Visibility.Visible;
            zoomedChartView.transitionParams = param;
            chartView.transitionParams = param;

            long max = 0;
            long min = long.MaxValue;
            for (int i = 0; i < data.chartData.lines.Count; i++)
            {
                if (data.chartData.lines[i].y[dateIndex] > max)
                {
                    max = data.chartData.lines[i].y[dateIndex];
                }

                if (data.chartData.lines[i].y[dateIndex] < min)
                {
                    min = data.chartData.lines[i].y[dateIndex];
                }
            }
            float pYPercentage = ((float)min + (max - min) - chartView.currentMinHeight) / (chartView.currentMaxHeight - chartView.currentMinHeight);


            chartView.FillTransitionParams(param);
            zoomedChartView.FillTransitionParams(param);
            ValueAnimator animator = ValueAnimator.OfFloat(chartView.Coordinator, inz ? 0f : 1f, inz ? 1f : 0f);
            animator.AddUpdateListener(new AnimatorUpdateListener(animation =>
            {
                float fullWidth = chartView.chartWidth / (chartView.pickerDelegate.pickerEnd - chartView.pickerDelegate.pickerStart);
                float offset = fullWidth * chartView.pickerDelegate.pickerStart - BaseChartView.HORIZONTAL_PADDING;

                param.pY = (float)chartView.chartArea.Top + (1f - pYPercentage) * (float)chartView.chartArea.Height;
                param.pX = chartView.chartFullWidth * param.xPercentage - offset;

                param.progress = (float)animation.GetAnimatedValue();
                zoomedChartView.Invalidate();
                zoomedChartView.FillTransitionParams(param);
                chartView.Invalidate();
            }));

            animator.SetDuration(400);
            animator.setInterpolator(new FastOutSlowInInterpolator());

            return animator;
        }
    }
}
