#include "pch.h"
#include "BufferLock.h"
#include "GIFMediaSource.h"

using namespace Unigram::Native;

GIFMediaSource::GIFMediaSource() :
	m_state(MediaSourceState::None)
{
}

GIFMediaSource::~GIFMediaSource()
{
	Shutdown();
}

HRESULT GIFMediaSource::RuntimeClassInitialize(IMFByteStream* byteStream)
{
	if (byteStream == nullptr)
		return E_POINTER;

	HRESULT result;
	ReturnIfFailed(result, MakeAndInitialize<GIFMediaStream>(&m_mediaStream, this, byteStream));
	ReturnIfFailed(result, MFCreateEventQueue(&m_events));

	m_state = MediaSourceState::Stopped;
	return S_OK;
}

HRESULT GIFMediaSource::GetService(REFGUID guidService, REFIID riid, LPVOID* ppvObject)
{
	if (ppvObject == nullptr)
		return E_POINTER;

	if (guidService != MF_MEDIASOURCE_SERVICE)
		return MF_E_UNSUPPORTED_SERVICE;

	return QueryInterface(riid, ppvObject);
}

HRESULT GIFMediaSource::CreatePresentationDescriptor(IMFPresentationDescriptor** ppPresentationDescriptor)
{
	if (ppPresentationDescriptor == nullptr)
		return E_POINTER;

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
		return MF_E_SHUTDOWN;

	return m_presentationDescriptor->Clone(ppPresentationDescriptor);
}

HRESULT GIFMediaSource::GetCharacteristics(DWORD* pdwCharacteristics)
{
	if (pdwCharacteristics == nullptr)
		return E_POINTER;

	HRESULT hr = S_OK;

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
		return MF_E_SHUTDOWN;

	*pdwCharacteristics = MFMEDIASOURCE_DOES_NOT_USE_NETWORK | MFMEDIASOURCE_CAN_SEEK | MFMEDIASOURCE_CAN_PAUSE;
	return S_OK;
}

HRESULT GIFMediaSource::BeginGetEvent(IMFAsyncCallback* caller, IUnknown* state)
{
	if (caller == nullptr)
		return E_POINTER;

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
		return MF_E_SHUTDOWN;

	return m_events->BeginGetEvent(caller, state);
}

HRESULT GIFMediaSource::EndGetEvent(IMFAsyncResult* result, IMFMediaEvent** out)
{
	if (result == nullptr || out == nullptr)
		return E_POINTER;

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
		return MF_E_SHUTDOWN;

	return m_events->EndGetEvent(result, out);
}

HRESULT GIFMediaSource::GetEvent(DWORD flags, IMFMediaEvent** out)
{
	ComPtr<IMFMediaEventQueue> events;
	{
		auto lock = m_criticalSection.Lock();

		if (m_state == MediaSourceState::Shutdown)
			return MF_E_SHUTDOWN;

		events = m_events.Get();
	}

	return events->GetEvent(flags, out);
}

HRESULT GIFMediaSource::QueueEvent(MediaEventType type, REFGUID guid, HRESULT status, PROPVARIANT const* val)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
		return MF_E_SHUTDOWN;

	return m_events->QueueEventParamVar(type, guid, status, val);
}

HRESULT GIFMediaSource::Shutdown()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
		return MF_E_SHUTDOWN;

	if (m_events != nullptr)
		m_events->Shutdown();

	if (m_mediaStream != nullptr)
		m_mediaStream->Shutdown();

	m_events.Reset();
	m_mediaStream.Reset();
	m_presentationDescriptor.Reset();
	m_state = MediaSourceState::Shutdown;

	return S_OK;
}

