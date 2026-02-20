#pragma once

#include "Media/AsyncMediaPlayer.g.h"

#include <vlc/vlc.h>

#include <thread>
#include <mutex>
#include <condition_variable>
#include <queue>
#include <functional>
#include <atomic>
#include <memory>
#include <chrono>

#include <winrt/Windows.Media.Devices.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.System.h>

using namespace winrt::Windows::Media::Devices;
using namespace winrt::Windows::System;

template<typename Func>
inline void post_to_threadpool(Func&& func)
{
    auto* heapFunc = new std::decay_t<Func>(std::forward<Func>(func));

    PTP_WORK work = CreateThreadpoolWork(
        [](PTP_CALLBACK_INSTANCE, PVOID context, PTP_WORK) {
            std::unique_ptr<std::decay_t<Func>> funcPtr(
                static_cast<std::decay_t<Func>*>(context)
            );
            (*funcPtr)();
        },
        heapFunc,
        nullptr
    );

    SubmitThreadpoolWork(work);
    CloseThreadpoolWork(work);
}

struct WorkItem
{
    std::function<void()> action;
    long version;

    WorkItem(std::function<void()> act, long ver)
        : action(std::move(act))
        , version(ver)
    {
    }
};

class WorkQueue
{
private:
    mutable std::mutex work_available_mutex_;
    std::condition_variable work_available_cv_;
    std::queue<std::shared_ptr<WorkItem>> work_;
    bool shutdown_ = false;

public:
    void push(std::shared_ptr<WorkItem> item)
    {
        std::lock_guard<std::mutex> lock(work_available_mutex_);
        if (shutdown_) return;

        work_.push(item);
        work_available_cv_.notify_one();
    }

    std::shared_ptr<WorkItem> wait_and_pop(int timeout_ms = 3000)
    {
        std::unique_lock<std::mutex> lock(work_available_mutex_);

        while (true)
        {
            if (shutdown_)
            {
                return nullptr;
            }

            if (!work_.empty())
            {
                auto item = work_.front();
                work_.pop();
                return item;
            }

            if (work_available_cv_.wait_for(lock, std::chrono::milliseconds(timeout_ms)) == std::cv_status::timeout)
            {
                return nullptr;
            }
        }
    }

    void clear()
    {
        std::lock_guard<std::mutex> lock(work_available_mutex_);

        // Clear regular work queue
        while (!work_.empty())
        {
            work_.pop();
        }

        shutdown_ = true;
        work_available_cv_.notify_all();
    }
};

namespace winrt::Telegram::Native::Media::implementation
{
    struct AsyncMediaPlayer : AsyncMediaPlayerT<AsyncMediaPlayer>
    {
        AsyncMediaPlayer(AsyncMediaPlayerOptions const& options, AsyncMediaPlayerSwapChain const& context = nullptr);
        ~AsyncMediaPlayer();

        AsyncMediaPlayerSwapChain Context();

        void Play(IAsyncMediaPlayerSource stream, double position = 0);
        void Play(winrt::Windows::Foundation::Uri uri, double position = 0);
        void Play();
        void Stop();
        void Pause(bool pause = true);
        void Toggle();
        void Close();

        AsyncMediaPlayerState State();
        bool IsPlaying();
        bool CanPause();

        double Duration();

        double Position();
        void Position(double value);

        void Seek(double value, bool relative);

        double Rate();
        void Rate(double value);

        double Volume();
        void Volume(double value);

        bool Mute();
        void Mute(bool value);

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

        winrt::event_token Log(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Media::AsyncMediaPlayer,
            winrt::Telegram::Native::Media::AsyncMediaPlayerLogEventArgs> const& value);
        void Log(winrt::event_token const& token);

