// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include <algorithm>
#include <propvarutil.h>
#include "AnimatedImageSourceRendererFactory.h"
#include "AnimatedImageSourceRenderer.h"
#include "ReadFramesAsyncOperation.h"
#include "Helpers\COMHelper.h"

using namespace Unigram::Native;
using namespace Platform;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage::Streams;
using namespace Microsoft::WRL;
using Windows::ApplicationModel::Core::CoreApplication;
using Windows::Foundation::TypedEventHandler;


AnimatedImageSourceRenderer::AnimatedImageSourceRenderer(int maximumWidth, int maximumHeight, AnimatedImageSourceRendererFactory^ owner) :
	m_frameIndex(-1),
	m_size({}),
	m_maximumSize(D2D1::SizeU(static_cast<UINT32>(maximumWidth), static_cast<UINT32>(maximumHeight))),
	m_owner(owner)
{
	m_updatesCallback = Make<VirtualImageSourceRendererCallback>(this);

	auto application = Application::Current;
	m_eventTokens[0] = application->EnteredBackground += ref new EnteredBackgroundEventHandler(this, &AnimatedImageSourceRenderer::OnEnteredBackground);
	m_eventTokens[1] = application->LeavingBackground += ref new LeavingBackgroundEventHandler(this, &AnimatedImageSourceRenderer::OnLeavingBackground);
	m_eventTokens[2] = m_owner->SurfaceContentLost += ref new EventHandler<Object^>(this, &AnimatedImageSourceRenderer::OnSurfaceContentLost);

	ThrowIfFailed(InitializeImageSource());
}

AnimatedImageSourceRenderer::~AnimatedImageSourceRenderer()
{
	m_owner->SurfaceContentLost -= m_eventTokens[3];
}

HRESULT AnimatedImageSourceRenderer::Draw(RECT const& drawingBounds)
{
	auto lock = m_criticalSection.Lock();

	HRESULT result;
	result = m_owner->DrawFrame(m_imageSourceNative.Get(), drawingBounds, m_frameBitmap.Get());
	if (result == E_SURFACE_CONTENTS_LOST)
	{
		ReturnIfFailed(result, InitializeImageSource());
		return OnTimerTick();
	}

	return result;
}

HRESULT AnimatedImageSourceRenderer::Invalidate(bool resize)
{
	if (m_imageSourceNative == nullptr)
	{
		return WS_E_INVALID_OPERATION;
	}

	if (resize)
	{
		HRESULT result;
		ReturnIfFailed(result, m_imageSourceNative->Resize(m_size.width, m_size.height));
	}

	return m_imageSourceNative->Invalidate({ 0, 0, static_cast<LONG>(m_size.width), static_cast<LONG>(m_size.height) });
}

void AnimatedImageSourceRenderer::NotifyPropertyChanged(String^ propertyName)
{
	PropertyChanged(this, ref new PropertyChangedEventArgs(propertyName));
}

HRESULT AnimatedImageSourceRenderer::OnUpdatesNeeded()
{
	auto lock = m_criticalSection.Lock();

	HRESULT result;
	ULONG drawingBoundsCount;
	ReturnIfFailed(result, m_imageSourceNative->GetUpdateRectCount(&drawingBoundsCount));

	auto drawingBounds = std::vector<RECT>(drawingBoundsCount);
	ReturnIfFailed(result, m_imageSourceNative->GetUpdateRects(drawingBounds.data(), drawingBoundsCount));

	for (ULONG i = 0; i < drawingBoundsCount; i++)
	{
		ReturnIfFailed(result, Draw(drawingBounds[i]));
	}

	if (!m_updatesCallback->IsTimerRunning())
	{
		return OnTimerTick();
	}

	return S_OK;
}

HRESULT AnimatedImageSourceRenderer::OnTimerTick()
{
	auto lock = m_criticalSection.Lock();

	if (m_framesCacheStore == nullptr)
		return S_OK;

	auto frameCount = m_framesCacheStore->GetFrameCount();
	if (frameCount > 0)
	{
		RECT bounds;
		if (SUCCEEDED(m_imageSourceNative->GetVisibleBounds(&bounds)) && bounds.right > bounds.left && bounds.bottom > bounds.top)
		{
			HRESULT result;
			LONGLONG delay;
			ReturnIfFailed(result, m_framesCacheStore->ReadBitmapEntry(m_frameIndex, m_frameBitmap.Get(), &delay));
			ReturnIfFailed(result, Draw(bounds));

			m_frameIndex = (m_frameIndex + 1) % frameCount;
			return m_updatesCallback->StartTimer(delay);
		}
		else
		{
			return m_updatesCallback->StopTimer();
		}
	}

	return S_OK;
}

HRESULT AnimatedImageSourceRenderer::InitializeImageSource()
{
	m_imageSource = ref new VirtualSurfaceImageSource(m_size.width, m_size.height, false);

	HRESULT result;
	ComPtr<IDXGIDevice> dxgiDevice;
	ReturnIfFailed(result, m_owner->GetD3DDevice()->QueryInterface(IID_PPV_ARGS(&dxgiDevice)));
	ReturnIfFailed(result, reinterpret_cast<IUnknown*>(m_imageSource)->QueryInterface(IID_PPV_ARGS(&m_imageSourceNative)));
	ReturnIfFailed(result, m_imageSourceNative->SetDevice(dxgiDevice.Get()));
	ReturnIfFailed(result, m_imageSourceNative->RegisterForUpdatesNeeded(m_updatesCallback.Get()));

	NotifyPropertyChanged(L"ImageSource");

	return S_OK;
}