HRESULT GIFMediaSource::Start(IMFPresentationDescriptor* pPresentationDescriptor, GUID const* pguidTimeFormat, PROPVARIANT const* pvarStartPosition)
{
	if (pvarStartPosition == nullptr || pPresentationDescriptor == nullptr)
		return E_INVALIDARG;

	if (pguidTimeFormat != nullptr && *pguidTimeFormat != GUID_NULL)
		return MF_E_UNSUPPORTED_TIME_FORMAT;

	auto lock = m_criticalSection.Lock();

	/*if (pvarStartPosition->vt == VT_I8 && (m_state != MediaSourceState::Stopped || pvarStartPosition->hVal.QuadPart != 0))
	return MF_E_INVALIDREQUEST;*/

	if (m_state == MediaSourceState::Shutdown)
		return MF_E_SHUTDOWN;

	if (m_state == MediaSourceState::None)
		return MF_E_NOT_INITIALIZED;

	HRESULT result;
	DWORD streamDescriptorCount;
	ReturnIfFailed(result, pPresentationDescriptor->GetStreamDescriptorCount(&streamDescriptorCount));

	if (streamDescriptorCount != 1)
		return MF_E_UNSUPPORTED_REPRESENTATION;

	BOOL selected;
	ComPtr<IMFStreamDescriptor> streamDescriptor;
	ReturnIfFailed(result, pPresentationDescriptor->GetStreamDescriptorByIndex(0, &selected, &streamDescriptor));

	if (!selected)
		return MF_E_UNSUPPORTED_REPRESENTATION;

	ReturnIfFailed(result, m_events->QueueEventParamUnk(MENewStream, GUID_NULL, S_OK, m_mediaStream->CastToUnknown()));
	ReturnIfFailed(result, m_mediaStream->Start(pvarStartPosition));

	m_state = MediaSourceState::Started;
	return m_events->QueueEventParamVar(MESourceStarted, GUID_NULL, S_OK, pvarStartPosition);
}

HRESULT GIFMediaSource::Pause()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
		return MF_E_SHUTDOWN;

	if (m_state != MediaSourceState::Started)
		return MF_E_INVALID_STATE_TRANSITION;

	HRESULT result;
	ReturnIfFailed(result, m_mediaStream->Pause());

	m_state = MediaSourceState::Paused;
	return m_events->QueueEventParamVar(MESourcePaused, GUID_NULL, S_OK, nullptr);
}

HRESULT GIFMediaSource::Stop()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
		return MF_E_SHUTDOWN;

	if (m_state == MediaSourceState::None)
		return MF_E_NOT_INITIALIZED;

	HRESULT result;
	ReturnIfFailed(result, m_mediaStream->Stop());

	m_state = MediaSourceState::Stopped;
	return m_events->QueueEventParamVar(MESourceStopped, GUID_NULL, S_OK, nullptr);
}

HRESULT GIFMediaSource::GetSourceAttributes(IMFAttributes** ppAttributes)
{
	return E_NOTIMPL;
}

HRESULT GIFMediaSource::GetStreamAttributes(DWORD dwStreamIdentifier, IMFAttributes** ppAttributes)
{
	return E_NOTIMPL;
}

HRESULT GIFMediaSource::SetD3DManager(IUnknown* pManager)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
		return MF_E_SHUTDOWN;

	HRESULT result;
	ComPtr<IMFDXGIDeviceManager> deviceManager;
	ReturnIfFailed(result, pManager->QueryInterface(IID_PPV_ARGS(&deviceManager)));

	return m_mediaStream->SetD3DManager(deviceManager.Get());
}

HRESULT GIFMediaSource::NotifyEndOfPresentation()
{
	return m_events->QueueEventParamVar(MEEndOfPresentation, GUID_NULL, S_OK, nullptr);
}


GIFMediaStream::GIFMediaStream() :
	m_isShutdown(false),
	m_frameIndex(0),
	m_frameTime(0),
	m_backgroundColor(D2D1::ColorF(D2D1::ColorF::Black, 0.0f))
{
}

HRESULT GIFMediaStream::RuntimeClassInitialize(GIFMediaSource* mediaSource, IMFByteStream* byteStream)
{
	m_mediaSource = mediaSource;

	HRESULT result;
	ReturnIfFailed(result, CoCreateInstance(CLSID_WICImagingFactory, NULL, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&m_wicFactory)));

	ComPtr<IStream> gifStream;
	ReturnIfFailed(result, MFCreateStreamOnMFByteStreamEx(byteStream, IID_PPV_ARGS(&gifStream)));
	ReturnIfFailed(result, m_wicFactory->CreateDecoder(GUID_ContainerFormatGif, nullptr, &m_wicGIFDecoder));
	ReturnIfFailed(result, m_wicGIFDecoder->Initialize(gifStream.Get(), WICDecodeMetadataCacheOnDemand));

	ComPtr<IMFMediaType> mediaType;
	ComPtr<IMFStreamDescriptor> streamDescriptor;
	ReturnIfFailed(result, CreatePresentationDescriptor(m_wicGIFDecoder.Get(), &mediaType, &mediaSource->m_presentationDescriptor, &m_streamDescriptor));

	ComPtr<IWICMetadataQueryReader> metadataReader;
	ReturnIfFailed(result, m_wicGIFDecoder->GetMetadataQueryReader(&metadataReader));
	ReturnIfFailed(result, InitializeMetadataResources(metadataReader.Get(), mediaType.Get()));
	ReturnIfFailed(result, CreateDeviceResources());

	return MFCreateEventQueue(&m_events);
}

