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

#include "UncompressedAudioSampleProvider.h"
#include "NativeBufferFactory.h"
#include "AudioEffectFactory.h"

extern "C"
{
#include <libswresample/swresample.h>
}

using namespace Unigram::Native::Streaming;

#define min(X,Y) ((X) < (Y) ? (X) : (Y))
#define max(X,Y) ((X) < (Y) ? (Y) : (X))

UncompressedAudioSampleProvider::UncompressedAudioSampleProvider(
	FFmpegReader^ reader,
	AVFormatContext* avFormatCtx,
	AVCodecContext* avCodecCtx,
	FFmpegInteropConfig^ config,
	int streamIndex)
	: UncompressedSampleProvider(reader, avFormatCtx, avCodecCtx, config, streamIndex)
	, m_pSwrCtx(nullptr)
{
}

IMediaStreamDescriptor^ UncompressedAudioSampleProvider::CreateStreamDescriptor()
{
	inChannels = outChannels = m_pAvCodecCtx->profile == FF_PROFILE_AAC_HE_V2 && m_pAvCodecCtx->channels == 1 ? 2 : m_pAvCodecCtx->channels;
	inChannelLayout = m_pAvCodecCtx->channel_layout && (m_pAvCodecCtx->profile != FF_PROFILE_AAC_HE_V2 || m_pAvCodecCtx->channels > 1) ? m_pAvCodecCtx->channel_layout : av_get_default_channel_layout(inChannels);
	outChannelLayout = av_get_default_channel_layout(outChannels);
	inSampleRate = outSampleRate = m_pAvCodecCtx->sample_rate;
	inSampleFormat = m_pAvCodecCtx->sample_fmt;
	outSampleFormat =
		(inSampleFormat == AV_SAMPLE_FMT_S32 || inSampleFormat == AV_SAMPLE_FMT_S32P) ? AV_SAMPLE_FMT_S32 :
		(inSampleFormat == AV_SAMPLE_FMT_FLT || inSampleFormat == AV_SAMPLE_FMT_FLTP) ? AV_SAMPLE_FMT_FLT :
		AV_SAMPLE_FMT_S16;

	frameProvider = ref new UncompressedFrameProvider(m_pAvFormatCtx, m_pAvCodecCtx, ref new AudioEffectFactory(m_pAvCodecCtx, inChannelLayout, inChannels));

	needsUpdateResampler = inSampleFormat != outSampleFormat || inChannels != outChannels || inChannelLayout != outChannelLayout || inSampleRate != outSampleRate;

	// We try to preserve source format
	if (outSampleFormat == AV_SAMPLE_FMT_S32)
	{
		return ref new AudioStreamDescriptor(AudioEncodingProperties::CreatePcm(outSampleRate, outChannels, 32));
	}
	else if (outSampleFormat == AV_SAMPLE_FMT_FLT)
	{
		auto properties = ref new AudioEncodingProperties();
		properties->Subtype = MediaEncodingSubtypes::Float;
		properties->BitsPerSample = 32;
		properties->SampleRate = outSampleRate;
		properties->ChannelCount = outChannels;
		properties->Bitrate = 32 * outSampleRate * outChannels;
		return ref new AudioStreamDescriptor(properties);
	}
	else
	{
		// Use S16 for all other cases
		return ref new AudioStreamDescriptor(AudioEncodingProperties::CreatePcm(outSampleRate, outChannels, 16));
	}
}

HRESULT UncompressedAudioSampleProvider::CheckFormatChanged(AVFrame* frame)
{
	HRESULT hr = S_OK;

	auto channels = frame->channels;
	bool hasFormatChanged = channels != inChannels || frame->sample_rate != inSampleRate || frame->format != inSampleFormat;
	if (hasFormatChanged)
	{
		inChannels = channels;
		inChannelLayout = frame->channel_layout ? frame->channel_layout : av_get_default_channel_layout(inChannels);
		inSampleRate = frame->sample_rate;
		inSampleFormat = (AVSampleFormat)frame->format;
		needsUpdateResampler = true;
	}

	if (needsUpdateResampler)
	{
		hr = UpdateResampler();
	}

	return hr;
}

