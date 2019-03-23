//*****************************************************************************
//
//	Copyright 2015 Microsoft Corporation
//
//	Licensed under the Apache License, Version 2.0 (the "License");
//	you may not use this file except in compliance with the License.
//	You may obtain a copy of the License at
//
//	http ://www.apache.org/licenses/LICENSE-2.0
//
//	Unless required by applicable law or agreed to in writing, software
//	distributed under the License is distributed on an "AS IS" BASIS,
//	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//	See the License for the specific language governing permissions and
//	limitations under the License.
//
//*****************************************************************************

#include "pch.h"
#include "FFmpegInteropMSS.h"
#include "CompressedSampleProvider.h"
#include "H264AVCSampleProvider.h"
#include "NALPacketSampleProvider.h"
#include "HEVCSampleProvider.h"
#include "UncompressedAudioSampleProvider.h"
#include "UncompressedVideoSampleProvider.h"
#include "SubtitleProviderSsaAss.h"
#include "SubtitleProviderBitmap.h"
#include "CritSec.h"
#include "shcore.h"
#include <mfapi.h>
#include <dshow.h>
#include "LanguageTagConverter.h"

extern "C"
{
#include <libavutil/imgutils.h>
}

using namespace concurrency;
using namespace Unigram::Native::Streaming;
using namespace Platform;
using namespace Platform::Collections;
using namespace Windows::Storage::Streams;
using namespace Windows::Media::MediaProperties;
using namespace Telegram::Td::Api;

#define min(X,Y) ((X) < (Y) ? (X) : (Y))
#define max(X,Y) ((X) < (Y) ? (Y) : (X))

// Static functions passed to FFmpeg
static int FileStreamRead(void* ptr, uint8_t* buf, int bufSize);
static int64_t FileStreamSeek(void* ptr, int64_t pos, int whence);

// Initialize an FFmpegInteropObject
FFmpegInteropMSS::FFmpegInteropMSS(FFmpegInteropConfig^ interopConfig)
	: config(interopConfig)
	, thumbnailStreamIndex(AVERROR_STREAM_NOT_FOUND)
	, isFirstSeek(true)
	, m_handle(INVALID_HANDLE_VALUE)
{
}

FFmpegInteropMSS::~FFmpegInteropMSS()
{
	mutexGuard.lock();
	if (mss)
	{
		mss->Starting -= startingRequestedToken;
		mss->SampleRequested -= sampleRequestedToken;
		mss->SwitchStreamsRequested -= switchStreamRequestedToken;
		mss = nullptr;
	}

	if (playbackItem)
	{
		playbackItem->AudioTracksChanged -= audioTracksChangedToken;
		playbackItem->TimedMetadataTracks->PresentationModeChanged -= subtitlePresentationModeChangedToken;
		playbackItem = nullptr;
	}

	// Clear our data
	currentAudioStream = nullptr;
	videoStream = nullptr;

	if (m_pReader != nullptr)
	{
		m_pReader = nullptr;
	}

	sampleProviders.clear();
	audioStreams.clear();

	avformat_close_input(&avFormatCtx);
	av_free(avIOCtx);
	av_dict_free(&avDict);

	if (fileStreamData != nullptr)
	{
		fileStreamData->Release();
	}
	mutexGuard.unlock();
}

IAsyncOperation<FFmpegInteropMSS^>^ FFmpegInteropMSS::CreateFromFileAsync(Client^ client, File^ file, FFmpegInteropConfig^ config)
{
	return create_async([this, client, file, config]
	{
		auto result = this->CreateFromFile(client, file, config, nullptr);
		if (result == nullptr)
		{
			throw ref new Exception(E_FAIL, "Could not create MediaStreamSource.");
		}
		return result;
	});
};

FFmpegInteropMSS^ FFmpegInteropMSS::CreateFromFile(Client^ client, File^ file, FFmpegInteropConfig^ config, MediaStreamSource^ mss)
{
	//auto interopMSS = ref new FFmpegInteropMSS(config);
	auto hr = this->CreateMediaStreamSource(client, file, mss);
	if (!SUCCEEDED(hr))
	{
		throw ref new Exception(hr, "Failed to open media.");
	}
	return this;
}

MediaStreamSource^ FFmpegInteropMSS::GetMediaStreamSource()
{
	if (this->config->IsFrameGrabber) throw ref new Exception(E_UNEXPECTED);
	return mss;
}

MediaSource^ FFmpegInteropMSS::CreateMediaSource()
{
	if (this->config->IsFrameGrabber) throw ref new Exception(E_UNEXPECTED);
	MediaSource^ source = MediaSource::CreateFromMediaStreamSource(mss);
	for each (auto stream in subtitleStreams)
	{
		source->ExternalTimedMetadataTracks->Append(stream->SubtitleTrack);
	}
	return source;
}

MediaPlaybackItem^ FFmpegInteropMSS::CreateMediaPlaybackItem()
{
	mutexGuard.lock();
	try
	{
		if (this->config->IsFrameGrabber || playbackItem != nullptr) throw ref new Exception(E_UNEXPECTED);
		playbackItem = ref new MediaPlaybackItem(CreateMediaSource());
		InitializePlaybackItem(playbackItem);
		mutexGuard.unlock();
		return playbackItem;
	}
	catch (...)
	{
		mutexGuard.unlock();
		throw;
	}
}

