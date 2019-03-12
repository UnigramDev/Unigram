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
#include "UncompressedSampleProvider.h"

extern "C"
{
#include <libswresample/swresample.h>
}

namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			ref class UncompressedAudioSampleProvider : UncompressedSampleProvider
			{
			public:
				virtual ~UncompressedAudioSampleProvider();

			internal:
				UncompressedAudioSampleProvider(
					FFmpegReader^ reader,
					AVFormatContext* avFormatCtx,
					AVCodecContext* avCodecCtx,
					FFmpegInteropConfig^ config,
					int streamIndex);
				virtual HRESULT CreateBufferFromFrame(IBuffer^* pBuffer, AVFrame* avFrame, int64_t& framePts, int64_t& frameDuration) override;
				IMediaStreamDescriptor^ CreateStreamDescriptor() override;
				HRESULT CheckFormatChanged(AVFrame* inputFrame);
				HRESULT UpdateResampler();


			private:
				SwrContext* m_pSwrCtx;
				AVSampleFormat inSampleFormat, outSampleFormat;
				int inSampleRate, outSampleRate, inChannels, outChannels;
				int64 inChannelLayout, outChannelLayout;
				bool needsUpdateResampler;
			};
		}
	}
}