HRESULT GIFMediaStream::GetStreamDescriptor(IMFStreamDescriptor** ppStreamDescriptor)
{
	if (ppStreamDescriptor == nullptr)
		return E_POINTER;

	auto lock = m_criticalSection.Lock();

	if (m_isShutdown)
		return MF_E_SHUTDOWN;

	if (m_streamDescriptor == nullptr)
		return E_UNEXPECTED;

	return m_streamDescriptor.CopyTo(ppStreamDescriptor);
}

HRESULT GIFMediaStream::BeginGetEvent(IMFAsyncCallback* caller, IUnknown* state)
{
	auto lock = m_criticalSection.Lock();

	if (m_isShutdown)
		return MF_E_SHUTDOWN;

	return m_events->BeginGetEvent(caller, state);
}

HRESULT GIFMediaStream::EndGetEvent(IMFAsyncResult* result, IMFMediaEvent** out)
{
	auto lock = m_criticalSection.Lock();

	if (m_isShutdown)
		return MF_E_SHUTDOWN;

	return m_events->EndGetEvent(result, out);
}

HRESULT GIFMediaStream::GetEvent(DWORD flags, IMFMediaEvent** out)
{
	ComPtr<IMFMediaEventQueue> events;
	{
		auto lock = m_criticalSection.Lock();

		if (m_isShutdown)
			return MF_E_SHUTDOWN;

		events = m_events.Get();
	}

	return events->GetEvent(flags, out);
}

HRESULT GIFMediaStream::QueueEvent(MediaEventType type, REFGUID guid, HRESULT status, PROPVARIANT const* val)
{
	auto lock = m_criticalSection.Lock();

	if (m_isShutdown)
		return MF_E_SHUTDOWN;

	return m_events->QueueEventParamVar(type, guid, status, val);
}

HRESULT GIFMediaStream::GetMediaSource(IMFMediaSource** ppMediaSource)
{
	if (ppMediaSource == nullptr)
		return E_POINTER;

	auto lock = m_criticalSection.Lock();

	if (m_isShutdown)
		return MF_E_SHUTDOWN;

	return m_mediaSource->QueryInterface(IID_PPV_ARGS(ppMediaSource));
}

HRESULT GIFMediaStream::RequestSample(IUnknown* pToken)
{
	auto lock = m_criticalSection.Lock();

	if (m_isShutdown)
		return MF_E_SHUTDOWN;

	if (m_frameIndex >= m_frameDefinitions.size())
		return MF_E_END_OF_STREAM;

	HRESULT result;
	ComPtr<IMFSample> sample;
	auto& frameDefinition = m_frameDefinitions[m_frameIndex];
	ReturnIfFailed(result, DrawFrame(&frameDefinition, &sample));
	ReturnIfFailed(result, sample->SetSampleTime(m_frameTime));
	ReturnIfFailed(result, sample->SetSampleDuration(frameDefinition.Delay));

	if (pToken != nullptr)
		ReturnIfFailed(result, sample->SetUnknown(MFSampleExtension_Token, pToken));

	ReturnIfFailed(result, m_events->QueueEventParamUnk(MEMediaSample, GUID_NULL, S_OK, sample.Get()));

	if (++m_frameIndex == m_frameDefinitions.size())
	{
		ReturnIfFailed(result, m_events->QueueEventParamVar(MEEndOfStream, GUID_NULL, S_OK, nullptr));
		ReturnIfFailed(result, m_mediaSource->NotifyEndOfPresentation());
	}

	m_frameTime += frameDefinition.Delay;
	return S_OK;
}

HRESULT GIFMediaStream::Shutdown()
{
	auto lock = m_criticalSection.Lock();

	if (m_isShutdown)
		return MF_E_SHUTDOWN;

	if (m_events != nullptr)
		m_events->Shutdown();

	m_events.Reset();
	m_frameDefinitions.clear();
	m_mediaSource.Reset();
	m_wicGIFDecoder.Reset();
	m_wicFactory.Reset();
	m_dxgiDeviceManager.Reset();
	m_frameTargetBitmap.Reset();
	m_frameBufferBitmap.Reset();
	m_d2dDevice.Reset();
	m_d2dDeviceContext.Reset();
	m_streamDescriptor.Reset();
	m_isShutdown = true;

	return S_OK;
}

