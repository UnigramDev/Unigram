#pragma once

#include <cstdint>
#include <limits>
#include <string>
#include <fcntl.h>
#include <libyuv.h>

#include <Microsoft.Graphics.Canvas.h>
#include <Microsoft.Graphics.Canvas.native.h>

extern "C"
{
#include <libavformat/avformat.h>
#include <libavutil/eval.h>
#include <libswscale/swscale.h>
}

static const std::string av_make_error_str(int errnum) {
    char errbuf[AV_ERROR_MAX_STRING_SIZE];
    av_strerror(errnum, errbuf, AV_ERROR_MAX_STRING_SIZE);
    return (std::string) errbuf;
}

#undef av_err2str
#define av_err2str(errnum) av_make_error_str(errnum).c_str()
#define FFMPEG_AVSEEK_SIZE 0x10000
        
using namespace Platform;
using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::WRL;

namespace Unigram
{
    namespace Native
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

        public ref class VideoAnimation sealed
        {
        public:
            virtual ~VideoAnimation() {
                if (video_dec_ctx) {
                    avcodec_close(video_dec_ctx);
                    video_dec_ctx = nullptr;
                }
                if (fmt_ctx) {
                    avformat_close_input(&fmt_ctx);
                    fmt_ctx = nullptr;
                }
                if (frame) {
                    av_frame_free(&frame);
                    frame = nullptr;
                }
                //if (src) {
                //    delete[] src;
                //    src = nullptr;
                //}
                if (ioContext != nullptr) {
                    if (ioContext->buffer) {
                        av_freep(&ioContext->buffer);
                    }
                    avio_context_free(&ioContext);
                    ioContext = nullptr;
                }
                if (sws_ctx != nullptr) {
                    sws_freeContext(sws_ctx);
                    sws_ctx = nullptr;
                }
                if (fd != INVALID_HANDLE_VALUE) {
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

            static VideoAnimation^ LoadFromFile(String^ filePath, bool preview, bool limitFps);

            void Stop();
            void PrepareToSeek();
            void SeekToMilliseconds(int64_t ms, bool precise);

            int RenderSync(CanvasBitmap^ bitmap, bool preview);

            property int PixelWidth
            {
                int get() { return pixelWidth; }
            }

            property int PixelHeight
            {
                int get() { return pixelHeight; }
            }

        internal:
            VideoAnimation();

            //int open_codec_context(int* stream_idx, AVCodecContext** dec_ctx, AVFormatContext* fmt_ctx, enum AVMediaType type);
            int decode_packet(VideoAnimation^ info, int* got_frame);
            //void requestFd(Class1^ info);
            //int readCallback(void* opaque, uint8_t* buf, int buf_size);
            //int64_t seekCallback(void* opaque, int64_t offset, int whence);



            AVFormatContext* fmt_ctx = nullptr;
            const wchar_t* src = nullptr;
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
            int64_t file_size = 0;
            int64_t last_seek_p = 0;

            bool limitFps;
            const int64_t limitedDuration = 1000 / 30;

            int64_t prevFrame;
            int64_t prevDuration;

            int64_t nextFrame;

            int32_t pixelWidth;
            int32_t pixelHeight;

            int32_t rotation;
            int32_t duration;
            int32_t framerate;
        };
    }
}