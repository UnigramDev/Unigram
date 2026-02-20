#pragma once

#include "Media/AsyncMediaPlayerLogEventArgs.g.h"

namespace winrt::Telegram::Native::Media::implementation
{
    struct AsyncMediaPlayerLogEventArgs : AsyncMediaPlayerLogEventArgsT<AsyncMediaPlayerLogEventArgs>
    {
        AsyncMediaPlayerLogEventArgs(AsyncMediaPlayerLogLevel level, hstring message, hstring module, hstring sourceFile, uint32_t sourceLine);

        AsyncMediaPlayerLogLevel Level();
        hstring Message();
        hstring Module();
        hstring SourceFile();
        uint32_t SourceLine();

    private:
        AsyncMediaPlayerLogLevel m_level;
        hstring m_message;
        hstring m_module;
        hstring m_sourceFile;
        uint32_t m_sourceLine;
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct AsyncMediaPlayerLogEventArgs : AsyncMediaPlayerLogEventArgsT<AsyncMediaPlayerLogEventArgs, implementation::AsyncMediaPlayerLogEventArgs>
    {
    };
}
