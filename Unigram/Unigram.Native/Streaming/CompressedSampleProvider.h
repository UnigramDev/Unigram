#pragma once

#include "MediaSampleProvider.h"

namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			ref class CompressedSampleProvider : MediaSampleProvider
			{
			public:
				virtual ~CompressedSampleProvider();

			internal:
				CompressedSampleProvider(FFmpegReader^ reader, AVFormatContext* avFormatCtx, AVCodecContext* avCodecCtx, FFmpegInteropConfig^ config, int streamIndex);
				CompressedSampleProvider(FFmpegReader^ reader, AVFormatContext* avFormatCtx, AVCodecContext* avCodecCtx, FFmpegInteropConfig^ config, int streamIndex, VideoEncodingProperties^ encodingProperties);
				CompressedSampleProvider(FFmpegReader^ reader, AVFormatContext* avFormatCtx, AVCodecContext* avCodecCtx, FFmpegInteropConfig^ config, int streamIndex, AudioEncodingProperties^ encodingProperties);
				virtual HRESULT CreateNextSampleBuffer(IBuffer^* pBuffer, int64_t& samplePts, int64_t& sampleDuration) override;
				virtual HRESULT CreateBufferFromPacket(AVPacket* avPacket, IBuffer^* pBuffer);
				virtual IMediaStreamDescriptor^ CreateStreamDescriptor() override;

			private:
				VideoEncodingProperties^ videoEncodingProperties;
				AudioEncodingProperties^ audioEncodingProperties;
			};
		}
	}
}