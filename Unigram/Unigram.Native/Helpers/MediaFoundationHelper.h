#pragma once
#include <mfobjects.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <Mferror.h>
#include <mfapi.h>
#include "COMHelper.h"

using Microsoft::WRL::ComPtr;

inline void CreateSourceReader(_In_ Windows::Foundation::Uri^ uri, _Out_ IMFSourceReader** pSourceReader)
{
	ComPtr<IMFAttributes> attributes;
	ThrowIfFailed(MFCreateAttributes(&attributes, 1));
	ThrowIfFailed(attributes->SetUINT32(MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING, TRUE));

	ThrowIfFailed(MFCreateSourceReaderFromURL(uri->RawUri->Data(), attributes.Get(), pSourceReader));
}

inline void CreateSourceReader(_In_ Windows::Storage::Streams::IRandomAccessStream^ stream, _Out_ IMFSourceReader** pSourceReader)
{
	ComPtr<IMFByteStream> spMFByteStream;
	ThrowIfFailed(MFCreateMFByteStreamOnStreamEx(reinterpret_cast<IUnknown*>(stream), &spMFByteStream));

	ComPtr<IMFAttributes> attributes;
	ThrowIfFailed(MFCreateAttributes(&attributes, 1));
	ThrowIfFailed(attributes->SetUINT32(MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING, TRUE));

	ThrowIfFailed(MFCreateSourceReaderFromByteStream(spMFByteStream.Get(), attributes.Get(), pSourceReader));
}

inline void CreateSourceReader(_In_ Windows::Media::Core::IMediaSource^ pMediaSource, _Out_ IMFSourceReader** pSourceReader)
{
	ComPtr<IMFMediaSource> mfMediaSource;
	ThrowIfFailed(MFGetService(reinterpret_cast<IUnknown*>(pMediaSource), MF_MEDIASOURCE_SERVICE, IID_PPV_ARGS(&mfMediaSource)));
	
	ComPtr<IMFAttributes> attributes;
	ThrowIfFailed(MFCreateAttributes(&attributes, 1));
	ThrowIfFailed(attributes->SetUINT32(MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING, TRUE));

	ThrowIfFailed(MFCreateSourceReaderFromMediaSource(mfMediaSource.Get(), attributes.Get(), pSourceReader));
}