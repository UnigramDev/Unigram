using System;

namespace Unigram.Logs
{
    public interface ILogListener
    {
        void Log(LogLevel level, Type type, string message, string member, string filePath, int line);
        void Log(LogLevel level, string message, string member, string filePath, int line);
    }
}
