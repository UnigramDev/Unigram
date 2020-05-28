// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <queue>
#include "MediaSource.h"
#include "OpusInputByteStream.h"

using namespace Microsoft::WRL;

namespace Unigram
{
	namespace Native
	{

		class OpusMediaSource WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, MediaSource>
		{
			friend class OpusMediaStream;

			InspectableClass(L"Unigram.Native.OpusMediaSource", TrustLevel::BaseTrust);

		public:
			OpusMediaSource();

			STDMETHODIMP RuntimeClassInitialize(_In_ OpusInputByteStream* opusStream);

		protected:
			virtual DWORD GetCharacteristics() noexcept override;
			virtual DWORD GetMediaStreamCount() noexcept override;
			virtual MediaStream* GetMediaStreamByIndex(DWORD streamIndex) noexcept override;
			virtual MediaStream* GetMediaStreamById(DWORD streamId) noexcept override;
			virtual HRESULT OnStart(MFTIME position) override;
			virtual HRESULT OnSeek(MFTIME position) override;
			virtual HRESULT OnPause() override;
			virtual HRESULT OnStop() override;
			virtual HRESULT OnShutdown() override;

		private:
			DWORD m_characteristics;
			ComPtr<OpusMediaStream> m_mediaStream;
		};

		class OpusMediaStream WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, MediaStream, IMFAsyncCallback>
		{
			friend class OpusMediaSource;

		public:
			OpusMediaStream();

			STDMETHODIMP RuntimeClassInitialize(_In_ OpusMediaSource* mediaSource, _In_ OpusInputByteStream* opusStream,
				_Out_ IMFPresentationDescriptor** ppPresentationDescriptor);

		protected:
			virtual bool IsEndOfStream() noexcept override;
			virtual HRESULT OnSampleRequested(_In_ IUnknown* pToken) override;
			virtual HRESULT OnStart(MFTIME position) override;
			virtual HRESULT OnSeek(MFTIME position) override;
			virtual HRESULT OnPause() override;
			virtual HRESULT OnStop() override;
			virtual HRESULT OnShutdown() override;

		private:
			IFACEMETHODIMP GetParameters(DWORD* pdwFlags, DWORD* pdwQueue);
			IFACEMETHODIMP Invoke(IMFAsyncResult* pAsyncResult);
			HRESULT SetStreamPosition(LONGLONG time);

			UINT32 m_channelCount;
			UINT32 m_sampleRate;
			DWORD m_workQueueId;
			bool m_isEndOfStream;
			LONGLONG m_currentTime;
			ComPtr<OpusInputByteStream> m_opusStream;
		};

	}
}