    private:
        struct EventContext
        {
            EventContext(libvlc_media_player_t* player, winrt::weak_ref<AsyncMediaPlayer> weak)
                : m_weak(weak)
            {
                libvlc_event_manager_t* em = libvlc_media_player_event_manager(player);
                libvlc_event_attach(em, libvlc_MediaPlayerESSelected, &EventCallback, this);
                libvlc_event_attach(em, libvlc_MediaPlayerVout, &EventCallback, this);
                libvlc_event_attach(em, libvlc_MediaPlayerBuffering, &EventCallback, this);
                libvlc_event_attach(em, libvlc_MediaPlayerEndReached, &EventCallback, this);
                libvlc_event_attach(em, libvlc_MediaPlayerTimeChanged, &EventCallback, this);
                libvlc_event_attach(em, libvlc_MediaPlayerLengthChanged, &EventCallback, this);
                libvlc_event_attach(em, libvlc_MediaPlayerPlaying, &EventCallback, this);
                libvlc_event_attach(em, libvlc_MediaPlayerPaused, &EventCallback, this);
                libvlc_event_attach(em, libvlc_MediaPlayerStopped, &EventCallback, this);
                libvlc_event_attach(em, libvlc_MediaPlayerAudioVolume, &EventCallback, this);
                libvlc_event_attach(em, libvlc_MediaPlayerEncounteredError, &EventCallback, this);
                libvlc_event_attach(em, libvlc_MediaPlayerNothingSpecial, &EventCallback, this);
                libvlc_event_attach(em, libvlc_MediaPlayerOpening, &EventCallback, this);
            }

            void Detach(libvlc_media_player_t* player)
            {
                libvlc_event_manager_t* em = libvlc_media_player_event_manager(player);
                libvlc_event_detach(em, libvlc_MediaPlayerESSelected, &EventCallback, this);
                libvlc_event_detach(em, libvlc_MediaPlayerVout, &EventCallback, this);
                libvlc_event_detach(em, libvlc_MediaPlayerBuffering, &EventCallback, this);
                libvlc_event_detach(em, libvlc_MediaPlayerEndReached, &EventCallback, this);
                libvlc_event_detach(em, libvlc_MediaPlayerTimeChanged, &EventCallback, this);
                libvlc_event_detach(em, libvlc_MediaPlayerLengthChanged, &EventCallback, this);
                libvlc_event_detach(em, libvlc_MediaPlayerPlaying, &EventCallback, this);
                libvlc_event_detach(em, libvlc_MediaPlayerPaused, &EventCallback, this);
                libvlc_event_detach(em, libvlc_MediaPlayerStopped, &EventCallback, this);
                libvlc_event_detach(em, libvlc_MediaPlayerAudioVolume, &EventCallback, this);
                libvlc_event_detach(em, libvlc_MediaPlayerEncounteredError, &EventCallback, this);
                libvlc_event_detach(em, libvlc_MediaPlayerNothingSpecial, &EventCallback, this);
                libvlc_event_detach(em, libvlc_MediaPlayerOpening, &EventCallback, this);
            }

            winrt::weak_ref<AsyncMediaPlayer> m_weak;

            static void EventCallback(const libvlc_event_t* event, void* user_data)
            {
                auto* ctx = static_cast<EventContext*>(user_data);
                if (auto strong = ctx->m_weak.get())
                {
                    strong->HandleEvent(event);
                }
            }
        };

        class CleanupManager
        {
        public:
            static void Close(libvlc_instance_t* instance, libvlc_media_player_t* player, EventContext* events, AsyncMediaPlayerSwapChain const& swapChain, std::thread workerThread)
            {
                post_to_threadpool([instance, player, events, swapChain, workerThread = std::move(workerThread)]() mutable {
                    // First we wait for any pending operation to be completed
                    if (workerThread.joinable())
                    {
                        workerThread.join();
                    }
                    // Then we release all objects
                    if (player)
                    {
                        libvlc_media_player_stop(player);

                        if (events)
                        {
                            events->Detach(player);
                            delete events;
                        }

                        libvlc_media_player_set_media(player, nullptr);
                        libvlc_media_player_release(player);
                    }
                    if (instance)
                    {
                        libvlc_release(instance);
                    }
                    if (swapChain)
                    {
                        swapChain.Close();
                    }
                    });
            }
        };

        AsyncMediaPlayerOptions m_options;

