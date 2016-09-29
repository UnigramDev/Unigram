#include "pch.h"
#include "VideoImageSourceRenderer.h"
#include <propvarutil.h>

using namespace Unigram::Native;
using namespace Platform;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage::Streams;
using namespace Microsoft::WRL;
using namespace Windows::ApplicationModel;
using Windows::Foundation::TypedEventHandler;

VideoImageSourceRenderer::VideoImageSourceRenderer(int width, int height) :
	m_width(width),
	m_height(height),
	m_timer(ref new DispatcherTimer())
{
	auto application = Application::Current;
	m_eventTokens[0] = application->Suspending += ref new SuspendingEventHandler(this, &VideoImageSourceRenderer::OnSuspending);
	m_eventTokens[1] = m_timer->Tick += ref new Windows::Foundation::EventHandler<Platform::Object ^>(this, &VideoImageSourceRenderer::OnTick);

	//auto displayInformation = DisplayInformation::GetForCurrentView();
	//m_eventTokens[1] = displayInformation->DpiChanged += ref new TypedEventHandler<DisplayInformation^, Object^>(this, &VideoImageSourceRenderer::OnDpiChanged);
	//m_compositionScale.x = m_compositionScale.y = displayInformation->LogicalDpi / 96.0f;
	//m_compositionScale.x = m_compositionScale.y = 1.0f;

	m_updatesCallback = Make<VirtualImageSourceRendererCallback>(this);
	m_imageSource = ref new VirtualSurfaceImageSource(static_cast<int32>(m_width), static_cast<int32>(m_height), true);

	CreateDeviceIndependentResources();
	CreateDeviceResources();
}

VideoImageSourceRenderer::~VideoImageSourceRenderer()
{
	MFShutdown();

	Application::Current->Suspending -= m_eventTokens[0];
	//DisplayInformation::GetForCurrentView()->DpiChanged -= m_eventTokens[1];
}

void VideoImageSourceRenderer::OnSuspending(Object^ sender, SuspendingEventArgs^ args)
{
	ComPtr<IDXGIDevice3> dxgiDevice;
	m_d3dDevice.As(&dxgiDevice);
	dxgiDevice->Trim();
}

void VideoImageSourceRenderer::CreateDeviceResources()
{
	UINT creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

	//#if defined(_DEBUG)     
	//			creationFlags |= D3D11_CREATE_DEVICE_DEBUG;
	//#endif 

	const D3D_FEATURE_LEVEL featureLevels[] =
	{
		D3D_FEATURE_LEVEL_11_1,
		D3D_FEATURE_LEVEL_11_0,
		D3D_FEATURE_LEVEL_10_1,
		D3D_FEATURE_LEVEL_10_0,
		D3D_FEATURE_LEVEL_9_3,
		D3D_FEATURE_LEVEL_9_2,
		D3D_FEATURE_LEVEL_9_1,
	};

	ThrowIfFailed(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, creationFlags, featureLevels,
		ARRAYSIZE(featureLevels), D3D11_SDK_VERSION, &m_d3dDevice, nullptr, nullptr));

	ComPtr<IDXGIDevice> dxgiDevice;
	ThrowIfFailed(m_d3dDevice.As(&dxgiDevice));
	ThrowIfFailed(D2D1CreateDevice(dxgiDevice.Get(), nullptr, &m_d2dDevice));
	ThrowIfFailed(m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &m_d2dDeviceContext));

	m_d2dDeviceContext->SetUnitMode(D2D1_UNIT_MODE_DIPS);
	m_d2dDeviceContext->SetDpi(96.0f, 96.0f);
	m_d2dDeviceContext->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);

	ThrowIfFailed(reinterpret_cast<IUnknown*>(m_imageSource)->QueryInterface(IID_PPV_ARGS(&m_imageSourceNative)));
	ThrowIfFailed(m_imageSourceNative->SetDevice(dxgiDevice.Get()));
	ThrowIfFailed(m_imageSourceNative->RegisterForUpdatesNeeded(m_updatesCallback.Get()));

	//TODO: Create device dependent resources here
}

void VideoImageSourceRenderer::CreateDeviceIndependentResources()
{
	//TODO: Create device independent resources here
}

