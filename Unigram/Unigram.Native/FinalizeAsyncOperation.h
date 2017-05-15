// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <mfobjects.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <Mferror.h>
#include <mfapi.h>
#include <wrl.h>
#include <wrl\wrappers\corewrappers.h>

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Unigram
{
	namespace Native
	{

		class FinalizeAsyncOperation WrlSealed : public RuntimeClass<RuntimeClassFlags<RuntimeClassType::ClassicCom>, IMFAsyncCallback>
		{
		public:
			FinalizeAsyncOperation();
			~FinalizeAsyncOperation();

			STDMETHODIMP RuntimeClassInitialize(_In_ IMFAsyncCallback* callback, _In_opt_ IUnknown* state, DWORD pendingOperationCount);
			HRESULT Cancel(HRESULT result);

		private:
			IFACEMETHODIMP GetParameters(DWORD* pdwFlags, DWORD* pdwQueue);
			IFACEMETHODIMP Invoke(IMFAsyncResult* pAsyncResult);

			CriticalSection m_criticalSection;
			DWORD m_pendingOperationCount;
			ComPtr<IMFAsyncResult> m_asyncResult;
		};

	}
}