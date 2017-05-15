// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include "MediaSource.h"

namespace Unigram
{
	namespace Native
	{

		class ByteStreamHandler abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>,
			ABI::Windows::Media::IMediaExtension, IMFByteStreamHandler>
		{
		public:
			IFACEMETHODIMP SetProperties(ABI::Windows::Foundation::Collections::IPropertySet* pConfiguration);
			IFACEMETHODIMP BeginCreateObject(IMFByteStream* pByteStream, LPCWSTR pwszURL, DWORD dwFlags, IPropertyStore* pProps,
				IUnknown** ppIUnknownCancelCookie, IMFAsyncCallback* pCallback, IUnknown* punkState);
			IFACEMETHODIMP EndCreateObject(IMFAsyncResult* pResult, MF_OBJECT_TYPE* pObjectType, IUnknown** ppObject);
			IFACEMETHODIMP CancelObjectCreation(IUnknown* pIUnknownCancelCookie);
			IFACEMETHODIMP GetMaxNumberOfBytesRequiredForResolution(QWORD* pqwBytes);

		protected:
			virtual QWORD GetMaxNumberOfBytesRequiredForResolution() noexcept = 0;
			virtual HRESULT ValidateURL(_In_ LPCWSTR url) = 0;
			virtual HRESULT ValidateByteStream(_In_ IMFByteStream* byteStream) = 0;
			virtual HRESULT CreateMediaSource(_In_  IMFByteStream* byteStream, _In_ IPropertyStore* properties,
				_Out_ IMFMediaSource** mediaSource) = 0;

			static bool CheckExtension(_In_ LPCWSTR url, _In_ LPCWSTR extension);
		};

	}
}