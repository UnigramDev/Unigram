using System;
using System.Runtime.CompilerServices;

namespace Unigram.Services.Logging
{
    public delegate void DebugWriteDelegate(string text = null, Severities severity = Severities.Info, Targets target = Targets.Debug, [CallerMemberName] string caller = null);

    public enum Severities { Template10, Info, Warning, Error, Critical }

    public enum Targets { Debug, Log }

    public static class LoggingService
    {
        public static bool Enabled { get; set; } = true;

        public static DebugWriteDelegate WriteLine { get; set; } = new DebugWriteDelegate(WriteLineInternal);

        private static void WriteLineInternal(string text = null, Severities severity = Severities.Info, Targets target = Targets.Debug, [CallerMemberName] string caller = null)
        {
            switch (target)
            {
                case Targets.Debug:
                    if (Enabled)
                    {
                        System.Diagnostics.Debug.WriteLine($"{DateTime.Now.TimeOfDay.ToString()} {severity} {caller} {text}");
                    }
                    break;
                case Targets.Log:
                    throw new NotImplementedException();
            }
        }
    }
}
