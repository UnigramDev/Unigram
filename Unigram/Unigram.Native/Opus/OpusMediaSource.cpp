// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "BufferLock.h"
#include "Helpers\MediaFoundationHelper.h"
#include "OpusMediaSource.h"

using namespace Unigram::Native;

OpusMediaSource::OpusMediaSource() :
	m_characteristics(MFMEDIASOURCE_CAN_PAUSE)
{
}

HRESULT OpusMediaSource::RuntimeClassInitialize(OpusInputByteStream* opusStream)
{
	HRESULT result;
	DWORD capabilities;
	ReturnIfFailed(result, opusStream->GetCapabilities(&capabilities));

	m_characteristics |= GetMediaSourceCharacteristicsFromByteStreamCapabilities(capabilities);

	ComPtr<IMFPresentationDescriptor> presentationDescriptor;
	ReturnIfFailed(result, MakeAndInitialize<OpusMediaStream>(&m_mediaStream, this, opusStream, &presentationDescriptor));

	return MediaSource::RuntimeClassInitialize(presentationDescriptor.Get());
}

DWORD OpusMediaSource::GetCharacteristics() noexcept
{
	return m_characteristics;
}

DWORD OpusMediaSource::GetMediaStreamCount() noexcept
{
	return 1;
}

MediaStream* OpusMediaSource::GetMediaStreamByIndex(DWORD streamIndex) noexcept
{
	if (streamIndex != 0)
		return nullptr;

	return m_mediaStream.Get();
}

MediaStream* OpusMediaSource::GetMediaStreamById(DWORD streamId) noexcept
{
	if (streamId != 0)
		return nullptr;

	return m_mediaStream.Get();
}

HRESULT OpusMediaSource::OnStart(MFTIME position)
{
	return S_OK;
}

HRESULT OpusMediaSource::OnSeek(MFTIME position)
{
	return S_OK;
}

HRESULT OpusMediaSource::OnPause()
{
	return S_OK;
}

HRESULT OpusMediaSource::OnStop()
{
	return S_OK;
}

HRESULT OpusMediaSource::OnShutdown()
{
	if (m_mediaStream != nullptr)
	{
		m_mediaStream->Shutdown();
	}

	m_mediaStream.Reset();
	return S_OK;
}


OpusMediaStream::OpusMediaStream() :
	m_workQueueId(0),
	m_isEndOfStream(false),
	m_currentTime(0)
{
}

HRESULT OpusMediaStream::RuntimeClassInitialize(OpusMediaSource* mediaSource, OpusInputByteStream* opusStream, IMFPresentationDescriptor** ppPresentationDescriptor)
{
	HRESULT result;
	ComPtr<IMFMediaType> mediaType;
	ReturnIfFailed(result, opusStream->ReadMediaType(&mediaType));
	ReturnIfFailed(result, mediaType->GetUINT32(MF_MT_AUDIO_NUM_CHANNELS, &m_channelCount));
	ReturnIfFailed(result, mediaType->GetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, &m_sampleRate));

	LONGLONG duration;
	ReturnIfFailed(result, opusStream->GetDuration(&duration));

	ComPtr<IMFStreamDescriptor> streamDescriptor;
	ReturnIfFailed(result, MFCreateStreamDescriptor(0, 1, mediaType.GetAddressOf(), &streamDescriptor));

	ComPtr<IMFMediaTypeHandler> mediaTypeHandler;
	ReturnIfFailed(result, streamDescriptor->GetMediaTypeHandler(&mediaTypeHandler));
	ReturnIfFailed(result, mediaTypeHandler->SetCurrentMediaType(mediaType.Get()));

	ComPtr<IMFPresentationDescriptor> presentationDescriptor;
	ReturnIfFailed(result, MFCreatePresentationDescriptor(1, streamDescriptor.GetAddressOf(), &presentationDescriptor));
	ReturnIfFailed(result, presentationDescriptor->SetUINT64(MF_PD_DURATION, duration));
	ReturnIfFailed(result, presentationDescriptor->SelectStream(0));

	ReturnIfFailed(result, MFAllocateSerialWorkQueue(MFASYNC_CALLBACK_QUEUE_STANDARD, &m_workQueueId));

	m_opusStream = opusStream;
	*ppPresentationDescriptor = presentationDescriptor.Detach();
	return MediaStream::RuntimeClassInitialize(mediaSource, streamDescriptor.Get());
}

