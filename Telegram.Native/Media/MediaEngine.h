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

    // What the reads of an open file answer to: the session playing it, or a preload.
    class IMediaInputOwner
    {
    public:
        virtual ~IMediaInputOwner() = default;

        // Before every read. False ends it as interrupted.
        virtual bool BeginRead() = 0;
        virtual void EndRead() = 0;

        // Whether the reads were interrupted, which makes a read that came back without bytes
        // not an error, and abandons an open.
        virtual bool Interrupted() = 0;

        // A read that got nothing for a long while: whether to ask again. Elapsed is how long the
        // read took, as a closed source answers at once.
        virtual bool Stalled(double elapsed) = 0;
    };

    struct MediaInput;

    // Not MediaPreloadResult: inside this namespace that name would hide the projected struct.
    struct MediaPreloadOutcome
    {
        // Whether it stopped where it was meant to, at the seconds asked for, the limit or the end
        // of the file, rather than failing or being canceled.
        bool completed{ false };

        // Bytes read to open the file, and past that, and how far into the media the latter go.
        int64_t openBytes{ 0 };
        int64_t mediaBytes{ 0 };
        double mediaSeconds{ 0 };
    };

    // Reads what playing a video reads first, without decoding any of it: the open, with the same
    // options, then the first seconds of packets. Every read waits for its bytes, so once it
    // returns what playback reads first is on disk, wherever in the file the open finds it.
    //
    // Not probed for its streams as playback is: the probe only reads the first packets, which
    // the packets read after it include.
    class MediaPreload : private IMediaInputOwner
    {
    public:
        explicit MediaPreload(std::shared_ptr<IMediaRendition> rendition);

        MediaPreload(const MediaPreload&) = delete;
        MediaPreload& operator=(const MediaPreload&) = delete;

        // Blocks until the media is read up to the seconds given, or the limit of bytes past the
        // open, or until it fails or is canceled.
        MediaPreloadOutcome Run(double seconds, int64_t limit);

        // From another thread, ending a Run blocked on the network.
        void Cancel();

    private:
        void Read(MediaInput& input, double seconds, int64_t limit, MediaPreloadOutcome& result);

        bool BeginRead() override;
        void EndRead() override;
        bool Interrupted() override;
        bool Stalled(double elapsed) override;

        const std::shared_ptr<IMediaRendition> m_rendition;

        std::mutex m_lock;
        std::shared_ptr<IMediaByteSource> m_source;
        std::atomic<bool> m_canceled{ false };

        // Only touched by the thread running it.
        bool m_stalled{ false };
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
