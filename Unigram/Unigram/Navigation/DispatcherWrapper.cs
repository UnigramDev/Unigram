using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace Unigram.Navigation
{
    public interface IDispatcherWrapper
    {
        void Dispatch(Action action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal);
        Task DispatchAsync(Func<Task> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal);
        Task DispatchAsync(Action action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal);
        Task<T> DispatchAsync<T>(Func<T> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal);

        void DispatchIdle(Action action);
        Task DispatchIdleAsync(Func<Task> func);
        Task DispatchIdleAsync(Action action);
        Task<T> DispatchIdleAsync<T>(Func<T> func);

        bool HasThreadAccess { get; }
    }

    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-DispatcherWrapper
    public class DispatcherWrapper : IDispatcherWrapper
    {
        #region Debug

        [Conditional("DEBUG")]
        static void DebugWrite(string text = null, Unigram.Services.Logging.Severities severity = Unigram.Services.Logging.Severities.Template10, [CallerMemberName] string caller = null) =>
            Unigram.Services.Logging.LoggingService.WriteLine(text, severity, caller: $"DispatcherWrapper.{caller}");

        #endregion

        public static IDispatcherWrapper Current() => WindowContext.GetForCurrentView().Dispatcher;

        public DispatcherWrapper(CoreDispatcher dispatcher)
        {
            DebugWrite(caller: "Constructor");
            this.dispatcher = dispatcher;
        }

        public bool HasThreadAccess => dispatcher.HasThreadAccess;

        private readonly CoreDispatcher dispatcher;

        [DebuggerNonUserCode]
        public async Task DispatchAsync(Action action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (dispatcher.HasThreadAccess && priority == CoreDispatcherPriority.Normal)
            {
                action();
            }
            else
            {
                var tcs = new TaskCompletionSource<object>();
                await dispatcher.RunAsync(priority, () =>
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
        }

        [DebuggerNonUserCode]
        public async Task DispatchAsync(Func<Task> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (dispatcher.HasThreadAccess && priority == CoreDispatcherPriority.Normal)
            {
                await func().ConfigureAwait(false);
            }
            else
            {
                var tcs = new TaskCompletionSource<object>();
                await dispatcher.RunAsync(priority, async () =>
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
        }

        [DebuggerNonUserCode]
        public async Task<T> DispatchAsync<T>(Func<T> func, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (dispatcher.HasThreadAccess && priority == CoreDispatcherPriority.Normal)
            {
                return func();
            }
            else
            {
                var tcs = new TaskCompletionSource<T>();
                await dispatcher.RunAsync(priority, () =>
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
        }

        [DebuggerNonUserCode]
        public async void Dispatch(Action action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (dispatcher.HasThreadAccess && priority == CoreDispatcherPriority.Normal)
            {
                action();
            }
            else
            {
                //dispatcher.RunAsync(priority, () => action()).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                await dispatcher.RunAsync(priority, () => action());
            }
        }

        [DebuggerNonUserCode]
        public T Dispatch<T>(Func<T> action, CoreDispatcherPriority priority = CoreDispatcherPriority.Normal)
        {
            if (dispatcher.HasThreadAccess && priority == CoreDispatcherPriority.Normal)
            {
                return action();
            }
            else
            {
                var tcs = new TaskCompletionSource<T>();
                dispatcher.RunAsync(priority, delegate
                {
                    try
                    {
                        tcs.TrySetResult(action());
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                return tcs.Task.ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        [DebuggerNonUserCode]
        public async Task DispatchIdleAsync(Action action)
        {
            var tcs = new TaskCompletionSource<object>();
            await dispatcher.RunIdleAsync(delegate
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
            await dispatcher.RunIdleAsync(async delegate
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
            await dispatcher.RunIdleAsync(delegate
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
        public async void DispatchIdle(Action action)
        {
            dispatcher.RunIdleAsync(args => action()).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        [DebuggerNonUserCode]
        public T DispatchIdle<T>(Func<T> action) where T : class
        {
            var tcs = new TaskCompletionSource<T>();
            dispatcher.RunIdleAsync(delegate
            {
                try
                {
                    tcs.TrySetResult(action());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            return tcs.Task.ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
