#include "pch.h"
#include "AsyncMediaPlayer.h"
#if __has_include("Media/AsyncMediaPlayer.g.cpp")
#include "Media/AsyncMediaPlayer.g.cpp"
#endif

#include <winrt/Windows.Foundation.Collections.h>

namespace winrt::Telegram::Native::Media::implementation
{
    // The bytes of an IAsyncMediaPlayerSource for the engine. ReadCallback only waits until the
    // bytes are on disk, and they are read from the file it downloads into.
    class EngineSource : public IMediaByteSource
    {
    public:
        explicit EngineSource(IAsyncMediaPlayerSource source)
            : m_source(std::move(source))
        {
        }

        ~EngineSource() override
        {
            if (m_file != INVALID_HANDLE_VALUE)
            {
                CloseHandle(m_file);
            }
        }

        void Open() override
        {
            m_source.Open();
        }

        void Close() override
        {
            m_source.Close();
        }

        int64_t Size() override
        {
            return m_source.FileSize();
        }

        int64_t Read(int64_t offset, uint8_t* buffer, int64_t count, int64_t window) override
        {
            if (m_source.Offset() != offset)
            {
                m_source.SeekCallback(offset);
            }

            int64_t available = 0;
            m_source.ReadCallback(count, window, available);

            if (available < 0)
            {
                return ReadStalled;
            }
            else if (available == 0)
            {
                return 0;
            }

            if (m_file == INVALID_HANDLE_VALUE)
            {
                // Opened once and kept: TDLib moves the file when the download completes, and an
                // open handle follows it.
                m_file = CreateFile2FromAppW(m_source.FilePath().c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, OPEN_EXISTING, nullptr);

                if (m_file == INVALID_HANDLE_VALUE)
                {
                    return ReadFailed;
                }
            }

            LARGE_INTEGER position{};
            position.QuadPart = offset;

            DWORD read = 0;
            const DWORD wanted = static_cast<DWORD>(std::min<int64_t>(count, available));

            if (!SetFilePointerEx(m_file, position, nullptr, FILE_BEGIN) || !ReadFile(m_file, buffer, wanted, &read, nullptr) || read == 0)
            {
                return ReadFailed;
            }

            m_source.SeekCallback(offset + read);
            return read;
        }

    private:
        IAsyncMediaPlayerSource m_source;
        HANDLE m_file{ INVALID_HANDLE_VALUE };
    };

    class EngineRendition : public IMediaRendition
    {
    public:
        explicit EngineRendition(IAsyncMediaPlayerRendition rendition)
            : m_rendition(std::move(rendition))
        {
        }

        int32_t Width() override
        {
            return m_rendition.Width();
        }

        int32_t Height() override
        {
            return m_rendition.Height();
        }

        int64_t Size() override
        {
            return m_rendition.Size();
        }

        bool HasPlaylist() override
        {
            return m_rendition.HasPlaylist();
        }

        std::string LoadPlaylist() override
        {
            return winrt::to_string(m_rendition.LoadPlaylist());
        }

        void CancelPlaylist() override
        {
            m_rendition.CancelPlaylist();
        }

        std::shared_ptr<IMediaByteSource> CreateSource() override
        {
            auto source = m_rendition.CreateSource();
            return source ? std::make_shared<EngineSource>(source) : nullptr;
        }

        double DownloadSpeed() override
        {
            return m_rendition.DownloadSpeed();
        }

    private:
        IAsyncMediaPlayerRendition m_rendition;
    };

    // What the engine reports, raised on the thread the player was created on. The player is
    // resolved only once there: a strong reference taken on one of the engine's threads could be
    // the last, and destroy the player on the very thread it would join.
    struct AsyncMediaPlayer::EngineEvents : IMediaEngineHost
    {
        EngineEvents(winrt::weak_ref<AsyncMediaPlayer> weak, DispatcherQueue dispatcherQueue)
            : m_weak(std::move(weak))
            , m_dispatcherQueue(std::move(dispatcherQueue))
        {
        }

