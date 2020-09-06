using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Charts.Data;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views;
using Windows.Data.Json;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class ChatStatisticsViewModel : TLViewModelBase, IDelegable<IChatDelegate>
    {
        public IChatDelegate Delegate { get; set; }

        public ChatStatisticsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<ChartViewData>();

            Interactions = new MvxObservableCollection<MessageInteractionCounters>();
            TopInviters = new MvxObservableCollection<ChatStatisticsInviterInfo>();
            TopAdministrators = new MvxObservableCollection<ChatStatisticsAdministratorActionsInfo>();
            TopSenders = new MvxObservableCollection<ChatStatisticsMessageSenderInfo>();
            TopSendersLeft = new MvxObservableCollection<ChatStatisticsMessageSenderInfo>();

            TopSendersCommand = new RelayCommand(TopSendersExecute);
            OpenProfileCommand = new RelayCommand<int>(OpenProfileExecute);
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

        public RelayCommand TopSendersCommand { get; }
        private void TopSendersExecute()
        {
            TopSenders.AddRange(TopSendersLeft);
            TopSendersLeft.Clear();
        }

        public RelayCommand<int> OpenProfileCommand { get; }
        private async void OpenProfileExecute(int userId)
        {
            var response = await ProtoService.SendAsync(new CreatePrivateChat(userId, false));
            if (response is Chat chat)
            {
                NavigationService.Navigate(typeof(ProfilePage), chat.Id);
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var chatId = (long)parameter;

            IsLoading = true;

            Chat = ProtoService.GetChat(chatId);
            Delegate?.UpdateChat(Chat);



            var response = await ProtoService.SendAsync(new GetChatStatistics(chatId, false));
            if (response is ChatStatistics statistics)
            {
                Statistics = statistics;

                List<ChartViewData> stats;
                if (statistics is ChatStatisticsChannel channelStats)
                {
                    Period = channelStats.Period;

                    stats = new List<ChartViewData>(9);
                    stats.Add(ChartViewData.create(channelStats.MemberCountGraph, Strings.Resources.GrowthChartTitle, 0));
                    stats.Add(ChartViewData.create(channelStats.JoinGraph, Strings.Resources.FollowersChartTitle, 0));
                    stats.Add(ChartViewData.create(channelStats.MuteGraph, Strings.Resources.NotificationsChartTitle, 0));
                    stats.Add(ChartViewData.create(channelStats.ViewCountByHourGraph, Strings.Resources.TopHoursChartTitle, /*0*/5));
                    stats.Add(ChartViewData.create(channelStats.ViewCountBySourceGraph, Strings.Resources.ViewsBySourceChartTitle, 2));
                    stats.Add(ChartViewData.create(channelStats.JoinBySourceGraph, Strings.Resources.NewFollowersBySourceChartTitle, 2));
                    stats.Add(ChartViewData.create(channelStats.LanguageGraph, Strings.Resources.LanguagesChartTitle, 4));
                    stats.Add(ChartViewData.create(channelStats.MessageInteractionGraph, Strings.Resources.InteractionsChartTitle, /*1*/6));
                    stats.Add(ChartViewData.create(channelStats.InstantViewInteractionGraph, Strings.Resources.IVInteractionsChartTitle, /*1*/6));

                    var messages = await ProtoService.SendAsync(new GetMessages(chatId, channelStats.RecentMessageInteractions.Select(x => x.MessageId).ToArray())) as Messages;
                    if (messages == null)
                    {
                        return;
                    }

                    var interactions = new List<MessageInteractionCounters>(messages.MessagesValue.Count);

                    foreach (var message in messages.MessagesValue)
                    {
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

                    stats = new List<ChartViewData>(8);
                    stats.Add(ChartViewData.create(groupStats.MemberCountGraph, Strings.Resources.GrowthChartTitle, 0));
                    stats.Add(ChartViewData.create(groupStats.JoinGraph, Strings.Resources.GroupMembersChartTitle, 0));
                    stats.Add(ChartViewData.create(groupStats.JoinBySourceGraph, Strings.Resources.NewMembersBySourceChartTitle, 2));
                    stats.Add(ChartViewData.create(groupStats.LanguageGraph, Strings.Resources.MembersLanguageChartTitle, 4));
                    stats.Add(ChartViewData.create(groupStats.MessageContentGraph, Strings.Resources.MessagesChartTitle, 2));
                    stats.Add(ChartViewData.create(groupStats.ActionGraph, Strings.Resources.ActionsChartTitle, 1));
                    stats.Add(ChartViewData.create(groupStats.DayGraph, Strings.Resources.TopHoursChartTitle, /*0*/5));
                    stats.Add(ChartViewData.create(groupStats.WeekGraph, Strings.Resources.TopDaysOfWeekChartTitle, 4));

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

                for (int i = 0; i < stats.Count; i++)
                {
                    if (i == Views.Chats.ChatStatisticsPage._loadIndex && stats[i].token != null)
                    {
                        var resp = await ProtoService.SendAsync(new GetChatStatisticsGraph(chatId, stats[i].token, 0));
                        if (resp is StatisticsGraphData data)
                        {
                            if (stats[i].title == Strings.Resources.LanguagesChartTitle)
                            {
                                System.Diagnostics.Debugger.Break();
                            }

                            stats[i] = ChartViewData.create(data, stats[i].title, stats[i].graphType);
                        }
                    }
                }

                Items.ReplaceWith(stats);


                //foreach (var item in stats)
                //{
                //    var model =
                //}
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
        public String errorMessage;
        public long activeZoom;
        public bool viewShowed;
        public ChartData chartData;
        ChartData childChartData;
        public String token;
        String zoomToken;

        public readonly int graphType;
        public readonly String title;

        bool loading;
        bool isEmpty;

        public ChartViewData(String title, int grahType)
        {
            this.title = title;
            this.graphType = grahType;
        }

        public static ChartViewData create(StatisticsGraph graph, String title, int graphType)
        {
            if (graph == null || graph is StatisticsGraphError)
            {
                return null;
            }
            ChartViewData viewData = new ChartViewData(title, graphType);
            if (graph is StatisticsGraphData data)
            {
                String json = data.JsonData;
                try
                {
                    viewData.chartData = createChartData(JsonObject.Parse(json), graphType);
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
                catch (Exception e)
                {
                    //e.printStackTrace();
                    return null;
                }
            }
            else if (graph is StatisticsGraphAsync async)
            {
                viewData.token = async.Token;
            }

            return viewData;
        }

        private static ChartData createChartData(JsonObject jsonObject, int graphType)
        {
            if (graphType == 0 || graphType == 5)
            {
                return new ChartData(jsonObject);
            }
            else if (graphType == 1 || graphType == 6)
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

        public async Task LoadAsync(IProtoService protoService, long chatId)
        {
            var graph = await protoService.SendAsync(new GetChatStatisticsGraph(chatId, token, 0)) as StatisticsGraph;
            var viewData = this;

            if (graph == null || graph is StatisticsGraphError)
            {
                return;
            }
            if (graph is StatisticsGraphData data)
            {
                String json = data.JsonData;
                try
                {
                    viewData.chartData = createChartData(JsonObject.Parse(json), graphType);
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
                catch (Exception e)
                {
                    //e.printStackTrace();
                }
            }
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
