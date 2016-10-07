#include "pch.h"
#include <propvarutil.h>
#include "Helpers\MathHelper.h"
#include "Helpers\MediaFoundationHelper.h"
#include "BufferLock.h"
#include "VideoImageSourceRendererFactory.h"
#include "VideoImageSourceRenderer.h"

using namespace Mp4ImageSourceRenderer;
using namespace Platform;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage::Streams;
using namespace Microsoft::WRL;
using Windows::ApplicationModel::Core::CoreApplication;
using Windows::Foundation::TypedEventHandler;

VideoImageSourceRenderer::VideoImageSourceRenderer(int maximumWidth, int maximumHeight, VideoImageSourceRendererFactory^ owner) :
	m_frameIndex(-1),
	m_hiddenTicks(-1),
	m_maximumHiddenTicks(0),
	m_isUpdatingFrames(false),
	m_size({}),
	m_maximumSize({ maximumWidth, maximumHeight }),
	m_owner(owner)
{
	m_updatesCallback = Make<VirtualImageSourceRendererCallback>(this);

	auto application = Application::Current;
	m_eventTokens[0] = application->EnteredBackground += ref new EnteredBackgroundEventHandler(this, &VideoImageSourceRenderer::OnEnteredBackground);
	m_eventTokens[1] = application->LeavingBackground += ref new LeavingBackgroundEventHandler(this, &VideoImageSourceRenderer::OnLeavingBackground);
	m_eventTokens[2] = m_owner->SurfaceContentLost += ref new EventHandler<Object^>(this, &VideoImageSourceRenderer::OnSurfaceContentLost);

	ThrowIfFailed(InitializeImageSource());
}

VideoImageSourceRenderer::~VideoImageSourceRenderer()
{
	m_owner->SurfaceContentLost -= m_eventTokens[3];
}

HRESULT VideoImageSourceRenderer::BeginDraw(RECT const& drawingBounds, ID2D1DeviceContext** ppDeviceContext)
{
	HRESULT result;
	POINT offset;
	ComPtr<ID2D1DeviceContext> deviceContext;
	if (SUCCEEDED(result = m_imageSourceNativeD2D->BeginDraw(drawingBounds, IID_PPV_ARGS(&deviceContext), &offset)))
	{
		deviceContext->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<float>(offset.x - drawingBounds.left),
			static_cast<float>(offset.y - drawingBounds.top)));
		deviceContext->Clear(D2D1::ColorF(D2D1::ColorF::Black, 0.0f));
	}
	else if (result == DXGI_ERROR_DEVICE_REMOVED || result == DXGI_ERROR_DEVICE_RESET || result == E_SURFACE_CONTENTS_LOST)
	{
		result = m_owner->NotifyDeviceContentLost();
	}

	*ppDeviceContext = deviceContext.Detach();
	return result;
}

HRESULT VideoImageSourceRenderer::EndDraw(ID2D1DeviceContext* deviceContext)
{
	deviceContext->SetTransform(D2D1::IdentityMatrix());
	return m_imageSourceNativeD2D->EndDraw();
}

HRESULT VideoImageSourceRenderer::Draw(RECT const& drawingBounds)
{
	HRESULT result;
	auto lock = m_criticalSection.Lock();

	ComPtr<ID2D1DeviceContext> deviceContext;
	if (SUCCEEDED(result = BeginDraw(drawingBounds, &deviceContext)))
	{
		if (!m_frames.empty())
			deviceContext->DrawBitmap(m_frames[m_frameIndex].Get());

		result = EndDraw(deviceContext.Get());
	}

	return result;
}

HRESULT VideoImageSourceRenderer::Invalidate(bool resize)
{
	if (m_imageSourceNative == nullptr)
		return WS_E_INVALID_OPERATION;

	HRESULT result;
	if (resize)
		ReturnIfFailed(result, m_imageSourceNative->Resize(m_size.cx, m_size.cy));

	RECT bounds = { 0 ,0, m_size.cx ,m_size.cy };
	return m_imageSourceNative->Invalidate(bounds);
}

void VideoImageSourceRenderer::NotifyPropertyChanged(String^ propertyName)
{
	PropertyChanged(this, ref new PropertyChangedEventArgs(propertyName));
}

