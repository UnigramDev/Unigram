// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <initguid.h>
#include <Mfapi.h>

#define OPUS_DECODER_BUFFER_LENGTH 4096
#define OPUS_SAMPLES_PER_SECOND 48000

#pragma comment(lib, "libopusfile.lib")

DEFINE_MEDIATYPE_GUID(MFAudioFormat_Opus, WAVE_FORMAT_OPUS);   // {0000704F-0000-0010-8000-00aa00389b71}

namespace Opus
{

#include <opusfile.h>

	inline HRESULT OpusResultToHRESULT(int opusResult)
	{
		switch (opusResult)
		{
		case OPUS_OK:
			return S_OK;
		case OPUS_BAD_ARG:
			return E_INVALIDARG;
		case OPUS_BUFFER_TOO_SMALL:
			return MF_E_BUFFERTOOSMALL;
		case OPUS_INVALID_PACKET:
			return MF_E_INVALID_STREAM_DATA;
		case OPUS_UNIMPLEMENTED:
			return E_NOTIMPL;
		case OPUS_INVALID_STATE:
			return E_NOT_VALID_STATE;
		case OPUS_ALLOC_FAIL:
			return E_OUTOFMEMORY;
		default:
			return E_FAIL;
		}
	}

}