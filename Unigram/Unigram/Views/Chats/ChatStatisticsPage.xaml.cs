//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Text;
using Telegram.Td.Api;
using Unigram.Charts;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Converters;
using Unigram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatStatisticsPage : HostedPage
    {
        public ChatStatisticsViewModel ViewModel => DataContext as ChatStatisticsViewModel;

        public ChatStatisticsPage()
        {
            InitializeComponent();
            Title = Strings.Resources.Statistics;
        }

        #region Binding

        private string ConvertPeriod(int startDate, int endDate)
        {
            var start = Utils.UnixTimestampToDateTime(startDate);
            var end = Utils.UnixTimestampToDateTime(endDate);

            return string.Format("{0} - {1}", Converter.ShortDate.Format(start), Converter.ShortDate.Format(end));
        }

        private string ConvertShowMore(int count)
        {
            return Locale.Declension("ShowVotes", count);
        }

        #endregion

        private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var root = args.ItemContainer.ContentTemplateRoot as ChartCell;
            var data = args.Item as ChartViewData;

            if (args.InRecycleQueue)
            {
                root.UpdateData(null);
                return;
            }

            var header = root.Items[0] as ChartHeaderView;
            var border = root.Items[1] as AspectView;
            var checks = root.Items[2] as WrapPanel;

            root.Header = data.title;
            border.Children.Clear();
            border.Constraint = data;

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(2, OnContainerContentChanging);
                return;
            }

            if (data.token != null && data.chartData == null)
            {
                await data.LoadAsync(ViewModel.ClientService, ViewModel.Chat.Id);
            }

            if (data.chartData == null)
            {
                ViewModel.Items.Remove(data);
            }
            else
            {
                root.UpdateData(data);
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
                    if (message.Content is MessageVoiceNote)
                    {
                        title.Text = Strings.Resources.AttachAudio;
                    }
                    else if (message.Content is MessageVideo)
                    {
                        title.Text = Strings.Resources.AttachVideo;
                    }
                    else if (message.Content is MessageAnimation)
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
                    else if (message.Content is MessageAnimatedEmoji animatedEmoji)
                    {
                        title.Text = animatedEmoji.Emoji;
                    }
                    else if (message.Content is MessageLocation location)
                    {
                        title.Text = location.LivePeriod > 0 ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation;
                    }
                    else if (message.Content is MessageVenue)
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

                subtitle.Text = Converter.DateAt(counters.Message.Date);

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
                    var user = ViewModel.ClientService.GetUser(senderInfo.UserId);
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

                    title.Text = user.FullName();
                    subtitle.Text = stringBuilder.ToString();
                    photo.SetUser(ViewModel.ClientService, user, 36);

                    button.CommandParameter = senderInfo.UserId;
                    button.Command = ViewModel.OpenProfileCommand;
                }
                else if (button.DataContext is ChatStatisticsAdministratorActionsInfo adminInfo)
                {
                    var user = ViewModel.ClientService.GetUser(adminInfo.UserId);
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

                    title.Text = user.FullName();
                    subtitle.Text = stringBuilder.ToString();
                    photo.SetUser(ViewModel.ClientService, user, 36);

                    button.CommandParameter = adminInfo.UserId;
                    button.Command = ViewModel.OpenProfileCommand;
                }
                else if (button.DataContext is ChatStatisticsInviterInfo inviterInfo)
                {
                    var user = ViewModel.ClientService.GetUser(inviterInfo.UserId);
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

                    title.Text = user.FullName();
                    photo.SetUser(ViewModel.ClientService, user, 36);

                    button.CommandParameter = inviterInfo.UserId;
                    button.Command = ViewModel.OpenProfileCommand;
                }
            }
        }
    }
}
