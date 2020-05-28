// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "AsyncCallbackState.h"
#include "MediaSource.h"

using namespace Unigram::Native;

MediaSource::MediaSource() :
	m_state(MediaSourceState::None),
	m_workQueueId(0),
	m_activeStreamCount(0)
{
}

MediaSource::~MediaSource()
{
	Shutdown();
}

HRESULT MediaSource::RuntimeClassInitialize(IMFPresentationDescriptor* presentationDescriptor)
{
	if (presentationDescriptor == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	ReturnIfFailed(result, MFCreateEventQueue(&m_events));
	ReturnIfFailed(result, MFAllocateSerialWorkQueue(MFASYNC_CALLBACK_QUEUE_STANDARD, &m_workQueueId));

	m_presentationDescriptor = presentationDescriptor;
	m_state = MediaSourceState::Stopped;
	return S_OK;
}

HRESULT MediaSource::GetService(REFGUID guidService, REFIID riid, LPVOID* ppvObject)
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

HRESULT MediaSource::CreatePresentationDescriptor(IMFPresentationDescriptor** ppPresentationDescriptor)
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

HRESULT MediaSource::GetCharacteristics(DWORD* pdwCharacteristics)
{
	if (pdwCharacteristics == nullptr)
	{
		return E_POINTER;
	}

	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	*pdwCharacteristics = GetCharacteristics();
	return S_OK;
}

HRESULT MediaSource::BeginGetEvent(IMFAsyncCallback* caller, IUnknown* state)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->BeginGetEvent(caller, state);
}

HRESULT MediaSource::EndGetEvent(IMFAsyncResult* result, IMFMediaEvent** out)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->EndGetEvent(result, out);
}

HRESULT MediaSource::GetEvent(DWORD flags, IMFMediaEvent** out)
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

HRESULT MediaSource::QueueEvent(MediaEventType type, REFGUID guid, HRESULT status, PROPVARIANT const* val)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->QueueEventParamVar(type, guid, status, val);
}

HRESULT MediaSource::Start(IMFPresentationDescriptor* pPresentationDescriptor,
	GUID const* pguidTimeFormat, PROPVARIANT const* pvarStartPosition)
{
	if (pvarStartPosition == nullptr || pPresentationDescriptor == nullptr)
	{
		return E_INVALIDARG;
	}

	if (pguidTimeFormat != nullptr && *pguidTimeFormat != GUID_NULL)
		return MF_E_UNSUPPORTED_TIME_FORMAT;

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
	ReturnIfFailed(result, ValidatePresentationDescriptor(pPresentationDescriptor));

	if (pvarStartPosition->vt == VT_I8)
	{
		if (m_state > MediaSourceState::Stopped && !(pvarStartPosition->hVal.QuadPart == 0 || GetCharacteristics() & MFMEDIASOURCE_CAN_SEEK))
		{
			return MF_E_INVALIDREQUEST;
		}
	}
	else if (pvarStartPosition->vt != VT_EMPTY)
	{
		return MF_E_UNSUPPORTED_TIME_FORMAT;
	}

	auto startInfo = Make<StartInfo>(pPresentationDescriptor, pvarStartPosition);
	return AsyncCallbackState::QueueAsyncCallback<MediaSource, &MediaSource::OnAsyncStart>(this, m_workQueueId, startInfo.Get());
}

HRESULT MediaSource::MediaSource::Stop()
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

	return AsyncCallbackState::QueueAsyncCallback<MediaSource, &MediaSource::OnAsyncStop>(this, m_workQueueId);
}

HRESULT MediaSource::MediaSource::Pause()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (!(GetCharacteristics() & MFMEDIASOURCE_CAN_PAUSE))
	{
		return MF_E_INVALIDREQUEST;
	}

	if (m_state < MediaSourceState::Stopped)
	{
		return MF_E_NOT_INITIALIZED;
	}

	if (m_state != MediaSourceState::Started)
	{
		return MF_E_INVALID_STATE_TRANSITION;
	}

	return AsyncCallbackState::QueueAsyncCallback<MediaSource, &MediaSource::OnAsyncPause>(this, m_workQueueId);
}

