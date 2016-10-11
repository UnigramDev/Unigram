#include "pch.h"
#include <algorithm>
#include <propvarutil.h>
#include "Helpers\MediaFoundationHelper.h"
#include "AnimatedImageSourceRendererFactory.h"
#include "AnimatedImageSourceRenderer.h"

using namespace Platform;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage::Streams;
using namespace Microsoft::WRL;
using Windows::ApplicationModel::Core::CoreApplication;
using Windows::Foundation::TypedEventHandler;

using namespace Unigram::Native;

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

	/*POINT offset;
	ComPtr<ID2D1DeviceContext> deviceContext;
	if (SUCCEEDED(result = m_imageSourceNativeD2D->BeginDraw(drawingBounds, IID_PPV_ARGS(&deviceContext), &offset)))
	{
		deviceContext->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<float>(offset.x - drawingBounds.left),
			static_cast<float>(offset.y - drawingBounds.top)));

		deviceContext->Clear(D2D1::ColorF(D2D1::ColorF::Black, 0.0f));

		if (m_frameBitmap != nullptr)
			deviceContext->DrawBitmap(m_frameBitmap.Get());

		deviceContext->SetTransform(D2D1::IdentityMatrix());

		return m_imageSourceNativeD2D->EndDraw();
	}
	else if (result == DXGI_ERROR_DEVICE_REMOVED || result == DXGI_ERROR_DEVICE_RESET)
	{
		ReturnIfFailed(result, m_owner->NotifyDeviceContentLost());

		return Draw(drawingBounds);
	}
	else if (result == E_SURFACE_CONTENTS_LOST)
	{
		ReturnIfFailed(result, InitializeImageSource());

		return Draw(drawingBounds);
	}*/
}

HRESULT AnimatedImageSourceRenderer::Invalidate(bool resize)
{
	if (m_imageSourceNative == nullptr)
		return WS_E_INVALID_OPERATION;

	HRESULT result;
	if (resize)
		ReturnIfFailed(result, m_imageSourceNative->Resize(m_size.width, m_size.height));

	RECT bounds = { 0, 0, static_cast<LONG>(m_size.width), static_cast<LONG>(m_size.height) };
	return m_imageSourceNative->Invalidate(bounds);
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

	for (uint32 i = 0; i < std::min(1UL, drawingBoundsCount); i++)
	{
		ReturnIfFailed(result, Draw(drawingBounds[i]));
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
			return m_updatesCallback->StartTimer(5000000);
		}
	}

	return S_OK;
}

HRESULT AnimatedImageSourceRenderer::InitializeImageSource()
{
	m_imageSource = ref new VirtualSurfaceImageSource(m_size.width, m_size.height, false);

	HRESULT result;
	/*ReturnIfFailed(result, reinterpret_cast<IUnknown*>(m_imageSource)->QueryInterface(IID_PPV_ARGS(&m_imageSourceNativeD2D)));
	ReturnIfFailed(result, m_imageSourceNativeD2D->SetDevice(m_owner->GetD2DDevice()));
	ReturnIfFailed(result, m_imageSourceNativeD2D.As(&m_imageSourceNative));
	ReturnIfFailed(result, m_imageSourceNative->RegisterForUpdatesNeeded(m_updatesCallback.Get()));*/

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

void AnimatedImageSourceRenderer::SetSource(Windows::Foundation::Uri^ uri)
{
	auto lock = m_criticalSection.Lock();

	Reset();

	if (uri != nullptr)
	{
		ComPtr<IMFSourceReader> sourceReader;
		ThrowIfFailed(CreateSourceReader(uri, &sourceReader));
		Initialize(sourceReader);
	}
}

void AnimatedImageSourceRenderer::SetSource(Windows::Storage::Streams::IRandomAccessStream^ stream)
{
	auto lock = m_criticalSection.Lock();

	Reset();

	if (stream != nullptr)
	{
		ComPtr<IMFSourceReader> sourceReader;
		ThrowIfFailed(CreateSourceReader(stream, &sourceReader));
		Initialize(sourceReader);
	}
}

void AnimatedImageSourceRenderer::SetSource(Windows::Media::Core::IMediaSource^ mediaSource)
{
	auto lock = m_criticalSection.Lock();

	Reset();

	if (mediaSource != nullptr)
	{
		ComPtr<IMFSourceReader> sourceReader;
		ThrowIfFailed(CreateSourceReader(mediaSource, &sourceReader));
		Initialize(sourceReader);
	}
}

void AnimatedImageSourceRenderer::Reset()
{
	m_frameIndex = -1;
	m_size = {};

	m_cancellationTokenSource.cancel();
	m_frameBitmap.Reset();
	m_framesCacheStore.Reset();

	ThrowIfFailed(m_updatesCallback->StopTimer());
	ThrowIfFailed(Invalidate(true));
}

void AnimatedImageSourceRenderer::Initialize(ComPtr<IMFSourceReader>& sourceReader)
{
	auto uiThreadContext = task_continuation_context::use_current();

	m_cancellationTokenSource.cancel();
	m_cancellationTokenSource = concurrency::cancellation_token_source();

	ComPtr<ID2D1DeviceContext> d2dDeviceContext = m_owner->GetD2DDeviceContext();
	CreateFrameBitmapsAsync(m_maximumSize, d2dDeviceContext, sourceReader, m_cancellationTokenSource.get_token())
		.then([=](task<ComPtr<FrameBitmapsResult>> task)
	{
		auto lock = m_criticalSection.Lock();

		try
		{
			if (task.is_done())
			{
				auto result = task.get();
				m_size = result->FrameSize;
				m_framesCacheStore = result->CacheStore;
				m_frameIndex = 0;

				D2D1_BITMAP_PROPERTIES properties = { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED, 96.0f, 96.0f };
				ThrowIfFailed(m_owner->GetD2DDeviceContext()->CreateBitmap(result->FrameSize, nullptr, 0, &properties, &m_frameBitmap));

				if (m_framesCacheStore->GetFrameCount() > 0)
				{
					LONGLONG delay;
					ThrowIfFailed(m_framesCacheStore->ReadBitmapEntry(0, m_frameBitmap.Get(), &delay));
				}

				ThrowIfFailed(Invalidate(true));
				ThrowIfFailed(OnTimerTick());
			}
			else
			{
				m_size = {};
				m_frameIndex = -1;
				m_framesCacheStore.Reset();
				m_frameBitmap.Reset();
			}

			return task_from_result();
		}
		catch (Exception^ ex)
		{
			return task_from_exception<void>(ex);
		}
	}, uiThreadContext);
}

task<ComPtr<AnimatedImageSourceRenderer::FrameBitmapsResult>> AnimatedImageSourceRenderer::CreateFrameBitmapsAsync(D2D1_SIZE_U maximumSize,
	ComPtr<ID2D1DeviceContext>& d2dDeviceContext, ComPtr<IMFSourceReader>& sourceReader, cancellation_token& ct)
{
	return create_task([=]
	{
		auto frameBitmapsResult = Make<FrameBitmapsResult>(maximumSize);

		ThrowIfFailed(InitializeSourceReader(sourceReader.Get(), &frameBitmapsResult->FrameSize));

		DWORD flags = 0;
		ComPtr<IMFSample> sample;
		uint32 offset = 0;
		LONGLONG lastSampleTime = 0;
		DWORD stride = frameBitmapsResult->FrameSize.width * sizeof(DWORD);

		do
		{
			ThrowIfFailed(sourceReader->ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, nullptr, &flags, nullptr, &sample));
			if (flags & MF_SOURCE_READERF_STREAMTICK || sample == nullptr)
				continue;

			LONGLONG delay;
			if (FAILED(sample->GetSampleDuration(&delay)))
				delay = 10000000;

			BufferLock bufferLock(sample.Get());
			if (!bufferLock.IsValid())
				ThrowException(MF_E_NO_VIDEO_SAMPLE_AVAILABLE);

			ThrowIfFailed(frameBitmapsResult->CacheStore->WriteBitmapEntry(bufferLock.GetBuffer(),
				bufferLock.GetLength(), stride, delay));

		} while (!(flags &  MF_SOURCE_READERF_ENDOFSTREAM || ct.is_canceled()));

		if (ct.is_canceled())
			cancel_current_task();

		ThrowIfFailed(frameBitmapsResult->CacheStore->Lock());

		return frameBitmapsResult;
	}, task_continuation_context::use_arbitrary());
}

