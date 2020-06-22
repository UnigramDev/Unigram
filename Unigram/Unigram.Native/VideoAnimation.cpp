#include "pch.h"
#include "VideoAnimation.h"

using namespace Unigram::Native;
using namespace Platform;

VideoAnimation::VideoAnimation()
{
}

static int open_codec_context(int* stream_idx, AVCodecContext** dec_ctx, AVFormatContext* fmt_ctx, enum AVMediaType type) {
    int ret, stream_index;
    AVStream* st;
    AVCodec* dec = NULL;
    AVDictionary* opts = NULL;

    ret = av_find_best_stream(fmt_ctx, type, -1, -1, NULL, 0);
    if (ret < 0) {
        //OutputDebugStringFormat(L"can't find %s stream in input file", av_get_media_type_string(type));
        return ret;
    }
    else {
        stream_index = ret;
        st = fmt_ctx->streams[stream_index];

        dec = avcodec_find_decoder(st->codecpar->codec_id);
        if (!dec) {
            //OutputDebugStringFormat(L"failed to find %s codec", av_get_media_type_string(type));
            return AVERROR(EINVAL);
        }

        *dec_ctx = avcodec_alloc_context3(dec);
        if (!*dec_ctx) {
            //OutputDebugStringFormat(L"Failed to allocate the %s codec context", av_get_media_type_string(type));
            return AVERROR(ENOMEM);
        }

        if ((ret = avcodec_parameters_to_context(*dec_ctx, st->codecpar)) < 0) {
            //OutputDebugStringFormat(L"Failed to copy %s codec parameters to decoder context", av_get_media_type_string(type));
            return ret;
        }

        av_dict_set(&opts, "refcounted_frames", "1", 0);
        if ((ret = avcodec_open2(*dec_ctx, dec, &opts)) < 0) {
            //OutputDebugStringFormat(L"Failed to open %s codec", av_get_media_type_string(type));
            return ret;
        }
        *stream_idx = stream_index;
    }

    return 0;
}

int VideoAnimation::decode_packet(VideoAnimation^ info, int* got_frame)
{
    int ret = 0;
    int decoded = info->pkt.size;
    *got_frame = 0;

    if (info->pkt.stream_index == info->video_stream_idx) {
#pragma warning(disable: 4996)
        ret = avcodec_decode_video2(info->video_dec_ctx, info->frame, got_frame, &info->pkt);
        if (ret != 0) {
            return ret;
        }
    }

    return decoded;
}

static void requestFd(VideoAnimation^ info)
{
    //JNIEnv* jniEnv = nullptr;

    //JavaVMAttachArgs jvmArgs;
    //jvmArgs.version = JNI_VERSION_1_6;

    //bool attached;
    //if (JNI_EDETACHED == javaVm->GetEnv((void**)&jniEnv, JNI_VERSION_1_6)) {
    //    javaVm->AttachCurrentThread(&jniEnv, &jvmArgs);
    //    attached = true;
    //}
    //else {
    //    attached = false;
    //}
    //jniEnv->CallIntMethod(info->stream, jclass_AnimatedFileDrawableStream_read, (jint)0, (jint)1);
    //if (attached) {
    //    javaVm->DetachCurrentThread();
    //}
    //info->fd = open(info->src, O_RDONLY, S_IRUSR);
    info->fd = CreateFile2(info->src, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, OPEN_EXISTING, nullptr);
}