HRESULT MediaSource::Shutdown()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaSourceState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ReturnIfFailed(result, OnShutdown());
	ReturnIfFailed(result, MFUnlockWorkQueue(m_workQueueId));

	if (m_events != nullptr)
		m_events->Shutdown();

	m_events.Reset();
	m_presentationDescriptor.Reset();
	m_state = MediaSourceState::Shutdown;
	return S_OK;
}

HRESULT MediaSource::Close()
{
	HRESULT result = Shutdown();
	if (result == MF_E_SHUTDOWN)
	{
		return RO_E_CLOSED;
	}

	return result;
}

HRESULT MediaSource::GetParameters(DWORD* pdwFlags, DWORD* pdwQueue)
{
	if (pdwFlags == nullptr || pdwQueue == nullptr)
	{
		return E_POINTER;
	}

	*pdwQueue = m_workQueueId;
	return S_OK;
}

HRESULT MediaSource::Invoke(IMFAsyncResult* pAsyncResult)
{
	auto lock = m_criticalSection.Lock();

	return AsyncCallbackState::CompleteAsyncCallback(pAsyncResult);
}

HRESULT MediaSource::GetSourceAttributes(IMFAttributes** ppAttributes)
{
	return E_NOTIMPL;
}

HRESULT MediaSource::GetStreamAttributes(DWORD dwStreamIdentifier, IMFAttributes** ppAttributes)
{
	return E_NOTIMPL;
}

HRESULT MediaSource::SetD3DManager(IUnknown* pManager)
{
	return E_NOTIMPL;
}

HRESULT MediaSource::NotifyEndOfStream()
{
	auto lock = m_criticalSection.Lock();

	if (--m_activeStreamCount == 0)
	{
		return m_events->QueueEventParamVar(MEEndOfPresentation, GUID_NULL, S_OK, nullptr);
	}

	return S_OK;
}

HRESULT MediaSource::NotifyError(HRESULT result)
{
	return m_events->QueueEventParamVar(MEError, GUID_NULL, result, nullptr);
}

HRESULT MediaSource::ValidatePresentationDescriptor(IMFPresentationDescriptor* presentationDescriptor)
{
	HRESULT result;
	DWORD streamCount;
	ReturnIfFailed(result, presentationDescriptor->GetStreamDescriptorCount(&streamCount));

	if (streamCount != GetMediaStreamCount())
	{
		return MF_E_OUT_OF_RANGE;
	}

	for (DWORD i = 0; i < streamCount; i++)
	{
		BOOL selected;
		ComPtr<IMFStreamDescriptor> streamDescriptor;
		ReturnIfFailed(result, presentationDescriptor->GetStreamDescriptorByIndex(i, &selected, &streamDescriptor));

		if (selected)
		{
			return S_OK;
		}
	}

	return MF_E_MEDIA_SOURCE_NO_STREAMS_SELECTED;
}

DWORD MediaSource::GetCharacteristics() noexcept
{
	return MFMEDIASOURCE_CAN_PAUSE;
}

