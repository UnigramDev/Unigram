// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "OpusMediaSource.h"
#include "OpusByteStreamHandler.h"
#include "Helpers\COMHelper.h"

using namespace Unigram::Native;

ActivatableClass(OpusByteStreamHandler)

QWORD OpusByteStreamHandler::GetMaxNumberOfBytesRequiredForResolution() noexcept
{
	return 27;
}

HRESULT OpusByteStreamHandler::CreateMediaSource(IMFByteStream* byteStream, IPropertyStore* properties, IMFMediaSource** ppMediaSource)
{
	HRESULT result;
	ComPtr<OpusInputByteStream> opusStream;
	ReturnIfFailed(result, MakeAndInitialize<OpusInputByteStream>(&opusStream, byteStream));

	ComPtr<OpusMediaSource> mediaSource;
	ReturnIfFailed(result, MakeAndInitialize<OpusMediaSource>(&mediaSource, opusStream.Get()));

	return mediaSource.CopyTo(ppMediaSource);
}

HRESULT OpusByteStreamHandler::ValidateURL(LPCWSTR url)
{
	if (CheckExtension(url, L".ogg"))
	{
		return S_OK;
	}

	return E_INVALIDARG;
}

HRESULT OpusByteStreamHandler::ValidateByteStream(IMFByteStream* byteStream)
{
	HRESULT result;
	DWORD capabilities;
	ReturnIfFailed(result, byteStream->GetCapabilities(&capabilities));

	if (capabilities & MFBYTESTREAM_IS_READABLE)
	{
		return S_OK;
	}

	return MF_E_UNSUPPORTED_BYTESTREAM_TYPE;
}