static int readCallback(void* opaque, uint8_t* buf, int buf_size)
{
    VideoAnimation^ info = reinterpret_cast<VideoAnimation^>(opaque);
    if (!info->stopped) {
        if (info->fd == INVALID_HANDLE_VALUE) {
            requestFd(info);
        }
        if (info->fd != INVALID_HANDLE_VALUE) {
            if (info->last_seek_p + buf_size > info->file_size) {
                buf_size = (int)(info->file_size - info->last_seek_p);
            }
            if (buf_size > 0) {
                DWORD bytesRead;
                BOOL result = ReadFile(info->fd, buf, buf_size, &bytesRead, NULL);
                info->last_seek_p += buf_size;

                return (int)bytesRead;
            }
            //    JNIEnv* jniEnv = nullptr;

            //    JavaVMAttachArgs jvmArgs;
            //    jvmArgs.version = JNI_VERSION_1_6;

            //    bool attached;
            //    if (JNI_EDETACHED == javaVm->GetEnv((void**)&jniEnv, JNI_VERSION_1_6)) {
            //        javaVm->AttachCurrentThread(&jniEnv, &jvmArgs);
            //        attached = true;
            //    }
            //    else {
            //        attached = false;
            //    }

            //    buf_size = jniEnv->CallIntMethod(info->stream, jclass_AnimatedFileDrawableStream_read, (int)info->last_seek_p, (int)buf_size);
            //    info->last_seek_p += buf_size;
            //    if (attached) {
            //        javaVm->DetachCurrentThread();
            //    }
            //    return (int)read(info->fd, buf, (size_t)buf_size);
            //}
        }
    }
    return 0;
}

static int64_t seekCallback(void* opaque, int64_t offset, int whence)
{
    VideoAnimation^ info = reinterpret_cast<VideoAnimation^>(opaque);
    if (!info->stopped) {
        if (info->fd < 0) {
            requestFd(info);
        }
        if (info->fd >= 0) {
            if (whence & FFMPEG_AVSEEK_SIZE) {
                return info->file_size;
            }
            else {
                info->last_seek_p = offset;
                //lseek(info->fd, off_t(offset), SEEK_SET);
                SetFilePointer(info->fd, offset, NULL, FILE_BEGIN);
                return offset;
            }
        }
    }
    return 0;
}

VideoAnimation^ VideoAnimation::LoadFromFile(String^ filePath, bool preview, bool limitFps)
{
    VideoAnimation^ info = ref new VideoAnimation();

    int ret;
    info->src = filePath->Data();
    info->fd = CreateFile2(info->src, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, OPEN_EXISTING, nullptr);
    info->limitFps = limitFps;

    LARGE_INTEGER size;
    GetFileSizeEx(info->fd, &size);

    info->file_size = size.QuadPart;
    info->ioBuffer = (unsigned char*)av_malloc(64 * 1024);
    info->ioContext = avio_alloc_context(info->ioBuffer, 64 * 1024, 0, (void*)info, readCallback, nullptr, seekCallback);
    if (info->ioContext == nullptr) {
        delete info;
        return nullptr;
    }

    info->fmt_ctx = avformat_alloc_context();
    info->fmt_ctx->pb = info->ioContext;

    AVDictionary* options = NULL;
    av_dict_set(&options, "usetoc", "1", 0);
    ret = avformat_open_input(&info->fmt_ctx, "http://localhost/file", NULL, &options);
    av_dict_free(&options);
    if (ret < 0) {
        //OutputDebugStringFormat(L"can't open source file %s, %s", info->src, av_err2str(ret));
        delete info;
        return nullptr;
    }
    info->fmt_ctx->flags |= AVFMT_FLAG_FAST_SEEK;
    if (preview) {
        info->fmt_ctx->flags |= AVFMT_FLAG_NOBUFFER;
    }

    if ((ret = avformat_find_stream_info(info->fmt_ctx, NULL)) < 0) {
        //OutputDebugStringFormat(L"can't find stream information %s, %s", info->src, av_err2str(ret));
        delete info;
        return nullptr;
    }

    if (open_codec_context(&info->video_stream_idx, &info->video_dec_ctx, info->fmt_ctx, AVMEDIA_TYPE_VIDEO) >= 0) {
        info->video_stream = info->fmt_ctx->streams[info->video_stream_idx];
    }

    if (info->video_stream == nullptr) {
        //OutputDebugStringFormat(L"can't find video stream in the input, aborting %s", info->src);
        delete info;
        return nullptr;
    }

    info->frame = av_frame_alloc();
    if (info->frame == nullptr) {
        //OutputDebugStringFormat(L"can't allocate frame %s", info->src);
        delete info;
        return nullptr;
    }

    av_init_packet(&info->pkt);
    info->pkt.data = NULL;
    info->pkt.size = 0;

    info->pixelWidth = info->video_dec_ctx->width;
    info->pixelHeight = info->video_dec_ctx->height;

    //float pixelWidthHeightRatio = info->video_dec_ctx->sample_aspect_ratio.num / info->video_dec_ctx->sample_aspect_ratio.den; TODO support
    AVDictionaryEntry* rotate_tag = av_dict_get(info->video_stream->metadata, "rotate", NULL, 0);
    if (rotate_tag && *rotate_tag->value && strcmp(rotate_tag->value, "0")) {
        char* tail;
        info->rotation = (int)av_strtod(rotate_tag->value, &tail);
        if (*tail) {
            info->rotation = 0;
        }
    }
    else {
        info->rotation = 0;
    }
    info->duration = (int32_t)(info->fmt_ctx->duration * 1000 / AV_TIME_BASE);
    //(int32_t) (1000 * info->video_stream->duration * av_q2d(info->video_stream->time_base));
    //env->ReleaseIntArrayElements(data, dataArr, 0);

    if (info->video_stream->codecpar->codec_id == AV_CODEC_ID_H264) {
        info->framerate = (int)av_q2d(info->video_stream->avg_frame_rate);
    }
    else {
        info->framerate = (int)av_q2d(info->video_stream->r_frame_rate);
    }

    //OutputDebugStringFormat(L"successfully opened file %s", info->src);

    return info;
}