HRESULT MediaSource::OnAsyncStart(IMFAsyncResult* asyncResult)
{
	HRESULT result;

	ComPtr<StartInfo> startInfo;
	if (FAILED(result = asyncResult->GetState(&startInfo)))
	{
		return m_events->QueueEventParamVar(MESourceStarted, GUID_NULL, result, nullptr);
	}

	do
	{
		if (startInfo->StartPosition.vt == VT_I8 && m_state > MediaSourceState::Stopped)
		{
			BreakIfFailed(result, OnSeek(startInfo->StartPosition.hVal.QuadPart));
		}
		else
		{
			BreakIfFailed(result, OnStart(startInfo->StartPosition.vt == VT_EMPTY ?
				PRESENTATION_CURRENT_POSITION : startInfo->StartPosition.hVal.QuadPart));
		}

		m_activeStreamCount = 0;

		DWORD streamDescriptorCount;
		BreakIfFailed(result, startInfo->PresentationDescriptor->GetStreamDescriptorCount(&streamDescriptorCount));

		for (DWORD i = 0; i < streamDescriptorCount; i++)
		{
			BOOL selected;
			ComPtr<IMFStreamDescriptor> streamDescriptor;
			BreakIfFailed(result, startInfo->PresentationDescriptor->GetStreamDescriptorByIndex(i, &selected, &streamDescriptor));

			if (selected)
			{
				DWORD streamId;
				BreakIfFailed(result, streamDescriptor->GetStreamIdentifier(&streamId));

				auto stream = GetMediaStreamById(streamId);
				if (stream == nullptr)
				{
					result = MF_E_INVALIDSTREAMNUMBER;
					break;
				}

				bool streamActivationState = stream->IsActive();
				BreakIfFailed(result, stream->Activate(true));

				m_activeStreamCount++;

				if (streamActivationState)
				{
					BreakIfFailed(result, m_events->QueueEventParamUnk(MEUpdatedStream, GUID_NULL, result, stream->CastToUnknown()));
				}
				else
				{
					BreakIfFailed(result, m_events->QueueEventParamUnk(MENewStream, GUID_NULL, result, stream->CastToUnknown()));
				}

				BreakIfFailed(result, stream->Start(&startInfo->StartPosition));
			}
		}
	} while (false);

	if (SUCCEEDED(result))
	{
		m_state = MediaSourceState::Started;
	}

	return m_events->QueueEventParamVar(MESourceStarted, GUID_NULL, result, &startInfo->StartPosition);
}

HRESULT MediaSource::OnAsyncStop(IMFAsyncResult* asyncResult)
{
	HRESULT result;

	do
	{
		for (DWORD i = 0; i < GetMediaStreamCount(); i++)
		{
			auto stream = GetMediaStreamByIndex(i);
			if (stream == nullptr)
			{
				result = MF_E_INVALIDSTREAMNUMBER;
				break;
			}

			if (stream->IsActive())
			{
				BreakIfFailed(result, stream->Stop());
			}
		}
	} while (false);

	if (SUCCEEDED(result))
	{
		m_state = MediaSourceState::Stopped;
	}

	return m_events->QueueEventParamVar(MESourceStopped, GUID_NULL, result, nullptr);
}

HRESULT MediaSource::OnAsyncPause(IMFAsyncResult* asyncResult)
{
	HRESULT result;

	do
	{
		for (DWORD i = 0; i < GetMediaStreamCount(); i++)
		{
			auto stream = GetMediaStreamByIndex(i);
			if (stream == nullptr)
			{
				result = MF_E_INVALIDSTREAMNUMBER;
				break;
			}

			if (stream->IsActive())
			{
				BreakIfFailed(result, stream->Pause());
			}
		}
	} while (false);

	if (SUCCEEDED(result))
	{
		m_state = MediaSourceState::Paused;
	}

	return m_events->QueueEventParamVar(MESourcePaused, GUID_NULL, result, nullptr);
}


MediaStream::MediaStream() :
	m_state(MediaStreamState::None),
	m_isActive(false)
{
}

MediaStream::~MediaStream()
{
	Shutdown();
}

HRESULT MediaStream::RuntimeClassInitialize(MediaSource* mediaSource, IMFStreamDescriptor* streamDescriptor)
{
	if (mediaSource == nullptr || streamDescriptor == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	ReturnIfFailed(result, MFCreateEventQueue(&m_events));

	m_mediaSource = mediaSource;
	m_mediaSource->CastToUnknown()->AddRef();

	m_streamDescriptor = streamDescriptor;
	m_state = MediaStreamState::Stopped;
	return S_OK;
}

HRESULT MediaStream::GetStreamDescriptor(IMFStreamDescriptor** ppStreamDescriptor)
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
		return E_UNEXPECTED;
	}

	return m_streamDescriptor.CopyTo(ppStreamDescriptor);
}

HRESULT MediaStream::BeginGetEvent(IMFAsyncCallback* caller, IUnknown* state)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->BeginGetEvent(caller, state);
}

HRESULT MediaStream::EndGetEvent(IMFAsyncResult* result, IMFMediaEvent** out)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->EndGetEvent(result, out);
}

HRESULT MediaStream::GetEvent(DWORD flags, IMFMediaEvent** out)
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

HRESULT MediaStream::QueueEvent(MediaEventType type, REFGUID guid, HRESULT status, PROPVARIANT const* val)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	return m_events->QueueEventParamVar(type, guid, status, val);
}

