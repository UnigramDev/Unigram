#include "pch.h"
#include "WebPFrame.h"

using namespace Windows::Storage;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage::Streams;
using namespace Microsoft::WRL;
using namespace Unigram::Native;
using namespace Platform;

WebPFrame::WebPFrame()
{

}

WriteableBitmap^ WebPFrame::RenderFrame()
{
	WebPDecoderConfig config;
	int ret = WebPInitDecoderConfig(&config);
	if (!ret)
	{
		throw ref new FailureException(ref new String(L"WebPInitDecoderConfig failed"));
	}

	//const uint8_t* pPayload = this->pPayload.get();
	const uint8_t* pPayload = this->pPayload;
	size_t payloadSize = this->payloadSize;

	ret = (WebPGetFeatures(pPayload, payloadSize, &config.input) == VP8_STATUS_OK);
	if (!ret)
	{
		throw ref new FailureException(ref new String(L"WebPGetFeatures failed"));
	}

	WriteableBitmap^ bitmap = ref new WriteableBitmap(this->width, this->height);

	unsigned int length;
	uint8_t* pixels = GetPointerToPixelData(bitmap->PixelBuffer, &length);

	config.options.no_fancy_upsampling = 1;
	config.output.colorspace = MODE_bgrA;
	config.output.is_external_memory = 1;
	config.output.u.RGBA.rgba = pixels;
	config.output.u.RGBA.stride = this->width * 4;
	config.output.u.RGBA.size = (this->width * 4) * this->height;

	ret = WebPDecode(pPayload, payloadSize, &config);

	if (ret != VP8_STATUS_OK)
	{
		throw ref new FailureException(ref new String(L"Failed to decode frame"));
	}

	return bitmap;
}

uint8_t* WebPFrame::GetPointerToPixelData(IBuffer^ pixelBuffer, unsigned int *length)
{
	if (length != nullptr)
	{
		*length = pixelBuffer->Length;
	}

	ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(pixelBuffer)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));

	byte* pixels = nullptr;
	bufferByteAccess->Buffer(&pixels);
	return pixels;
}