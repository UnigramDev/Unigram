// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <ppl.h>
#include <ppltasks.h>
#include <pplcancellation_token.h>
#include <Mferror.h>
#include <mfreadwrite.h>
#include "BufferLock.h"
#include "FramesCacheStore.h"

using namespace Concurrency;

namespace Unigram
{
	namespace Native
	{

		class ReadFramesAsyncOperation WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IMFSourceReaderCallback>
		{
		public:
			inline D2D1_SIZE_U GetFrameSize() const
			{
				return m_frameSize;
			}

			task<ComPtr<FramesCacheStore>> Start(cancellation_token& ct);
			STDMETHODIMP RuntimeClassInitialize(const D2D1_SIZE_U& maximumFrameSize, _In_ Windows::Foundation::Uri^ uri);
			STDMETHODIMP RuntimeClassInitialize(const D2D1_SIZE_U& maximumFrameSize, _In_ Windows::Storage::Streams::IRandomAccessStream^ stream);
			STDMETHODIMP RuntimeClassInitialize(const D2D1_SIZE_U& maximumFrameSize, _In_ Windows::Media::Core::IMediaSource^ mediaSource);

		private:
			IFACEMETHODIMP OnReadSample(HRESULT hrStatus, DWORD dwStreamIndex, DWORD dwStreamFlags,
				LONGLONG llTimestamp, IMFSample* pSample);
			IFACEMETHODIMP OnFlush(DWORD dwStreamIndex);
			IFACEMETHODIMP OnEvent(DWORD dwStreamIndex, IMFMediaEvent* pEvent);
			HRESULT CreateSourceReaderAttributes(_Out_ IMFAttributes** ppAttributes);
			HRESULT RuntimeClassInitialize(const D2D1_SIZE_U& maximumFrameSize);

			static HRESULT CreateUncompressedMediaType(_In_ IMFMediaType* pType, _In_ const GUID& subtype,
				_Out_ D2D1_SIZE_U* frameSize, _Out_ IMFMediaType** ppType);

			D2D1_SIZE_U m_frameSize;
			CriticalSection m_criticalSection;
			task_completion_event<ComPtr<FramesCacheStore>> m_taskCompletionEvent;
			ComPtr<IMFSourceReader> m_sourceReader;
			ComPtr<FramesCacheStore> m_framesCacheStore;
		};

	}
}