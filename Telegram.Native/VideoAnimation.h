#pragma once

#include <VideoAnimation.g.h>

#include <cstdint>
#include <limits>
#include <string>
#include <fcntl.h>
#include <libyuv.h>

extern "C"
{
#include <libavformat/avformat.h>
#include <libavutil/display.h>
#include <libavutil/eval.h>
#include <libswscale/swscale.h>
#include <libavutil/imgutils.h>
#include <libavcodec/avcodec.h>
}

static const std::string av_make_error_str(int errnum)
{
    char errbuf[AV_ERROR_MAX_STRING_SIZE];
    av_strerror(errnum, errbuf, AV_ERROR_MAX_STRING_SIZE);
    return (std::string)errbuf;
}

#undef av_err2str
#define av_err2str(errnum) av_make_error_str(errnum).c_str()
#define FFMPEG_AVSEEK_SIZE 0x10000

using namespace winrt::Windows::Storage::Streams;

namespace winrt::Telegram::Native::implementation
{
    enum PARAM_NUM
    {
        PARAM_NUM_SUPPORTED_VIDEO_CODEC = 0,
        PARAM_NUM_WIDTH = 1,
        PARAM_NUM_HEIGHT = 2,
        PARAM_NUM_BITRATE = 3,
        PARAM_NUM_DURATION = 4,
        PARAM_NUM_AUDIO_FRAME_SIZE = 5,
        PARAM_NUM_VIDEO_FRAME_SIZE = 6,
        PARAM_NUM_FRAMERATE = 7,
        PARAM_NUM_ROTATION = 8,
        PARAM_NUM_SUPPORTED_AUDIO_CODEC = 9,
        PARAM_NUM_HAS_AUDIO = 10,
        PARAM_NUM_COUNT = 11,
    };

    class FrameDropper
    {
    private:
        double source_fps;
        double target_fps;
        double effective_fps;
        int preferred_divisor;
        bool use_clean_division;
        int64_t frame_count = 0;
        int64_t frames_displayed = 0;

    public:
        FrameDropper(double src_fps, double tgt_fps = 30.0, double tolerance = 0.9)
            : source_fps(src_fps)
            , target_fps(tgt_fps)
        {
            if (source_fps <= target_fps)
            {
                preferred_divisor = 1;
                effective_fps = source_fps;
                use_clean_division = true;
                return;
            }

            preferred_divisor = (int)std::round(source_fps / target_fps);
            effective_fps = source_fps / preferred_divisor;

            if (effective_fps < target_fps * tolerance)
            {
                use_clean_division = false;
            }
            else
            {
                use_clean_division = true;
            }
        }

        double frame_rate()
        {
            return effective_fps;
        }

        bool should_display_frame()
        {
            if (source_fps <= target_fps)
            {
                return true;
            }

            frame_count++;

            if (use_clean_division)
            {
                return (frame_count % preferred_divisor) == 0;
            }
            else
            {
                int64_t expected = (frame_count * target_fps) / source_fps;
                if (frames_displayed < expected)
                {
                    frames_displayed++;
                    return true;
                }
                return false;
            }
        }
    };

    struct VideoAnimation : VideoAnimationT<VideoAnimation>
    {
    public:
        virtual ~VideoAnimation()
        {
            Close();
        }

