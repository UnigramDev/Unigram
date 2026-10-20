#include "pch.h"
#include "MediaEngine.h"
#include "MediaAudioRenderer.h"
#include "MediaPlaylist.h"

#include <winrt/Telegram.Native.h>

#include <cstring>
#include <format>
#include <thread>

extern "C"
{
#include <libavformat/avformat.h>
#include <libavutil/buffer.h>
#include <libavutil/display.h>
#include <libavutil/imgutils.h>
#include <libavutil/pixdesc.h>
#include <libswscale/swscale.h>
}

namespace winrt::Telegram::Native::Media::implementation
{
    struct MediaStreamSetup
    {
        AVCodecParameters* parameters{ nullptr };
        AVRational timeBase{ 0, 1 };
        double startTime{ 0 };
        double frameDuration{ 1.0 / 30 };
        MediaVideoFormat format;
        int32_t rendition{ -1 };

        MediaStreamSetup() = default;
        MediaStreamSetup(const MediaStreamSetup&) = delete;
        MediaStreamSetup& operator=(const MediaStreamSetup&) = delete;

        ~MediaStreamSetup()
        {
            avcodec_parameters_free(&parameters);
        }
    };

    class MediaSession;

    // One open file: the demuxer, and the bytes it reads.
    struct MediaInput
    {
        MediaSession* session{ nullptr };
        int32_t rendition{ 0 };
        std::shared_ptr<IMediaByteSource> source;

        // How a quality's segments are laid out for the demuxer, null for a plain file.
        std::unique_ptr<MediaSegmentMap> map;

        AVFormatContext* format{ nullptr };
        AVIOContext* io{ nullptr };
        int64_t position{ 0 };
        int64_t size{ 0 };

        // How far past each read the source keeps downloading.
        int64_t window{ 0 };

        int videoIndex{ -1 };
        int audioIndex{ -1 };
        std::shared_ptr<MediaStreamSetup> video;
        std::shared_ptr<MediaStreamSetup> audio;

        double openedAt{ MediaNow() };

        MediaInput() = default;
        MediaInput(const MediaInput&) = delete;
        MediaInput& operator=(const MediaInput&) = delete;

        ~MediaInput()
        {
            // A custom AVIOContext is not the format's to free.
            avformat_close_input(&format);

            if (io != nullptr)
            {
                av_freep(&io->buffer);
                avio_context_free(&io);
            }
        }
    };

    namespace
    {
        constexpr int kIoBufferSize = 64 * 1024;

        // Demuxing stops this far ahead of playback, or at this many bytes queued, whichever comes
        // first. RemoteFileSource downloads as far as it is read, so this is the read-ahead too.
        constexpr double kMaxAhead = 30;
        constexpr int64_t kMaxQueuedBytes = 64 * 1024 * 1024;

        // Having run dry, playback waits for this much before going on, so that a slow link makes
        // it stop once and then play, rather than stutter on every packet.
        constexpr double kResumeAhead = 3;

        // How much audio is decoded before the clock starts, and how far ahead it is decoded.
        constexpr double kAudioPreroll = 0.2;
        constexpr double kAudioDecodeAhead = 1;

        constexpr double kPositionInterval = 0.25;

        // Frame threading keeps several pictures in flight inside the decoder already, so a short
        // queue here is enough to ride out one slow frame.
        constexpr size_t kVideoFrames = 3;

        // A picture this late, with a newer one already due, makes the decoder skip the pictures
        // nothing else refers to until it catches up.
        constexpr double kVideoBehind = 0.2;

        constexpr int kMaxReadErrors = 32;

        // How far past each read TDLib is asked to have downloaded. Short, because the demuxer
        // already reads its own 30 seconds ahead: this only has to keep a read from waiting on the
        // round trip of asking for more, and anything past it is downloaded for nothing when the
        // quality changes or the viewer leaves.
        constexpr double kDownloadAhead = 5;
        constexpr int64_t kMinDownloadWindow = 1024 * 1024;
        constexpr int64_t kMaxDownloadWindow = 16 * 1024 * 1024;

        // TDLib cancels in-flight parts outside the window whenever it moves, so a window on a part
        // boundary keeps the parts it protects exactly the same as reads move along.
        int64_t DownloadWindow(int64_t size, double duration) noexcept
        {
            constexpr int64_t quantum = 512 * 1024;

            const int64_t window = size > 0 && duration > 0
                ? static_cast<int64_t>(size / duration * kDownloadAhead)
                : kMinDownloadWindow;

            return (std::clamp(window, kMinDownloadWindow, kMaxDownloadWindow) + quantum - 1) / quantum * quantum;
        }

        // Automatic quality. A step up needs this much buffered and this much headroom in the
        // measured speed over the quality's average rate; it is taken at a segment boundary at
        // least this far ahead, past what the decoders already hold, so that it is seamless.
        constexpr double kAdaptInterval = 0.5;
        constexpr double kUpBuffered = 4;
        constexpr double kUpHeadroom = 1.2;
        constexpr double kSwitchMargin = 3;

        // A step down is taken at once when playback has been waiting this long, and ahead of time
        // when the speed no longer covers the quality and less than this much is buffered.
        constexpr double kStallDown = 0.5;
        constexpr double kDownAhead = 10;

        // The speed of a quality just opened describes the previous one, or nothing; after a step
        // down, a step up waits so that a link at the edge does not flip back and forth.
        constexpr double kSpeedSettle = 3;
        constexpr double kSwitchHold = 5;
        constexpr double kUpHold = 15;

        // Video read this far past the last audio means the file's sound has ended before its
        // picture, rather than that the audio is late: playback does not wait for it.
        constexpr double kAudioGone = 2;

        double ToSeconds(int64_t value, AVRational base) noexcept
        {
            return value == AV_NOPTS_VALUE ? NAN : static_cast<double>(value) * av_q2d(base);
        }

        // The same reading as VideoAnimation's: the rotate tag first, the display matrix after.
        int32_t RotationOf(AVStream const* stream)
        {
            AVDictionaryEntry* entry = av_dict_get(stream->metadata, "rotate", nullptr, 0);
            if (entry && entry->value)
            {
                if (!strcmp(entry->value, "90") || !strcmp(entry->value, "-270"))
                {
                    return 90;
                }
                else if (!strcmp(entry->value, "270") || !strcmp(entry->value, "-90"))
                {
                    return 270;
                }
                else if (!strcmp(entry->value, "180") || !strcmp(entry->value, "-180"))
                {
                    return 180;
                }
                else if (!strcmp(entry->value, "0"))
                {
                    return 0;
                }
            }

            const AVPacketSideData* matrix = av_packet_side_data_get(
                stream->codecpar->coded_side_data, stream->codecpar->nb_coded_side_data, AV_PKT_DATA_DISPLAYMATRIX);

            if (matrix)
            {
                const int32_t rotation = ((int32_t)-av_display_rotation_get(reinterpret_cast<const int32_t*>(matrix->data)) + 360) % 360;

                // Anything but a quarter turn is not something the renderer can show.
                return (rotation + 45) / 90 * 90 % 360;
            }

            return 0;
        }

        const AVCodec* FindDecoder(AVCodecID id)
        {
            // The native AV1 decoder only decodes through a hardware accelerator, and none is built.
            if (id == AV_CODEC_ID_AV1)
            {
                if (const AVCodec* codec = avcodec_find_decoder_by_name("libdav1d"))
                {
                    return codec;
                }
            }

            return avcodec_find_decoder(id);
        }

        // Set by GetHardwareFormat when a stream turns out to be one the GPU does not take.
        struct DecoderState
        {
            bool hardwareFailed{ false };
        };

        // The codecs whose D3D11VA hardware decoders are built. VP9 is not among them: hardware VP9
        // needs ffmpeg's own VP9 decoder, which would then also be picked for video stickers, and
        // only libvpx keeps their alpha.
        bool SupportsHardware(AVCodecID id) noexcept
        {
            return id == AV_CODEC_ID_H264 || id == AV_CODEC_ID_HEVC || id == AV_CODEC_ID_AV1;
        }

        AVPixelFormat GetHardwareFormat(AVCodecContext* context, const AVPixelFormat* formats)
        {
            for (auto const* format = formats; *format != AV_PIX_FMT_NONE; format++)
            {
                if (*format == AV_PIX_FMT_D3D11)
                {
                    return *format;
                }
            }

            // Offered no GPU format, or only ones whose hardware decoder failed to start: a profile
            // or a size the GPU does not decode. The decoder thread starts over in software.
            static_cast<DecoderState*>(context->opaque)->hardwareFailed = true;

            for (auto const* format = formats; *format != AV_PIX_FMT_NONE; format++)
            {
                if ((av_pix_fmt_desc_get(*format)->flags & AV_PIX_FMT_FLAG_HWACCEL) == 0)
                {
                    return *format;
                }
            }

            return AV_PIX_FMT_NONE;
        }

        AVCodecContext* CreateDecoder(MediaStreamSetup const& setup, bool video, AVBufferRef* hardware = nullptr, DecoderState* state = nullptr)
        {
            const AVCodecID id = setup.parameters->codec_id;
            const bool accelerated = video && hardware != nullptr && state != nullptr && SupportsHardware(id);

            // The native AV1 decoder decodes nothing on its own, so it only ever runs on the GPU,
            // and dav1d is what decodes AV1 in software.
            const AVCodec* codec = accelerated && id == AV_CODEC_ID_AV1
                ? avcodec_find_decoder_by_name("av1")
                : FindDecoder(id);

            if (codec == nullptr)
            {
                return nullptr;
            }

            AVCodecContext* context = avcodec_alloc_context3(codec);
            if (context == nullptr)
            {
                return nullptr;
            }

            avcodec_parameters_to_context(context, setup.parameters);
            context->pkt_timebase = setup.timeBase;

            if (accelerated)
            {
                state->hardwareFailed = false;

                context->hw_device_ctx = av_buffer_ref(hardware);
                context->get_format = &GetHardwareFormat;
                context->opaque = state;

                // Surfaces held outside the decoder: the frame queue, the one on screen, and the
                // one being drawn. Without them the decoder runs out of surfaces and stalls.
                context->extra_hw_frames = static_cast<int>(kVideoFrames) + 2;

                // The GPU decodes one frame at a time whatever the threads, and each thread would
                // hold surfaces of its own.
                context->thread_count = 1;
            }
            else if (video)
            {
                context->thread_count = 0;
                context->thread_type = FF_THREAD_FRAME | FF_THREAD_SLICE;
            }

            if (avcodec_open2(context, codec, nullptr) < 0)
            {
                LOGGER_WARNING(L"avcodec_open2 failed for codec {}", static_cast<int>(setup.parameters->codec_id));
                avcodec_free_context(&context);
            }

            return context;
        }

        void NameThread(const wchar_t* name) noexcept
        {
            SetThreadDescription(GetCurrentThread(), name);
        }

        // A plain file, played as the one quality it has.
        class SourceRendition : public IMediaRendition
        {
        public:
            explicit SourceRendition(std::shared_ptr<IMediaByteSource> source)
                : m_source(std::move(source))
            {
            }

