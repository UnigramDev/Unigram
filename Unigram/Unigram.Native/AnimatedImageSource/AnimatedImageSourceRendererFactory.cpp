// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "MediaFoundationExtensions.h"
#include "AnimatedImageSourceRendererFactory.h"
#include "Helpers\COMHelper.h"

using namespace Unigram::Native;
using Windows::UI::Xaml::Media::CompositionTarget;


AnimatedImageSourceRendererFactory::AnimatedImageSourceRendererFactory()
{
	MFStartup(MF_VERSION, MFSTARTUP_LITE);

	m_eventTokens[0] = Application::Current->Suspending += ref new SuspendingEventHandler(this, &AnimatedImageSourceRendererFactory::OnSuspending);
	m_eventTokens[1] = CompositionTarget::SurfaceContentsLost += ref new EventHandler<Object^>(this, &AnimatedImageSourceRendererFactory::OnSurfaceContentLost);

	ComPtr<IMFActivate> activate;
	ThrowIfFailed(MFCreateMediaExtensionActivate(L"Unigram.Native.GIFByteStreamHandler", nullptr, IID_PPV_ARGS(&activate)));
	ThrowIfFailed(MFRegisterLocalByteStreamHandler(L".gif", L"image/gif", activate.Get()));

	ThrowIfFailed(CreateDeviceResources());
}

AnimatedImageSourceRendererFactory::~AnimatedImageSourceRendererFactory()
{
	MFShutdown();

	Application::Current->Suspending -= m_eventTokens[0];
	CompositionTarget::SurfaceContentsLost -= m_eventTokens[1];
}

void AnimatedImageSourceRendererFactory::OnSuspending(Object^ sender, SuspendingEventArgs^ args)
{
	ComPtr<IDXGIDevice3> dxgiDevice;
	ThrowIfFailed(m_d3dDevice.As(&dxgiDevice));

	dxgiDevice->Trim();
}

void AnimatedImageSourceRendererFactory::OnSurfaceContentLost(Object^ sender, Object^ args)
{
	ThrowIfFailed(CreateDeviceResources());
	SurfaceContentLost(this, args);
}

HRESULT AnimatedImageSourceRendererFactory::NotifyDeviceContentLost()
{
	HRESULT result;
	ReturnIfFailed(result, CreateDeviceResources());

	try
	{
		SurfaceContentLost(this, nullptr);
	}
	catch (Exception^ ex)
	{
		return ex->HResult;
	}

	return DXGI_ERROR_DEVICE_RESET;
}

HRESULT AnimatedImageSourceRendererFactory::CreateDeviceResources()
{
	HRESULT result;
	auto lock = m_criticalSection.Lock();

	UINT creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
	D2D1_DEBUG_LEVEL debugLevel = D2D1_DEBUG_LEVEL_NONE;

	/*#ifdef _DEBUG
			creationFlags |= D3D11_CREATE_DEVICE_DEBUG;
			debugLevel = D2D1_DEBUG_LEVEL_ERROR;
	#endif */

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

	ReturnIfFailed(result, D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, creationFlags, featureLevels,
		ARRAYSIZE(featureLevels), D3D11_SDK_VERSION, &m_d3dDevice, nullptr, &m_d3dDeviceContext));

	ComPtr<IDXGIDevice> dxgiDevice;
	ReturnIfFailed(result, m_d3dDevice.As(&dxgiDevice));

	ComPtr<IDXGIDevice1> dxgiDevice1;
	ReturnIfFailed(result, m_d3dDevice.As(&dxgiDevice1));
	ReturnIfFailed(result, dxgiDevice1->SetMaximumFrameLatency(1));

	D2D1_CREATION_PROPERTIES deviceProperties = { D2D1_THREADING_MODE_MULTI_THREADED, debugLevel, D2D1_DEVICE_CONTEXT_OPTIONS_ENABLE_MULTITHREADED_OPTIMIZATIONS };
	ReturnIfFailed(result, D2D1CreateDevice(dxgiDevice.Get(), &deviceProperties, &m_d2dDevice));

	ReturnIfFailed(result, m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_ENABLE_MULTITHREADED_OPTIMIZATIONS, &m_d2dDeviceContext));

	m_d2dDeviceContext->SetUnitMode(D2D1_UNIT_MODE_DIPS);
	m_d2dDeviceContext->SetDpi(96.0f, 96.0f);
	m_d2dDeviceContext->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);

	return S_OK;
}

HRESULT AnimatedImageSourceRendererFactory::DrawFrame(IVirtualSurfaceImageSourceNative* imageSourceNative,
	RECT const& drawingBounds, ID2D1Bitmap* frameBitmap)
{
	POINT offset;
	HRESULT result;
	ComPtr<IDXGISurface> surface;
	if (SUCCEEDED(result = imageSourceNative->BeginDraw(drawingBounds, &surface, &offset)))
	{
		auto lock = m_criticalSection.Lock();

		D2D1_BITMAP_PROPERTIES1 bitmapProperties = { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED , 96.0f, 96.0f ,
			D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW };

		ComPtr<ID2D1Bitmap1> bitmap;
		ReturnIfFailed(result, m_d2dDeviceContext->CreateBitmapFromDxgiSurface(surface.Get(), &bitmapProperties, &bitmap));

		m_d2dDeviceContext->BeginDraw();
		m_d2dDeviceContext->SetTarget(bitmap.Get());
		m_d2dDeviceContext->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<float>(offset.x - drawingBounds.left),
			static_cast<float>(offset.y - drawingBounds.top)));

		m_d2dDeviceContext->Clear(D2D1::ColorF(D2D1::ColorF::Black, 0.0f));

		if (frameBitmap != nullptr)
		{
			m_d2dDeviceContext->DrawBitmap(frameBitmap);
		}

		m_d2dDeviceContext->SetTransform(D2D1::IdentityMatrix());
		m_d2dDeviceContext->SetTarget(nullptr);

		ReturnIfFailed(result, m_d2dDeviceContext->EndDraw());
		ReturnIfFailed(result, imageSourceNative->EndDraw());
	}
	else if (result == DXGI_ERROR_DEVICE_REMOVED || result == DXGI_ERROR_DEVICE_RESET)
	{
		ReturnIfFailed(result, CreateDeviceResources());

		return DrawFrame(imageSourceNative, drawingBounds, frameBitmap);
	}

	return result;
}

AnimatedImageSourceRenderer^ AnimatedImageSourceRendererFactory::CreateRenderer(int maximumWidth, int maximumHeight)
{
	if (maximumWidth < 0)
	{
		throw ref new OutOfBoundsException(L"The parameter maximumWidth must be greater or equal to 0");
	}

	if (maximumWidth < 0)
	{
		throw ref new OutOfBoundsException(L"The parameter maximumWidth must be greater or equal to 0");
	}

	return ref new AnimatedImageSourceRenderer(maximumWidth, maximumHeight, this);
}