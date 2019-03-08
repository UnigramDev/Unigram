#pragma once
extern "C"
{
#include <libavformat/avformat.h>
}
namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			ref class IAvEffect abstract
			{
			public:
				virtual	~IAvEffect() {}

			internal:

				virtual HRESULT AddFrame(AVFrame* frame) abstract;
				virtual HRESULT GetFrame(AVFrame* frame) abstract;
			};
		}
	}
}