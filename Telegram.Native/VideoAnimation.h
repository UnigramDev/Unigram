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
#include <libavutil/eval.h>
#include <libswscale/swscale.h>
#include <libavutil/imgutils.h>
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

    struct VideoAnimation : VideoAnimationT<VideoAnimation>
    {
    public:
        virtual ~VideoAnimation()
        {
            Close();
        }

        void Close()
        {
            if (video_dec_ctx)
            {
                avcodec_close(video_dec_ctx);
                video_dec_ctx = nullptr;
            }
            if (fmt_ctx)
            {
                avformat_close_input(&fmt_ctx);
                fmt_ctx = nullptr;
            }
            if (frame)
            {
                av_frame_free(&frame);
                frame = nullptr;
            }
            if (ioContext != nullptr)
            {
                if (ioContext->buffer)
                {
                    av_freep(&ioContext->buffer);
                }
                avio_context_free(&ioContext);
                ioContext = nullptr;
            }
            if (sws_ctx != nullptr)
            {
                sws_freeContext(sws_ctx);
                sws_ctx = nullptr;
            }
            if (fd != INVALID_HANDLE_VALUE)
            {
                CloseHandle(fd);
                fd = INVALID_HANDLE_VALUE;
                //close(fd);
                //fd = -1;
            }

            av_packet_unref(&orig_pkt);

            video_stream_idx = -1;
            video_stream = nullptr;
            audio_stream = nullptr;
        }

        static winrt::Telegram::Native::VideoAnimation LoadFromFile(IVideoAnimationSource file, bool preview, bool limitFps);

        VideoAnimation() = default;

        void Stop();
        void PrepareToSeek();
        void SeekToMilliseconds(int64_t ms, bool precise);

        int RenderSync(IBuffer buffer, int32_t width, int32_t height, bool preview, int32_t& seconds);
        int RenderSync(uint8_t* pixels, int32_t width, int32_t height, bool preview, int32_t& seconds, bool& completed);

        int PixelWidth()
        {
            return pixelWidth;
        }

        int PixelHeight()
        {
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

    private:
        int decode_packet(VideoAnimation* info, int* got_frame);
        static void requestFd(VideoAnimation* info);
        static int readCallback(void* opaque, uint8_t* buf, int buf_size);
        static int64_t seekCallback(void* opaque, int64_t offset, int whence);

        static void RedirectLoggingOutputs(void* ptr, int level, const char* fmt, va_list vargs);


        winrt::slim_mutex m_lock;

        AVFormatContext* fmt_ctx = nullptr;
        IVideoAnimationSource file{ nullptr };
        HANDLE fileEvent = INVALID_HANDLE_VALUE;
        int video_stream_idx = -1;
        AVStream* video_stream = nullptr;
        AVStream* audio_stream = nullptr;
        AVCodecContext* video_dec_ctx = nullptr;
        AVFrame* frame = nullptr;
        bool has_decoded_frames = false;
        AVPacket pkt;
        AVPacket orig_pkt;
        bool stopped = false;
        bool seeking = false;

        uint8_t* dst_data[1];
        int32_t dst_linesize[1];

        struct SwsContext* sws_ctx = nullptr;

        AVIOContext* ioContext = nullptr;
        unsigned char* ioBuffer = nullptr;
        HANDLE fd = INVALID_HANDLE_VALUE;
        //int64_t last_seek_p = 0;

        bool limitFps;
        const int64_t limitedDuration = 1000 / 30;

        int64_t prevFrame = 0;
        int64_t prevDuration = 0;

        int64_t nextFrame = 0;

        int32_t pixelWidth = 0;
        int32_t pixelHeight = 0;

        int32_t rotation = 0;
        int32_t duration = 0;
        double framerate = 0;
    };
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
    struct VideoAnimation : VideoAnimationT<VideoAnimation, implementation::VideoAnimation>
    {
    };
} // namespace winrt::Telegram::Native::factory_implementation