MediaPlaybackItem^ FFmpegInteropMSS::CreateMediaPlaybackItem(TimeSpan startTime)
{
	mutexGuard.lock();
	try
	{
		if (this->config->IsFrameGrabber || playbackItem != nullptr) throw ref new Exception(E_UNEXPECTED);
		playbackItem = ref new MediaPlaybackItem(CreateMediaSource(), startTime);
		InitializePlaybackItem(playbackItem);
		mutexGuard.unlock();
		return playbackItem;
	}
	catch (...)
	{
		mutexGuard.unlock();
		throw;
	}
}

MediaPlaybackItem^ FFmpegInteropMSS::CreateMediaPlaybackItem(TimeSpan startTime, TimeSpan durationLimit)
{
	mutexGuard.lock();
	try
	{
		if (this->config->IsFrameGrabber || playbackItem != nullptr) throw ref new Exception(E_UNEXPECTED);
		playbackItem = ref new MediaPlaybackItem(CreateMediaSource(), startTime, durationLimit);
		InitializePlaybackItem(playbackItem);
		mutexGuard.unlock();
		return playbackItem;
	}
	catch (...)
	{
		mutexGuard.unlock();
		throw;
	}
}

void FFmpegInteropMSS::InitializePlaybackItem(MediaPlaybackItem^ playbackitem)
{
	audioTracksChangedToken = playbackitem->AudioTracksChanged += ref new Windows::Foundation::TypedEventHandler<Windows::Media::Playback::MediaPlaybackItem ^, Windows::Foundation::Collections::IVectorChangedEventArgs ^>(this, &Unigram::Native::Streaming::FFmpegInteropMSS::OnAudioTracksChanged);
	subtitlePresentationModeChangedToken = playbackitem->TimedMetadataTracks->PresentationModeChanged += ref new Windows::Foundation::TypedEventHandler<Windows::Media::Playback::MediaPlaybackTimedMetadataTrackList ^, Windows::Media::Playback::TimedMetadataPresentationModeChangedEventArgs ^>(this, &Unigram::Native::Streaming::FFmpegInteropMSS::OnPresentationModeChanged);

	if (config->AutoSelectForcedSubtitles)
	{
		int index = 0;
		for each (auto stream in subtitleStreams)
		{
			if (subtitleStreamInfos->GetAt(index)->IsForced)
			{
				playbackitem->TimedMetadataTracks->SetPresentationMode(index, TimedMetadataTrackPresentationMode::PlatformPresented);
				break;
			}
			index++;
		}
	}
}

void Unigram::Native::Streaming::FFmpegInteropMSS::OnPresentationModeChanged(MediaPlaybackTimedMetadataTrackList ^sender, TimedMetadataPresentationModeChangedEventArgs ^args)
{
	mutexGuard.lock();
	int index = 0;
	for each (auto stream in subtitleStreams)
	{
		if (stream->SubtitleTrack == args->Track)
		{
			if (args->NewPresentationMode == TimedMetadataTrackPresentationMode::Disabled)
			{
				stream->DisableStream();
			}
			else
			{
				stream->EnableStream();
			}
		}
		index++;
	}
	mutexGuard.unlock();
}

void Unigram::Native::Streaming::FFmpegInteropMSS::OnAudioTracksChanged(MediaPlaybackItem ^sender, IVectorChangedEventArgs ^args)
{
	mutexGuard.lock();
	if (sender->AudioTracks->Size == AudioStreams->Size)
	{
		for (unsigned int i = 0; i < AudioStreams->Size; i++)
		{
			auto track = sender->AudioTracks->GetAt(i);
			auto info = AudioStreams->GetAt(i);
			if (info->Name != nullptr)
			{
				track->Label = info->Name;
			}
			else if (info->Language != nullptr)
			{
				track->Label = info->Language;
			}
		}
	}
	mutexGuard.unlock();
}