            int32_t Width() override
            {
                return 0;
            }

            int32_t Height() override
            {
                return 0;
            }

            int64_t Size() override
            {
                return 0;
            }

            bool HasPlaylist() override
            {
                return false;
            }

            std::string LoadPlaylist() override
            {
                return {};
            }

            void CancelPlaylist() override
            {
            }

            std::shared_ptr<IMediaByteSource> CreateSource() override
            {
                return m_source;
            }

            double DownloadSpeed() override
            {
                return 0;
            }

        private:
            std::shared_ptr<IMediaByteSource> m_source;
        };
    }

    class MediaSession : public std::enable_shared_from_this<MediaSession>
    {
    public:
        MediaSession(std::shared_ptr<MediaEngine> engine, std::vector<std::shared_ptr<IMediaRendition>> renditions, int32_t rendition, bool automatic,
            std::shared_ptr<MediaVideoRenderer> renderer, std::shared_ptr<MediaSession> previous, double position, uint64_t id);
        ~MediaSession();

        MediaSession(const MediaSession&) = delete;
        MediaSession& operator=(const MediaSession&) = delete;

        void Start();
        void Shutdown();

        // A rendition of -1 keeps the current one. Quiet reports no change of state, and no end
        // reached once more, for a seek that only brings back what was already on screen.
        void Seek(double target, int32_t rendition = -1, bool quiet = false);

        // The caller paused or resumed.
        void Wake();

        void Rate(double rate);
        void Volume(double volume, bool mute);
        void Automatic(bool automatic);
        void AudioDeviceChanged();
        void Resized();
        void DeviceReset();

        double Position();

    private:
        // Seeks to where playback is, to start over everything that is decoded.
        void Resync();

        void DemuxMain();
        void VideoMain();
        void AudioMain();
        void PresentMain();

        bool Open();
        std::unique_ptr<MediaInput> OpenInput(int32_t rendition, double target);
        std::shared_ptr<MediaStreamSetup> CreateSetup(MediaInput const& input, int index, double startTime, bool video);
        MediaPlaylist const* Playlist(int32_t rendition);
        std::shared_ptr<IMediaByteSource> Activate(int32_t rendition);

        void Begin(int32_t serial, double target, bool seek);
        void Reposition(int32_t serial, double target, int32_t rendition);
        void ReadLoop();
        bool QueuesFull();
        void EndOfStream();

        void Adapt();
        bool SwitchAt(int32_t rendition, int32_t segment);
        int32_t StepDown(int32_t rendition) const;
        double Ahead(double position);

        bool TakeSeek(double& target, int32_t& serial, int32_t& rendition);
        bool HasPendingSeek();
        bool SeekInterrupts();
        void KeepRendition(int32_t rendition);
        void ReopenIfClosed();
        void RenditionShown(MediaStreamSetup const* setup);

        AVFrame* Normalize(AVFrame* frame);

        bool LoopAt(double time);

        double CurrentTime();
        void UpdatePlayback();
        void SetRunningLocked(bool running);
        void ReportPosition(double position);

        void Join();
        void Release();

        static int ReadPacket(void* opaque, uint8_t* buffer, int size);
        static int64_t SeekPacket(void* opaque, int64_t offset, int whence);

        const std::shared_ptr<MediaEngine> m_engine;
        const std::vector<std::shared_ptr<IMediaRendition>> m_renditions;
        const std::shared_ptr<MediaVideoRenderer> m_renderer;
        std::shared_ptr<MediaSession> m_previous;
        const uint64_t m_id;

        // Read from the renditions once, on the caller's thread: each is a call into managed code.
        std::vector<int32_t> m_heights;
        std::vector<int64_t> m_sizes;
        std::vector<bool> m_segmented;

        std::atomic<bool> m_abort{ false };

        // Bumped by every seek. Packets and frames carry the serial they were read under, and
        // whatever carries an old one is dropped wherever it is found.
        std::atomic<int32_t> m_serial{ 0 };
        std::atomic<int32_t> m_demuxSerial{ 0 };

        // Signalled once the demux thread has stopped using the source, which the next session
        // may then open again.
        HANDLE m_demuxExited{ nullptr };

        // Guards which source is being read, and whether it is open.
        std::mutex m_sourceLock;
        std::shared_ptr<IMediaByteSource> m_active;
        bool m_sourceClosed{ true };
        std::atomic<bool> m_reading{ false };
        std::atomic<bool> m_opened{ false };

        std::mutex m_seekLock;
        bool m_seekPending{ false };
        double m_seekTarget{ 0 };
        int32_t m_seekSerial{ 0 };
        int32_t m_seekRendition{ -1 };
        double m_startPosition{ 0 };

        // The demux thread's, all of them.
        std::unique_ptr<MediaInput> m_input;
        std::vector<std::shared_ptr<IMediaByteSource>> m_sources;
        std::vector<MediaPlaylist> m_playlists;
        std::vector<int8_t> m_playlistLoaded;

        // Qualities that failed to open, which automatic quality no longer moves to.
        std::vector<bool> m_unplayable;

        // The segment of the last packet read from the current input, -1 before the first.
        int32_t m_lastSegment{ -1 };

        double m_lastAdapt{ 0 };
        double m_lastSwitch{ 0 };
        double m_lastDown{ -kUpHold };

        // The quality last reported, which is the one on screen: a seamless switch is reported when
        // the decoder reaches it, not when the demuxer does, up to half a minute earlier.
        std::atomic<int32_t> m_rendition{ 0 };
        std::atomic<bool> m_automatic{ false };

        std::atomic<double> m_duration{ 0 };
        bool m_hasVideo{ false };
        bool m_hasAudio{ false };
        double m_frameDuration{ 1.0 / 30 };

        SwsContext* m_scaler{ nullptr };
        AVBufferPool* m_scalerPool{ nullptr };
        int m_scalerPoolSize{ 0 };

        MediaWaiter m_demuxWaiter;
        MediaWaiter m_presentWaiter;
        MediaWaiter m_audioWaiter;

        PacketQueue m_videoPackets;
        PacketQueue m_audioPackets{ &m_audioWaiter };
        FrameQueue m_frames{ kVideoFrames, &m_presentWaiter };
        std::unique_ptr<MediaAudioRenderer> m_audio;

        MediaClock m_clock;

        // The serial the audio renderer was last flushed for. Until it catches up with a seek, what
        // it holds belongs to where playback was, and says nothing about where it is going.
        std::atomic<int32_t> m_audioSerial{ -1 };

        std::atomic<bool> m_demuxEnded{ false };
        std::atomic<bool> m_readingPaused{ false };
        std::atomic<bool> m_videoBehind{ false };
        std::atomic<bool> m_running{ false };
        std::atomic<bool> m_stalledHint{ false };
        std::atomic<double> m_lastReport{ 0 };

        // The playback state, for the current serial. Seek resets it.
        std::mutex m_controlLock;
        bool m_prerolling{ true };
        double m_prerollTarget{ 0 };
        bool m_stalled{ false };
        double m_stalledAt{ 0 };
        bool m_ended{ false };
        double m_endPosition{ 0 };
        bool m_videoShown{ false };
        bool m_videoEnded{ false };
        double m_videoEnd{ 0 };
        bool m_videoOut{ false };
        bool m_quiet{ false };
        float m_lastBuffering{ -1 };

        std::thread m_demuxThread;
        std::thread m_videoThread;
        std::thread m_audioThread;
        std::thread m_presentThread;
    };

    MediaSession::MediaSession(std::shared_ptr<MediaEngine> engine, std::vector<std::shared_ptr<IMediaRendition>> renditions, int32_t rendition, bool automatic,
        std::shared_ptr<MediaVideoRenderer> renderer, std::shared_ptr<MediaSession> previous, double position, uint64_t id)
        : m_engine(std::move(engine))
        , m_renditions(std::move(renditions))
        , m_renderer(std::move(renderer))
        , m_previous(std::move(previous))
        , m_id(id)
        , m_startPosition(std::max(0.0, position))
        , m_prerollTarget(std::max(0.0, position))
        , m_videoEnd(std::max(0.0, position))
    {
        const size_t count = m_renditions.size();

        m_sources.resize(count);
        m_playlists.resize(count);
        m_playlistLoaded.resize(count);
        m_unplayable.resize(count);

        for (auto const& item : m_renditions)
        {
            int32_t height = 0;
            int64_t size = 0;
            bool segmented = false;

            try
            {
                height = item->Height();
                size = item->Size();
                segmented = item->HasPlaylist();
            }
            catch (...)
            {
                // A rendition that cannot answer is one automatic quality never moves to.
            }

            m_heights.push_back(height);
            m_sizes.push_back(size);
            m_segmented.push_back(segmented);
        }

        m_rendition = std::clamp(rendition, 0, std::max(0, static_cast<int32_t>(count) - 1));
        m_automatic = automatic;

        m_demuxExited = CreateEventExW(nullptr, nullptr, CREATE_EVENT_MANUAL_RESET, EVENT_ALL_ACCESS);
        m_clock.Set(m_startPosition);
        m_clock.Rate(m_engine->m_rate.load());
    }

    MediaSession::~MediaSession()
    {
        // Join() has run by now on every path but a session that never started.
        m_abort = true;
        Join();
        Release();

        if (m_demuxExited != nullptr)
        {
            CloseHandle(m_demuxExited);
        }
    }

    void MediaSession::Start()
    {
        m_demuxThread = std::thread([this] { DemuxMain(); });
    }

    void MediaSession::Shutdown()
    {
        {
            std::lock_guard<std::mutex> lock(m_sourceLock);

            m_abort = true;

            if (m_active && !m_sourceClosed)
            {
                try
                {
                    // Ends a read blocked on the network, which is the only thing the threads
                    // below can be waiting on for long.
                    m_active->Close();
                }
                catch (...)
                {
                    // The source is managed code; whatever it threw, the threads are told to stop.
                }
            }

            m_sourceClosed = true;
        }

        // The demux thread may be waiting on a playlist instead, which no source close ends.
        for (auto const& rendition : m_renditions)
        {
            try
            {
                rendition->CancelPlaylist();
            }
            catch (...)
            {
                // Then the thread pool waits on it, which only delays the release.
            }
        }

        m_videoPackets.Abort();
        m_audioPackets.Abort();
        m_frames.Abort();

        m_demuxWaiter.Signal();
        m_presentWaiter.Signal();
        m_audioWaiter.Signal();

        // Joining is left to the thread pool: the decoders finish whatever they hold first, and
        // the caller is the UI thread.
        RunOnThreadPool([self = shared_from_this()]
            {
                self->Join();
                self->Release();
            });
    }

    void MediaSession::Join()
    {
        // The demux thread first: it is the one that starts the others.
        if (m_demuxThread.joinable())
        {
            m_demuxThread.join();
        }

        if (m_videoThread.joinable())
        {
            m_videoThread.join();
        }

        if (m_audioThread.joinable())
        {
            m_audioThread.join();
        }

        if (m_presentThread.joinable())
        {
            m_presentThread.join();
        }
    }

