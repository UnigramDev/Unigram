// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "Helpers\MediaFoundationHelper.h"
#include "AsyncCallbackState.h"
#include "FinalizeAsyncOperation.h"
#include "MediaSink.h"

using namespace Unigram::Native;

MediaSink::MediaSink() :
	m_state(MediaSinkState::None)
{
}

MediaSink::~MediaSink()
{
	Shutdown();
}

HRESULT MediaSink::RuntimeClassInitialize()
{
	m_state = MediaSinkState::Stopped;
	return S_OK;
}

HRESULT MediaSink::SetProperties(ABI::Windows::Foundation::Collections::IPropertySet* pConfiguration)
{
	auto lock = m_criticalSection.Lock();

	return OnSetProperties(pConfiguration);
}

HRESULT MediaSink::AddStreamSink(DWORD dwStreamSinkIdentifier, IMFMediaType* pMediaType, IMFStreamSink** ppStreamSink)
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

	HRESULT result;
	StreamSink* streamSink;
	ReturnIfFailed(result, OnAddStream(dwStreamSinkIdentifier, pMediaType, &streamSink));

	*ppStreamSink = streamSink;
	return S_OK;
}

HRESULT MediaSink::GetCharacteristics(DWORD* pdwCharacteristics)
{
	if (pdwCharacteristics == nullptr)
		return E_POINTER;

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	*pdwCharacteristics = GetCharacteristics();
	return S_OK;
}

HRESULT MediaSink::GetPresentationClock(IMFPresentationClock** ppPresentationClock)
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
	{
		return MF_E_NO_CLOCK;
	}

	return m_clock.CopyTo(ppPresentationClock);
}

HRESULT MediaSink::GetStreamSinkById(DWORD dwStreamSinkIdentifier, IMFStreamSink** ppStreamSink)
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

	auto streamSink = GetStreamSinkById(dwStreamSinkIdentifier);
	if (streamSink == nullptr)
	{
		return MF_E_INVALIDSTREAMNUMBER;
	}

	return streamSink->CastToUnknown()->QueryInterface(ppStreamSink);
}

HRESULT MediaSink::GetStreamSinkByIndex(DWORD dwIndex, IMFStreamSink** ppStreamSink)
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

	auto streamSink = GetStreamSinkById(dwIndex);
	if (streamSink == nullptr)
	{
		return MF_E_OUT_OF_RANGE;
	}

	return streamSink->CastToUnknown()->QueryInterface(ppStreamSink);
}

HRESULT MediaSink::GetStreamSinkCount(DWORD* pcStreamSinkCount)
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

	*pcStreamSinkCount = GetStreamSinkCount();
	return S_OK;
}

HRESULT MediaSink::RemoveStreamSink(DWORD dwStreamSinkIdentifier)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return OnRemoveStream(dwStreamSinkIdentifier);
}

HRESULT MediaSink::SetPresentationClock(IMFPresentationClock* pPresentationClock)
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

HRESULT MediaSink::Shutdown()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ReturnIfFailed(result, OnShutdown());

	m_clock.Reset();
	m_state = MediaSinkState::Shutdown;
	return S_OK;
}

HRESULT MediaSink::Close()
{
	HRESULT result = Shutdown();
	if (result == MF_E_SHUTDOWN)
	{
		return RO_E_CLOSED;
	}

	return result;
}

HRESULT MediaSink::OnClockPause(MFTIME hnsSystemTime)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	for (DWORD i = 0; i < GetStreamSinkCount(); i++)
	{
		auto stream = GetStreamSinkByIndex(i);
		if (stream == nullptr)
		{
			return MF_E_INVALIDSTREAMNUMBER;
		}

		ReturnIfFailed(result, stream->Pause());
	}

	m_state = MediaSinkState::Paused;
	return S_OK;
}

HRESULT MediaSink::OnClockRestart(MFTIME hnsSystemTime)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	for (DWORD i = 0; i < GetStreamSinkCount(); i++)
	{
		auto stream = GetStreamSinkByIndex(i);
		if (stream == nullptr)
		{
			return MF_E_INVALIDSTREAMNUMBER;
		}

		ReturnIfFailed(result, stream->Restart(hnsSystemTime));
	}

	m_state = MediaSinkState::Started;
	return S_OK;
}

HRESULT MediaSink::OnClockSetRate(MFTIME hnsSystemTime, float flRate)
{
	return S_OK;
}

HRESULT MediaSink::OnClockStart(MFTIME hnsSystemTime, LONGLONG llClockStartOffset)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	for (DWORD i = 0; i < GetStreamSinkCount(); i++)
	{
		auto stream = GetStreamSinkByIndex(i);
		if (stream == nullptr)
		{
			return MF_E_INVALIDSTREAMNUMBER;
		}

		ReturnIfFailed(result, stream->Start(hnsSystemTime, llClockStartOffset));
	}

	m_state = MediaSinkState::Started;
	return S_OK;
}

