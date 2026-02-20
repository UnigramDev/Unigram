#pragma once

#include "FatalError.g.h"

namespace winrt::Telegram::Native::implementation
{
    struct FatalError : FatalErrorT<FatalError>
    {
        FatalError(hstring type, hstring message, hstring stackTrace, winrt::Windows::Foundation::Collections::IVector<FatalErrorFrame> frames)
            : m_type(type)
            , m_message(message)
            , m_stackTrace(stackTrace)
            , m_frames(frames)
            , m_innerException(nullptr)
        {

        }

        hstring Type()
        {
            return m_type;
        }

        void Type(hstring value)
        {
            m_type = value;
        }

        hstring Message()
        {
            return m_message;
        }

        void Message(hstring value)
        {
            m_message = value;
        }

        hstring StackTrace()
        {
            return m_stackTrace;
        }

        void StackTrace(hstring value)
        {
            m_stackTrace = value;
        }

        winrt::Windows::Foundation::Collections::IVector<FatalErrorFrame> Frames()
        {
            return m_frames;
        }

        winrt::Telegram::Native::FatalError InnerException()
        {
            return m_innerException;
        }

        void InnerException(winrt::Telegram::Native::FatalError value)
        {
            m_innerException = value;
        }

    private:
        hstring m_type;
        hstring m_message;
        hstring m_stackTrace;
        winrt::Windows::Foundation::Collections::IVector<FatalErrorFrame> m_frames;

        winrt::Telegram::Native::FatalError m_innerException;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct FatalError : FatalErrorT<FatalError, implementation::FatalError>
    {
    };
}
