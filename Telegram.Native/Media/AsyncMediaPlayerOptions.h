#pragma once

#include "Media/AsyncMediaPlayerOptions.g.h"

#include <winrt/Windows.Foundation.Collections.h>

namespace winrt::Telegram::Native::Media::implementation
{
    struct AsyncMediaPlayerOptions : AsyncMediaPlayerOptionsT<AsyncMediaPlayerOptions>
    {
        AsyncMediaPlayerOptions();

        AsyncMediaPlayerMode Mode() const;
        void Mode(AsyncMediaPlayerMode value);

        bool Debug() const;
        void Debug(bool value);

        bool CreateSwapChain() const;
        void CreateSwapChain(bool value);

        bool Mute() const;
        void Mute(bool value);

        double Volume() const;
        void Volume(double value);

        double Rate() const;
        void Rate(double value);

        winrt::Windows::Foundation::Collections::IVector<hstring> Arguments();

    private:
        AsyncMediaPlayerMode m_mode;
        bool m_debug;
        bool m_createSwapChain;
        bool m_mute;
        double m_volume;
        double m_rate;
        winrt::Windows::Foundation::Collections::IVector<hstring> m_arguments;
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct AsyncMediaPlayerOptions : AsyncMediaPlayerOptionsT<AsyncMediaPlayerOptions, implementation::AsyncMediaPlayerOptions>
    {
    };
}