HRESULT MediaSink::OnClockStop(MFTIME hnsSystemTime)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	for (DWORD i = 0; i < GetStreamSinkCount(); i++)
	{
		auto stream = GetStreamSinkByIndex(i);
		if (stream == nullptr)
		{
			return MF_E_INVALIDSTREAMNUMBER;
		}

		ReturnIfFailed(result, stream->Stop());
	}

	m_state = MediaSinkState::Stopped;
	return S_OK;
}

HRESULT MediaSink::BeginFinalize(IMFAsyncCallback* pCallback, IUnknown* punkState)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	auto streamCount = GetStreamSinkCount();
	ComPtr<FinalizeAsyncOperation> finalizeAsyncOperation;
	ReturnIfFailed(result, MakeAndInitialize<FinalizeAsyncOperation>(&finalizeAsyncOperation, pCallback, punkState, streamCount));

	for (DWORD i = 0; i < streamCount; i++)
	{
		auto stream = GetStreamSinkByIndex(i);
		if (stream == nullptr)
		{
			return MF_E_INVALIDSTREAMNUMBER;
		}

		BreakIfFailed(result, stream->Finalize(finalizeAsyncOperation.Get()));
	}

	if (FAILED(result))
	{
		return finalizeAsyncOperation->Cancel(result);
	}

	return S_OK;
}

HRESULT MediaSink::EndFinalize(IMFAsyncResult* pResult)
{
	if (pResult == nullptr)
	{
		return E_INVALIDARG;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ReturnIfFailed(result, pResult->GetStatus());

	m_state = MediaSinkState::Finalized;
	return S_OK;
}

DWORD MediaSink::GetCharacteristics() noexcept
{
	return MEDIASINK_FIXED_STREAMS | MEDIASINK_RATELESS;
}

HRESULT MediaSink::OnAddStream(DWORD streamSinkIdentifier, IMFMediaType* mediaType, StreamSink** streamSink)
{
	return MF_E_STREAMSINKS_FIXED;
}

HRESULT MediaSink::OnRemoveStream(DWORD streamSinkIdentifier)
{
	return MF_E_STREAMSINKS_FIXED;
}


StreamSink::StreamSink() :
	m_state(StreamSinkState::None),
	m_workQueueId(0)
{
}

StreamSink::~StreamSink()
{
	Shutdown();
}

HRESULT StreamSink::RuntimeClassInitialize(MediaSink* mediaSink, IMFMediaType* mediaType)
{
	HRESULT result;
	ReturnIfFailed(result, MFCreateEventQueue(&m_events));
	ReturnIfFailed(result, MFAllocateSerialWorkQueue(MFASYNC_CALLBACK_QUEUE_STANDARD, &m_workQueueId));

	m_mediaSink = mediaSink;
	m_mediaSink->CastToUnknown()->AddRef();

	m_mediaType = mediaType;
	m_state = StreamSinkState::Stopped;
	return S_OK;
}

HRESULT StreamSink::BeginGetEvent(IMFAsyncCallback* caller, IUnknown* state)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->BeginGetEvent(caller, state);
}

HRESULT StreamSink::EndGetEvent(IMFAsyncResult* result, IMFMediaEvent** out)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->EndGetEvent(result, out);
}

HRESULT StreamSink::GetEvent(DWORD flags, IMFMediaEvent** out)
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

HRESULT StreamSink::QueueEvent(MediaEventType type, REFGUID guid, HRESULT status, PROPVARIANT const* val)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->QueueEventParamVar(type, guid, status, val);
}

HRESULT StreamSink::SetCurrentMediaType(IMFMediaType* pMediaType)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	if (pMediaType == nullptr)
	{
		ReturnIfFailed(result, OnMediaTypeChange(nullptr));

		m_mediaType.Reset();
	}
	else
	{
		if (m_mediaType != nullptr)
		{
			BOOL areEquals;
			ReturnIfFailed(result, m_mediaType->Compare(pMediaType, MF_ATTRIBUTES_MATCH_ALL_ITEMS, &areEquals));

			if (areEquals)
			{
				return S_OK;
			}
		}

		ReturnIfFailed(result, ValidateMediaType(pMediaType));
		ReturnIfFailed(result, OnMediaTypeChange(pMediaType));

		m_mediaType = pMediaType;
	}

	return S_OK;
}

HRESULT StreamSink::GetCurrentMediaType(IMFMediaType** ppMediaType)
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

HRESULT StreamSink::GetMajorType(GUID* pguidMajorType)
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

	*pguidMajorType = GetMajorType();
	return S_OK;
}

