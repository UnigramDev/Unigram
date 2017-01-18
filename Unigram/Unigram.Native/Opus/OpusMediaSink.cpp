// Copyright (c) 2016 Lorenzo Rossoni

#include "pch.h"
#include "BufferLock.h"
#include "OpusMediaSink.h"

using namespace Unigram::Native;

HRESULT OpusMediaSink::RuntimeClassInitialize(OpusOutputByteStream* opusStream)
{
	HRESULT result;
	ReturnIfFailed(result, MakeAndInitialize<OpusStreamSink>(&m_streamSink, this, opusStream));

	return MediaSink::RuntimeClassInitialize();
}

DWORD OpusMediaSink::GetStreamSinkCount() noexcept
{
	return 1;
}

StreamSink* OpusMediaSink::GetStreamSinkByIndex(DWORD streamIndex) noexcept
{
	if (streamIndex != 0)
		return nullptr;

	return m_streamSink.Get();
}

StreamSink* OpusMediaSink::GetStreamSinkById(DWORD streamId) noexcept
{
	if (streamId != 0)
		return nullptr;

	return m_streamSink.Get();
}

HRESULT OpusMediaSink::OnStart()
{
	return S_OK;
}

HRESULT OpusMediaSink::OnPause()
{
	return S_OK;
}

HRESULT OpusMediaSink::OnStop()
{
	return S_OK;
}

HRESULT OpusMediaSink::OnShutdown()
{
	if (m_streamSink != nullptr)
		m_streamSink->Shutdown();

	m_streamSink.Reset();
	return S_OK;
}

HRESULT OpusMediaSink::OnSetProperties(ABI::Windows::Foundation::Collections::IPropertySet* configuration)
{
	return S_OK;
}


HRESULT OpusStreamSink::RuntimeClassInitialize(OpusMediaSink* mediaSink, OpusOutputByteStream* opusStream)
{
	m_opusStream = opusStream;
	return StreamSink::RuntimeClassInitialize(mediaSink);
}

DWORD OpusStreamSink::GetIdentifier() noexcept
{
	return 0;
}

const GUID& OpusStreamSink::GetMajorType() noexcept
{
	return MFMediaType_Audio;
}

DWORD OpusStreamSink::GetMediaTypeCount() noexcept
{
	return 1;
}

HRESULT OpusStreamSink::ValidateMediaType(IMFMediaType* mediaType)
{
	HRESULT result;
	GUID majorType;
	ReturnIfFailed(result, mediaType->GetMajorType(&majorType));

	if (majorType != MFMediaType_Audio)
		return MF_E_INVALIDMEDIATYPE;

	GUID subType;
	ReturnIfFailed(result, mediaType->GetGUID(MF_MT_SUBTYPE, &subType));

	if (subType != MFAudioFormat_PCM)
		return MF_E_INVALIDMEDIATYPE;

	UINT32 allSamplesIndependent;
	ReturnIfFailed(result, mediaType->GetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, &allSamplesIndependent));
	if (allSamplesIndependent == FALSE)
		return MF_E_INVALIDMEDIATYPE;

	UINT32 bitPerSample;
	ReturnIfFailed(result, mediaType->GetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, &bitPerSample));
	if (bitPerSample != 16)
		return MF_E_INVALIDMEDIATYPE;

	UINT32 channelCount;
	ReturnIfFailed(result, mediaType->GetUINT32(MF_MT_AUDIO_NUM_CHANNELS, &channelCount));

	UINT32 samplesPerSecond;
	ReturnIfFailed(result, mediaType->GetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, &samplesPerSecond));
	if (samplesPerSecond != OPUS_SAMPLES_PER_SECOND)
		return MF_E_INVALIDMEDIATYPE;

	UINT32 blockAlignment;
	ReturnIfFailed(result, mediaType->GetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, &blockAlignment));
	if (blockAlignment != channelCount * 2)
		return MF_E_INVALIDMEDIATYPE;

	UINT32 avgBytesPerSecond;
	ReturnIfFailed(result, mediaType->GetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, &avgBytesPerSecond));
	if (avgBytesPerSecond != blockAlignment * samplesPerSecond)
		return MF_E_INVALIDMEDIATYPE;

	return S_OK;
}

HRESULT OpusStreamSink::GetSupportedMediaType(DWORD index, IMFMediaType** ppMediaType)
{
	if (index > 0)
		return MF_E_NO_MORE_TYPES;

	HRESULT result;
	ComPtr<IMFMediaType> mediaType;
	ReturnIfFailed(result, MFCreateMediaType(&mediaType));
	ReturnIfFailed(result, mediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio));
	ReturnIfFailed(result, mediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, 1));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, OPUS_SAMPLES_PER_SECOND));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, 2));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, 2 * OPUS_SAMPLES_PER_SECOND));

	*ppMediaType = mediaType.Detach();
	return S_OK;
}

HRESULT OpusStreamSink::OnProcessSample(IMFSample* sample)
{
	HRESULT result;
	BufferLock bufferLock(sample);
	if (!bufferLock.IsValid())
		return MF_E_SINK_NO_SAMPLES_PROCESSED;

	ReturnIfFailed(result, m_opusStream->WriteFrame(bufferLock.GetBuffer(), bufferLock.GetLength()));

	return NotifyRequestSample();
}

HRESULT OpusStreamSink::OnMediaTypeChange(IMFMediaType* type)
{
	return m_opusStream->Initialize(type);
}

HRESULT OpusStreamSink::OnStart(MFTIME position)
{
	return NotifyRequestSample();
}

HRESULT OpusStreamSink::OnRestart(MFTIME position)
{
	return S_OK;
}

HRESULT OpusStreamSink::OnStop()
{
	return S_OK;
}

HRESULT OpusStreamSink::OnPause()
{
	return S_OK;
}

HRESULT OpusStreamSink::OnPlaceMarker(MFSTREAMSINK_MARKER_TYPE type, PROPVARIANT const* markerValue, PROPVARIANT const* contextValue)
{
	return S_OK;
}

HRESULT OpusStreamSink::OnFlush()
{
	return S_OK;
}

HRESULT OpusStreamSink::OnFinalize()
{
	return m_opusStream->Finalize();
}

HRESULT OpusStreamSink::OnShutdown()
{
	m_opusStream.Reset();
	return S_OK;
}
