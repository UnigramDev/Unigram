#include "pch.h"
#include "WebPImage.h"
#include <wincodec.h>
#include <shcore.h>

using namespace Windows::Storage;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage::Streams;
using namespace Unigram::Native;
using namespace Platform;

WebPImage::WebPImage()
{

}

WebPImage^ WebPImage::CreateFromByteArray(const Array<uint8> ^bytes)
{
	auto vBuffer = std::vector<uint8_t>(bytes->Length);
	std::copy(bytes->begin(), bytes->end(), vBuffer.begin());

	WebPData webPData;
	webPData.bytes = vBuffer.data();
	webPData.size = vBuffer.size();

	WebPImage^ image = ref new WebPImage();

	auto spDemuxer = std::unique_ptr<WebPDemuxer, decltype(&WebPDemuxDelete)>
	{
		WebPDemux(&webPData),
		WebPDemuxDelete
	};
	if (!spDemuxer)
	{
		//throw ref new InvalidArgumentException(ref new String(L"Failed to create demuxer"));
		return nullptr;
	}

	image->pixelWidth = WebPDemuxGetI(spDemuxer.get(), WEBP_FF_CANVAS_WIDTH);
	image->pixelHeight = WebPDemuxGetI(spDemuxer.get(), WEBP_FF_CANVAS_HEIGHT);
	image->numFrames = WebPDemuxGetI(spDemuxer.get(), WEBP_FF_FRAME_COUNT);
	image->loopCount = WebPDemuxGetI(spDemuxer.get(), WEBP_FF_LOOP_COUNT);

	std::vector<WebPFrame^> frames;
	int durationMs = 0;
	//std::vector<int> frameDurationsMs;
	WebPIterator iter;
	if (WebPDemuxGetFrame(spDemuxer.get(), 1, &iter))
	{
		do
		{
			durationMs += iter.duration;
			//frameDurationsMs.push_back(iter.duration);

			WebPFrame^ frame = ref new WebPFrame();

			frame->spDemuxer = image->spDemuxer;
			frame->frameNum = iter.frame_num;
			frame->offset = Point(static_cast<float>(iter.x_offset), static_cast<float>(iter.y_offset));
			frame->duration = iter.duration;
			frame->width = iter.width;
			frame->height = iter.height;
			frame->disposeToBackgroundColor = iter.dispose_method == WEBP_MUX_DISPOSE_BACKGROUND;
			frame->blendWithPreviousFrame = iter.blend_method == WEBP_MUX_BLEND;
			//frame->pPayload = std::make_unique<uint8_t[]>(iter.fragment.size);
			frame->pPayload = iter.fragment.bytes;
			frame->payloadSize = iter.fragment.size;

			//CopyMemory(frame->pPayload.get(), iter.fragment.bytes, iter.fragment.size);

			frames.push_back(frame);

		} while (WebPDemuxNextFrame(&iter));

		WebPDemuxReleaseIterator(&iter);
	}

	image->totalDuration = durationMs;
	//spNativeContext->frameDurationsMs = ref new Array<int>(frameDurationsMs.data(), frameDurationsMs.size());
	image->frames = ref new Array<WebPFrame^>(frames.data(), frames.size());
	image->spDemuxer = std::shared_ptr<WebPDemuxerWrapper>(new WebPDemuxerWrapper(std::move(spDemuxer), std::move(vBuffer)));

	return image;
}

WriteableBitmap ^ Unigram::Native::WebPImage::DecodeFromBuffer(IBuffer ^ buffer)
{
	ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(buffer)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));

	uint8* webPBuffer;
	bufferByteAccess->Buffer(&webPBuffer);

	WebPData webPData = { webPBuffer, buffer->Length };

	return CreateWriteableBitmapFromWebPData(webPData);
}

WriteableBitmap^ WebPImage::DecodeFromByteArray(const Array<uint8> ^bytes)
{
	auto vBuffer = std::vector<uint8_t>(bytes->Length);
	std::copy(bytes->begin(), bytes->end(), vBuffer.begin());

	WebPData webPData;
	webPData.bytes = vBuffer.data();
	webPData.size = vBuffer.size();

	return CreateWriteableBitmapFromWebPData(webPData);
}

