using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Charts;
using Unigram.Charts.Data;
using Unigram.Charts.DataView;
using Unigram.Common;
using Unigram.ViewModels.Chats;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Cells
{
    public sealed partial class ChartCell : HeaderedControl
    {
        private ChartViewData data;

        private BaseChartView chartView;
        private BaseChartView zoomedChartView;

        public ChartCell()
        {
            InitializeComponent();
        }

        public void UpdateData(ChartViewData data)
        {
            //if (args.ItemIndex != _loadIndex)
            //{
            //    root.Header = data.title;
            //    return;
            //}

            BaseChartView chartView = null;
            BaseChartView zoomedChartView = null;

            switch (data.graphType)
            {
                case 1:
                    chartView = new DoubleLinearChartView();
                    //zoomedChartView = new DoubleLinearChartView();
                    //zoomedChartView.legendSignatureView.useHour = true;
                    break;
                case 2:
                    chartView = new StackBarChartView();
                    //zoomedChartView = new StackBarChartView();
                    //zoomedChartView.legendSignatureView.useHour = true;
                    break;
                case 3:
                    chartView = new BarChartView();
                    //zoomedChartView = new LinearChartView();
                    //zoomedChartView.legendSignatureView.useHour = true;
                    break;
                case 4:
                    chartView = new StackLinearChartView();
                    chartView.legendSignatureView.showPercentage = true;
                    //zoomedChartView = new PieChartView();
                    break;
                case 5:
                    chartView = new StepChartView();
                    chartView.legendSignatureView.isTopHourChart = true;
                    //zoomedChartView = new LinearChartView();
                    //zoomedChartView.legendSignatureView.useHour = true;
                    break;
                case 6:
                    chartView = new DoubleStepChartView();
                    //zoomedChartView = new DoubleLinearChartView();
                    //zoomedChartView.legendSignatureView.useHour = true;
                    break;
                default:
                    chartView = new LinearChartView();
                    //zoomedChartView = new LinearChartView();
                    //zoomedChartView.legendSignatureView.useHour = true;
                    break;
            }

            LayoutRoot.Children.Clear();
            LayoutRoot.Children.Add(chartView);

            this.data = data;
            this.chartView = chartView;
            this.zoomedChartView = zoomedChartView;

            if (zoomedChartView != null)
            {
                chartView.Tapped += (s, args) =>
                {
                    onZoomed();
                };

                chartHeaderView.Click += (s, args) =>
                {
                    zoomOut(true);
                };

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
                        check.Style = App.Current.Resources["LineCheckBoxStyle"] as Style;
                        check.Content = line.line.name;
                        check.IsChecked = line.enabled;
                        check.Background = new SolidColorBrush(line.lineColor);
                        check.Margin = new Thickness(12, 0, 0, 12);
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
                    zoomChart(true);
                }
                else
                {
                    zoomOut(false);
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

        public void onZoomed()
        {
            if (data.activeZoom > 0)
            {
                return;
            }
            //performClick();
            if (!chartView.legendSignatureView.canGoZoom)
            {
                return;
            }
            long x = chartView.GetSelectedDate();
            if (data.graphType == 4)
            {
                data.childChartData = new StackLinearChartData(data.chartData, x);
                zoomChart(false);
                return;
            }

            if (data.zoomToken == null)
            {
                return;
            }

            //cancelZoom();
            //String cacheKey = data.zoomToken + "_" + x;
            //ChartData dataFromCache = childDataCache.get(cacheKey);
            //if (dataFromCache != null)
            //{
            //    data.childChartData = dataFromCache;
            //    zoomChart(false);
            //    return;
            //}

            //TLRPC.TL_stats_loadAsyncGraph request = new TLRPC.TL_stats_loadAsyncGraph();
            //request.token = data.zoomToken;
            //if (x != 0)
            //{
            //    request.x = x;
            //    request.flags |= 1;
            //}
            //ZoomCancelable finalCancelabel;
            //lastCancelable = finalCancelabel = new ZoomCancelable();
            //finalCancelabel.adapterPosition = recyclerListView.getChildAdapterPosition(ChartCell.this);

            //chartView.legendSignatureView.showProgress(true, false);

            //int reqId = ConnectionsManager.getInstance(currentAccount).sendRequest(request, (response, error)-> {
            //    ChartData childData = null;
            //    if (response instanceof TLRPC.TL_statsGraph) {
            //        String json = ((TLRPC.TL_statsGraph)response).json.data;
            //        try
            //        {
            //            childData = createChartData(new JSONObject(json), data.graphType, data == languagesData);
            //        }
            //        catch (JSONException e)
            //        {
            //            e.printStackTrace();
            //        }
            //    } else if (response instanceof TLRPC.TL_statsGraphError) {
            //        Toast.makeText(getContext(), ((TLRPC.TL_statsGraphError)response).error, Toast.LENGTH_LONG).show();
            //    }

            //    ChartData finalChildData = childData;
            //    AndroidUtilities.runOnUIThread(()-> {
            //        if (finalChildData != null)
            //        {
            //            childDataCache.put(cacheKey, finalChildData);
            //        }
            //        if (finalChildData != null && !finalCancelabel.canceled && finalCancelabel.adapterPosition >= 0)
            //        {
            //            View view = layoutManager.findViewByPosition(finalCancelabel.adapterPosition);
            //            if (view instanceof ChartCell) {
            //                data.childChartData = finalChildData;
            //                ((ChartCell)view).chartView.legendSignatureView.showProgress(false, false);
            //                ((ChartCell)view).zoomChart(false);
            //            }
            //        }
            //        cancelZoom();
            //    });
            //}, null, null, 0, chat.stats_dc, ConnectionsManager.ConnectionTypeGeneric, true);
            //ConnectionsManager.getInstance(currentAccount).bindRequestToGuid(reqId, classGuid);
        }

        private void zoomChart(bool skipTransition)
        {
            chartView.Visibility = Visibility.Visible;

            long d = chartView.GetSelectedDate();
            var childData = data.childChartData;
            if (childData == null) return;

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
                            if (check) enabledCount++;
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
                chartHeaderView.zoomTo(zoomedChartView, d, true);
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
                chartHeaderView.zoomTo(zoomedChartView, d, false);
            }
            else
            {
                ValueAnimator animator = createTransitionAnimator(d, true);
                animator.AddListener(new AnimatorUpdateListener(null, animation =>
                {
                    _ = chartView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
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

        private void zoomOut(bool animated)
        {
            if (data.chartData.x == null)
            {
                return;
            }
            chartHeaderView.zoomOut(chartView, animated);
            //chartView.legendSignatureView.chevron.setAlpha(1f);
            zoomedChartView.SetHeader(null);

            long d = chartView.GetSelectedDate();
            data.activeZoom = 0;

            //chartView.Visibility = Visibility.Visible;
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
                ValueAnimator animator = createTransitionAnimator(d, false);
                animator.AddListener(new AnimatorUpdateListener(animator =>
                {
                    _ = zoomedChartView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        zoomedChartView.Visibility = Visibility.Collapsed;
                    });

                    chartView.transitionMode = BaseChartView.TRANSITION_MODE_NONE;
                    zoomedChartView.transitionMode = BaseChartView.TRANSITION_MODE_NONE;

                    //chartView.enabled = true;
                    //zoomedChartView.enabled = false;

                    if (!(chartView is StackLinearChartView))
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

        private ValueAnimator createTransitionAnimator(long d, bool inz)
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

            int max = 0;
            int min = int.MaxValue;
            for (int i = 0; i < data.chartData.lines.Count; i++)
            {
                if (data.chartData.lines[i].y[dateIndex] > max)
                    max = data.chartData.lines[i].y[dateIndex];
                if (data.chartData.lines[i].y[dateIndex] < min)
                    min = data.chartData.lines[i].y[dateIndex];
            }
            float pYPercentage = (((float)min + (max - min)) - chartView.currentMinHeight) / (chartView.currentMaxHeight - chartView.currentMinHeight);


            var hidden = chartView.Visibility == Visibility.Collapsed;

            chartView.FillTransitionParams(param);
            zoomedChartView.FillTransitionParams(param);
            ValueAnimator animator = ValueAnimator.OfFloat(inz ? 0f : 1f, inz ? 1f : 0f);
            animator.AddUpdateListener(new AnimatorUpdateListener(animation =>
            {
                float fullWidth = (chartView.chartWidth / (chartView.pickerDelegate.pickerEnd - chartView.pickerDelegate.pickerStart));
                float offset = fullWidth * (chartView.pickerDelegate.pickerStart) - BaseChartView.HORIZONTAL_PADDING;

                param.pY = (float)chartView.chartArea.Top + (1f - pYPercentage) * (float)chartView.chartArea.Height;
                param.pX = chartView.chartFullWidth * param.xPercentage - offset;

                param.progress = (float)animation.GetAnimatedValue();
                zoomedChartView.Invalidate();
                zoomedChartView.FillTransitionParams(param);
                chartView.Invalidate();

                if (hidden)
                {
                    _ = chartView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        chartView.Visibility = Visibility.Visible;
                    });
                }
            }));

            animator.SetDuration(400);
            animator.setInterpolator(new FastOutSlowInInterpolator());

            return animator;
        }
    }
}