HRESULT StreamSink::GetMediaTypeCount(DWORD* pdwTypeCount)
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

	*pdwTypeCount = GetMediaTypeCount();
	return S_OK;
}

HRESULT StreamSink::GetMediaTypeByIndex(DWORD dwIndex, IMFMediaType** ppType)
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

	return GetSupportedMediaType(dwIndex, ppType);
}

HRESULT StreamSink::IsMediaTypeSupported(IMFMediaType* pMediaType, IMFMediaType** ppMediaType)
{
	if (pMediaType == nullptr)
	{
		return E_INVALIDARG;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
		return MF_E_SHUTDOWN;

	HRESULT result;
	if (FAILED(result = ValidateMediaType(pMediaType)))
	{
		if (ppMediaType != nullptr)
		{
			if (m_mediaType == nullptr)
			{
				*ppMediaType = nullptr;
			}
			else
			{
				CloneMediaType(m_mediaType.Get(), ppMediaType);
			}
		}

		return result;
	}

	return S_OK;
}

HRESULT StreamSink::Flush()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ReturnIfFailed(result, OnFlush());

	m_queuedSamples = {};
	return S_OK;
}

HRESULT StreamSink::GetMediaSink(IMFMediaSink** ppMediaSink)
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

	return m_mediaSink->CastToUnknown()->QueryInterface(ppMediaSink);
}

HRESULT StreamSink::GetIdentifier(DWORD* pdwIdentifier)
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

	*pdwIdentifier = GetIdentifier();
	return S_OK;
}

HRESULT StreamSink::GetMediaTypeHandler(IMFMediaTypeHandler** ppHandler)
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

	return CastToUnknown()->QueryInterface(IID_PPV_ARGS(ppHandler));
}

HRESULT StreamSink::ProcessSample(IMFSample* pSample)
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

	return AsyncCallbackState::QueueAsyncCallback<StreamSink, &StreamSink::OnAsyncProcessSamples>(this, m_workQueueId, pSample);
}

HRESULT StreamSink::PlaceMarker(MFSTREAMSINK_MARKER_TYPE eMarkerType, PROPVARIANT const* pvarMarkerValue, PROPVARIANT const* pvarContextValue)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	auto markerInfo = Make<MarkerInfo>(eMarkerType, pvarMarkerValue, pvarContextValue);
	return AsyncCallbackState::QueueAsyncCallback<StreamSink, &StreamSink::OnAsyncPlaceMarker>(this, m_workQueueId, markerInfo.Get());
}

HRESULT StreamSink::GetParameters(DWORD* pdwFlags, DWORD* pdwQueue)
{
	if (pdwFlags == nullptr || pdwQueue == nullptr)
	{
		return E_POINTER;
	}

	*pdwQueue = m_workQueueId;
	return S_OK;
}

HRESULT StreamSink::Invoke(IMFAsyncResult* pAsyncResult)
{
	auto lock = m_criticalSection.Lock();

	return AsyncCallbackState::CompleteAsyncCallback(pAsyncResult);
}

HRESULT StreamSink::Shutdown()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ReturnIfFailed(result, OnShutdown());
	ReturnIfFailed(result, MFUnlockWorkQueue(m_workQueueId));

	if (m_events != nullptr)
		m_events->Shutdown();

	if (m_mediaSink != nullptr)
	{
		m_mediaSink->CastToUnknown()->Release();
		m_mediaSink = nullptr;
	}

	m_mediaType.Reset();
	m_events.Reset();
	m_queuedSamples = {};
	m_state = StreamSinkState::Shutdown;

	return S_OK;
}

HRESULT StreamSink::NotifyRequestSample()
{
	return m_events->QueueEventParamVar(MEStreamSinkRequestSample, GUID_NULL, S_OK, nullptr);
}

HRESULT StreamSink::NotifyError(HRESULT result)
{
	return m_events->QueueEventParamVar(MEError, GUID_NULL, result, nullptr);
}

HRESULT StreamSink::Start(MFTIME position, LONGLONG clockStartOffset)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	auto startInfo = Make<StartInfo>(position, clockStartOffset);
	return AsyncCallbackState::QueueAsyncCallback<StreamSink, &StreamSink::OnAsyncStart>(this, m_workQueueId, startInfo.Get());
}

HRESULT StreamSink::Restart(MFTIME position)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	auto startInfo = Make<StartInfo>(position, 0);
	return AsyncCallbackState::QueueAsyncCallback<StreamSink, &StreamSink::OnAsyncRestart>(this, m_workQueueId, startInfo.Get());
}

HRESULT StreamSink::Pause()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return AsyncCallbackState::QueueAsyncCallback<StreamSink, &StreamSink::OnAsyncPause>(this, m_workQueueId);
}

