using System;
using System.Diagnostics;
using Telegram.Api.Native.Diagnostics;

namespace Telegram.Api.Native.Test
{

    public sealed class DebugLogger : ILogger
    {
        #region methods
        public void Log(LogLevel logLevel, string message)
        {
            Debug.WriteLine($"[{logLevel} - {DateTime.UtcNow}] {message}");
        }
        #endregion
    }

}