WriteableBitmap ^ Unigram::Native::WebPImage::CreateWriteableBitmapFromWebPData(WebPData webPData)
{
	auto spDemuxer = std::unique_ptr<WebPDemuxer, decltype(&WebPDemuxDelete)>
	{
		WebPDemux(&webPData),
		WebPDemuxDelete
	};
	if (!spDemuxer)
	{
		//throw ref new InvalidArgumentException(ref new String(L"Failed to create demuxer"));
		return nullptr;
	}

	WebPIterator iter;
	if (WebPDemuxGetFrame(spDemuxer.get(), 1, &iter))
	{
		WebPDecoderConfig config;
		int ret = WebPInitDecoderConfig(&config);
		if (!ret)
		{
			//throw ref new FailureException(ref new String(L"WebPInitDecoderConfig failed"));
			return nullptr;
		}

		ret = (WebPGetFeatures(iter.fragment.bytes, iter.fragment.size, &config.input) == VP8_STATUS_OK);
		if (!ret)
		{
			//throw ref new FailureException(ref new String(L"WebPGetFeatures failed"));
			return nullptr;
		}

		WriteableBitmap^ bitmap = ref new WriteableBitmap(iter.width, iter.height);

		unsigned int length;
		uint8_t* pixels = WebPFrame::GetPointerToPixelData(bitmap->PixelBuffer, &length);

		config.options.no_fancy_upsampling = 1;
		config.output.colorspace = MODE_bgrA;
		config.output.is_external_memory = 1;
		config.output.u.RGBA.rgba = pixels;
		config.output.u.RGBA.stride = iter.width * 4;
		config.output.u.RGBA.size = (iter.width * 4) * iter.height;

		ret = WebPDecode(iter.fragment.bytes, iter.fragment.size, &config);

		if (ret != VP8_STATUS_OK)
		{
			//throw ref new FailureException(ref new String(L"Failed to decode frame"));
			return nullptr;
		}

		return bitmap;
	}

	return nullptr;
}

IRandomAccessStream^ Unigram::Native::WebPImage::Encode(const Array<uint8> ^bytes)
{
	auto vBuffer = std::vector<uint8_t>(bytes->Length);
	std::copy(bytes->begin(), bytes->end(), vBuffer.begin());

	WebPData webPData;
	webPData.bytes = vBuffer.data();
	webPData.size = vBuffer.size();

	auto spDemuxer = std::unique_ptr<WebPDemuxer, decltype(&WebPDemuxDelete)>
	{
		WebPDemux(&webPData),
		WebPDemuxDelete
	};
	if (!spDemuxer)
	{
		//throw ref new InvalidArgumentException(ref new String(L"Failed to create demuxer"));
		return nullptr;
	}

	WebPIterator iter;
	if (WebPDemuxGetFrame(spDemuxer.get(), 1, &iter))
	{
		WebPDecoderConfig config;
		int ret = WebPInitDecoderConfig(&config);
		if (!ret)
		{
			//throw ref new FailureException(ref new String(L"WebPInitDecoderConfig failed"));
			return nullptr;
		}

		ret = (WebPGetFeatures(iter.fragment.bytes, iter.fragment.size, &config.input) == VP8_STATUS_OK);
		if (!ret)
		{
			//throw ref new FailureException(ref new String(L"WebPGetFeatures failed"));
			return nullptr;
		}

		int width = iter.width;
		int height = iter.height;

		if (iter.width > 256 || iter.height > 256)
		{
			auto ratioX = (double)256 / iter.width;
			auto ratioY = (double)256 / iter.height;
			auto ratio = std::min(ratioX, ratioY);

			width = (int)(iter.width * ratio);
			height = (int)(iter.height * ratio);
		}

		uint8_t* pixels = new uint8_t[(width * 4) * height];

		config.options.scaled_width = width;
		config.options.scaled_height = height;
		config.options.use_scaling = 1;
		config.options.no_fancy_upsampling = 1;
		config.output.colorspace = MODE_bgrA;
		config.output.is_external_memory = 1;
		config.output.u.RGBA.rgba = pixels;
		config.output.u.RGBA.stride = width * 4;
		config.output.u.RGBA.size = (width * 4) * height;

		ret = WebPDecode(iter.fragment.bytes, iter.fragment.size, &config);

		if (ret != VP8_STATUS_OK)
		{
			//throw ref new FailureException(ref new String(L"Failed to decode frame"));
			return nullptr;
		}

		//return ref new Array<uint8>(vPixels.data(), vPixels.size());

		InMemoryRandomAccessStream^ stream = ref new InMemoryRandomAccessStream();

		ComPtr<IWICImagingFactory> piFactory;
		ComPtr<IWICBitmapEncoder> piEncoder;
		ComPtr<IStream> piStream;

		CoCreateInstance(CLSID_WICImagingFactory, NULL, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&piFactory));
		
		HRESULT dioBubu = CreateStreamOverRandomAccessStream(stream, IID_PPV_ARGS(&piStream));

		piFactory->CreateEncoder(GUID_ContainerFormatPng, NULL, &piEncoder);
		piEncoder->Initialize(piStream.Get(), WICBitmapEncoderNoCache);

		ComPtr<IPropertyBag2> propertyBag;
		ComPtr<IWICBitmapFrameEncode> frame;
		piEncoder->CreateNewFrame(&frame, &propertyBag);

		frame->Initialize(propertyBag.Get());
		frame->SetSize(width, height);

		WICPixelFormatGUID format = GUID_WICPixelFormat32bppPBGRA;
		frame->SetPixelFormat(&format);
		frame->WritePixels(height, width * 4, (width * 4) * height, pixels);

		frame->Commit();
		piEncoder->Commit();

		delete pixels;

		return stream;
	}

	return nullptr;
}