HRESULT GIFMediaStream::SetD3DManager(IMFDXGIDeviceManager* deviceManager)
{
	auto lock = m_criticalSection.Lock();

	if (m_isShutdown)
		return MF_E_SHUTDOWN;

	m_dxgiDeviceManager = deviceManager;
	return CreateDeviceResources();
}

HRESULT GIFMediaStream::Start(PROPVARIANT const* variant)
{
	auto lock = m_criticalSection.Lock();

	if (m_isShutdown)
		return MF_E_SHUTDOWN;

	bool isSeeking;
	LONGLONG startPosition;
	if (variant->vt == VT_I8)
	{
		isSeeking = m_mediaSource->m_state > GIFMediaSource::MediaSourceState::Stopped;
		startPosition = variant->hVal.QuadPart;
	}
	else if (variant->vt == VT_EMPTY)
	{
		isSeeking = false;
		startPosition = 0;
	}
	else
	{
		return MF_E_UNSUPPORTED_TIME_FORMAT;
	}

	m_frameIndex = 0;
	m_frameTime = 0;

	for (size_t i = 0; i < m_frameDefinitions.size(); i++)
	{
		if (m_frameTime + m_frameDefinitions[i].Delay > startPosition)
			break;

		m_frameIndex = i;
		m_frameTime += m_frameDefinitions[i].Delay;
	}

	HRESULT result;
	m_d2dDeviceContext->BeginDraw();
	m_d2dDeviceContext->Clear(m_backgroundColor);
	ReturnIfFailed(result, m_d2dDeviceContext->EndDraw());

	return m_events->QueueEventParamVar(isSeeking ? MEStreamSeeked : MEStreamStarted, GUID_NULL, S_OK, nullptr);
}

HRESULT GIFMediaStream::Pause()
{
	auto lock = m_criticalSection.Lock();

	if (m_isShutdown)
		return MF_E_SHUTDOWN;

	return m_events->QueueEventParamVar(MEStreamPaused, GUID_NULL, S_OK, nullptr);
}

HRESULT GIFMediaStream::Stop()
{
	auto lock = m_criticalSection.Lock();

	if (m_isShutdown)
		return MF_E_SHUTDOWN;

	m_frameIndex = 0;
	return m_events->QueueEventParamVar(MEStreamStopped, GUID_NULL, S_OK, nullptr);
}

HRESULT GIFMediaStream::CreateDeviceResources()
{
	HRESULT result;
	auto lock = m_criticalSection.Lock();

	ComPtr<ID3D11Device> d3dDevice;
	if (m_dxgiDeviceManager == nullptr)
	{
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

		ReturnIfFailed(result, D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr,
			D3D11_CREATE_DEVICE_BGRA_SUPPORT | D3D11_CREATE_DEVICE_VIDEO_SUPPORT | D3D11_CREATE_DEVICE_SINGLETHREADED, featureLevels,
			ARRAYSIZE(featureLevels), D3D11_SDK_VERSION, &d3dDevice, nullptr, nullptr));
	}
	else
	{
		HANDLE deviceHandle;
		ReturnIfFailed(result, m_dxgiDeviceManager->OpenDeviceHandle(&deviceHandle));
		ReturnIfFailed(result, m_dxgiDeviceManager->GetVideoService(deviceHandle, IID_PPV_ARGS(&d3dDevice)));
		ReturnIfFailed(result, m_dxgiDeviceManager->CloseDeviceHandle(deviceHandle));
	}

	ComPtr<IDXGIDevice> dxgiDevice;
	ReturnIfFailed(result, d3dDevice.As(&dxgiDevice));

	D2D1_CREATION_PROPERTIES deviceProperties = { D2D1_THREADING_MODE_SINGLE_THREADED, D2D1_DEBUG_LEVEL_NONE, D2D1_DEVICE_CONTEXT_OPTIONS_NONE };
	ReturnIfFailed(result, D2D1CreateDevice(dxgiDevice.Get(), &deviceProperties, &m_d2dDevice));

	ReturnIfFailed(result, m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &m_d2dDeviceContext));

	D2D1_BITMAP_PROPERTIES1 targetBitmapProperties = { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED, 96.0f, 96.0f,
		D2D1_BITMAP_OPTIONS_TARGET };
	ReturnIfFailed(result, m_d2dDeviceContext->CreateBitmap(m_frameSize, nullptr, 0, &targetBitmapProperties, &m_frameTargetBitmap));

	m_d2dDeviceContext->SetTarget(m_frameTargetBitmap.Get());
	m_d2dDeviceContext->SetUnitMode(D2D1_UNIT_MODE_DIPS);
	m_d2dDeviceContext->SetDpi(96.0f, 96.0f);
	m_d2dDeviceContext->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);

	D2D1_BITMAP_PROPERTIES1 bufferBitmapProperties = { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED, 96.0f, 96.0f,
		 D2D1_BITMAP_OPTIONS_CANNOT_DRAW | D2D1_BITMAP_OPTIONS_CPU_READ };
	return m_d2dDeviceContext->CreateBitmap(m_frameSize, nullptr, 0, &bufferBitmapProperties, &m_frameBufferBitmap);
}

