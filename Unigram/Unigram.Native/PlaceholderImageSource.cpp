#include "pch.h"
#include <ShCore.h>
#include "PlaceholderImageSource.h"

using namespace D2D1;
using namespace Windows::Storage;
using namespace Unigram::Native;

void PlaceholderImageSource::Draw(Color clear, Platform::String^ text, IRandomAccessStream^ randomAccessStream)
{
	static const auto instance = ref new PlaceholderImageSource(192, 192);
	ThrowIfFailed(instance->InternalDraw(clear, text, randomAccessStream));
}

HRESULT PlaceholderImageSource::InternalDraw(Color clear, Platform::String^ text, IRandomAccessStream^ randomAccessStream)
{
	auto lock = m_criticalSection.Lock();

	HRESULT result;
	DWRITE_TEXT_METRICS textMetrics;
	ReturnIfFailed(result, MeasureText(text, &textMetrics));

	//ID2D1LinearGradientBrush *m_pLinearGradientBrush;
	//ID2D1GradientStopCollection *pGradientStops = NULL;

	//D2D1_GRADIENT_STOP gradientStops[2];
	//gradientStops[0].color = D2D1::ColorF(clear.R / 255.0f, clear.G / 255.0f, clear.B / 255.0f, clear.A / 255.0f);
	//gradientStops[0].position = 0.0f;
	//gradientStops[1].color = D2D1::ColorF(end.R / 255.0f, end.G / 255.0f, end.B / 255.0f, end.A / 255.0f);
	//gradientStops[1].position = 1.0f;
	//ThrowIfFailed(m_d2dContext->CreateGradientStopCollection(
	//	gradientStops,
	//	2,
	//	D2D1_GAMMA_2_2,
	//	D2D1_EXTEND_MODE_CLAMP,
	//	&pGradientStops)
	//);

	//ThrowIfFailed(m_d2dContext->CreateLinearGradientBrush(
	//	D2D1::LinearGradientBrushProperties(
	//		D2D1::Point2F(0, 0),
	//		D2D1::Point2F(0, 192)),
	//	pGradientStops,
	//	&m_pLinearGradientBrush)
	//);

	//m_d2dContext->FillRectangle(D2D1::RectF(0, 0, 192, 192), m_pLinearGradientBrush);

	m_d2dContext->BeginDraw();
	m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());
	m_d2dContext->Clear(D2D1::ColorF(clear.R / 255.0f, clear.G / 255.0f, clear.B / 255.0f, clear.A / 255.0f));

	D2D1_RECT_F layoutRect = { (192.0f - textMetrics.width) / 2.0f, (180.0f - textMetrics.height) / 2.0f, m_renderTargetSize.width, m_renderTargetSize.height };
	m_d2dContext->DrawText(text->Data(), text->Length(), m_textFormat.Get(), &layoutRect, m_textBrush.Get());

	if ((result = m_d2dContext->EndDraw()) == D2DERR_RECREATE_TARGET)
	{
		ReturnIfFailed(result, CreateDeviceResources());
		return InternalDraw(clear, text, randomAccessStream);
	}

	ComPtr<IStream> stream;
	ReturnIfFailed(result, CreateStreamOverRandomAccessStream(randomAccessStream, IID_PPV_ARGS(&stream)));

	return SaveBitmapToStream(GUID_ContainerFormatPng, stream.Get());
}

PlaceholderImageSource::PlaceholderImageSource(int width, int height) :
	m_renderTargetSize({ static_cast<FLOAT>(width), static_cast<FLOAT>(height) })
{
	ThrowIfFailed(CreateDeviceIndependentResources());
	ThrowIfFailed(CreateDeviceResources());
}

HRESULT PlaceholderImageSource::CreateDeviceIndependentResources()
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

HRESULT PlaceholderImageSource::CreateDeviceResources()
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
		creationFlags,							// optionally set debug and Direct2D compatibility flags
		featureLevels,							// list of feature levels this app can support
		ARRAYSIZE(featureLevels),				// number of possible feature levels
		D3D11_SDK_VERSION,
		&device,								// returns the Direct3D device created
		&m_featureLevel,						// returns feature level of device created
		&context								// returns the device immediate context
	));

	ComPtr<IDXGIDevice> dxgiDevice;
	ReturnIfFailed(result, device.As(&dxgiDevice));
	ReturnIfFailed(result, m_d2dFactory->CreateDevice(dxgiDevice.Get(), &m_d2dDevice));
	ReturnIfFailed(result, m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &m_d2dContext));
	ReturnIfFailed(result, m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White), &m_textBrush));

	D2D1_SIZE_U size = { static_cast<UINT32>(m_renderTargetSize.width), static_cast<UINT32>(m_renderTargetSize.height) };
	D2D1_BITMAP_PROPERTIES1 properties = { { DXGI_FORMAT_R8G8B8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED }, 96, 96, D2D1_BITMAP_OPTIONS_TARGET, 0 };
	ReturnIfFailed(result, m_d2dContext->CreateBitmap(size, nullptr, 0, &properties, &m_targetBitmap));

	m_d2dContext->SetTarget(m_targetBitmap.Get());

	return m_wicFactory->CreateImageEncoder(m_d2dDevice.Get(), &m_imageEncoder);
}

HRESULT PlaceholderImageSource::MeasureText(Platform::String^ text, DWRITE_TEXT_METRICS* textMetrics)
{
	HRESULT result;
	ComPtr<IDWriteTextLayout> textLayout;
	ReturnIfFailed(result, m_dwriteFactory->CreateTextLayout(
		text->Data(),							// The string to be laid out and formatted.
		text->Length(),							// The length of the string.
		m_textFormat.Get(),						// The text format to apply to the string (contains font information, etc).
		m_renderTargetSize.width,				// The width of the layout box.
		m_renderTargetSize.height,				// The height of the layout box.
		&textLayout								// The IDWriteTextLayout interface pointer.
	));

	return textLayout->GetMetrics(textMetrics);
}


HRESULT PlaceholderImageSource::SaveBitmapToStream(REFGUID wicFormat, IStream* stream)
{
	HRESULT result;
	ComPtr<IWICBitmapEncoder> wicBitmapEncoder;
	ReturnIfFailed(result, m_wicFactory->CreateEncoder(wicFormat, nullptr, &wicBitmapEncoder));
	ReturnIfFailed(result, wicBitmapEncoder->Initialize(stream, WICBitmapEncoderNoCache));

	ComPtr<IWICBitmapFrameEncode> wicFrameEncode;
	ReturnIfFailed(result, wicBitmapEncoder->CreateNewFrame(&wicFrameEncode, nullptr));
	ReturnIfFailed(result, wicFrameEncode->Initialize(nullptr));

	WICImageParameters params = { m_targetBitmap->GetPixelFormat(), 96.0f, 96.0f, 0.0f, 0.0f, static_cast<UINT32>(m_renderTargetSize.width), static_cast<UINT32>(m_renderTargetSize.height) };
	ReturnIfFailed(result, m_imageEncoder->WriteFrame(m_targetBitmap.Get(), wicFrameEncode.Get(), &params));
	ReturnIfFailed(result, wicFrameEncode->Commit());
	ReturnIfFailed(result, wicBitmapEncoder->Commit());

	return stream->Commit(STGC_DEFAULT);
}