HRESULT StreamSink::Stop()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return AsyncCallbackState::QueueAsyncCallback<StreamSink, &StreamSink::OnAsyncStop>(this, m_workQueueId);
}

HRESULT StreamSink::OnAsyncStart(IMFAsyncResult* asyncResult)
{
	HRESULT result;
	ComPtr<StartInfo> startInfo;
	if (SUCCEEDED(result = asyncResult->GetState(&startInfo)) &&
		SUCCEEDED(result = OnStart(startInfo->StartPosition)))
	{
		m_state = StreamSinkState::Started;
		result = ProcessQueuedSamples();
	}

	return m_events->QueueEventParamVar(MEStreamSinkStarted, GUID_NULL, result, nullptr);
}

HRESULT StreamSink::OnAsyncRestart(IMFAsyncResult* asyncResult)
{
	HRESULT result;
	ComPtr<StartInfo> startInfo;
	if (SUCCEEDED(result = asyncResult->GetState(&startInfo)) &&
		SUCCEEDED(result = OnRestart(startInfo->StartPosition)))
	{
		m_state = StreamSinkState::Started;
		result = ProcessQueuedSamples();
	}

	return m_events->QueueEventParamVar(MEStreamSinkStarted, GUID_NULL, result, nullptr);
}

HRESULT StreamSink::OnAsyncStop(IMFAsyncResult* asyncResult)
{
	HRESULT result;
	if (SUCCEEDED(result = OnStop()))
	{
		m_state = StreamSinkState::Stopped;
		m_queuedSamples = {};
	}

	return m_events->QueueEventParamVar(MEStreamSinkStopped, GUID_NULL, result, nullptr);
}

HRESULT StreamSink::OnAsyncPause(IMFAsyncResult* asyncResult)
{
	HRESULT result;
	if (SUCCEEDED(result = OnPause()))
		m_state = StreamSinkState::Paused;

	return m_events->QueueEventParamVar(MEStreamSinkPaused, GUID_NULL, result, nullptr);
}

HRESULT StreamSink::OnAsyncPlaceMarker(IMFAsyncResult* asyncResult)
{
	HRESULT result;
	ComPtr<MarkerInfo> markerInfo;
	if (SUCCEEDED(result = asyncResult->GetState(&markerInfo)) &&
		SUCCEEDED(result = OnPlaceMarker(markerInfo->Type, markerInfo->MarkerValue.get(), markerInfo->ContextValue.get())))
	{
		return m_events->QueueEventParamVar(MEStreamSinkMarker, GUID_NULL, S_OK, markerInfo->ContextValue.get());
	}

	return m_events->QueueEventParamVar(MEStreamSinkMarker, GUID_NULL, result, nullptr);
}

HRESULT StreamSink::OnAsyncProcessSamples(IMFAsyncResult* asyncResult)
{
	HRESULT result;
	ComPtr<IMFSample> sample;
	if (SUCCEEDED(result = asyncResult->GetState(&sample)))
	{
		if (m_state == StreamSinkState::Started)
		{
			if (SUCCEEDED(result = OnProcessSample(sample.Get())))
			{
				return S_OK;
			}
		}
		else
		{
			m_queuedSamples.push(sample);
			return S_OK;
		}
	}

	return m_events->QueueEventParamVar(MEError, GUID_NULL, result, nullptr);
}

HRESULT StreamSink::OnAsyncFinalize(IMFAsyncResult* asyncResult)
{
	HRESULT result;
	ComPtr<IMFAsyncResult> finalizeAsyncResult;
	if (SUCCEEDED(result = asyncResult->GetState(&finalizeAsyncResult)))
	{
		if (m_state == StreamSinkState::Finalized)
		{
			result = S_OK;
		}
		else if (SUCCEEDED(result = OnFinalize()))
		{
			m_state = StreamSinkState::Finalized;
		}

		finalizeAsyncResult->SetStatus(result);
		return MFInvokeCallback(finalizeAsyncResult.Get());
	}

	return m_events->QueueEventParamVar(MEError, GUID_NULL, result, nullptr);
}

HRESULT StreamSink::ProcessQueuedSamples()
{
	HRESULT result;
	while (!m_queuedSamples.empty())
	{
		auto sample = m_queuedSamples.front();
		ReturnIfFailed(result, OnProcessSample(sample.Get()));

		m_queuedSamples.pop();
	}

	return S_OK;
}

HRESULT StreamSink::Finalize(IMFAsyncCallback* callback)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == StreamSinkState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ComPtr<IMFAsyncResult> asyncResult;
	ReturnIfFailed(result, MFCreateAsyncResult(nullptr, callback, nullptr, &asyncResult));

	return AsyncCallbackState::QueueAsyncCallback<StreamSink, &StreamSink::OnAsyncFinalize>(this, m_workQueueId, asyncResult.Get());
}