HRESULT VideoImageSourceRenderer::OnUpdatesNeeded()
{
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

HRESULT VideoImageSourceRenderer::OnTimerTick()
{
	auto lock = m_criticalSection.Lock();

	RECT bounds;
	if (SUCCEEDED(m_imageSourceNative->GetVisibleBounds(&bounds)) && bounds.right > bounds.left && bounds.bottom > bounds.top)
	{
		if (m_frames.empty())
		{
			if (!m_isUpdatingFrames && m_sourceReader != nullptr)
			{
				m_cancellationTokenSource.cancel();
				m_cancellationTokenSource = concurrency::cancellation_token_source();
				UpdateFrames(m_cancellationTokenSource.get_token());
			}
		}
		else
		{
			m_hiddenTicks = 0;
			m_frameIndex = (m_frameIndex + 1) % m_frames.size();

			Draw(bounds);
		}
	}
	else if (++m_hiddenTicks >= m_maximumHiddenTicks)
	{
		m_cancellationTokenSource.cancel();
		m_frames.clear();
		m_frameIndex = -1;
		m_hiddenTicks = -1;
		m_maximumHiddenTicks = 0;
	}

	return S_OK;
}

HRESULT VideoImageSourceRenderer::InitializeImageSource()
{
	HRESULT result;
	m_imageSource = ref new VirtualSurfaceImageSource(0, 0, false);

	ReturnIfFailed(result, reinterpret_cast<IUnknown*>(m_imageSource)->QueryInterface(IID_PPV_ARGS(&m_imageSourceNativeD2D)));
	ReturnIfFailed(result, m_imageSourceNativeD2D->SetDevice(m_owner->GetDevice()));

	ReturnIfFailed(result, m_imageSourceNativeD2D.As(&m_imageSourceNative));
	ReturnIfFailed(result, m_imageSourceNative->RegisterForUpdatesNeeded(m_updatesCallback.Get()));

	NotifyPropertyChanged(L"ImageSource");
	return S_OK;
}

VirtualSurfaceImageSource^ VideoImageSourceRenderer::ImageSource::get()
{
	return m_imageSource;
}

void VideoImageSourceRenderer::OnSurfaceContentLost(Object^ sender, Object^ args)
{
	ThrowIfFailed(InitializeImageSource());
	Reset();
	Initialize();
}

void VideoImageSourceRenderer::OnEnteredBackground(Object^ sender, EnteredBackgroundEventArgs^ args)
{
	auto lock = m_criticalSection.Lock();

	m_cancellationTokenSource.cancel();
	m_frames.clear();
	m_frameIndex = -1;
	m_hiddenTicks = -1;
	m_maximumHiddenTicks = 0;
	m_updatesCallback->StopTimer();
}

void VideoImageSourceRenderer::OnLeavingBackground(Object^ sender, LeavingBackgroundEventArgs^ args)
{
	auto lock = m_criticalSection.Lock();

	m_updatesCallback->ResumeTimer();
}

void VideoImageSourceRenderer::SetSource(Windows::Foundation::Uri^ uri)
{
	auto lock = m_criticalSection.Lock();

	Reset();

	if (uri != nullptr)
	{
		CreateSourceReader(uri, &m_sourceReader);
		Initialize();
	}
}

void VideoImageSourceRenderer::SetSource(Windows::Storage::Streams::IRandomAccessStream^ stream)
{
	auto lock = m_criticalSection.Lock();

	Reset();

	if (stream != nullptr)
	{
		CreateSourceReader(stream, &m_sourceReader);
		Initialize();
	}
}

void VideoImageSourceRenderer::SetSource(Windows::Media::Core::IMediaSource^ mediaSource)
{
	auto lock = m_criticalSection.Lock();

	Reset();

	if (mediaSource != nullptr)
	{
		CreateSourceReader(mediaSource, &m_sourceReader);
		Initialize();
	}
}

void VideoImageSourceRenderer::Reset()
{
	m_cancellationTokenSource.cancel();
	m_frames.clear();
	m_frameIndex = -1;
	m_hiddenTicks = -1;
	m_maximumHiddenTicks = 0;
	m_size = {};
	m_sourceReader.Reset();

	ThrowIfFailed(m_updatesCallback->StopTimer());
	ThrowIfFailed(Invalidate(true));
}

void VideoImageSourceRenderer::Initialize()
{
	float frameRate;
	SIZE frameSize = m_maximumSize;
	ThrowIfFailed(InitializeSourceReader(m_sourceReader.Get(), frameSize, &frameRate));

	auto frameDuration = static_cast<int64>(std::round(10000000.0f / frameRate));
	m_size = frameSize;
	m_maximumHiddenTicks = 30000000 / frameDuration;
	m_hiddenTicks = m_maximumHiddenTicks;

	ThrowIfFailed(Invalidate(true));
	ThrowIfFailed(m_updatesCallback->StartTimer(frameDuration));
}

task<void> VideoImageSourceRenderer::UpdateFrames(cancellation_token& ct)
{
	return create_task([this, ct]
	{
		SIZE frameSize;
		ComPtr<IMFSourceReader> sourceReader;
		ComPtr<ID2D1DeviceContext> deviceContext;

		{
			auto lock = m_criticalSection.Lock();

			m_isUpdatingFrames = true;
			sourceReader = m_sourceReader;
			deviceContext = m_owner->GetDeviceContext();
			frameSize = m_size;
		}

		PROPVARIANT positionVariant = {};
		positionVariant.vt = VT_I8;
		ThrowIfFailed(sourceReader->SetCurrentPosition(GUID_NULL, positionVariant));

		DWORD flags = 0;
		ComPtr<IMFSample> sample;
		uint32 offset = 0;
		std::vector<ComPtr<ID2D1Bitmap>> frames;

		do
		{
			ThrowIfFailed(sourceReader->ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, nullptr, &flags, nullptr, &sample));

			if (sample == nullptr)
				continue;

			ComPtr<ID2D1Bitmap> bitmap;
			D2D1_BITMAP_PROPERTIES properties = { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED, 96.0f, 96.0f };
			BufferLock bufferLock(sample.Get());
			ThrowIfFailed(deviceContext->CreateBitmap(D2D1::SizeU(frameSize.cx, frameSize.cy),
				bufferLock.GetBuffer(), frameSize.cx * sizeof(DWORD), &properties, &bitmap));

			frames.push_back(bitmap);

		} while (!(flags &  MF_SOURCE_READERF_ENDOFSTREAM || ct.is_canceled()));

		return create_task(m_imageSource->Dispatcher->RunAsync(CoreDispatcherPriority::Normal,
			ref new DispatchedHandler([this, ct, frames]
		{
			auto lock = m_criticalSection.Lock();

			m_isUpdatingFrames = false;

			if (ct.is_canceled())
			{
				m_frames.clear();
				m_frameIndex = -1;
				m_hiddenTicks = -1;
			}
			else
			{
				m_frames = frames;
				m_frameIndex = 0;
				m_hiddenTicks = 0;

				Invalidate(false);
			}
		}, CallbackContext::Any)));
	}, task_continuation_context::use_arbitrary());
}

