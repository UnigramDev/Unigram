#include "pch.h"
#include "AsyncMediaPlayerOptions.h"
#if __has_include("Media/AsyncMediaPlayerOptions.g.cpp")
#include "Media/AsyncMediaPlayerOptions.g.cpp"
#endif

namespace winrt::Telegram::Native::Media::implementation
{
    AsyncMediaPlayerOptions::AsyncMediaPlayerOptions()
        : m_mode(AsyncMediaPlayerMode::Audio | AsyncMediaPlayerMode::Video)
        , m_debug(false)
        , m_createSwapChain(false)
        , m_mute(false)
        , m_volume(1)
        , m_rate(1)
        , m_arguments(winrt::single_threaded_vector<hstring>())
    {

    }

    AsyncMediaPlayerMode AsyncMediaPlayerOptions::Mode() const
    {
        return m_mode;
    }

    void AsyncMediaPlayerOptions::Mode(AsyncMediaPlayerMode value)
    {
        m_mode = value;
    }

    bool AsyncMediaPlayerOptions::Debug() const
    {
        return m_debug;
    }

    void AsyncMediaPlayerOptions::Debug(bool value)
    {
        m_debug = value;
    }

    bool AsyncMediaPlayerOptions::CreateSwapChain() const
    {
        return m_createSwapChain;
    }

    void AsyncMediaPlayerOptions::CreateSwapChain(bool value)
    {
        m_createSwapChain = value;
    }

    bool AsyncMediaPlayerOptions::Mute() const
    {
        return m_mute;
    }

    void AsyncMediaPlayerOptions::Mute(bool value)
    {
        m_mute = value;
    }

    double AsyncMediaPlayerOptions::Volume() const
    {
        return m_volume;
    }

    void AsyncMediaPlayerOptions::Volume(double value)
    {
        m_volume = value;
    }

    double AsyncMediaPlayerOptions::Rate() const
    {
        return m_rate;
    }

    void AsyncMediaPlayerOptions::Rate(double value)
    {
        m_rate = value;
    }

    winrt::Windows::Foundation::Collections::IVector<hstring> AsyncMediaPlayerOptions::Arguments()
    {
        return m_arguments;
    }
}
