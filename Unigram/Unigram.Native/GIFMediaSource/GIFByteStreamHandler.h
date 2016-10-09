#pragma once
#include "GIFMediaSource.h"

namespace Unigram
{
	namespace Native
	{

		class GIFByteStreamHandler WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>,
			ABI::Windows::Media::IMediaExtension, IMFByteStreamHandler>
		{
			InspectableClass(L"Unigram.Native.GIFByteStreamHandler", TrustLevel::BaseTrust);

		public:
			STDMETHODIMP SetProperties(ABI::Windows::Foundation::Collections::IPropertySet* pConfiguration);
			STDMETHODIMP BeginCreateObject(IMFByteStream* pByteStream, LPCWSTR pwszURL, DWORD dwFlags, IPropertyStore* pProps,
				IUnknown** ppIUnknownCancelCookie, IMFAsyncCallback* pCallback, IUnknown* punkState);
			STDMETHODIMP EndCreateObject(IMFAsyncResult* pResult, MF_OBJECT_TYPE* pObjectType, IUnknown** ppObject);
			STDMETHODIMP CancelObjectCreation(IUnknown* pIUnknownCancelCookie);
			STDMETHODIMP GetMaxNumberOfBytesRequiredForResolution(QWORD* pqwBytes);

		private:
			static bool IsValidURL(_In_ LPCWSTR url);
			static bool IsValidByteStream(_In_ IMFByteStream* byteStream);
		};

	}
}