HRESULT VideoImageSourceRenderer::InitializeSourceReader(IMFSourceReader* sourceReader, SIZE& frameSize, float* frameRate)
{
	HRESULT result;
	ReturnIfFailed(result, sourceReader->SetStreamSelection(MF_SOURCE_READER_ALL_STREAMS, FALSE));

	ComPtr<IMFMediaType> nativeMediaType;
	ReturnIfFailed(result, sourceReader->GetNativeMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0, &nativeMediaType));

	ComPtr<IMFMediaType> mediaType;
	ReturnIfFailed(result, ConvertVideoTypeToUncompressedType(nativeMediaType.Get(), MFVideoFormat_RGB32, frameSize, &mediaType));
	ReturnIfFailed(result, sourceReader->SetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, nullptr, mediaType.Get()));

	UINT32 numerator;
	UINT32 denominator;
	if (SUCCEEDED(MFGetAttributeRatio(mediaType.Get(), MF_MT_FRAME_RATE, &numerator, &denominator)))
		*frameRate = (static_cast<float>(numerator) / static_cast<float>(denominator));

	return sourceReader->SetStreamSelection(MF_SOURCE_READER_FIRST_VIDEO_STREAM, TRUE);
}

HRESULT VideoImageSourceRenderer::ConvertVideoTypeToUncompressedType(IMFMediaType* pType, const GUID& subtype, SIZE& frameSize, IMFMediaType** ppType)
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
	if (width > height)
	{
		frameSize.cy = static_cast<LONG>(static_cast<float>(frameSize.cx * height) / static_cast<float>(width));
	}
	else
	{
		frameSize.cx = static_cast<LONG>(static_cast<float>(frameSize.cy * width) / static_cast<float>(height));
	}

	ReturnIfFailed(result, MFSetAttributeSize(uncompressedType.Get(), MF_MT_FRAME_SIZE, frameSize.cx, frameSize.cy));

	MFRatio ratio = { 0 };
	if (FAILED(result = MFGetAttributeRatio(uncompressedType.Get(), MF_MT_PIXEL_ASPECT_RATIO, (UINT32*)&ratio.Numerator, (UINT32*)&ratio.Denominator)))
		ReturnIfFailed(result, MFSetAttributeRatio(uncompressedType.Get(), MF_MT_PIXEL_ASPECT_RATIO, 1, 1));

	*ppType = uncompressedType.Detach();
	return result;
}