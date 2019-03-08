#pragma once
#include "IAvEffect.h"
#include "AvEffectDefinition.h"
#include "AbstractEffectFactory.h"
#include <mutex>

extern "C"
{
#include <libavformat/avformat.h>
}

using namespace Windows::Foundation::Collections;

namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			ref class UncompressedFrameProvider sealed
			{
				IAvEffect^ filter;
				AVFormatContext* m_pAvFormatCtx;
				AVCodecContext* m_pAvCodecCtx;
				AbstractEffectFactory^ m_effectFactory;

			internal:

				UncompressedFrameProvider(AVFormatContext* p_pAvFormatCtx, AVCodecContext* p_pAvCodecCtx, AbstractEffectFactory^ p_effectFactory)
				{
					m_pAvCodecCtx = p_pAvCodecCtx;
					m_pAvFormatCtx = p_pAvFormatCtx;
					m_effectFactory = p_effectFactory;
				}

				void UpdateFilter(IVectorView<AvEffectDefinition^>^ effects)
				{
					filter = m_effectFactory->CreateEffect(effects);
				}

				void DisableFilter()
				{
					filter = nullptr;
				}

				HRESULT GetFrameFromCodec(AVFrame *avFrame)
				{
					HRESULT hr = avcodec_receive_frame(m_pAvCodecCtx, avFrame);
					if (SUCCEEDED(hr))
					{
						if (filter)
						{
							hr = filter->AddFrame(avFrame);
							if (SUCCEEDED(hr))
							{
								hr = filter->GetFrame(avFrame);
							}
							if (FAILED(hr))
							{
								av_frame_unref(avFrame);
							}
						}
					}
					return hr;
				}
			};
		}
	}
}