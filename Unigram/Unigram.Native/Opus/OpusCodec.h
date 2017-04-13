// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <ppl.h>
#include <ppltasks.h>
#include <wrl.h>
#include <mfobjects.h>

using namespace Windows::Foundation;
using namespace Windows::Media;
using namespace Windows::Media::Core;
using namespace Windows::Storage;
using namespace Windows::Storage::Streams;
using namespace Microsoft::WRL;
using namespace Concurrency;
using Windows::Foundation::Metadata::DefaultOverloadAttribute;

namespace Unigram
{
	namespace Native
	{

		public ref class OpusCodec sealed
		{
		public:
			[DefaultOverload]
			static IAsyncOperation<IMediaSource^>^ CreateMediaSourceAsync(_In_ StorageFile^ inputFile);
			static IAsyncOperation<IMediaSource^>^ CreateMediaSourceAsync(_In_ IRandomAccessStream^ inputStream);
			[DefaultOverload]
			static IAsyncOperation<IMediaExtension^>^ CreateMediaSinkAsync(_In_ StorageFile^ outputFile);
			static IAsyncOperation<IMediaExtension^>^ CreateMediaSinkAsync(_In_ IRandomAccessStream^ outputStream);

		private:
			OpusCodec();
		};

	}
}