    void MediaSession::Release()
    {
        m_frames.Clear();
        m_videoPackets.Flush();
        m_audioPackets.Flush();

        m_audio.reset();
        m_input.reset();

        sws_freeContext(m_scaler);
        m_scaler = nullptr;

        av_buffer_pool_uninit(&m_scalerPool);

        m_previous.reset();
    }

    void MediaSession::DemuxMain()
    {
        NameThread(L"Media demux");

        if (m_previous)
        {
            // The previous session may be reading the same source. Opening it again before that
            // read has given up would hand the read back a source that is open, and leave it
            // moving the offset this session reads from.
            WaitForSingleObjectEx(m_previous->m_demuxExited, INFINITE, FALSE);
            m_previous.reset();
        }

        if (!m_abort)
        {
            if (Open())
            {
                ReadLoop();
            }
            else if (!m_abort)
            {
                m_engine->ReportError(m_id);
            }
        }

        SetEvent(m_demuxExited);
    }

    MediaPlaylist const* MediaSession::Playlist(int32_t rendition)
    {
        if (!m_segmented[rendition])
        {
            return nullptr;
        }

        if (m_playlistLoaded[rendition] == 0)
        {
            std::string text;

            try
            {
                text = m_renditions[rendition]->LoadPlaylist();
            }
            catch (...)
            {
                // Then it plays as a plain file.
            }

            const bool parsed = !text.empty() && MediaPlaylist::Parse(text, m_playlists[rendition]);
            m_playlistLoaded[rendition] = parsed ? 1 : -1;

            if (!parsed)
            {
                LOGGER_WARNING(L"No usable playlist for rendition {}, playing it as a plain file", rendition);
            }
        }

        return m_playlistLoaded[rendition] == 1 ? &m_playlists[rendition] : nullptr;
    }

    std::shared_ptr<IMediaByteSource> MediaSession::Activate(int32_t rendition)
    {
        if (!m_sources[rendition])
        {
            try
            {
                m_sources[rendition] = m_renditions[rendition]->CreateSource();
            }
            catch (...)
            {
                return nullptr;
            }
        }

        auto source = m_sources[rendition];

        if (!source)
        {
            return nullptr;
        }

        std::lock_guard<std::mutex> lock(m_sourceLock);

        if (m_abort)
        {
            return nullptr;
        }

        if (m_active != source)
        {
            // Only one quality downloads at a time: closing the one left behind cancels what it
            // was still fetching ahead.
            if (m_active && !m_sourceClosed)
            {
                try
                {
                    m_active->Close();
                }
                catch (...)
                {
                    // It only stops downloading ahead; nothing here depends on it.
                }
            }

            m_active = source;
            m_sourceClosed = true;
        }

        if (m_sourceClosed)
        {
            try
            {
                source->Open();
            }
            catch (...)
            {
                return nullptr;
            }

            m_sourceClosed = false;
        }

        return source;
    }

    std::shared_ptr<MediaStreamSetup> MediaSession::CreateSetup(MediaInput const& input, int index, double startTime, bool video)
    {
        AVStream* stream = input.format->streams[index];

        if (FindDecoder(stream->codecpar->codec_id) == nullptr)
        {
            LOGGER_WARNING(L"No decoder for codec {}", static_cast<int>(stream->codecpar->codec_id));
            return nullptr;
        }

        auto setup = std::make_shared<MediaStreamSetup>();
        setup->parameters = avcodec_parameters_alloc();

        if (setup->parameters == nullptr || avcodec_parameters_copy(setup->parameters, stream->codecpar) < 0)
        {
            return nullptr;
        }

        setup->timeBase = stream->time_base;
        setup->startTime = startTime;
        setup->rendition = input.rendition;

        if (video)
        {
            const AVRational rate = av_guess_frame_rate(input.format, stream, nullptr);
            if (rate.num > 0 && rate.den > 0)
            {
                setup->frameDuration = av_q2d(av_inv_q(rate));
            }

            setup->format.rotation = RotationOf(stream);
            setup->format.aspect = av_guess_sample_aspect_ratio(input.format, stream, nullptr);
        }

        return setup;
    }

    std::unique_ptr<MediaInput> MediaSession::OpenInput(int32_t rendition, double target)
    {
        if (rendition < 0 || rendition >= static_cast<int32_t>(m_renditions.size()))
        {
            rendition = 0;
        }

        const double started = MediaNow();

        // Before the source: it can block on downloading the playlist, and nothing is read yet.
        MediaPlaylist const* playlist = Playlist(rendition);

        auto source = Activate(rendition);
        if (!source)
        {
            return nullptr;
        }

        auto input = std::make_unique<MediaInput>();
        input->session = this;
        input->rendition = rendition;
        input->source = source;

        if (playlist)
        {
            input->map = std::make_unique<MediaSegmentMap>(*playlist, playlist->SegmentAt(target));
            input->size = input->map->Size();
            input->window = DownloadWindow(m_sizes[rendition], playlist->Duration());
        }
        else
        {
            try
            {
                input->size = source->Size();
            }
            catch (...)
            {
                return nullptr;
            }

            // The duration is unknown until the file is open; the smallest window until then.
            input->window = DownloadWindow(0, 0);
        }

        input->format = avformat_alloc_context();
        if (input->format == nullptr)
        {
            return nullptr;
        }

        auto* buffer = static_cast<uint8_t*>(av_malloc(kIoBufferSize));
        if (buffer == nullptr)
        {
            return nullptr;
        }

        input->io = avio_alloc_context(buffer, kIoBufferSize, 0, input.get(), &ReadPacket, nullptr, &SeekPacket);
        if (input->io == nullptr)
        {
            av_free(buffer);
            return nullptr;
        }

        // Read front to back only: every seek or switch opens a new input. Seekable, the mov demuxer
        // walks to every moof in the file while opening it, to index fragments it never needs, and
        // over the network each of those is a download somewhere else in the file.
        if (playlist)
        {
            input->io->seekable = 0;
        }

        input->format->pb = input->io;
        input->format->flags |= AVFMT_FLAG_CUSTOM_IO;

        // Nothing a file names is ever fetched: the playlist demuxers would otherwise open any URL
        // they find in one.
        AVDictionary* options = nullptr;
        av_dict_set(&options, "protocol_whitelist", "", 0);

        int result = avformat_open_input(&input->format, nullptr, nullptr, &options);
        av_dict_free(&options);

        if (result < 0)
        {
            // avformat_open_input frees the context it was given when it fails.
            if (!m_abort && !HasPendingSeek())
            {
                LOGGER_WARNING(L"avformat_open_input failed: {}", result);
            }

            return nullptr;
        }

        // A quality's init section already says everything there is to know, and reading packets
        // to find out more would delay every switch.
        if (playlist == nullptr)
        {
            result = avformat_find_stream_info(input->format, nullptr);
            if (result < 0 && !m_abort)
            {
                LOGGER_WARNING(L"avformat_find_stream_info failed: {}", result);
            }
        }

        if (m_abort)
        {
            return nullptr;
        }

        if (playlist == nullptr && input->format->duration != AV_NOPTS_VALUE && input->format->duration > 0)
        {
            input->window = DownloadWindow(input->size, static_cast<double>(input->format->duration) / AV_TIME_BASE);
        }

        const double elapsed = MediaNow() - started;

        if (elapsed > 2)
        {
            LOGGER_WARNING(L"Opening rendition {} at {:.1f}s took {:.1f}s", rendition, target, elapsed);
        }
        else
        {
            LOGGER_INFO(L"Opened rendition {} at {:.1f}s in {:.0f}ms", rendition, target, elapsed * 1000);
        }

        // A quality opened at a segment reports that segment's time as where it starts, but the
        // timestamps in it are the whole video's already.
        const double startTime = playlist == nullptr && input->format->start_time != AV_NOPTS_VALUE
            ? static_cast<double>(input->format->start_time) / AV_TIME_BASE
            : 0;

        auto const& engineOptions = m_engine->m_options;

        // The first file decides which kinds of stream play; another quality only fills them.
        if (engineOptions.video && m_renderer && (!m_opened || m_hasVideo))
        {
            const int index = av_find_best_stream(input->format, AVMEDIA_TYPE_VIDEO, -1, -1, nullptr, 0);

            // Cover art in an audio file is a picture, not a video.
            if (index >= 0 && (input->format->streams[index]->disposition & AV_DISPOSITION_ATTACHED_PIC) == 0)
            {
                input->video = CreateSetup(*input, index, startTime, true);
                input->videoIndex = input->video ? index : -1;
            }
        }

        if (engineOptions.audio && (!m_opened || m_hasAudio))
        {
            const int index = av_find_best_stream(input->format, AVMEDIA_TYPE_AUDIO, -1, input->videoIndex, nullptr, 0);

            if (index >= 0)
            {
                input->audio = CreateSetup(*input, index, startTime, false);
                input->audioIndex = input->audio ? index : -1;
            }
        }

        for (unsigned index = 0; index < input->format->nb_streams; index++)
        {
            if (static_cast<int>(index) != input->videoIndex && static_cast<int>(index) != input->audioIndex)
            {
                input->format->streams[index]->discard = AVDISCARD_ALL;
            }
        }

        return input;
    }

    bool MediaSession::Open()
    {
        // Where the caller asked to start, unless a seek moved that while the file was opening.
        double target;
        int32_t serial;
        int32_t rendition;

        if (!TakeSeek(target, serial, rendition))
        {
            target = m_startPosition;
            serial = m_serial.load();
            rendition = -1;
        }

        if (rendition < 0)
        {
            rendition = m_rendition.load();
        }

        std::unique_ptr<MediaInput> input;

        while (true)
        {
            input = OpenInput(rendition, target);

            // A quality picked while the first was still opening ends that open, and is opened
            // in its place: on a slow link the first may take a long while.
            int32_t next;

            if (input || m_abort || !TakeSeek(target, serial, next))
            {
                break;
            }

            rendition = next >= 0 ? next : rendition;
        }

        if (!input)
        {
            return false;
        }

        if (!input->video && !input->audio)
        {
            LOGGER_WARNING(L"Nothing playable in {} stream(s)", input->format->nb_streams);
            return false;
        }

        if (input->audio)
        {
            auto audio = std::make_unique<MediaAudioRenderer>(m_audioWaiter);

            if (audio->Open(input->audio->parameters->sample_rate, m_engine->m_rate.load(), m_engine->m_volume.load(), m_engine->m_mute.load()))
            {
                {
                    std::lock_guard<std::mutex> lock(m_controlLock);
                    m_audio = std::move(audio);
                }

                // A change made while the output was being built did not reach it.
                m_audio->Rate(m_engine->m_rate.load());
                m_audio->Volume(m_engine->m_volume.load(), m_engine->m_mute.load());
            }
            else
            {
                // No output device: the picture still plays, on its own clock.
                LOGGER_WARNING(L"No audio output, playing without audio");

                input->format->streams[input->audioIndex]->discard = AVDISCARD_ALL;
                input->audio = nullptr;
                input->audioIndex = -1;
            }
        }

        m_hasVideo = input->video != nullptr;
        m_hasAudio = input->audio != nullptr;

        if (!m_hasVideo && !m_hasAudio)
        {
            return false;
        }

        if (m_hasVideo)
        {
            m_frameDuration = input->video->frameDuration;
        }

        if (auto const* playlist = Playlist(input->rendition))
        {
            m_duration = playlist->Duration();
        }
        else if (input->format->duration != AV_NOPTS_VALUE && input->format->duration > 0)
        {
            m_duration = static_cast<double>(input->format->duration) / AV_TIME_BASE;
        }

        m_engine->ReportDuration(m_id, m_duration.load());

        if (m_hasVideo)
        {
            auto const* parameters = input->video->parameters;
            auto const& format = input->video->format;

            double width = parameters->width;
            double height = parameters->height;

            if (format.aspect.num > 0 && format.aspect.den > 0)
            {
                width = width * format.aspect.num / format.aspect.den;
            }

            if (format.rotation == 90 || format.rotation == 270)
            {
                std::swap(width, height);
            }

            m_engine->ReportStream(m_id, MediaStreamKind::Video, input->videoIndex, static_cast<int32_t>(std::lround(width)), static_cast<int32_t>(std::lround(height)));
        }

        if (m_hasAudio)
        {
            m_engine->ReportStream(m_id, MediaStreamKind::Audio, input->audioIndex, 0, 0);
        }

        m_rendition = input->rendition;
        m_engine->ReportRendition(m_id, input->rendition);

        const bool seek = input->map == nullptr && target > 0;
        m_input = std::move(input);

        if (m_hasVideo)
        {
            m_videoThread = std::thread([this] { VideoMain(); });
            m_presentThread = std::thread([this] { PresentMain(); });
        }

        if (m_hasAudio)
        {
            m_audioThread = std::thread([this] { AudioMain(); });
        }

        m_opened = true;
        Begin(serial, target, seek);

        return true;
    }

