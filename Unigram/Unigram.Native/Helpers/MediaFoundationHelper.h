#pragma once
#include <mfobjects.h>
#include <mfapi.h>
#include <wrl.h>
#include "Helpers\COMHelper.h"

using namespace Microsoft::WRL;

inline HRESULT CloneMediaType(_In_ IMFMediaType* pMediaType, _Out_ IMFMediaType** ppMediaType)
{
	HRESULT result;
	ComPtr<IMFMediaType> mediaType;
	ReturnIfFailed(result, MFCreateMediaType(&mediaType));
	ReturnIfFailed(result, pMediaType->CopyAllItems(mediaType.Get()));

	*ppMediaType = mediaType.Detach();
	return S_OK;
}

inline DWORD GetMediaSourceCharacteristicsFromByteStreamCapabilities(DWORD capabilities)
{
	DWORD characteristics = 0;
	if (capabilities & MFBYTESTREAM_IS_SEEKABLE)
		characteristics |= MFMEDIASOURCE_CAN_SEEK;

	if (capabilities & MFBYTESTREAM_DOES_NOT_USE_NETWORK)
		characteristics |= MFMEDIASOURCE_DOES_NOT_USE_NETWORK;

	if (capabilities & MFBYTESTREAM_HAS_SLOW_SEEK)
		characteristics |= MFMEDIASOURCE_HAS_SLOW_SEEK;

	if (capabilities &MFBYTESTREAM_IS_REMOTE)
		characteristics |= MFMEDIASOURCE_IS_LIVE;

	return characteristics;
}