        DispatcherQueue m_dispatcherQueue{ nullptr };
        AsyncMediaPlayerSwapChain m_context{ nullptr };
        winrt::event_token m_defaultAudioRenderDeviceChanged{};

        AsyncMediaPlayerStateChangedEventArgs m_stateChangedEventArgs;
        AsyncMediaPlayerBufferingEventArgs m_bufferingEventArgs;
        AsyncMediaPlayerPositionChangedEventArgs m_positionChangedEventArgs;
        AsyncMediaPlayerDurationChangedEventArgs m_durationChangedEventArgs;

        libvlc_instance_t* m_instance;
        libvlc_media_player_t* m_player;
        EventContext* m_events;
        IAsyncMediaPlayerSource m_stream{ nullptr };

        void OnDefaultAudioRenderDeviceChanged(winrt::Windows::Foundation::IInspectable const& sender, DefaultAudioRenderDeviceChangedEventArgs const& args);

        static void LogCallback(void* data, int level, const libvlc_log_t* ctx, const char* fmt, va_list args);

        void HandleLog(int level, const libvlc_log_t* ctx, const char* fmt, va_list args);

        void HandleEvent(const libvlc_event_t* event);
        void TryEnqueue(DispatcherQueueHandler const& callback) const;

        void GetVideoTrackInfo(int32_t trackId, int32_t& width, int32_t& height);

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
            winrt::Telegram::Native::Media::AsyncMediaPlayerLogEventArgs>> m_log;

    private:
        mutable std::mutex close_lock_;
        bool closed_ = false;

        std::atomic<bool> work_started_{ false };
        std::unique_ptr<std::thread> work_thread_;

        WorkQueue work_queue_;
        mutable std::mutex work_lock_;

        std::atomic<long> work_version_{ 0 };

        template<typename U>
        U Read(std::function<U()> value)
        {
            std::lock_guard<std::mutex> lock(close_lock_);
            if (closed_) return U{};
            return value();
        }

        void Write(std::function<void()> action, bool increment = false)
        {
            {
                std::lock_guard<std::mutex> lock(close_lock_);
                if (closed_) return;
            }

            long version = increment ? ++work_version_ : -1;
            auto work_item = std::make_shared<WorkItem>(std::move(action), version);

            work_queue_.push(work_item);

            std::lock_guard<std::mutex> lock(work_lock_);
            if (!work_started_.load())
            {
                if (work_thread_ && work_thread_->joinable())
                {
                    work_thread_->join();
                }

                // We need to pass a strong reference to this as the thread may outlive the instance
                work_started_ = true;
                work_thread_ = std::make_unique<std::thread>(&AsyncMediaPlayer::Work, get_strong());
            }
        }

        template<typename Func, typename... Args>
        auto Get(Func func, Args&&... args) -> decltype(func(m_player, std::forward<Args>(args)...))
        {
            return Read<decltype(func(m_player, std::forward<Args>(args)...))>(
                [this, func, args...] { return func(m_player, args...); });
        }

        template<typename Func, typename... Args>
        void Set(Func func, Args&&... args)
        {
            Write([this, func, args...] {
                func(m_player, args...);
                });
        }

        void Work()
        {
            try
            {
                while (true)
                {
                    auto work = work_queue_.wait_and_pop();
                    if (!work)
                    {
                        break;
                    }

                    {
                        std::lock_guard<std::mutex> lock(close_lock_);
                        if (closed_) break;
                    }

                    try
                    {
                        if (work->version == -1 || (work->version == work_version_.load()))
                        {
                            work->action();
                        }
                    }
                    catch (...)
                    {
                        // Shit happens...
                    }

                    {
                        std::lock_guard<std::mutex> lock(close_lock_);
                        if (closed_) break;
                    }
                }
            }
            catch (...)
            {
                // Handle any unexpected exceptions
            }

            std::lock_guard<std::mutex> lock(work_lock_);
            work_started_ = false;
        }
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct AsyncMediaPlayer : AsyncMediaPlayerT<AsyncMediaPlayer, implementation::AsyncMediaPlayer>
    {
    };
}
