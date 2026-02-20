#include "pch.h"
#include "AsyncMediaPlayerLogEventArgs.h"
#if __has_include("Media/AsyncMediaPlayerLogEventArgs.g.cpp")
#include "Media/AsyncMediaPlayerLogEventArgs.g.cpp"
#endif

namespace winrt::Telegram::Native::Media::implementation
{
    AsyncMediaPlayerLogEventArgs::AsyncMediaPlayerLogEventArgs(AsyncMediaPlayerLogLevel level, hstring message, hstring module, hstring sourceFile, uint32_t sourceLine)
        : m_level(level)
        , m_message(message)
        , m_module(module)
        , m_sourceFile(sourceFile)
        , m_sourceLine(sourceLine)
    {

    }

    AsyncMediaPlayerLogLevel AsyncMediaPlayerLogEventArgs::Level()
    {
        return m_level;
    }

    hstring AsyncMediaPlayerLogEventArgs::Message()
    {
        return m_message;
    }

    hstring AsyncMediaPlayerLogEventArgs::Module()
    {
        return m_module;
    }

    hstring AsyncMediaPlayerLogEventArgs::SourceFile()
    {
        return m_sourceFile;
    }

    uint32_t AsyncMediaPlayerLogEventArgs::SourceLine()
    {
        return m_sourceLine;
    }
}