VirtualSurfaceImageSource^ AnimatedImageSourceRenderer::ImageSource::get()
{
	return m_imageSource;
}

void AnimatedImageSourceRenderer::OnSurfaceContentLost(Object^ sender, Object^ args)
{
	auto lock = m_criticalSection.Lock();

	D2D1_BITMAP_PROPERTIES properties = { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED, 96.0f, 96.0f };
	ThrowIfFailed(m_owner->GetD2DDeviceContext()->CreateBitmap(m_size, nullptr, 0, &properties, &m_frameBitmap));

	ThrowIfFailed(InitializeImageSource());
	ThrowIfFailed(OnTimerTick());
}

void AnimatedImageSourceRenderer::OnEnteredBackground(Object^ sender, EnteredBackgroundEventArgs^ args)
{
	auto lock = m_criticalSection.Lock();

	m_cancellationTokenSource.cancel();
	m_updatesCallback->StopTimer();
}

void AnimatedImageSourceRenderer::OnLeavingBackground(Object^ sender, LeavingBackgroundEventArgs^ args)
{
	auto lock = m_criticalSection.Lock();
	ThrowIfFailed(InitializeImageSource());
	ThrowIfFailed(OnTimerTick());
}

IAsyncAction^ AnimatedImageSourceRenderer::SetSourceAsync(Windows::Foundation::Uri^ uri)
{
	return create_async([=]
	{
		auto lock = m_criticalSection.Lock();

		if (uri == nullptr)
		{
			return Reset();
		}
		else
		{
			HRESULT result;
			ComPtr<ReadFramesAsyncOperation> asyncOperation;
			if (FAILED(result = MakeAndInitialize<ReadFramesAsyncOperation>(&asyncOperation, m_maximumSize, uri)))
			{
				return task_from_exception<void>(Exception::CreateException(result));
			}

			return Initialize(asyncOperation);
		}
	});
}

IAsyncAction^ AnimatedImageSourceRenderer::SetSourceAsync(Windows::Storage::Streams::IRandomAccessStream^ stream)
{
	return create_async([=]
	{
		auto lock = m_criticalSection.Lock();

		if (stream == nullptr)
		{
			return Reset();
		}
		else
		{
			HRESULT result;
			ComPtr<ReadFramesAsyncOperation> asyncOperation;
			if (FAILED(result = MakeAndInitialize<ReadFramesAsyncOperation>(&asyncOperation, m_maximumSize, stream)))
				return task_from_exception<void>(Exception::CreateException(result));

			return Initialize(asyncOperation);
		}
	});
}

IAsyncAction^ AnimatedImageSourceRenderer::SetSourceAsync(Windows::Media::Core::IMediaSource^ mediaSource)
{
	return create_async([=]
	{
		auto lock = m_criticalSection.Lock();

		if (mediaSource == nullptr)
		{
			return Reset();
		}
		else
		{
			HRESULT result;
			ComPtr<ReadFramesAsyncOperation> asyncOperation;
			if (FAILED(result = MakeAndInitialize<ReadFramesAsyncOperation>(&asyncOperation, m_maximumSize, mediaSource)))
				return task_from_exception<void>(Exception::CreateException(result));

			return Initialize(asyncOperation);
		}
	});
}

task<void> AnimatedImageSourceRenderer::Reset()
{
	auto lock = m_criticalSection.Lock();

	m_frameIndex = -1;
	m_size = {};

	m_cancellationTokenSource.cancel();
	m_frameBitmap.Reset();
	m_framesCacheStore.Reset();

	HRESULT result;

	do
	{
		BreakIfFailed(result, m_updatesCallback->StopTimer());
		BreakIfFailed(result, Invalidate(true));
	} while (false);

	if (SUCCEEDED(result))
	{
		return task_from_result();
	}
	else
	{
		return task_from_exception<void>(Exception::CreateException(result));
	}
}

task<void> AnimatedImageSourceRenderer::Initialize(ComPtr<ReadFramesAsyncOperation>& asyncOperation)
{
	auto lock = m_criticalSection.Lock();
	auto uiThreadContext = task_continuation_context::use_current();

	m_cancellationTokenSource.cancel();
	m_cancellationTokenSource = concurrency::cancellation_token_source();

	auto& cancellationToken = m_cancellationTokenSource.get_token();
	return asyncOperation->Start(cancellationToken).then([asyncOperation, this](task<ComPtr<FramesCacheStore>> task)
	{
		if (task.is_done())
		{
			auto lock = m_criticalSection.Lock();

			m_framesCacheStore = task.get();
			m_size = asyncOperation->GetFrameSize();
			m_frameIndex = 0;

			HRESULT result;

			do
			{
				D2D1_BITMAP_PROPERTIES properties = { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED, 96.0f, 96.0f };
				BreakIfFailed(result, m_owner->GetD2DDeviceContext()->CreateBitmap(m_size, nullptr, 0, &properties, &m_frameBitmap));

				if (m_framesCacheStore->GetFrameCount() > 0)
				{
					LONGLONG delay;
					BreakIfFailed(result, m_framesCacheStore->ReadBitmapEntry(0, m_frameBitmap.Get(), &delay));
				}

				BreakIfFailed(result, Invalidate(true));
				BreakIfFailed(result, OnTimerTick());
			} while (false);

			if (FAILED(result))
			{
				return task_from_exception<void>(Exception::CreateException(result));
			}
		}

		return task_from_result();
	}, cancellationToken, uiThreadContext);

	//m_cancellationTokenSource.cancel();
}