void VideoImageSourceRenderer::BeginDraw(RECT const& drawingBounds)
{
	POINT offset = {};
	ComPtr<IDXGISurface> surface;

	auto result = m_imageSourceNative->BeginDraw(drawingBounds, &surface, &offset);
	if (SUCCEEDED(result))
	{
		D2D1_BITMAP_PROPERTIES1 bitmapProperties = D2D1::BitmapProperties1(
			D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
			D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED),
			96.0f, 96.0f);

		ComPtr<ID2D1Bitmap1> bitmap;
		ThrowIfFailed(m_d2dDeviceContext->CreateBitmapFromDxgiSurface(surface.Get(), &bitmapProperties, &bitmap));

		m_d2dDeviceContext->BeginDraw();
		m_d2dDeviceContext->SetTarget(bitmap.Get());
		//m_d2dDeviceContext->PushAxisAlignedClip(D2D1::RectF(static_cast<float>(offset.x), static_cast<float>(offset.y),
		//	static_cast<float>(offset.x + (drawingBounds.right - drawingBounds.left)),
		//	std::ceil(m_stickerSize.height * static_cast<float>(offset.y + (drawingBounds.bottom - drawingBounds.top))) / (m_stickerSize.height)),
		//	D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);
		m_d2dDeviceContext->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<float>(offset.x - drawingBounds.left),
			static_cast<float>(offset.y - drawingBounds.top)));
		m_d2dDeviceContext->Clear(D2D1::ColorF(D2D1::ColorF::Black, 0.0f));
	}
	else if (result == DXGI_ERROR_DEVICE_REMOVED || result == DXGI_ERROR_DEVICE_RESET)
	{
		CreateDeviceResources();
		ThrowIfFailed(m_imageSourceNative->Invalidate(drawingBounds));
	}
	else
	{
		ThrowIfFailed(result);
	}
}

void VideoImageSourceRenderer::EndDraw()
{
	m_d2dDeviceContext->SetTransform(D2D1::IdentityMatrix());
	//m_d2dDeviceContext->PopAxisAlignedClip();
	m_d2dDeviceContext->SetTarget(nullptr);
	ThrowIfFailed(m_d2dDeviceContext->EndDraw());

	ThrowIfFailed(m_imageSourceNative->EndDraw());
}

void VideoImageSourceRenderer::Draw(RECT const& drawingBounds)
{
	BeginDraw(drawingBounds);

	DWORD pdwActualStreamIndex;
	DWORD pdwStreamFlags;
	LONGLONG pllTimestamp;
	ComPtr<IMFSample> ppSample;
	HRESULT hr = ppSourceReader->ReadSample(
		MF_SOURCE_READER_FIRST_VIDEO_STREAM,
		0,
		&pdwActualStreamIndex,
		&pdwStreamFlags,
		&pllTimestamp,
		&ppSample);

	if (ppSample == NULL)
	{
		PROPVARIANT var;
		PropVariantInit(&var);
		var.vt = VT_I8;
		var.hVal.QuadPart = 0;
		if (SUCCEEDED(hr))
		{
			hr = ppSourceReader->SetCurrentPosition(GUID_NULL, var);
			PropVariantClear(&var);
		}

		hr = ppSourceReader->ReadSample(
			MF_SOURCE_READER_FIRST_VIDEO_STREAM,
			0,
			&pdwActualStreamIndex,
			&pdwStreamFlags,
			&pllTimestamp,
			&ppSample);
	}

	ComPtr<IMFMediaBuffer> pBuffer;
	hr = ppSample->ConvertToContiguousBuffer(&pBuffer);

	BYTE *pData = NULL;
	hr = pBuffer->Lock(&pData, NULL, NULL);

	ComPtr<ID2D1Bitmap> bitmap;
	D2D1_BITMAP_PROPERTIES properties = { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_IGNORE, 96.0f, 96.0f };
	ThrowIfFailed(m_d2dDeviceContext->CreateBitmap(D2D1::SizeU(m_width, m_height), pData, m_width * 4, &properties, &bitmap));

	m_d2dDeviceContext->DrawBitmap(bitmap.Get());

	pBuffer->Unlock();

	EndDraw();
}

