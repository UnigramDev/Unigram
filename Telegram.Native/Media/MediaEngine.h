#pragma once

#include "Media/MediaEngineCommon.h"
#include "Media/MediaVideoRenderer.h"

#include <atomic>
#include <cstdint>
#include <memory>
#include <mutex>

namespace winrt::Telegram::Native::Media::implementation
{
    // Value for value the same as AsyncMediaPlayerState.
    enum class MediaEngineState : int32_t
    {
        NothingSpecial = 0,
        Opening = 1,
        Buffering = 2,
        Playing = 3,
        Paused = 4,
        Stopped = 5,
        Ended = 6,
        Error = 7
    };

    // Value for value the same as AsyncMediaPlayerStreamType.
    enum class MediaStreamKind : int32_t
    {
        Audio = 0,
        Video = 1
    };

    struct MediaEngineOptions
    {
        bool audio{ true };
        bool video{ true };
        double rate{ 1 };
        double volume{ 1 };
        bool mute{ false };
    };

    class IMediaByteSource
    {
    public:
        // Nothing arrived for a long time. Not an error: the file may still come, and the engine
        // keeps waiting until it is closed or seeked.
        static constexpr int64_t ReadStalled = -1;
        static constexpr int64_t ReadFailed = -2;

        virtual ~IMediaByteSource() = default;

        virtual void Open() = 0;

        // Ends a read blocked on the network. The source is opened again before the next one.
        virtual void Close() = 0;

        virtual int64_t Size() = 0;

        // Blocks until bytes at the offset are there. Returns how many were read, zero at the end
        // of the file, or one of the values above.
        virtual int64_t Read(int64_t offset, uint8_t* buffer, int64_t count) = 0;
    };

    // Called on the engine's threads; an implementation posts to wherever it needs to be.
    class IMediaEngineHost
    {
    public:
        virtual ~IMediaEngineHost() = default;

        virtual void StateChanged(MediaEngineState state) = 0;
        virtual void PositionChanged(double position) = 0;
        virtual void DurationChanged(double duration) = 0;
        virtual void Buffering(float progress) = 0;
        virtual void StreamSelected(MediaStreamKind kind, int32_t id, int32_t width, int32_t height) = 0;
        virtual void VideoOut() = 0;
        virtual void EndReached() = 0;
        virtual void EncounteredError() = 0;
        virtual void VolumeChanged() = 0;
    };

    class MediaSession;

    // Plays one file at a time with ffmpeg: demuxing, decoding, the clock, and the policy for
    // waiting on the network. Every method returns without waiting on any of that.
    class MediaEngine : public std::enable_shared_from_this<MediaEngine>
    {
    public:
        static std::shared_ptr<MediaEngine> Create(MediaEngineOptions const& options, std::shared_ptr<IMediaEngineHost> host, std::shared_ptr<MediaVideoRenderer> renderer);

        MediaEngine(MediaEngineOptions const& options, std::shared_ptr<IMediaEngineHost> host, std::shared_ptr<MediaVideoRenderer> renderer);
        ~MediaEngine();

        MediaEngine(const MediaEngine&) = delete;
        MediaEngine& operator=(const MediaEngine&) = delete;

        void Open(std::shared_ptr<IMediaByteSource> source, double position);
        void Play();
        void Pause(bool pause);
        void Toggle();
        void Stop();
        void Close();

        void Seek(double position);
        void SeekBy(double offset);

        MediaEngineState State() const noexcept
        {
            return m_state.load();
        }

        bool IsPlaying() const noexcept
        {
            return m_state.load() == MediaEngineState::Playing;
        }

        double Position();

        double Duration() const noexcept
        {
            return m_duration.load();
        }

        double Rate() const noexcept
        {
            return m_rate.load();
        }

        void Rate(double value);

        double Volume() const noexcept
        {
            return m_volume.load();
        }

        void Volume(double value);

        bool Mute() const noexcept
        {
            return m_mute.load();
        }

        void Mute(bool value);

        void AudioDeviceChanged();

    private:
        friend class MediaSession;

        // Called by sessions. Each drops what it is told unless the session is still the current
        // one, so a session winding down never reaches the caller.
        bool IsCurrent(uint64_t session) const noexcept
        {
            return m_current.load() == session;
        }

        void SetState(uint64_t session, MediaEngineState state);
        void ReportPosition(uint64_t session, double position);
        void ReportDuration(uint64_t session, double duration);
        void ReportBuffering(uint64_t session, float progress);
        void ReportStream(uint64_t session, MediaStreamKind kind, int32_t id, int32_t width, int32_t height);
        void ReportVideoOut(uint64_t session);
        void ReportEnded(uint64_t session, double position);
        void ReportError(uint64_t session);

        void RendererResized();

        void StartLocked(double position);
        void SetStateLocked(MediaEngineState state);

        MediaEngineOptions m_options;
        std::shared_ptr<IMediaEngineHost> m_host;
        std::shared_ptr<MediaVideoRenderer> m_renderer;

        std::mutex m_lock;
        std::shared_ptr<MediaSession> m_session;

        // Kept after Stop, because Play opens it again: a stopped player plays from the start.
        std::shared_ptr<IMediaByteSource> m_source;

        uint64_t m_sessions{ 0 };
        std::atomic<uint64_t> m_current{ 0 };
        bool m_closed{ false };

        std::atomic<MediaEngineState> m_state{ MediaEngineState::NothingSpecial };
        std::atomic<double> m_position{ 0 };
        std::atomic<double> m_duration{ 0 };

        // What the caller asked for. Whether the clock actually runs also depends on the session
        // having something to play.
        std::atomic<bool> m_paused{ false };

        std::atomic<double> m_rate{ 1 };
        std::atomic<double> m_volume{ 1 };
        std::atomic<bool> m_mute{ false };
    };
}
