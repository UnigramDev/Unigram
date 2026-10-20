#pragma once

#include "Media/MediaEngineCommon.h"
#include "Media/MediaVideoRenderer.h"

#include <atomic>
#include <cstdint>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

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
        // of the file, or one of the values above. Window is how far past the offset to have
        // downloaded meanwhile.
        virtual int64_t Read(int64_t offset, uint8_t* buffer, int64_t count, int64_t window) = 0;
    };

    // One quality of a video. A file without qualities is played as a single one of these.
    class IMediaRendition
    {
    public:
        virtual ~IMediaRendition() = default;

        virtual int32_t Width() = 0;
        virtual int32_t Height() = 0;
        virtual int64_t Size() = 0;

        // Whether LoadPlaylist has anything to load. Only qualities with one can be switched to
        // seamlessly; the others are opened at a position like any file.
        virtual bool HasPlaylist() = 0;

        // The HLS playlist cutting the file into segments, or empty for a plain file. Called when
        // the quality is first needed, on the engine's thread, and may block on the download.
        virtual std::string LoadPlaylist() = 0;

        // Ends a LoadPlaylist blocked on the download, from another thread, so that a player closed
        // while the playlist cannot arrive is not held open until it does.
        virtual void CancelPlaylist() = 0;

        virtual std::shared_ptr<IMediaByteSource> CreateSource() = 0;

        // Bytes per second the source last created has been downloading at, zero while unknown.
        virtual double DownloadSpeed() = 0;
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
        virtual void RenditionChanged() = 0;
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

        // Plays a video available in several qualities, starting with the one given. Automatic
        // lets the engine move between them as the connection allows.
        void Open(std::vector<std::shared_ptr<IMediaRendition>> renditions, int32_t rendition, bool automatic, double position);

        // -1 hands the choice back to the engine.
        void SelectRendition(int32_t rendition);

        int32_t Rendition() const noexcept
        {
            return m_rendition.load();
        }

        bool IsRenditionAutomatic() const noexcept
        {
            return m_automatic.load();
        }
        void Play();
        void Pause(bool pause);
        void Toggle();
        void Stop();
        void Close();

        void Seek(double position);
        void SeekBy(double offset);

        // Playback reaching the end goes back to the start instead. An end not past the start
        // turns it off.
        void Loop(double start, double end);

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
        // one, so a session winding down never reaches the caller. Under m_reportLock, together
        // with what it reports: checked first and acted on after, an Ended could land on the next
        // session.
        bool IsCurrent(uint64_t session) const noexcept
        {
            return m_current == session;
        }

        void SetState(uint64_t session, MediaEngineState state);
        void ReportPosition(uint64_t session, double position);
        void ReportDuration(uint64_t session, double duration);
        void ReportBuffering(uint64_t session, float progress);
        void ReportStream(uint64_t session, MediaStreamKind kind, int32_t id, int32_t width, int32_t height);
        void ReportVideoOut(uint64_t session);
        void ReportEnded(uint64_t session, double position);
        void ReportError(uint64_t session);
        void ReportRendition(uint64_t session, int32_t rendition);

        void RendererResized();
        void RendererReset();

        void OpenRenditions(std::vector<std::shared_ptr<IMediaRendition>> renditions, int32_t rendition, bool automatic, double position, bool shared);
        void StartLocked(double position);
        void SetStateLocked(MediaEngineState state);

        MediaEngineOptions m_options;
        std::shared_ptr<IMediaEngineHost> m_host;
        std::shared_ptr<MediaVideoRenderer> m_renderer;

        std::mutex m_lock;
        std::shared_ptr<MediaSession> m_session;

        // The session Stop shut down, which may still be reading the shared source that Play
        // opens again.
        std::shared_ptr<MediaSession> m_stopped;

        // Taken after m_lock and after any lock of a session's, and nothing is taken under it: the
        // host only posts.
        std::mutex m_reportLock;

        // Kept after Stop, because Play opens them again: a stopped player plays from the start.
        std::vector<std::shared_ptr<IMediaRendition>> m_renditions;

        // Whether the renditions are one plain source, which sessions then share.
        bool m_shared{ false };

        uint64_t m_sessions{ 0 };
        uint64_t m_current{ 0 };
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

        std::atomic<int32_t> m_rendition{ 0 };
        std::atomic<bool> m_automatic{ false };

        std::atomic<double> m_loopStart{ 0 };
        std::atomic<double> m_loopEnd{ 0 };
    };
}
