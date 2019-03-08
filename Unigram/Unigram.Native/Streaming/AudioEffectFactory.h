#pragma once
#include "AbstractEffectFactory.h"
#include "AudioFilter.h"
namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			ref class AudioEffectFactory : public AbstractEffectFactory
			{
				AVCodecContext* InputContext;
				long long inChannelLayout;
				int nb_channels;

			internal:

				AudioEffectFactory(AVCodecContext* input_ctx, long long p_inChannelLayout, int p_nb_channels)
				{
					InputContext = input_ctx;
					inChannelLayout = p_inChannelLayout;
					nb_channels = p_nb_channels;
				}

				IAvEffect^ CreateEffect(IVectorView<AvEffectDefinition^>^ definitions) override
				{
					AudioFilter^ filter = ref new AudioFilter(InputContext, inChannelLayout, nb_channels);
					auto hr = filter ? S_OK : E_OUTOFMEMORY;
					if (SUCCEEDED(hr))
					{
						hr = filter->AllocResources(definitions);
					}
					if (SUCCEEDED(hr))
					{
						return filter;
					}
					else
					{
						return nullptr;
					}

				}
			};
		}
	}
}