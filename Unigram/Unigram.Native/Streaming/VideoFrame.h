#pragma once

#include <robuffer.h>
#include <pplawait.h>

using namespace Windows::Foundation;
using namespace Windows::Graphics::Imaging;
using namespace Windows::Media::MediaProperties;
using namespace Windows::Storage::Streams;
using namespace Concurrency;

namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			public ref class VideoFrame sealed
			{
			public:
				property IBuffer^ PixelData { IBuffer^ get() { return pixelData; }}
				property unsigned int PixelWidth { unsigned int get() { return pixelWidth; }}
				property unsigned int PixelHeight { unsigned int get() { return pixelHeight; }}
				property MediaRatio^ PixelAspectRatio { MediaRatio^ get() { return pixelAspectRatio; } }
				property TimeSpan Timestamp { TimeSpan get() { return timestamp; }}

				VideoFrame(IBuffer^ pixelData, unsigned int width, unsigned int height, MediaRatio^ pixelAspectRatio, TimeSpan timestamp)
				{
					this->pixelData = pixelData;
					this->pixelWidth = width;
					this->pixelHeight = height;
					this->pixelAspectRatio = pixelAspectRatio;
					if (pixelAspectRatio != nullptr &&
						pixelAspectRatio->Numerator > 0 &&
						pixelAspectRatio->Denominator > 0 &&
						pixelAspectRatio->Numerator != pixelAspectRatio->Denominator)
					{
						hasNonSquarePixels = true;
					}
					this->timestamp = timestamp;
				}

				virtual ~VideoFrame()
				{

				}

				IAsyncAction^ EncodeAsBmpAsync(IRandomAccessStream^ stream)
				{
					return create_async([this, stream]
					{
						return this->Encode(stream, BitmapEncoder::BmpEncoderId);
					});
				}

				IAsyncAction^ EncodeAsJpegAsync(IRandomAccessStream^ stream)
				{
					return create_async([this, stream]
					{
						return this->Encode(stream, BitmapEncoder::JpegEncoderId);
					});
				}

				IAsyncAction^ EncodeAsPngAsync(IRandomAccessStream^ stream)
				{
					return create_async([this, stream]
					{
						return this->Encode(stream, BitmapEncoder::PngEncoderId);
					});
				}

				property unsigned int DisplayWidth
				{
					unsigned int get()
					{
						if (hasNonSquarePixels)
						{
							if (pixelAspectRatio->Numerator > pixelAspectRatio->Denominator)
							{
								return (unsigned int)round(((double)pixelAspectRatio->Numerator / pixelAspectRatio->Denominator) * pixelWidth);
							}
						}
						return pixelWidth;
					}
				}

				property unsigned int DisplayHeight
				{
					unsigned int get()
					{
						if (hasNonSquarePixels)
						{
							if (pixelAspectRatio->Numerator < pixelAspectRatio->Denominator)
							{
								return (unsigned int)round(((double)pixelAspectRatio->Denominator / pixelAspectRatio->Numerator) * pixelHeight);
							}
						}
						return pixelHeight;
					}
				}

				property double DisplayAspectRatio
				{
					double get()
					{
						double result = (double)pixelWidth / pixelHeight;
						if (hasNonSquarePixels)
						{
							return result * pixelAspectRatio->Numerator / pixelAspectRatio->Denominator;
						}
						return result;
					}
				}

			private:
				IBuffer ^ pixelData;
				unsigned int pixelWidth;
				unsigned int pixelHeight;
				TimeSpan timestamp;
				MediaRatio^ pixelAspectRatio;
				bool hasNonSquarePixels;

				task<void> Encode(IRandomAccessStream^ stream, Guid encoderGuid)
				{
					// Query the IBufferByteAccess interface.  
					Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
					reinterpret_cast<IInspectable*>(pixelData)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));

					// Retrieve the buffer data.  
					byte* pixels = nullptr;
					bufferByteAccess->Buffer(&pixels);
					auto length = pixelData->Length;

					return create_task(BitmapEncoder::CreateAsync(encoderGuid, stream)).then([this, pixels, length](task<BitmapEncoder^> encoder)
					{
						auto encoderValue = encoder.get();

						if (hasNonSquarePixels)
						{
							encoderValue->BitmapTransform->ScaledWidth = DisplayWidth;
							encoderValue->BitmapTransform->ScaledHeight = DisplayHeight;
							encoderValue->BitmapTransform->InterpolationMode = BitmapInterpolationMode::Linear;
						}

						encoderValue->SetPixelData(
							BitmapPixelFormat::Bgra8,
							BitmapAlphaMode::Ignore,
							pixelWidth,
							pixelHeight,
							72,
							72,
							ArrayReference<byte>(pixels, length));

						return encoderValue->FlushAsync();
					});
				}

			};
		}
	}
}