    void MediaSession::Begin(int32_t serial, double target, bool seek)
    {
        if (seek)
        {
            const double startTime = m_input->video ? m_input->video->startTime : m_input->audio->startTime;
            const int64_t timestamp = static_cast<int64_t>((target + startTime) * AV_TIME_BASE);

            // A read the last seek interrupted leaves its error on the context.
            m_input->io->error = 0;
            m_input->io->eof_reached = 0;

            // Backward only: allowed to land after the target, a seek reads the whole file when the
            // target is past the last keyframe in the index. The decoders drop what comes before
            // the target, so landing early costs decoding time but never shows the wrong frame.
            const int result = avformat_seek_file(m_input->format, -1, INT64_MIN, timestamp, timestamp, 0);

            if (result < 0 && !m_abort)
            {
                LOGGER_WARNING(L"Seeking to {} failed: {}", target, result);
            }
        }

        m_demuxSerial = serial;
        m_demuxEnded = false;
        m_lastSegment = -1;

        if (m_hasVideo)
        {
            m_videoPackets.Push({ MediaPacket::Kind::Flush, nullptr, serial, target, -1, NAN, m_input->video });
        }

        if (m_hasAudio)
        {
            m_audioPackets.Push({ MediaPacket::Kind::Flush, nullptr, serial, target, -1, NAN, m_input->audio });
        }
    }

    void MediaSession::Reposition(int32_t serial, double target, int32_t rendition)
    {
        if (rendition < 0)
        {
            rendition = m_input->rendition;
        }

        // Nothing to read from: end here rather than wait for packets that never come. Flushed for
        // the new serial first, or the decoders would drop the end as stale.
        auto fail = [&]()
            {
                if (!m_abort && !HasPendingSeek())
                {
                    Begin(serial, target, false);
                    EndOfStream();
                }
            };

        // A plain file seeks in place. A quality is opened again at the segment the target is in,
        // which costs its init section and nothing more.
        if (rendition == m_input->rendition && m_input->map == nullptr)
        {
            // A change of quality a seek interrupted leaves that quality's source the active one,
            // and this one closed.
            if (!Activate(rendition))
            {
                fail();
                return;
            }

            Begin(serial, target, true);
            return;
        }

        auto input = OpenInput(rendition, target);

        if (!input && !m_abort && rendition != m_input->rendition)
        {
            if (HasPendingSeek())
            {
                // Interrupted rather than failed: the seek that did it still goes to this quality.
                KeepRendition(rendition);
                return;
            }

            LOGGER_WARNING(L"Rendition {} could not be opened, staying on {}", rendition, m_input->rendition);

            m_unplayable[rendition] = true;
            input = OpenInput(m_input->rendition, target);
        }

        if (!input)
        {
            fail();
            return;
        }

        const bool changed = input->rendition != m_input->rendition;
        const bool seek = input->map == nullptr;

        // The same quality has the same parameters, so its decoders only flush rather than being
        // built again on every step of a scrub.
        if (!changed)
        {
            input->video = input->video && m_input->video ? m_input->video : input->video;
            input->audio = input->audio && m_input->audio ? m_input->audio : input->audio;
        }

        m_input = std::move(input);

        // Compared with what was last shown rather than what was read: a seamless switch the
        // demuxer made may not have reached the screen, and never will now.
        if (m_rendition.exchange(m_input->rendition) != m_input->rendition)
        {
            m_engine->ReportRendition(m_id, m_input->rendition);
        }

        Begin(serial, target, seek);
    }

    bool MediaSession::TakeSeek(double& target, int32_t& serial, int32_t& rendition)
    {
        std::lock_guard<std::mutex> lock(m_seekLock);

        if (!m_seekPending)
        {
            return false;
        }

        m_seekPending = false;
        target = m_seekTarget;
        serial = m_seekSerial;
        rendition = m_seekRendition;
        m_seekRendition = -1;

        return true;
    }

    bool MediaSession::HasPendingSeek()
    {
        std::lock_guard<std::mutex> lock(m_seekLock);
        return m_seekPending;
    }

    bool MediaSession::SeekInterrupts()
    {
        std::lock_guard<std::mutex> lock(m_seekLock);

        // While the first file opens, only a change of quality is worth starting over for: a
        // plain seek is applied once it is open.
        return m_seekPending && (m_opened || m_seekRendition >= 0);
    }

    void MediaSession::KeepRendition(int32_t rendition)
    {
        std::lock_guard<std::mutex> lock(m_seekLock);

        if (m_seekPending && m_seekRendition < 0)
        {
            m_seekRendition = rendition;
        }
    }

    void MediaSession::RenditionShown(MediaStreamSetup const* setup)
    {
        if (setup != nullptr && setup->rendition >= 0 && m_rendition.exchange(setup->rendition) != setup->rendition)
        {
            m_engine->ReportRendition(m_id, setup->rendition);
        }
    }

    void MediaSession::ReopenIfClosed()
    {
        std::lock_guard<std::mutex> lock(m_sourceLock);

        if (m_sourceClosed && m_active && !m_abort)
        {
            try
            {
                m_active->Open();
            }
            catch (...)
            {
                // The read that follows reports it.
            }

            m_sourceClosed = false;
        }
    }

    void MediaSession::EndOfStream()
    {
        const int32_t current = m_demuxSerial.load();

        if (m_hasVideo)
        {
            m_videoPackets.Push({ MediaPacket::Kind::Drain, nullptr, current });
        }

        if (m_hasAudio)
        {
            m_audioPackets.Push({ MediaPacket::Kind::Drain, nullptr, current });
        }

        m_demuxEnded = true;
        UpdatePlayback();
    }

    void MediaSession::ReadLoop()
    {
        AVPacket* packet = av_packet_alloc();
        int errors = 0;

        while (!m_abort && packet != nullptr)
        {
            double target;
            int32_t serial;
            int32_t rendition;

            if (TakeSeek(target, serial, rendition))
            {
                Reposition(serial, target, rendition);
                errors = 0;
                continue;
            }

            if (m_demuxEnded)
            {
                m_demuxWaiter.Wait(0.25);
                continue;
            }

            Adapt();

            if (QueuesFull())
            {
                m_readingPaused = true;

                if (m_stalledHint)
                {
                    UpdatePlayback();
                }

                m_demuxWaiter.Wait(0.05);
                continue;
            }

            m_readingPaused = false;

            const int result = av_read_frame(m_input->format, packet);

            if (result < 0)
            {
                if (m_abort || HasPendingSeek())
                {
                    continue;
                }

                if (result == AVERROR(EAGAIN))
                {
                    continue;
                }

                auto* io = m_input->io;

                // A damaged packet is skipped, as long as it is a packet and not the file ending.
                if (result != AVERROR_EOF && !avio_feof(io) && io->error == 0 && ++errors < kMaxReadErrors)
                {
                    continue;
                }

                if (result != AVERROR_EOF && !avio_feof(io))
                {
                    LOGGER_WARNING(L"av_read_frame failed: {}", result);
                }

                EndOfStream();
                continue;
            }

            errors = 0;

            PacketQueue* queue = nullptr;
            MediaStreamSetup const* setup = nullptr;

            if (packet->stream_index == m_input->videoIndex)
            {
                queue = &m_videoPackets;
                setup = m_input->video.get();
            }
            else if (packet->stream_index == m_input->audioIndex)
            {
                queue = &m_audioPackets;
                setup = m_input->audio.get();
            }

            if (queue == nullptr)
            {
                av_packet_unref(packet);
                continue;
            }

            double end = ToSeconds(packet->pts != AV_NOPTS_VALUE ? packet->pts : packet->dts, setup->timeBase);

            if (!std::isnan(end))
            {
                end -= setup->startTime;

                if (packet->duration > 0)
                {
                    end += ToSeconds(packet->duration, setup->timeBase);
                }
            }

            const int32_t segment = m_input->map ? m_input->map->SegmentOf(packet->pos) : -1;

            AVPacket* item = av_packet_alloc();
            if (item == nullptr)
            {
                av_packet_unref(packet);
                continue;
            }

            av_packet_move_ref(item, packet);
            queue->Push({ MediaPacket::Kind::Data, item, m_demuxSerial.load(), 0, segment, end });

            if (segment >= 0)
            {
                m_lastSegment = segment;
            }

            if (m_stalledHint)
            {
                UpdatePlayback();
            }
        }

        av_packet_free(&packet);
    }

    double MediaSession::Ahead(double position)
    {
        double ahead = std::numeric_limits<double>::infinity();

        if (m_hasVideo)
        {
            ahead = std::min(ahead, m_videoPackets.End() - position);
        }

        if (m_hasAudio)
        {
            ahead = std::min(ahead, m_audioPackets.End() - position);
        }

        return ahead;
    }

    bool MediaSession::QueuesFull()
    {
        if (m_videoPackets.Bytes() + m_audioPackets.Bytes() >= kMaxQueuedBytes)
        {
            return true;
        }

        return Ahead(CurrentTime()) >= kMaxAhead;
    }

    int32_t MediaSession::StepDown(int32_t rendition) const
    {
        int32_t lower = -1;

        for (int32_t index = 0; index < static_cast<int32_t>(m_renditions.size()); index++)
        {
            if (m_segmented[index] && !m_unplayable[index] && m_heights[index] < m_heights[rendition] && (lower < 0 || m_heights[index] > m_heights[lower]))
            {
                lower = index;
            }
        }

        return lower;
    }

