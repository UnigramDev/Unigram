//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Charts.Data;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;
using Windows.Data.Json;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Chats
{
    public class ChatStatisticsViewModel : ViewModelBase
    {
        public ChatStatisticsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<ChartViewData>();

            Interactions = new MvxObservableCollection<MessageInteractionCounters>();
            TopInviters = new MvxObservableCollection<ChatStatisticsInviterInfo>();
            TopAdministrators = new MvxObservableCollection<ChatStatisticsAdministratorActionsInfo>();
            TopSenders = new MvxObservableCollection<ChatStatisticsMessageSenderInfo>();
            TopSendersLeft = new MvxObservableCollection<ChatStatisticsMessageSenderInfo>();

            OpenProfileCommand = new RelayCommand<long>(OpenProfileExecute);
            OpenPostCommand = new RelayCommand<Message>(OpenPostExecute);
        }

        private Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private ChatStatistics _statistics;
        public ChatStatistics Statistics
        {
            get => _statistics;
            set => Set(ref _statistics, value);
        }

        private DateRange _period;
        public DateRange Period
        {
            get => _period;
            set => Set(ref _period, value);
        }

        public MvxObservableCollection<ChartViewData> Items { get; private set; }


        public MvxObservableCollection<MessageInteractionCounters> Interactions { get; private set; }

        //
        // Summary:
        //     List of most active inviters of new members in the last week.
        public MvxObservableCollection<ChatStatisticsInviterInfo> TopInviters { get; private set; }
        //
        // Summary:
        //     List of most active administrators in the last week.
        public MvxObservableCollection<ChatStatisticsAdministratorActionsInfo> TopAdministrators { get; private set; }
        //
        // Summary:
        //     List of users sent most messages in the last week.
        public MvxObservableCollection<ChatStatisticsMessageSenderInfo> TopSenders { get; private set; }
        public MvxObservableCollection<ChatStatisticsMessageSenderInfo> TopSendersLeft { get; private set; }

        public void ExpandTopSenders()
        {
            TopSenders.AddRange(TopSendersLeft);
            TopSendersLeft.Clear();
        }

        public RelayCommand<long> OpenProfileCommand { get; }
        private async void OpenProfileExecute(long userId)
        {
            var response = await ClientService.SendAsync(new CreatePrivateChat(userId, false));
            if (response is Chat chat)
            {
                NavigationService.Navigate(typeof(ProfilePage), chat.Id);
            }
        }

        public RelayCommand<Message> OpenPostCommand { get; }
        private void OpenPostExecute(Message message)
        {
            NavigationService.NavigateToChat(message.ChatId, message.Id);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            IsLoading = true;

            Chat = ClientService.GetChat(chatId);



            var response = await ClientService.SendAsync(new GetChatStatistics(chatId, false));
            if (response is ChatStatistics statistics)
            {
                Statistics = statistics;

                List<ChartViewData> stats;
                if (statistics is ChatStatisticsChannel channelStats)
                {
                    Period = channelStats.Period;

                    stats = new List<ChartViewData>(9)
                    {
                        ChartViewData.Create(channelStats.MemberCountGraph, Strings.GrowthChartTitle, 0),
                        ChartViewData.Create(channelStats.JoinGraph, Strings.FollowersChartTitle, 0),
                        ChartViewData.Create(channelStats.MuteGraph, Strings.NotificationsChartTitle, 0),
                        ChartViewData.Create(channelStats.ViewCountByHourGraph, Strings.TopHoursChartTitle, /*0*/5),
                        ChartViewData.Create(channelStats.ViewCountBySourceGraph, Strings.ViewsBySourceChartTitle, 2),
                        ChartViewData.Create(channelStats.JoinBySourceGraph, Strings.NewFollowersBySourceChartTitle, 2),
                        ChartViewData.Create(channelStats.LanguageGraph, Strings.LanguagesChartTitle, 4),
                        ChartViewData.Create(channelStats.MessageInteractionGraph, Strings.InteractionsChartTitle, /*1*/6),
                        ChartViewData.Create(channelStats.InstantViewInteractionGraph, Strings.IVInteractionsChartTitle, /*1*/6)
                    };

                    var messages = await ClientService.SendAsync(new GetMessages(chatId, channelStats.RecentMessageInteractions.Select(x => x.MessageId).ToArray())) as Messages;
                    if (messages == null)
                    {
                        return;
                    }

                    var interactions = new List<MessageInteractionCounters>(messages.MessagesValue.Count);

                    foreach (var message in messages.MessagesValue)
                    {
                        if (message == null)
                        {
                            continue;
                        }

                        var counters = channelStats.RecentMessageInteractions.FirstOrDefault(x => x.MessageId == message.Id);
                        interactions.Add(new MessageInteractionCounters(message, counters.ForwardCount, counters.ViewCount));
                    }

                    Interactions.ReplaceWith(interactions);
                    TopInviters.Clear();
                    TopAdministrators.Clear();
                    TopSenders.Clear();
                    TopSendersLeft.Clear();
                }
                else if (statistics is ChatStatisticsSupergroup groupStats)
                {
                    Period = groupStats.Period;

                    stats = new List<ChartViewData>(8)
                    {
                        ChartViewData.Create(groupStats.MemberCountGraph, Strings.GrowthChartTitle, 0),
                        ChartViewData.Create(groupStats.JoinGraph, Strings.GroupMembersChartTitle, 0),
                        ChartViewData.Create(groupStats.JoinBySourceGraph, Strings.NewMembersBySourceChartTitle, 2),
                        ChartViewData.Create(groupStats.LanguageGraph, Strings.MembersLanguageChartTitle, 4),
                        ChartViewData.Create(groupStats.MessageContentGraph, Strings.MessagesChartTitle, 2),
                        ChartViewData.Create(groupStats.ActionGraph, Strings.ActionsChartTitle, 1),
                        ChartViewData.Create(groupStats.DayGraph, Strings.TopHoursChartTitle, /*0*/5),
                        ChartViewData.Create(groupStats.WeekGraph, Strings.TopDaysOfWeekChartTitle, 4)
                    };

                    if (stats[7] != null)
                    {
                        stats[7].useWeekFormat = true;
                    }

                    Interactions.Clear();
                    TopInviters.ReplaceWith(groupStats.TopInviters);
                    TopAdministrators.ReplaceWith(groupStats.TopAdministrators);

                    if (groupStats.TopSenders.Count > 10)
                    {
                        TopSenders.ReplaceWith(groupStats.TopSenders.Take(10));
                        TopSendersLeft.ReplaceWith(groupStats.TopSenders.Skip(10));
                    }
                    else
                    {
                        TopSenders.ReplaceWith(groupStats.TopSenders);
                        TopSendersLeft.Clear();
                    }
                }
                else
                {
                    stats = null;
                }

                if (stats != null)
                {
                    Items.ReplaceWith(stats.Where(x => x?.chartData != null || x?.token != null));
                }
                else
                {
                    Items.Clear();
                }
            }

            IsLoading = false;
        }
    }

    public class MessageInteractionCounters
    {
        public Message Message { get; private set; }
        public int ForwardCount { get; private set; }
        public int ViewCount { get; private set; }

        public MessageInteractionCounters(Message message, int forward, int view)
        {
            Message = message;
            ForwardCount = forward;
            ViewCount = view;
        }
    }

    public class ChartViewData
    {

        public bool isError;
        public string errorMessage;
        public long activeZoom;
        public bool viewShowed;
        public ChartData chartData;
        public ChartData childChartData;
        public string token;
        public string zoomToken;

        public readonly int graphType;
        public readonly string title;

        public bool loading;
        public bool isEmpty;
        public bool isLanguages;
        public bool useWeekFormat;

        public ChartViewData(string title, int grahType)
        {
            this.title = title;
            graphType = grahType;
        }

        public static ChartViewData Create(StatisticalGraph graph, string title, int graphType)
        {
            if (graph is null or StatisticalGraphError)
            {
                return null;
            }
            var viewData = new ChartViewData(title, graphType);
            if (graph is StatisticalGraphData data)
            {
                string json = data.JsonData;
                try
                {
                    viewData.chartData = CreateChartData(JsonObject.Parse(json), graphType);
                    viewData.zoomToken = data.ZoomToken;
                    if (viewData.chartData == null || viewData.chartData.x == null || viewData.chartData.x.Length < 2)
                    {
                        viewData.isEmpty = true;
                    }
                    if (graphType == 4 && viewData.chartData != null && viewData.chartData.x != null && viewData.chartData.x.Length > 0)
                    {
                        long x = viewData.chartData.x[viewData.chartData.x.Length - 1];
                        viewData.childChartData = new StackLinearChartData(viewData.chartData, x);
                        viewData.activeZoom = x;
                    }
                }
                catch (Exception)
                {
                    //e.printStackTrace();
                    return null;
                }
            }
            else if (graph is StatisticalGraphAsync async)
            {
                viewData.token = async.Token;
            }

            return viewData;
        }

        private static ChartData CreateChartData(JsonObject jsonObject, int graphType)
        {
            if (graphType is 0 or 5)
            {
                return new ChartData(jsonObject);
            }
            else if (graphType is 1 or 6)
            {
                return new DoubleLinearChartData(jsonObject);
            }
            else if (graphType == 2)
            {
                return new StackBarChartData(jsonObject);
            }
            else if (graphType == 4)
            {
                return new StackLinearChartData(jsonObject);
            }
            return null;
        }

        public async Task<bool> LoadAsync(IClientService clientService, long chatId)
        {
            var graph = await clientService.SendAsync(new GetStatisticalGraph(chatId, token, 0)) as StatisticalGraph;
            var viewData = this;

            if (graph is null or StatisticalGraphError)
            {
                return false;
            }
            else if (graph is StatisticalGraphData data)
            {
                string json = data.JsonData;
                try
                {
                    viewData.chartData = CreateChartData(JsonObject.Parse(json), graphType);
                    viewData.zoomToken = data.ZoomToken;
                    if (viewData.chartData == null || viewData.chartData.x == null || viewData.chartData.x.Length < 2)
                    {
                        viewData.isEmpty = true;
                    }
                    if (graphType == 4 && viewData.chartData != null && viewData.chartData.x != null && viewData.chartData.x.Length > 0)
                    {
                        long x = viewData.chartData.x[viewData.chartData.x.Length - 1];
                        viewData.childChartData = new StackLinearChartData(viewData.chartData, x);
                        viewData.activeZoom = x;
                    }

                    return true;
                }
                catch (Exception)
                {
                    //e.printStackTrace();
                }
            }

            return false;
        }

        //public void load(int accountId, int classGuid, int dc, RecyclerListView recyclerListView, Adapter adapter, DiffUtilsCallback difCallback)
        //{
        //    if (!loading)
        //    {
        //        loading = true;
        //        TLRPC.TL_stats_loadAsyncGraph request = new TLRPC.TL_stats_loadAsyncGraph();
        //        request.token = token;
        //        int reqId = ConnectionsManager.getInstance(accountId).sendRequest(request, (response, error)-> {
        //            ChartData chartData = null;
        //            String zoomToken = null;
        //            if (error == null)
        //            {
        //                if (response instanceof TLRPC.TL_statsGraph) {
        //                    String json = ((TLRPC.TL_statsGraph)response).json.data;
        //                    try
        //                    {
        //                        chartData = createChartData(new JSONObject(json), graphType);
        //                        zoomToken = ((TLRPC.TL_statsGraph)response).zoom_token;
        //                        if (graphType == 4 && chartData.x != null && chartData.x.length > 0)
        //                        {
        //                            long x = chartData.x[chartData.x.length - 1];
        //                            childChartData = new StackLinearChartData(chartData, x);
        //                            activeZoom = x;
        //                        }
        //                    }
        //                    catch (JSONException e)
        //                    {
        //                        e.printStackTrace();
        //                    }
        //                }
        //                if (response instanceof TLRPC.TL_statsGraphError) {
        //                    isEmpty = false;
        //                    isError = true;
        //                    errorMessage = ((TLRPC.TL_statsGraphError)response).error;
        //                }
        //            }

        //            ChartData finalChartData = chartData;
        //            String finalZoomToken = zoomToken;
        //            AndroidUtilities.runOnUIThread(()-> {
        //                loading = false;
        //                this.chartData = finalChartData;
        //                this.zoomToken = finalZoomToken;

        //                int n = recyclerListView.getChildCount();
        //                boolean found = false;
        //                for (int i = 0; i < n; i++)
        //                {
        //                    View child = recyclerListView.getChildAt(i);
        //                    if (child instanceof ChartCell && ((ChartCell)child).data == this) {
        //                    ((ChartCell)child).updateData(this, true);
        //                    found = true;
        //                    break;
        //                }
        //            }
        //            if (!found)
        //            {
        //                difCallback.update();
        //            }
        //        });
        //    }, null, null, 0, dc, ConnectionsManager.ConnectionTypeGeneric, true);
        //    ConnectionsManager.getInstance(accountId).bindRequestToGuid(reqId, classGuid);
        //}
        //}
    }
}
