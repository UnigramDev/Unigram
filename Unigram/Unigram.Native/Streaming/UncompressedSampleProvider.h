//*****************************************************************************
//
//	Copyright 2016 Microsoft Corporation
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
#include "MediaSampleProvider.h"
#include "UncompressedFrameProvider.h"

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
			ref class UncompressedSampleProvider abstract : public MediaSampleProvider
			{
			internal:
				UncompressedSampleProvider(
					FFmpegReader^ reader,
					AVFormatContext* avFormatCtx,
					AVCodecContext* avCodecCtx,
					FFmpegInteropConfig^ config,
					int streamIndex
				);
				virtual HRESULT CreateNextSampleBuffer(IBuffer^* pBuffer, int64_t& samplePts, int64_t& sampleDuration) override;
				virtual HRESULT CreateBufferFromFrame(IBuffer^* pBuffer, AVFrame* avFrame, int64_t& framePts, int64_t& frameDuration) { return E_FAIL; }; // must be overridden by specific decoders
				virtual HRESULT GetFrameFromFFmpegDecoder(AVFrame* avFrame, int64_t& framePts, int64_t& frameDuration);
				virtual HRESULT FeedPacketToDecoder();
				void SetFilters(IVectorView<AvEffectDefinition^>^ effects) override {
					frameProvider->UpdateFilter(effects);
				}
				void DisableFilters() override
				{
					frameProvider->DisableFilter();
				}
				UncompressedFrameProvider ^ frameProvider;

			public:
				virtual void Flush() override;

			private:
				int64 nextFramePts;
				bool hasNextFramePts;
			};
		}
	}
}