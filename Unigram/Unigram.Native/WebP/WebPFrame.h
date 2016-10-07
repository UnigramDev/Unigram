#pragma once
#include "DemuxerWrapper.h"

using namespace Platform;
using namespace Windows::Storage;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage::Streams;
using namespace Windows::Foundation;
using namespace Microsoft::WRL;

namespace Unigram
{
	namespace Native
	{
		[Windows::Foundation::Metadata::WebHostHidden]
		public ref class WebPFrame sealed
		{
		internal:
			WebPFrame();

			std::shared_ptr<WebPDemuxerWrapper> spDemuxer;

			int frameNum;
			Point offset;
			int duration;
			int width;
			int height;
			bool disposeToBackgroundColor;
			bool blendWithPreviousFrame;

			//std::unique_ptr<uint8_t[]> pPayload;
			const uint8_t* pPayload;
			size_t payloadSize;

			static uint8_t* GetPointerToPixelData(IBuffer^ pixelBuffer, unsigned int *length);

		public:
			WriteableBitmap^ RenderFrame();

			property int PixelWidth
			{
				int get() { return width; }
			}

			property int PixelHeight
			{
				int get() { return height; }
			}

			property int Duration
			{
				int get() { return duration; }
			}

			property Point Offset
			{
				Point get() { return offset; }
			}
		};
	}
}