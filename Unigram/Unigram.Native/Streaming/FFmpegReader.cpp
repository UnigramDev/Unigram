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
#include "FFmpegReader.h"

using namespace Unigram::Native::Streaming;

FFmpegReader::FFmpegReader(AVFormatContext* avFormatCtx, std::vector<MediaSampleProvider^>* initProviders)
	: m_pAvFormatCtx(avFormatCtx)
	, sampleProviders(initProviders)
{
}

FFmpegReader::~FFmpegReader()
{
}

// Read the next packet from the stream and push it into the appropriate
// sample provider
int FFmpegReader::ReadPacket()
{
	int ret;
	AVPacket *avPacket = av_packet_alloc();
	if (!avPacket)
	{
		return E_OUTOFMEMORY;
	}

	ret = av_read_frame(m_pAvFormatCtx, avPacket);
	if (ret < 0)
	{
		av_packet_free(&avPacket);
		return ret;
	}

	if (avPacket->stream_index > (int)sampleProviders->size() || avPacket->stream_index < 0)
	{
		av_packet_free(&avPacket);
		return E_FAIL;
	}

	MediaSampleProvider^ provider = sampleProviders->at(avPacket->stream_index);
	if (provider)
	{
		provider->QueuePacket(avPacket);
	}
	else
	{
		DebugMessage(L"Ignoring unused stream\n");
		av_packet_free(&avPacket);
	}

	return ret;
}

