// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "FinalizeAsyncOperation.h"
#include "Helpers\COMHelper.h"

using namespace Unigram::Native;

FinalizeAsyncOperation::FinalizeAsyncOperation() :
	m_pendingOperationCount(0)
{
}

FinalizeAsyncOperation::~FinalizeAsyncOperation()
{
	Cancel(E_ABORT);
}

HRESULT FinalizeAsyncOperation::RuntimeClassInitialize(IMFAsyncCallback* callback, IUnknown* state, DWORD pendingOperationCount)
{
	if (callback == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	ReturnIfFailed(result, MFCreateAsyncResult(nullptr, callback, state, &m_asyncResult));
	m_pendingOperationCount = pendingOperationCount;

	if (pendingOperationCount == 0)
	{
		return Cancel(S_OK);
	}

	return S_OK;
}

HRESULT FinalizeAsyncOperation::Cancel(HRESULT error)
{
	auto lock = m_criticalSection.Lock();

	if (m_asyncResult == nullptr)
	{
		return E_UNEXPECTED;
	}

	m_asyncResult->SetStatus(error);

	HRESULT result;
	ReturnIfFailed(result, MFInvokeCallback(m_asyncResult.Get()));

	m_pendingOperationCount = 0;
	m_asyncResult.Reset();
	return S_OK;
}

HRESULT FinalizeAsyncOperation::GetParameters(DWORD* pdwFlags, DWORD* pdwQueue)
{
	if (pdwFlags == nullptr || pdwQueue == nullptr)
	{
		return E_POINTER;
	}

	*pdwQueue = MFASYNC_CALLBACK_QUEUE_STANDARD;
	return S_OK;
}

HRESULT FinalizeAsyncOperation::Invoke(IMFAsyncResult* pAsyncResult)
{
	auto lock = m_criticalSection.Lock();

	if (m_asyncResult == nullptr)
	{
		return E_UNEXPECTED;
	}

	HRESULT result;
	if (SUCCEEDED(result = pAsyncResult->GetStatus()) && --m_pendingOperationCount > 0)
	{
		return S_OK;
	}

	m_asyncResult->SetStatus(result);
	ReturnIfFailed(result, MFInvokeCallback(m_asyncResult.Get()));

	m_asyncResult.Reset();
	return S_OK;
}