        void StateChanged(MediaEngineState state) override
        {
            Post([state](AsyncMediaPlayer& player)
                {
                    player.m_stateChangedEventArgs.State(static_cast<AsyncMediaPlayerState>(state));
                    player.m_stateChanged(player, player.m_stateChangedEventArgs);

                    switch (state)
                    {
                    case MediaEngineState::Playing:
                        player.m_playing(player, nullptr);
                        break;
                    case MediaEngineState::Paused:
                        player.m_paused(player, nullptr);
                        break;
                    case MediaEngineState::Stopped:
                        player.m_stopped(player, nullptr);
                        break;
                    default:
                        break;
                    }
                });
        }

        void PositionChanged(double position) override
        {
            Post([position](AsyncMediaPlayer& player)
                {
                    player.m_positionChangedEventArgs.Position(position);
                    player.m_positionChanged(player, player.m_positionChangedEventArgs);
                });
        }

        void DurationChanged(double duration) override
        {
            Post([duration](AsyncMediaPlayer& player)
                {
                    player.m_durationChangedEventArgs.Duration(duration);
                    player.m_durationChanged(player, player.m_durationChangedEventArgs);
                });
        }

        void Buffering(float progress) override
        {
            Post([progress](AsyncMediaPlayer& player)
                {
                    player.m_bufferingEventArgs.Cache(progress);
                    player.m_buffering(player, player.m_bufferingEventArgs);
                });
        }

        void StreamSelected(MediaStreamKind kind, int32_t id, int32_t width, int32_t height) override
        {
            Post([=](AsyncMediaPlayer& player)
                {
                    player.m_streamSelected(player, AsyncMediaPlayerStreamSelectedEventArgs(id, static_cast<AsyncMediaPlayerStreamType>(kind), width, height));
                });
        }

        void VideoOut() override
        {
            Post([](AsyncMediaPlayer& player)
                {
                    player.m_videoOut(player, nullptr);
                });
        }

        void EndReached() override
        {
            Post([](AsyncMediaPlayer& player)
                {
                    player.m_endReached(player, nullptr);
                });
        }

        void EncounteredError() override
        {
            Post([](AsyncMediaPlayer& player)
                {
                    player.m_encounteredError(player, nullptr);
                });
        }

        void VolumeChanged() override
        {
            Post([](AsyncMediaPlayer& player)
                {
                    player.m_volumeChanged(player, nullptr);
                });
        }

        void RenditionChanged() override
        {
            Post([](AsyncMediaPlayer& player)
                {
                    player.m_renditionChanged(player, nullptr);
                });
        }

    private:
        template <typename Func>
        void Post(Func&& func)
        {
            DispatcherQueueHandler handler = [weak = m_weak, func = std::forward<Func>(func)]()
                {
                    if (auto strong = weak.get())
                    {
                        func(*strong);
                    }
                };

            try
            {
                if (m_dispatcherQueue)
                {
                    m_dispatcherQueue.TryEnqueue(handler);
                }
                else
                {
                    RunOnThreadPool([handler = std::move(handler)]()
                        {
                            handler();
                        });
                }
            }
            catch (...)
            {
                // Likely Window teardown
            }
        }

        winrt::weak_ref<AsyncMediaPlayer> m_weak;
        DispatcherQueue m_dispatcherQueue{ nullptr };
    };

    AsyncMediaPlayer::AsyncMediaPlayer(AsyncMediaPlayerOptions const& options, AsyncMediaPlayerSwapChain const& context)
        : m_dispatcherQueue(DispatcherQueue::GetForCurrentThread())
        , m_context(context)
        , m_stateChangedEventArgs(AsyncMediaPlayerState::NothingSpecial)
        , m_bufferingEventArgs(0.0f)
        , m_positionChangedEventArgs(0.0)
        , m_durationChangedEventArgs(0.0)
    {
        const auto mode = options.Mode();
        const bool audio = (mode & AsyncMediaPlayerMode::Audio) != AsyncMediaPlayerMode::None;
        const bool video = (mode & AsyncMediaPlayerMode::Video) != AsyncMediaPlayerMode::None;

        if (options.CreateSwapChain() && !m_context)
        {
            m_context = AsyncMediaPlayerSwapChain(true);
        }

        std::shared_ptr<MediaVideoRenderer> renderer;

        // A swap chain that failed to come up is not a reason to fail construction: players are
        // created from XAML Loaded handlers, and the Direct3D device can legitimately be
        // unavailable while the display driver is resetting. AsyncMediaPlayerSwapChain::Create
        // logs why it failed.
        if (video && m_context && m_context.IsLoaded())
        {
            renderer = std::make_shared<MediaVideoRenderer>(m_context);
        }
        else if (video && m_context)
        {
            LOGGER_ERROR(L"swap chain unavailable, playing without video");
        }

        MediaEngineOptions engineOptions;
        engineOptions.audio = audio;
        engineOptions.video = renderer != nullptr;
        engineOptions.rate = options.Rate();
        engineOptions.volume = options.Volume();
        engineOptions.mute = options.Mute();

        m_engine = MediaEngine::Create(engineOptions, std::make_shared<EngineEvents>(get_weak(), m_dispatcherQueue), std::move(renderer));

        // A static event that can fire on a system thread, so a raw this could dangle if it races
        // teardown. It is also detached in Close().
        m_defaultAudioRenderDeviceChanged = MediaDevice::DefaultAudioRenderDeviceChanged({ get_weak(), &AsyncMediaPlayer::OnDefaultAudioRenderDeviceChanged });
    }

