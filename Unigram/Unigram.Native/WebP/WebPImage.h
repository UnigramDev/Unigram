#pragma once

#include "WebPFrame.h"

using namespace Platform;
using namespace Windows::Storage;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage::Streams;
using namespace Microsoft::WRL;

namespace Unigram
{
	namespace Native
	{
		[Windows::Foundation::Metadata::WebHostHidden]
		public ref class WebPImage sealed
		{
		internal:
			WebPImage();

			std::shared_ptr<WebPDemuxerWrapper> spDemuxer;

		private:
			int pixelWidth;
			int pixelHeight;
			int numFrames;
			int loopCount;
			int totalDuration;

			//const Array<int>^ frameDurationsMs;

			Array<WebPFrame^>^ frames;

			static WriteableBitmap^ CreateWriteableBitmapFromWebPData(WebPData webPData);

		public:
			static WebPImage^ CreateFromByteArray(const Array<uint8> ^bytes);

			static WriteableBitmap^ DecodeFromBuffer(IBuffer^ buffer);
			static WriteableBitmap^ DecodeFromByteArray(const Array<uint8> ^bytes);
			static IRandomAccessStream^ Encode(const Array<uint8> ^bytes);

			property int PixelWidth
			{
				int get() { return pixelWidth; }
			}

			property int PixelHeight
			{
				int get() { return pixelHeight; }
			}

			property int LoopCount
			{
				int get() { return loopCount; }
			}

			property int TotalDuration
			{
				int get() { return totalDuration; }
			}

			//property const Array<int>^ FrameDurations
			//{
			//	const Array<int>^ get() { return frameDurationsMs; }
			//}

			property Array<WebPFrame^>^ Frames
			{
				Array<WebPFrame^>^ get() { return frames; }
			}

			//WebPFrame^ GetFrame(int index);
		};
	}
}