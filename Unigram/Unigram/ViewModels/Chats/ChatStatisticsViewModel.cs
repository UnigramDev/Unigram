using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Charts.Data;
using Unigram.Collections;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
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

        public MvxObservableCollection<ChartViewData> Items { get; private set; }
        public MvxObservableCollection<MessageInteractionCounters> Interactions { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var chatId = (long)parameter;

            Chat = ProtoService.GetChat(chatId);
            Delegate?.UpdateChat(Chat);



            var response = await ProtoService.SendAsync(new GetChatStatistics(chatId, false));
            if (response is ChatStatistics statistics)
            {
                Statistics = statistics;

                var stats = new List<ChartViewData>(9);
                stats.Add(ChartViewData.create(statistics.MemberCountGraph, Strings.Resources.GrowthChartTitle, 0));
                stats.Add(ChartViewData.create(statistics.JoinGraph, Strings.Resources.FollowersChartTitle, 0));
                stats.Add(ChartViewData.create(statistics.MuteGraph, Strings.Resources.NotificationsChartTitle, 0));
                stats.Add(ChartViewData.create(statistics.ViewCountByHourGraph, Strings.Resources.TopHoursChartTitle, /*0*/5));
                stats.Add(ChartViewData.create(statistics.ViewCountBySourceGraph, Strings.Resources.ViewsBySourceChartTitle, 2));
                stats.Add(ChartViewData.create(statistics.JoinBySourceGraph, Strings.Resources.NewFollowersBySourceChartTitle, 2));
                stats.Add(ChartViewData.create(statistics.LanguageGraph, Strings.Resources.LanguagesChartTitle, 4));
                stats.Add(ChartViewData.create(statistics.MessageInteractionGraph, Strings.Resources.InteractionsChartTitle, /*1*/6));
                stats.Add(ChartViewData.create(statistics.InstantViewInteractionGraph, Strings.Resources.IVInteractionsChartTitle, /*1*/6));

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

                var messages = await ProtoService.SendAsync(new GetMessages(chatId, statistics.RecentMessageInteractions.Select(x => x.MessageId).ToArray())) as Messages;
                if (messages == null)
                {
                    return;
                }

                var interactions = new List<MessageInteractionCounters>(messages.MessagesValue.Count);

                foreach (var message in messages.MessagesValue)
                {
                    var counters = statistics.RecentMessageInteractions.FirstOrDefault(x => x.MessageId == message.Id);
                    interactions.Add(new MessageInteractionCounters(message, counters.ForwardCount, counters.ViewCount));
                }

                Interactions.ReplaceWith(interactions);

                //foreach (var item in stats)
                //{
                //    var model =
                //}
            }
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
