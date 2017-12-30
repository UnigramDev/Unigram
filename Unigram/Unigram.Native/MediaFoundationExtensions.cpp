// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "Helpers\LibraryHelper.h"
#include "Helpers\COMHelper.h"
#include "MediaFoundationExtensions.h"

using Microsoft::WRL::ComPtr;

static LibraryInstance s_mfPlat(L"Mfplat.dll");

HRESULT MFScheduleWorkItem(IMFAsyncCallback* pCallback, IUnknown* pState, INT64 Timeout, MFWORKITEM_KEY* pKey)
{
	typedef HRESULT(WINAPI *pMFScheduleWorkItem)(_In_ IMFAsyncCallback*, _In_ IUnknown*, _In_ INT64, _Out_ MFWORKITEM_KEY*);
	static const auto procMFScheduleWorkItem = s_mfPlat.GetMethod<pMFScheduleWorkItem>("MFScheduleWorkItem");

	return procMFScheduleWorkItem(pCallback, pState, Timeout, pKey);
}

HRESULT MFRegisterLocalByteStreamHandler(PCWSTR szFileExtension, PCWSTR szMimeType, IMFActivate* pActivate)
{
	typedef HRESULT(WINAPI *pMFRegisterLocalByteStreamHandler)(_In_ PCWSTR, _In_ PCWSTR, _In_ IMFActivate*);
	static const auto procMFRegisterLocalByteStreamHandler = s_mfPlat.GetMethod<pMFRegisterLocalByteStreamHandler>("MFRegisterLocalByteStreamHandler");

	return procMFRegisterLocalByteStreamHandler(szFileExtension, szMimeType, pActivate);
}