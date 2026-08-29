#include "pch.h"
#include "AsyncMediaPlayer.h"
#if __has_include("Media/AsyncMediaPlayer.g.cpp")
#include "Media/AsyncMediaPlayer.g.cpp"
#endif

#include <string>
#include <format>

#include <winrt/Windows.Foundation.Collections.h>

namespace winrt::Telegram::Native::Media::implementation
{
    // Each player owns a whole libvlc instance, so this is also the number of live instances
    // competing for the same audio endpoint. Worth having in a crash log tail: the audio faults
    // are suspected to involve one instance being torn down while others are still playing.
    static std::atomic<int> s_instances{ 0 };

    AsyncMediaPlayer::AsyncMediaPlayer(AsyncMediaPlayerOptions  const& options, AsyncMediaPlayerSwapChain const& context)
        : m_options(options)
        , m_context(context)
        , m_dispatcherQueue(DispatcherQueue::GetForCurrentThread())
        , m_stateChangedEventArgs(AsyncMediaPlayerState::NothingSpecial)
        , m_bufferingEventArgs(0.0f)
        , m_positionChangedEventArgs(0.0)
        , m_durationChangedEventArgs(0.0)
    {
        auto args = options.Arguments();

        std::vector<std::string> argsStorage;
        argsStorage.reserve(args.Size());

        for (const auto& opt : args)
        {
            argsStorage.push_back(winrt::to_string(opt));
        }

        if (options.CreateSwapChain() && !m_context)
        {
            m_context = AsyncMediaPlayerSwapChain(true);
        }

        // A swap chain that failed to come up is not a reason to fail construction: players are
        // created from XAML Loaded handlers, so throwing here is an unhandled exception, and the
        // Direct3D device can legitimately be unavailable while the display driver is resetting.
        // Play audio only instead -- AsyncMediaPlayerSwapChain::Create logs why it failed.
        bool video = true;

        if (m_context && m_context.IsLoaded())
        {
            for (const auto& opt : m_context.SwapChainOptions())
            {
                argsStorage.push_back(winrt::to_string(opt));
            }
        }
        else if (m_context)
        {
            LOGGER_ERROR(L"swap chain unavailable, playing without video");
            video = false;
        }

        argsStorage.push_back("--aout=winstore");
        argsStorage.push_back("--volume-save");

        // plugins.dat is generated against the plugins that ship beside it, but packaging rewrites
        // every plugin's mtime, so the cache never matches and libvlc reloads all 36 plugin DLLs --
        // 2.3s on the first libvlc_new. Skip the directory walk and trust the cache instead.
        argsStorage.push_back("--no-plugins-scan");

        if (options.Debug())
        {
            argsStorage.push_back("--verbose=3");
        }

        auto mode = options.Mode();
        if ((mode & AsyncMediaPlayerMode::Audio) == AsyncMediaPlayerMode::None)
        {
            argsStorage.push_back("--no-audio");
        }

        if ((mode & AsyncMediaPlayerMode::Video) == AsyncMediaPlayerMode::None || !video)
        {
            argsStorage.push_back("--no-video");
        }

        // Generating plugins cache requires a breakpoint in bank.c#504
        //argsStorage.clear();
        //argsStorage.push_back("--quiet");
        //argsStorage.push_back("--reset-plugins-cache");

        std::vector<const char*> argv;
        argv.reserve(argsStorage.size());

        for (const auto& s : argsStorage)
        {
            argv.push_back(s.c_str());
        }

        m_instance = libvlc_new(argv.size(), argv.data());

        // Always attach the log callback, not just in debug. libvlc reports the reason an audio
        // or demux failure happened -- "cannot get render client (error 0x...)" and similar --
        // and without this none of it reaches the crash log tail, which is all we get back from
        // the field. HandleLog drops anything below warning before formatting it, so the cost
        // outside debug is a comparison per message.
        libvlc_log_set(m_instance, &LogCallback, this);

        s_instances += 1;
        LOGGER_INFO(L"libvlc instance created ({} alive)", s_instances.load());

        m_player = libvlc_media_player_new(m_instance);

        libvlc_audio_set_volume(m_player, static_cast<int>(options.Volume() * 100));
        libvlc_audio_set_mute(m_player, options.Mute());
        libvlc_media_player_set_rate(m_player, options.Rate());

        m_events = new EventContext(m_player, get_weak());
        // MediaDevice::DefaultAudioRenderDeviceChanged is a static (app-lifetime) event that can
        // fire on a system thread; a raw 'this' would dangle if it races teardown. get_weak()
        // makes it a no-op once the player is gone (it is also detached in Close()).
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
            // Logged on both sides on purpose. The system event arrives on an arbitrary thread
            // and the switch is applied later on the worker, and in between the old device is
            // still being played to -- which is the window the audio crashes cluster in. Seeing
            // both timestamps in a crash log tail tells us whether the fault landed inside it.
            LOGGER_INFO(L"default audio render device changed, queueing switch");

            // Set() captures its arguments by value and runs them later on the worker thread,
            // so a const char* is copied as a pointer and not as characters. The temporary
            // string from to_string dies at the end of this statement, leaving libvlc to read
            // freed memory once the work item is finally dequeued. Own the string in the
            // capture so it outlives the queue.
            Write([this, id = winrt::to_string(args.Id())]
                {
                    LOGGER_INFO(L"applying audio device switch");
                    libvlc_audio_output_device_set(m_player, NULL, id.c_str());
                });
        }
    }

    AsyncMediaPlayerSwapChain AsyncMediaPlayer::Context()
    {
        return m_context;
    }

    // Lives for one libvlc media, holding the source it reads through and the handle to
    // the cache file the bytes land in.
    //
    // How far ahead of the read position to keep downloading used to be worked out here,
    // from a media bitrate measured off the read sizes. RemoteFilePrefetch does it now:
    // it measures the same thing without counting the time the reader spends blocked, it
    // knows what TDLib does with the limit it is given, and being in C# it also serves
    // the readers that never come through here.
    struct MediaContext
    {
        MediaContext(IAsyncMediaPlayerSource source)
            : source(source)
            , file(INVALID_HANDLE_VALUE)
        {
            source.Open();
        }

        IAsyncMediaPlayerSource source;
        HANDLE file;
    };

    static int OpenCallback(void* opaque, void** datap, uint64_t* sizep)
    {
        IAsyncMediaPlayerSource source{ nullptr };
        winrt::copy_from_abi(source, opaque);

        auto* ctx = new MediaContext(source);

        *datap = ctx;
        *sizep = source.FileSize();

        return 0;
    }

    static ssize_t ReadCallback(void* opaque, unsigned char* buf, size_t len)
    {
        auto* ctx = static_cast<MediaContext*>(opaque);

        int64_t offset = ctx->source.Offset();
        int64_t bytesRead;

        // Zero window: a source played from here sizes its own, and ignores this. It is
        // the sources serving one ranged request each, which have no rate to measure,
        // that are handed a window by their caller.
        ctx->source.ReadCallback(len, 0, bytesRead);

        if (ctx->file == INVALID_HANDLE_VALUE)
        {
            ctx->file = CreateFile2FromAppW(ctx->source.FilePath().data(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, OPEN_EXISTING, nullptr);;

            LARGE_INTEGER distancetoMove{};
            distancetoMove.QuadPart = offset;

            BOOL moved = SetFilePointerEx(ctx->file, distancetoMove, NULL, FILE_BEGIN);
            if (!moved)
            {
                return -1;
            }
        }

        if (ctx->file != INVALID_HANDLE_VALUE && bytesRead >= 0)
        {
            DWORD read;
            if (ReadFile(ctx->file, buf, len > bytesRead ? bytesRead : len, &read, NULL))
            {
                ctx->source.SeekCallback(offset + read);
                return read;
            }
        }

        return -1;
    }

    static int SeekCallback(void* opaque, uint64_t offset)
    {
        auto* ctx = static_cast<MediaContext*>(opaque);
        ctx->source.SeekCallback(offset);

        if (ctx->file != INVALID_HANDLE_VALUE)
        {
            LARGE_INTEGER distancetoMove{};
            distancetoMove.QuadPart = offset;

            BOOL moved = SetFilePointerEx(ctx->file, distancetoMove, NULL, FILE_BEGIN);
            return moved ? 0 : -1;
        }

        return 0;
    }

    static void CloseCallback(void* opaque)
    {
        auto* ctx = static_cast<MediaContext*>(opaque);
        ctx->source.Close();

        if (ctx->file != INVALID_HANDLE_VALUE)
        {
            CloseHandle(ctx->file);
        }

        delete ctx;
    }

    void AsyncMediaPlayer::Play(IAsyncMediaPlayerSource stream, double position)
    {
        Write([this, stream, position]() {
            if (m_stream)
            {
                m_stream.Close();
            }

            m_stream = stream;

            // TODO: make sure IAsyncMediaPlayerSource is not leaked once playback is done
            winrt::Windows::Foundation::IInspectable obj = stream;
            void* ptr = winrt::detach_abi(obj);
            auto media = libvlc_media_new_callbacks(m_instance, &OpenCallback, &ReadCallback, &SeekCallback, &CloseCallback, ptr);

            libvlc_media_player_set_media(m_player, media);
            libvlc_media_player_play(m_player);
            libvlc_media_release(media);

            if (position != 0)
            {
                libvlc_media_player_set_time(m_player, static_cast<libvlc_time_t>(position * 1000));
            }
            }, true);
    }

    void AsyncMediaPlayer::Play(winrt::Windows::Foundation::Uri uri, double position)
    {
        Write([this, uri, position]() {
            if (m_stream)
            {
                m_stream.Close();
            }

            m_stream = nullptr;

            // #define CLOCK_FREQ         INT64_C(1000000)
            // #define DEFAULT_PTS_DELAY (3*CLOCK_FREQ/10)
            // INT64_C(1000) * var_InheritInteger(access, "network-caching")

            auto path = winrt::to_string(uri.AbsoluteUri());
            auto media = libvlc_media_new_location(m_instance, path.c_str());
            libvlc_media_add_option(media, ":network-caching=300");

            libvlc_media_player_set_media(m_player, media);
            libvlc_media_player_play(m_player);
            libvlc_media_release(media);

            if (position != 0)
            {
                libvlc_media_player_set_time(m_player, static_cast<libvlc_time_t>(position * 1000));
            }
            }, true);
    }

    static inline void libvlc_media_player_play_aware(libvlc_media_player_t* p_mi)
    {
        auto state = libvlc_media_player_get_state(p_mi);
        switch (state)
        {
        case libvlc_Ended:
            libvlc_media_player_stop(p_mi);
        case libvlc_Paused:
        case libvlc_Stopped:
        case libvlc_Error:
            libvlc_media_player_play(p_mi);
            break;
        }
    }

    void AsyncMediaPlayer::Play()
    {
        Set(libvlc_media_player_play_aware);
    }

    void AsyncMediaPlayer::Stop()
    {
        Set(libvlc_media_player_stop);
    }

    static inline void libvlc_media_player_set_pause_aware(libvlc_media_player_t* p_mi, int do_pause)
    {
        auto state = libvlc_media_player_get_state(p_mi);
        if (state == libvlc_Ended)
        {
            libvlc_media_player_stop(p_mi);
            libvlc_media_player_play(p_mi);
        }

        libvlc_media_player_set_pause(p_mi, do_pause);
    }

    void AsyncMediaPlayer::Pause(bool pause)
    {
        Set(libvlc_media_player_set_pause_aware, pause);
    }

    static inline void libvlc_media_player_toggle_aware(libvlc_media_player_t* p_mi)
    {
        auto state = libvlc_media_player_get_state(p_mi);
        switch (state)
        {
        case libvlc_Ended:
            libvlc_media_player_stop(p_mi);
        case libvlc_Paused:
        case libvlc_Stopped:
        case libvlc_Error:
            libvlc_media_player_play(p_mi);
            break;
        default:
            libvlc_media_player_set_pause(p_mi, true);
            break;
        }
    }

    void AsyncMediaPlayer::Toggle()
    {
        Set(libvlc_media_player_toggle_aware);
    }

    void AsyncMediaPlayer::Close()
    {
        std::lock_guard<std::mutex> lock(close_lock_);
        if (closed_) return;

        closed_ = true;
        const auto queued = work_queue_.clear();

        // Teardown is the prime suspect for the audio crashes: this runs on whichever thread
        // called Close, while the worker may still be inside a queued libvlc call and the audio
        // output is being stopped underneath it. Record what was in flight.
        const int alive = s_instances.fetch_sub(1) - 1;
        LOGGER_INFO(L"closing, {} queued action(s) dropped, {} instance(s) still alive", queued, alive);

        MediaDevice::DefaultAudioRenderDeviceChanged(m_defaultAudioRenderDeviceChanged);

        libvlc_media_player_set_pause(m_player, true);

        libvlc_log_unset(m_instance);

        if (m_context)
        {
            m_context.Detach();
        }

        if (m_stream)
        {
            m_stream.Close();
        }

        m_stream = nullptr;

        {
            std::lock_guard<std::mutex> lock(work_lock_);
            // work_thread_ is only created on the first Write(); if the player is closed before
            // any command was issued it is still null, so don't dereference it.
            CleanupManager::Close(m_instance, m_player, m_events, m_context, work_thread_ ? std::move(*work_thread_) : std::thread{});
        }
    }

    AsyncMediaPlayerState AsyncMediaPlayer::State()
    {
        return (AsyncMediaPlayerState)Get(libvlc_media_player_get_state);
    }

    bool AsyncMediaPlayer::IsPlaying()
    {
        return Get(libvlc_media_player_is_playing);
    }

    bool AsyncMediaPlayer::CanPause()
    {
        return Get(libvlc_media_player_can_pause);
    }

    double AsyncMediaPlayer::Duration()
    {
        return std::clamp(Get(libvlc_media_player_get_length) / 1000.0, 0.0, 922337203685.0);
    }

    double AsyncMediaPlayer::Position()
    {
        auto time = Read<double>([this]() {
            auto state = libvlc_media_player_get_state(m_player);
            if (state == libvlc_Ended)
            {
                return libvlc_media_player_get_length(m_player);
            }
            else
            {
                return libvlc_media_player_get_time(m_player);
            }
            });

        return std::clamp(time / 1000.0, 0.0, 922337203685.0);
    }

    static inline void libvlc_media_player_set_time_aware(libvlc_media_player_t* p_mi, libvlc_time_t i_time)
    {
        auto state = libvlc_media_player_get_state(p_mi);
        if (state == libvlc_Ended)
        {
            libvlc_media_player_stop(p_mi);
            libvlc_media_player_play(p_mi);
        }

        libvlc_media_player_set_time(p_mi, i_time);
    }

    void AsyncMediaPlayer::Position(double value)
    {
        auto time = static_cast<libvlc_time_t>(value * 1000);
        Set(libvlc_media_player_set_time_aware, time);
    }

    void AsyncMediaPlayer::Seek(double value, bool relative)
    {
        auto time = static_cast<libvlc_time_t>(value * 1000);

        if (relative)
        {
            Write([this, time] { libvlc_media_player_set_time_aware(m_player, libvlc_media_player_get_time(m_player) + time); });
        }
        else
        {
            Set(libvlc_media_player_set_time_aware, time);
        }
    }

    double AsyncMediaPlayer::Rate()
    {
        return Get(libvlc_media_player_get_rate);
    }

    void AsyncMediaPlayer::Rate(double value)
    {
        auto rate = static_cast<float>(value);
        Set(libvlc_media_player_set_rate, rate);
    }

    double AsyncMediaPlayer::Volume()
    {
        return Get(libvlc_audio_get_volume) / 100.0;
    }

    void AsyncMediaPlayer::Volume(double value)
    {
        auto volume = static_cast<int>(value * 100);
        Set(libvlc_audio_set_volume, volume);
    }

    bool AsyncMediaPlayer::Mute()
    {
        return Get(libvlc_audio_get_mute);
    }

    void AsyncMediaPlayer::Mute(bool value)
    {
        Set(libvlc_audio_set_mute, value);
    }

    void AsyncMediaPlayer::LogCallback(void* data, int level, const libvlc_log_t* ctx, const char* fmt, va_list args)
    {
        AsyncMediaPlayer* instance = static_cast<AsyncMediaPlayer*>(data);
        instance->HandleLog(level, ctx, fmt, args);
    }

    void AsyncMediaPlayer::HandleLog(int level, const libvlc_log_t* ctx, const char* fmt, va_list args)
    {
        // Formatting costs two vsnprintf passes and an allocation, and libvlc is talkative at
        // notice level and below, so decide whether the message is wanted before paying for it.
        const bool debug = m_options.Debug();
        if (level < LIBVLC_WARNING && !debug)
            return;

        int byteLength = vsnprintf(nullptr, 0, fmt, args) + 1;
        if (byteLength <= 1)
            return;

        char* buffer = new char[byteLength];
        vsprintf_s(buffer, byteLength, fmt, args);
        std::string message(buffer, byteLength - 1);
        delete[] buffer;

        const char* module;
        const char* file;
        unsigned int line = 0;
        libvlc_log_get_context(ctx, &module, &file, &line);

        // TODO: td_execute
        //std::stringstream ss;
        //ss << "[AsyncMediaPlayer.cpp][" << file << ":" << line << "][" << message;
        //winrt::Telegram::Td::Client::Execute(winrt::Telegram::Td::Api::AddLogMessage(2, winrt::to_hstring(ss.str())));

        // Warnings and errors go to the app log so they survive into a crash report; the event
        // is only for a debug session, where nothing may be listening at all.
        if (level >= LIBVLC_WARNING)
        {
            // std::format needs wide arguments to match the wide format string that hstring
            // requires; module and message are both narrow coming out of libvlc.
            const std::wstring wmodule = winrt::to_hstring(module ? module : "?").c_str();
            const std::wstring wmessage = winrt::to_hstring(message).c_str();

            if (level >= LIBVLC_ERROR)
            {
                LOGGER_ERROR(L"vlc[{}] {}", wmodule, wmessage);
            }
            else
            {
                LOGGER_WARNING(L"vlc[{}] {}", wmodule, wmessage);
            }
        }

        if (debug)
        {
            m_log(*this, AsyncMediaPlayerLogEventArgs((AsyncMediaPlayerLogLevel)level, winrt::to_hstring(message), winrt::to_hstring(module), winrt::to_hstring(file), line));
        }
    }

    void AsyncMediaPlayer::HandleEvent(const libvlc_event_t* event)
    {
        switch (event->type)
        {
        case libvlc_MediaPlayerESSelected:
        {
            auto trackId = event->u.media_player_es_changed.i_id;
            auto trackType = event->u.media_player_es_changed.i_type;
            TryEnqueue([weakThis{ get_weak() }, trackId, trackType]() {
                if (auto strongThis = weakThis.get())
                {
                    int width = 0;
                    int height = 0;

                    if (trackType == libvlc_track_video)
                    {
                        strongThis->GetVideoTrackInfo(trackId, width, height);
                    }

                    strongThis->m_streamSelected(*strongThis, AsyncMediaPlayerStreamSelectedEventArgs(trackId, (AsyncMediaPlayerStreamType)trackType, width, height));
                }
                });
        }
        break;
        case libvlc_MediaPlayerVout:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_videoOut(*strongThis, nullptr);
                }
                });
            break;
        case libvlc_MediaPlayerBuffering:
        {
            auto cache = event->u.media_player_buffering.new_cache;
            TryEnqueue([weakThis{ get_weak() }, cache]() {
                if (auto strongThis = weakThis.get())
                {
                    //strongThis->m_stateChangedEventArgs.State(AsyncMediaPlayerState::Buffering);
                    //strongThis->m_stateChanged(*strongThis, strongThis->m_stateChangedEventArgs);

                    strongThis->m_bufferingEventArgs.Cache(cache);
                    strongThis->m_buffering(*strongThis, strongThis->m_bufferingEventArgs);
                }
                });
        }
        break;
        case libvlc_MediaPlayerEndReached:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_stateChangedEventArgs.State(AsyncMediaPlayerState::Ended);
                    strongThis->m_stateChanged(*strongThis, strongThis->m_stateChangedEventArgs);

                    strongThis->m_endReached(*strongThis, nullptr);
                }
                });
            break;

        case libvlc_MediaPlayerTimeChanged:
        {
            auto position = std::clamp(event->u.media_player_time_changed.new_time / 1000.0, 0.0, 922337203685.0);
            TryEnqueue([weakThis{ get_weak() }, position]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_positionChangedEventArgs.Position(position);
                    strongThis->m_positionChanged(*strongThis, strongThis->m_positionChangedEventArgs);
                }
                });
        }
        break;
        case libvlc_MediaPlayerLengthChanged:
        {
            auto duration = std::clamp(event->u.media_player_length_changed.new_length / 1000.0, 0.0, 922337203685.0);
            TryEnqueue([weakThis{ get_weak() }, duration]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_durationChangedEventArgs.Duration(duration);
                    strongThis->m_durationChanged(*strongThis, strongThis->m_durationChangedEventArgs);
                }
                });
        }
        break;
        case libvlc_MediaPlayerPlaying:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_stateChangedEventArgs.State(AsyncMediaPlayerState::Playing);
                    strongThis->m_stateChanged(*strongThis, strongThis->m_stateChangedEventArgs);

                    strongThis->m_playing(*strongThis, nullptr);
                }
                });
            break;
        case libvlc_MediaPlayerPaused:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_stateChangedEventArgs.State(AsyncMediaPlayerState::Paused);
                    strongThis->m_stateChanged(*strongThis, strongThis->m_stateChangedEventArgs);

                    strongThis->m_paused(*strongThis, nullptr);
                }
                });
            break;
        case libvlc_MediaPlayerStopped:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_stateChangedEventArgs.State(AsyncMediaPlayerState::Stopped);
                    strongThis->m_stateChanged(*strongThis, strongThis->m_stateChangedEventArgs);

                    strongThis->m_stopped(*strongThis, nullptr);
                }
                });
            break;
        case libvlc_MediaPlayerAudioVolume:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_volumeChanged(*strongThis, nullptr);
                }
                });
            break;
        case libvlc_MediaPlayerEncounteredError:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_stateChangedEventArgs.State(AsyncMediaPlayerState::Error);
                    strongThis->m_stateChanged(*strongThis, strongThis->m_stateChangedEventArgs);
                    strongThis->m_encounteredError(*strongThis, nullptr);
                }
                });
            break;
        case libvlc_MediaPlayerNothingSpecial:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_stateChangedEventArgs.State(AsyncMediaPlayerState::NothingSpecial);
                    strongThis->m_stateChanged(*strongThis, strongThis->m_stateChangedEventArgs);
                }
                });
            break;
        case libvlc_MediaPlayerOpening:
            TryEnqueue([weakThis{ get_weak() }]() {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->m_stateChangedEventArgs.State(AsyncMediaPlayerState::Opening);
                    strongThis->m_stateChanged(*strongThis, strongThis->m_stateChangedEventArgs);
                }
                });
            break;
        }
    }

    void AsyncMediaPlayer::TryEnqueue(DispatcherQueueHandler const& callback) const
    {
        try
        {
            if (m_dispatcherQueue)
            {
                m_dispatcherQueue.TryEnqueue(callback);
            }
            else
            {
                post_to_threadpool([callback = std::move(callback)]() {
                    callback();
                    });
            }
        }
        catch (...)
        {
            // Likely Window teardown
        }
    }

    void AsyncMediaPlayer::GetVideoTrackInfo(int32_t trackId, int32_t& width, int32_t& height)
    {
        Read<int>([this, trackId, &width, &height]() {
            libvlc_media_t* media = libvlc_media_player_get_media(m_player);
            if (media)
            {
                libvlc_media_track_t** tracks;
                unsigned int trackCount = libvlc_media_tracks_get(media, &tracks);

                for (unsigned int i = 0; i < trackCount; ++i)
                {
                    if (tracks[i]->i_id == trackId && tracks[i]->i_type == libvlc_track_video)
                    {
                        auto* video = tracks[i]->video;
                        if (video)
                        {
                            // TODO: Original C# code used right_top and left_top, but it was likely a bug
                            if (video->i_orientation == libvlc_video_orient_left_bottom || video->i_orientation == libvlc_video_orient_right_top)
                            {
                                width = video->i_height;
                                height = video->i_width;
                            }
                            else
                            {
                                width = video->i_width;
                                height = video->i_height;
                            }
                        }
                        break;
                    }
                }

                libvlc_media_tracks_release(tracks, trackCount);
                libvlc_media_release(media);
            }

            return 0;
            });
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

    winrt::event_token AsyncMediaPlayer::Log(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Media::AsyncMediaPlayer,
        winrt::Telegram::Native::Media::AsyncMediaPlayerLogEventArgs> const& value)
    {
        return m_log.add(value);
    }

    void AsyncMediaPlayer::Log(winrt::event_token const& token)
    {
        m_log.remove(token);
    }
}