void VideoAnimation::Stop()
{
    VideoAnimation^ info = this;
    info->stopped = true;
}

void VideoAnimation::PrepareToSeek()
{
    VideoAnimation^ info = this;
    info->seeking = true;
}

void VideoAnimation::SeekToMilliseconds(int64_t ms, bool precise)
{
    VideoAnimation^ info = this;
    info->seeking = false;
    int64_t pts = (int64_t)(ms / av_q2d(info->video_stream->time_base) / 1000);
    int ret = 0;
    if ((ret = av_seek_frame(info->fmt_ctx, info->video_stream_idx, pts, AVSEEK_FLAG_BACKWARD | AVSEEK_FLAG_FRAME)) < 0) {
        //OutputDebugStringFormat(L"can't seek file %s, %s", info->src, av_err2str(ret));
        return;
    }
    else {
        avcodec_flush_buffers(info->video_dec_ctx);
        if (!precise) {
            return;
        }
        int got_frame = 0;
        int32_t tries = 1000;
        while (tries > 0) {
            if (info->pkt.size == 0) {
                ret = av_read_frame(info->fmt_ctx, &info->pkt);
                if (ret >= 0) {
                    info->orig_pkt = info->pkt;
                }
            }

            if (info->pkt.size > 0) {
                ret = decode_packet(info, &got_frame);
                if (ret < 0) {
                    if (info->has_decoded_frames) {
                        ret = 0;
                    }
                    info->pkt.size = 0;
                }
                else {
                    info->pkt.data += ret;
                    info->pkt.size -= ret;
                }
                if (info->pkt.size == 0) {
                    av_packet_unref(&info->orig_pkt);
                }
            }
            else {
                info->pkt.data = NULL;
                info->pkt.size = 0;
                ret = decode_packet(info, &got_frame);
                if (ret < 0) {
                    return;
                }
                if (got_frame == 0) {
                    av_seek_frame(info->fmt_ctx, info->video_stream_idx, 0, AVSEEK_FLAG_BACKWARD | AVSEEK_FLAG_FRAME);
                    return;
                }
            }
            if (ret < 0) {
                return;
            }
            if (got_frame) {
                if (info->frame->format == AV_PIX_FMT_YUV420P || info->frame->format == AV_PIX_FMT_BGRA || info->frame->format == AV_PIX_FMT_YUVJ420P) {
                    int64_t pkt_pts = info->frame->best_effort_timestamp;
                    if (pkt_pts >= pts) {
                        return;
                    }
                }
                av_frame_unref(info->frame);
            }
            tries--;
        }
    }
}