    AsyncMediaPlayer::~AsyncMediaPlayer()
    {
        Close();
    }

    void AsyncMediaPlayer::OnDefaultAudioRenderDeviceChanged(winrt::Windows::Foundation::IInspectable const& sender, DefaultAudioRenderDeviceChangedEventArgs const& args)
    {
        if (AudioDeviceRole::Default == args.Role())
        {
            LOGGER_INFO(L"default audio render device changed, rebuilding the output");
            m_engine->AudioDeviceChanged();
        }
    }

    AsyncMediaPlayerSwapChain AsyncMediaPlayer::Context()
    {
        return m_context;
    }

    void AsyncMediaPlayer::Play(IAsyncMediaPlayerSource stream, double position)
    {
        if (stream)
        {
            m_engine->Open(std::make_shared<EngineSource>(stream), position);
        }
    }

    void AsyncMediaPlayer::Play(winrt::Windows::Foundation::Collections::IVectorView<IAsyncMediaPlayerRendition> const& renditions, int32_t rendition, bool automatic, double position)
    {
        if (renditions == nullptr || renditions.Size() == 0)
        {
            return;
        }

        std::vector<std::shared_ptr<IMediaRendition>> items;
        items.reserve(renditions.Size());

        for (auto const& item : renditions)
        {
            items.push_back(std::make_shared<EngineRendition>(item));
        }

        m_engine->Open(std::move(items), std::clamp(rendition, 0, static_cast<int32_t>(renditions.Size()) - 1), automatic, position);
    }

    int32_t AsyncMediaPlayer::Rendition()
    {
        return m_engine->Rendition();
    }

    bool AsyncMediaPlayer::IsRenditionAutomatic()
    {
        return m_engine->IsRenditionAutomatic();
    }

    void AsyncMediaPlayer::SelectRendition(int32_t rendition)
    {
        m_engine->SelectRendition(rendition);
    }

    void AsyncMediaPlayer::Play()
    {
        m_engine->Play();
    }

    void AsyncMediaPlayer::Stop()
    {
        m_engine->Stop();
    }

    void AsyncMediaPlayer::Pause(bool pause)
    {
        m_engine->Pause(pause);
    }

    void AsyncMediaPlayer::Toggle()
    {
        m_engine->Toggle();
    }

    void AsyncMediaPlayer::Close()
    {
        std::lock_guard<std::mutex> lock(m_closeLock);

        if (m_closed)
        {
            return;
        }

        m_closed = true;

        MediaDevice::DefaultAudioRenderDeviceChanged(m_defaultAudioRenderDeviceChanged);

        // Returns at once: the engine joins its threads on the thread pool. It stays referenced
        // here, closed, so that nothing racing this sees it disappear.
        m_engine->Close();

        if (m_context)
        {
            // Detaching from the SwapChainPanel is a XAML call, and Close runs on whichever thread
            // dropped the last reference, which is not necessarily the one that owns the panel.
            if (m_dispatcherQueue && !m_dispatcherQueue.HasThreadAccess())
            {
                m_dispatcherQueue.TryEnqueue([context = m_context] { context.Detach(); });
            }
            else
            {
                m_context.Detach();
            }

            m_context.Close();
        }
    }

    AsyncMediaPlayerState AsyncMediaPlayer::State()
    {
        return static_cast<AsyncMediaPlayerState>(m_engine->State());
    }