    void MediaSession::Adapt()
    {
        const double now = MediaNow();

        if (now - m_lastAdapt < kAdaptInterval)
        {
            return;
        }

        m_lastAdapt = now;

        const double duration = m_duration.load();
        const int32_t current = m_input->rendition;

        // Only between segmented qualities: a plain file, the original, has nothing to splice at.
        if (!m_automatic || m_renditions.size() < 2 || m_input->map == nullptr || duration <= 0)
        {
            return;
        }

        bool stalled;
        bool prerolling;
        double stalledAt;

        {
            std::lock_guard<std::mutex> lock(m_controlLock);

            stalled = m_stalled;
            prerolling = m_prerolling;
            stalledAt = m_stalledAt;
        }

        // Already waiting, so a step down right away costs nothing that is not being lost anyway.
        if (stalled && now - stalledAt >= kStallDown)
        {
            const int32_t lower = StepDown(current);

            if (lower >= 0)
            {
                LOGGER_INFO(L"Stalled for {:.1f}s, down from {}p to {}p", now - stalledAt, m_heights[current], m_heights[lower]);

                m_lastDown = now;
                m_lastSwitch = now;
                Seek(Position(), lower);
            }

            return;
        }

        if (stalled || prerolling || m_demuxEnded || now - m_input->openedAt < kSpeedSettle || now - m_lastSwitch < kSwitchHold)
        {
            return;
        }

        double speed = 0;

        try
        {
            speed = m_renditions[current]->DownloadSpeed();
        }
        catch (...)
        {
            return;
        }

        if (speed <= 0)
        {
            return;
        }

        const double rate = std::max(0.25, m_engine->m_rate.load());
        const double position = CurrentTime();
        const double ahead = Ahead(position);

        int32_t target = -1;

        if (speed < m_sizes[current] / duration * rate && ahead < kDownAhead)
        {
            // The link no longer keeps up with this quality and what is buffered is running down:
            // step down at the next boundary, before it runs out.
            target = StepDown(current);
        }
        else if (ahead >= kUpBuffered && now - m_lastDown >= kUpHold)
        {
            for (int32_t index = 0; index < static_cast<int32_t>(m_renditions.size()); index++)
            {
                const bool better = m_segmented[index] && !m_unplayable[index] && m_heights[index] > m_heights[current]
                    && (target < 0 || m_heights[index] > m_heights[target]);

                if (better && speed >= m_sizes[index] / duration * rate * kUpHeadroom)
                {
                    target = index;
                }
            }
        }

        if (target < 0)
        {
            return;
        }

        auto const* playlist = Playlist(current);
        int32_t segment = playlist ? playlist->SegmentFrom(position + kSwitchMargin) : -1;

        // Not past where the demuxer has read to: what lies between there and the boundary would
        // never be read. A step up can wait for it. A step down cannot, and takes the segment being
        // read instead, which works as long as no decoder has reached it.
        if (segment > m_lastSegment)
        {
            segment = m_heights[target] < m_heights[current] ? m_lastSegment : -1;
        }

        if (segment < 0)
        {
            return;
        }

        if (SwitchAt(target, segment))
        {
            LOGGER_INFO(L"{:.0f} KB/s, {} from {}p to {}p at {:.1f}s", speed / 1024,
                m_heights[target] > m_heights[current] ? L"up" : L"down", m_heights[current], m_heights[target], playlist->segments[segment].start);

            m_lastSwitch = now;

            if (m_heights[target] < m_heights[current])
            {
                m_lastDown = now;
            }
        }
    }

    bool MediaSession::SwitchAt(int32_t rendition, int32_t segment)
    {
        // Loaded before anything is dropped: it can block on the download, and fail.
        auto const* playlist = Playlist(rendition);
        auto const* current = Playlist(m_input->rendition);

        if (playlist == nullptr)
        {
            // Its playlist failed to load, and is not asked for again.
            m_unplayable[rendition] = true;
            return false;
        }

        // Only at a boundary the demuxer has read to, see Adapt.
        if (current == nullptr || segment >= static_cast<int32_t>(current->segments.size()) || segment > m_lastSegment || HasPendingSeek())
        {
            return false;
        }

        if (!PacketQueue::TrimFrom(m_videoPackets, m_audioPackets, segment))
        {
            return false;
        }

        const double start = current->segments[segment].start;
        auto input = OpenInput(rendition, start);

        if (!input && !m_abort && !HasPendingSeek())
        {
            LOGGER_WARNING(L"Rendition {} could not be opened, staying on {}", rendition, m_input->rendition);
            m_unplayable[rendition] = true;

            // What followed the boundary in this quality was dropped already, so it is read again.
            input = OpenInput(m_input->rendition, start);
        }

        if (!input)
        {
            if (!m_abort && !HasPendingSeek())
            {
                EndOfStream();
            }

            return false;
        }

        const bool changed = input->rendition != m_input->rendition;

        // The same quality read again goes on from where it was cut, through the same decoders.
        if (!changed)
        {
            input->video = input->video && m_input->video ? m_input->video : input->video;
            input->audio = input->audio && m_input->audio ? m_input->audio : input->audio;
        }

        m_input = std::move(input);
        m_demuxEnded = false;
        m_lastSegment = -1;

        if (changed)
        {
            const int32_t serial = m_demuxSerial.load();

            if (m_hasVideo && m_input->video)
            {
                m_videoPackets.Push({ MediaPacket::Kind::Switch, nullptr, serial, start, -1, NAN, m_input->video });
            }

            if (m_hasAudio && m_input->audio)
            {
                m_audioPackets.Push({ MediaPacket::Kind::Switch, nullptr, serial, start, -1, NAN, m_input->audio });
            }
        }

        return changed;
    }

    int MediaSession::ReadPacket(void* opaque, uint8_t* buffer, int size)
    {
        auto* input = static_cast<MediaInput*>(opaque);
        auto* self = input->session;

        while (true)
        {
            if (self->m_abort)
            {
                return AVERROR_EXIT;
            }

            self->ReopenIfClosed();

            // Published before checking for a seek, which publishes the other way round, so that
            // a seek either finds this read in progress and closes the source under it, or this
            // read finds the seek and never starts.
            self->m_reading = true;

            if (self->SeekInterrupts())
            {
                self->m_reading = false;
                return AVERROR_EXIT;
            }

            int64_t offset = input->position;
            int64_t count = size;
            int64_t window = input->window;

            if (input->map)
            {
                int64_t available = 0;

                if (!input->map->Locate(input->position, offset, available))
                {
                    self->m_reading = false;
                    return AVERROR_EOF;
                }

                count = std::min(count, available);

                // The init section is all that is wanted from the start of the file. A window there
                // would start downloading parts that the next read, a segment further on, cancels.
                if (input->map->SegmentOf(input->position) < 0)
                {
                    window = available;
                }
            }

            const double started = MediaNow();
            int64_t read;

            try
            {
                read = input->source->Read(offset, buffer, count, window);
            }
            catch (...)
            {
                read = IMediaByteSource::ReadFailed;
            }

            self->m_reading = false;

            if (read > 0)
            {
                input->position += read;
                return static_cast<int>(read);
            }

            if (read == 0)
            {
                return AVERROR_EOF;
            }

            if (self->m_abort || self->SeekInterrupts())
            {
                return AVERROR_EXIT;
            }

            if (read != IMediaByteSource::ReadStalled)
            {
                return AVERROR(EIO);
            }

            // RemoteFileSource gives up after 30 seconds without a byte. The file can still come,
            // and the player already shows that it is waiting, so it waits on: a seek or a close is
            // what ends this. A closed source answers at once, and is not asked again in a spin.
            if (MediaNow() - started < 0.01)
            {
                self->m_demuxWaiter.Wait(0.05);
            }
        }
    }

    int64_t MediaSession::SeekPacket(void* opaque, int64_t offset, int whence)
    {
        auto* input = static_cast<MediaInput*>(opaque);
        whence &= ~AVSEEK_FORCE;

        if (whence == AVSEEK_SIZE)
        {
            return input->size > 0 ? input->size : AVERROR(ENOSYS);
        }

        int64_t position;

        switch (whence)
        {
        case SEEK_SET:
            position = offset;
            break;
        case SEEK_CUR:
            position = input->position + offset;
            break;
        case SEEK_END:
            if (input->size <= 0)
            {
                return AVERROR(ENOSYS);
            }

            position = input->size + offset;
            break;
        default:
            return AVERROR(EINVAL);
        }

        if (position < 0)
        {
            return AVERROR(EINVAL);
        }

        // Only remembered: nothing is read, or asked for, until a read needs it.
        input->position = position;
        return position;
    }

    AVFrame* MediaSession::Normalize(AVFrame* frame)
    {
        switch (frame->format)
        {
        case AV_PIX_FMT_D3D11:
        case AV_PIX_FMT_NV12:
        case AV_PIX_FMT_YUV420P:
        case AV_PIX_FMT_YUVJ420P:
        {
            AVFrame* output = av_frame_alloc();
            if (output != nullptr)
            {
                av_frame_move_ref(output, frame);
            }

            return output;
        }
        default:
            break;
        }

        // Anything else - 10 bit, 4:2:2, 4:4:4, RGB - is brought down to NV12 here, on the
        // decoder's thread rather than the presenter's.
        m_scaler = sws_getCachedContext(m_scaler,
            frame->width, frame->height, static_cast<AVPixelFormat>(frame->format),
            frame->width, frame->height, AV_PIX_FMT_NV12,
            SWS_BILINEAR, nullptr, nullptr, nullptr);

        if (m_scaler == nullptr)
        {
            return nullptr;
        }

        const int size = av_image_get_buffer_size(AV_PIX_FMT_NV12, frame->width, frame->height, 32);
        if (size <= 0)
        {
            return nullptr;
        }

        // Pooled, because this runs for every frame of such a video.
        if (m_scalerPool == nullptr || m_scalerPoolSize != size)
        {
            av_buffer_pool_uninit(&m_scalerPool);
            m_scalerPool = av_buffer_pool_init(size, nullptr);
            m_scalerPoolSize = size;
        }

        AVFrame* output = av_frame_alloc();
        if (output == nullptr)
        {
            return nullptr;
        }

        output->buf[0] = av_buffer_pool_get(m_scalerPool);

        if (output->buf[0] == nullptr)
        {
            av_frame_free(&output);
            return nullptr;
        }

        output->format = AV_PIX_FMT_NV12;
        output->width = frame->width;
        output->height = frame->height;

        av_image_fill_arrays(output->data, output->linesize, output->buf[0]->data, AV_PIX_FMT_NV12, frame->width, frame->height, 32);
        sws_scale(m_scaler, frame->data, frame->linesize, 0, frame->height, output->data, output->linesize);
        av_frame_copy_props(output, frame);

        return output;
    }

