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

#pragma once
#include <queue>
#include "FFmpegInteropConfig.h"
#include "AvEffectDefinition.h"

extern "C"
{
#include <libavformat/avformat.h>
}

using namespace Windows::Storage::Streams;
using namespace Windows::Media::Core;
using namespace Windows::Media::MediaProperties;

namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			ref class FFmpegReader;

			ref class MediaSampleProvider abstract
			{
			public:
				virtual ~MediaSampleProvider();
				virtual MediaStreamSample^ GetNextSample();
				virtual void Flush();

				property IMediaStreamDescriptor^ StreamDescriptor
				{
					IMediaStreamDescriptor^ get() { return m_streamDescriptor; }
				}

				property int StreamIndex
				{
					int get() { return m_streamIndex; }
				}

				property bool IsEnabled
				{
					bool get() { return m_isEnabled; }
				}

				property bool IsCleanSample;

				property String^ Name;
				property String^ Language;
				property String^ CodecName;

			internal:
				virtual HRESULT Initialize();
				void InitializeNameLanguageCodec();
				virtual void QueuePacket(AVPacket *packet);
				AVPacket* PopPacket();
				HRESULT GetNextPacket(AVPacket** avPacket, LONGLONG & packetPts, LONGLONG & packetDuration);
				virtual HRESULT CreateNextSampleBuffer(IBuffer^* pBuffer, int64_t& samplePts, int64_t& sampleDuration) = 0;
				virtual IMediaStreamDescriptor^ CreateStreamDescriptor() = 0;
				virtual HRESULT SetSampleProperties(MediaStreamSample^ sample) { return S_OK; }; // can be overridded for setting extended properties
				void EnableStream();
				void DisableStream();
				virtual void SetFilters(IVectorView<AvEffectDefinition^>^ effects) { };// override for setting effects in sample providers
				virtual void DisableFilters() {};//override for disabling filters in sample providers;
				virtual void SetCommonVideoEncodingProperties(VideoEncodingProperties^ videoEncodingProperties, bool isCompressedFormat);

			protected private:
				MediaSampleProvider(
					FFmpegReader^ reader,
					AVFormatContext* avFormatCtx,
					AVCodecContext* avCodecCtx,
					FFmpegInteropConfig^ config,
					int streamIndex);

			private:
				std::queue<AVPacket*> m_packetQueue;
				int64 m_nextPacketPts;
				IMediaStreamDescriptor^ m_streamDescriptor;

			internal:
				// The FFmpeg context. Because they are complex types
				// we declare them as internal so they don't get exposed
				// externally
				FFmpegInteropConfig^ m_config;
				FFmpegReader^ m_pReader;
				AVFormatContext* m_pAvFormatCtx;
				AVCodecContext* m_pAvCodecCtx;
				AVStream* m_pAvStream;
				bool m_isEnabled = false;
				bool m_isDiscontinuous;
				int m_streamIndex;
				int64 m_startOffset;

			};
		}
	}
}

String^ ConvertString(const char* charString);

// free AVBufferRef*
void free_buffer(void *lpVoid);
