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

		class OpusInputByteStream WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IUnknown>
		{
		public:
			OpusInputByteStream();
			~OpusInputByteStream();

			inline HRESULT GetCapabilities(_Out_ DWORD* capabilities)
			{
				return m_byteStream->GetCapabilities(capabilities);
			}

			STDMETHODIMP RuntimeClassInitialize(_In_ IMFByteStream* byteStream);
			HRESULT ReadMediaType(_Out_ IMFMediaType** mediaType);
			HRESULT Seek(LONGLONG time);
			HRESULT GetDuration(_Out_ LONGLONG* duration);
			HRESULT ReadSamples(_Out_writes_(bufferLength) int16* buffer, DWORD bufferSize, _Out_ DWORD* readSamples);

		private:
			HRESULT Close();

			static int DecoderReadCallback(_In_opt_ void* datasource, _Out_writes_(nbytes) unsigned char* ptr, int nbytes);
			static int DecoderSeekCallback(_In_opt_ void* datasource, int64_t offset, int whence);
			static int DecoderCloseCallback(_In_opt_ void* datasource);
			static int64_t DecoderTellCallback(_In_opt_ void* datasource);

			Opus::OggOpusFile* m_opusFile;
			const Opus::OpusHead* m_header;
			ComPtr<IMFByteStream> m_byteStream;
			static const Opus::OpusFileCallbacks s_reader;
		};
	}
}