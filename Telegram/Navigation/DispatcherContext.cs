//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Telegram.Navigation
{
    public interface IDispatcherContext
    {
        void Dispatch(DispatcherQueueHandler action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal);
        Task DispatchAsync(Func<Task> func, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal);
        Task DispatchAsync(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal);
        Task<T> DispatchAsync<T>(Func<T> func, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal);
        Task<T> DispatchAsync<T>(Func<Task<T>> func, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal);

        bool HasThreadAccess { get; }
    }

    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-DispatcherWrapper
    public class DispatcherContext : IDispatcherContext
    {
        public static IDispatcherContext Current => WindowContext.Current.Dispatcher;

        public DispatcherContext(DispatcherQueue dispatcher)
        {
            Logger.Info("Constructor");
            _dispatcher = dispatcher;
        }

        public bool HasThreadAccess => _dispatcher.HasThreadAccess;

        private readonly DispatcherQueue _dispatcher;

        [DebuggerNonUserCode]
        public Task DispatchAsync(Action action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            if (_dispatcher.HasThreadAccess && priority == DispatcherQueuePriority.Normal)
            {
                action();
                return Task.CompletedTask;
            }
            else
            {
                var tcs = new TaskCompletionSource<bool>();
                var result = _dispatcher.TryEnqueue(priority, () =>
                {
                    try
                    {
                        action();
                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

                if (result)
                {
                    return tcs.Task;
                }

                return Task.CompletedTask;
            }
        }

        [DebuggerNonUserCode]
        public Task DispatchAsync(Func<Task> func, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            if (_dispatcher.HasThreadAccess && priority == DispatcherQueuePriority.Normal)
            {
                return func();
            }
            else
            {
                var tcs = new TaskCompletionSource<bool>();
                var result = _dispatcher.TryEnqueue(priority, async () =>
                {
                    try
                    {
                        await func();
                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

                if (result)
                {
                    return tcs.Task;
                }

                return Task.CompletedTask;
            }
        }

        [DebuggerNonUserCode]
        public Task<T> DispatchAsync<T>(Func<T> func, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            if (_dispatcher.HasThreadAccess && priority == DispatcherQueuePriority.Normal)
            {
                return Task.FromResult(func());
            }
            else
            {
                var tcs = new TaskCompletionSource<T>();
                var result = _dispatcher.TryEnqueue(priority, () =>
                {
                    try
                    {
                        tcs.TrySetResult(func());
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });

                if (result)
                {
                    return tcs.Task;
                }
                else
                {
                    return Task.FromResult<T>(default);
                }
            }
        }

        [DebuggerNonUserCode]
        public Task<T> DispatchAsync<T>(Func<Task<T>> func, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            if (_dispatcher.HasThreadAccess && priority == DispatcherQueuePriority.Normal)
            {
                return func();
            }
            else
            {
                var tcs = new TaskCompletionSource<T>();
                var result = _dispatcher.TryEnqueue(priority, async () =>
                {
                    try
                    {
                        tcs.TrySetResult(await func());
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });
                if (result)
                {
                    return tcs.Task;
                }
                else
                {
                    return Task.FromResult<T>(default);
                }
            }
        }

        [DebuggerNonUserCode]
        public void Dispatch(DispatcherQueueHandler action, DispatcherQueuePriority priority = DispatcherQueuePriority.Normal)
        {
            if (_dispatcher.HasThreadAccess && priority == DispatcherQueuePriority.Normal)
            {
                action();
            }
            else
            {
                try
                {
                    _dispatcher.TryEnqueue(priority, action);
                }
                catch
                {
                    // Most likey Excep_InvalidComObject_NoRCW_Wrapper, so we can just ignore it
                }
            }
        }
    }
}
