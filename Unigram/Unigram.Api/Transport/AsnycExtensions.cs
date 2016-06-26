using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Telegram.Api.Transport
{
    public static class AsyncExtensions
    {
        public static async Task WithTimeout(this IAsyncAction task, double timeout)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            await WindowsRuntimeSystemExtensions.AsTask(task, cancellationTokenSource.Token);
        }

        public static async Task<T> WithTimeout<T>(this IAsyncOperation<T> task, double timeout)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            return await WindowsRuntimeSystemExtensions.AsTask<T>(task, cancellationTokenSource.Token);
        }
    }
}