HRESULT GIFMediaStream::DrawFrame(GIFFrameDefinition const* frameDefinition, IMFSample** ppSample)
{
	HRESULT result;
	ComPtr<IWICBitmapFrameDecode> frame;
	ReturnIfFailed(result, m_wicGIFDecoder->GetFrame(m_frameIndex, &frame));

	ComPtr<IWICFormatConverter> wicFormatConverter;
	ReturnIfFailed(result, m_wicFactory->CreateFormatConverter(&wicFormatConverter));
	ReturnIfFailed(result, wicFormatConverter->Initialize(frame.Get(), GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherTypeNone, nullptr, 0.0f, WICBitmapPaletteTypeCustom));

	ComPtr<ID2D1Bitmap> frameBitmap;
	D2D1_BITMAP_PROPERTIES properties = { DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED, 96.0f, 96.0f };
	ReturnIfFailed(result, m_d2dDeviceContext->CreateBitmapFromWicBitmap(wicFormatConverter.Get(), &properties, &frameBitmap));

	ComPtr<ID2D1Bitmap> previousFrameBitmap;
	ReturnIfFailed(result, m_d2dDeviceContext->CreateBitmap(m_frameSize, properties, &previousFrameBitmap));
	ReturnIfFailed(result, previousFrameBitmap->CopyFromBitmap(nullptr, m_frameBufferBitmap.Get(), nullptr));

	m_d2dDeviceContext->BeginDraw();
	m_d2dDeviceContext->DrawBitmap(frameBitmap.Get(), frameDefinition->Bounds);

	if (SUCCEEDED(result = m_d2dDeviceContext->EndDraw()))
	{
		ReturnIfFailed(result, m_frameBufferBitmap->CopyFromBitmap(nullptr, m_frameTargetBitmap.Get(), nullptr));
		ReturnIfFailed(result, CreateFrameSample(m_frameBufferBitmap.Get(), m_frameSize, ppSample));

		m_d2dDeviceContext->BeginDraw();

		switch (frameDefinition->DisposalMethod)
		{
		case GIFFrameDisposalMethod::RestoreBackgroundColor:
			m_d2dDeviceContext->Clear(m_backgroundColor);
			break;
		case GIFFrameDisposalMethod::RestorePrevious:
			m_d2dDeviceContext->DrawBitmap(previousFrameBitmap.Get());
			break;
		}

		result = m_d2dDeviceContext->EndDraw();
	}

	if (result == D2DERR_RECREATE_TARGET)
	{
		ReturnIfFailed(result, CreateDeviceResources());
		ReturnIfFailed(result, DrawFrame(frameDefinition, ppSample));
	}

	return S_OK;
}

