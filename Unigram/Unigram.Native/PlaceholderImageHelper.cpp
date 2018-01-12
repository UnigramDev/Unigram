#include "pch.h"
#include <ShCore.h>
#include "PlaceholderImageHelper.h"

using namespace D2D1;
using namespace Windows::Storage;
using namespace Unigram::Native;

PlaceholderImageHelper^ PlaceholderImageHelper::Instance::get()
{
	static const auto instance = ref new PlaceholderImageHelper();
	return instance;
}

void PlaceholderImageHelper::DrawProfilePlaceholder(Color clear, Platform::String^ text, IRandomAccessStream^ randomAccessStream)
{
	ThrowIfFailed(Instance->InternalDrawProfilePlaceholder(clear, text, randomAccessStream));
}

void PlaceholderImageHelper::DrawThumbnailPlaceholder(Platform::String^ fileName, float blurAmount, IRandomAccessStream^ randomAccessStream)
{
	ThrowIfFailed(Instance->InternalDrawThumbnailPlaceholder(fileName, blurAmount, randomAccessStream));
}

HRESULT PlaceholderImageHelper::InternalDrawProfilePlaceholder(Color clear, Platform::String^ text, IRandomAccessStream^ randomAccessStream)
{
	auto lock = m_criticalSection.Lock();

	HRESULT result;
	DWRITE_TEXT_METRICS textMetrics;
	ReturnIfFailed(result, MeasureText(text, &textMetrics));

	m_d2dContext->SetTarget(m_targetBitmap.Get());
	m_d2dContext->BeginDraw();
	//m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
	m_d2dContext->Clear(D2D1::ColorF(clear.R / 255.0f, clear.G / 255.0f, clear.B / 255.0f, clear.A / 255.0f));

	D2D1_RECT_F layoutRect = { (192.0f - textMetrics.width) / 2.0f, (180.0f - textMetrics.height) / 2.0f, 192.0f, 192.0f };
	m_d2dContext->DrawText(text->Data(), text->Length(), m_textFormat.Get(), &layoutRect, m_textBrush.Get());

	if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
	{
		ReturnIfFailed(result, CreateDeviceResources());
		return InternalDrawProfilePlaceholder(clear, text, randomAccessStream);
	}

	return SaveImageToStream(m_targetBitmap.Get(), GUID_ContainerFormatPng, randomAccessStream);
}

HRESULT PlaceholderImageHelper::InternalDrawThumbnailPlaceholder(Platform::String^ fileName, float blurAmount, IRandomAccessStream^ randomAccessStream)
{
	auto lock = m_criticalSection.Lock();

	HRESULT result;
	ComPtr<IWICBitmapDecoder> wicBitmapDecoder;
	ReturnIfFailed(result, m_wicFactory->CreateDecoderFromFilename(fileName->Data(), nullptr, GENERIC_READ, WICDecodeMetadataCacheOnLoad, &wicBitmapDecoder));

	ComPtr<IWICBitmapFrameDecode> wicFrameDecode;
	ReturnIfFailed(result, wicBitmapDecoder->GetFrame(0, &wicFrameDecode));

	ComPtr<IWICFormatConverter> wicFormatConverter;
	ReturnIfFailed(result, m_wicFactory->CreateFormatConverter(&wicFormatConverter));
	ReturnIfFailed(result, wicFormatConverter->Initialize(wicFrameDecode.Get(), GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherTypeNone, nullptr, 0.f, WICBitmapPaletteTypeCustom));

	return InternalDrawThumbnailPlaceholder(wicFormatConverter.Get(), blurAmount, randomAccessStream);
}

HRESULT PlaceholderImageHelper::InternalDrawThumbnailPlaceholder(IWICBitmapSource* wicBitmapSource, float blurAmount, IRandomAccessStream^ randomAccessStream)
{
	HRESULT result;
	ComPtr<ID2D1ImageSourceFromWic> imageSource;
	ReturnIfFailed(result, m_d2dContext->CreateImageSourceFromWic(wicBitmapSource, &imageSource));

	D2D1_SIZE_U size;
	ReturnIfFailed(result, wicBitmapSource->GetSize(&size.width, &size.height));

	ComPtr<ID2D1Bitmap1> targetBitmap;
	D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8G8B8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };
	ReturnIfFailed(result, m_d2dContext->CreateBitmap(size, nullptr, 0, &properties, &targetBitmap));

	ReturnIfFailed(result, m_gaussianBlurEffect->SetValue(D2D1_GAUSSIANBLUR_PROP_STANDARD_DEVIATION, blurAmount));

	m_gaussianBlurEffect->SetInput(0, imageSource.Get());

	m_d2dContext->SetTarget(targetBitmap.Get());
	m_d2dContext->BeginDraw();
	//m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
	m_d2dContext->Clear(D2D1::ColorF(ColorF::Black, 0.0f));
	m_d2dContext->DrawImage(m_gaussianBlurEffect.Get());

	if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
	{
		ReturnIfFailed(result, CreateDeviceResources());
		return InternalDrawThumbnailPlaceholder(wicBitmapSource, blurAmount, randomAccessStream);
	}

	return SaveImageToStream(targetBitmap.Get(), GUID_ContainerFormatPng, randomAccessStream);
}

PlaceholderImageHelper::PlaceholderImageHelper()
{
	ThrowIfFailed(CreateDeviceIndependentResources());
	ThrowIfFailed(CreateDeviceResources());
}

