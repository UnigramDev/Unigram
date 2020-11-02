using System.Linq;
using Telegram.Td.Api;
using Unigram.Charts;
using Unigram.Charts.DataView;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Views.Chats
{
    public sealed partial class MessageStatisticsPage : HostedPage, IChatDelegate
    {
        public MessageStatisticsViewModel ViewModel => DataContext as MessageStatisticsViewModel;

        public MessageStatisticsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<MessageStatisticsViewModel, IChatDelegate>(this);
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

        #region Binding

        private string ConvertViews(Message message)
        {
            if (message?.InteractionInfo != null)
            {
                return message.InteractionInfo.ViewCount.ToString("N0");
            }

            return string.Empty;
        }

        private string ConvertPublicShares(Message message, int totalCount)
        {
            return totalCount.ToString("N0");
        }

        private string ConvertPrivateShares(Message message, int totalCount)
        {
            if (message?.InteractionInfo != null)
            {
                return string.Format("≈{0:N0}", message.InteractionInfo.ForwardCount - totalCount);
            }

            return string.Empty;
        }

        #endregion

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var button = args.ItemContainer.ContentTemplateRoot as Button;
            var message = args.Item as Message;

            var content = button.Content as Grid;

            var title = content.Children[1] as TextBlock;
            var subtitle = content.Children[2] as TextBlock;

            var photo = content.Children[0] as ProfilePicture;

            var chat = ViewModel.CacheService.GetChat(message.ChatId);
            if (chat == null)
            {
                return;
            }

            title.Text = chat.Title;
            subtitle.Text = Locale.Declension("Views", message.InteractionInfo?.ViewCount ?? 0);

            photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);

            button.CommandParameter = message;
            button.Command = ViewModel.OpenPostCommand;
        }

        private async void OnElementPrepared(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var root = sender as HeaderedControl;
            var data = args.NewValue as ChartViewData;

            if (data == null)
            {
                return;
            }

            var border = root.Items[0] as AspectView;
            var checks = root.Items[1] as WrapPanel;

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

            root.Header = data.title;
            border.Children.Clear();
            border.Children.Add(chartView);
            border.Children.Add(chartView.legendSignatureView);
            checks.Children.Clear();

            chartView.setHeader(chartView.legendSignatureView.isTopHourChart ? null : root);
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