        void Close()
        {
            if (closed)
            {
                return;
            }

            closed = true;

            if (has_decoded_frames && video_dec_ctx && frame)
            {
                int ret = avcodec_send_packet(video_dec_ctx, NULL);
                if (ret >= 0 || ret == AVERROR_EOF)
                {
                    while (true)
                    {
                        ret = avcodec_receive_frame(video_dec_ctx, frame);
                        if (ret < 0)
                            break;
                        av_frame_unref(frame);
                    }
                }
            }

            // Free in correct order
            if (frame)
            {
                av_frame_free(&frame);
                frame = nullptr;
            }

            if (pkt)
            {
                av_packet_free(&pkt);
                pkt = nullptr;
            }

            if (video_dec_ctx)
            {
                avcodec_free_context(&video_dec_ctx);
                video_dec_ctx = nullptr;
            }

            if (fmt_ctx)
            {
                avformat_close_input(&fmt_ctx);
                fmt_ctx = nullptr;
            }

            if (ioContext)
            {
                if (ioContext->buffer)
                {
                    av_freep(&ioContext->buffer);
                }
                avio_context_free(&ioContext);
                ioContext = nullptr;
            }

            if (sws_ctx)
            {
                sws_freeContext(sws_ctx);
                sws_ctx = nullptr;
            }

            if (dst_data)
            {
                av_free(dst_data);
                dst_data = nullptr;
            }

            if (fd != INVALID_HANDLE_VALUE)
            {
                CloseHandle(fd);
                fd = INVALID_HANDLE_VALUE;
            }

            video_stream_idx = -1;
            video_stream = nullptr;
            audio_stream = nullptr;
        }

        static winrt::Telegram::Native::VideoAnimation LoadFromFile(IVideoAnimationSource file, bool preview, bool limitFps, bool probe);

        VideoAnimation() = default;

        void Stop();
        void PrepareToSeek();
        void SeekToMilliseconds(int64_t ms, bool precise);

        IRandomAccessStream GetAlbumCover();

        int RenderSync(IBuffer buffer, int32_t width, int32_t height, bool preview, double& seconds);
        int RenderSync(uint8_t* pixels, int32_t width, int32_t height, bool preview, double& seconds, bool& completed);

        int PixelWidth()
        {
            return maxWidth;
            return pixelWidth;
        }

        int PixelHeight()
        {
            return maxHeight;
            return pixelHeight;
        }

        double FrameRate()
        {
            return framerate;
        }

        int Duration()
        {
            return duration;
        }

        int Rotation()
        {
            return rotation;
        }

        hstring Artist()
        {
            return artist;
        }

        hstring Title()
        {
            return title;
        }

        bool HasVideo()
        {
            return video_stream_idx != -1;
        }

        bool HasAudio()
        {
            return audio_stream_idx != -1;
        }

        bool HasAlbumCover()
        {
            return album_stream_idx != -1;
        }

    private:
        int decode_frame(uint8_t* pixels, int32_t width, int32_t height);
        static int readCallback(void* opaque, uint8_t* buf, int buf_size);
        static int64_t seekCallback(void* opaque, int64_t offset, int whence);

        static void RedirectLoggingOutputs(void* ptr, int level, const char* fmt, va_list vargs);

        winrt::slim_mutex m_lock;

        AVFormatContext* fmt_ctx = nullptr;
        IVideoAnimationSource file{ nullptr };
        int video_stream_idx = -1;
        int audio_stream_idx = -1;
        int album_stream_idx = -1;
        AVStream* video_stream = nullptr;
        AVStream* audio_stream = nullptr;
        AVCodecContext* video_dec_ctx = nullptr;
        AVFrame* frame = nullptr;
        bool has_decoded_frames = false;
        bool closed = false;
        AVPacket* pkt;
        //AVPacket orig_pkt;
        bool stopped = false;
        bool seeking = false;

        uint8_t* dst_data;

        struct SwsContext* sws_ctx = nullptr;

        AVIOContext* ioContext = nullptr;
        unsigned char* ioBuffer = nullptr;
        HANDLE fd = INVALID_HANDLE_VALUE;
        //int64_t last_seek_p = 0;

        FrameDropper dropper{ 0 };

        int32_t pixelWidth = 0;
        int32_t pixelHeight = 0;

        int32_t maxWidth = 0;
        int32_t maxHeight = 0;

        int32_t rotation = 0;
        int32_t duration = 0;
        double framerate = 0;

        hstring artist;
        hstring title;

        enum Waiting
        {
            ReadFrame = 0,
            SendPacket = 1,
            ReceiveFrame = 2
        };

        Waiting waiting = Waiting::ReadFrame;
    };
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
    struct VideoAnimation : VideoAnimationT<VideoAnimation, implementation::VideoAnimation>
    {
    };
} // namespace winrt::Telegram::Native::factory_implementation