HRESULT PlaceholderImageHelper::CreateDeviceIndependentResources()
{
	HRESULT result;
	D2D1_FACTORY_OPTIONS options = {};
	ReturnIfFailed(result, D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof(ID2D1Factory1), &options, &m_d2dFactory));
	ReturnIfFailed(result, CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&m_wicFactory)));
	ReturnIfFailed(result, DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), &m_dwriteFactory));

	ReturnIfFailed(result, m_dwriteFactory->CreateTextFormat(
		L"Segoe UI",							// font family name
		nullptr,								// system font collection
		DWRITE_FONT_WEIGHT_NORMAL,				// font weight 
		DWRITE_FONT_STYLE_NORMAL,				// font style
		DWRITE_FONT_STRETCH_NORMAL,				// default font stretch
		82.0f,									// font size
		L"",									// locale name
		&m_textFormat
	));
	ReturnIfFailed(result, m_textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING));

	return m_textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR);
}

HRESULT PlaceholderImageHelper::CreateDeviceResources()
{
	HRESULT result;
	UINT creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

	D3D_FEATURE_LEVEL featureLevels[] =
	{
		D3D_FEATURE_LEVEL_11_1,
		D3D_FEATURE_LEVEL_11_0,
		D3D_FEATURE_LEVEL_10_1,
		D3D_FEATURE_LEVEL_10_0,
		D3D_FEATURE_LEVEL_9_3,
		D3D_FEATURE_LEVEL_9_2,
		D3D_FEATURE_LEVEL_9_1
	};

	ComPtr<ID3D11Device> device;
	ComPtr<ID3D11DeviceContext> context;
	ReturnIfFailed(result, D3D11CreateDevice(nullptr,	// specify null to use the default adapter
		D3D_DRIVER_TYPE_HARDWARE, 0,
		creationFlags,									// optionally set debug and Direct2D compatibility flags
		featureLevels,									// list of feature levels this app can support
		ARRAYSIZE(featureLevels),						// number of possible feature levels
		D3D11_SDK_VERSION,
		&device,										// returns the Direct3D device created
		&m_featureLevel,								// returns feature level of device created
		&context										// returns the device immediate context
	));

	ComPtr<IDXGIDevice> dxgiDevice;
	ReturnIfFailed(result, device.As(&dxgiDevice));
	ReturnIfFailed(result, m_d2dFactory->CreateDevice(dxgiDevice.Get(), &m_d2dDevice));

	ComPtr<ID2D1DeviceContext> d2dContext;
	ReturnIfFailed(result, m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &d2dContext));
	ReturnIfFailed(result, d2dContext.As(&m_d2dContext));

	ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White), &m_textBrush));
	ReturnIfFailed(result, m_d2dContext->CreateEffect(CLSID_D2D1GaussianBlur, &m_gaussianBlurEffect));
	ReturnIfFailed(result, m_gaussianBlurEffect->SetValue(D2D1_GAUSSIANBLUR_PROP_BORDER_MODE, D2D1_BORDER_MODE_HARD));

	D2D1_SIZE_U size = { 192, 192 };
	D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8G8B8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };
	ReturnIfFailed(result, m_d2dContext->CreateBitmap(size, nullptr, 0, &properties, &m_targetBitmap));

	return m_wicFactory->CreateImageEncoder(m_d2dDevice.Get(), &m_imageEncoder);
}

HRESULT PlaceholderImageHelper::MeasureText(Platform::String^ text, DWRITE_TEXT_METRICS* textMetrics)
{
	HRESULT result;
	ComPtr<IDWriteTextLayout> textLayout;
	ReturnIfFailed(result, m_dwriteFactory->CreateTextLayout(
		text->Data(),							// The string to be laid out and formatted.
		text->Length(),							// The length of the string.
		m_textFormat.Get(),						// The text format to apply to the string (contains font information, etc).
		192.0f,									// The width of the layout box.
		192.0f,									// The height of the layout box.
		&textLayout								// The IDWriteTextLayout interface pointer.
	));

	return textLayout->GetMetrics(textMetrics);
}

HRESULT PlaceholderImageHelper::SaveImageToStream(ID2D1Image* image, REFGUID wicFormat, IRandomAccessStream^ randomAccessStream)
{
	HRESULT result;
	ComPtr<IStream> stream;
	ReturnIfFailed(result, CreateStreamOverRandomAccessStream(randomAccessStream, IID_PPV_ARGS(&stream)));

	ComPtr<IWICBitmapEncoder> wicBitmapEncoder;
	ReturnIfFailed(result, m_wicFactory->CreateEncoder(wicFormat, nullptr, &wicBitmapEncoder));
	ReturnIfFailed(result, wicBitmapEncoder->Initialize(stream.Get(), WICBitmapEncoderNoCache));

	ComPtr<IWICBitmapFrameEncode> wicFrameEncode;
	ReturnIfFailed(result, wicBitmapEncoder->CreateNewFrame(&wicFrameEncode, nullptr));
	ReturnIfFailed(result, wicFrameEncode->Initialize(nullptr));

	ReturnIfFailed(result, m_imageEncoder->WriteFrame(image, wicFrameEncode.Get(), nullptr));
	ReturnIfFailed(result, wicFrameEncode->Commit());
	ReturnIfFailed(result, wicBitmapEncoder->Commit());

	ReturnIfFailed(result, stream->Commit(STGC_DEFAULT));

	return stream->Seek({ 0 }, STREAM_SEEK_SET, nullptr);
}
