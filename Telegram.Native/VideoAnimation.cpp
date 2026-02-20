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
                int64_t offset = info->file.Offset();
                int64_t bytesRead;
                info->file.ReadCallback(buf_size, 0, bytesRead);

                if (info->fd == INVALID_HANDLE_VALUE)
                {
                    info->fd = CreateFile2FromAppW(info->file.FilePath().data(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, OPEN_EXISTING, nullptr);
                
                    LARGE_INTEGER distancetoMove{};
                    distancetoMove.QuadPart = offset;

                    BOOL moved = SetFilePointerEx(info->fd, distancetoMove, NULL, FILE_BEGIN);
                    if (!moved)
                    {
                        return 0;
                    }
                }

                if (info->fd != INVALID_HANDLE_VALUE && bytesRead >= 0)
                {
                    DWORD read;
                    if (ReadFile(info->fd, buf, buf_size > bytesRead ? bytesRead : buf_size, &read, NULL))
                    {
                        info->file.SeekCallback(offset + read);
                        return read > 0 ? read : AVERROR_EOF;
                    }
                }

                return AVERROR_EOF;
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

                if (info->fd != INVALID_HANDLE_VALUE)
                {
                    LARGE_INTEGER distancetoMove{};
                    distancetoMove.QuadPart = offset;

                    BOOL moved = SetFilePointerEx(info->fd, distancetoMove, NULL, FILE_BEGIN);
                    return moved ? offset : 0;
                }

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

    static int get_stream_rotation(const AVStream* stream)
    {
        AVDictionaryEntry* e = av_dict_get(stream->metadata, "rotate", NULL, 0);
        if (e && e->value)
        {
            if (!strcmp(e->value, "90") || !strcmp(e->value, "-270"))
            {
                return 90;
            }
            else if (!strcmp(e->value, "270") || !strcmp(e->value, "-90"))
            {
                return 270;
            }
            else if (!strcmp(e->value, "180") || !strcmp(e->value, "-180"))
            {
                return 180;
            }
            else if (!strcmp(e->value, "0"))
            {
                return 0;
            }
        }

        const AVPacketSideData* displaymatrix = av_packet_side_data_get(
            stream->codecpar->coded_side_data, stream->codecpar->nb_coded_side_data, AV_PKT_DATA_DISPLAYMATRIX);
        if (displaymatrix)
        {
            return ((int)-av_display_rotation_get((int32_t*)displaymatrix->data) + 360) % 360;
        }

        return 0;
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
            info->rotation = get_stream_rotation(info->video_stream);

            auto guess = av_guess_frame_rate(info->fmt_ctx, info->video_stream, NULL);
            auto framerate = av_q2d(guess);

            info->dropper = FrameDropper(framerate, limitFps ? 30.0 : 60.0);
            info->framerate = info->dropper.frame_rate();
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
        slim_lock_guard const guard(m_lock);

        if (!fmt_ctx || !video_stream || video_stream_idx < 0)
        {
            return;
        }

        seeking = true;

        // Convert milliseconds to timestamp in stream time base
        int64_t target_ts = av_rescale_q(ms, { 1, 1000 }, video_stream->time_base);

        // Seek to keyframe before or at target timestamp
        int ret = avformat_seek_file(fmt_ctx, video_stream_idx, INT64_MIN, target_ts, target_ts, 0);
        if (ret < 0)
        {
            // Fallback to av_seek_frame if avformat_seek_file fails
            ret = av_seek_frame(fmt_ctx, video_stream_idx, target_ts, AVSEEK_FLAG_BACKWARD);
            if (ret < 0)
            {
                seeking = false;
                return;
            }
        }

        // Flush decoder buffers after seek
        avcodec_flush_buffers(video_dec_ctx);

        seeking = false;

        if (!precise)
        {
            return; // Fast seek - just go to nearest keyframe
        }

        // Precise seek - decode frames until we reach the target timestamp
        AVPacket* pkt = av_packet_alloc();
        if (!pkt)
        {
            return;
        }

        AVFrame* temp_frame = av_frame_alloc();
        if (!temp_frame)
        {
            av_packet_free(&pkt);
            return;
        }

        int max_tries = 1000;
        bool found_target = false;

        while (max_tries > 0 && !found_target)
        {
            ret = av_read_frame(fmt_ctx, pkt);
            if (ret < 0)
            {
                if (ret == AVERROR_EOF)
                {
                    break; // End of file
                }
                continue;
            }

            // Only process packets from our video stream
            if (pkt->stream_index != video_stream_idx)
            {
                av_packet_unref(pkt);
                continue;
            }

            // Send packet to decoder
            ret = avcodec_send_packet(video_dec_ctx, pkt);
            av_packet_unref(pkt);

            if (ret < 0 && ret != AVERROR(EAGAIN))
            {
                break;
            }

            // Receive decoded frames
            while (ret >= 0)
            {
                ret = avcodec_receive_frame(video_dec_ctx, temp_frame);
                if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF)
                {
                    break;
                }
                if (ret < 0)
                {
                    break;
                }

                // Check if this frame is at or past our target timestamp
                int64_t frame_ts = temp_frame->best_effort_timestamp;
                if (frame_ts != AV_NOPTS_VALUE)
                {
                    if (frame_ts >= target_ts)
                    {
                        // Copy this frame to your main frame buffer
                        av_frame_unref(frame); // Assuming 'frame' is your main frame
                        av_frame_ref(frame, temp_frame);
                        found_target = true;
                        break;
                    }
                }

                av_frame_unref(temp_frame);
            }

            max_tries--;
        }

        // Cleanup
        av_frame_free(&temp_frame);
        av_packet_free(&pkt);

        // If we couldn't find the exact frame, seek back to the beginning
        if (!found_target)
        {
            avformat_seek_file(fmt_ctx, video_stream_idx, 0, 0, 0, 0);
            avcodec_flush_buffers(video_dec_ctx);
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

    int VideoAnimation::RenderSync(IBuffer bitmap, int32_t w, int32_t h, bool preview, double& seconds)
    {
        uint8_t* pixels = bitmap.data();
        bool completed;
        auto result = RenderSync(pixels, w, h, preview, seconds, completed);

        return result;
    }

    inline double clamp(double value, double min, double max)
    {
        if (value > max)
        {
            return max;
        }
        else if (value < min)
        {
            return min;
        }

        return value;
    }

    int VideoAnimation::RenderSync(uint8_t* pixels, int32_t width, int32_t height, bool preview, double& seconds, bool& completed)
    {
        slim_lock_guard const guard(m_lock);
        completed = false;
        int ret = 0;
        int32_t triesCount = preview ? 50 : 6;

        if (!fmt_ctx || !video_dec_ctx || !pkt || !frame)
        {
            return 0;
        }

        while (!stopped && triesCount > 0)
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
                        continue; // Skip non-video packets immediately
                    }
                }
                else if (ret == AVERROR_EOF)
                {
                    // Handle EOF - send flush packet
                    if (has_decoded_frames && !preview)
                    {
                        completed = true;

                        // Seek back to beginning for loop playback
                        ret = avformat_seek_file(fmt_ctx, video_stream_idx, 0, 0, 0, 0);
                        if (ret < 0)
                        {
                            // Fallback to av_seek_frame
                            ret = av_seek_frame(fmt_ctx, video_stream_idx, 0, AVSEEK_FLAG_BACKWARD);
                        }
                        if (ret < 0)
                        {
                            goto Cleanup;
                        }
                        avcodec_flush_buffers(video_dec_ctx);
                        waiting = Waiting::ReadFrame;
                        continue;
                    }
                    else
                    {
                        // Send NULL packet to flush decoder
                        ret = avcodec_send_packet(video_dec_ctx, nullptr);
                        if (ret >= 0)
                        {
                            waiting = Waiting::ReceiveFrame;
                        }
                        else
                        {
                            goto Cleanup;
                        }
                    }
                }
                else
                {
                    // Other errors
                    completed = true;
                    goto Cleanup;
                }
            }

            if (waiting == Waiting::SendPacket)
            {
                ret = avcodec_send_packet(video_dec_ctx, pkt);
                if (ret >= 0)
                {
                    waiting = Waiting::ReceiveFrame;
                    av_packet_unref(pkt); // Unref after successful send
                }
                else if (ret == AVERROR(EAGAIN))
                {
                    // Decoder needs more frames to be received first
                    waiting = Waiting::ReceiveFrame;
                }
                else
                {
                    // Error sending packet
                    av_packet_unref(pkt);
                    waiting = Waiting::ReadFrame;
                    continue;
                }
            }

            if (waiting == Waiting::ReceiveFrame)
            {
                ret = avcodec_receive_frame(video_dec_ctx, frame);
                if (ret >= 0)
                {
                    has_decoded_frames = true;

                    // Calculate frame timestamp
                    int64_t pts = frame->best_effort_timestamp;
                    if (pts == AV_NOPTS_VALUE)
                    {
                        pts = frame->pts;
                    }

                    if (pts != AV_NOPTS_VALUE)
                    {
                        if (dropper.should_display_frame())
                        {
                            double nextFrame = pts * av_q2d(video_stream->time_base);
                            seconds = clamp(nextFrame, 0.0, duration);

                            // Decode and render the frame
                            int decode_result = decode_frame(pixels, width, height);
                            if (decode_result >= 0)
                            {
                                return 1; // Successfully rendered frame
                            }
                        }
                    }

                    av_frame_unref(frame);
                    waiting = Waiting::ReadFrame;

                }
                else if (ret == AVERROR(EAGAIN))
                {
                    // Need to send more packets
                    waiting = Waiting::ReadFrame;

                }
                else if (ret == AVERROR_EOF)
                {
                    // Decoder is fully drained
                    if (has_decoded_frames && !preview)
                    {
                        completed = true;

                        // Reset for loop playback
                        ret = avformat_seek_file(fmt_ctx, video_stream_idx, 0, 0, 0, 0);
                        if (ret < 0)
                        {
                            ret = av_seek_frame(fmt_ctx, video_stream_idx, 0, AVSEEK_FLAG_BACKWARD);
                        }
                        if (ret < 0)
                        {
                            goto Cleanup;
                        }
                        avcodec_flush_buffers(video_dec_ctx);
                        waiting = Waiting::ReadFrame;
                        has_decoded_frames = false;
                        continue;
                    }
                    else
                    {
                        completed = true;
                        goto Cleanup;
                    }

                }
                else
                {
                    // Other receive errors
                    waiting = Waiting::ReadFrame;
                    continue;
                }
            }

            // Decrement tries only if we haven't decoded any frames yet
            if (!has_decoded_frames)
            {
                triesCount--;
            }
        }

    Cleanup:
        // Ensure packet is unreferenced
        if (pkt)
        {
            av_packet_unref(pkt);
        }

        // If we stopped due to tries exhaustion without decoding, mark as completed
        if (!has_decoded_frames && triesCount <= 0)
        {
            completed = true;
        }

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

    int VideoAnimation::decode_frame(uint8_t* pixels, int32_t width, int32_t height)
    {
        if (!frame || !pixels || width <= 0 || height <= 0)
        {
            return -1;
        }

        // Check if frame has valid format
        if (frame->format == AV_PIX_FMT_NONE || frame->width <= 0 || frame->height <= 0)
        {
            return -1;
        }

        // Supported pixel formats
        bool supported_format = (frame->format == AV_PIX_FMT_YUV420P ||
            frame->format == AV_PIX_FMT_YUVA420P ||
            frame->format == AV_PIX_FMT_BGRA ||
            frame->format == AV_PIX_FMT_RGBA ||
            frame->format == AV_PIX_FMT_YUVJ420P ||
            frame->format == AV_PIX_FMT_YUV444P);

        if (!supported_format)
        {
            return -1;
        }

        // Initialize SWS context if needed (and pixels are aligned)
        if (sws_ctx == nullptr && ((intptr_t)pixels) % 16 == 0)
        {
            AVPixelFormat src_format = (AVPixelFormat)frame->format;

            // Validate pixel format range
            if (src_format > AV_PIX_FMT_NONE && src_format < AV_PIX_FMT_NB)
            {
                sws_ctx = sws_getContext(
                    frame->width, frame->height, src_format,
                    width, height, AV_PIX_FMT_BGRA,
                    SWS_BILINEAR, nullptr, nullptr, nullptr
                );
            }
            // Fallback to decoder context format
            else if (video_dec_ctx &&
                video_dec_ctx->pix_fmt > AV_PIX_FMT_NONE &&
                video_dec_ctx->pix_fmt < AV_PIX_FMT_NB)
            {
                sws_ctx = sws_getContext(
                    video_dec_ctx->width, video_dec_ctx->height, video_dec_ctx->pix_fmt,
                    width, height, AV_PIX_FMT_BGRA,
                    SWS_BILINEAR, nullptr, nullptr, nullptr
                );
            }
        }

        // Fast path: Use libyuv for direct conversion (no SWS context)
        if (sws_ctx == nullptr)
        {
            switch (frame->format)
            {
            case AV_PIX_FMT_YUVA420P: {
                // Check alpha plane exists
                if (frame->data[3])
                {
                    // Convert to ARGB first, then swap to BGRA
                    libyuv::I420AlphaToARGBMatrix(
                        frame->data[0], frame->linesize[0],
                        frame->data[1], frame->linesize[1],
                        frame->data[2], frame->linesize[2],
                        frame->data[3], frame->linesize[3],
                        pixels, width * 4,
                        &libyuv::kYvuI601Constants,
                        width, height, 1
                    );
                    // Convert ARGB to BGRA in-place
                    libyuv::ARGBToBGRA(pixels, width * 4, pixels, width * 4, width, height);
                }
                else
                {
                    return -1; // Invalid YUVA format without alpha
                }
                break;
            }

            case AV_PIX_FMT_YUV444P: {
                // Convert to ABGR first, then swap to BGRA
                libyuv::H444ToARGB(
                    frame->data[0], frame->linesize[0],
                    frame->data[2], frame->linesize[2],
                    frame->data[1], frame->linesize[1],
                    pixels, width * 4, width, height
                );
                // Convert ABGR to BGRA (swap A and R channels)
                libyuv::ARGBToBGRA(pixels, width * 4, pixels, width * 4, width, height);
                break;
            }

            case AV_PIX_FMT_YUV420P:
            case AV_PIX_FMT_YUVJ420P: {
                // Convert to I420/H420 -> ABGR first, then to BGRA
                if (frame->colorspace == AVCOL_SPC_BT709)
                {
                    libyuv::H420ToARGB(
                        frame->data[0], frame->linesize[0],
                        frame->data[2], frame->linesize[2],
                        frame->data[1], frame->linesize[1],
                        pixels, width * 4, width, height
                    );

                    // Convert ABGR to BGRA (swap A and R channels)
                    libyuv::ARGBToBGRA(pixels, width * 4, pixels, width * 4, width, height);
                }
                else
                {
                    libyuv::I420ToBGRA(
                        frame->data[0], frame->linesize[0],
                        frame->data[2], frame->linesize[2],
                        frame->data[1], frame->linesize[1],
                        pixels, width * 4, width, height
                    );
                }
                break;
            }

            case AV_PIX_FMT_RGBA:
                // Convert RGBA to BGRA (swap R and B channels)
                //libyuv::RGBAToBGRA(frame->data[0], frame->linesize[0], pixels, width * 4, width, height);
                //break;
                return -1;

            case AV_PIX_FMT_BGRA:
                // Direct copy - already in BGRA format
                if (frame->width == width && frame->height == height &&
                    frame->linesize[0] == width * 4)
                {
                    memcpy(pixels, frame->data[0], width * height * 4);
                }
                else
                {
                    // Line-by-line copy for different strides
                    for (int y = 0; y < std::min(height, frame->height); y++)
                    {
                        memcpy(pixels + y * width * 4,
                            frame->data[0] + y * frame->linesize[0],
                            std::min(width, frame->width) * 4);
                    }
                }
                break;

            default:
                return -1; // Unsupported format
            }
        }
        // SWS scaling path
        else
        {
            // Calculate alignment and padding for better performance
            auto dstWidth = FFALIGN(width, 16);
            auto dstDiff = dstWidth - width;
            auto srcWidth = frame->linesize[0] - width;
            auto srcDiff = FFALIGN(srcWidth, 12) - srcWidth;

            auto padding = (srcDiff > 0 && dstDiff > 0)
                ? std::min(srcDiff, dstDiff)
                : std::max(srcDiff, dstDiff);
            padding = std::min(padding, width % 16);

            int32_t linesize = width * 4;

            // Check if destination linesize is aligned to 64 bytes
            bool dstAligned = (linesize % 64 == 0);

            // Direct scaling if no padding issues
            if (dstAligned && (padding == 0 || srcWidth % 30 == 0))
            {
                uint8_t* dst_planes[4] = { pixels, nullptr, nullptr, nullptr };
                int dst_linesize[4] = { linesize, 0, 0, 0 };

                int result = sws_scale(sws_ctx,
                    (const uint8_t* const*)frame->data,
                    frame->linesize,
                    0, frame->height,
                    dst_planes, dst_linesize);
                if (result < 0)
                {
                    return -1;
                }
            }
            // Use intermediate buffer for alignment issues
            else
            {
                if (dst_data == nullptr)
                {
                    int32_t paddedsize = std::max(width + padding, 16) * height * 4;
                    dst_data = (uint8_t*)av_malloc(paddedsize);
                    if (!dst_data)
                    {
                        return -1;
                    }
                }

                uint8_t* dst_planes[4] = { dst_data, nullptr, nullptr, nullptr };
                int dst_linesize[4] = { linesize, 0, 0, 0 };

                int result = sws_scale(sws_ctx,
                    (const uint8_t* const*)frame->data,
                    frame->linesize,
                    0, frame->height,
                    dst_planes, dst_linesize);
                if (result < 0)
                {
                    return -1;
                }

                memcpy(pixels, dst_data, linesize * height);
            }

            // Post-process alpha for YUVA420P when using SWS
            if (frame->format == AV_PIX_FMT_YUVA420P)
            {
                for (int i = 0; i < width * height; i++)
                {
                    uint8_t* pixel = &pixels[i * 4];
                    uint8_t alpha = pixel[3];
                    if (alpha < 255)
                    {
                        pixel[0] = FAST_DIV255(pixel[0] * alpha);
                        pixel[1] = FAST_DIV255(pixel[1] * alpha);
                        pixel[2] = FAST_DIV255(pixel[2] * alpha);
                    }
                }
            }
        }

        has_decoded_frames = true;
        return 0; // Success
    }
} // namespace winrt::Telegram::Native::implementation
