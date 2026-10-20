#pragma once

#include "Media/AsyncMediaPlayerOptions.g.h"

namespace winrt::Telegram::Native::Media::implementation
{
    struct AsyncMediaPlayerOptions : AsyncMediaPlayerOptionsT<AsyncMediaPlayerOptions>
    {
        AsyncMediaPlayerOptions();

        AsyncMediaPlayerMode Mode() const;
        void Mode(AsyncMediaPlayerMode value);

        bool CreateSwapChain() const;
        void CreateSwapChain(bool value);

        bool Mute() const;
        void Mute(bool value);

        double Volume() const;
        void Volume(double value);

        double Rate() const;
        void Rate(double value);

    private:
        AsyncMediaPlayerMode m_mode;
        bool m_createSwapChain;
        bool m_mute;
        double m_volume;
        double m_rate;
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct AsyncMediaPlayerOptions : AsyncMediaPlayerOptionsT<AsyncMediaPlayerOptions, implementation::AsyncMediaPlayerOptions>
    {
    };
}
