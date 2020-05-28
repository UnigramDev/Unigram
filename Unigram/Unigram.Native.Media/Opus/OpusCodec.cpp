// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include <Windows.Media.Core.h>
#include <Windows.Media.h>
#include "OpusMediaSource.h"
#include "OpusMediaSink.h"
#include "OpusCodec.h"
#include "Helpers\COMHelper.h"

using namespace Unigram::Native;
using Platform::Exception;

IAsyncOperation<IMediaSource^>^ OpusCodec::CreateMediaSourceAsync(StorageFile^ inputFile)
{
	return create_async([inputFile]
	{
		return create_task(inputFile->OpenReadAsync()).then([](IRandomAccessStream^ inputStream)
		{
			return create_task(CreateMediaSourceAsync(inputStream));
		});
	});
}

IAsyncOperation<IMediaSource^>^ OpusCodec::CreateMediaSourceAsync(IRandomAccessStream^ inputStream)
{
	return create_async([inputStream]
	{
		HRESULT result;

		do
		{
			ComPtr<IMFByteStream> byteStream;
			BreakIfFailed(result, MFCreateMFByteStreamOnStreamEx(reinterpret_cast<IUnknown*>(inputStream), &byteStream));

			ComPtr<OpusInputByteStream> opusByteStream;
			BreakIfFailed(result, MakeAndInitialize<OpusInputByteStream>(&opusByteStream, byteStream.Get()));

			ComPtr<ABI::Windows::Media::Core::IMediaSource> mediaSource;
			BreakIfFailed(result, MakeAndInitialize<OpusMediaSource>(&mediaSource, opusByteStream.Get()));

			return task_from_result(reinterpret_cast<IMediaSource^>(mediaSource.Get()));
		} while (false);

		return task_from_exception<IMediaSource^>(Exception::CreateException(result));
	});
}

IAsyncOperation<IMediaExtension^>^ OpusCodec::CreateMediaSinkAsync(StorageFile^ outputFile)
{
	return create_async([outputFile]
	{
		return create_task(outputFile->OpenAsync(FileAccessMode::ReadWrite)).then([](IRandomAccessStream^ inputStream)
		{
			return create_task(CreateMediaSinkAsync(inputStream));
		});
	});
}

IAsyncOperation<IMediaExtension^>^ OpusCodec::CreateMediaSinkAsync(IRandomAccessStream^ outputStream)
{
	return create_async([outputStream]
	{
		HRESULT result;

		do
		{
			ComPtr<IMFByteStream> byteStream;
			BreakIfFailed(result, MFCreateMFByteStreamOnStreamEx(reinterpret_cast<IUnknown*>(outputStream), &byteStream));

			ComPtr<OpusOutputByteStream> opusByteStream;
			BreakIfFailed(result, MakeAndInitialize<OpusOutputByteStream>(&opusByteStream, byteStream.Get()));

			ComPtr<ABI::Windows::Media::IMediaExtension> mediaSink;
			BreakIfFailed(result, MakeAndInitialize<OpusMediaSink>(&mediaSink, opusByteStream.Get()));

			return task_from_result(reinterpret_cast<IMediaExtension^>(mediaSink.Get()));
		} while (false);

		return task_from_exception<IMediaExtension^>(Exception::CreateException(result));
	});
}
