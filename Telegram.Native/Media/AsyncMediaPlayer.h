#pragma once

#include "Media/AsyncMediaPlayer.g.h"
#include "Media/MediaEngine.h"

#include <memory>
#include <mutex>

#include <winrt/Windows.Media.Devices.h>
#include <winrt/Windows.Foundation.h>

using namespace winrt::Windows::Media::Devices;
using namespace winrt::UIDispatching;

namespace winrt::Telegram::Native::Media::implementation
{
    struct AsyncMediaPlayer : AsyncMediaPlayerT<AsyncMediaPlayer>
    {
        AsyncMediaPlayer(AsyncMediaPlayerOptions const& options, AsyncMediaPlayerSwapChain const& context = nullptr);
        ~AsyncMediaPlayer();

        AsyncMediaPlayerSwapChain Context();

        void Play(IAsyncMediaPlayerSource stream, double position = 0);
        void Play(winrt::Windows::Foundation::Collections::IVectorView<IAsyncMediaPlayerRendition> const& renditions, int32_t rendition, bool automatic, double position);
        void Play();
        void Stop();
        void Pause(bool pause = true);
        void Toggle();
        void Close();

        AsyncMediaPlayerState State();
        bool IsPlaying();

        double Duration();

        double Position();
        void Position(double value);

        void Seek(double value, bool relative);
        void SetLoop(double start, double end);

        double Rate();
        void Rate(double value);

        double Volume();
        void Volume(double value);

        bool Mute();
        void Mute(bool value);

        int32_t Rendition();
        bool IsRenditionAutomatic();
        void SelectRendition(int32_t rendition);

        winrt::event_token StateChanged(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Telegram::Native::Media::AsyncMediaPlayerStateChangedEventArgs> const& value);
        void StateChanged(winrt::event_token const& token);

        winrt::event_token VideoOut(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable> const& value);
        void VideoOut(winrt::event_token const& token);

        winrt::event_token StreamSelected(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Telegram::Native::Media::AsyncMediaPlayerStreamSelectedEventArgs> const& value);
        void StreamSelected(winrt::event_token const& token);

        winrt::event_token EndReached(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable> const& value);
        void EndReached(winrt::event_token const& token);

        winrt::event_token Buffering(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Telegram::Native::Media::AsyncMediaPlayerBufferingEventArgs> const& value);
        void Buffering(winrt::event_token const& token);

        winrt::event_token PositionChanged(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Telegram::Native::Media::AsyncMediaPlayerPositionChangedEventArgs> const& value);
        void PositionChanged(winrt::event_token const& token);

        winrt::event_token DurationChanged(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Telegram::Native::Media::AsyncMediaPlayerDurationChangedEventArgs> const& value);
        void DurationChanged(winrt::event_token const& token);

        winrt::event_token Playing(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable> const& value);
        void Playing(winrt::event_token const& token);

        winrt::event_token Paused(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable> const& value);
        void Paused(winrt::event_token const& token);

        winrt::event_token Stopped(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable> const& value);
        void Stopped(winrt::event_token const& token);

        winrt::event_token VolumeChanged(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable> const& value);
        void VolumeChanged(winrt::event_token const& token);

        winrt::event_token EncounteredError(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable> const& value);
        void EncounteredError(winrt::event_token const& token);

        winrt::event_token RenditionChanged(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable> const& value);
        void RenditionChanged(winrt::event_token const& token);

    private:
        DispatcherQueue m_dispatcherQueue{ nullptr };
        AsyncMediaPlayerSwapChain m_context{ nullptr };
        winrt::event_token m_defaultAudioRenderDeviceChanged{};

        AsyncMediaPlayerStateChangedEventArgs m_stateChangedEventArgs;
        AsyncMediaPlayerBufferingEventArgs m_bufferingEventArgs;
        AsyncMediaPlayerPositionChangedEventArgs m_positionChangedEventArgs;
        AsyncMediaPlayerDurationChangedEventArgs m_durationChangedEventArgs;

        std::shared_ptr<MediaEngine> m_engine;

        struct EngineEvents;

        std::mutex m_closeLock;
        bool m_closed{ false };

        void OnDefaultAudioRenderDeviceChanged(winrt::Windows::Foundation::IInspectable const& sender, DefaultAudioRenderDeviceChangedEventArgs const& args);

        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Telegram::Native::Media::AsyncMediaPlayerStateChangedEventArgs>> m_stateChanged;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable>> m_videoOut;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Telegram::Native::Media::AsyncMediaPlayerStreamSelectedEventArgs>> m_streamSelected;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable>> m_endReached;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Telegram::Native::Media::AsyncMediaPlayerBufferingEventArgs>> m_buffering;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Telegram::Native::Media::AsyncMediaPlayerPositionChangedEventArgs>> m_positionChanged;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Telegram::Native::Media::AsyncMediaPlayerDurationChangedEventArgs>> m_durationChanged;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable>> m_playing;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable>> m_paused;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable>> m_stopped;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable>> m_volumeChanged;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable>> m_encounteredError;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Windows::Foundation::IInspectable>> m_renditionChanged;
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct AsyncMediaPlayer : AsyncMediaPlayerT<AsyncMediaPlayer, implementation::AsyncMediaPlayer>
    {
    };
}