HRESULT GIFMediaStream::InitializeMetadataResources(IWICMetadataQueryReader* metadataReader, IMFMediaType* mediaType)
{
	HRESULT result;
	PROPVARIANT variant;
	PropVariantInit(&variant);
	ReturnIfFailed(result, metadataReader->GetMetadataByName(L"/logscrdesc/Width", &variant));
	m_frameSize.width = variant.uiVal;

	PropVariantInit(&variant);
	ReturnIfFailed(result, metadataReader->GetMetadataByName(L"/logscrdesc/Height", &variant));
	m_frameSize.height = variant.uiVal;

	PropVariantInit(&variant);
	ReturnIfFailed(result, metadataReader->GetMetadataByName(L"/logscrdesc/PixelAspectRatio", &variant));
	if (variant.bVal > 0)
	{
		ReturnIfFailed(result, MFSetAttributeRatio(mediaType, MF_MT_PIXEL_ASPECT_RATIO, variant.bVal + 15, 64));
	}
	else
	{
		ReturnIfFailed(result, MFSetAttributeRatio(mediaType, MF_MT_PIXEL_ASPECT_RATIO, 1, 1));
	}

	PropVariantInit(&variant);
	if (SUCCEEDED(metadataReader->GetMetadataByName(L"/logscrdesc/BackgroundColorIndex", &variant)))
	{
		std::vector<WICColor> paletteColors;
		ReturnIfFailed(result, GetBitmapPaletteColors(m_wicFactory.Get(), m_wicGIFDecoder.Get(), paletteColors));

		auto color = paletteColors[variant.bVal];
		m_backgroundColor = D2D1::ColorF(color, (color >> 24) / 255.f);
	}

	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_SAMPLE_SIZE, m_frameSize.width * m_frameSize.height * sizeof(DWORD)));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_DEFAULT_STRIDE, m_frameSize.width * sizeof(DWORD)));
	ReturnIfFailed(result, MFSetAttributeSize(mediaType, MF_MT_FRAME_SIZE, m_frameSize.width, m_frameSize.height));

	UINT frameCount;
	ReturnIfFailed(result, m_wicGIFDecoder->GetFrameCount(&frameCount));

	m_frameDefinitions.reserve(frameCount);

	DWORD totalDelay = 0;
	for (UINT i = 0; i < frameCount; i++)
	{
		ComPtr<IWICBitmapFrameDecode> frame;
		ReturnIfFailed(result, m_wicGIFDecoder->GetFrame(i, &frame));

		GIFFrameDefinition frameDefinition(m_frameSize);
		ReturnIfFailed(result, GetFrameDefinition(frame.Get(), &frameDefinition));

		totalDelay += frameDefinition.Delay;
		m_frameDefinitions.push_back(frameDefinition);
	}

	ReturnIfFailed(result, m_mediaSource->m_presentationDescriptor->SetUINT64(MF_PD_DURATION, totalDelay));
	ReturnIfFailed(result, MFSetAttributeRatio(mediaType, MF_MT_FRAME_RATE, frameCount * 100, totalDelay / 100000));

	return S_OK;
}

HRESULT GIFMediaStream::GetFrameDefinition(IWICBitmapFrameDecode* frame, GIFFrameDefinition* frameDefinition)
{
	HRESULT result;
	ComPtr<IWICMetadataQueryReader> metadataReader;
	ReturnIfFailed(result, frame->GetMetadataQueryReader(&metadataReader));

	PROPVARIANT variant;
	PropVariantInit(&variant);
	if (SUCCEEDED(metadataReader->GetMetadataByName(L"/imgdesc/Left", &variant)))
		frameDefinition->Bounds.left = variant.uiVal;

	PropVariantInit(&variant);
	if (SUCCEEDED(metadataReader->GetMetadataByName(L"/imgdesc/Top", &variant)))
		frameDefinition->Bounds.top = variant.uiVal;

	PropVariantInit(&variant);
	if (SUCCEEDED(metadataReader->GetMetadataByName(L"/imgdesc/Width", &variant)))
		frameDefinition->Bounds.right = frameDefinition->Bounds.left + variant.uiVal;
	
	PropVariantInit(&variant);
	if (SUCCEEDED(metadataReader->GetMetadataByName(L"/imgdesc/Height", &variant)))
		frameDefinition->Bounds.bottom = frameDefinition->Bounds.top + variant.uiVal;

	PropVariantInit(&variant);
	if (SUCCEEDED(metadataReader->GetMetadataByName(L"/grctlext/Delay", &variant)) && variant.uiVal > 0)
		frameDefinition->Delay = std::max(90000, variant.uiVal * 100000);

	PropVariantInit(&variant);
	if (SUCCEEDED(metadataReader->GetMetadataByName(L"/grctlext/Disposal", &variant)))
		frameDefinition->DisposalMethod = static_cast<GIFFrameDisposalMethod>(variant.bVal);

	return S_OK;
}