void VideoImageSourceRenderer::Invalidate(Boolean resize)
{
	if (m_imageSourceNative == nullptr)
		ThrowException(WS_E_INVALID_OPERATION);

	if (resize)
	{
		//m_renderSize.cx = static_cast<int32>(m_stickersPerRow * m_stickerSize.width);
		//m_renderSize.cy = static_cast<int32>(m_stickerSize.height * ceil(m_stickerDefinitions.size() / static_cast<float>(m_stickersPerRow)));

		//ThrowIfFailed(m_imageSourceNative->Resize(static_cast<int32>(m_renderSize.cx), static_cast<int32>(m_renderSize.cy)));

		//OnPropertyChanged(L"RenderSize");
	}

	RECT bounds = {};
	bounds.right = static_cast<int32>(m_width);
	bounds.bottom = static_cast<int32>(m_height);

	ThrowIfFailed(m_imageSourceNative->Invalidate(bounds));
}

void VideoImageSourceRenderer::NotifyUpdatesNeeded()
{
	ULONG drawingBoundsCount = 0;
	ThrowIfFailed(m_imageSourceNative->GetUpdateRectCount(&drawingBoundsCount));

	auto drawingBounds = std::make_unique<RECT[]>(drawingBoundsCount);
	ThrowIfFailed(m_imageSourceNative->GetUpdateRects(drawingBounds.get(), drawingBoundsCount));

	for (uint32 i = 0; i < drawingBoundsCount; i++)
	{
		Draw(drawingBounds[i]);
	}
}

VirtualSurfaceImageSource^ VideoImageSourceRenderer::ImageSource::get()
{
	return m_imageSource;
}

void VideoImageSourceRenderer::Initialize(String^ path)
{
	auto pathData = path->Data();

	MFStartup(MF_VERSION, MFSTARTUP_LITE);

	/*ComPtr<IMFSourceReader> ppSourceReader;*/
	HRESULT hr = MFCreateSourceReaderFromURL(pathData, NULL, &ppSourceReader);

	IMFMediaType *pNativeType = NULL;
	IMFMediaType *pType = NULL;

	hr = ppSourceReader->GetNativeMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, &pNativeType);
	hr = ConvertVideoTypeToUncompressedType(pNativeType, MFVideoFormat_RGB32, &pType);
	hr = ppSourceReader->SetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, NULL, pType);

	UINT32 pNumerator;
	UINT32 pDenominator;
	MFGetAttributeRatio(pNativeType, MF_MT_FRAME_RATE, &pNumerator, &pDenominator);

	float frameRate = static_cast<float>(pNumerator) / static_cast<float>(pDenominator);

	TimeSpan t;
	t.Duration = std::floor(frameRate);

	m_timer->Interval = t;
	m_timer->Start();

	//pBuffer->Release();
	//ppSample->Release();
	//ppSourceReader->Release();
}

void VideoImageSourceRenderer::OnTick(Platform::Object ^sender, Platform::Object ^args)
{
	RECT bounds;
	m_imageSourceNative->GetVisibleBounds(&bounds);
	if (bounds.right - bounds.left > 0 && bounds.bottom - bounds.top > 0) 
	{
		Invalidate(false);
	}
}

HRESULT VideoImageSourceRenderer::ConvertVideoTypeToUncompressedType(IMFMediaType *pType, const GUID& subtype, IMFMediaType **ppType)
{
	IMFMediaType *pTypeUncomp = NULL;

	HRESULT hr = S_OK;
	GUID majortype = { 0 };
	MFRatio par = { 0 };

	hr = pType->GetMajorType(&majortype);

	if (majortype != MFMediaType_Video)
	{
		return ERROR;
	}

	if (SUCCEEDED(hr))
	{
		hr = MFCreateMediaType(&pTypeUncomp);
	}

	if (SUCCEEDED(hr))
	{
		hr = pType->CopyAllItems(pTypeUncomp);
	}

	if (SUCCEEDED(hr))
	{
		hr = pTypeUncomp->SetGUID(MF_MT_SUBTYPE, subtype);
	}

	if (SUCCEEDED(hr))
	{
		hr = pTypeUncomp->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE);
	}

	if (SUCCEEDED(hr))
	{
		hr = MFGetAttributeRatio(
			pTypeUncomp,
			MF_MT_PIXEL_ASPECT_RATIO,
			(UINT32*)&par.Numerator,
			(UINT32*)&par.Denominator
		);

		if (FAILED(hr))
		{
			hr = MFSetAttributeRatio(
				pTypeUncomp,
				MF_MT_PIXEL_ASPECT_RATIO,
				1, 1
			);
		}
	}

	if (SUCCEEDED(hr))
	{
		*ppType = pTypeUncomp;
		(*ppType)->AddRef();
	}

	pTypeUncomp->Release();
	return hr;
}