int VideoAnimation::RenderSync(CanvasBitmap^ bitmap, bool preview)
{
    //int64_t time = ConnectionsManager::getInstance(0).getCurrentTimeMonotonicMillis();
    VideoAnimation^ info = this;

    if (info->limitFps && info->nextFrame && info->nextFrame < info->prevFrame + info->prevDuration + info->limitedDuration) {
        info->nextFrame += info->limitedDuration;
        return 0;
    }

    int ret = 0;
    int got_frame = 0;
    int32_t triesCount = preview ? 50 : 6;
    //info->has_decoded_frames = false;
    while (!info->stopped && triesCount != 0) {
        if (info->pkt.size == 0) {
            ret = av_read_frame(info->fmt_ctx, &info->pkt);
            //OutputDebugStringFormat(L"got packet with size %d", info->pkt.size);
            if (ret >= 0) {
                info->orig_pkt = info->pkt;
            }
        }

        if (info->pkt.size > 0) {
            ret = decode_packet(info, &got_frame);
            if (ret < 0) {
                if (info->has_decoded_frames) {
                    ret = 0;
                }
                info->pkt.size = 0;
            }
            else {
                //OutputDebugStringFormat(L"read size %d from packet", ret);
                info->pkt.data += ret;
                info->pkt.size -= ret;
            }

            if (info->pkt.size == 0) {
                av_packet_unref(&info->orig_pkt);
            }
        }
        else {
            info->pkt.data = NULL;
            info->pkt.size = 0;
            ret = decode_packet(info, &got_frame);
            if (ret < 0) {
                //OutputDebugStringFormat(L"can't decode packet flushed %s", info->src);
                return 0;
            }
            if (!preview && got_frame == 0) {
                if (info->has_decoded_frames) {
                    info->nextFrame = 0;
                    if ((ret = av_seek_frame(info->fmt_ctx, info->video_stream_idx, 0, AVSEEK_FLAG_BACKWARD | AVSEEK_FLAG_FRAME)) < 0) {
                        //OutputDebugStringFormat(L"can't seek to begin of file %s, %s", info->src, av_err2str(ret));
                        return 0;
                    }
                    else {
                        avcodec_flush_buffers(info->video_dec_ctx);
                    }
                }
            }
        }
        if (ret < 0 || info->seeking) {
            return 0;
        }
        if (got_frame) {
            auto timestamp = (1000 * info->frame->best_effort_timestamp * av_q2d(info->video_stream->time_base));

            if (info->limitFps && timestamp < info->nextFrame) {
                info->has_decoded_frames = true;
                av_frame_unref(info->frame);

                continue;
            }

            //OutputDebugStringFormat(L"decoded frame with w = %d, h = %d, format = %d", info->frame->width, info->frame->height, info->frame->format);
            if (info->frame->format == AV_PIX_FMT_YUV420P || info->frame->format == AV_PIX_FMT_BGRA || info->frame->format == AV_PIX_FMT_YUVJ420P) {
                //jint* dataArr = env->GetIntArrayElements(data, 0);

                //void* pixels;
                void* pixels = malloc(pixelWidth * pixelHeight * 4);
                //if (AndroidBitmap_lockPixels(env, bitmap, &pixels) >= 0)
                {
                    if (info->sws_ctx == nullptr) {
                        if (info->frame->format > AV_PIX_FMT_NONE && info->frame->format < AV_PIX_FMT_NB) {
                            info->sws_ctx = sws_getContext(info->frame->width, info->frame->height, (AVPixelFormat)info->frame->format, info->frame->width, info->frame->height, AV_PIX_FMT_RGBA, SWS_BILINEAR, NULL, NULL, NULL);
                        }
                        else if (info->video_dec_ctx->pix_fmt > AV_PIX_FMT_NONE && info->video_dec_ctx->pix_fmt < AV_PIX_FMT_NB) {
                            info->sws_ctx = sws_getContext(info->video_dec_ctx->width, info->video_dec_ctx->height, info->video_dec_ctx->pix_fmt, info->video_dec_ctx->width, info->video_dec_ctx->height, AV_PIX_FMT_RGBA, SWS_BILINEAR, NULL, NULL, NULL);
                        }
                    }
                    if (info->sws_ctx == nullptr || ((intptr_t)pixels) % 16 != 0) {
                        if (info->frame->format == AV_PIX_FMT_YUV420P || info->frame->format == AV_PIX_FMT_YUVJ420P) {
                            if (info->frame->colorspace == AVColorSpace::AVCOL_SPC_BT709) {
                                libyuv::H420ToARGB(info->frame->data[0], info->frame->linesize[0], info->frame->data[2], info->frame->linesize[2], info->frame->data[1], info->frame->linesize[1], (uint8_t*)pixels, info->frame->width * 4, info->frame->width, info->frame->height);
                            }
                            else {
                                libyuv::I420ToARGB(info->frame->data[0], info->frame->linesize[0], info->frame->data[2], info->frame->linesize[2], info->frame->data[1], info->frame->linesize[1], (uint8_t*)pixels, info->frame->width * 4, info->frame->width, info->frame->height);
                            }
                        }
                        else if (info->frame->format == AV_PIX_FMT_BGRA) {
                            libyuv::ABGRToARGB(info->frame->data[0], info->frame->linesize[0], (uint8_t*)pixels, info->frame->width * 4, info->frame->width, info->frame->height);
                        }
                    }
                    else {
                        info->dst_data[0] = (uint8_t*)pixels;
                        info->dst_linesize[0] = pixelWidth * 4;
                        sws_scale(info->sws_ctx, info->frame->data, info->frame->linesize, 0, info->frame->height, info->dst_data, info->dst_linesize);
                    }

                    //libyuv::ARGBToABGR((uint8_t*)pixels, stride, (uint8_t*)pixels, stride, wantedWidth, wantedHeight);

                    //AndroidBitmap_unlockPixels(env, bitmap);
                    ComPtr<ABI::Microsoft::Graphics::Canvas::ICanvasBitmap> bitmapAbi;
                    auto unknown = reinterpret_cast<IUnknown*>(bitmap);
                    unknown->QueryInterface(IID_PPV_ARGS(&bitmapAbi));

                    bitmapAbi->SetPixelBytes(pixelWidth * pixelHeight * 4, (BYTE*)pixels);
                    free(pixels);

                    info->prevFrame = timestamp;
                    info->prevDuration = (1000 * info->frame->pkt_duration * av_q2d(info->video_stream->time_base));

                    info->nextFrame = timestamp + info->limitedDuration;
                }
            }
            info->has_decoded_frames = true;
            av_frame_unref(info->frame);

            return 1;
        }
        if (!info->has_decoded_frames) {
            triesCount--;
        }
    }
    return 0;
}

//jint videoOnJNILoad(JavaVM* vm, JNIEnv* env) {
//    jclass_AnimatedFileDrawableStream = (jclass)env->NewGlobalRef(env->FindClass("org/telegram/messenger/AnimatedFileDrawableStream"));
//    if (jclass_AnimatedFileDrawableStream == 0) {
//        return JNI_FALSE;
//    }
//    jclass_AnimatedFileDrawableStream_read = env->GetMethodID(jclass_AnimatedFileDrawableStream, "read", "(II)I");
//    if (jclass_AnimatedFileDrawableStream_read == 0) {
//        return JNI_FALSE;
//    }
//    jclass_AnimatedFileDrawableStream_cancel = env->GetMethodID(jclass_AnimatedFileDrawableStream, "cancel", "()V");
//    if (jclass_AnimatedFileDrawableStream_cancel == 0) {
//        return JNI_FALSE;
//    }
//
//    return JNI_TRUE;
//}