HRESULT MediaStream::GetMediaSource(IMFMediaSource** ppMediaSource)
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

	return m_mediaSource->CastToUnknown()->QueryInterface(IID_PPV_ARGS(ppMediaSource));
}

HRESULT MediaStream::RequestSample(IUnknown* pToken)
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

	if (IsEndOfStream())
	{
		return MF_E_END_OF_STREAM;
	}

	HRESULT result;
	if (FAILED(result = OnSampleRequested(pToken)))
	{
		return m_mediaSource->NotifyError(result);
	}

	return S_OK;
}

HRESULT MediaStream::Shutdown()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	ReturnIfFailed(result, OnShutdown());

	if (m_events != nullptr)
	{
		m_events->Shutdown();
	}

	if (m_mediaSource != nullptr)
	{
		m_mediaSource->CastToUnknown()->Release();
		m_mediaSource = nullptr;
	}

	m_events.Reset();
	m_streamDescriptor.Reset();
	m_queuedSamples = {};
	m_state = MediaStreamState::Shutdown;
	return S_OK;
}

HRESULT MediaStream::NotifyEndOfStream()
{
	HRESULT result;
	ReturnIfFailed(result, m_events->QueueEventParamVar(MEEndOfStream, GUID_NULL, S_OK, NULL));

	return m_mediaSource->NotifyEndOfStream();
}

HRESULT MediaStream::DeliverSample(IMFSample* sample)
{
	if (m_state == MediaStreamState::Started)
	{
		return m_events->QueueEventParamUnk(MEMediaSample, GUID_NULL, S_OK, sample);
	}
	else
	{
		m_queuedSamples.push(sample);
		return S_OK;
	}
}

HRESULT MediaStream::Activate(bool active)
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
		m_queuedSamples = {};
		return S_OK;
	}
}

HRESULT MediaStream::Start(PROPVARIANT const* position)
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	if (position->vt == VT_I8 && m_state > MediaStreamState::Stopped)
	{
		if (SUCCEEDED(result = OnSeek(position->hVal.QuadPart)))
		{
			m_state = MediaStreamState::Started;
			m_queuedSamples = {};
		}

		return m_events->QueueEventParamVar(MEStreamSeeked, GUID_NULL, result, position);
	}
	else
	{
		if (SUCCEEDED(result = OnStart(position->vt == VT_EMPTY ? PRESENTATION_CURRENT_POSITION : position->hVal.QuadPart)))
		{
			m_state = MediaStreamState::Started;

			ReturnIfFailed(result, m_events->QueueEventParamVar(MEStreamStarted, GUID_NULL, S_OK, position));

			return DeliverQueuedSamples();
		}
		else
		{
			return m_events->QueueEventParamVar(MEStreamStarted, GUID_NULL, result, position);
		}
	}
}

HRESULT MediaStream::Pause()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	if (m_state != MediaStreamState::Started)
	{
		return MF_E_INVALID_STATE_TRANSITION;
	}

	HRESULT result;
	if (SUCCEEDED(result = OnPause()))
	{
		m_state = MediaStreamState::Paused;
	}

	return m_events->QueueEventParamVar(MEStreamPaused, GUID_NULL, result, nullptr);
}

HRESULT MediaStream::Stop()
{
	auto lock = m_criticalSection.Lock();

	if (m_state == MediaStreamState::Shutdown)
	{
		return MF_E_SHUTDOWN;
	}

	HRESULT result;
	if (SUCCEEDED(result = OnStop()))
	{
		m_state = MediaStreamState::Stopped;
		m_queuedSamples = {};
	}

	return m_events->QueueEventParamVar(MEStreamPaused, GUID_NULL, result, nullptr);
}

HRESULT MediaStream::DeliverQueuedSamples()
{
	HRESULT result;

	while (!m_queuedSamples.empty())
	{
		auto sample = m_queuedSamples.front();
		ReturnIfFailed(result, m_events->QueueEventParamUnk(MEMediaSample, GUID_NULL, S_OK, sample.Get()));

		m_queuedSamples.pop();
	}

	return S_OK;
}