    void MediaSession::VideoMain()
    {
        NameThread(L"Media video decoder");

        AVFrame* frame = av_frame_alloc();
        AVCodecContext* codec = nullptr;
        std::shared_ptr<MediaStreamSetup> setup;

        // Shared by every decoder of the session, and the renderer's: what is decoded into it is
        // drawn where it is. Taken again whenever the renderer has moved to a new device.
        uint64_t generation = m_renderer->Generation();
        AVBufferRef* hardware = m_renderer->HardwareDevice();
        DecoderState state;

        auto refreshDevice = [&]()
            {
                const uint64_t current = m_renderer->Generation();

                if (current == generation)
                {
                    return false;
                }

                generation = current;
                av_buffer_unref(&hardware);
                hardware = m_renderer->HardwareDevice();

                return true;
            };

        int32_t serial = -1;
        double target = 0;
        double nextPts = 0;
        bool pushed = false;

        // The last picture before the target, shown if the target turns out to be past the end.
        VideoFrame last;

        auto receive = [&]()
            {
                while (!m_abort && codec != nullptr && avcodec_receive_frame(codec, frame) >= 0)
                {
                    double pts = ToSeconds(frame->best_effort_timestamp, setup->timeBase);
                    pts = std::isnan(pts) ? nextPts : pts - setup->startTime;

                    const double duration = frame->duration > 0 ? ToSeconds(frame->duration, setup->timeBase) : setup->frameDuration;
                    nextPts = pts + duration;

                    if (pts + duration <= target + 0.001)
                    {
                        av_frame_free(&last.frame);
                        last.frame = av_frame_alloc();

                        if (last.frame != nullptr)
                        {
                            av_frame_move_ref(last.frame, frame);
                            last = { last.frame, pts, duration, serial, setup->format };
                        }

                        av_frame_unref(frame);
                        continue;
                    }

                    AVFrame* output = Normalize(frame);
                    av_frame_unref(frame);

                    if (output == nullptr)
                    {
                        continue;
                    }

                    av_frame_free(&last.frame);

                    if (!m_frames.Push({ output, pts, duration, serial, setup->format }, m_serial))
                    {
                        break;
                    }

                    pushed = true;
                }
            };

        // A decoder per file: another quality comes with its own parameters, which the decoder of
        // the previous one cannot take on.
        auto open = [&](std::shared_ptr<MediaStreamSetup> const& next)
            {
                avcodec_free_context(&codec);

                setup = next;
                codec = setup ? CreateDecoder(*setup, true, hardware, &state) : nullptr;

                // AV1 on the GPU has its own decoder, which fails to open where dav1d would not.
                if (codec == nullptr && setup && hardware != nullptr)
                {
                    codec = CreateDecoder(*setup, true);
                }

                if (codec == nullptr)
                {
                    // Nothing will be decoded, so the end is now, rather than playback waiting
                    // for a picture forever.
                    m_frames.Push({ nullptr, nextPts, 0, serial }, m_serial);
                }
            };

        while (!m_abort && frame != nullptr)
        {
            MediaPacket item;
            if (!m_videoPackets.Pop(item, std::chrono::milliseconds(100)))
            {
                continue;
            }

            if (item.serial != m_serial.load())
            {
                av_packet_free(&item.packet);
                continue;
            }

            switch (item.kind)
            {
            case MediaPacket::Kind::Flush:
                av_frame_free(&last.frame);

                serial = item.serial;
                target = item.target;
                nextPts = target;
                pushed = false;

                // A decoder on a lost device decodes nothing more, so it is built again. One that
                // failed to open is tried again: it marks the end only for the serial it failed in.
                if (refreshDevice() || item.setup != setup || codec == nullptr)
                {
                    open(item.setup);
                }
                else if (codec != nullptr)
                {
                    avcodec_flush_buffers(codec);
                }

                break;

            case MediaPacket::Kind::Switch:
                // Everything the previous quality still holds plays before the boundary.
                if (codec != nullptr)
                {
                    avcodec_send_packet(codec, nullptr);
                    receive();
                }

                refreshDevice();
                open(item.setup);

                RenditionShown(item.setup.get());
                break;

            case MediaPacket::Kind::Drain:
                if (codec != nullptr)
                {
                    avcodec_send_packet(codec, nullptr);
                    receive();
                }

                if (!pushed && last.frame != nullptr)
                {
                    AVFrame* output = Normalize(last.frame);
                    av_frame_free(&last.frame);

                    if (output != nullptr && m_frames.Push({ output, last.pts, last.duration, serial, last.format }, m_serial))
                    {
                        pushed = true;
                    }
                }

                m_frames.Push({ nullptr, nextPts, 0, serial }, m_serial);

                // A drained decoder takes nothing more until it is flushed, which a seek after
                // the end would otherwise find it still needing.
                if (codec != nullptr)
                {
                    avcodec_flush_buffers(codec);
                }

                break;

            case MediaPacket::Kind::Data:
            {
                if (codec == nullptr)
                {
                    av_packet_free(&item.packet);
                    break;
                }

                // Catching up to a seek target, every frame decoded is thrown away, and one that
                // nothing refers to cannot change the frame that is shown: skipping those makes a
                // seek into a long group of pictures several times faster.
                const double packetPts = ToSeconds(item.packet->pts, setup->timeBase) - setup->startTime;
                const bool catchingUp = !std::isnan(packetPts) && packetPts + setup->frameDuration <= target - 0.001;

                codec->skip_frame = catchingUp || m_videoBehind ? AVDISCARD_NONREF : AVDISCARD_DEFAULT;

                int result = avcodec_send_packet(codec, item.packet);

                // The first packet is where a decoder learns what the stream is, and where it finds
                // out the GPU cannot take it. Started over in software, with its threads, before any
                // frame of it is received, so nothing is decoded twice.
                if (state.hardwareFailed && codec->hw_device_ctx != nullptr)
                {
                    LOGGER_INFO(L"Codec {} at {}x{} is not decoded by the GPU, decoding on the CPU", static_cast<int>(setup->parameters->codec_id), setup->parameters->width, setup->parameters->height);

                    state.hardwareFailed = false;
                    avcodec_free_context(&codec);

                    codec = CreateDecoder(*setup, true);

                    if (codec == nullptr)
                    {
                        av_packet_free(&item.packet);
                        m_frames.Push({ nullptr, nextPts, 0, serial }, m_serial);
                        break;
                    }

                    result = avcodec_send_packet(codec, item.packet);
                }

                while (result == AVERROR(EAGAIN) && !m_abort)
                {
                    receive();
                    result = avcodec_send_packet(codec, item.packet);
                }

                if (result < 0 && result != AVERROR_EOF && codec->hw_device_ctx != nullptr)
                {
                    m_renderer->CheckDevice();
                }

                receive();
                av_packet_free(&item.packet);
                break;
            }
            }
        }

        av_frame_free(&last.frame);
        av_frame_free(&frame);
        avcodec_free_context(&codec);
        av_buffer_unref(&hardware);
    }

    void MediaSession::PresentMain()
    {
        NameThread(L"Media presenter");
        SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_ABOVE_NORMAL);

        int32_t shown = -1;