    bool AsyncMediaPlayer::IsPlaying()
    {
        return m_engine->IsPlaying();
    }

    double AsyncMediaPlayer::Duration()
    {
        return m_engine->Duration();
    }

    double AsyncMediaPlayer::Position()
    {
        return m_engine->Position();
    }

    void AsyncMediaPlayer::Position(double value)
    {
        m_engine->Seek(value);
    }

    void AsyncMediaPlayer::Seek(double value, bool relative)
    {
        if (relative)
        {
            m_engine->SeekBy(value);
        }
        else
        {
            m_engine->Seek(value);
        }
    }

    void AsyncMediaPlayer::SetLoop(double start, double end)
    {
        m_engine->Loop(start, end);
    }

    double AsyncMediaPlayer::Rate()
    {
        return m_engine->Rate();
    }

    void AsyncMediaPlayer::Rate(double value)
    {
        m_engine->Rate(value);
    }

    double AsyncMediaPlayer::Volume()
    {
        return m_engine->Volume();
    }

    void AsyncMediaPlayer::Volume(double value)
    {
        m_engine->Volume(value);
    }

    bool AsyncMediaPlayer::Mute()
    {
        return m_engine->Mute();
    }

    void AsyncMediaPlayer::Mute(bool value)
    {
        m_engine->Mute(value);
    }

    winrt::event_token AsyncMediaPlayer::StateChanged(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Telegram::Native::Media::AsyncMediaPlayerStateChangedEventArgs> const& value)
    {
        return m_stateChanged.add(value);
    }

    void AsyncMediaPlayer::StateChanged(winrt::event_token const& token)
    {
        m_stateChanged.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::VideoOut(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_videoOut.add(value);
    }

    void AsyncMediaPlayer::VideoOut(winrt::event_token const& token)
    {
        m_videoOut.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::StreamSelected(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Telegram::Native::Media::AsyncMediaPlayerStreamSelectedEventArgs> const& value)
    {
        return m_streamSelected.add(value);
    }

    void AsyncMediaPlayer::StreamSelected(winrt::event_token const& token)
    {
        m_streamSelected.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::EndReached(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_endReached.add(value);
    }

    void AsyncMediaPlayer::EndReached(winrt::event_token const& token)
    {
        m_endReached.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::Buffering(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Telegram::Native::Media::AsyncMediaPlayerBufferingEventArgs> const& value)
    {
        return m_buffering.add(value);
    }

    void AsyncMediaPlayer::Buffering(winrt::event_token const& token)
    {
        m_buffering.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::PositionChanged(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Telegram::Native::Media::AsyncMediaPlayerPositionChangedEventArgs> const& value)
    {
        return m_positionChanged.add(value);
    }
    void AsyncMediaPlayer::PositionChanged(winrt::event_token const& token)
    {
        m_positionChanged.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::DurationChanged(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Telegram::Native::Media::AsyncMediaPlayerDurationChangedEventArgs> const& value)
    {
        return m_durationChanged.add(value);
    }

    void AsyncMediaPlayer::DurationChanged(winrt::event_token const& token)
    {
        m_durationChanged.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::Playing(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_playing.add(value);
    }

    void AsyncMediaPlayer::Playing(winrt::event_token const& token)
    {
        m_playing.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::Paused(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_paused.add(value);
    }

    void AsyncMediaPlayer::Paused(winrt::event_token const& token)
    {
        m_paused.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::Stopped(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_stopped.add(value);
    }

    void AsyncMediaPlayer::Stopped(winrt::event_token const& token)
    {
        m_stopped.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::VolumeChanged(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_volumeChanged.add(value);
    }

    void AsyncMediaPlayer::VolumeChanged(winrt::event_token const& token)
    {
        m_volumeChanged.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::EncounteredError(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_encounteredError.add(value);
    }

    void AsyncMediaPlayer::EncounteredError(winrt::event_token const& token)
    {
        m_encounteredError.remove(token);
    }

    winrt::event_token AsyncMediaPlayer::RenditionChanged(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Windows::Foundation::IInspectable> const& value)
    {
        return m_renditionChanged.add(value);
    }

    void AsyncMediaPlayer::RenditionChanged(winrt::event_token const& token)
    {
        m_renditionChanged.remove(token);
    }
}
