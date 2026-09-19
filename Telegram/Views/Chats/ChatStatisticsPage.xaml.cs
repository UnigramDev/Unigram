//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Text;
using Telegram.Charts;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Chats
{
    public sealed partial class ChatStatisticsPage : HostedPage
    {
        public ChatStatisticsViewModel ViewModel => DataContext as ChatStatisticsViewModel;

        public ChatStatisticsPage()
        {
            InitializeComponent();
            Title = Strings.Statistics;
        }

        #region Binding

        private string ConvertPeriod(int startDate, int endDate)
        {
            return string.Format("{0} - {1}", Formatter.Date(startDate), Formatter.Date(endDate));
        }

        private string ConvertShowMore(int count)
        {
            return Locale.Declension(Strings.R.ShowVotes, count);
        }

        #endregion

        private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is MessageInteractionCounters counters)
            {
                if (args.InRecycleQueue)
                {
                    return;
                }

                PrepareInteractionCounters(counters, args);
                return;
            }
            else if (args.Item is ChatStatisticsMessageSenderInfo senderInfo)
            {
                if (args.InRecycleQueue)
                {
                    return;
                }

                PrepareMessageSenderInfo(senderInfo, args);
                return;
            }
            else if (args.Item is ChatStatisticsAdministratorActionsInfo adminInfo)
            {
                if (args.InRecycleQueue)
                {
                    return;
                }

                PrepareAdministratorActionsInfo(adminInfo, args);
                return;
            }
            else if (args.Item is ChatStatisticsInviterInfo inviterInfo)
            {
                if (args.InRecycleQueue)
                {
                    return;
                }

                PrepareInviterInfo(inviterInfo, args);
                return;
            }

            var root = args.ItemContainer.ContentTemplateRoot as ChartCell;
            var data = args.Item as ChartViewData;

            if (args.InRecycleQueue)
            {
                root.UpdateData(null);
                return;
            }

            root.PrepareData(data);

            // Without a name of its own, ListViewItem announces its content's ToString.
            AutomationProperties.SetName(args.ItemContainer, data.title);

            args.Handled = true;

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(2, OnContainerContentChanging);
                return;
            }

            if (data.token != null && data.chartData == null)
            {
                await data.LoadAsync();
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

        private void PrepareInteractionCounters(MessageInteractionCounters counters, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var photo = content.Children[0] as Image;
            var profile = content.Children[1] as ProfilePicture;

            var title = content.Children[2] as FormattedTextBlock;
            var subtitle = content.Children[3] as TextBlock;

            var views = content.Children[4] as TextBlock;
            var shares = content.Children[5] as TextBlock;

            var brief = ChatCell.UpdateBriefLabel(counters.Message.Content, false, false, out var thumbnail);

            title.SetText(ViewModel.ClientService, brief);
            subtitle.Text = Formatter.DateAt(counters.Message.Date);

            views.Text = Locale.Declension(Strings.R.Views, counters.ViewCount);
            shares.Text = Locale.Declension(Strings.R.Shares, counters.ForwardCount);

            if (thumbnail != null)
            {
                double ratioX = (double)36 / thumbnail.Width;
                double ratioY = (double)36 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                var bitmap = new BitmapImage
                {
                    DecodePixelWidth = width,
                    DecodePixelHeight = height,
                    DecodePixelType = DecodePixelType.Logical
                };

                photo.Source = bitmap;
                photo.Visibility = Visibility.Visible;

                using (var stream = new InMemoryRandomAccessStream())
                {
                    try
                    {
                        Direct2DDevice.WriteBytes(thumbnail.Data, stream);
                        _ = bitmap.SetSourceAsync(stream);
                    }
                    catch
                    {
                        // Throws when the data is not a valid encoded image,
                        // not so frequent, but if it happens during ContainerContentChanging it crashes the app.
                    }
                }

                profile.Visibility = Visibility.Collapsed;
                profile.Source = null;
            }
            else
            {
                photo.Visibility = Visibility.Collapsed;
                photo.Source = null;

                profile.Source = ProfilePictureSource.Chat(ViewModel.ClientService, ViewModel.Chat);
                profile.Visibility = Visibility.Visible;
            }

            args.Handled = true;
        }

        private void PrepareMessageSenderInfo(ChatStatisticsMessageSenderInfo senderInfo, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var photo = content.Children[0] as ProfilePicture;
            var title = content.Children[1] as TextBlock;
            var subtitle = content.Children[2] as TextBlock;

            var user = ViewModel.ClientService.GetUser(senderInfo.UserId);
            if (user == null)
            {
                return;
            }

            var stringBuilder = new StringBuilder();
            if (senderInfo.SentMessageCount > 0)
            {
                stringBuilder.Append(Locale.Declension(Strings.R.messages, senderInfo.SentMessageCount));
            }

            if (senderInfo.AverageCharacterCount > 0)
            {
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append(", ");
                }
                stringBuilder.AppendFormat(Strings.CharactersPerMessage, Locale.Declension(Strings.R.Characters, senderInfo.AverageCharacterCount));
            }

            title.Text = user.FullName();
            subtitle.Text = stringBuilder.ToString();
            photo.Source = ProfilePictureSource.User(ViewModel.ClientService, user);

            args.Handled = true;
        }

        private void PrepareAdministratorActionsInfo(ChatStatisticsAdministratorActionsInfo adminInfo, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var photo = content.Children[0] as ProfilePicture;
            var title = content.Children[1] as TextBlock;
            var subtitle = content.Children[2] as TextBlock;

            var user = ViewModel.ClientService.GetUser(adminInfo.UserId);
            if (user == null)
            {
                return;
            }

            var stringBuilder = new StringBuilder();
            if (adminInfo.DeletedMessageCount > 0)
            {
                stringBuilder.Append(Locale.Declension(Strings.R.Deletions, adminInfo.DeletedMessageCount));
            }

            if (adminInfo.BannedUserCount > 0)
            {
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append(", ");
                }

                stringBuilder.Append(Locale.Declension(Strings.R.Bans, adminInfo.BannedUserCount));
            }

            if (adminInfo.RestrictedUserCount > 0)
            {
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append(", ");
                }

                stringBuilder.Append(Locale.Declension(Strings.R.Restrictions, adminInfo.RestrictedUserCount));
            }

            title.Text = user.FullName();
            subtitle.Text = stringBuilder.ToString();
            photo.Source = ProfilePictureSource.User(ViewModel.ClientService, user);

            args.Handled = true;
        }

        private void PrepareInviterInfo(ChatStatisticsInviterInfo inviterInfo, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var photo = content.Children[0] as ProfilePicture;
            var title = content.Children[1] as TextBlock;
            var subtitle = content.Children[2] as TextBlock;

            var user = ViewModel.ClientService.GetUser(inviterInfo.UserId);
            if (user == null)
            {
                return;
            }

            if (inviterInfo.AddedMemberCount > 0)
            {
                subtitle.Text = Locale.Declension(Strings.R.Invitations, inviterInfo.AddedMemberCount);
            }
            else
            {
                subtitle.Text = string.Empty;
            }

            title.Text = user.FullName();
            photo.Source = ProfilePictureSource.User(ViewModel.ClientService, user);

            args.Handled = true;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MessageInteractionCounters counters)
            {
                ViewModel.OpenPost(counters);
            }
            else if (e.ClickedItem is ChatStatisticsMessageSenderInfo senderInfo)
            {
                ViewModel.OpenProfile(senderInfo.UserId);
            }
            else if (e.ClickedItem is ChatStatisticsAdministratorActionsInfo adminInfo)
            {
                ViewModel.OpenProfile(adminInfo.UserId);
            }
            else if (e.ClickedItem is ChatStatisticsInviterInfo inviterInfo)
            {
                ViewModel.OpenProfile(inviterInfo.UserId);
            }
        }
    }
}
