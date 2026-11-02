//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Text;
using System.Threading.Tasks;
using Telegram.Native.Widgets;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Common.Widgets
{
    // One running widget and the message it belongs to. Storage commits and requests are bound to this
    // message here, not to whatever message the bubble shows by the time they arrive: the last ones come
    // in while the view closes, after the bubble has moved on.
    public sealed class WidgetSession
    {
        private readonly IClientService _clientService;
        private readonly WidgetStore _store;

        public WidgetSession(IClientService clientService, WidgetView view, WidgetKey key, ulong hash, WidgetStore store)
        {
            _clientService = clientService;
            View = view;
            Key = key;
            Hash = hash;
            _store = store;

            View.StorageCommitted += OnStorageCommitted;
            View.RequestReceived += OnRequestReceived;
        }

        public WidgetView View { get; }

        public WidgetKey Key { get; }

        // Of the HTML it runs, for an edit to compare against and for the crash count.
        public ulong Hash { get; }

        // What it was last given, so a bubble that takes it back from the pool sends only what changed.
        public double Width { get; set; }

        public WidgetThemeKey Theme { get; set; }

        public string ChatData { get; set; }

        public string PublicData { get; set; }

        public WidgetContentSafeArea SafeArea { get; set; }

        public void SetData(string chatData, string publicData)
        {
            if (ChatData != chatData || PublicData != publicData)
            {
                ChatData = chatData;
                PublicData = publicData;
                View.SetData(chatData ?? string.Empty, publicData ?? string.Empty);
            }
        }

        // The client only ever sets the band and one corner.
        public void SetSafeArea(WidgetContentSafeArea area)
        {
            var current = SafeArea;
            if (current.Bottom != area.Bottom || current.CornerLeft != area.CornerLeft || current.CornerRight != area.CornerRight)
            {
                SafeArea = area;
                View.SetContentSafeArea(area);
            }
        }

        // Joins the widget's threads, so it does not run on the UI thread. The commits still on their
        // way arrive during Close and are flushed after it.
        public void CloseInBackground()
        {
            _ = Task.Run(Close);
        }

        private void Close()
        {
            View.Dispose();
            View.StorageCommitted -= OnStorageCommitted;
            View.RequestReceived -= OnRequestReceived;
            _store.Flush();
        }

        // FNV-1a over the UTF-8 HTML.
        public static ulong HashOf(byte[] html)
        {
            ulong hash = 14695981039346656037;
            for (int i = 0; i < html.Length; i++)
            {
                hash = unchecked((hash ^ html[i]) * 1099511628211);
            }

            return hash;
        }

        // The widget's host thread.
        private void OnStorageCommitted(WidgetView sender, WidgetStorageCommit args)
        {
            _store.Commit(Key, args.Area, args.Values);
        }

        // The widget's host thread. The answer carries no state and nothing to show: the widget shows it, and the
        // bot changes the state with its own edits (widget-sandbox docs/integration.md, "The widget's state and requests").
        private void OnRequestReceived(WidgetView sender, WidgetRequest args)
        {
            // With the wallet, the server attaches the user's wallet card to the bot's update, and creates the wallet if there is none.
            _clientService.Send(new GetWidgetQueryAnswer(Key.ChatId, Key.MessageId, new WidgetQueryTypeData(args.Wallet), Encoding.UTF8.GetBytes(args.Data)), response =>
            {
                if (response is WidgetQueryAnswer answer)
                {
                    args.Complete(answer.Data ?? string.Empty);
                }
                else
                {
                    args.Fail(Failure(response));
                }
            });
        }

        // What a query that got no answer fails the widget's call with.
        public static WidgetRequestFailure Failure(object response)
        {
            // 502: the bot did not answer before the query timed out.
            return response is Error error
                ? error.Code switch
                {
                    502 => WidgetRequestFailure.Timeout,
                    429 => WidgetRequestFailure.RateLimited,
                    _ => WidgetRequestFailure.Failed
                }
                : WidgetRequestFailure.Failed;
        }
    }
}
