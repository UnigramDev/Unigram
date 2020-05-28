// Copyright(c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "Helpers\MediaFoundationHelper.h"
#include "AsyncCallbackState.h"
#include "MediaCapturePreview.h"

using namespace Unigram::Native;

MediaCapturePreviewMediaSink::MediaCapturePreviewMediaSink() :
	m_state(MediaSinkState::None)
{
}

MediaCapturePreviewMediaSink::~MediaCapturePreviewMediaSink()
{
	Shutdown();
}

HRESULT MediaCapturePreviewMediaSink::RuntimeClassInitialize(MediaCapturePreviewSource^ previewSource, IMFMediaType* mediaType)
{
	HRESULT result;
	ReturnIfFailed(result, MFCreateEventQueue(&m_events));
	ReturnIfFailed(result, MakeAndInitialize<MediaCapturePreviewStreamSink>(&m_stream, this, mediaType));

	m_previewSource = previewSource;
	m_state = MediaSinkState::Stopped;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSink::SetProperties(ABI::Windows::Foundation::Collections::IPropertySet* configuration)
{
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSink::GetCharacteristics(DWORD* pdwCharacteristics)
{
	if (pdwCharacteristics == nullptr)
	{
		return E_POINTER;
	}

	*pdwCharacteristics = MEDIASINK_RATELESS | MEDIASINK_FIXED_STREAMS | MEDIASINK_CLOCK_REQUIRED;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSink::Shutdown()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	m_stream.Reset();
	m_clock.Reset();

	m_state = MediaSinkState::Shutdown;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSink::GetPresentationClock(IMFPresentationClock** ppPresentationClock)
{
	if (ppPresentationClock == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (m_clock == nullptr)
		return MF_E_NO_CLOCK;
	{ }

	return m_clock.CopyTo(ppPresentationClock);
}

HRESULT MediaCapturePreviewMediaSink::SetPresentationClock(IMFPresentationClock* pPresentationClock)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result = S_OK;

	if (m_clock != nullptr)
	{
		result = m_clock->RemoveClockStateSink(this);
	}

	if (SUCCEEDED(result) || pPresentationClock != nullptr)
	{
		result = pPresentationClock->AddClockStateSink(this);
	}

	if (SUCCEEDED(result))
	{
		m_clock = pPresentationClock;
	}

	return result;
}

HRESULT MediaCapturePreviewMediaSink::GetStreamSinkById(DWORD dwStreamSinkIdentifier, IMFStreamSink** ppStreamSink)
{
	if (ppStreamSink == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (dwStreamSinkIdentifier != 0)
	{
		return MF_E_INVALIDSTREAMNUMBER;
	}

	return m_stream.CopyTo(ppStreamSink);
}

HRESULT MediaCapturePreviewMediaSink::GetStreamSinkByIndex(DWORD dwIndex, IMFStreamSink** ppStreamSink)
{
	if (ppStreamSink == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (dwIndex != 0)
	{
		return MF_E_OUT_OF_RANGE;
	}

	return m_stream.CopyTo(ppStreamSink);
}

HRESULT MediaCapturePreviewMediaSink::GetStreamSinkCount(DWORD* pcStreamSinkCount)
{
	if (pcStreamSinkCount == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	*pcStreamSinkCount = 1;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSink::AddStreamSink(DWORD dwStreamSinkIdentifier, IMFMediaType* pMediaType, IMFStreamSink** ppStreamSink)
{
	if (ppStreamSink == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return MF_E_STREAMSINKS_FIXED;
}

HRESULT MediaCapturePreviewMediaSink::RemoveStreamSink(DWORD dwStreamSinkIdentifier)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return MF_E_STREAMSINKS_FIXED;
}

HRESULT MediaCapturePreviewMediaSink::OnClockPause(MFTIME hnsSystemTime)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return S_OK;
}

HRESULT MediaCapturePreviewMediaSink::OnClockRestart(MFTIME hnsSystemTime)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ReturnIfFailed(result, m_stream->Start(hnsSystemTime, 0));

	m_state = MediaSinkState::Started;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSink::OnClockSetRate(MFTIME hnsSystemTime, float flRate)
{
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSink::OnClockStart(MFTIME hnsSystemTime, LONGLONG llClockStartOffset)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ReturnIfFailed(result, m_stream->Start(hnsSystemTime, llClockStartOffset));

	m_state = MediaSinkState::Started;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSink::OnClockStop(MFTIME hnsSystemTime)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ReturnIfFailed(result, m_stream->Stop());

	m_state = MediaSinkState::Stopped;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSink::RequestSample()
{
	auto lock = m_criticalSection.Lock();

	return m_stream->RequestSample();
}


MediaCapturePreviewMediaSource::MediaCapturePreviewMediaSource() :
	m_state(MediaSourceState::None),
	m_workQueueId(0)
{
}

MediaCapturePreviewMediaSource::~MediaCapturePreviewMediaSource()
{
	Shutdown();
}

HRESULT MediaCapturePreviewMediaSource::RuntimeClassInitialize(MediaCapturePreviewSource^ previewSource, IMFMediaType* mediaType)
{
	HRESULT result;
	ReturnIfFailed(result, MFCreateEventQueue(&m_events));
	ReturnIfFailed(result, MFAllocateSerialWorkQueue(MFASYNC_CALLBACK_QUEUE_STANDARD, &m_workQueueId));
	ReturnIfFailed(result, MakeAndInitialize<MediaCapturePreviewMediaStream>(&m_stream, this, mediaType));

	ComPtr<IMFStreamDescriptor> streamDescriptor;
	ReturnIfFailed(result, m_stream->GetStreamDescriptor(&streamDescriptor));
	ReturnIfFailed(result, MFCreatePresentationDescriptor(1, streamDescriptor.GetAddressOf(), &m_presentationDescriptor));
	ReturnIfFailed(result, m_presentationDescriptor->SelectStream(0));

	m_previewSource = previewSource;
	m_state = MediaSourceState::Stopped;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSource::BeginGetEvent(IMFAsyncCallback* caller, IUnknown* state)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->BeginGetEvent(caller, state);
}

HRESULT MediaCapturePreviewMediaSource::EndGetEvent(IMFAsyncResult* result, IMFMediaEvent** out)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->EndGetEvent(result, out);
}

HRESULT MediaCapturePreviewMediaSource::GetEvent(DWORD flags, IMFMediaEvent** out)
{
	ComPtr<IMFMediaEventQueue> events;
	{
		auto lock = m_criticalSection.Lock();

		if (m_state == MediaSourceState::Shutdown)
		{
			return MF_E_SHUTDOWN;
		}

		events = m_events.Get();
	}

	return events->GetEvent(flags, out);
}

HRESULT MediaCapturePreviewMediaSource::QueueEvent(MediaEventType type, REFGUID guid, HRESULT status, PROPVARIANT const* val)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->QueueEventParamVar(type, guid, status, val);
}

HRESULT MediaCapturePreviewMediaSource::GetCharacteristics(DWORD* pdwCharacteristics)
{
	if (pdwCharacteristics == nullptr)
	{
		return E_POINTER;
	}

	*pdwCharacteristics = MFMEDIASOURCE_IS_LIVE | MFMEDIASOURCE_DOES_NOT_USE_NETWORK;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSource::CreatePresentationDescriptor(IMFPresentationDescriptor** ppPresentationDescriptor)
{
	if (ppPresentationDescriptor == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (m_presentationDescriptor == nullptr)
	{
		return MF_E_NOT_INITIALIZED;
	}

	return m_presentationDescriptor->Clone(ppPresentationDescriptor);
}

HRESULT MediaCapturePreviewMediaSource::Start(IMFPresentationDescriptor* pPresentationDescriptor,
	GUID const* pguidTimeFormat, PROPVARIANT const* pvarStartPosition)
{
	if (pvarStartPosition == nullptr || pPresentationDescriptor == nullptr)
	{
		return E_INVALIDARG;
	}

	if (pguidTimeFormat != nullptr && *pguidTimeFormat != GUID_NULL)
	{
		return MF_E_UNSUPPORTED_TIME_FORMAT;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (m_state < MediaSourceState::Stopped)
	{
		return MF_E_NOT_INITIALIZED;
	}

	HRESULT result;
	DWORD streamCount;
	ReturnIfFailed(result, pPresentationDescriptor->GetStreamDescriptorCount(&streamCount));

	if (streamCount != 1)
	{
		return MF_E_OUT_OF_RANGE;
	}

	BOOL selected;
	ComPtr<IMFStreamDescriptor> streamDescriptor;
	ReturnIfFailed(result, pPresentationDescriptor->GetStreamDescriptorByIndex(0, &selected, &streamDescriptor));

	if (!selected)
	{
		return MF_E_MEDIA_SOURCE_NO_STREAMS_SELECTED;
	}

	if (pvarStartPosition->vt == VT_I8)
	{
		if (m_state > MediaSourceState::Stopped && !(pvarStartPosition->hVal.QuadPart == 0))
		{
			return MF_E_INVALIDREQUEST;
		}
	}
	else if (pvarStartPosition->vt != VT_EMPTY)
	{
		return MF_E_UNSUPPORTED_TIME_FORMAT;
	}

	auto startInfo = Make<StartInfo>(pPresentationDescriptor, pvarStartPosition);
	return AsyncCallbackState::QueueAsyncCallback<MediaCapturePreviewMediaSource, &MediaCapturePreviewMediaSource::OnAsyncStart>(this, m_workQueueId, startInfo.Get());
}

HRESULT MediaCapturePreviewMediaSource::Stop()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (m_state < MediaSourceState::Stopped)
	{
		return MF_E_NOT_INITIALIZED;
	}

	return AsyncCallbackState::QueueAsyncCallback<MediaCapturePreviewMediaSource, &MediaCapturePreviewMediaSource::OnAsyncStop>(this, m_workQueueId);
}

HRESULT MediaCapturePreviewMediaSource::Pause()
{
	return MF_E_INVALIDREQUEST;
}

HRESULT MediaCapturePreviewMediaSource::Shutdown()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ReturnIfFailed(result, MFUnlockWorkQueue(m_workQueueId));

	if (m_events != nullptr)
	{
		m_events->Shutdown();
	}

	m_stream.Reset();
	m_events.Reset();
	m_presentationDescriptor.Reset();

	m_state = MediaSourceState::Shutdown;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSource::GetService(REFGUID guidService, REFIID riid, LPVOID* ppvObject)
{
	if (ppvObject == nullptr)
	{
		return E_POINTER;
	}

	if (guidService != MF_MEDIASOURCE_SERVICE)
	{
		return MF_E_UNSUPPORTED_SERVICE;
	}

	return CastToUnknown()->QueryInterface(riid, ppvObject);
}

HRESULT MediaCapturePreviewMediaSource::GetParameters(DWORD* pdwFlags, DWORD* pdwQueue)
{
	if (pdwFlags == nullptr || pdwQueue == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	*pdwQueue = m_workQueueId;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaSource::Invoke(IMFAsyncResult* pAsyncResult)
{
	auto lock = m_criticalSection.Lock();

	return AsyncCallbackState::CompleteAsyncCallback(pAsyncResult);
}

HRESULT MediaCapturePreviewMediaSource::OnAsyncStart(IMFAsyncResult* asyncResult)
{
	HRESULT result;

	ComPtr<MediaCapturePreviewMediaSource::StartInfo> startInfo;
	if (FAILED(result = asyncResult->GetState(&startInfo)))
		return m_events->QueueEventParamVar(MESourceStarted, GUID_NULL, result, nullptr);

	do
	{
		BOOL selected;
		ComPtr<IMFStreamDescriptor> streamDescriptor;
		BreakIfFailed(result, startInfo->PresentationDescriptor->GetStreamDescriptorByIndex(0, &selected, &streamDescriptor));

		if (selected)
		{
			DWORD streamId;
			BreakIfFailed(result, streamDescriptor->GetStreamIdentifier(&streamId));

			if (streamId != 0)
			{
				result = MF_E_INVALIDSTREAMNUMBER;
				break;
			}

			bool streamActivationState = m_stream->IsActive();
			BreakIfFailed(result, m_stream->Activate(true));

			if (streamActivationState)
			{
				BreakIfFailed(result, m_events->QueueEventParamUnk(MEUpdatedStream, GUID_NULL, result, m_stream->CastToUnknown()));
			}
			else
			{
				BreakIfFailed(result, m_events->QueueEventParamUnk(MENewStream, GUID_NULL, result, m_stream->CastToUnknown()));
			}

			BreakIfFailed(result, m_stream->Start(&startInfo->StartPosition));
		}
	} while (false);

	if (SUCCEEDED(result))
	{
		m_state = MediaSourceState::Started;
	}

	return m_events->QueueEventParamVar(MESourceStarted, GUID_NULL, result, &startInfo->StartPosition);
}

HRESULT MediaCapturePreviewMediaSource::OnAsyncStop(IMFAsyncResult* asyncResult)
{
	HRESULT result;
	if (SUCCEEDED(result = m_stream->Stop()))
	{
		m_state = MediaSourceState::Stopped;
	}

	return m_events->QueueEventParamVar(MESourceStopped, GUID_NULL, result, nullptr);
}

HRESULT MediaCapturePreviewMediaSource::NotifyEndOfStream()
{
	auto lock = m_criticalSection.Lock();

	return m_events->QueueEventParamVar(MEEndOfPresentation, GUID_NULL, S_OK, nullptr);
}

HRESULT MediaCapturePreviewMediaSource::DeliverSample(IMFSample* sample)
{
	auto lock = m_criticalSection.Lock();

	return m_stream->DeliverSample(sample);
}


MediaCapturePreviewStreamSink::MediaCapturePreviewStreamSink() :
	m_state(StreamSinkState::None),
	m_workQueueId(0)
{
}

MediaCapturePreviewStreamSink::~MediaCapturePreviewStreamSink()
{
	Shutdown();
}

HRESULT MediaCapturePreviewStreamSink::RuntimeClassInitialize(MediaCapturePreviewMediaSink* mediaSink, IMFMediaType* mediaType)
{
	HRESULT result;
	ReturnIfFailed(result, MFCreateEventQueue(&m_events));
	ReturnIfFailed(result, MFAllocateSerialWorkQueue(MFASYNC_CALLBACK_QUEUE_STANDARD, &m_workQueueId));

	m_mediaSink = mediaSink;
	m_mediaType = mediaType;
	m_state = StreamSinkState::Stopped;
	return S_OK;
}

HRESULT MediaCapturePreviewStreamSink::BeginGetEvent(IMFAsyncCallback* caller, IUnknown* state)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->BeginGetEvent(caller, state);
}

HRESULT MediaCapturePreviewStreamSink::EndGetEvent(IMFAsyncResult* result, IMFMediaEvent** out)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->EndGetEvent(result, out);
}

HRESULT MediaCapturePreviewStreamSink::GetEvent(DWORD flags, IMFMediaEvent** out)
{
	ComPtr<IMFMediaEventQueue> events;
	{
		auto lock = m_criticalSection.Lock();

		if (m_state == StreamSinkState::Shutdown)
		{
			return MF_E_SHUTDOWN;
		}

		events = m_events.Get();
	}

	return events->GetEvent(flags, out);
}

HRESULT MediaCapturePreviewStreamSink::QueueEvent(MediaEventType type, REFGUID guid, HRESULT status, PROPVARIANT const* val)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->QueueEventParamVar(type, guid, status, val);
}

HRESULT MediaCapturePreviewStreamSink::Shutdown()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ReturnIfFailed(result, MFUnlockWorkQueue(m_workQueueId));

	if (m_events != nullptr)
	{
		m_events->Shutdown();
	}

	m_events.Reset();
	m_mediaSink.Reset();

	m_state = StreamSinkState::Shutdown;
	return S_OK;
}

HRESULT MediaCapturePreviewStreamSink::Start(MFTIME position, LONGLONG clockStartOffset)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return AsyncCallbackState::QueueAsyncCallback<MediaCapturePreviewStreamSink, &MediaCapturePreviewStreamSink::OnAsyncStart>(this, m_workQueueId);
}

HRESULT MediaCapturePreviewStreamSink::Stop()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return AsyncCallbackState::QueueAsyncCallback<MediaCapturePreviewStreamSink, &MediaCapturePreviewStreamSink::OnAsyncStop>(this, m_workQueueId);
}

HRESULT MediaCapturePreviewStreamSink::GetParameters(DWORD* pdwFlags, DWORD* pdwQueue)
{
	if (pdwFlags == nullptr || pdwQueue == nullptr)
	{
		return E_POINTER;
	}

	*pdwQueue = m_workQueueId;
	return S_OK;
}

HRESULT MediaCapturePreviewStreamSink::Invoke(IMFAsyncResult* pAsyncResult)
{
	auto lock = m_criticalSection.Lock();

	return AsyncCallbackState::CompleteAsyncCallback(pAsyncResult);
}

HRESULT MediaCapturePreviewStreamSink::SetCurrentMediaType(IMFMediaType* pMediaType)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (pMediaType == nullptr)
	{
		m_mediaType.Reset();
	}
	else
	{
		if (m_mediaType != nullptr)
		{
			BOOL areEquals;
			HRESULT result;
			ReturnIfFailed(result, m_mediaType->Compare(pMediaType, MF_ATTRIBUTES_MATCH_ALL_ITEMS, &areEquals));

			if (areEquals)
			{
				return S_OK;
			}
		}

		m_mediaType = pMediaType;
	}

	return S_OK;
}

HRESULT MediaCapturePreviewStreamSink::GetCurrentMediaType(IMFMediaType** ppMediaType)
{
	if (ppMediaType == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (m_mediaType == nullptr)
	{
		return MF_E_NOT_INITIALIZED;
	}

	return m_mediaType.CopyTo(ppMediaType);
}

HRESULT MediaCapturePreviewStreamSink::GetMajorType(GUID* pguidMajorType)
{
	if (pguidMajorType == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	*pguidMajorType = MFMediaType_Video;
	return S_OK;
}

HRESULT MediaCapturePreviewStreamSink::GetMediaTypeCount(DWORD* pdwTypeCount)
{
	if (pdwTypeCount == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	*pdwTypeCount = 1;
	return S_OK;
}

HRESULT MediaCapturePreviewStreamSink::GetMediaTypeByIndex(DWORD dwIndex, IMFMediaType** ppType)
{
	if (ppType == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (m_mediaType == nullptr)
	{
		return MF_E_NOT_INITIALIZED;
	}

	if (dwIndex > 0)
	{
		return MF_E_NO_MORE_TYPES;
	}

	return CloneMediaType(m_mediaType.Get(), ppType);
}

HRESULT MediaCapturePreviewStreamSink::IsMediaTypeSupported(IMFMediaType* pMediaType, IMFMediaType** ppMediaType)
{
	if (pMediaType == nullptr)
	{
		return E_INVALIDARG;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	BOOL areEquals;
	HRESULT result;
	ReturnIfFailed(result, m_mediaType->Compare(pMediaType, MF_ATTRIBUTES_MATCH_ALL_ITEMS, &areEquals));

	if (areEquals)
	{
		return S_OK;
	}

	if (ppMediaType != nullptr)
		CloneMediaType(m_mediaType.Get(), ppMediaType);

	return MF_E_INVALIDMEDIATYPE;
}

HRESULT MediaCapturePreviewStreamSink::Flush()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return S_OK;
}

HRESULT MediaCapturePreviewStreamSink::GetMediaSink(IMFMediaSink** ppMediaSink)
{
	if (ppMediaSink == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (m_mediaSink == nullptr)
	{
		return E_UNEXPECTED;
	}

	return m_mediaSink.CopyTo(ppMediaSink);
}

HRESULT MediaCapturePreviewStreamSink::GetIdentifier(DWORD* pdwIdentifier)
{
	if (pdwIdentifier == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	*pdwIdentifier = 0;
	return S_OK;
}

HRESULT MediaCapturePreviewStreamSink::GetMediaTypeHandler(IMFMediaTypeHandler** ppHandler)
{
	if (ppHandler == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return CastToUnknown()->QueryInterface(ppHandler);
}

HRESULT MediaCapturePreviewStreamSink::ProcessSample(IMFSample* pSample)
{
	if (pSample == nullptr)
	{
		return E_INVALIDARG;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return AsyncCallbackState::QueueAsyncCallback<MediaCapturePreviewStreamSink, &MediaCapturePreviewStreamSink::OnAsyncProcessSamples>(this, m_workQueueId, pSample);
}

HRESULT MediaCapturePreviewStreamSink::PlaceMarker(MFSTREAMSINK_MARKER_TYPE eMarkerType, PROPVARIANT const* pvarMarkerValue, PROPVARIANT const* pvarContextValue)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	auto markerInfo = Make<MarkerInfo>(eMarkerType, pvarMarkerValue, pvarContextValue);
	return AsyncCallbackState::QueueAsyncCallback<MediaCapturePreviewStreamSink, &MediaCapturePreviewStreamSink::OnAsyncPlaceMarker>(this, m_workQueueId, markerInfo.Get());
}

HRESULT MediaCapturePreviewStreamSink::OnAsyncStart(IMFAsyncResult* asyncResult)
{
	m_state = StreamSinkState::Started;

	HRESULT result = m_events->QueueEventParamVar(MEStreamSinkRequestSample, GUID_NULL, S_OK, nullptr);
	return m_events->QueueEventParamVar(MEStreamSinkStarted, GUID_NULL, result, nullptr);
}

HRESULT MediaCapturePreviewStreamSink::OnAsyncStop(IMFAsyncResult* asyncResult)
{
	m_state = StreamSinkState::Stopped;

	return m_events->QueueEventParamVar(MEStreamSinkStopped, GUID_NULL, S_OK, nullptr);
}

HRESULT MediaCapturePreviewStreamSink::OnAsyncPlaceMarker(IMFAsyncResult* asyncResult)
{
	HRESULT result;
	ComPtr<MarkerInfo> markerInfo;
	if (SUCCEEDED(result = asyncResult->GetState(&markerInfo)))
	{
		return m_events->QueueEventParamVar(MEStreamSinkMarker, GUID_NULL, S_OK, markerInfo->ContextValue.get());
	}

	return m_events->QueueEventParamVar(MEStreamSinkMarker, GUID_NULL, result, nullptr);
}

HRESULT MediaCapturePreviewStreamSink::OnAsyncProcessSamples(IMFAsyncResult* asyncResult)
{
	HRESULT result;
	ComPtr<IMFSample> sample;
	if (SUCCEEDED(result = asyncResult->GetState(&sample)))
	{
		if (m_state == StreamSinkState::Started)
		{
			if (SUCCEEDED(m_mediaSink->GetPreviewSource()->DeliverSample(sample.Get())))
			{
				return S_OK;
			}

			return m_events->QueueEventParamVar(MEStreamSinkRequestSample, GUID_NULL, S_OK, nullptr);
		}
		else
		{
			return S_OK;
		}
	}

	return m_events->QueueEventParamVar(MEError, GUID_NULL, result, nullptr);
}

HRESULT MediaCapturePreviewStreamSink::RequestSample()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Started)
	{
		return m_events->QueueEventParamVar(MEStreamSinkRequestSample, GUID_NULL, S_OK, nullptr);
	}

	return MF_E_NOT_INITIALIZED;
}


MediaCapturePreviewMediaStream::MediaCapturePreviewMediaStream() :
	m_state(MediaStreamState::None),
	m_isActive(false),
	m_workQueueId(0)
{
}

MediaCapturePreviewMediaStream::~MediaCapturePreviewMediaStream()
{
	Shutdown();
}

HRESULT MediaCapturePreviewMediaStream::RuntimeClassInitialize(MediaCapturePreviewMediaSource* mediaSource, IMFMediaType* mediaType)
{
	HRESULT result;
	ReturnIfFailed(result, MFCreateEventQueue(&m_events));
	ReturnIfFailed(result, MFAllocateSerialWorkQueue(MFASYNC_CALLBACK_QUEUE_STANDARD, &m_workQueueId));
	ReturnIfFailed(result, MFCreateStreamDescriptor(0, 1, &mediaType, &m_streamDescriptor));

	ComPtr<IMFMediaTypeHandler> mediaTypeHandler;
	ReturnIfFailed(result, m_streamDescriptor->GetMediaTypeHandler(&mediaTypeHandler));
	ReturnIfFailed(result, mediaTypeHandler->SetCurrentMediaType(mediaType));

	m_mediaSource = mediaSource;
	m_state = MediaStreamState::Stopped;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaStream::BeginGetEvent(IMFAsyncCallback* caller, IUnknown* state)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->BeginGetEvent(caller, state);
}

HRESULT MediaCapturePreviewMediaStream::EndGetEvent(IMFAsyncResult* result, IMFMediaEvent** out)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->EndGetEvent(result, out);
}

HRESULT MediaCapturePreviewMediaStream::GetEvent(DWORD flags, IMFMediaEvent** out)
{
	ComPtr<IMFMediaEventQueue> events;
	{
		auto lock = m_criticalSection.Lock();

		if (m_state == MediaStreamState::Shutdown)
		{
			return MF_E_SHUTDOWN;
		}

		events = m_events.Get();
	}

	return events->GetEvent(flags, out);
}

HRESULT MediaCapturePreviewMediaStream::QueueEvent(MediaEventType type, REFGUID guid, HRESULT status, PROPVARIANT const* val)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->QueueEventParamVar(type, guid, status, val);
}

HRESULT MediaCapturePreviewMediaStream::Shutdown()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ReturnIfFailed(result, MFUnlockWorkQueue(m_workQueueId));

	if (m_events != nullptr)
		m_events->Shutdown();

	m_events.Reset();
	m_mediaSource.Reset();
	m_streamDescriptor.Reset();

	//m_queuedSamples = {};

	m_state = MediaStreamState::Shutdown;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaStream::GetParameters(DWORD* pdwFlags, DWORD* pdwQueue)
{
	if (pdwFlags == nullptr || pdwQueue == nullptr)
	{
		return E_POINTER;
	}

	*pdwQueue = m_workQueueId;
	return S_OK;
}

HRESULT MediaCapturePreviewMediaStream::Invoke(IMFAsyncResult* pAsyncResult)
{
	auto lock = m_criticalSection.Lock();

	return m_mediaSource->GetPreviewSource()->RequestSample(pAsyncResult->GetStateNoAddRef());;
}

HRESULT MediaCapturePreviewMediaStream::GetStreamDescriptor(IMFStreamDescriptor** ppStreamDescriptor)
{
	if (ppStreamDescriptor == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (m_streamDescriptor == nullptr)
	{
		return MF_E_NOT_INITIALIZED;
	}

	return m_streamDescriptor.CopyTo(ppStreamDescriptor);
}

HRESULT MediaCapturePreviewMediaStream::GetMediaSource(IMFMediaSource** ppMediaSource)
{
	if (ppMediaSource == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (m_mediaSource == nullptr)
	{
		return E_UNEXPECTED;
	}

	return m_mediaSource.CopyTo(ppMediaSource);
}

HRESULT MediaCapturePreviewMediaStream::RequestSample(IUnknown* pToken)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (m_state == MediaStreamState::Stopped || !m_isActive)
	{
		return MF_E_INVALIDREQUEST;
	}

	HRESULT result;

	do
	{
		ComPtr<IMFAsyncResult> asyncResult;
		BreakIfFailed(result, MFCreateAsyncResult(nullptr, this, pToken, &asyncResult));
		BreakIfFailed(result, MFPutWorkItemEx2(m_workQueueId, 0, asyncResult.Get()));

		return m_events->QueueEventParamVar(MEStreamSinkRequestSample, GUID_NULL, S_OK, nullptr);
	} while (false);

	return m_events->QueueEventParamVar(MEError, GUID_NULL, result, nullptr);
}

HRESULT MediaCapturePreviewMediaStream::Start(PROPVARIANT const* position)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	m_state = MediaStreamState::Started;

	return m_events->QueueEventParamVar(MEStreamStarted, GUID_NULL, S_OK, position);
}

HRESULT MediaCapturePreviewMediaStream::Stop()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	m_state = MediaStreamState::Stopped;

	return m_events->QueueEventParamVar(MEStreamStopped, GUID_NULL, S_OK, nullptr);
}

HRESULT MediaCapturePreviewMediaStream::Activate(bool active)
{
	auto lock = m_criticalSection.Lock();

	if (m_isActive == active)
	{
		return S_OK;
	}

	if (active)
	{
		m_isActive = true;
		return S_OK;
	}
	else
	{
		m_isActive = false;

		return S_OK;
	}
}

HRESULT MediaCapturePreviewMediaStream::DeliverSample(IMFSample* sample)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Started)
	{
		return m_events->QueueEventParamUnk(MEMediaSample, GUID_NULL, S_OK, sample);
	}

	return MF_E_NOT_INITIALIZED;
}


MediaCapturePreviewSource::MediaCapturePreviewSource()
{
}

MediaCapturePreviewSource::~MediaCapturePreviewSource()
{
	m_mediaSink.Reset();
	m_mediaSource.Reset();
}

Windows::Media::IMediaExtension^ MediaCapturePreviewSource::MediaSink::get()
{
	if (m_mediaSink == nullptr)
	{
		throw ref new Platform::ObjectDisposedException();
	}

	ComPtr<ABI::Windows::Media::IMediaExtension> mediaSink;
	ThrowIfFailed(m_mediaSink.As(&mediaSink));

	return reinterpret_cast<Windows::Media::IMediaExtension^>(mediaSink.Get());
}

Windows::Media::Core::IMediaSource^ MediaCapturePreviewSource::MediaSource::get()
{
	if (m_mediaSource == nullptr)
	{
		throw ref new Platform::ObjectDisposedException();
	}

	ComPtr<ABI::Windows::Media::Core::IMediaSource> mediaSource;
	ThrowIfFailed(m_mediaSource.As(&mediaSource));

	return reinterpret_cast<Windows::Media::Core::IMediaSource^>(mediaSource.Get());
}

HRESULT MediaCapturePreviewSource::Initialize(IVideoEncodingProperties* videoEncodingProperties)
{
	HRESULT result;
	ComPtr<IMFMediaType> mediaType;
	ReturnIfFailed(result, MFCreateMediaTypeFromProperties(videoEncodingProperties, &mediaType));

	ReturnIfFailed(result, MakeAndInitialize<MediaCapturePreviewMediaSink>(&m_mediaSink, this, mediaType.Get()));
	ReturnIfFailed(result, MakeAndInitialize<MediaCapturePreviewMediaSource>(&m_mediaSource, this, mediaType.Get()));

	return S_OK;
}

HRESULT MediaCapturePreviewSource::RequestSample(IUnknown* token)
{
	return m_mediaSink->RequestSample();
}

HRESULT MediaCapturePreviewSource::DeliverSample(IMFSample* sample)
{
	return m_mediaSource->DeliverSample(sample);
}

MediaCapturePreviewSource^ MediaCapturePreviewSource::CreateFromVideoEncodingProperties(Windows::Media::MediaProperties::VideoEncodingProperties^ videoEncodingProperties)
{
	auto previewSource = ref new MediaCapturePreviewSource();
	ThrowIfFailed(previewSource->Initialize(reinterpret_cast<IVideoEncodingProperties*>(videoEncodingProperties)));

	return previewSource;
}