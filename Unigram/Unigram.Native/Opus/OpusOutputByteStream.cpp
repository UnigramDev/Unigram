// Copyright (c) 2016 Lorenzo Rossoni

#include "pch.h"
#include <mferror.h>
#include "OpusOutputByteStream.h"

using namespace Unigram::Native;
using namespace Opus;

#define FRAME_SIZE 960
#define COMMENT_PADDING 512

OpusOutputByteStream::OpusOutputByteStream() :
	m_opusEncoder(nullptr),
	m_header({}),
	m_oggStreamState({}),
	m_oggPage({}),
	m_oggPacket({}),
	m_lastSegments(0),
	m_sizeSegments(0),
	m_totalSamples(0),
	m_lastGranulePosition(0),
	m_encoderGranulePosition(0)
{
}

OpusOutputByteStream::~OpusOutputByteStream()
{
	Close();
}

HRESULT OpusOutputByteStream::RuntimeClassInitialize(IMFByteStream* byteStream)
{
	HRESULT result;
	DWORD capabilities;
	ReturnIfFailed(result, byteStream->GetCapabilities(&capabilities));

	if (!(capabilities & MFBYTESTREAM_IS_WRITABLE))
		return MF_E_UNSUPPORTED_BYTESTREAM_TYPE;

	m_byteStream = byteStream;

	return S_OK;
}

HRESULT OpusOutputByteStream::Close()
{
	if (m_opusEncoder != nullptr)
	{
		opus_encoder_destroy(m_opusEncoder);
		m_opusEncoder = nullptr;
	}

	ogg_stream_clear(&m_oggStreamState);

	if (m_byteStream != nullptr)
	{
		HRESULT result;
		ReturnIfFailed(result, m_byteStream->Close());
	}

	m_byteStream.Reset();
	return S_OK;
}

HRESULT OpusOutputByteStream::Initialize(IMFMediaType* mediaType)
{
	if (m_opusEncoder != nullptr)
		return E_UNEXPECTED;

	HRESULT result;
	UINT32 channelCount;
	ReturnIfFailed(result, mediaType->GetUINT32(MF_MT_AUDIO_NUM_CHANNELS, &channelCount));

	UINT32 samplesPerSecond;
	ReturnIfFailed(result, mediaType->GetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, &samplesPerSecond));

	int opusResult;
	m_opusEncoder = opus_encoder_create(samplesPerSecond, channelCount, OPUS_APPLICATION_AUDIO, &opusResult);
	if (opusResult != OPUS_OK)
		return OpusResultToHRESULT(opusResult);

	opus_int32 lookahead;
	opusResult = opus_encoder_ctl(m_opusEncoder, OPUS_GET_LOOKAHEAD(&lookahead));
	if (result != OPUS_OK)
		return OpusResultToHRESULT(opusResult);

	m_lastSegments = 0;
	m_sizeSegments = 0;
	m_totalSamples = 0;
	m_lastGranulePosition = 0;
	m_encoderGranulePosition = 0;
	m_header.channel_count = channelCount;
	m_header.input_sample_rate = samplesPerSecond;
	m_header.stream_count = 1;
	m_header.pre_skip = static_cast<int>(lookahead * (static_cast<double>(OPUS_SAMPLES_PER_SECOND) / samplesPerSecond));
	m_oggPacket.packetno = 2;

	m_inputBuffer.reserve(FRAME_SIZE * sizeof(WORD) * channelCount);
	m_outputBuffer.resize((1275 * 3 + 7) * channelCount);

	if (ogg_stream_init(&m_oggStreamState, rand()) < 0)
		return E_FAIL;

	ReturnIfFailed(result, WriteOpusHeader(&m_header));

	return WriteOpusComments(opus_get_version_string());
}

HRESULT OpusOutputByteStream::WriteFrame(byte const* buffer, DWORD bufferLength)
{
	const DWORD bytesPerFrame = FRAME_SIZE * sizeof(WORD) * m_header.channel_count;

	HRESULT result;
	auto inputBufferLength = static_cast<DWORD>(m_inputBuffer.size());
	if (inputBufferLength > 0)
	{
		auto newInputBufferLength = std::min(inputBufferLength + bufferLength, bytesPerFrame);
		m_inputBuffer.resize(newInputBufferLength);

		auto bytesToCopy = newInputBufferLength - inputBufferLength;
		CopyMemory(m_inputBuffer.data() + inputBufferLength, buffer, bytesToCopy);

		buffer += bytesToCopy;
		bufferLength -= bytesToCopy;

		if (newInputBufferLength == bytesPerFrame)
		{
			ReturnIfFailed(result, WriteOpusFrame(m_inputBuffer.data(), bytesPerFrame));
			m_inputBuffer.clear();
		}
	}

	while (bufferLength >= bytesPerFrame)
	{
		ReturnIfFailed(result, WriteOpusFrame(buffer, bytesPerFrame));

		buffer += bytesPerFrame;
		bufferLength -= bytesPerFrame;
	}

	if (bufferLength > 0)
	{
		m_inputBuffer.resize(bufferLength);
		CopyMemory(m_inputBuffer.data(), buffer, bufferLength);
	}

	return S_OK;
}

