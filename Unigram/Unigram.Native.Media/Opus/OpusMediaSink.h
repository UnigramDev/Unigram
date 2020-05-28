// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include "MediaSink.h"
#include "OpusOutputByteStream.h"

namespace Unigram
{
	namespace Native
	{

		class OpusMediaSink WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, MediaSink>
		{
			friend class OpusStreamSink;

			InspectableClass(L"Unigram.Native.OpusMediaSink", TrustLevel::BaseTrust);

		public:
			STDMETHODIMP RuntimeClassInitialize(_In_ OpusOutputByteStream* opusStream);

		protected:
			virtual DWORD GetStreamSinkCount() noexcept override;
			virtual StreamSink* GetStreamSinkByIndex(DWORD streamIndex) noexcept override;
			virtual StreamSink* GetStreamSinkById(DWORD streamId) noexcept override;
			virtual HRESULT OnStart() override;
			virtual HRESULT OnPause() override;
			virtual HRESULT OnStop() override;
			virtual HRESULT OnShutdown() override;
			virtual HRESULT OnSetProperties(_In_ ABI::Windows::Foundation::Collections::IPropertySet* configuration) override;

		private:
			ComPtr<OpusStreamSink> m_streamSink;
		};

		class OpusStreamSink WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, StreamSink>
		{
			friend class OpusMediaSink;

		public:
			STDMETHODIMP RuntimeClassInitialize(_In_ OpusMediaSink* mediaSink, _In_ OpusOutputByteStream* opusStream);

		protected:
			virtual DWORD GetIdentifier() noexcept override;
			virtual const GUID& GetMajorType() noexcept override;
			virtual DWORD GetMediaTypeCount() noexcept override;
			virtual HRESULT ValidateMediaType(_In_ IMFMediaType* mediaType) override;
			virtual HRESULT GetSupportedMediaType(DWORD index, _Out_ IMFMediaType** mediaType) override;
			virtual HRESULT OnProcessSample(_In_ IMFSample* sample) override;
			virtual HRESULT OnMediaTypeChange(_In_ IMFMediaType* type) override;
			virtual HRESULT OnStart(MFTIME position) override;
			virtual HRESULT OnRestart(MFTIME position) override;
			virtual HRESULT OnStop() override;
			virtual HRESULT OnPause() override;
			virtual HRESULT OnPlaceMarker(MFSTREAMSINK_MARKER_TYPE type, PROPVARIANT const* markerValue, PROPVARIANT const* contextValue) override;
			virtual HRESULT OnFlush() override;
			virtual HRESULT OnFinalize() override;
			virtual HRESULT OnShutdown() override;

		private:
			ComPtr<OpusOutputByteStream> m_opusStream;
		};

	}
}