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
#include <libswscale/swscale.h>
}

using namespace Platform;

namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			ref class UncompressedVideoSampleProvider : UncompressedSampleProvider
			{
			public:
				virtual ~UncompressedVideoSampleProvider();
				virtual void Flush() override
				{
					hasFirstInterlacedFrame = false;
					UncompressedSampleProvider::Flush();
				}

			internal:
				UncompressedVideoSampleProvider(
					FFmpegReader^ reader,
					AVFormatContext* avFormatCtx,
					AVCodecContext* avCodecCtx,
					FFmpegInteropConfig^ config,
					int streamIndex);
				IMediaStreamDescriptor^ CreateStreamDescriptor() override;
				virtual HRESULT CreateBufferFromFrame(IBuffer^* pBuffer, AVFrame* avFrame, int64_t& framePts, int64_t& frameDuration) override;
				virtual HRESULT SetSampleProperties(MediaStreamSample^ sample) override;
				AVPixelFormat GetOutputPixelFormat() { return m_OutputPixelFormat; }

			private:
				void SelectOutputFormat();
				HRESULT InitializeScalerIfRequired();
				HRESULT FillLinesAndBuffer(int* linesize, byte** data, AVBufferRef** buffer);
				AVBufferRef* AllocateBuffer(int totalSize);
				static int get_buffer2(AVCodecContext *avCodecContext, AVFrame *frame, int flags);

				String^ outputMediaSubtype;
				int decoderWidth;
				int decoderHeight;

				AVBufferPool *m_pBufferPool;
				AVPixelFormat m_OutputPixelFormat;
				SwsContext* m_pSwsCtx;
				bool m_interlaced_frame;
				bool m_top_field_first;
				AVChromaLocation m_chroma_location;
				bool m_bUseScaler;
				bool hasFirstInterlacedFrame;
			};
		}
	}
}