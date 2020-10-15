using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Unigram.Navigation
{
    public interface IDispatcherContext
    {
        void Dispatch(Action action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal);
        Task DispatchAsync(Func<Task> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal);
        Task DispatchAsync(Action action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal);
        Task<T> DispatchAsync<T>(Func<T> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal);
        Task<T> DispatchAsync<T>(Func<Task<T>> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal);

        void DispatchIdle(Action action);
        Task DispatchIdleAsync(Func<Task> func);
        Task DispatchIdleAsync(Action action);
        Task<T> DispatchIdleAsync<T>(Func<Task<T>> func);

        bool HasThreadAccess { get; }
    }

    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-DispatcherWrapper
    public class DispatcherContext : IDispatcherContext
    {
        #region Debug

        [Conditional("DEBUG")]
        static void DebugWrite(string text = null, Unigram.Services.Logging.Severities severity = Unigram.Services.Logging.Severities.Template10, [CallerMemberName] string caller = null) =>
            Unigram.Services.Logging.LoggingService.WriteLine(text, severity, caller: $"DispatcherWrapper.{caller}");

        #endregion

        public static IDispatcherContext Current() => WindowContext.GetForCurrentView().Dispatcher;

        public DispatcherContext(CoreDispatcher dispatcher)
        {
            DebugWrite(caller: "Constructor");
            this._dispatcher = dispatcher;
        }

        public bool HasThreadAccess => _dispatcher.HasThreadAccess;

        private readonly CoreDispatcher _dispatcher;

        [DebuggerNonUserCode]
        public async Task DispatchAsync(Action action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (_dispatcher.HasThreadAccess && priority == CoreDispatcherPriority.Normal)
            {
                action();
            }
            else
            {
                var tcs = new TaskCompletionSource<object>();
                await _dispatcher.RunAsync(priority, () =>
                {
                    try
                    {
                        action();
                        tcs.TrySetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }).AsTask();
                await tcs.Task;
            }
        }

        [DebuggerNonUserCode]
        public async Task DispatchAsync(Func<Task> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (_dispatcher.HasThreadAccess && priority == CoreDispatcherPriority.Normal)
            {
                await func().ConfigureAwait(false);
            }
            else
            {
                var tcs = new TaskCompletionSource<object>();
                await _dispatcher.RunAsync(priority, async () =>
                {
                    try
                    {
                        await func();
                        tcs.TrySetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });
                await tcs.Task;
            }
        }

        [DebuggerNonUserCode]
        public async Task<T> DispatchAsync<T>(Func<T> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (_dispatcher.HasThreadAccess && priority == CoreDispatcherPriority.Normal)
            {
                return func();
            }
            else
            {
                var tcs = new TaskCompletionSource<T>();
                await _dispatcher.RunAsync(priority, () =>
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
                return await tcs.Task;
            }
        }

        [DebuggerNonUserCode]
        public async Task<T> DispatchAsync<T>(Func<Task<T>> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (_dispatcher.HasThreadAccess && priority == CoreDispatcherPriority.Normal)
            {
                return await func();
            }
            else
            {
                var tcs = new TaskCompletionSource<T>();
                await _dispatcher.RunAsync(priority, async () =>
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
                return await tcs.Task;
            }
        }

        [DebuggerNonUserCode]
        public void Dispatch(Action action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (_dispatcher.HasThreadAccess && priority == CoreDispatcherPriority.Normal)
            {
                action();
            }
            else
            {
                //dispatcher.RunAsync(priority, () => action()).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                try
                {
                    _ = _dispatcher.RunAsync(priority, () => action());
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        [DebuggerNonUserCode]
        public async Task DispatchIdleAsync(Action action)
        {
            var tcs = new TaskCompletionSource<object>();
            await _dispatcher.RunIdleAsync(delegate
            {
                try
                {
                    action();
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }).AsTask().ConfigureAwait(false);
            await tcs.Task.ConfigureAwait(false);
        }

        [DebuggerNonUserCode]
        public async Task DispatchIdleAsync(Func<Task> func)
        {
            var tcs = new TaskCompletionSource<object>();
            await _dispatcher.RunIdleAsync(async delegate
            {
                try
                {
                    await func().ConfigureAwait(false);
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }).AsTask().ConfigureAwait(false);
            await tcs.Task.ConfigureAwait(false);
        }

        [DebuggerNonUserCode]
        public async Task<T> DispatchIdleAsync<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            await _dispatcher.RunIdleAsync(delegate
            {
                try
                {
                    tcs.TrySetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }).AsTask().ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }

        [DebuggerNonUserCode]
        public async Task<T> DispatchIdleAsync<T>(Func<Task<T>> func)
        {
            var tcs = new TaskCompletionSource<T>();
            await _dispatcher.RunIdleAsync(async delegate
            {
                try
                {
                    tcs.TrySetResult(await func());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }).AsTask().ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }

        [DebuggerNonUserCode]
        public void DispatchIdle(Action action)
        {
            _ = _dispatcher.RunIdleAsync(args => action());
        }
    }
}