HRESULT AnimatedImageSourceRenderer::InitializeSourceReader(IMFSourceReader* sourceReader, D2D1_SIZE_U* frameSize)
{
	HRESULT result;
	ReturnIfFailed(result, sourceReader->SetStreamSelection(MF_SOURCE_READER_ALL_STREAMS, FALSE));

	ComPtr<IMFMediaType> nativeMediaType;
	ReturnIfFailed(result, sourceReader->GetNativeMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, &nativeMediaType));

	ComPtr<IMFMediaType> mediaType;
	ReturnIfFailed(result, ConvertVideoTypeToUncompressedType(nativeMediaType.Get(), MFVideoFormat_ARGB32, frameSize, &mediaType));
	ReturnIfFailed(result, sourceReader->SetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, nullptr, mediaType.Get()));

	return sourceReader->SetStreamSelection(MF_SOURCE_READER_FIRST_VIDEO_STREAM, TRUE);
}

HRESULT AnimatedImageSourceRenderer::ConvertVideoTypeToUncompressedType(IMFMediaType* pType, const GUID& subtype, D2D1_SIZE_U* frameSize, IMFMediaType** ppType)
{
	HRESULT result;
	GUID majorType;
	ReturnIfFailed(result, pType->GetMajorType(&majorType));
	if (majorType != MFMediaType_Video)
		return MF_E_INVALIDMEDIATYPE;

	ComPtr<IMFMediaType> uncompressedType;
	ReturnIfFailed(result, MFCreateMediaType(&uncompressedType));
	ReturnIfFailed(result, pType->CopyAllItems(uncompressedType.Get()));
	ReturnIfFailed(result, uncompressedType->SetGUID(MF_MT_SUBTYPE, subtype));
	ReturnIfFailed(result, uncompressedType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE));

	UINT32 width;
	UINT32 height;
	ReturnIfFailed(result, MFGetAttributeSize(pType, MF_MT_FRAME_SIZE, &width, &height));
	if (width > frameSize->width || height > frameSize->height)
	{
		if (width > height)
		{
			frameSize->height = static_cast<UINT32>(static_cast<float>(frameSize->width  * height) / static_cast<float>(width));
		}
		else
		{
			frameSize->width = static_cast<UINT32>(static_cast<float>(frameSize->height * width) / static_cast<float>(height));
		}
	}
	else
	{
		frameSize->width = width;
		frameSize->height = height;
	}

	ReturnIfFailed(result, MFSetAttributeSize(uncompressedType.Get(), MF_MT_FRAME_SIZE, frameSize->width, frameSize->height));

	MFRatio ratio = {};
	if (FAILED(result = MFGetAttributeRatio(uncompressedType.Get(), MF_MT_PIXEL_ASPECT_RATIO,
		reinterpret_cast<UINT32*>(&ratio.Numerator), reinterpret_cast<UINT32*>(&ratio.Denominator))))
		ReturnIfFailed(result, MFSetAttributeRatio(uncompressedType.Get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1));

	*ppType = uncompressedType.Detach();
	return result;
}