HRESULT OpusOutputByteStream::WriteOpusFrame(byte const* buffer, DWORD bufferLength)
{
	const DWORD bytesPerSample = sizeof(WORD) * m_header.channel_count;

	auto sampleCount = bufferLength / bytesPerSample;
	if (sampleCount > FRAME_SIZE)
		return MF_E_INVALID_STREAM_DATA;

	m_totalSamples += sampleCount;

	if (sampleCount < FRAME_SIZE)
	{
		m_oggPacket.e_o_s = 1;
	}
	else
	{
		m_oggPacket.e_o_s = 0;
	}

	opus_int16 encodedBytes;
	if (bufferLength > 0)
	{
		if (sampleCount < FRAME_SIZE)
		{
			std::vector<byte> paddedBuffer(FRAME_SIZE * bytesPerSample);
			CopyMemory(paddedBuffer.data(), buffer, bufferLength);
			ZeroMemory(paddedBuffer.data() + bufferLength, paddedBuffer.size() - bufferLength);

			encodedBytes = opus_encode(m_opusEncoder, reinterpret_cast<opus_int16 const*>(paddedBuffer.data()), FRAME_SIZE,
				m_outputBuffer.data(), static_cast<opus_int32>(m_outputBuffer.size()) / 10);
		}
		else
		{
			encodedBytes = opus_encode(m_opusEncoder, reinterpret_cast<opus_int16 const*>(buffer), FRAME_SIZE,
				m_outputBuffer.data(), static_cast<opus_int32>(m_outputBuffer.size()) / 10);
		}

		if (encodedBytes < 0)
			return OpusResultToHRESULT(encodedBytes);

		m_encoderGranulePosition += FRAME_SIZE * OPUS_SAMPLES_PER_SECOND / m_header.input_sample_rate;
		m_sizeSegments = (encodedBytes + 255) / 255;
	}
	else
	{
		encodedBytes = 0;
	}

	HRESULT result;
	while ((((m_sizeSegments <= 255) && (m_lastSegments + m_sizeSegments > 255)) ||
		(m_encoderGranulePosition - m_lastGranulePosition > 0)) && ogg_stream_flush_fill(&m_oggStreamState, &m_oggPage, 255 * 255))
	{
		if (ogg_page_packets(&m_oggPage) != 0)
			m_lastGranulePosition = ogg_page_granulepos(&m_oggPage);

		m_lastSegments -= m_oggPage.header[26];

		DWORD bytesWritten;
		ReturnIfFailed(result, WriteOggPage(&m_oggPage, &bytesWritten));
	}

	m_oggPacket.packet = m_outputBuffer.data();
	m_oggPacket.bytes = encodedBytes;
	m_oggPacket.b_o_s = 0;
	m_oggPacket.granulepos = m_encoderGranulePosition;

	if (m_oggPacket.e_o_s)
		m_oggPacket.granulepos = ((m_totalSamples * OPUS_SAMPLES_PER_SECOND + m_header.input_sample_rate - 1) / m_header.input_sample_rate) + m_header.pre_skip;

	m_oggPacket.packetno += 1;
	ogg_stream_packetin(&m_oggStreamState, &m_oggPacket);
	m_lastSegments += m_sizeSegments;

	while ((m_oggPacket.e_o_s || (m_encoderGranulePosition + (FRAME_SIZE * OPUS_SAMPLES_PER_SECOND / m_header.input_sample_rate) - m_lastGranulePosition > 0) ||
		(m_lastSegments >= 255)) ? ogg_stream_flush_fill(&m_oggStreamState, &m_oggPage, 255 * 255) :
		ogg_stream_pageout_fill(&m_oggStreamState, &m_oggPage, 255 * 255))
	{
		if (ogg_page_packets(&m_oggPage) != 0)
			m_lastGranulePosition = ogg_page_granulepos(&m_oggPage);

		m_lastSegments -= m_oggPage.header[26];

		DWORD bytesWritten;
		ReturnIfFailed(result, WriteOggPage(&m_oggPage, &bytesWritten));
	}

	return S_OK;
}

