//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    public static class IncrementalLoading
    {
        public static IAsyncOperation<LoadMoreItemsResult> Run(Func<CancellationToken, Task<LoadMoreItemsResult>> taskProvider, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Telegram.Logger.Info(member: member, filePath: filePath, line: line);

            var context = SynchronizationContext.Current;
            return AsyncInfo.Run(token => RunImpl(taskProvider, context, token, member, filePath, line));
        }

        private static async Task<LoadMoreItemsResult> RunImpl(Func<CancellationToken, Task<LoadMoreItemsResult>> taskProvider, SynchronizationContext context, CancellationToken token, string member, string filePath, int line)
        {
            // AsyncInfo is no longer the .NET Native adapter, which invoked the provider inline
            // on the calling thread. If this one ever schedules instead, the state machine starts
            // off the UI thread and every await inside it resumes on the pool; nothing else in the
            // app is positioned to notice.
            if (SynchronizationContext.Current != context)
            {
                Telegram.Logger.Error("Incremental loading started off the calling context", member: member, filePath: filePath, line: line);
            }

            var task = taskProvider(token);

            // A load that completes inline with nothing to show is the pathological case: a list
            // whose HasMoreItems is still true asks again at once, on the same stack, so neither
            // the message pump nor any continuation queued on it ever gets a turn. Yielding bounds
            // that loop by the pump instead of by the stack. It costs a dispatcher turn, but only
            // here — a load that returned items, or that was asynchronous anyway, is untouched.
            if (task.Status == TaskStatus.RanToCompletion && task.Result.Count == 0)
            {
                await YieldToPump();
            }

            return await task;
        }

        // Task.Yield resumes at Normal priority, which is where the list's next request comes
        // from as well: a collection that still loops then starves the pump rather than the
        // stack, and the window stops drawing at all. Low puts input and rendering first, so a
        // list that never terminates costs a spinning core instead of a frozen app.
        private static Task YieldToPump()
        {
            var dispatcher = global::Windows.System.DispatcherQueue.GetForCurrentThread();
            if (dispatcher != null)
            {
                var tcs = new TaskCompletionSource<bool>();
                if (dispatcher.TryEnqueue(global::Windows.System.DispatcherQueuePriority.Low, () => tcs.TrySetResult(true)))
                {
                    return tcs.Task;
                }
            }

            return Task.CompletedTask;
        }
    }
}