HRESULT UncompressedAudioSampleProvider::UpdateResampler()
{
	HRESULT hr = S_OK;

	auto needsResampler = inChannels != outChannels || inChannelLayout != outChannelLayout || inSampleRate != outSampleRate || inSampleFormat != outSampleFormat;
	if (needsResampler)
	{
		if (m_pSwrCtx)
		{
			swr_free(&m_pSwrCtx);
		}

		// Set up resampler to convert to output format and channel layout.
		m_pSwrCtx = swr_alloc_set_opts(
			NULL,
			outChannelLayout,
			outSampleFormat,
			outSampleRate,
			inChannelLayout,
			inSampleFormat,
			inSampleRate,
			0,
			NULL);

		if (!m_pSwrCtx)
		{
			hr = E_OUTOFMEMORY;
		}

		if (SUCCEEDED(hr))
		{
			if (swr_init(m_pSwrCtx) < 0)
			{
				hr = E_FAIL;
				swr_free(&m_pSwrCtx);
			}
		}
	}
	else
	{
		//dispose of it if we don't need it anymore
		if (m_pSwrCtx)
		{
			swr_free(&m_pSwrCtx);
		}
	}

	// force update next time if there was an error
	needsUpdateResampler = FAILED(hr);

	return hr;
}

UncompressedAudioSampleProvider::~UncompressedAudioSampleProvider()
{
	// Free 
	swr_free(&m_pSwrCtx);
}

HRESULT UncompressedAudioSampleProvider::CreateBufferFromFrame(IBuffer^* pBuffer, AVFrame* avFrame, int64_t& framePts, int64_t& frameDuration)
{
	HRESULT hr = S_OK;
	
	hr = CheckFormatChanged(avFrame);
	
	if (SUCCEEDED(hr))
	{
		if (m_pSwrCtx)
		{
			// Resample uncompressed frame to output format
			uint8_t **resampledData = nullptr;
			unsigned int aBufferSize = av_samples_alloc_array_and_samples(&resampledData, NULL, outChannels, avFrame->nb_samples, outSampleFormat, 0);
			int resampledDataSize = swr_convert(m_pSwrCtx, resampledData, aBufferSize, (const uint8_t **)avFrame->extended_data, avFrame->nb_samples);

			if (resampledDataSize < 0)
			{
				hr = E_FAIL;
			}
			else
			{
				auto size = min(aBufferSize, (unsigned int)(resampledDataSize * outChannels * av_get_bytes_per_sample(outSampleFormat)));
				*pBuffer = NativeBuffer::NativeBufferFactory::CreateNativeBuffer(resampledData[0], size, av_freep, resampledData);
			}
		}
		else
		{
			// Using direct buffer: just create a buffer reference to hand out to MSS pipeline
			auto bufferRef = av_buffer_ref(avFrame->buf[0]);
			if (bufferRef)
			{
				auto size = min(bufferRef->size, avFrame->nb_samples * outChannels * av_get_bytes_per_sample(outSampleFormat));
				*pBuffer = NativeBuffer::NativeBufferFactory::CreateNativeBuffer(bufferRef->data, size, free_buffer, bufferRef);
			}
			else
			{
				hr = E_FAIL;
			}
		}
	}

	if (SUCCEEDED(hr))
	{
		// always update duration with real decoded sample duration
		auto actualDuration = (long long)avFrame->nb_samples * m_pAvStream->time_base.den / (outSampleRate * m_pAvStream->time_base.num);

		if (frameDuration != actualDuration)
		{
			// compensate for start encoder padding (gapless playback)
			if (m_pAvStream->nb_decoded_frames == 1 && m_pAvStream->start_skip_samples > 0)
			{
				// check if duration difference matches encoder padding
				auto skipDuration = (long long)m_pAvStream->start_skip_samples * m_pAvStream->time_base.den / (outSampleRate * m_pAvStream->time_base.num);
				if (skipDuration == frameDuration - actualDuration)
				{
					framePts += skipDuration;
				}
			}
			frameDuration = actualDuration;
		}
	}

	return hr;
}

