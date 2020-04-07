using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Charts;
using Unigram.Charts.DataView;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatStatisticsPage : Page, IChatDelegate
    {
        public ChatStatisticsViewModel ViewModel => DataContext as ChatStatisticsViewModel;

        public ChatStatisticsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<ChatStatisticsViewModel, IChatDelegate>(this);

            _loadIndex++;

            if (_loadIndex > 8)
            {
                _loadIndex = 0;
            }
        }

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
        }

        public void UpdateChatTitle(Chat chat)
        {
            Title.Text = ViewModel.CacheService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);
        }

        #endregion

        public static int _loadIndex = 2;

        private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var root = args.ItemContainer.ContentTemplateRoot as HeaderedControl;
            var data = args.Item as ChartViewData;

            var border = root.Items[0] as AspectView;
            var checks = root.Items[1] as WrapPanel;

            if (args.Phase < 2)
            {
                root.Header = data.title;
                border.Children.Clear();

                args.RegisterUpdateCallback(2, OnContainerContentChanging);
                return;
            }

            border.Constraint = data;

            if (data.token != null)
            {
                if (data.title == Strings.Resources.LanguagesChartTitle)
                {
                    System.Diagnostics.Debugger.Break();
                }
                await data.LoadAsync(ViewModel.ProtoService, ViewModel.Chat.Id);

            }

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
                    //chartView.legendSignatureView.showPercentage = true;
                    //zoomedChartView = new PieChartView();
                    break;
                case 5:
                    chartView = new StepChartView();
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

            root.Header = data.title;
            border.Children.Clear();
            border.Children.Add(chartView);
            checks.Children.Clear();

            chartView.Loaded += (s, args) =>
            {
                chartView.SetDataPublic(data.chartData);

                var lines = chartView.GetLines();
                if (lines.Count > 1)
                {
                    foreach (var line in lines)
                    {
                        var check = new FauxCheckBox();
                        check.Style = Resources["LineCheckBoxStyle"] as Style;
                        check.Content = line.line.name;
                        check.IsChecked = line.enabled;
                        check.Background = new SolidColorBrush(line.lineColor);
                        check.Margin = new Thickness(12, 0, 0, 12);
                        check.DataContext = line;
                        check.Tag = chartView;
                        check.Click += CheckBox_Checked;

                        checks.Children.Add(check);
                    }

                    checks.Visibility = Visibility.Visible;
                }
                else
                {
                    checks.Visibility = Visibility.Collapsed;
                }
            };
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox check && check.DataContext is LineViewData line && check.Tag is BaseChartView chartView)
            {
                var lines = chartView.GetLines();
                if (line.enabled && lines.Except(new[] { line }).Any(x => x.enabled))
                {
                    line.enabled = false;
                    check.IsChecked = false;

                    chartView.onCheckChanged();
                }
                else if (!line.enabled)
                {
                    line.enabled = true;
                    check.IsChecked = true;

                    chartView.onCheckChanged();
                }
                else
                {
                    VisualUtilities.ShakeView(check);
                }

                //var border =

                //test.onCheckChanged();
            }

        }
    }
}
