#include "pch.h"
#include "VideoAnimation.h"
#if __has_include("VideoAnimation.g.cpp")
#include "VideoAnimation.g.cpp"
#endif

#include <Microsoft.Graphics.Canvas.h>
#include <Microsoft.Graphics.Canvas.native.h>

// divide by 255 and round to nearest
// apply a fast variant: (X+127)/255 = ((X+127)*257+257)>>16 = ((X+128)*257)>>16
#define FAST_DIV255(x) ((((x)+128) * 257) >> 16)

namespace winrt::Unigram::Native::implementation
{
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

	int VideoAnimation::decode_packet(VideoAnimation* info, int* got_frame)
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

	void VideoAnimation::requestFd(VideoAnimation* info)
	{
		info->fd = CreateFile2FromAppW(info->file.FilePath().data(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, OPEN_EXISTING, nullptr);
	}

	int VideoAnimation::readCallback(void* opaque, uint8_t* buf, int buf_size)
	{
		VideoAnimation* info = reinterpret_cast<VideoAnimation*>(opaque);
		if (!info->stopped) {
			info->file.ReadCallback(buf_size);

			if (info->fd == INVALID_HANDLE_VALUE) {
				requestFd(info);
			}

			DWORD bytesRead;
			DWORD moved = SetFilePointer(info->fd, info->file.Offset(), NULL, FILE_BEGIN);
			BOOL result = ReadFile(info->fd, buf, buf_size, &bytesRead, NULL);

			info->file.SeekCallback(bytesRead + info->file.Offset());
			return bytesRead == 0 ? AVERROR_EOF : bytesRead;
		}
		return 0;
	}

	int64_t VideoAnimation::seekCallback(void* opaque, int64_t offset, int whence)
	{
		VideoAnimation* info = reinterpret_cast<VideoAnimation*>(opaque);
		if (!info->stopped) {
			if (whence & FFMPEG_AVSEEK_SIZE) {
				return info->file.FileSize();
			}
			else {
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

	winrt::Unigram::Native::VideoAnimation VideoAnimation::LoadFromFile(IVideoAnimationSource file, bool preview, bool limitFps)
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
		if (info->ioContext == nullptr) {
			//delete info;
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
			//delete info;
			return nullptr;
		}
		info->fmt_ctx->flags |= AVFMT_FLAG_FAST_SEEK;
		if (preview) {
			info->fmt_ctx->flags |= AVFMT_FLAG_NOBUFFER;
		}

		if ((ret = avformat_find_stream_info(info->fmt_ctx, NULL)) < 0) {
			//OutputDebugStringFormat(L"can't find stream information %s, %s", info->src, av_err2str(ret));
			//delete info;
			return nullptr;
		}

		if (open_codec_context(&info->video_stream_idx, &info->video_dec_ctx, info->fmt_ctx, AVMEDIA_TYPE_VIDEO) >= 0) {
			info->video_stream = info->fmt_ctx->streams[info->video_stream_idx];
		}

		if (info->video_stream == nullptr) {
			//OutputDebugStringFormat(L"can't find video stream in the input, aborting %s", info->src);
			//delete info;
			return nullptr;
		}

		info->frame = av_frame_alloc();
		if (info->frame == nullptr) {
			//OutputDebugStringFormat(L"can't allocate frame %s", info->src);
			//delete info;
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
			info->framerate = av_q2d(info->video_stream->avg_frame_rate);
		}
		else {
			info->framerate = av_q2d(info->video_stream->r_frame_rate);
		}

		//OutputDebugStringFormat(L"successfully opened file %s", info->src);

		info->limitFps = limitFps && info->framerate > 30;
		return info.as<winrt::Unigram::Native::VideoAnimation>();
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
		this->seeking = false;
		int64_t pts = (int64_t)(ms / av_q2d(this->video_stream->time_base) / 1000);
		int ret = 0;
		if ((ret = av_seek_frame(this->fmt_ctx, this->video_stream_idx, pts, AVSEEK_FLAG_BACKWARD | AVSEEK_FLAG_FRAME)) < 0) {
			//OutputDebugStringFormat(L"can't seek file %s, %s", this->src, av_err2str(ret));
			return;
		}
		else {
			avcodec_flush_buffers(this->video_dec_ctx);
			if (!precise) {
				return;
			}
			int got_frame = 0;
			int32_t tries = 1000;
			while (tries > 0) {
				if (this->pkt.size == 0) {
					ret = av_read_frame(this->fmt_ctx, &this->pkt);
					if (ret >= 0) {
						this->orig_pkt = this->pkt;
					}
				}

				if (this->pkt.size > 0) {
					ret = decode_packet(this, &got_frame);
					if (ret < 0) {
						if (this->has_decoded_frames) {
							ret = 0;
						}
						this->pkt.size = 0;
					}
					else {
						this->pkt.data += ret;
						this->pkt.size -= ret;
					}
					if (this->pkt.size == 0) {
						av_packet_unref(&this->orig_pkt);
					}
				}
				else {
					this->pkt.data = NULL;
					this->pkt.size = 0;
					ret = decode_packet(this, &got_frame);
					if (ret < 0) {
						return;
					}
					if (got_frame == 0) {
						av_seek_frame(this->fmt_ctx, this->video_stream_idx, 0, AVSEEK_FLAG_BACKWARD | AVSEEK_FLAG_FRAME);
						return;
					}
				}
				if (ret < 0) {
					return;
				}
				if (got_frame) {
					if (this->frame->format == AV_PIX_FMT_YUV420P || this->frame->format == AV_PIX_FMT_BGRA || this->frame->format == AV_PIX_FMT_YUVJ420P) {
						int64_t pkt_pts = this->frame->best_effort_timestamp;
						if (pkt_pts >= pts) {
							return;
						}
					}
					av_frame_unref(this->frame);
				}
				tries--;
			}
		}
	}

	int VideoAnimation::RenderSync(CanvasBitmap bitmap, bool preview, int32_t& seconds)
	{
		auto size = bitmap.SizeInPixels();
		auto w = size.Width;
		auto h = size.Height;

		uint8_t* pixels = new uint8_t[w * h * 4];
		bool completed;
		auto result = RenderSync(pixels, w, h, preview, seconds, completed);

		bitmap.SetPixelBytes(winrt::array_view(pixels, w * h * 4));
		delete[] pixels;

		return result;
	}

	int VideoAnimation::RenderSync(uint8_t* pixels, int32_t width, int32_t height, bool preview, int32_t& seconds, bool& completed)
	{
		//int64_t time = ConnectionsManager::getInstance(0).getCurrentTimeMonotonicMillis();
		completed = false;

		if (this->limitFps && this->nextFrame && this->nextFrame < this->prevFrame + this->prevDuration + this->limitedDuration) {
			this->nextFrame += this->limitedDuration;
			return 0;
		}

		int ret = 0;
		int got_frame = 0;
		int32_t triesCount = preview ? 50 : 6;
		//this->has_decoded_frames = false;
		while (!this->stopped && triesCount != 0) {
			if (this->pkt.size == 0) {
				ret = av_read_frame(this->fmt_ctx, &this->pkt);
				//OutputDebugStringFormat(L"got packet with size %d", this->pkt.size);
				if (ret >= 0) {
					this->orig_pkt = this->pkt;
				}
			}

			if (this->pkt.size > 0) {
				ret = decode_packet(this, &got_frame);
				if (ret < 0) {
					if (this->has_decoded_frames) {
						ret = 0;
					}
					this->pkt.size = 0;
				}
				else {
					//OutputDebugStringFormat(L"read size %d from packet", ret);
					this->pkt.data += ret;
					this->pkt.size -= ret;
				}

				if (this->pkt.size == 0) {
					av_packet_unref(&this->orig_pkt);
				}
			}
			else {
				this->pkt.data = NULL;
				this->pkt.size = 0;
				ret = decode_packet(this, &got_frame);
				if (ret < 0) {
					//OutputDebugStringFormat(L"can't decode packet flushed %s", this->src);
					return 0;
				}
				if (!preview && got_frame == 0) {
					if (this->has_decoded_frames) {
						this->nextFrame = 0;
						completed = true;
						if ((ret = av_seek_frame(this->fmt_ctx, this->video_stream_idx, 0, AVSEEK_FLAG_BACKWARD | AVSEEK_FLAG_FRAME)) < 0) {
							//OutputDebugStringFormat(L"can't seek to begin of file %s, %s", this->src, av_err2str(ret));
							return 0;
						}
						else {
							avcodec_flush_buffers(this->video_dec_ctx);
						}
					}
				}
			}
			if (ret < 0 || this->seeking) {
				return 0;
			}
			if (got_frame) {
				auto timestamp = (1000 * this->frame->best_effort_timestamp * av_q2d(this->video_stream->time_base));

				if (this->limitFps && timestamp < this->nextFrame) {
					this->has_decoded_frames = true;
					av_frame_unref(this->frame);

					continue;
				}

				//OutputDebugStringFormat(L"decoded frame with w = %d, h = %d, format = %d", this->frame->width, this->frame->height, this->frame->format);
				if (this->frame->format == AV_PIX_FMT_YUV420P || this->frame->format == AV_PIX_FMT_YUVA420P || this->frame->format == AV_PIX_FMT_BGRA || this->frame->format == AV_PIX_FMT_YUVJ420P) {
					//jint* dataArr = env->GetIntArrayElements(data, 0);

					//void* pixels;
					//if (pixels == nullptr) {
					//	pixels = new uint8_t[pixelWidth * pixelHeight * 4];
					//}

					if (this->sws_ctx == nullptr) {
						if (this->frame->format > AV_PIX_FMT_NONE && this->frame->format < AV_PIX_FMT_NB) {
							this->sws_ctx = sws_getContext(this->frame->width, this->frame->height, (AVPixelFormat)this->frame->format, width, height, AV_PIX_FMT_BGRA, SWS_BILINEAR, NULL, NULL, NULL);
						}
						else if (this->video_dec_ctx->pix_fmt > AV_PIX_FMT_NONE && this->video_dec_ctx->pix_fmt < AV_PIX_FMT_NB) {
							this->sws_ctx = sws_getContext(this->video_dec_ctx->width, this->video_dec_ctx->height, this->video_dec_ctx->pix_fmt, width, height, AV_PIX_FMT_BGRA, SWS_BILINEAR, NULL, NULL, NULL);
						}
					}
					if (this->sws_ctx == nullptr || ((intptr_t)pixels) % 16 != 0) {
						if (this->frame->format == AV_PIX_FMT_YUV420P || this->frame->format == AV_PIX_FMT_YUVA420P || this->frame->format == AV_PIX_FMT_YUVJ420P) {
							if (this->frame->colorspace == AVColorSpace::AVCOL_SPC_BT709) {
								libyuv::H420ToARGB(this->frame->data[0], this->frame->linesize[0], this->frame->data[2], this->frame->linesize[2], this->frame->data[1], this->frame->linesize[1], (uint8_t*)pixels, this->frame->width * 4, this->frame->width, this->frame->height);
							}
							else {
								libyuv::I420ToARGB(this->frame->data[0], this->frame->linesize[0], this->frame->data[2], this->frame->linesize[2], this->frame->data[1], this->frame->linesize[1], (uint8_t*)pixels, this->frame->width * 4, this->frame->width, this->frame->height);
							}
						}
						else if (this->frame->format == AV_PIX_FMT_BGRA) {
							libyuv::ABGRToARGB(this->frame->data[0], this->frame->linesize[0], (uint8_t*)pixels, this->frame->width * 4, this->frame->width, this->frame->height);
						}
					}
					else {
						//if (this->frame->format == AV_PIX_FMT_YUVA420P) {
						//	libyuv::I420AlphaToARGBMatrix(this->frame->data[0], this->frame->linesize[0], this->frame->data[2], this->frame->linesize[2], this->frame->data[1], this->frame->linesize[1], this->frame->data[3], this->frame->linesize[3], (uint8_t*)pixels, width * 4, &libyuv::kYvuI601Constants, width, height, 50);
						//}
						//else {
							this->dst_data[0] = (uint8_t*)pixels;
							this->dst_linesize[0] = width * 4;
							sws_scale(this->sws_ctx, this->frame->data, this->frame->linesize, 0, this->frame->height, this->dst_data, this->dst_linesize);
						//}
					}

					// This is fine enough to premultiply straight alpha pixels
					// but we use I420AlphaToARGBMatrix to do everything in a single pass.
					if (this->frame->format == AV_PIX_FMT_YUVA420P) {
						for (int i = 0; i < width * height * 4; i += 4) {
							auto alpha = pixels[i + 3];
							pixels[i + 0] = FAST_DIV255(pixels[i + 0] * alpha);
							pixels[i + 1] = FAST_DIV255(pixels[i + 1] * alpha);
							pixels[i + 2] = FAST_DIV255(pixels[i + 2] * alpha);
						}
					}

					//bitmap.SetPixelBytes(winrt::array_view(pixels, pixelWidth * pixelHeight * 4));
					seconds = this->frame->best_effort_timestamp * av_q2d(this->video_stream->time_base);
					//delete[] pixels;

					this->prevFrame = timestamp;
					this->prevDuration = (1000 * this->frame->pkt_duration * av_q2d(this->video_stream->time_base));

					this->nextFrame = timestamp + this->limitedDuration;
				}

				this->has_decoded_frames = true;
				av_frame_unref(this->frame);

				return 1;
			}
			if (!this->has_decoded_frames) {
				triesCount--;
			}
		}
		return 0;
	}
} // namespace winrt::Unigram::Native::implementation