HRESULT FFmpegInteropMSS::CreateMediaStreamSource(Client^ client, File^ file, MediaStreamSource^ mss)
{
	HRESULT hr = S_OK;
	if (!file)
	{
		hr = E_INVALIDARG;
	}

	m_client = client;
	m_file = file;
	m_event = CreateEvent(NULL, TRUE, TRUE, NULL);

	if (SUCCEEDED(hr))
	{
		// Setup FFmpeg custom IO to access file as stream. This is necessary when accessing any file outside of app installation directory and appdata folder.
		// Credit to Philipp Sch http://www.codeproject.com/Tips/489450/Creating-Custom-FFmpeg-IO-Context
		fileStreamBuffer = (unsigned char*)av_malloc(config->StreamBufferSize);
		if (fileStreamBuffer == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	if (SUCCEEDED(hr))
	{
		avIOCtx = avio_alloc_context(fileStreamBuffer, config->StreamBufferSize, 0, (void*)this, FileStreamRead, 0, FileStreamSeek);
		if (avIOCtx == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	if (SUCCEEDED(hr))
	{
		avFormatCtx = avformat_alloc_context();
		if (avFormatCtx == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	if (SUCCEEDED(hr))
	{
		// Populate AVDictionary avDict based on PropertySet ffmpegOptions. List of options can be found in https://www.ffmpeg.org/ffmpeg-protocols.html
		hr = ParseOptions(config->FFmpegOptions);
	}

	if (SUCCEEDED(hr))
	{
		avFormatCtx->pb = avIOCtx;
		avFormatCtx->flags |= AVFMT_FLAG_CUSTOM_IO;

		// Open media file using custom IO setup above instead of using file name. Opening a file using file name will invoke fopen C API call that only have
		// access within the app installation directory and appdata folder. Custom IO allows access to file selected using FilePicker dialog.
		//auto error = avformat_open_input(&avFormatCtx, "", NULL, &avDict);
		//char errbuf[1024];
		//av_strerror(error, errbuf, 1024);
		if (avformat_open_input(&avFormatCtx, "", NULL, &avDict) < 0)
		{
			hr = E_FAIL; // Error opening file
		}

		// avDict is not NULL only when there is an issue with the given ffmpegOptions such as invalid key, value type etc. Iterate through it to see which one is causing the issue.
		if (avDict != nullptr)
		{
			DebugMessage(L"Invalid FFmpeg option(s)");
			av_dict_free(&avDict);
			avDict = nullptr;
		}
	}

	if (SUCCEEDED(hr))
	{
		this->mss = mss;
		hr = InitFFmpegContext();
	}

	return hr;
}

static int is_hwaccel_pix_fmt(enum AVPixelFormat pix_fmt)
{
	const AVPixFmtDescriptor *desc = av_pix_fmt_desc_get(pix_fmt);
	return desc->flags & AV_PIX_FMT_FLAG_HWACCEL;
}

static AVPixelFormat get_format(struct AVCodecContext *s, const enum AVPixelFormat *fmt)
{
	AVPixelFormat result = (AVPixelFormat)-1;
	AVPixelFormat format;
	int index = 0;
	do
	{
		format = fmt[index++];
		if (format != -1 && result == -1 && !is_hwaccel_pix_fmt(format))
		{
			// take first non hw accelerated format
			result = format;
		}
		else if (format == AV_PIX_FMT_NV12 && result != AV_PIX_FMT_YUVA420P)
		{
			// switch to NV12 if available, unless this is an alpha channel file
			result = format;
		}
	} while (format != -1);
	return result;
}

HRESULT FFmpegInteropMSS::InitFFmpegContext()
{
	HRESULT hr = S_OK;

	if (SUCCEEDED(hr))
	{
		if (avformat_find_stream_info(avFormatCtx, NULL) < 0)
		{
			hr = E_FAIL; // Error finding info
		}
	}

	if (SUCCEEDED(hr))
	{
		m_pReader = ref new FFmpegReader(avFormatCtx, &sampleProviders);
		if (m_pReader == nullptr)
		{
			hr = E_OUTOFMEMORY;
		}
	}

	auto audioStrInfos = ref new Vector<AudioStreamInfo^>();
	auto subtitleStrInfos = ref new Vector<SubtitleStreamInfo^>();

	AVCodec* avVideoCodec;
	auto videoStreamIndex = av_find_best_stream(avFormatCtx, AVMEDIA_TYPE_VIDEO, -1, -1, &avVideoCodec, 0);
	auto audioStreamIndex = av_find_best_stream(avFormatCtx, AVMEDIA_TYPE_AUDIO, -1, -1, NULL, 0);
	auto subtitleStreamIndex = av_find_best_stream(avFormatCtx, AVMEDIA_TYPE_SUBTITLE, -1, -1, NULL, 0);

	for (unsigned int index = 0; index < avFormatCtx->nb_streams; index++)
	{
		auto avStream = avFormatCtx->streams[index];
		MediaSampleProvider^ stream;

		if (avStream->codecpar->codec_type == AVMEDIA_TYPE_AUDIO && !config->IsFrameGrabber)
		{
			stream = CreateAudioStream(avStream, index);
			if (stream)
			{
				bool isDefault = index == audioStreamIndex;

				// TODO get info from sample provider
				auto channels = avStream->codecpar->channels;
				if (channels == 1 && avStream->codecpar->codec_id == AV_CODEC_ID_AAC && avStream->codecpar->profile == FF_PROFILE_AAC_HE_V2)
				{
					channels = 2;
				}
				auto info = ref new AudioStreamInfo(stream->Name, stream->Language, stream->CodecName, avStream->codecpar->bit_rate, isDefault,
					channels, avStream->codecpar->sample_rate,
					max(avStream->codecpar->bits_per_raw_sample, avStream->codecpar->bits_per_coded_sample));
				if (isDefault)
				{
					currentAudioStream = stream;
					audioStrInfos->InsertAt(0, info);
					audioStreams.insert(audioStreams.begin(), stream);
				}
				else
				{
					audioStrInfos->Append(info);
					audioStreams.push_back(stream);
				}
			}
		}
		else if (avStream->codecpar->codec_type == AVMEDIA_TYPE_VIDEO && avStream->disposition == AV_DISPOSITION_ATTACHED_PIC && thumbnailStreamIndex == AVERROR_STREAM_NOT_FOUND)
		{
			thumbnailStreamIndex = index;
		}
		else if (avStream->codecpar->codec_type == AVMEDIA_TYPE_VIDEO && index == videoStreamIndex)
		{
			videoStream = stream = CreateVideoStream(avStream, index);

			if (videoStream)
			{
				videoStreamInfo = ref new VideoStreamInfo(stream->Name, stream->Language, stream->CodecName, avStream->codecpar->bit_rate, true,
					avStream->codecpar->width, avStream->codecpar->height,
					max(avStream->codecpar->bits_per_raw_sample, avStream->codecpar->bits_per_coded_sample));
			}
		}
		else if (avStream->codecpar->codec_type == AVMEDIA_TYPE_SUBTITLE)
		{
			stream = CreateSubtitleSampleProvider(avStream, index);
			if (stream)
			{
				auto isDefault = index == subtitleStreamIndex;
				auto info = ref new SubtitleStreamInfo(stream->Name, stream->Language, stream->CodecName,
					isDefault, (avStream->disposition & AV_DISPOSITION_FORCED) == AV_DISPOSITION_FORCED, ((SubtitleProvider^)stream)->SubtitleTrack);
				if (isDefault)
				{
					subtitleStrInfos->InsertAt(0, info);
					subtitleStreams.insert(subtitleStreams.begin(), (SubtitleProvider^)stream);
				}
				else
				{
					subtitleStrInfos->Append(info);
					subtitleStreams.push_back((SubtitleProvider^)stream);
				}
			}
		}

		sampleProviders.push_back(stream);
	}

	if (!currentAudioStream && audioStreams.size() > 0)
	{
		currentAudioStream = audioStreams[0];
	}

	audioStreamInfos = audioStrInfos->GetView();
	subtitleStreamInfos = subtitleStrInfos->GetView();

	if (videoStream)
	{
		for each (auto stream in subtitleStreams)
		{
			stream->NotifyVideoFrameSize(videoStream->m_pAvCodecCtx->width, videoStream->m_pAvCodecCtx->height);
		}
	}

	if (videoStream && currentAudioStream)
	{
		mss = ref new MediaStreamSource(videoStream->StreamDescriptor, currentAudioStream->StreamDescriptor);
		videoStream->EnableStream();
		currentAudioStream->EnableStream();
	}
	else if (currentAudioStream)
	{
		mss = ref new MediaStreamSource(currentAudioStream->StreamDescriptor);
		currentAudioStream->EnableStream();
	}
	else if (videoStream)
	{
		mss = ref new MediaStreamSource(videoStream->StreamDescriptor);
		videoStream->EnableStream();
	}
	else
	{
		hr = E_FAIL;
	}

	if (SUCCEEDED(hr))
	{
		for each (auto stream in audioStreams)
		{
			if (stream != currentAudioStream)
			{
				mss->AddStreamDescriptor(stream->StreamDescriptor);
			}
		}
	}

	if (SUCCEEDED(hr))
	{
		// Convert media duration from AV_TIME_BASE to TimeSpan unit
		mediaDuration = { LONGLONG(avFormatCtx->duration * 10000000 / double(AV_TIME_BASE)) };

		TimeSpan buffer = { 0 };
		//TimeSpan buffer = { 60 * 10000000L };
		//mss->BufferTime = buffer;

		if (Windows::Foundation::Metadata::ApiInformation::IsPropertyPresent("Windows.Media.Core.MediaStreamSource", "MaxSupportedPlaybackRate"))
		{
			mss->MaxSupportedPlaybackRate = config->MaxSupportedPlaybackRate;
		}

		if (mediaDuration.Duration > 0)
		{
			mss->Duration = mediaDuration;
			mss->CanSeek = true;
		}

		startingRequestedToken = mss->Starting += ref new TypedEventHandler<MediaStreamSource ^, MediaStreamSourceStartingEventArgs ^>(this, &FFmpegInteropMSS::OnStarting);
		sampleRequestedToken = mss->SampleRequested += ref new TypedEventHandler<MediaStreamSource ^, MediaStreamSourceSampleRequestedEventArgs ^>(this, &FFmpegInteropMSS::OnSampleRequested);
		switchStreamRequestedToken = mss->SwitchStreamsRequested += ref new TypedEventHandler<MediaStreamSource ^, MediaStreamSourceSwitchStreamsRequestedEventArgs ^>(this, &FFmpegInteropMSS::OnSwitchStreamsRequested);
	}

	return hr;
}

SubtitleProvider^ FFmpegInteropMSS::CreateSubtitleSampleProvider(AVStream * avStream, int index)
{
	HRESULT hr = S_OK;
	SubtitleProvider^ avSubsStream = nullptr;
	auto avSubsCodec = avcodec_find_decoder(avStream->codecpar->codec_id);
	if (avSubsCodec)
	{
		// allocate a new decoding context
		auto avSubsCodecCtx = avcodec_alloc_context3(avSubsCodec);
		if (!avSubsCodecCtx)
		{
			DebugMessage(L"Could not allocate a decoding context\n");
			hr = E_OUTOFMEMORY;
		}

		if (SUCCEEDED(hr))
		{
			// initialize the stream parameters with demuxer information
			if (avcodec_parameters_to_context(avSubsCodecCtx, avStream->codecpar) < 0)
			{
				hr = E_FAIL;
			}

			if (SUCCEEDED(hr))
			{
				if (avcodec_open2(avSubsCodecCtx, avSubsCodec, NULL) < 0)
				{
					hr = E_FAIL;
				}
				else
				{
					if ((avSubsCodecCtx->codec_descriptor->props & AV_CODEC_PROP_TEXT_SUB) == AV_CODEC_PROP_TEXT_SUB)
					{
						avSubsStream = ref new SubtitleProviderSsaAss(m_pReader, avFormatCtx, avSubsCodecCtx, config, index);
					}
					else if ((avSubsCodecCtx->codec_descriptor->props & AV_CODEC_PROP_BITMAP_SUB) == AV_CODEC_PROP_BITMAP_SUB)
					{
						avSubsStream = ref new SubtitleProviderBitmap(m_pReader, avFormatCtx, avSubsCodecCtx, config, index);
					}
					else
					{
						hr = E_FAIL;
					}
				}
			}
		}

		if (SUCCEEDED(hr))
		{
			hr = avSubsStream->Initialize();
		}

		if (FAILED(hr))
		{
			avSubsStream = nullptr;
		}

		// free codec context if failed
		if (!avSubsStream && avSubsCodecCtx)
		{
			avcodec_free_context(&avSubsCodecCtx);
		}
	}
	else
	{
		DebugMessage(L"Could not find decoder\n");
	}

	return avSubsStream;
}

MediaSampleProvider^ FFmpegInteropMSS::CreateAudioStream(AVStream * avStream, int index)
{
	HRESULT hr = S_OK;
	MediaSampleProvider^ audioStream = nullptr;
	auto avAudioCodec = avcodec_find_decoder(avStream->codecpar->codec_id);
	if (avAudioCodec)
	{
		// allocate a new decoding context
		auto avAudioCodecCtx = avcodec_alloc_context3(avAudioCodec);
		if (!avAudioCodecCtx)
		{
			DebugMessage(L"Could not allocate a decoding context\n");
			hr = E_OUTOFMEMORY;
		}

		if (SUCCEEDED(hr))
		{
			// initialize the stream parameters with demuxer information
			if (avcodec_parameters_to_context(avAudioCodecCtx, avStream->codecpar) < 0)
			{
				hr = E_FAIL;
			}

			if (SUCCEEDED(hr))
			{
				if (avAudioCodecCtx->sample_fmt == AV_SAMPLE_FMT_S16P)
				{
					avAudioCodecCtx->request_sample_fmt = AV_SAMPLE_FMT_S16;
				}
				else if (avAudioCodecCtx->sample_fmt == AV_SAMPLE_FMT_S32P)
				{
					avAudioCodecCtx->request_sample_fmt = AV_SAMPLE_FMT_S32;
				}
				else if (avAudioCodecCtx->sample_fmt == AV_SAMPLE_FMT_FLTP)
				{
					avAudioCodecCtx->request_sample_fmt = AV_SAMPLE_FMT_FLT;
				}

				// enable multi threading
				unsigned threads = std::thread::hardware_concurrency();
				if (threads > 0)
				{
					avAudioCodecCtx->thread_count = config->MaxAudioThreads == 0 ? threads : min(threads, config->MaxAudioThreads);
					avAudioCodecCtx->thread_type = FF_THREAD_FRAME | FF_THREAD_SLICE;
				}

				if (avcodec_open2(avAudioCodecCtx, avAudioCodec, NULL) < 0)
				{
					hr = E_FAIL;
				}
				else
				{
					// Detect audio format and create audio stream descriptor accordingly
					audioStream = CreateAudioSampleProvider(avStream, avAudioCodecCtx, index);
				}
			}
		}

		// free codec context if failed
		if (!audioStream && avAudioCodecCtx)
		{
			avcodec_free_context(&avAudioCodecCtx);
		}
	}
	else
	{
		DebugMessage(L"Could not find decoder\n");
	}

	return audioStream;
}

MediaSampleProvider^ FFmpegInteropMSS::CreateVideoStream(AVStream * avStream, int index)
{
	HRESULT hr = S_OK;
	MediaSampleProvider^ result = nullptr;

	// Find the video stream and its decoder
	auto avVideoCodec = avcodec_find_decoder(avStream->codecpar->codec_id);

	if (avVideoCodec)
	{
		// allocate a new decoding context
		auto avVideoCodecCtx = avcodec_alloc_context3(avVideoCodec);
		if (!avVideoCodecCtx)
		{
			DebugMessage(L"Could not allocate a decoding context\n");
			hr = E_OUTOFMEMORY;
		}

		if (SUCCEEDED(hr))
		{
			avVideoCodecCtx->get_format = &get_format;

			// initialize the stream parameters with demuxer information
			if (avcodec_parameters_to_context(avVideoCodecCtx, avStream->codecpar) < 0)
			{
				hr = E_FAIL;
			}
		}

		if (SUCCEEDED(hr))
		{
			// enable multi threading
			unsigned threads = std::thread::hardware_concurrency();
			if (threads > 0)
			{
				avVideoCodecCtx->thread_count = config->MaxVideoThreads == 0 ? threads : min(threads, config->MaxVideoThreads);
				avVideoCodecCtx->thread_type = config->IsFrameGrabber ? FF_THREAD_SLICE : FF_THREAD_FRAME | FF_THREAD_SLICE;
			}

			if (avcodec_open2(avVideoCodecCtx, avVideoCodec, NULL) < 0)
			{
				hr = E_FAIL;
			}
			else
			{
				// Detect video format and create video stream descriptor accordingly
				result = CreateVideoSampleProvider(avStream, avVideoCodecCtx, index);
			}
		}

		// free codec context if failed
		if (!result && avVideoCodecCtx)
		{
			avcodec_free_context(&avVideoCodecCtx);
		}
	}

	return result;
}

void FFmpegInteropMSS::SetAudioEffects(IVectorView<AvEffectDefinition^>^ audioEffects)
{
	mutexGuard.lock();
	if (currentAudioStream)
	{
		currentAudioStream->SetFilters(audioEffects);
		currentAudioEffects = audioEffects;
	}
	mutexGuard.unlock();
}

void FFmpegInteropMSS::SetVideoEffects(IVectorView<AvEffectDefinition^>^ videoEffects)
{
	mutexGuard.lock();
	if (videoStream)
	{
		videoStream->SetFilters(videoEffects);
	}
	mutexGuard.unlock();
}

void FFmpegInteropMSS::DisableAudioEffects()
{
	mutexGuard.lock();
	if (currentAudioStream)
	{
		currentAudioStream->DisableFilters();
		currentAudioEffects = nullptr;
	}
	mutexGuard.unlock();
}

void FFmpegInteropMSS::DisableVideoEffects()
{
	mutexGuard.lock();
	if (videoStream)
	{
		videoStream->DisableFilters();
	}
	mutexGuard.unlock();
}

MediaThumbnailData ^ FFmpegInteropMSS::ExtractThumbnail()
{
	if (thumbnailStreamIndex != AVERROR_STREAM_NOT_FOUND)
	{
		// FFmpeg identifies album/cover art from a music file as a video stream
		// Avoid creating unnecessarily video stream from this album/cover art
		if (avFormatCtx->streams[thumbnailStreamIndex]->disposition == AV_DISPOSITION_ATTACHED_PIC)
		{
			auto imageStream = avFormatCtx->streams[thumbnailStreamIndex];
			//save album art to file.
			String^ extension = ".jpeg";
			switch (imageStream->codecpar->codec_id)
			{
			case AV_CODEC_ID_MJPEG:
			case AV_CODEC_ID_MJPEGB:
			case AV_CODEC_ID_JPEG2000:
			case AV_CODEC_ID_JPEGLS: extension = ".jpeg"; break;
			case AV_CODEC_ID_PNG: extension = ".png"; break;
			case AV_CODEC_ID_BMP: extension = ".bmp"; break;
			}


			auto vector = ArrayReference<uint8_t>(imageStream->attached_pic.data, imageStream->attached_pic.size);
			DataWriter^ writer = ref new DataWriter();
			writer->WriteBytes(vector);

			return (ref new MediaThumbnailData(writer->DetachBuffer(), extension));
		}
	}

	return nullptr;
}

MediaSampleProvider^ FFmpegInteropMSS::CreateAudioSampleProvider(AVStream* avStream, AVCodecContext* avAudioCodecCtx, int index)
{
	MediaSampleProvider^ audioSampleProvider;
	if (avAudioCodecCtx->codec_id == AV_CODEC_ID_AAC && config->PassthroughAudioAAC)
	{
		AudioEncodingProperties^ encodingProperties;
		if (avAudioCodecCtx->extradata_size == 0)
		{
			encodingProperties = AudioEncodingProperties::CreateAacAdts(avAudioCodecCtx->sample_rate, avAudioCodecCtx->channels, (unsigned int)avAudioCodecCtx->bit_rate);
		}
		else
		{
			encodingProperties = AudioEncodingProperties::CreateAac(avAudioCodecCtx->profile == FF_PROFILE_AAC_HE || avAudioCodecCtx->profile == FF_PROFILE_AAC_HE_V2 ? avAudioCodecCtx->sample_rate / 2 : avAudioCodecCtx->sample_rate, avAudioCodecCtx->channels, (unsigned int)avAudioCodecCtx->bit_rate);
		}
		audioSampleProvider = ref new CompressedSampleProvider(m_pReader, avFormatCtx, avAudioCodecCtx, config, index, encodingProperties);
	}
	else if (avAudioCodecCtx->codec_id == AV_CODEC_ID_MP3 && config->PassthroughAudioMP3)
	{
		AudioEncodingProperties^ encodingProperties = AudioEncodingProperties::CreateMp3(avAudioCodecCtx->sample_rate, avAudioCodecCtx->channels, (unsigned int)avAudioCodecCtx->bit_rate);
		audioSampleProvider = ref new CompressedSampleProvider(m_pReader, avFormatCtx, avAudioCodecCtx, config, index, encodingProperties);
	}
	else
	{
		audioSampleProvider = ref new UncompressedAudioSampleProvider(m_pReader, avFormatCtx, avAudioCodecCtx, config, index);
	}

	auto hr = audioSampleProvider->Initialize();
	if (FAILED(hr))
	{
		audioSampleProvider = nullptr;
	}

	return audioSampleProvider;
}

MediaSampleProvider^ FFmpegInteropMSS::CreateVideoSampleProvider(AVStream* avStream, AVCodecContext* avVideoCodecCtx, int index)
{
	MediaSampleProvider^ videoSampleProvider;
	VideoEncodingProperties^ videoProperties;

	if (avVideoCodecCtx->codec_id == AV_CODEC_ID_H264 && config->PassthroughVideoH264 && !config->IsFrameGrabber && (avVideoCodecCtx->profile <= 100 || config->PassthroughVideoH264Hi10P))
	{
		auto videoProperties = VideoEncodingProperties::CreateH264();

		// Check for H264 bitstream flavor. H.264 AVC extradata starts with 1 while non AVC one starts with 0
		if (avVideoCodecCtx->extradata != nullptr && avVideoCodecCtx->extradata_size > 0 && avVideoCodecCtx->extradata[0] == 1)
		{
			videoSampleProvider = ref new H264AVCSampleProvider(m_pReader, avFormatCtx, avVideoCodecCtx, config, index, videoProperties);
		}
		else
		{
			videoSampleProvider = ref new NALPacketSampleProvider(m_pReader, avFormatCtx, avVideoCodecCtx, config, index, videoProperties);
		}
	}
#if _WIN32_WINNT >= 0x0A00 // only compile if platform toolset is Windows 10 or higher
	else if (avVideoCodecCtx->codec_id == AV_CODEC_ID_HEVC && config->PassthroughVideoHEVC && !config->IsFrameGrabber &&
		Windows::Foundation::Metadata::ApiInformation::IsMethodPresent("Windows.Media.MediaProperties.VideoEncodingProperties", "CreateHevc"))
	{
		auto videoProperties = VideoEncodingProperties::CreateHevc();

		// Check for HEVC bitstream flavor.
		if (avVideoCodecCtx->extradata != nullptr && avVideoCodecCtx->extradata_size > 22 &&
			(avVideoCodecCtx->extradata[0] || avVideoCodecCtx->extradata[1] || avVideoCodecCtx->extradata[2] > 1))
		{
			videoSampleProvider = ref new HEVCSampleProvider(m_pReader, avFormatCtx, avVideoCodecCtx, config, index, videoProperties);
		}
		else
		{
			videoSampleProvider = ref new NALPacketSampleProvider(m_pReader, avFormatCtx, avVideoCodecCtx, config, index, videoProperties);
		}
	}
#endif
	else
	{
		videoSampleProvider = ref new UncompressedVideoSampleProvider(m_pReader, avFormatCtx, avVideoCodecCtx, config, index);
	}

	auto hr = videoSampleProvider->Initialize();

	if (FAILED(hr))
	{
		videoSampleProvider = nullptr;
	}

	return videoSampleProvider;
}

HRESULT FFmpegInteropMSS::ParseOptions(PropertySet^ ffmpegOptions)
{
	HRESULT hr = S_OK;

	// Convert FFmpeg options given in PropertySet to AVDictionary. List of options can be found in https://www.ffmpeg.org/ffmpeg-protocols.html
	if (ffmpegOptions != nullptr)
	{
		auto options = ffmpegOptions->First();

		while (options->HasCurrent)
		{
			String^ key = options->Current->Key;
			std::wstring keyW(key->Begin());
			std::string keyA(keyW.begin(), keyW.end());
			const char* keyChar = keyA.c_str();

			// Convert value from Object^ to const char*. avformat_open_input will internally convert value from const char* to the correct type
			String^ value = options->Current->Value->ToString();
			std::wstring valueW(value->Begin());
			std::string valueA(valueW.begin(), valueW.end());
			const char* valueChar = valueA.c_str();

			// Add key and value pair entry
			if (av_dict_set(&avDict, keyChar, valueChar, 0) < 0)
			{
				hr = E_INVALIDARG;
				break;
			}

			options->MoveNext();
		}
	}

	return hr;
}

void FFmpegInteropMSS::OnStarting(MediaStreamSource ^sender, MediaStreamSourceStartingEventArgs ^args)
{
	MediaStreamSourceStartingRequest^ request = args->Request;

	// Perform seek operation when MediaStreamSource received seek event from MediaElement
	if (request->StartPosition && request->StartPosition->Value.Duration <= mediaDuration.Duration && (!isFirstSeek || request->StartPosition->Value.Duration > 0))
	{
		auto hr = Seek(request->StartPosition->Value);
		if (SUCCEEDED(hr))
		{
			request->SetActualStartPosition(request->StartPosition->Value);
		}

		if (videoStream && !videoStream->IsEnabled)
		{
			videoStream->EnableStream();
		}

		if (currentAudioStream && !currentAudioStream->IsEnabled)
		{
			currentAudioStream->EnableStream();
		}
	}

	isFirstSeek = false;
}

void FFmpegInteropMSS::OnSampleRequested(Windows::Media::Core::MediaStreamSource ^sender, MediaStreamSourceSampleRequestedEventArgs ^args)
{
	mutexGuard.lock();
	if (mss != nullptr)
	{
		MediaStreamSourceSampleRequestDeferral^ deferral;
		if (WaitForSingleObject(m_event, 0) != WAIT_OBJECT_0)
		{
			deferral = args->Request->GetDeferral();
			args->Request->ReportSampleProgress(1);

			WaitForSingleObject(m_event, INFINITE);
		}

		if (currentAudioStream && args->Request->StreamDescriptor == currentAudioStream->StreamDescriptor)
		{
			args->Request->Sample = currentAudioStream->GetNextSample();
		}
		else if (videoStream && args->Request->StreamDescriptor == videoStream->StreamDescriptor)
		{
			args->Request->Sample = videoStream->GetNextSample();
		}
		else
		{
			args->Request->Sample = nullptr;
		}

		if (deferral != nullptr)
		{
			deferral->Complete();
		}
	}
	mutexGuard.unlock();
}

void FFmpegInteropMSS::OnSwitchStreamsRequested(MediaStreamSource ^ sender, MediaStreamSourceSwitchStreamsRequestedEventArgs ^ args)
{
	mutexGuard.lock();
	if (currentAudioStream && args->Request->OldStreamDescriptor == currentAudioStream->StreamDescriptor)
	{
		if (currentAudioEffects)
		{
			currentAudioStream->DisableFilters();
		}
		currentAudioStream->DisableStream();
		currentAudioStream = nullptr;
	}
	for each (auto stream in audioStreams)
	{
		if (stream->StreamDescriptor == args->Request->NewStreamDescriptor)
		{
			currentAudioStream = stream;
			currentAudioStream->EnableStream();
			if (currentAudioEffects)
			{
				currentAudioStream->SetFilters(currentAudioEffects);
			}
		}
	}
	mutexGuard.unlock();
}

HRESULT FFmpegInteropMSS::Seek(TimeSpan position)
{
	auto hr = S_OK;

	// Select the first valid stream either from video or audio
	int streamIndex = videoStream ? videoStream->StreamIndex : currentAudioStream ? currentAudioStream->StreamIndex : -1;

	if (streamIndex >= 0)
	{
		// Compensate for file start_time, then convert to stream time_base
		int64 correctedPosition;
		if (avFormatCtx->start_time == AV_NOPTS_VALUE)
		{
			correctedPosition = 0;
		}
		else
		{
			correctedPosition = position.Duration + (avFormatCtx->start_time * 10);
		}

		int64_t seekTarget = static_cast<int64_t>(correctedPosition / (av_q2d(avFormatCtx->streams[streamIndex]->time_base) * 10000000));

		if (av_seek_frame(avFormatCtx, streamIndex, seekTarget, AVSEEK_FLAG_BACKWARD) < 0)
		{
			hr = E_FAIL;
			DebugMessage(L" - ### Error while seeking\n");
		}
		else
		{
			// Flush the AudioSampleProvider
			if (currentAudioStream != nullptr)
			{
				currentAudioStream->Flush();
			}

			// Flush the VideoSampleProvider
			if (videoStream != nullptr)
			{
				videoStream->Flush();
			}
		}
	}
	else
	{
		hr = E_FAIL;
	}

	return hr;
}

// Static function to read file stream and pass data to FFmpeg. Credit to Philipp Sch http://www.codeproject.com/Tips/489450/Creating-Custom-FFmpeg-IO-Context
static int FileStreamRead(void* ptr, uint8_t* buf, int bufSize)
{
	FFmpegInteropMSS^ pStream = reinterpret_cast<FFmpegInteropMSS^>(ptr);
	LocalFile^ local = pStream->m_file->Local;

	auto begin = local->DownloadOffset;
	auto end = local->DownloadOffset + local->DownloadedPrefixSize;

	auto inBegin = pStream->m_offset >= begin;
	auto inEnd = end >= pStream->m_offset + bufSize || end == pStream->m_file->Size;
	auto difference = end - pStream->m_offset;

	if (local->Path && (inBegin && inEnd) || local->IsDownloadingCompleted)
	{
		// Media has enough buffer to play already
		// TODO: buffer more just to make sure that the video can play without stops
	}
	else
	{
		pStream->m_size = bufSize;
		//pStream->m_client->Send(ref new DownloadFile(pStream->m_file->Id, 32, pStream->m_offset, bufSize), nullptr);
		pStream->m_client->Send(ref new DownloadFile(pStream->m_file->Id, 32, pStream->m_offset, 4 * 1024 * 1024), nullptr);
		//pStream->m_client->Send(ref new DownloadFile(pStream->m_file->Id, 32, pStream->m_offset, 0), nullptr);

		ResetEvent(pStream->m_event);
		WaitForSingleObject(pStream->m_event, INFINITE);
	}

	//if (pStream->m_handle == INVALID_HANDLE_VALUE)
	//{
		HANDLE file = CreateFile2(pStream->m_file->Local->Path->Data(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, OPEN_EXISTING, nullptr);
	//}

	DWORD bytesRead;
	DWORD moved = SetFilePointer(file, pStream->m_offset, NULL, FILE_BEGIN);
	BOOL result = ReadFile(file, buf, bufSize, &bytesRead, NULL);

	CloseHandle(file);

	if (!result)
	{
		return -1;
	}

	// If we succeed but don't have any bytes, assume end of file
	if (bytesRead == 0)
	{
		return AVERROR_EOF;  // Let FFmpeg know that we have reached eof
	}

	pStream->m_offset += bytesRead;

	return (int)bytesRead;
}

// Static function to seek in file stream. Credit to Philipp Sch http://www.codeproject.com/Tips/489450/Creating-Custom-FFmpeg-IO-Context
static int64_t FileStreamSeek(void* ptr, int64_t pos, int whence)
{
	FFmpegInteropMSS^ pStream = reinterpret_cast<FFmpegInteropMSS^>(ptr);

	if (whence == AVSEEK_SIZE)
	{
		return pStream->m_file->Size;
	}

	int64_t offset;
	if (whence == SEEK_SET)
	{
		offset = pos;
	}
	else if (whence == SEEK_CUR)
	{
		offset = pStream->m_offset + pos;
	}
	else if (whence == SEEK_END)
	{
		offset = pStream->m_file->Size - pos;
	}

	pStream->m_offset = offset;

	return offset;
}



void FFmpegInteropMSS::UpdateFile(File^ file)
{
	if (file->Id != m_file->Id)
	{
		return;
	}

	m_file = file;

	if (file->Local->Path && (file->Local->DownloadOffset == m_offset && file->Local->DownloadedPrefixSize >= m_size) || file->Local->IsDownloadingCompleted)
	{
		SetEvent(m_event);
	}
}



static int lock_manager(void **mtx, enum AVLockOp op)
{
	switch (op)
	{
	case AV_LOCK_CREATE:
	{
		*mtx = new CritSec();
		return 0;
	}
	case AV_LOCK_OBTAIN:
	{
		auto mutex = static_cast<CritSec*>(*mtx);
		mutex->Lock();
		return 0;
	}
	case AV_LOCK_RELEASE:
	{
		auto mutex = static_cast<CritSec*>(*mtx);
		mutex->Unlock();
		return 0;
	}
	case AV_LOCK_DESTROY:
	{
		auto mutex = static_cast<CritSec*>(*mtx);
		delete mutex;
		return 0;
	}
	}
	return 1;
}

