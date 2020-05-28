// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <wrl.h>
#include <mfobjects.h>
#include "OpusMediaType.h"

using namespace Microsoft::WRL;

namespace Unigram
{
	namespace Native
	{

		class OpusOutputByteStream WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IUnknown>
		{
		public:
			OpusOutputByteStream();
			~OpusOutputByteStream();

			inline HRESULT GetCapabilities(_Out_ DWORD* capabilities)
			{
				return m_byteStream->GetCapabilities(capabilities);
			}

			STDMETHODIMP RuntimeClassInitialize(_In_ IMFByteStream* byteStream);
			HRESULT Initialize(_In_ IMFMediaType* mediaType);
			HRESULT WriteFrame(_In_ byte const* buffer, DWORD bufferLength);
			HRESULT Finalize();

		private:
			struct Packet
			{
				byte* buffer;
				DWORD bufferSize;
				DWORD position;
			};

			HRESULT Close();
			HRESULT WriteOpusHeader(_In_ Opus::OpusHead const* header);
			HRESULT WriteOpusComments(char const* version);
			HRESULT WriteOpusFrame(_In_ byte const* buffer, DWORD bufferLength);
			HRESULT WriteOggPacket(_In_ Opus::ogg_packet* packet);
			HRESULT WriteOggPage(_In_ Opus::ogg_page const* page, _Out_ DWORD* bytesWritten);

			inline static DWORD WriteUInt32(byte* buffer, DWORD value)
			{
				buffer[0] = (value) & 0xFF;
				buffer[1] = (value >> 8) & 0xFF;
				buffer[2] = (value >> 16) & 0xFF;
				buffer[3] = (value >> 24) & 0xFF;
				return 4;
			}

			inline static DWORD WriteUInt16(byte* buffer, WORD value)
			{
				buffer[0] = (value) & 0xFF;
				buffer[1] = (value >> 8) & 0xFF;
				return 2;
			}

			inline static DWORD WriteUInt8(byte* buffer, BYTE value)
			{
				buffer[0] = value;
				return 1;
			}

			inline static DWORD WriteBuffer(byte* buffer, byte const* value, DWORD count)
			{
				CopyMemory(buffer, value, count);
				return count;
			}

			DWORD m_lastSegments;
			DWORD m_sizeSegments;		
			LONGLONG m_totalSamples;
			LONGLONG m_lastGranulePosition;
			LONGLONG m_encoderGranulePosition;
			Opus::OpusEncoder* m_opusEncoder;
			Opus::OpusHead m_header;	
			std::vector<byte> m_inputBuffer;
			std::vector<byte> m_outputBuffer;
			Opus::ogg_packet m_oggPacket;
			Opus::ogg_page m_oggPage;
			Opus::ogg_stream_state m_oggStreamState;
			ComPtr<IMFByteStream> m_byteStream;
		};
	}
}