        while (!m_abort)
        {
            m_renderer->Refresh(m_id);

            const int32_t serial = m_serial.load();
            VideoFrame* front = m_frames.Front();

            while (front != nullptr && front->serial != serial)
            {
                m_frames.Pop();
                front = m_frames.Front();
            }

            if (front == nullptr)
            {
                UpdatePlayback();
                m_presentWaiter.Wait(0.05);
                continue;
            }

            if (front->frame == nullptr)
            {
                // The end of the stream. Kept at the front until a seek drops it, so that this
                // goes on knowing.
                {
                    std::lock_guard<std::mutex> lock(m_controlLock);

                    if (serial == m_serial.load())
                    {
                        m_videoEnded = true;
                    }
                }

                UpdatePlayback();
                m_presentWaiter.Wait(0.05);
                continue;
            }

            if (shown != serial)
            {
                // The first picture after opening or seeking goes up at once, paused or not, so
                // that scrubbing shows where it landed.
                m_renderer->Render(m_id, front->frame, front->format);
                shown = serial;

                bool videoOut = false;

                {
                    std::lock_guard<std::mutex> lock(m_controlLock);

                    if (serial == m_serial.load())
                    {
                        m_videoShown = true;
                        m_videoEnd = front->pts + front->duration;
                    }

                    videoOut = !m_videoOut;
                    m_videoOut = true;
                }

                m_frames.Pop();

                if (videoOut)
                {
                    m_engine->ReportVideoOut(m_id);
                }

                UpdatePlayback();
                continue;
            }

            if (!m_running)
            {
                UpdatePlayback();
                m_presentWaiter.Wait(0.05);
                continue;
            }

            const double now = CurrentTime();

            if (front->pts > now)
            {
                const double rate = std::max(0.1, m_engine->m_rate.load());
                m_presentWaiter.Wait(std::min((front->pts - now) / rate, 0.05));
                continue;
            }

            // The first picture past the end of a loop is never shown.
            if (LoopAt(front->pts))
            {
                continue;
            }

            VideoFrame* next = m_frames.Next();

            if (next != nullptr && next->frame != nullptr && next->serial == serial && next->pts <= now)
            {
                // Late, and the next one is already due.
                m_videoBehind = now - front->pts > kVideoBehind;
                m_frames.Pop();
                continue;
            }

            m_videoBehind = false;
            m_renderer->Render(m_id, front->frame, front->format);

            {
                std::lock_guard<std::mutex> lock(m_controlLock);

                if (serial == m_serial.load())
                {
                    m_videoEnd = front->pts + front->duration;
                }
            }

            m_frames.Pop();
            ReportPosition(now);
        }
    }

    void MediaSession::AudioMain()
    {
        NameThread(L"Media audio");
        SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_ABOVE_NORMAL);

        AVFrame* frame = av_frame_alloc();
        AVCodecContext* codec = nullptr;
        std::shared_ptr<MediaStreamSetup> setup;

        int32_t flushed = -1;
        double target = 0;
        double nextPts = 0;

        auto receive = [&]()
            {
                while (!m_abort && codec != nullptr && avcodec_receive_frame(codec, frame) >= 0)
                {
                    double pts = ToSeconds(frame->best_effort_timestamp, setup->timeBase);
                    pts = std::isnan(pts) ? nextPts : pts - setup->startTime;

                    if (frame->sample_rate > 0)
                    {
                        nextPts = pts + static_cast<double>(frame->nb_samples) / frame->sample_rate;
                    }

                    m_audio->Enqueue(frame, pts, target);
                    av_frame_unref(frame);
                }
            };

        auto open = [&](std::shared_ptr<MediaStreamSetup> const& next)
            {
                avcodec_free_context(&codec);

                setup = next;
                codec = setup ? CreateDecoder(*setup, false) : nullptr;

                if (codec == nullptr)
                {
                    m_audio->EndOfStream();
                }
            };

        while (!m_abort && frame != nullptr)
        {
            const int32_t current = m_serial.load();

            // At once, rather than when the flush reaches the queue: what was playing stops as
            // soon as the seek is asked for.
            if (flushed != current)
            {
                m_audio->Flush();
                flushed = current;
                m_audioSerial = current;
            }

            m_audio->Pump(m_running);

            // What was decoded while there was no output is gone, and the decoder ran ahead
            // through the queue meanwhile: started over from where the picture is.
            if (m_audio->TakeRecovered())
            {
                Resync();
                continue;
            }

            UpdatePlayback();

            if (!m_hasVideo && m_running)
            {
                const double now = CurrentTime();

                if (LoopAt(now))
                {
                    continue;
                }

                ReportPosition(now);
            }

            bool decoded = false;
            MediaPacket item;

            if (m_audio->Pending() < kAudioDecodeAhead && m_audioPackets.TryPop(item))
            {
                decoded = true;

                if (item.serial != current)
                {
                    av_packet_free(&item.packet);
                    continue;
                }

                switch (item.kind)
                {
                case MediaPacket::Kind::Flush:
                    target = item.target;
                    nextPts = target;

                    if (item.setup != setup || codec == nullptr)
                    {
                        open(item.setup);
                    }
                    else
                    {
                        avcodec_flush_buffers(codec);
                    }

                    break;

                case MediaPacket::Kind::Switch:
                    // The renderer carries on: the new quality's sound picks up where the old one
                    // ends, and any overlap or gap at the boundary is smoothed over there.
                    if (codec != nullptr)
                    {
                        avcodec_send_packet(codec, nullptr);
                        receive();
                    }

                    open(item.setup);

                    if (!m_hasVideo)
                    {
                        RenditionShown(item.setup.get());
                    }

                    break;

                case MediaPacket::Kind::Drain:
                    if (codec != nullptr)
                    {
                        avcodec_send_packet(codec, nullptr);
                        receive();
                        avcodec_flush_buffers(codec);
                    }

                    m_audio->EndOfStream();
                    break;

                case MediaPacket::Kind::Data:
                {
                    if (codec == nullptr)
                    {
                        av_packet_free(&item.packet);
                        break;
                    }

                    int result = avcodec_send_packet(codec, item.packet);

                    while (result == AVERROR(EAGAIN) && !m_abort)
                    {
                        receive();
                        result = avcodec_send_packet(codec, item.packet);
                    }

                    receive();
                    av_packet_free(&item.packet);
                    break;
                }
                }
            }

            if (!decoded)
            {
                // Woken by a buffer finishing, a packet arriving, or a command.
                m_audioWaiter.Wait(0.05);
            }
        }

        av_frame_free(&frame);
        avcodec_free_context(&codec);
    }

    bool MediaSession::LoopAt(double time)
    {
        const double end = m_engine->m_loopEnd.load();

        if (end <= 0 || time < end)
        {
            return false;
        }

        Seek(m_engine->m_loopStart.load());
        return true;
    }

    double MediaSession::CurrentTime()
    {
        double time;
        const int32_t serial = m_serial.load();

        if (m_audio && m_audioSerial.load() == serial && m_audio->Clock(time))
        {
            m_clock.Sync(time, serial);
            return time;
        }

        return m_clock.Now();
    }

    void MediaSession::SetRunningLocked(bool running)
    {
        if (m_running.exchange(running) != running)
        {
            m_clock.Run(running);

            m_presentWaiter.Signal();
            m_audioWaiter.Signal();
        }
    }

    void MediaSession::UpdatePlayback()
    {
        std::lock_guard<std::mutex> lock(m_controlLock);

        if (m_abort || !m_opened)
        {
            return;
        }

        const bool paused = m_engine->m_paused.load();
        const bool hasVideo = m_hasVideo;
        const bool hasAudio = m_hasAudio;
        const bool audioCurrent = hasAudio && m_audioSerial.load() == m_serial.load();

        const bool audioEnded = !hasAudio || m_audio->Failed() || (audioCurrent && m_audio->Finished());
        const bool videoEnded = !hasVideo || m_videoEnded;

        // The sound of the file ended before its picture: nothing to wait for.
        const bool audioGone = hasVideo && !videoEnded && m_videoPackets.End() - m_audioPackets.End() > kAudioGone;

        if (m_prerolling)
        {
            const bool videoReady = !hasVideo || m_videoShown || m_videoEnded;

            // Less audio left than the preroll, all of it submitted, is as ready as it gets: it is
            // only played once the clock starts.
            const bool audioReady = !hasAudio || audioEnded
                || (audioCurrent && (m_audio->Buffered() >= kAudioPreroll || m_audio->EndSubmitted()));

            if (!videoReady || !audioReady)
            {
                return;
            }

            m_prerolling = false;
            m_stalled = false;
            m_stalledHint = false;
            m_clock.Set(m_prerollTarget);

            m_lastBuffering = 100;
            m_engine->ReportBuffering(m_id, 100);
            m_engine->ReportPosition(m_id, m_prerollTarget);
        }

        if (!m_ended && videoEnded && audioEnded && m_demuxEnded)
        {
            const double now = CurrentTime();

            // The last picture is still on screen for as long as it lasts.
            if (!hasVideo || now >= m_videoEnd)
            {
                m_ended = true;
                m_stalled = false;
                m_stalledHint = false;
                m_endPosition = m_duration.load() > 0 ? m_duration.load() : now;

                SetRunningLocked(false);

                if (!m_quiet)
                {
                    m_engine->ReportEnded(m_id, m_endPosition);
                }

                return;
            }
        }

        if (m_ended)
        {
            SetRunningLocked(false);
            return;
        }

        if (m_stalled)
        {
            const double now = CurrentTime();
            const double duration = m_duration.load();
            const double need = duration > 0
                ? std::min(kResumeAhead, std::max(0.0, duration - now))
                : kResumeAhead;

            double ahead = std::numeric_limits<double>::infinity();

            if (hasVideo && !videoEnded)
            {
                ahead = std::min(ahead, m_videoPackets.End() - now);
            }

            if (hasAudio && !audioEnded && !audioGone)
            {
                ahead = std::min(ahead, m_audioPackets.End() - now);
            }

            // Reading paused counts as enough: the queues are as full as they are allowed to be.
            if (m_demuxEnded || m_readingPaused || ahead >= need)
            {
                m_stalled = false;
                m_stalledHint = false;
                m_lastBuffering = 100;
                m_engine->ReportBuffering(m_id, 100);
            }
            else
            {
                const float progress = static_cast<float>(std::clamp(ahead / need * 100, 0.0, 99.0));

                if (std::abs(progress - m_lastBuffering) >= 1)
                {
                    m_lastBuffering = progress;
                    m_engine->ReportBuffering(m_id, progress);
                }
            }
        }
        else if (m_running && !m_demuxEnded)
        {
            const double now = CurrentTime();

            // Starved means nothing left to play and nothing waiting to be decoded either: a
            // decoder that is merely slow is not a reason to stop the clock. Nor is one that
            // decodes nothing, as on a lost device, with the demuxer as far ahead as it goes:
            // waiting would not bring a picture, and only flip the buffering on and off.
            const bool videoStarved = hasVideo && !videoEnded && !m_readingPaused
                && m_frames.Front() == nullptr && m_videoPackets.Empty()
                && now > m_videoEnd + m_frameDuration * 2;

            const bool audioStarved = audioCurrent && !audioEnded && !audioGone
                && m_audio->Starved() && m_audioPackets.Empty();

            if (videoStarved || audioStarved)
            {
                m_stalled = true;
                m_stalledAt = MediaNow();
                m_stalledHint = true;
                m_lastBuffering = 0;
                m_engine->ReportBuffering(m_id, 0);
            }
        }

        SetRunningLocked(!m_stalled && !paused);

        if (!m_quiet)
        {
            m_engine->SetState(m_id, paused ? MediaEngineState::Paused : MediaEngineState::Playing);
        }
    }

    void MediaSession::ReportPosition(double position)
    {
        const double now = MediaNow();
        double last = m_lastReport.load();

        if (now - last < kPositionInterval || !m_lastReport.compare_exchange_strong(last, now))
        {
            return;
        }

        if (m_duration.load() > 0)
        {
            position = std::min(position, m_duration.load());
        }

        m_engine->ReportPosition(m_id, std::max(0.0, position));
    }

    void MediaSession::Seek(double target, int32_t rendition, bool quiet)
    {
        target = std::max(0.0, target);

        if (m_duration.load() > 0)
        {
            target = std::min(target, m_duration.load());
        }

        int32_t serial;
        bool interrupt;

        {
            std::lock_guard<std::mutex> lock(m_seekLock);

            // A plain seek arriving before a pending change of quality keeps the change.
            if (rendition >= 0 || !m_seekPending)
            {
                m_seekRendition = rendition;
            }

            m_seekPending = true;
            m_seekTarget = target;
            m_seekSerial = serial = ++m_serial;

            interrupt = m_opened || m_seekRendition >= 0;

            // Inside the lock: the demux thread takes the seek under it, and the Flush it then
            // queues for the new serial must not be among what is flushed here.
            m_videoPackets.Flush();
            m_audioPackets.Flush();
        }

        {
            std::lock_guard<std::mutex> lock(m_controlLock);

            m_prerolling = true;
            m_prerollTarget = target;
            m_stalled = false;
            m_stalledHint = false;
            m_ended = false;
            m_videoShown = false;
            m_videoEnded = false;
            m_videoEnd = target;
            m_quiet = quiet;

            SetRunningLocked(false);
            m_clock.Set(target, serial);
        }

        m_frames.Wake();

        if (interrupt && m_reading)
        {
            std::lock_guard<std::mutex> lock(m_sourceLock);

            // The read in progress is for where playback was. Closing the source is the only way
            // to end it early, and the next read opens it again.
            if (!m_abort && !m_sourceClosed && m_reading && m_active)
            {
                m_sourceClosed = true;

                try
                {
                    m_active->Close();
                }
                catch (...)
                {
                    // Then the read ends on its own, and the seek waits for it.
                }
            }
        }

        m_demuxWaiter.Signal();
        m_presentWaiter.Signal();
        m_audioWaiter.Signal();
    }

    void MediaSession::Wake()
    {
        UpdatePlayback();

        m_presentWaiter.Signal();
        m_audioWaiter.Signal();
    }

    void MediaSession::Rate(double rate)
    {
        m_clock.Rate(rate);

        {
            // The demux thread assigns the renderer while the file opens.
            std::lock_guard<std::mutex> lock(m_controlLock);

            if (m_audio)
            {
                m_audio->Rate(rate);
            }
        }

        m_presentWaiter.Signal();
    }

    void MediaSession::Volume(double volume, bool mute)
    {
        std::lock_guard<std::mutex> lock(m_controlLock);

        if (m_audio)
        {
            m_audio->Volume(volume, mute);
        }
    }

    void MediaSession::Automatic(bool automatic)
    {
        m_automatic = automatic;
    }

    void MediaSession::AudioDeviceChanged()
    {
        std::lock_guard<std::mutex> lock(m_controlLock);

        if (m_audio)
        {
            m_audio->RequestRebuild();
        }
    }

    void MediaSession::Resized()
    {
        m_presentWaiter.Signal();
    }

    void MediaSession::DeviceReset()
    {
        // Every frame decoded so far is on the lost device, and so are the decoders. A seek to where
        // playback is drops the frames, and its flush is where the decoders pick up the new device.
        if (m_opened && m_hasVideo)
        {
            Resync();
        }
    }

    void MediaSession::Resync()
    {
        bool ended;

        {
            std::lock_guard<std::mutex> lock(m_controlLock);
            ended = m_ended;
        }

        // At the end, only the last picture comes back: the end is not reached, and reported, again.
        Seek(Position(), -1, ended);
    }

    double MediaSession::Position()
    {
        {
            std::lock_guard<std::mutex> lock(m_controlLock);

            if (m_prerolling)
            {
                return m_prerollTarget;
            }

            if (m_ended)
            {
                return m_endPosition;
            }
        }

        const double position = std::max(0.0, CurrentTime());
        const double duration = m_duration.load();
        return duration > 0 ? std::min(position, duration) : position;
    }

    std::shared_ptr<MediaEngine> MediaEngine::Create(MediaEngineOptions const& options, std::shared_ptr<IMediaEngineHost> host, std::shared_ptr<MediaVideoRenderer> renderer)
    {
        auto engine = std::make_shared<MediaEngine>(options, std::move(host), renderer);

        if (renderer)
        {
            const std::weak_ptr<MediaEngine> weak = engine;

            renderer->Attach(
                [weak]()
                {
                    if (auto strong = weak.lock())
                    {
                        strong->RendererResized();
                    }
                },
                [weak]()
                {
                    if (auto strong = weak.lock())
                    {
                        strong->RendererReset();
                    }
                });
        }

        return engine;
    }

    MediaEngine::MediaEngine(MediaEngineOptions const& options, std::shared_ptr<IMediaEngineHost> host, std::shared_ptr<MediaVideoRenderer> renderer)
        : m_options(options)
        , m_host(std::move(host))
        , m_renderer(std::move(renderer))
        , m_rate(options.rate)
        , m_volume(options.volume)
        , m_mute(options.mute)
    {
    }

    MediaEngine::~MediaEngine()
    {
        Close();
    }

    void MediaEngine::StartLocked(double position)
    {
        auto previous = std::move(m_session);

        if (previous)
        {
            previous->Shutdown();
        }
        else
        {
            previous = std::move(m_stopped);
        }

        m_stopped = nullptr;

        const uint64_t id = ++m_sessions;

        {
            // Before the session starts, so that an error it reports at once is not overwritten.
            std::lock_guard<std::mutex> lock(m_reportLock);

            m_current = id;
            SetStateLocked(MediaEngineState::Opening);
        }

        if (m_renderer)
        {
            m_renderer->Activate(id);
        }

        m_duration = 0;
        m_position = position;

        // Only a plain source is shared with the previous session, which then has to let go of it
        // first. Qualities hand every session sources of their own.
        m_session = std::make_shared<MediaSession>(shared_from_this(), m_renditions, m_rendition.load(), m_automatic.load(), m_renderer,
            m_shared ? std::move(previous) : nullptr, position, id);
        m_session->Start();
    }

    void MediaEngine::SetStateLocked(MediaEngineState state)
    {
        if (m_state.exchange(state) != state)
        {
            m_host->StateChanged(state);
        }
    }

    void MediaEngine::Open(std::shared_ptr<IMediaByteSource> source, double position)
    {
        OpenRenditions({ std::make_shared<SourceRendition>(std::move(source)) }, 0, false, position, true);
    }

    void MediaEngine::Open(std::vector<std::shared_ptr<IMediaRendition>> renditions, int32_t rendition, bool automatic, double position)
    {
        OpenRenditions(std::move(renditions), rendition, automatic, position, false);
    }

    void MediaEngine::OpenRenditions(std::vector<std::shared_ptr<IMediaRendition>> renditions, int32_t rendition, bool automatic, double position, bool shared)
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_closed || renditions.empty())
        {
            return;
        }

        m_shared = shared;
        m_renditions = std::move(renditions);
        m_rendition = std::clamp(rendition, 0, static_cast<int32_t>(m_renditions.size()) - 1);
        m_automatic = automatic;
        m_paused = false;

        StartLocked(position);
    }

    void MediaEngine::SelectRendition(int32_t rendition)
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_closed || !m_session || rendition >= static_cast<int32_t>(m_renditions.size()))
        {
            return;
        }

        if (rendition < 0)
        {
            // Automatic picks up from wherever it is now; nothing has to be reopened for that.
            m_automatic = true;
            m_session->Automatic(true);
        }
        else
        {
            m_automatic = false;
            m_session->Automatic(false);

            // At once, from where playback is: the picture stays up meanwhile, and a pick by hand
            // is something to see right away, not at the next boundary.
            if (rendition != m_rendition.load())
            {
                m_session->Seek(m_session->Position(), rendition);
            }
        }

        m_host->RenditionChanged();
    }

    void MediaEngine::Play()
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_closed)
        {
            return;
        }

        m_paused = false;

        if (!m_session)
        {
            // Stopped, or never started: play again from the start.
            if (!m_renditions.empty())
            {
                StartLocked(0);
            }

            return;
        }

        if (m_state.load() == MediaEngineState::Ended)
        {
            m_session->Seek(0);
            return;
        }

        m_session->Wake();
    }

    void MediaEngine::Pause(bool pause)
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_closed || !m_session)
        {
            return;
        }

        m_paused = pause;

        // Paused at the end, a player starts over, as libvlc's did.
        if (m_state.load() == MediaEngineState::Ended)
        {
            m_session->Seek(0);
            return;
        }

        m_session->Wake();
    }

    void MediaEngine::Toggle()
    {
        if (m_state.load() == MediaEngineState::Ended || m_paused.load())
        {
            Play();
        }
        else
        {
            Pause(true);
        }
    }

    void MediaEngine::Stop()
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_closed)
        {
            return;
        }

        if (m_session)
        {
            m_session->Shutdown();
            m_stopped = std::move(m_session);
        }

        {
            // An id no session has, so that what the one just stopped still reports goes nowhere,
            // and nothing it still has queued is drawn over a panel the caller is about to clear.
            std::lock_guard<std::mutex> lock(m_reportLock);

            m_current = ++m_sessions;
            SetStateLocked(MediaEngineState::Stopped);
        }

        m_position = 0;

        if (m_renderer)
        {
            m_renderer->Activate(0);
        }
    }

    void MediaEngine::Close()
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_closed)
        {
            return;
        }

        m_closed = true;

        if (m_session)
        {
            m_session->Shutdown();
            m_session = nullptr;
        }

        m_stopped = nullptr;

        {
            std::lock_guard<std::mutex> lock(m_reportLock);
            m_current = ++m_sessions;
        }

        m_renditions.clear();

        if (m_renderer)
        {
            m_renderer->Activate(0);
            m_renderer->Detach();
        }
    }

    void MediaEngine::Seek(double position)
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_closed)
        {
            return;
        }

        position = std::max(0.0, position);

        // Read back at once by callers deciding what to do next, before the seek has happened.
        m_position = position;

        if (!m_session)
        {
            return;
        }

        // Seeking at the end plays again, as it did with libvlc.
        if (m_state.load() == MediaEngineState::Ended)
        {
            m_paused = false;
        }

        m_session->Seek(position);
    }

    void MediaEngine::SeekBy(double offset)
    {
        Seek(Position() + offset);
    }

    void MediaEngine::Loop(double start, double end)
    {
        m_loopStart = std::max(0.0, start);
        m_loopEnd = end > start ? end : 0;
    }

    double MediaEngine::Position()
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_session)
        {
            return m_session->Position();
        }

        return m_position.load();
    }

    void MediaEngine::Rate(double value)
    {
        m_rate = value;

        std::lock_guard<std::mutex> lock(m_lock);

        if (m_session)
        {
            m_session->Rate(value);
        }
    }

    void MediaEngine::Volume(double value)
    {
        m_volume = value;

        {
            std::lock_guard<std::mutex> lock(m_lock);

            if (m_session)
            {
                m_session->Volume(value, m_mute.load());
            }
        }

        m_host->VolumeChanged();
    }

    void MediaEngine::Mute(bool value)
    {
        m_mute = value;

        {
            std::lock_guard<std::mutex> lock(m_lock);

            if (m_session)
            {
                m_session->Volume(m_volume.load(), value);
            }
        }

        m_host->VolumeChanged();
    }

    void MediaEngine::AudioDeviceChanged()
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_session)
        {
            m_session->AudioDeviceChanged();
        }
    }

    void MediaEngine::RendererResized()
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_session)
        {
            m_session->Resized();
        }
    }

    void MediaEngine::RendererReset()
    {
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_session)
        {
            m_session->DeviceReset();
        }
    }

    void MediaEngine::SetState(uint64_t session, MediaEngineState state)
    {
        std::lock_guard<std::mutex> lock(m_reportLock);

        if (IsCurrent(session))
        {
            SetStateLocked(state);
        }
    }

    void MediaEngine::ReportPosition(uint64_t session, double position)
    {
        std::lock_guard<std::mutex> lock(m_reportLock);

        if (IsCurrent(session))
        {
            m_position = position;
            m_host->PositionChanged(position);
        }
    }

    void MediaEngine::ReportDuration(uint64_t session, double duration)
    {
        std::lock_guard<std::mutex> lock(m_reportLock);

        if (IsCurrent(session))
        {
            m_duration = duration;

            if (duration > 0)
            {
                m_host->DurationChanged(duration);
            }
        }
    }

    void MediaEngine::ReportBuffering(uint64_t session, float progress)
    {
        std::lock_guard<std::mutex> lock(m_reportLock);

        if (IsCurrent(session))
        {
            m_host->Buffering(progress);
        }
    }

    void MediaEngine::ReportStream(uint64_t session, MediaStreamKind kind, int32_t id, int32_t width, int32_t height)
    {
        std::lock_guard<std::mutex> lock(m_reportLock);

        if (IsCurrent(session))
        {
            m_host->StreamSelected(kind, id, width, height);
        }
    }

    void MediaEngine::ReportVideoOut(uint64_t session)
    {
        std::lock_guard<std::mutex> lock(m_reportLock);

        if (IsCurrent(session))
        {
            m_host->VideoOut();
        }
    }

    void MediaEngine::ReportEnded(uint64_t session, double position)
    {
        std::lock_guard<std::mutex> lock(m_reportLock);

        if (IsCurrent(session))
        {
            m_position = position;
            m_host->PositionChanged(position);

            if (m_state.exchange(MediaEngineState::Ended) != MediaEngineState::Ended)
            {
                m_host->StateChanged(MediaEngineState::Ended);
                m_host->EndReached();
            }
        }
    }

    void MediaEngine::ReportError(uint64_t session)
    {
        std::lock_guard<std::mutex> lock(m_reportLock);

        if (IsCurrent(session))
        {
            if (m_state.exchange(MediaEngineState::Error) != MediaEngineState::Error)
            {
                m_host->StateChanged(MediaEngineState::Error);
            }

            m_host->EncounteredError();
        }
    }

    void MediaEngine::ReportRendition(uint64_t session, int32_t rendition)
    {
        std::lock_guard<std::mutex> lock(m_reportLock);

        if (IsCurrent(session))
        {
            m_rendition = rendition;
            m_host->RenditionChanged();
        }
    }
}
