#include "pch.h"
#include "MediaEngine.h"
#include "MediaAudioRenderer.h"

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
#include <libswscale/swscale.h>
}

namespace winrt::Telegram::Native::Media::implementation
{
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

        template <typename Func>
        void RunOnThreadPool(Func&& func)
        {
            using Work = std::decay_t<Func>;
            auto* work = new Work(std::forward<Func>(func));

            const BOOL submitted = TrySubmitThreadpoolCallback([](PTP_CALLBACK_INSTANCE, PVOID context)
                {
                    std::unique_ptr<Work> owned(static_cast<Work*>(context));
                    (*owned)();
                }, work, nullptr);

            if (!submitted)
            {
                std::unique_ptr<Work> owned(work);
                (*owned)();
            }
        }

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

        void NameThread(const wchar_t* name) noexcept
        {
            SetThreadDescription(GetCurrentThread(), name);
        }
    }

    class MediaSession : public std::enable_shared_from_this<MediaSession>
    {
    public:
        MediaSession(std::shared_ptr<MediaEngine> engine, std::shared_ptr<IMediaByteSource> source, std::shared_ptr<MediaVideoRenderer> renderer,
            std::shared_ptr<MediaSession> previous, double position, uint64_t id);
        ~MediaSession();

        MediaSession(const MediaSession&) = delete;
        MediaSession& operator=(const MediaSession&) = delete;

        void Start();
        void Shutdown();

        void Seek(double target);

        // The caller paused or resumed.
        void Wake();

        void Rate(double rate);
        void Volume(double volume, bool mute);
        void AudioDeviceChanged();
        void Resized();

        double Position();

    private:
        void DemuxMain();
        void VideoMain();
        void AudioMain();
        void PresentMain();

        bool Open();
        bool OpenVideo(int index);
        bool OpenAudio(int index);
        void Begin(int32_t serial, double target, bool seek);
        void ReadLoop();
        bool QueuesFull();

        bool TakeSeek(double& target, int32_t& serial);
        bool HasPendingSeek();
        void ReopenIfClosed();

        AVFrame* Normalize(AVFrame* frame);

        double CurrentTime();
        void UpdatePlayback();
        void SetRunningLocked(bool running);
        void ReportPosition(double position);

        void Join();
        void Release();

        static int ReadPacket(void* opaque, uint8_t* buffer, int size);
        static int64_t SeekPacket(void* opaque, int64_t offset, int whence);

        const std::shared_ptr<MediaEngine> m_engine;
        const std::shared_ptr<IMediaByteSource> m_source;
        const std::shared_ptr<MediaVideoRenderer> m_renderer;
        std::shared_ptr<MediaSession> m_previous;
        const uint64_t m_id;

        std::atomic<bool> m_abort{ false };

        // Bumped by every seek. Packets and frames carry the serial they were read under, and
        // whatever carries an old one is dropped wherever it is found.
        std::atomic<int32_t> m_serial{ 0 };
        std::atomic<int32_t> m_demuxSerial{ 0 };

        // Signalled once the demux thread has stopped using the source, which the next session
        // may then open again.
        HANDLE m_demuxExited{ nullptr };

        std::mutex m_sourceLock;
        bool m_sourceClosed{ false };
        std::atomic<bool> m_reading{ false };
        std::atomic<bool> m_opened{ false };

        std::mutex m_seekLock;
        bool m_seekPending{ false };
        double m_seekTarget{ 0 };
        int32_t m_seekSerial{ 0 };
        double m_startPosition{ 0 };

        AVFormatContext* m_format{ nullptr };
        AVIOContext* m_io{ nullptr };
        int64_t m_ioOffset{ 0 };
        int64_t m_size{ 0 };
        double m_startTime{ 0 };
        std::atomic<double> m_duration{ 0 };

        int m_videoIndex{ -1 };
        int m_audioIndex{ -1 };
        AVCodecContext* m_videoCodec{ nullptr };
        AVCodecContext* m_audioCodec{ nullptr };
        AVRational m_videoBase{ 0, 1 };
        AVRational m_audioBase{ 0, 1 };
        double m_frameDuration{ 1.0 / 30 };
        MediaVideoFormat m_videoFormat;

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
        bool m_ended{ false };
        double m_endPosition{ 0 };
        bool m_videoShown{ false };
        bool m_videoEnded{ false };
        double m_videoEnd{ 0 };
        bool m_videoOut{ false };
        float m_lastBuffering{ -1 };

        std::thread m_demuxThread;
        std::thread m_videoThread;
        std::thread m_audioThread;
        std::thread m_presentThread;
    };

    MediaSession::MediaSession(std::shared_ptr<MediaEngine> engine, std::shared_ptr<IMediaByteSource> source, std::shared_ptr<MediaVideoRenderer> renderer,
        std::shared_ptr<MediaSession> previous, double position, uint64_t id)
        : m_engine(std::move(engine))
        , m_source(std::move(source))
        , m_renderer(std::move(renderer))
        , m_previous(std::move(previous))
        , m_id(id)
        , m_startPosition(std::max(0.0, position))
        , m_prerollTarget(std::max(0.0, position))
        , m_videoEnd(std::max(0.0, position))
    {
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
            m_sourceClosed = true;

            try
            {
                // Ends a read blocked on the network, which is the only thing the threads below
                // can be waiting on for long.
                m_source->Close();
            }
            catch (...)
            {
                // The source is managed code; whatever it threw, the threads are told to stop.
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

        avcodec_free_context(&m_videoCodec);
        avcodec_free_context(&m_audioCodec);

        sws_freeContext(m_scaler);
        m_scaler = nullptr;

        av_buffer_pool_uninit(&m_scalerPool);

        // A custom AVIOContext is not the format's to free.
        avformat_close_input(&m_format);

        if (m_io != nullptr)
        {
            av_freep(&m_io->buffer);
            avio_context_free(&m_io);
        }

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

    bool MediaSession::Open()
    {
        {
            std::lock_guard<std::mutex> lock(m_sourceLock);

            if (m_abort)
            {
                return false;
            }

            try
            {
                m_source->Open();
                m_size = m_source->Size();
            }
            catch (...)
            {
                LOGGER_WARNING(L"The source could not be opened");
                return false;
            }
        }

        m_format = avformat_alloc_context();
        if (m_format == nullptr)
        {
            return false;
        }

        auto* buffer = static_cast<uint8_t*>(av_malloc(kIoBufferSize));
        if (buffer == nullptr)
        {
            return false;
        }

        m_io = avio_alloc_context(buffer, kIoBufferSize, 0, this, &ReadPacket, nullptr, &SeekPacket);
        if (m_io == nullptr)
        {
            av_free(buffer);
            return false;
        }

        m_format->pb = m_io;
        m_format->flags |= AVFMT_FLAG_CUSTOM_IO;

        // Nothing a file names is ever fetched: the playlist demuxers would otherwise open any URL
        // they find in one.
        AVDictionary* options = nullptr;
        av_dict_set(&options, "protocol_whitelist", "", 0);

        int result = avformat_open_input(&m_format, nullptr, nullptr, &options);
        av_dict_free(&options);

        if (result < 0)
        {
            // avformat_open_input frees the context it was given when it fails.
            if (!m_abort)
            {
                LOGGER_WARNING(L"avformat_open_input failed: {}", result);
            }

            return false;
        }

        result = avformat_find_stream_info(m_format, nullptr);
        if (result < 0 && !m_abort)
        {
            LOGGER_WARNING(L"avformat_find_stream_info failed: {}", result);
        }

        if (m_abort)
        {
            return false;
        }

        m_startTime = m_format->start_time != AV_NOPTS_VALUE ? static_cast<double>(m_format->start_time) / AV_TIME_BASE : 0;

        auto const& engineOptions = m_engine->m_options;

        if (engineOptions.video && m_renderer)
        {
            const int index = av_find_best_stream(m_format, AVMEDIA_TYPE_VIDEO, -1, -1, nullptr, 0);

            // Cover art in an audio file is a picture, not a video.
            if (index >= 0 && (m_format->streams[index]->disposition & AV_DISPOSITION_ATTACHED_PIC) == 0)
            {
                OpenVideo(index);
            }
        }

        if (engineOptions.audio)
        {
            const int index = av_find_best_stream(m_format, AVMEDIA_TYPE_AUDIO, -1, m_videoIndex, nullptr, 0);

            if (index >= 0)
            {
                OpenAudio(index);
            }
        }

        if (m_videoIndex < 0 && m_audioIndex < 0)
        {
            LOGGER_WARNING(L"Nothing playable in {} stream(s)", m_format->nb_streams);
            return false;
        }

        for (unsigned index = 0; index < m_format->nb_streams; index++)
        {
            if (static_cast<int>(index) != m_videoIndex && static_cast<int>(index) != m_audioIndex)
            {
                m_format->streams[index]->discard = AVDISCARD_ALL;
            }
        }

        if (m_format->duration != AV_NOPTS_VALUE && m_format->duration > 0)
        {
            m_duration = static_cast<double>(m_format->duration) / AV_TIME_BASE;
        }

        m_engine->ReportDuration(m_id, m_duration.load());

        if (m_videoIndex >= 0)
        {
            auto const* parameters = m_format->streams[m_videoIndex]->codecpar;

            double width = parameters->width;
            double height = parameters->height;

            if (m_videoFormat.aspect.num > 0 && m_videoFormat.aspect.den > 0)
            {
                width = width * m_videoFormat.aspect.num / m_videoFormat.aspect.den;
            }

            if (m_videoFormat.rotation == 90 || m_videoFormat.rotation == 270)
            {
                std::swap(width, height);
            }

            m_engine->ReportStream(m_id, MediaStreamKind::Video, m_videoIndex, static_cast<int32_t>(std::lround(width)), static_cast<int32_t>(std::lround(height)));
        }

        if (m_audioIndex >= 0)
        {
            m_engine->ReportStream(m_id, MediaStreamKind::Audio, m_audioIndex, 0, 0);
        }

        if (m_videoIndex >= 0)
        {
            m_videoThread = std::thread([this] { VideoMain(); });
            m_presentThread = std::thread([this] { PresentMain(); });
        }

        if (m_audioIndex >= 0)
        {
            m_audioThread = std::thread([this] { AudioMain(); });
        }

        // Where the caller asked to start, unless a seek moved that while the file was opening.
        double target;
        int32_t serial;

        if (!TakeSeek(target, serial))
        {
            target = m_startPosition;
            serial = m_serial.load();
        }

        m_opened = true;
        Begin(serial, target, target > 0);

        return true;
    }

    bool MediaSession::OpenVideo(int index)
    {
        AVStream* stream = m_format->streams[index];
        const AVCodec* codec = nullptr;

        // The native AV1 decoder only decodes through a hardware accelerator, and none is built.
        if (stream->codecpar->codec_id == AV_CODEC_ID_AV1)
        {
            codec = avcodec_find_decoder_by_name("libdav1d");
        }

        if (codec == nullptr)
        {
            codec = avcodec_find_decoder(stream->codecpar->codec_id);
        }

        if (codec == nullptr)
        {
            LOGGER_WARNING(L"No decoder for video codec {}", static_cast<int>(stream->codecpar->codec_id));
            return false;
        }

        AVCodecContext* context = avcodec_alloc_context3(codec);
        if (context == nullptr)
        {
            return false;
        }

        avcodec_parameters_to_context(context, stream->codecpar);
        context->pkt_timebase = stream->time_base;
        context->thread_count = 0;
        context->thread_type = FF_THREAD_FRAME | FF_THREAD_SLICE;

        if (avcodec_open2(context, codec, nullptr) < 0)
        {
            LOGGER_WARNING(L"avcodec_open2 failed for video codec {}", static_cast<int>(stream->codecpar->codec_id));
            avcodec_free_context(&context);
            return false;
        }

        m_videoCodec = context;
        m_videoIndex = index;
        m_videoBase = stream->time_base;

        const AVRational rate = av_guess_frame_rate(m_format, stream, nullptr);
        if (rate.num > 0 && rate.den > 0)
        {
            m_frameDuration = av_q2d(av_inv_q(rate));
        }

        m_videoFormat.rotation = RotationOf(stream);
        m_videoFormat.aspect = av_guess_sample_aspect_ratio(m_format, stream, nullptr);

        return true;
    }

    bool MediaSession::OpenAudio(int index)
    {
        AVStream* stream = m_format->streams[index];
        const AVCodec* codec = avcodec_find_decoder(stream->codecpar->codec_id);

        if (codec == nullptr)
        {
            LOGGER_WARNING(L"No decoder for audio codec {}", static_cast<int>(stream->codecpar->codec_id));
            return false;
        }

        AVCodecContext* context = avcodec_alloc_context3(codec);
        if (context == nullptr)
        {
            return false;
        }

        avcodec_parameters_to_context(context, stream->codecpar);
        context->pkt_timebase = stream->time_base;

        if (avcodec_open2(context, codec, nullptr) < 0)
        {
            LOGGER_WARNING(L"avcodec_open2 failed for audio codec {}", static_cast<int>(stream->codecpar->codec_id));
            avcodec_free_context(&context);
            return false;
        }

        auto audio = std::make_unique<MediaAudioRenderer>(m_audioWaiter);

        if (!audio->Open(context->sample_rate, m_engine->m_rate.load(), m_engine->m_volume.load(), m_engine->m_mute.load()))
        {
            // No output device: the picture still plays, on its own clock.
            LOGGER_WARNING(L"No audio output, playing without audio");
            avcodec_free_context(&context);
            return false;
        }

        m_audio = std::move(audio);
        m_audioCodec = context;
        m_audioIndex = index;
        m_audioBase = stream->time_base;

        return true;
    }

    void MediaSession::Begin(int32_t serial, double target, bool seek)
    {
        if (seek)
        {
            const int64_t timestamp = static_cast<int64_t>((target + m_startTime) * AV_TIME_BASE);

            // A read the last seek interrupted leaves its error on the context.
            m_io->error = 0;
            m_io->eof_reached = 0;

            // Backward only: allowed to land after the target, a seek reads the whole file when the
            // target is past the last keyframe in the index. The decoders drop what comes before
            // the target, so landing early costs decoding time but never shows the wrong frame.
            const int result = avformat_seek_file(m_format, -1, INT64_MIN, timestamp, timestamp, 0);

            if (result < 0 && !m_abort)
            {
                LOGGER_WARNING(L"Seeking to {} failed: {}", target, result);
            }
        }

        m_demuxSerial = serial;
        m_demuxEnded = false;

        if (m_videoIndex >= 0)
        {
            m_videoPackets.Push({ MediaPacket::Kind::Flush, nullptr, serial, target });
        }

        if (m_audioIndex >= 0)
        {
            m_audioPackets.Push({ MediaPacket::Kind::Flush, nullptr, serial, target });
        }
    }

    bool MediaSession::TakeSeek(double& target, int32_t& serial)
    {
        std::lock_guard<std::mutex> lock(m_seekLock);

        if (!m_seekPending)
        {
            return false;
        }

        m_seekPending = false;
        target = m_seekTarget;
        serial = m_seekSerial;

        return true;
    }

    bool MediaSession::HasPendingSeek()
    {
        std::lock_guard<std::mutex> lock(m_seekLock);
        return m_seekPending;
    }

    void MediaSession::ReopenIfClosed()
    {
        std::lock_guard<std::mutex> lock(m_sourceLock);

        if (m_sourceClosed && !m_abort)
        {
            try
            {
                m_source->Open();
            }
            catch (...)
            {
                // The read that follows reports it.
            }

            m_sourceClosed = false;
        }
    }

    void MediaSession::ReadLoop()
    {
        AVPacket* packet = av_packet_alloc();
        int errors = 0;

        while (!m_abort && packet != nullptr)
        {
            double target;
            int32_t serial;

            if (TakeSeek(target, serial))
            {
                Begin(serial, target, true);
                errors = 0;
                continue;
            }

            if (m_demuxEnded)
            {
                m_demuxWaiter.Wait(0.25);
                continue;
            }

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

            const int result = av_read_frame(m_format, packet);

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

                // A damaged packet is skipped, as long as it is a packet and not the file ending.
                if (result != AVERROR_EOF && !avio_feof(m_io) && m_io->error == 0 && ++errors < kMaxReadErrors)
                {
                    continue;
                }

                if (result != AVERROR_EOF && !avio_feof(m_io))
                {
                    LOGGER_WARNING(L"av_read_frame failed: {}", result);
                }

                const int32_t current = m_demuxSerial.load();

                if (m_videoIndex >= 0)
                {
                    m_videoPackets.Push({ MediaPacket::Kind::Drain, nullptr, current });
                }

                if (m_audioIndex >= 0)
                {
                    m_audioPackets.Push({ MediaPacket::Kind::Drain, nullptr, current });
                }

                m_demuxEnded = true;
                UpdatePlayback();
                continue;
            }

            errors = 0;

            PacketQueue* queue = nullptr;
            AVRational base{ 0, 1 };

            if (packet->stream_index == m_videoIndex)
            {
                queue = &m_videoPackets;
                base = m_videoBase;
            }
            else if (packet->stream_index == m_audioIndex)
            {
                queue = &m_audioPackets;
                base = m_audioBase;
            }

            if (queue == nullptr)
            {
                av_packet_unref(packet);
                continue;
            }

            double end = ToSeconds(packet->pts != AV_NOPTS_VALUE ? packet->pts : packet->dts, base);

            if (!std::isnan(end))
            {
                end -= m_startTime;

                if (packet->duration > 0)
                {
                    end += ToSeconds(packet->duration, base);
                }
            }

            AVPacket* item = av_packet_alloc();
            if (item == nullptr)
            {
                av_packet_unref(packet);
                continue;
            }

            av_packet_move_ref(item, packet);
            queue->Push({ MediaPacket::Kind::Data, item, m_demuxSerial.load() }, end);

            if (m_stalledHint)
            {
                UpdatePlayback();
            }
        }

        av_packet_free(&packet);
    }

    bool MediaSession::QueuesFull()
    {
        if (m_videoPackets.Bytes() + m_audioPackets.Bytes() >= kMaxQueuedBytes)
        {
            return true;
        }

        const double now = CurrentTime();
        double ahead = std::numeric_limits<double>::infinity();

        if (m_videoIndex >= 0)
        {
            ahead = std::min(ahead, m_videoPackets.End() - now);
        }

        if (m_audioIndex >= 0)
        {
            ahead = std::min(ahead, m_audioPackets.End() - now);
        }

        return ahead >= kMaxAhead;
    }

    int MediaSession::ReadPacket(void* opaque, uint8_t* buffer, int size)
    {
        auto* self = static_cast<MediaSession*>(opaque);

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

            if (self->m_opened && self->HasPendingSeek())
            {
                self->m_reading = false;
                return AVERROR_EXIT;
            }

            int64_t read;

            try
            {
                read = self->m_source->Read(self->m_ioOffset, buffer, size);
            }
            catch (...)
            {
                read = IMediaByteSource::ReadFailed;
            }

            self->m_reading = false;

            if (read > 0)
            {
                self->m_ioOffset += read;
                return static_cast<int>(read);
            }

            if (read == 0)
            {
                return AVERROR_EOF;
            }

            if (self->m_abort || (self->m_opened && self->HasPendingSeek()))
            {
                return AVERROR_EXIT;
            }

            if (read != IMediaByteSource::ReadStalled)
            {
                return AVERROR(EIO);
            }

            // RemoteFileSource gives up after 30 seconds without a byte. The file can still come,
            // and the player already shows that it is waiting, so it waits on: a seek or a close is
            // what ends this.
        }
    }

    int64_t MediaSession::SeekPacket(void* opaque, int64_t offset, int whence)
    {
        auto* self = static_cast<MediaSession*>(opaque);
        whence &= ~AVSEEK_FORCE;

        if (whence == AVSEEK_SIZE)
        {
            return self->m_size > 0 ? self->m_size : AVERROR(ENOSYS);
        }

        int64_t position;

        switch (whence)
        {
        case SEEK_SET:
            position = offset;
            break;
        case SEEK_CUR:
            position = self->m_ioOffset + offset;
            break;
        case SEEK_END:
            if (self->m_size <= 0)
            {
                return AVERROR(ENOSYS);
            }

            position = self->m_size + offset;
            break;
        default:
            return AVERROR(EINVAL);
        }

        if (position < 0)
        {
            return AVERROR(EINVAL);
        }

        // Only remembered: nothing is read, or asked for, until a read needs it.
        self->m_ioOffset = position;
        return position;
    }

    AVFrame* MediaSession::Normalize(AVFrame* frame)
    {
        switch (frame->format)
        {
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

        int32_t serial = -1;
        double target = 0;
        double nextPts = 0;
        bool pushed = false;

        // The last picture before the target, shown if the target turns out to be past the end.
        VideoFrame last;

        auto receive = [&]()
            {
                while (!m_abort && avcodec_receive_frame(m_videoCodec, frame) >= 0)
                {
                    double pts = ToSeconds(frame->best_effort_timestamp, m_videoBase);
                    pts = std::isnan(pts) ? nextPts : pts - m_startTime;

                    const double duration = frame->duration > 0 ? ToSeconds(frame->duration, m_videoBase) : m_frameDuration;
                    nextPts = pts + duration;

                    if (pts + duration <= target + 0.001)
                    {
                        av_frame_free(&last.frame);
                        last.frame = av_frame_alloc();

                        if (last.frame != nullptr)
                        {
                            av_frame_move_ref(last.frame, frame);
                            last = { last.frame, pts, duration, serial };
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

                    if (!m_frames.Push({ output, pts, duration, serial }, m_serial))
                    {
                        break;
                    }

                    pushed = true;
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
                avcodec_flush_buffers(m_videoCodec);
                av_frame_free(&last.frame);

                serial = item.serial;
                target = item.target;
                nextPts = target;
                pushed = false;
                break;

            case MediaPacket::Kind::Drain:
                avcodec_send_packet(m_videoCodec, nullptr);
                receive();

                if (!pushed && last.frame != nullptr)
                {
                    AVFrame* output = Normalize(last.frame);
                    av_frame_free(&last.frame);

                    if (output != nullptr && m_frames.Push({ output, last.pts, last.duration, serial }, m_serial))
                    {
                        pushed = true;
                    }
                }

                m_frames.Push({ nullptr, nextPts, 0, serial }, m_serial);

                // A drained decoder takes nothing more until it is flushed, which a seek after
                // the end would otherwise find it still needing.
                avcodec_flush_buffers(m_videoCodec);
                break;

            case MediaPacket::Kind::Data:
            {
                m_videoCodec->skip_frame = m_videoBehind ? AVDISCARD_NONREF : AVDISCARD_DEFAULT;

                int result = avcodec_send_packet(m_videoCodec, item.packet);

                while (result == AVERROR(EAGAIN) && !m_abort)
                {
                    receive();
                    result = avcodec_send_packet(m_videoCodec, item.packet);
                }

                receive();
                av_packet_free(&item.packet);
                break;
            }
            }
        }

        av_frame_free(&last.frame);
        av_frame_free(&frame);
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
                m_renderer->Render(m_id, front->frame, m_videoFormat);
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

            VideoFrame* next = m_frames.Next();

            if (next != nullptr && next->frame != nullptr && next->serial == serial && next->pts <= now)
            {
                // Late, and the next one is already due.
                m_videoBehind = now - front->pts > kVideoBehind;
                m_frames.Pop();
                continue;
            }

            m_videoBehind = false;
            m_renderer->Render(m_id, front->frame, m_videoFormat);

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

        int32_t flushed = -1;
        double target = 0;
        double nextPts = 0;

        auto receive = [&]()
            {
                while (!m_abort && avcodec_receive_frame(m_audioCodec, frame) >= 0)
                {
                    double pts = ToSeconds(frame->best_effort_timestamp, m_audioBase);
                    pts = std::isnan(pts) ? nextPts : pts - m_startTime;

                    if (frame->sample_rate > 0)
                    {
                        nextPts = pts + static_cast<double>(frame->nb_samples) / frame->sample_rate;
                    }

                    m_audio->Enqueue(frame, pts, target);
                    av_frame_unref(frame);
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
            UpdatePlayback();

            if (m_videoIndex < 0 && m_running)
            {
                ReportPosition(CurrentTime());
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
                    avcodec_flush_buffers(m_audioCodec);
                    target = item.target;
                    nextPts = target;
                    break;

                case MediaPacket::Kind::Drain:
                    avcodec_send_packet(m_audioCodec, nullptr);
                    receive();
                    m_audio->EndOfStream();
                    avcodec_flush_buffers(m_audioCodec);
                    break;

                case MediaPacket::Kind::Data:
                {
                    int result = avcodec_send_packet(m_audioCodec, item.packet);

                    while (result == AVERROR(EAGAIN) && !m_abort)
                    {
                        receive();
                        result = avcodec_send_packet(m_audioCodec, item.packet);
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
    }

    double MediaSession::CurrentTime()
    {
        double time;

        if (m_audio && m_audioSerial.load() == m_serial.load() && m_audio->Clock(time))
        {
            m_clock.Set(time);
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
        const bool hasVideo = m_videoIndex >= 0;
        const bool hasAudio = m_audioIndex >= 0;
        const bool audioCurrent = hasAudio && m_audioSerial.load() == m_serial.load();

        const bool audioEnded = !hasAudio || m_audio->Failed() || (audioCurrent && m_audio->Finished());
        const bool videoEnded = !hasVideo || m_videoEnded;

        if (m_prerolling)
        {
            const bool videoReady = !hasVideo || m_videoShown || m_videoEnded;
            const bool audioReady = !hasAudio || audioEnded || (audioCurrent && m_audio->Buffered() >= kAudioPreroll);

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
                m_engine->ReportEnded(m_id, m_endPosition);
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

            if (hasAudio && !audioEnded)
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
            // decoder that is merely slow is not a reason to stop the clock.
            const bool videoStarved = hasVideo && !videoEnded
                && m_frames.Front() == nullptr && m_videoPackets.Empty()
                && now > m_videoEnd + m_frameDuration * 2;

            const bool audioStarved = audioCurrent && !audioEnded
                && m_audio->Starved() && m_audioPackets.Empty();

            if (videoStarved || audioStarved)
            {
                m_stalled = true;
                m_stalledHint = true;
                m_lastBuffering = 0;
                m_engine->ReportBuffering(m_id, 0);
            }
        }

        SetRunningLocked(!m_stalled && !paused);

        m_engine->SetState(m_id, paused ? MediaEngineState::Paused : MediaEngineState::Playing);
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

    void MediaSession::Seek(double target)
    {
        target = std::max(0.0, target);

        if (m_duration.load() > 0)
        {
            target = std::min(target, m_duration.load());
        }

        {
            std::lock_guard<std::mutex> lock(m_seekLock);

            m_seekPending = true;
            m_seekTarget = target;
            m_seekSerial = ++m_serial;
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

            SetRunningLocked(false);
            m_clock.Set(target);
        }

        m_videoPackets.Flush();
        m_audioPackets.Flush();
        m_frames.Wake();

        if (m_opened && m_reading)
        {
            std::lock_guard<std::mutex> lock(m_sourceLock);

            // The read in progress is for where playback was. Closing the source is the only way
            // to end it early, and the next read opens it again.
            if (!m_abort && !m_sourceClosed && m_reading)
            {
                m_sourceClosed = true;

                try
                {
                    m_source->Close();
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

        if (m_audio)
        {
            m_audio->Rate(rate);
        }

        m_presentWaiter.Signal();
    }

    void MediaSession::Volume(double volume, bool mute)
    {
        if (m_audio)
        {
            m_audio->Volume(volume, mute);
        }
    }

    void MediaSession::AudioDeviceChanged()
    {
        if (m_audio)
        {
            m_audio->RequestRebuild();
        }
    }

    void MediaSession::Resized()
    {
        m_presentWaiter.Signal();
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
            renderer->Attach([weak = std::weak_ptr<MediaEngine>(engine)]()
                {
                    if (auto strong = weak.lock())
                    {
                        strong->RendererResized();
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

        const uint64_t id = ++m_sessions;
        m_current = id;

        if (m_renderer)
        {
            m_renderer->Activate(id);
        }

        m_duration = 0;
        m_position = position;

        m_session = std::make_shared<MediaSession>(shared_from_this(), m_source, m_renderer, std::move(previous), position, id);
        m_session->Start();

        SetStateLocked(MediaEngineState::Opening);
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
        std::lock_guard<std::mutex> lock(m_lock);

        if (m_closed)
        {
            return;
        }

        m_source = std::move(source);
        m_paused = false;

        StartLocked(position);
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
            // Stopped, or never started: play the source again from the start.
            if (m_source)
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
            m_session = nullptr;
        }

        // An id no session has, so that what the one just stopped still reports goes nowhere.
        m_current = ++m_sessions;
        m_position = 0;

        SetStateLocked(MediaEngineState::Stopped);
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

        m_current = ++m_sessions;
        m_source = nullptr;

        if (m_renderer)
        {
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

    void MediaEngine::SetState(uint64_t session, MediaEngineState state)
    {
        if (IsCurrent(session) && m_state.exchange(state) != state)
        {
            m_host->StateChanged(state);
        }
    }

    void MediaEngine::ReportPosition(uint64_t session, double position)
    {
        if (IsCurrent(session))
        {
            m_position = position;
            m_host->PositionChanged(position);
        }
    }

    void MediaEngine::ReportDuration(uint64_t session, double duration)
    {
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
        if (IsCurrent(session))
        {
            m_host->Buffering(progress);
        }
    }

    void MediaEngine::ReportStream(uint64_t session, MediaStreamKind kind, int32_t id, int32_t width, int32_t height)
    {
        if (IsCurrent(session))
        {
            m_host->StreamSelected(kind, id, width, height);
        }
    }

    void MediaEngine::ReportVideoOut(uint64_t session)
    {
        if (IsCurrent(session))
        {
            m_host->VideoOut();
        }
    }

    void MediaEngine::ReportEnded(uint64_t session, double position)
    {
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
        if (IsCurrent(session))
        {
            if (m_state.exchange(MediaEngineState::Error) != MediaEngineState::Error)
            {
                m_host->StateChanged(MediaEngineState::Error);
            }

            m_host->EncounteredError();
        }
    }
}
