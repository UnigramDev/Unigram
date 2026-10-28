//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Threading;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Common.Widgets
{
    public interface IWidgetCarrier
    {
        // Any thread: the carrier moves it to its own.
        void ApplyPublicData(long widgetId, string publicData);
    }

    // Polls the shared state of shared widgets (widget-sandbox docs/integration.md, "Polling a shared widget's
    // shared state"): one call per widget id, through any one of its messages on screen whose widget watches,
    // and the answer goes to all of them. Any thread.
    public sealed class WidgetPublicDataPoller
    {
        // The server's default period, which also applies before the first answer.
        private const int DefaultPeriod = 30;
        private const int MinimumPeriod = 5;

        private static readonly Dictionary<int, WidgetPublicDataPoller> _sessions = new();

        public static WidgetPublicDataPoller For(IClientService clientService)
        {
            lock (_sessions)
            {
                if (!_sessions.TryGetValue(clientService.SessionId, out WidgetPublicDataPoller poller))
                {
                    _sessions[clientService.SessionId] = poller = new WidgetPublicDataPoller(clientService);
                }

                return poller;
            }
        }

        private sealed class Carrier
        {
            public IWidgetCarrier Target;
            public long ChatId;
            public long MessageId;
        }

        private sealed class Poll
        {
            public readonly List<Carrier> Carriers = new();
            public Timer Timer;
            public int Period = DefaultPeriod;
        }

        private readonly IClientService _clientService;
        private readonly object _lock = new();
        private readonly Dictionary<long, Poll> _polls = new();

        private WidgetPublicDataPoller(IClientService clientService)
        {
            _clientService = clientService;
        }

        public void Watch(long widgetId, IWidgetCarrier target, long chatId, long messageId)
        {
            lock (_lock)
            {
                if (!_polls.TryGetValue(widgetId, out Poll poll))
                {
                    _polls[widgetId] = poll = new Poll();
                }

                foreach (var carrier in poll.Carriers)
                {
                    if (carrier.Target == target)
                    {
                        return;
                    }
                }

                poll.Carriers.Add(new Carrier { Target = target, ChatId = chatId, MessageId = messageId });
                poll.Timer ??= new Timer(OnTimer, widgetId, poll.Period * 1000, Timeout.Infinite);
            }
        }

        public void Unwatch(long widgetId, IWidgetCarrier target)
        {
            lock (_lock)
            {
                if (!_polls.TryGetValue(widgetId, out Poll poll))
                {
                    return;
                }

                poll.Carriers.RemoveAll(x => x.Target == target);

                if (poll.Carriers.Count == 0)
                {
                    poll.Timer?.Dispose();
                    _polls.Remove(widgetId);
                }
            }
        }

        private void OnTimer(object state)
        {
            var widgetId = (long)state;
            Carrier carrier;

            lock (_lock)
            {
                if (!_polls.TryGetValue(widgetId, out Poll poll) || poll.Carriers.Count == 0)
                {
                    return;
                }

                carrier = poll.Carriers[0];
            }

            _clientService.Send(new GetWidgetPublicData(carrier.ChatId, carrier.MessageId), response =>
            {
                List<Carrier> carriers = null;

                lock (_lock)
                {
                    // Nobody watches any more: the timer went with the poll.
                    if (!_polls.TryGetValue(widgetId, out Poll poll))
                    {
                        return;
                    }

                    if (response is WidgetPublicData publicData)
                    {
                        poll.Period = publicData.PollPeriod < MinimumPeriod ? MinimumPeriod : publicData.PollPeriod;
                        carriers = new List<Carrier>(poll.Carriers);
                    }

                    poll.Timer.Change(poll.Period * 1000, Timeout.Infinite);
                }

                if (carriers != null && response is WidgetPublicData data)
                {
                    foreach (var item in carriers)
                    {
                        item.Target.ApplyPublicData(widgetId, data.Data);
                    }
                }
            });
        }
    }
}
