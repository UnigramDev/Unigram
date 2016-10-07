#include "pch.h"
#include "VideoImageSourceRendererFactory.h"

using namespace Unigram::Native;
using Windows::UI::Xaml::Media::CompositionTarget;

VideoImageSourceRendererFactory::VideoImageSourceRendererFactory()
{
	MFStartup(MF_VERSION, MFSTARTUP_LITE);

	m_eventTokens[0] = Application::Current->Suspending += ref new SuspendingEventHandler(this, &VideoImageSourceRendererFactory::OnSuspending);
	m_eventTokens[1] = CompositionTarget::SurfaceContentsLost += ref new EventHandler<Object^>(this, &VideoImageSourceRendererFactory::OnSurfaceContentLost);

	ThrowIfFailed(CreateDeviceResources());
}

VideoImageSourceRendererFactory::~VideoImageSourceRendererFactory()
{
	MFShutdown();

	Application::Current->Suspending -= m_eventTokens[0];
	CompositionTarget::SurfaceContentsLost -= m_eventTokens[1];
}

void VideoImageSourceRendererFactory::OnSuspending(Object^ sender, SuspendingEventArgs^ args)
{
	ComPtr<IDXGIDevice3> dxgiDevice;
	m_d3dDevice.As(&dxgiDevice);
	dxgiDevice->Trim();
}

void VideoImageSourceRendererFactory::OnSurfaceContentLost(Object^ sender, Object^ args)
{
	ThrowIfFailed(CreateDeviceResources());
	SurfaceContentLost(this, args);
}

HRESULT VideoImageSourceRendererFactory::NotifyDeviceContentLost()
{
	HRESULT result;
	ReturnIfFailed(result, CreateDeviceResources());
	SurfaceContentLost(this, nullptr);

	return DXGI_ERROR_DEVICE_RESET;
}

HRESULT VideoImageSourceRendererFactory::CreateDeviceResources()
{
	HRESULT result;
	auto lock = m_criticalSection.Lock();

	UINT creationFlags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;

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
		ARRAYSIZE(featureLevels), D3D11_SDK_VERSION, &m_d3dDevice, nullptr, nullptr));

	ComPtr<IDXGIDevice> dxgiDevice;
	ReturnIfFailed(result, m_d3dDevice.As(&dxgiDevice));

	D2D1_CREATION_PROPERTIES properties = { D2D1_THREADING_MODE_MULTI_THREADED, D2D1_DEBUG_LEVEL_NONE,
		D2D1_DEVICE_CONTEXT_OPTIONS_ENABLE_MULTITHREADED_OPTIMIZATIONS };
	ReturnIfFailed(result, D2D1CreateDevice(dxgiDevice.Get(), &properties, &m_d2dDevice));

	ReturnIfFailed(result, m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_ENABLE_MULTITHREADED_OPTIMIZATIONS, &m_d2dDeviceContext));

	m_d2dDeviceContext->SetUnitMode(D2D1_UNIT_MODE_DIPS);
	m_d2dDeviceContext->SetDpi(96.0f, 96.0f);
	m_d2dDeviceContext->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);

	return S_OK;
}


VideoImageSourceRenderer^ VideoImageSourceRendererFactory::CreateRenderer(int maximumWidth, int maximumHeight)
{
	if (maximumWidth < 0)
		throw ref new OutOfBoundsException(L"The parameter maximumWidth must be greater or equal to 0");

	if (maximumWidth < 0)
		throw ref new OutOfBoundsException(L"The parameter maximumWidth must be greater or equal to 0");

	return ref new VideoImageSourceRenderer(maximumWidth, maximumHeight, this);
}