HRESULT OpusMediaStream::GetParameters(DWORD* pdwFlags, DWORD* pdwQueue)
{
	if (pdwFlags == nullptr || pdwQueue == nullptr)
	{
		return E_POINTER;
	}

	*pdwQueue = m_workQueueId;
	return S_OK;
}

HRESULT OpusMediaStream::Invoke(IMFAsyncResult* pAsyncResult)
{
	auto lock = GetCriticalSection().Lock();

	auto state = GetState();
	if (state < MediaStreamState::Paused)
	{
		return S_OK;
	}

	HRESULT result;

	do
	{
		DWORD readSamples;
		std::vector<int16> buffer(OPUS_DECODER_BUFFER_LENGTH);
		BreakIfFailed(result, m_opusStream->ReadSamples(buffer.data(), OPUS_DECODER_BUFFER_LENGTH, &readSamples));

		if (readSamples > 0)
		{
			auto totalSamples = readSamples * m_channelCount;
			auto bufferSize = totalSamples * 2;
			ComPtr<IMFMediaBuffer> sampleBuffer;
			BreakIfFailed(result, MFCreateMemoryBuffer(bufferSize, &sampleBuffer));

			{
				BufferLock bufferlock(sampleBuffer.Get());
				if (!bufferlock.IsValid())
				{
					result = E_FAIL;
					break;
				}

				CopyMemory(bufferlock.GetBuffer(), buffer.data(), bufferSize);
			}

			BreakIfFailed(result, sampleBuffer->SetCurrentLength(bufferSize));

			ComPtr<IMFSample> sample;
			BreakIfFailed(result, MFCreateSample(&sample));
			BreakIfFailed(result, sample->AddBuffer(sampleBuffer.Get()));

			auto duration = static_cast<LONGLONG>((10000000.0 * readSamples) / m_sampleRate);
			BreakIfFailed(result, sample->SetSampleTime(m_currentTime));
			BreakIfFailed(result, sample->SetSampleDuration(duration));

			ComPtr<IUnknown> token;
			if (SUCCEEDED(pAsyncResult->GetState(&token)))
			{
				BreakIfFailed(result, sample->SetUnknown(MFSampleExtension_Token, token.Get()));
			}

			BreakIfFailed(result, DeliverSample(sample.Get()));
			m_currentTime += duration;
		}
		else
		{
			BreakIfFailed(result, NotifyEndOfStream());

			m_isEndOfStream = true;
		}

	} while (false);

	if (FAILED(result))
	{
		return NotifyError(result);
	}

	return S_OK;
}

bool OpusMediaStream::IsEndOfStream() noexcept
{
	return m_isEndOfStream;
}

HRESULT OpusMediaStream::OnSampleRequested(IUnknown* token)
{
	return MFPutWorkItem2(m_workQueueId, 0, this, token);
}

HRESULT OpusMediaStream::OnStart(MFTIME position)
{
	if (position != PRESENTATION_CURRENT_POSITION)
	{
		HRESULT result;
		ReturnIfFailed(result, m_opusStream->Seek(position));

		m_currentTime = position;
		m_isEndOfStream = false;
	}

	return S_OK;
}

HRESULT OpusMediaStream::OnSeek(MFTIME position)
{
	return OnStart(position);
}

HRESULT OpusMediaStream::OnPause()
{
	return S_OK;
}

HRESULT OpusMediaStream::OnStop()
{
	return S_OK;
}

HRESULT OpusMediaStream::OnShutdown()
{
	HRESULT result;
	ReturnIfFailed(result, MFUnlockWorkQueue(m_workQueueId));

	m_opusStream.Reset();
	return S_OK;
}