// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <type_traits>
#include <mfidl.h> 
#include <mfapi.h>
#include <mfobjects.h>
#include <wrl\module.h>
#include <functional>
#include "Helpers\COMHelper.h"

using namespace Microsoft::WRL;

namespace Details
{

	MIDL_INTERFACE("f6f81ff1-2731-49b8-9217-c4f78d8dc3f7") IAsyncCallbackState : public IUnknown
	{
	public:
		virtual HRESULT STDMETHODCALLTYPE Invoke(_In_ IMFAsyncResult* asyncResult) = 0;
	};

	template<class _Owner, HRESULT(_Owner::*_AsyncCallback)(IMFAsyncResult*)>
	class AsyncCallbackState WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IAsyncCallbackState>
	{
		typedef typename std::enable_if<std::is_base_of<IUnknown, _Owner>::value, _Owner>::type OwnerType;

	public:
		AsyncCallbackState(_In_ OwnerType* owner) :
			m_owner(owner)
		{
		}

	private:
		IFACEMETHODIMP Invoke(_In_ IMFAsyncResult* asyncResult)
		{
			return (m_owner->*_AsyncCallback)(asyncResult);
		}

		OwnerType* m_owner;
	};
}

class AsyncCallbackState sealed
{
public:
	template<class _Owner, HRESULT(_Owner::*_AsyncCallback)(IMFAsyncResult*)>
	inline static HRESULT QueueAsyncCallback(_In_ typename std::enable_if<std::is_base_of<IMFAsyncCallback,
		_Owner>::value, _Owner>::type* owner, DWORD workQueueId, _In_ IUnknown* state)
	{
		if (owner == nullptr)
		{
			return E_INVALIDARG;
		}

		HRESULT result;
		ComPtr<IMFAsyncResult> asyncResult;
		auto asyncCallbackState = Make<::Details::AsyncCallbackState<_Owner, _AsyncCallback>>(owner);
		ReturnIfFailed(result, MFCreateAsyncResult(asyncCallbackState.Get(), owner, state, &asyncResult));

		return MFPutWorkItemEx2(workQueueId, 0, asyncResult.Get());
	}

	template<class _Owner, HRESULT(_Owner::*_AsyncCallback)(IMFAsyncResult*)>
	inline static HRESULT QueueAsyncCallback(_In_ typename std::enable_if<std::is_base_of<IMFAsyncCallback,
		_Owner>::value, _Owner>::type* owner, DWORD workQueueId)
	{
		return QueueAsyncCallback<_Owner, _AsyncCallback>(owner, workQueueId, nullptr);
	}

	inline static HRESULT CompleteAsyncCallback(_In_ IMFAsyncResult* asyncResult)
	{
		if (asyncResult == nullptr)
		{
			return E_POINTER;
		}

		HRESULT result;
		//ReturnIfFailed(result, asyncResult->GetStatus());

		ComPtr<::Details::IAsyncCallbackState> asyncCallbackState;
		ReturnIfFailed(result, asyncResult->GetObject(&asyncCallbackState));

		return asyncCallbackState->Invoke(asyncResult);
	}

private:
	AsyncCallbackState() = default;
};
