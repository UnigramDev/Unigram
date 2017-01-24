// Copyright (c) 2016 Lorenzo Rossoni

#include "pch.h"
#include <Mferror.h>
#include "OpusInputByteStream.h"

using namespace Unigram::Native;
using namespace Opus;

const OpusFileCallbacks OpusInputByteStream::s_reader = {
	OpusInputByteStream::DecoderReadCallback,
	OpusInputByteStream::DecoderSeekCallback,
	OpusInputByteStream::DecoderTellCallback,
	OpusInputByteStream::DecoderCloseCallback
};

OpusInputByteStream::OpusInputByteStream() :
	m_opusFile(nullptr),
	m_header(nullptr)
{
}

OpusInputByteStream::~OpusInputByteStream()
{
	Close();
}

HRESULT OpusInputByteStream::RuntimeClassInitialize(IMFByteStream* byteStream)
{
	HRESULT result;
	DWORD capabilities;
	ReturnIfFailed(result, byteStream->GetCapabilities(&capabilities));

	if (!(capabilities & MFBYTESTREAM_IS_READABLE))
		return MF_E_UNSUPPORTED_BYTESTREAM_TYPE;

	m_byteStream = byteStream;
	m_opusFile = op_test_callbacks(this, &s_reader, nullptr, 0, nullptr);
	if (m_opusFile == nullptr)
		return MF_E_UNSUPPORTED_BYTESTREAM_TYPE;

	if (op_test_open(m_opusFile) < 0)
		return Close();

	return S_OK;
}

HRESULT OpusInputByteStream::Close()
{
	if (m_opusFile != nullptr)
	{
		op_free(m_opusFile);
		m_opusFile = nullptr;
	}

	m_header = nullptr;
	m_byteStream.Reset();
	return S_OK;
}

HRESULT OpusInputByteStream::ReadMediaType(IMFMediaType** ppMediaType)
{
	if (ppMediaType == nullptr)
		return E_POINTER;

	if (m_opusFile == nullptr)
		return MF_E_NOT_INITIALIZED;

	if (m_header == nullptr)
	{
		m_header = op_head(m_opusFile, -1);
		if (m_header == nullptr)
			return  E_FAIL;
	}

	HRESULT result;
	ComPtr<IMFMediaType> mediaType;
	ReturnIfFailed(result, MFCreateMediaType(&mediaType));
	ReturnIfFailed(result, mediaType->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio));
	ReturnIfFailed(result, mediaType->SetGUID(MF_MT_SUBTYPE, MFAudioFormat_PCM));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_ALL_SAMPLES_INDEPENDENT, TRUE));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, m_header->channel_count));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, OPUS_SAMPLES_PER_SECOND));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, 16));

	auto blockAlign = m_header->channel_count * 2;
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, blockAlign));
	ReturnIfFailed(result, mediaType->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, blockAlign * OPUS_SAMPLES_PER_SECOND));

	*ppMediaType = mediaType.Detach();
	return S_OK;
}

HRESULT OpusInputByteStream::Seek(LONGLONG position)
{
	if (m_opusFile == nullptr)
		return MF_E_NOT_INITIALIZED;

	if (op_pcm_seek(m_opusFile, static_cast<LONGLONG>((position * OPUS_SAMPLES_PER_SECOND) / 10000000.0f)) != 0)
		return E_FAIL;

	return S_OK;
}

HRESULT OpusInputByteStream::ReadSamples(int16* buffer, DWORD bufferSize, DWORD* pReadSamples)
{
	if (buffer == nullptr)
		return E_POINTER;

	if (m_opusFile == nullptr)
		return MF_E_NOT_INITIALIZED;

	auto readSamples = op_read(m_opusFile, buffer, bufferSize, nullptr);
	if (readSamples < 0)
		return E_FAIL;

	if (pReadSamples != nullptr)
		*pReadSamples = static_cast<DWORD>(readSamples);

	return S_OK;
}

HRESULT OpusInputByteStream::GetDuration(LONGLONG* pDuration)
{
	if (pDuration == nullptr)
		return E_POINTER;

	if (m_opusFile == nullptr)
		return MF_E_NOT_INITIALIZED;

	auto length = op_pcm_total(m_opusFile, -1);
	if (length < 0)
		return E_FAIL;

	*pDuration = static_cast<LONGLONG>((10000000.0f * length) / OPUS_SAMPLES_PER_SECOND);
	return S_OK;
}

int OpusInputByteStream::DecoderReadCallback(void* datasource, unsigned char* ptr, int nbytes)
{
	auto instance = reinterpret_cast<OpusInputByteStream*>(datasource);
	if (instance->m_byteStream == nullptr)
		return -1;

	ULONG readBytes;
	if (FAILED(instance->m_byteStream->Read(reinterpret_cast<byte*>(ptr), nbytes, &readBytes)))
		return -1;

	return readBytes;
}

int OpusInputByteStream::DecoderSeekCallback(void* datasource, int64_t offset, int whence)
{
	QWORD currentPosition;
	auto instance = reinterpret_cast<OpusInputByteStream*>(datasource);
	if (instance->m_byteStream == nullptr)
		return -1;

	switch (whence)
	{
	case SEEK_SET:
		if (FAILED(instance->m_byteStream->Seek(MFBYTESTREAM_SEEK_ORIGIN::msoBegin, offset, 0, &currentPosition)))
			return -1;
		break;
	case SEEK_CUR:
		if (FAILED(instance->m_byteStream->Seek(MFBYTESTREAM_SEEK_ORIGIN::msoCurrent, offset, 0, &currentPosition)))
			return -1;
		break;
	case SEEK_END:
		if (FAILED(instance->m_byteStream->GetLength(&currentPosition)))
			return -1;

		if (FAILED(instance->m_byteStream->Seek(MFBYTESTREAM_SEEK_ORIGIN::msoBegin, currentPosition + offset, 0, &currentPosition)))
			return -1;
		break;
	default:
		return -1;
	}

	return 0;
}

int OpusInputByteStream::DecoderCloseCallback(void* datasource)
{
	auto instance = reinterpret_cast<OpusInputByteStream*>(datasource);
	if (instance->m_byteStream == nullptr)
		return -1;

	if (FAILED(instance->m_byteStream->Close()))
		return -1;

	return 0;
}

int64_t OpusInputByteStream::DecoderTellCallback(void* datasource)
{
	QWORD currentPosition;
	auto instance = reinterpret_cast<OpusInputByteStream*>(datasource);
	if (instance->m_byteStream == nullptr)
		return -1;

	if (FAILED(instance->m_byteStream->GetCurrentPosition(&currentPosition)))
		return -1;

	return currentPosition;
}