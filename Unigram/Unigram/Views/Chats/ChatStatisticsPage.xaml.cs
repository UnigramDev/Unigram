using System.Linq;
using System.Text;
using Telegram.Td.Api;
using Unigram.Charts;
using Unigram.Charts.DataView;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatStatisticsPage : HostedPage, IChatDelegate
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

        #region Binding

        private string ConvertPeriod(int startDate, int endDate)
        {
            var start = Utils.UnixTimestampToDateTime(startDate);
            var end = Utils.UnixTimestampToDateTime(endDate);

            return string.Format("{0} - {1}", BindConvert.Current.ShortDate.Format(start), BindConvert.Current.ShortDate.Format(end));
        }

        private string ConvertShowMore(int count)
        {
            return Locale.Declension("ShowVotes", count);
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

        private void OnElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var content = button.Content as Grid;

            var title = content.Children[1] as TextBlock;
            var subtitle = content.Children[2] as TextBlock;
            var photo = content.Children[0] as ProfilePicture;

            if (button.DataContext is ChatStatisticsMessageSenderInfo senderInfo)
            {
                var user = ViewModel.CacheService.GetUser(senderInfo.UserId);
                if (user == null)
                {
                    return;
                }

                var stringBuilder = new StringBuilder();
                if (senderInfo.SentMessageCount > 0)
                {
                    stringBuilder.Append(Locale.Declension("messages", senderInfo.SentMessageCount));
                }

                if (senderInfo.AverageCharacterCount > 0)
                {
                    if (stringBuilder.Length > 0)
                    {
                        stringBuilder.Append(", ");
                    }
                    stringBuilder.AppendFormat(Strings.Resources.CharactersPerMessage, Locale.Declension("Characters", senderInfo.AverageCharacterCount));
                }

                title.Text = user.GetFullName();
                subtitle.Text = stringBuilder.ToString();
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);

                button.Command = ViewModel.OpenProfileCommand;
                button.CommandParameter = senderInfo.UserId;
            }
            else if (button.DataContext is ChatStatisticsAdministratorActionsInfo adminInfo)
            {
                var user = ViewModel.CacheService.GetUser(adminInfo.UserId);
                if (user == null)
                {
                    return;
                }

                var stringBuilder = new StringBuilder();
                if (adminInfo.DeletedMessageCount > 0)
                {
                    stringBuilder.Append(Locale.Declension("Deletions", adminInfo.DeletedMessageCount));
                }

                if (adminInfo.BannedUserCount > 0)
                {
                    if (stringBuilder.Length > 0)
                    {
                        stringBuilder.Append(", ");
                    }

                    stringBuilder.Append(Locale.Declension("Bans", adminInfo.BannedUserCount));
                }

                if (adminInfo.RestrictedUserCount > 0)
                {
                    if (stringBuilder.Length > 0)
                    {
                        stringBuilder.Append(", ");
                    }

                    stringBuilder.Append(Locale.Declension("Restrictions", adminInfo.RestrictedUserCount));
                }

                title.Text = user.GetFullName();
                subtitle.Text = stringBuilder.ToString();
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);

                button.Command = ViewModel.OpenProfileCommand;
                button.CommandParameter = adminInfo.UserId;
            }
            else if (button.DataContext is ChatStatisticsInviterInfo inviterInfo)
            {
                var user = ViewModel.CacheService.GetUser(inviterInfo.UserId);
                if (user == null)
                {
                    return;
                }

                if (inviterInfo.AddedMemberCount > 0)
                {
                    subtitle.Text = Locale.Declension("Invitations", inviterInfo.AddedMemberCount);
                }
                else
                {
                    subtitle.Text = string.Empty;
                }

                title.Text = user.GetFullName();
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);

                button.Command = ViewModel.OpenProfileCommand;
                button.CommandParameter = inviterInfo.UserId;
            }
        }
    }
}