HRESULT OpusOutputByteStream::Finalize()
{
	if (m_opusEncoder == nullptr)
		return E_UNEXPECTED;

	if (m_inputBuffer.size() > 0)
	{
		HRESULT result;
		ReturnIfFailed(result, WriteOpusFrame(m_inputBuffer.data(), static_cast<DWORD>(m_inputBuffer.size())));
	}

	ogg_stream_flush(&m_oggStreamState, &m_oggPage);
	ogg_stream_clear(&m_oggStreamState);

	ZeroMemory(&m_oggPacket, sizeof(ogg_packet));
	ZeroMemory(&m_oggPage, sizeof(ogg_page));
	ZeroMemory(&m_oggStreamState, sizeof(ogg_stream_state));
	ZeroMemory(&m_header, sizeof(OpusHead));

	m_inputBuffer.clear();
	m_outputBuffer.clear();

	return m_byteStream->Flush();
}

HRESULT OpusOutputByteStream::WriteOggPacket(Opus::ogg_packet* packet)
{
	if (ogg_stream_packetin(&m_oggStreamState, packet) < 0)
		return E_FAIL;

	HRESULT result;
	int oggResult;
	while ((oggResult = ogg_stream_flush(&m_oggStreamState, &m_oggPage)) != 0)
	{
		DWORD bytesWritten;
		ReturnIfFailed(result, WriteOggPage(&m_oggPage, &bytesWritten));
	}

	return S_OK;
}

HRESULT OpusOutputByteStream::WriteOggPage(Opus::ogg_page const* page, DWORD* pBytesWritten)
{
	HRESULT result;
	DWORD bytesWritten;
	ReturnIfFailed(result, m_byteStream->Write(m_oggPage.header, m_oggPage.header_len, &bytesWritten));
	if (bytesWritten != m_oggPage.header_len)
		return E_FAIL;

	*pBytesWritten = bytesWritten;

	ReturnIfFailed(result, m_byteStream->Write(m_oggPage.body, m_oggPage.body_len, &bytesWritten));
	if (bytesWritten != m_oggPage.body_len)
		return E_FAIL;

	*pBytesWritten += bytesWritten;
	return S_OK;
}

HRESULT OpusOutputByteStream::WriteOpusHeader(Opus::OpusHead const* header)
{
	std::vector<byte> buffer(header->mapping_family == 0 ? 19 : 21 + header->channel_count);
	auto bufferData = buffer.data();
	bufferData += WriteBuffer(bufferData, reinterpret_cast<byte*>("OpusHead"), 8);
	bufferData += WriteUInt8(bufferData, 1);
	bufferData += WriteUInt8(bufferData, header->channel_count);
	bufferData += WriteUInt16(bufferData, header->pre_skip);
	bufferData += WriteUInt32(bufferData, header->input_sample_rate);
	bufferData += WriteUInt16(bufferData, header->output_gain);
	bufferData += WriteUInt8(bufferData, header->mapping_family);

	if (header->mapping_family != 0)
	{
		bufferData += WriteUInt8(bufferData, header->stream_count);
		bufferData += WriteUInt8(bufferData, header->coupled_count);

		for (int i = 0; i < header->channel_count; i++)
		{
			bufferData += WriteUInt8(bufferData, header->mapping[i]);
		}
	}

	ogg_packet headerPacket = {};
	headerPacket.packet = buffer.data();
	headerPacket.b_o_s = 1;
	headerPacket.bytes = static_cast<long>(buffer.size());
	return WriteOggPacket(&headerPacket);
}

HRESULT OpusOutputByteStream::WriteOpusComments(char const* version)
{
	auto versionLength = static_cast<DWORD>(strlen(version));
	std::vector<byte> buffer((8 + 4 + versionLength + 35 + 4 + COMMENT_PADDING + 255) / 255 * 255 - 1);

	ZeroMemory(buffer.data(), buffer.size());
	CopyMemory(buffer.data(), "OpusTags", 8);
	WriteUInt32(buffer.data() + 8, versionLength + 35);
	CopyMemory(buffer.data() + 12, version, versionLength);
	CopyMemory(buffer.data() + 12 + versionLength, ", WinRT encoder by Lorenzo Rossoni", 35);

	ogg_packet commenstPacket = {};
	commenstPacket.packet = buffer.data();
	commenstPacket.bytes = static_cast<long>(buffer.size());
	commenstPacket.packetno = 1;
	return WriteOggPacket(&commenstPacket);
}