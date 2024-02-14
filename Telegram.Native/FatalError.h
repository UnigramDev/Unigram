#pragma once

#include "FatalError.g.h"

namespace winrt::Telegram::Native::implementation
{
    struct FatalError : FatalErrorT<FatalError>
    {
        FatalError(int32_t resultCode, hstring message, hstring stackTrace, winrt::Windows::Foundation::Collections::IVector<FatalErrorFrame> frames)
            : m_resultCode(resultCode)
            , m_message(message)
            , m_stackTrace(stackTrace)
            , m_frames(frames)
        {

        }

        int32_t ResultCode()
        {
            return m_resultCode;
        }

        hstring Message()
        {
            return m_message;
        }

        hstring StackTrace()
        {
            return m_stackTrace;
        }

        winrt::Windows::Foundation::Collections::IVector<FatalErrorFrame> Frames()
        {
            return m_frames;
        }

    private:
        int32_t m_resultCode;
        hstring m_message;
        hstring m_stackTrace;
        winrt::Windows::Foundation::Collections::IVector<FatalErrorFrame> m_frames;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct FatalError : FatalErrorT<FatalError, implementation::FatalError>
    {
    };
}
