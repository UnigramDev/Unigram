#pragma once
#include <mfobjects.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <Mferror.h>
#include <mfapi.h>
#include "COMHelper.h"
#include "DebugHelper.h"

using Microsoft::WRL::ComPtr;
using Platform::COMException;

inline HRESULT CreateSourceReader(_In_ Windows::Foundation::Uri^ uri,
	_In_ IMFDXGIDeviceManager* dxgiDeviceManager, _Out_ IMFSourceReader** ppSourceReader)
{
	HRESULT result;
	ComPtr<IMFAttributes> attributes;
	if (dxgiDeviceManager == nullptr)
	{
		ReturnIfFailed(result, MFCreateAttributes(&attributes, 1));
	}
	else
	{
		ReturnIfFailed(result, MFCreateAttributes(&attributes, 2));
		ReturnIfFailed(result, attributes->SetUnknown(MF_SOURCE_READER_D3D_MANAGER, dxgiDeviceManager));
	}

	ReturnIfFailed(result, attributes->SetUINT32(MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING, TRUE));

	return MFCreateSourceReaderFromURL(uri->RawUri->Data(), attributes.Get(), ppSourceReader);
}

inline HRESULT CreateSourceReader(_In_ Windows::Foundation::Uri^ uri, _Out_ IMFSourceReader** ppSourceReader)
{
	return CreateSourceReader(uri, nullptr, ppSourceReader);
}

inline HRESULT CreateSourceReader(_In_ Windows::Storage::Streams::IRandomAccessStream^ stream,
	_In_ IMFDXGIDeviceManager* dxgiDeviceManager, _Out_ IMFSourceReader** ppSourceReader)
{
	HRESULT result;
	ComPtr<IMFByteStream> spMFByteStream;
	ReturnIfFailed(result, MFCreateMFByteStreamOnStreamEx(reinterpret_cast<IUnknown*>(stream), &spMFByteStream));

	ComPtr<IMFAttributes> attributes;
	if (dxgiDeviceManager == nullptr)
	{
		ReturnIfFailed(result, MFCreateAttributes(&attributes, 1));
	}
	else
	{
		ReturnIfFailed(result, MFCreateAttributes(&attributes, 2));
		ReturnIfFailed(result, attributes->SetUnknown(MF_SOURCE_READER_D3D_MANAGER, dxgiDeviceManager));
	}

	ReturnIfFailed(result, attributes->SetUINT32(MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING, TRUE));

	return MFCreateSourceReaderFromByteStream(spMFByteStream.Get(), attributes.Get(), ppSourceReader);
}

inline HRESULT CreateSourceReader(_In_ Windows::Storage::Streams::IRandomAccessStream^ stream, _Out_ IMFSourceReader** ppSourceReader)
{
	return CreateSourceReader(stream, nullptr, ppSourceReader);
}

inline HRESULT CreateSourceReader(_In_ Windows::Media::Core::IMediaSource^ mediaSource,
	_In_ IMFDXGIDeviceManager* dxgiDeviceManager, _Out_ IMFSourceReader** ppSourceReader)
{
	HRESULT result;
	ComPtr<IMFMediaSource> mfMediaSource;
	ReturnIfFailed(result, MFGetService(reinterpret_cast<IUnknown*>(mediaSource), MF_MEDIASOURCE_SERVICE, IID_PPV_ARGS(&mfMediaSource)));

	ComPtr<IMFAttributes> attributes;
	if (dxgiDeviceManager == nullptr)
	{
		ReturnIfFailed(result, MFCreateAttributes(&attributes, 1));
	}
	else
	{
		ReturnIfFailed(result, MFCreateAttributes(&attributes, 2));
		ReturnIfFailed(result, attributes->SetUnknown(MF_SOURCE_READER_D3D_MANAGER, dxgiDeviceManager));
	}

	ReturnIfFailed(result, attributes->SetUINT32(MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING, TRUE));

	return MFCreateSourceReaderFromMediaSource(mfMediaSource.Get(), attributes.Get(), ppSourceReader);
}

inline HRESULT CreateSourceReader(_In_ Windows::Media::Core::IMediaSource^ mediaSource, _Out_ IMFSourceReader** ppSourceReader)
{
	return CreateSourceReader(mediaSource, nullptr, ppSourceReader);
}