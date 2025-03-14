#include "pch.h"
#include "VideoAnimation.h"
#if __has_include("VideoAnimation.g.cpp")
#include "VideoAnimation.g.cpp"
#endif

#include <VideoAnimationStreamSource.h>

// divide by 255 and round to nearest
// apply a fast variant: (X+127)/255 = ((X+127)*257+257)>>16 = ((X+128)*257)>>16
#define FAST_DIV255(x) ((((x)+128) * 257) >> 16)

namespace winrt::Telegram::Native::implementation
{
    static int open_codec_context(int* stream_idx, AVCodecContext** dec_ctx, AVFormatContext* fmt_ctx, enum AVMediaType type)
    {
        int ret, stream_index;
        AVStream* st;
        const AVCodec* dec = NULL;
        AVDictionary* opts = NULL;

        ret = av_find_best_stream(fmt_ctx, type, -1, -1, NULL, 0);
        if (ret < 0)
        {
            //OutputDebugStringFormat(L"can't find %s stream in input file", av_get_media_type_string(type));
            return ret;
        }
        else
        {
            stream_index = ret;
            st = fmt_ctx->streams[stream_index];

            dec = avcodec_find_decoder(st->codecpar->codec_id);
            if (!dec)
            {
                //OutputDebugStringFormat(L"failed to find %s codec", av_get_media_type_string(type));
                return AVERROR(EINVAL);
            }

            *dec_ctx = avcodec_alloc_context3(dec);
            if (!*dec_ctx)
            {
                //OutputDebugStringFormat(L"Failed to allocate the %s codec context", av_get_media_type_string(type));
                return AVERROR(ENOMEM);
            }

            if ((ret = avcodec_parameters_to_context(*dec_ctx, st->codecpar)) < 0)
            {
                //OutputDebugStringFormat(L"Failed to copy %s codec parameters to decoder context", av_get_media_type_string(type));
                return ret;
            }

            av_dict_set(&opts, "refcounted_frames", "1", 0);
            if ((ret = avcodec_open2(*dec_ctx, dec, &opts)) < 0)
            {
                //OutputDebugStringFormat(L"Failed to open %s codec", av_get_media_type_string(type));
                return ret;
            }
            *stream_idx = stream_index;
        }

        return 0;
    }

    static int find_best_stream(int* stream_idx, AVFormatContext* fmt_ctx, enum AVMediaType type)
    {
        int ret, stream_index;
        AVStream* st;
        const AVCodec* dec = NULL;
        AVDictionary* opts = NULL;

        ret = av_find_best_stream(fmt_ctx, type, -1, -1, NULL, 0);
        if (ret < 0)
        {
            return ret;
        }
        else
        {
            stream_index = ret;
            st = fmt_ctx->streams[stream_index];

            dec = avcodec_find_decoder(st->codecpar->codec_id);
            if (!dec)
            {
                //OutputDebugStringFormat(L"failed to find %s codec", av_get_media_type_string(type));
                return AVERROR(EINVAL);
            }

            *stream_idx = stream_index;
        }

        return 0;
    }