HRESULT GIFMediaStream::CreatePresentationDescriptor(IWICBitmapDecoder* wicGIFDecoder, IMFMediaType** ppMediaType,
	IMFPresentationDescriptor** ppPresentationDescriptor, IMFStreamDescriptor** ppStreamDescriptor)
{
	HRESULT result;
	ComPtr<IMFMediaType> mediaType;
	ReturnIfFailed(result, MFCreateMediaType(&mediaType));
	ReturnIfFailed(result, mediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Video));
	ReturnIfFailed(result, mediaType->SetGUID(MF_MT_SUBTYPE, MFVideoFormat_ARGB32));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_FIXED_SIZE_SAMPLES, TRUE));

	ComPtr<IMFStreamDescriptor> streamDescriptor;
	ReturnIfFailed(result, MFCreateStreamDescriptor(0, 1, mediaType.GetAddressOf(), &streamDescriptor));

	ComPtr<IMFMediaTypeHandler> mediaTypeHandler;
	ThrowIfFailed(streamDescriptor->GetMediaTypeHandler(&mediaTypeHandler));
	ThrowIfFailed(mediaTypeHandler->SetCurrentMediaType(mediaType.Get()));

	ComPtr<IMFPresentationDescriptor> presentationDescriptor;
	ReturnIfFailed(result, MFCreatePresentationDescriptor(1, streamDescriptor.GetAddressOf(), &presentationDescriptor));
	ReturnIfFailed(result, presentationDescriptor->SelectStream(0));

	*ppMediaType = mediaType.Detach();
	*ppPresentationDescriptor = presentationDescriptor.Detach();
	*ppStreamDescriptor = streamDescriptor.Detach();
	return S_OK;
}

HRESULT GIFMediaStream::GetBitmapPaletteColors(IWICImagingFactory* wicFactory, IUnknown* wicPaletteSource, std::vector<WICColor>& colors)
{
	HRESULT result;
	ComPtr<IWICPalette> palette;
	ReturnIfFailed(result, wicFactory->CreatePalette(&palette));

	ComPtr<IWICBitmapSource> bitmapSource;
	ComPtr<IWICBitmapDecoder> bitmapDecoder;
	if (SUCCEEDED(wicPaletteSource->QueryInterface(IID_PPV_ARGS(&bitmapSource))))
	{
		ReturnIfFailed(result, bitmapSource->CopyPalette(palette.Get()));
	}
	else if (SUCCEEDED(wicPaletteSource->QueryInterface(IID_PPV_ARGS(&bitmapDecoder))))
	{
		ReturnIfFailed(result, bitmapDecoder->CopyPalette(palette.Get()));
	}

	UINT colorCount;
	ReturnIfFailed(result, palette->GetColorCount(&colorCount));

	colors.resize(colorCount);
	return palette->GetColors(colorCount, colors.data(), &colorCount);
}

HRESULT GIFMediaStream::CreateFrameSample(ID2D1Bitmap1* frameBufferBitmap, D2D_SIZE_U frameSize, IMFSample** ppSample)
{
	HRESULT result;
	ComPtr<IMFMediaBuffer> mediaBuffer;
	ReturnIfFailed(result, MFCreate2DMediaBuffer(frameSize.width, frameSize.height, MFVideoFormat_ARGB32.Data1, FALSE, &mediaBuffer));

	ComPtr<IMF2DBuffer> mediaBuffer2D;
	ReturnIfFailed(result, mediaBuffer.As(&mediaBuffer2D));

	BufferLock2D bufferLock(mediaBuffer2D.Get());
	if (!bufferLock.IsValid())
		return MF_E_NO_VIDEO_SAMPLE_AVAILABLE;

	BitmapLock bitmapLock(D2D1_MAP_OPTIONS_READ, frameBufferBitmap);
	if (!bitmapLock.IsValid())
		return MF_E_NO_VIDEO_SAMPLE_AVAILABLE;

	for (DWORD i = 0; i < frameSize.height; i++)
	{
		CopyMemory(bufferLock.GetScanLine() + bufferLock.GetPitch() * i,
			bitmapLock.GetScanLine() + bitmapLock.GetPitch() * i, bufferLock.GetPitch());
	}

	ComPtr<IMFSample> sample;
	ReturnIfFailed(result, MFCreateSample(&sample));
	ReturnIfFailed(result, sample->AddBuffer(mediaBuffer.Get()));

	*ppSample = sample.Detach();
	return S_OK;
}
