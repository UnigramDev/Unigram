using System;
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
using Windows.UI.Xaml.Media.Imaging;

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

        private async void OnElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var content = button.Content as Grid;

            var title = content.Children[1] as TextBlock;
            var subtitle = content.Children[2] as TextBlock;

            if (button.DataContext is MessageInteractionCounters counters)
            {
                var photo = content.Children[0] as Image;

                var views = content.Children[3] as TextBlock;
                var shares = content.Children[4] as TextBlock;

                var caption = counters.Message.GetCaption();
                if (string.IsNullOrEmpty(caption?.Text))
                {
                    var message = counters.Message;
                    if (message.Content is MessageVoiceNote voiceNote)
                    {
                        title.Text = Strings.Resources.AttachAudio;
                    }
                    else if (message.Content is MessageVideo video)
                    {
                        title.Text = Strings.Resources.AttachVideo;
                    }
                    else if (message.Content is MessageAnimation animation)
                    {
                        title.Text = Strings.Resources.AttachGif;
                    }
                    else if (message.Content is MessageAudio audio)
                    {
                        var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                        var titloe = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                        if (performer == null || titloe == null)
                        {
                            title.Text = Strings.Resources.AttachMusic;
                        }
                        else
                        {
                            title.Text = $"\uD83C\uDFB5 {performer} - {titloe}";
                        }
                    }
                    else if (message.Content is MessageDocument document)
                    {
                        if (string.IsNullOrEmpty(document.Document.FileName))
                        {
                            title.Text = Strings.Resources.AttachDocument;
                        }
                        else
                        {
                            title.Text = document.Document.FileName;
                        }
                    }
                    else if (message.Content is MessageInvoice invoice)
                    {
                        title.Text = invoice.Title;
                    }
                    else if (message.Content is MessageContact)
                    {
                        title.Text = Strings.Resources.AttachContact;
                    }
                    else if (message.Content is MessageLocation location)
                    {
                        title.Text = location.LivePeriod > 0 ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation;
                    }
                    else if (message.Content is MessageVenue vanue)
                    {
                        title.Text = Strings.Resources.AttachLocation;
                    }
                    else if (message.Content is MessagePhoto)
                    {
                        title.Text = Strings.Resources.AttachPhoto;
                    }
                    else if (message.Content is MessagePoll poll)
                    {
                        title.Text = "\uD83D\uDCCA " + poll.Poll.Question;
                    }
                    else if (message.Content is MessageCall call)
                    {
                        title.Text = call.ToOutcomeText(message.IsOutgoing);
                    }
                    else if (message.Content is MessageUnsupported)
                    {
                        title.Text = Strings.Resources.UnsupportedAttachment;
                    }
                }
                else
                {
                    title.Text = caption.Text.Replace('\n', ' ');
                }

                subtitle.Text = BindConvert.DateAt(counters.Message.Date);

                views.Text = Locale.Declension("Views", counters.ViewCount);
                shares.Text = Locale.Declension("Shares", counters.ForwardCount);

                var thumbnail = counters.Message.GetMinithumbnail();
                if (thumbnail != null)
                {
                    double ratioX = (double)36 / thumbnail.Width;
                    double ratioY = (double)36 / thumbnail.Height;
                    double ratio = Math.Max(ratioX, ratioY);

                    var width = (int)(thumbnail.Width * ratio);
                    var height = (int)(thumbnail.Height * ratio);

                    var bitmap = new BitmapImage { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
                    var bytes = thumbnail.Data.ToArray();

                    using (var stream = new System.IO.MemoryStream(bytes))
                    {
                        var random = System.IO.WindowsRuntimeStreamExtensions.AsRandomAccessStream(stream);
                        await bitmap.SetSourceAsync(random);
                    }

                    photo.Source = bitmap;
                    photo.Visibility = Visibility.Visible;
                }
                else
                {
                    photo.Visibility = Visibility.Collapsed;
                    photo.Source = null;
                }

                button.CommandParameter = counters.Message;
                button.Command = ViewModel.OpenPostCommand;
            }
            else
            {
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

                    button.CommandParameter = senderInfo.UserId;
                    button.Command = ViewModel.OpenProfileCommand;
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

                    button.CommandParameter = adminInfo.UserId;
                    button.Command = ViewModel.OpenProfileCommand;
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

                    button.CommandParameter = inviterInfo.UserId;
                    button.Command = ViewModel.OpenProfileCommand;
                }
            }
        }
    }
}
