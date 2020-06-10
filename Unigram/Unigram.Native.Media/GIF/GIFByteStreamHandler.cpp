// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "GIFMediaSource.h"
#include "GIFByteStreamHandler.h"
#include "Helpers\COMHelper.h"

using namespace Unigram::Native;

ActivatableClass(GIFByteStreamHandler)

QWORD GIFByteStreamHandler::GetMaxNumberOfBytesRequiredForResolution() noexcept
{
	return 6;
}

HRESULT GIFByteStreamHandler::CreateMediaSource(IMFByteStream* byteStream, IPropertyStore* properties, IMFMediaSource** ppMediaSource)
{
	HRESULT result;
	ComPtr<GIFMediaSource> mediaSource;
	ReturnIfFailed(result, MakeAndInitialize<GIFMediaSource>(&mediaSource, byteStream));

	return mediaSource.CopyTo(ppMediaSource);
}

HRESULT GIFByteStreamHandler::ValidateURL(LPCWSTR url)
{
	if (CheckExtension(url, L".gif"))
	{
		return S_OK;
	}

	return E_INVALIDARG;
}

HRESULT GIFByteStreamHandler::ValidateByteStream(IMFByteStream* byteStream)
{
	HRESULT result;
	DWORD capabilities;
	ReturnIfFailed(result, byteStream->GetCapabilities(&capabilities));

	if (capabilities & (MFBYTESTREAM_IS_READABLE | MFBYTESTREAM_IS_SEEKABLE))
	{
		ULONG readBytes;
		char magicNumber[6];
		ReturnIfFailed(result, byteStream->Read(reinterpret_cast<byte*>(magicNumber), ARRAYSIZE(magicNumber), &readBytes));
		ReturnIfFailed(result, byteStream->SetCurrentPosition(0));

		if (strncmp(magicNumber, "GIF87a", 6) == 0 || strncmp(magicNumber, "GIF89a", 6) == 0)
		{
			return S_OK;
		}
	}

	return MF_E_UNSUPPORTED_BYTESTREAM_TYPE;
}