    void VideoAnimation::requestFd(VideoAnimation* info)
    {
        info->fd = CreateFile2FromAppW(info->file.FilePath().data(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, OPEN_EXISTING, nullptr);
    }

    int VideoAnimation::readCallback(void* opaque, uint8_t* buf, int buf_size)
    {
        VideoAnimation* info = reinterpret_cast<VideoAnimation*>(opaque);
        if (!info->stopped)
        {
            if (auto stream = info->file.try_as<implementation::VideoAnimationStreamSource>())
            {
                ULONG bytesRead;
                stream->m_stream->Read(buf, buf_size, &bytesRead);
                return bytesRead == 0 ? AVERROR_EOF : bytesRead;
            }
            else
            {
                info->file.ReadCallback(buf_size);

                if (info->fd == INVALID_HANDLE_VALUE)
                {
                    requestFd(info);
                }

                DWORD bytesRead;
                DWORD moved = SetFilePointer(info->fd, info->file.Offset(), NULL, FILE_BEGIN);
                BOOL result = ReadFile(info->fd, buf, buf_size, &bytesRead, NULL);

                info->file.SeekCallback(bytesRead + info->file.Offset());
                return bytesRead == 0 ? AVERROR_EOF : bytesRead;
            }
        }
        return 0;
    }

    int64_t VideoAnimation::seekCallback(void* opaque, int64_t offset, int whence)
    {
        VideoAnimation* info = reinterpret_cast<VideoAnimation*>(opaque);
        if (!info->stopped)
        {

            if (whence & FFMPEG_AVSEEK_SIZE)
            {
                return info->file.FileSize();
            }
            else if (auto stream = info->file.try_as<implementation::VideoAnimationStreamSource>())
            {
                LARGE_INTEGER li;
                li.QuadPart = offset;

                stream->m_stream->Seek(li, STREAM_SEEK_SET, NULL);
                return offset;
            }
            else
            {
                info->file.SeekCallback(offset);
                return offset;
            }
        }
        return 0;
    }

    void VideoAnimation::RedirectLoggingOutputs(void* ptr, int level, const char* fmt, va_list vargs)
    {
        CHAR buffer[1024];
        vsprintf_s(buffer, 1024, fmt, vargs);
        OutputDebugStringA(buffer);
    }

    winrt::Telegram::Native::VideoAnimation VideoAnimation::LoadFromFile(IVideoAnimationSource file, bool preview, bool limitFps, bool probe)
    {
        auto info = winrt::make_self<VideoAnimation>();
        file.SeekCallback(0);

        int ret;
        info->file = file;
        info->fileEvent = CreateEvent(NULL, TRUE, TRUE, NULL);

        //av_log_set_level(AV_LOG_DEBUG);
        //av_log_set_callback(RedirectLoggingOutputs);

        info->ioBuffer = (unsigned char*)av_malloc(64 * 1024);
        info->ioContext = avio_alloc_context(info->ioBuffer, 64 * 1024, 0, (void*)info.get(), readCallback, nullptr, seekCallback);
        if (info->ioContext == nullptr)
        {
            //delete info;
            return nullptr;
        }

        info->fmt_ctx = avformat_alloc_context();
        info->fmt_ctx->pb = info->ioContext;

        AVDictionary* options = NULL;
        av_dict_set(&options, "usetoc", "1", 0);
        ret = avformat_open_input(&info->fmt_ctx, "http://localhost/file", NULL, &options);
        av_dict_free(&options);
        if (ret < 0)
        {
            //OutputDebugStringFormat(L"can't open source file %s, %s", info->src, av_err2str(ret));
            //delete info;
            return nullptr;
        }
        info->fmt_ctx->flags |= AVFMT_FLAG_FAST_SEEK;
        if (preview)
        {
            info->fmt_ctx->flags |= AVFMT_FLAG_NOBUFFER;
        }

        if ((ret = avformat_find_stream_info(info->fmt_ctx, NULL)) < 0)
        {
            //OutputDebugStringFormat(L"can't find stream information %s, %s", info->src, av_err2str(ret));
            //delete info;
            return nullptr;
        }

        if (open_codec_context(&info->video_stream_idx, &info->video_dec_ctx, info->fmt_ctx, AVMEDIA_TYPE_VIDEO) >= 0)
        {
            info->video_stream = info->fmt_ctx->streams[info->video_stream_idx];
        }

        find_best_stream(&info->audio_stream_idx, info->fmt_ctx, AVMEDIA_TYPE_AUDIO);

        if (!probe)
        {
            if (info->video_stream == nullptr)
            {
                //OutputDebugStringFormat(L"can't find video stream in the input, aborting %s", info->src);
                //delete info;
                return nullptr;
            }

            info->frame = av_frame_alloc();
            if (info->frame == nullptr)
            {
                //OutputDebugStringFormat(L"can't allocate frame %s", info->src);
                //delete info;
                return nullptr;
            }

            info->pkt = av_packet_alloc();
            if (info->pkt == nullptr)
            {
                //OutputDebugStringFormat(L"can't allocate packet %s", info->src);
                //delete info;
                return nullptr;
            }
        }

        if (info->video_dec_ctx != nullptr)
        {
            info->pixelWidth = info->video_dec_ctx->width;
            info->pixelHeight = info->video_dec_ctx->height;

            //float pixelWidthHeightRatio = info->video_dec_ctx->sample_aspect_ratio.num / info->video_dec_ctx->sample_aspect_ratio.den; TODO support
            AVDictionaryEntry* rotate_tag = av_dict_get(info->video_stream->metadata, "rotate", NULL, 0);
            if (rotate_tag && *rotate_tag->value && strcmp(rotate_tag->value, "0"))
            {
                char* tail;
                info->rotation = (int)av_strtod(rotate_tag->value, &tail);
                if (*tail)
                {
                    info->rotation = 0;
                }
            }
            else
            {
                info->rotation = 0;
            }

            if (info->video_stream->codecpar->codec_id == AV_CODEC_ID_H264)
            {
                info->framerate = av_q2d(info->video_stream->avg_frame_rate);
            }
            else
            {
                info->framerate = av_q2d(info->video_stream->r_frame_rate);
            }

            info->limitFps = limitFps && info->framerate > 30;
        }
        else
        {
            info->pixelWidth = 0;
            info->pixelHeight = 0;
        }

        AVDictionaryEntry* title_tag = av_dict_get(info->fmt_ctx->metadata, "title", NULL, 0);
        if (title_tag && title_tag->value)
        {
            info->title = winrt::to_hstring(title_tag->value);
        }

        AVDictionaryEntry* artist_tag = av_dict_get(info->fmt_ctx->metadata, "album_artist", NULL, 0);
        if (artist_tag && artist_tag->value)
        {
            info->artist = winrt::to_hstring(artist_tag->value);
        }
        else
        {
            artist_tag = av_dict_get(info->fmt_ctx->metadata, "artist", NULL, 0);
            if (artist_tag && artist_tag->value)
            {
                info->artist = winrt::to_hstring(artist_tag->value);
            }
        }

        for (int32_t i = 0, l = info->fmt_ctx->nb_streams; i < l; ++i)
        {
            const auto stream = info->fmt_ctx->streams[i];
            if (stream->disposition & AV_DISPOSITION_ATTACHED_PIC)
            {
                const auto& packet = stream->attached_pic;
                if (packet.size)
                {
                    info->album_stream_idx = i;
                }

                break;
            }
        }

        //int requestedMaxSide = 420;

        //double ratioX = (double)requestedMaxSide / info->video_dec_ctx->width;
        //double ratioY = (double)requestedMaxSide / info->video_dec_ctx->height;
        //double ratio = std::max(ratioX, ratioY);

        info->maxWidth = info->pixelWidth; // (int)(info->video_dec_ctx->width * ratio);
        info->maxHeight = info->pixelHeight; // (int)(info->video_dec_ctx->height * ratio);

        //OutputDebugStringFormat(L"successfully opened file %s", info->src);

        info->duration = (int32_t)(info->fmt_ctx->duration * 1000 / AV_TIME_BASE);
        //(int32_t) (1000 * info->video_stream->duration * av_q2d(info->video_stream->time_base));
        //env->ReleaseIntArrayElements(data, dataArr, 0);

        return info.as<winrt::Telegram::Native::VideoAnimation>();
    }

    void VideoAnimation::Stop()
    {
        stopped = true;
    }

    void VideoAnimation::PrepareToSeek()
    {
        seeking = true;
    }

    void VideoAnimation::SeekToMilliseconds(int64_t ms, bool precise)
    {
        seeking = false;
        int64_t pts = (int64_t)(ms / av_q2d(video_stream->time_base) / 1000);
        int ret = 0;
        if ((ret = av_seek_frame(fmt_ctx, video_stream_idx, pts, AVSEEK_FLAG_BACKWARD | AVSEEK_FLAG_FRAME)) < 0)
        {
            //OutputDebugStringFormat(L"can't seek file %s, %s", src, av_err2str(ret));
            return;
        }
        else
        {
            avcodec_flush_buffers(video_dec_ctx);

            // TODO: Not currently supported
            //if (!precise)
            //{
            //    return;
            //}
            //int got_frame = 0;
            //int32_t tries = 1000;
            //while (tries > 0)
            //{
            //    if (pkt->size == 0)
            //    {
            //        ret = av_read_frame(fmt_ctx, pkt);
            //        if (ret >= 0)
            //        {
            //            orig_pkt = pkt;
            //        }
            //    }

            //    if (pkt->size > 0)
            //    {
            //        ret = decode_packet(&got_frame);
            //        if (ret < 0)
            //        {
            //            if (has_decoded_frames)
            //            {
            //                ret = 0;
            //            }
            //            pkt->size = 0;
            //        }
            //        else
            //        {
            //            pkt->data += ret;
            //            pkt->size -= ret;
            //        }
            //        if (pkt->size == 0)
            //        {
            //            av_packet_unref(&orig_pkt);
            //        }
            //    }
            //    else
            //    {
            //        pkt->data = NULL;
            //        pkt->size = 0;
            //        ret = decode_packet(&got_frame);
            //        if (ret < 0)
            //        {
            //            return;
            //        }
            //        if (got_frame == 0)
            //        {
            //            av_seek_frame(fmt_ctx, video_stream_idx, 0, AVSEEK_FLAG_BACKWARD | AVSEEK_FLAG_FRAME);
            //            return;
            //        }
            //    }
            //    if (ret < 0)
            //    {
            //        return;
            //    }
            //    if (got_frame)
            //    {
            //        if (frame->format == AV_PIX_FMT_YUV420P || frame->format == AV_PIX_FMT_BGRA || frame->format == AV_PIX_FMT_YUVJ420P)
            //        {
            //            int64_t pkt_pts = frame->best_effort_timestamp;
            //            if (pkt_pts >= pts)
            //            {
            //                return;
            //            }
            //        }
            //        av_frame_unref(frame);
            //    }
            //    tries--;
            //}
        }
    }

    IRandomAccessStream VideoAnimation::GetAlbumCover()
    {
        if (album_stream_idx)
        {
            const auto album = fmt_ctx->streams[album_stream_idx];
            const auto& packet = album->attached_pic;

            HRESULT result;
            IRandomAccessStream randomAccessStream = InMemoryRandomAccessStream();

            winrt::com_ptr<IStream> stream;
            CleanupIfFailed(result, CreateStreamOverRandomAccessStream(winrt::get_unknown(randomAccessStream), IID_PPV_ARGS(&stream)));

            CleanupIfFailed(result, stream->Write(packet.data, packet.size, nullptr));
            CleanupIfFailed(result, stream->Seek({ 0 }, STREAM_SEEK_SET, nullptr));

            return randomAccessStream;
        }

    Cleanup:
        return nullptr;
    }

    int VideoAnimation::RenderSync(IBuffer bitmap, int32_t w, int32_t h, bool preview, int32_t& seconds)
    {
        uint8_t* pixels = bitmap.data();
        bool completed;
        auto result = RenderSync(pixels, w, h, preview, seconds, completed);

        return result;
    }

    int VideoAnimation::RenderSync(uint8_t* pixels, int32_t width, int32_t height, bool preview, int32_t& seconds, bool& completed)
    {
        slim_lock_guard const guard(m_lock);

        //int64_t time = ConnectionsManager::getInstance(0).getCurrentTimeMonotonicMillis();
        completed = false;

        int ret = 0;
        int32_t triesCount = preview ? 50 : 6;
        //has_decoded_frames = false;
        while (!stopped && triesCount != 0)
        {
            if (waiting == Waiting::ReadFrame)
            {
                ret = av_read_frame(fmt_ctx, pkt);
                if (ret >= 0)
                {
                    if (pkt->stream_index == video_stream_idx)
                    {
                        waiting = Waiting::SendPacket;
                    }
                    else
                    {
                        av_packet_unref(pkt);
                    }
                }
            }

            if (waiting == Waiting::SendPacket)
            {
                ret = avcodec_send_packet(video_dec_ctx, pkt);
                waiting = ret >= 0
                    ? Waiting::ReceiveFrame
                    : Waiting::ReadFrame;
            }

            if (waiting == Waiting::ReceiveFrame)
            {
                ret = avcodec_receive_frame(video_dec_ctx, frame);
                if (ret >= 0)
                {
                    double nextFrame = frame->best_effort_timestamp * av_q2d(video_stream->time_base);

                    if (nextFrame >= prevFrame + 1.0 / 30 || framerate < 60)
                    {
                        seconds = static_cast<int32_t>(nextFrame);
                        prevFrame = nextFrame;

                        decode_frame(pixels, width, height);
                        return 1;
                    }
                }
                else
                {
                    waiting = Waiting::ReadFrame;
                    av_packet_unref(pkt);
                }
            }

            if (ret == AVERROR_EOF && has_decoded_frames && !preview)
            {
                completed = true;
                prevFrame = -1;

                ret = av_seek_frame(fmt_ctx, video_stream_idx, 0, AVSEEK_FLAG_BACKWARD | AVSEEK_FLAG_FRAME);
                if (ret < 0)
                {
                    //OutputDebugStringFormat(L"can't seek to begin of file %s, %s", src, av_err2str(ret));
                    goto Cleanup;
                }

                avcodec_flush_buffers(video_dec_ctx);
            }
            else if (ret < 0 && ret != AVERROR(EAGAIN))
            {
                completed = true;
                prevFrame = -1;

                goto Cleanup;
            }

            if (!has_decoded_frames)
            {
                triesCount--;
            }
        }

    Cleanup:
        av_packet_unref(pkt);
        return 0;
    }

    inline bool is_aligned(const void* ptr, std::uintptr_t alignment) noexcept
    {
        auto iptr = reinterpret_cast<std::uintptr_t>(ptr);
        return !(iptr % alignment);
    }

    inline int32_t ffalign(int32_t x, int32_t a)
    {
        return (((x)+(a)-1) & ~((a)-1));
    }

    void VideoAnimation::decode_frame(uint8_t* pixels, int32_t width, int32_t height)
    {
        //OutputDebugStringFormat(L"decoded frame with w = %d, h = %d, format = %d", frame->width, frame->height, frame->format);
        if (frame->format == AV_PIX_FMT_YUV420P || frame->format == AV_PIX_FMT_YUVA420P || frame->format == AV_PIX_FMT_BGRA || frame->format == AV_PIX_FMT_YUVJ420P || frame->format == AV_PIX_FMT_YUV444P)
        {
            if (sws_ctx == nullptr && ((intptr_t)pixels) % 16 == 0)
            {
                if (frame->format > AV_PIX_FMT_NONE && frame->format < AV_PIX_FMT_NB)
                {
                    sws_ctx = sws_getContext(frame->width, frame->height, (AVPixelFormat)frame->format, width, height, AV_PIX_FMT_BGRA, SWS_BILINEAR, NULL, NULL, NULL);
                }
                else if (video_dec_ctx->pix_fmt > AV_PIX_FMT_NONE && video_dec_ctx->pix_fmt < AV_PIX_FMT_NB)
                {
                    sws_ctx = sws_getContext(video_dec_ctx->width, video_dec_ctx->height, video_dec_ctx->pix_fmt, width, height, AV_PIX_FMT_BGRA, SWS_BILINEAR, NULL, NULL, NULL);
                }
            }

            if (sws_ctx == nullptr)
            {
                if (frame->format == AV_PIX_FMT_YUVA420P)
                {
                    libyuv::I420AlphaToARGBMatrix(frame->data[0], frame->linesize[0], frame->data[1], frame->linesize[1], frame->data[2], frame->linesize[2], frame->data[3], frame->linesize[3], pixels, width * 4, &libyuv::kYvuI601Constants, width, height, 1);
                }
                else if (frame->format == AV_PIX_FMT_YUV444P)
                {
                    libyuv::H444ToABGR(frame->data[0], frame->linesize[0], frame->data[2], frame->linesize[2], frame->data[1], frame->linesize[1], pixels, width * 4, width, height);
                }
                else if (frame->format == AV_PIX_FMT_YUV420P || frame->format == AV_PIX_FMT_YUVJ420P)
                {
                    if (frame->colorspace == AVColorSpace::AVCOL_SPC_BT709)
                    {
                        libyuv::H420ToABGR(frame->data[0], frame->linesize[0], frame->data[2], frame->linesize[2], frame->data[1], frame->linesize[1], pixels, width * 4, width, height);
                    }
                    else
                    {
                        libyuv::I420ToABGR(frame->data[0], frame->linesize[0], frame->data[2], frame->linesize[2], frame->data[1], frame->linesize[1], pixels, width * 4, width, height);
                    }
                }
                else if (frame->format == AV_PIX_FMT_RGBA)
                {
                    libyuv::ARGBToABGR(frame->data[0], frame->linesize[0], pixels, width * 4, width, height);
                }
            }
            else
            {
                // All of this could be avoided by simply allocating a buffer of the size of frame+AV_INPUT_BUFFER_PADDING_SIZE
                // Videos coming from CachedVideoAnimation will always use the fast path as width is aligned.

                auto dstWidth = FFALIGN(width, 16);
                auto dstDiff = dstWidth - width;

                auto srcWidth = frame->linesize[0] - width;
                auto srcDiff = FFALIGN(srcWidth, 12) - srcWidth;

                auto padding = srcDiff > 0 && dstDiff > 0
                    ? std::min(srcDiff, dstDiff)
                    : std::max(srcDiff, dstDiff);

                padding = std::min(padding, width % 16);

                int32_t linesize = width * 4;

                if (padding == 0 || srcWidth % 30 == 0)
                {
                    sws_scale(sws_ctx, frame->data, frame->linesize, 0, frame->height, &pixels, &linesize);
                }
                else
                {
                    if (dst_data == nullptr)
                    {
                        int32_t paddedsize = std::max(width + padding, 16) * height * 4;
                        dst_data = (uint8_t*)malloc(paddedsize);
                    }

                    sws_scale(sws_ctx, frame->data, frame->linesize, 0, frame->height, &dst_data, &linesize);
                    memcpy(pixels, dst_data, linesize * height);
                }

                if (frame->format == AV_PIX_FMT_YUVA420P)
                {
                    for (int i = 0; i < width * height * 4; i += 4)
                    {
                        auto alpha = pixels[i + 3];
                        pixels[i + 0] = FAST_DIV255(pixels[i + 0] * alpha);
                        pixels[i + 1] = FAST_DIV255(pixels[i + 1] * alpha);
                        pixels[i + 2] = FAST_DIV255(pixels[i + 2] * alpha);
                    }
                }
            }

            has_decoded_frames = true;
            av_packet_unref(pkt);
        }
    }
} // namespace winrt::Telegram::Native::implementation
