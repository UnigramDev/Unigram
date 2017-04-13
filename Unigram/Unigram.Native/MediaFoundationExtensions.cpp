// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "Helpers\COMHelper.h"
#include "MediaFoundationExtensions.h"

using Microsoft::WRL::ComPtr;
using Platform::COMException;

HMODULE GetModuleHandle(LPCTSTR libFileName)
{
	typedef HMODULE(WINAPI *pGetModuleHandle)(__in_opt LPCTSTR);

	MEMORY_BASIC_INFORMATION mbi;
	if (VirtualQuery(VirtualQuery, &mbi, sizeof(MEMORY_BASIC_INFORMATION)) == 0)
		throw ref new COMException(HRESULT_FROM_WIN32(GetLastError()));

	return reinterpret_cast<pGetModuleHandle>(GetProcAddress(reinterpret_cast<HMODULE>(mbi.AllocationBase),
		"GetModuleHandleW"))(libFileName);
}

HRESULT MFScheduleWorkItem(IMFAsyncCallback* pCallback, IUnknown* pState, INT64 Timeout, MFWORKITEM_KEY* pKey)
{
	typedef HRESULT(WINAPI *pMFScheduleWorkItem)(_In_ IMFAsyncCallback*, _In_ IUnknown*, _In_ INT64, _Out_ MFWORKITEM_KEY*);
	static const auto procMFScheduleWorkItem = reinterpret_cast<pMFScheduleWorkItem>(GetProcAddress(GetModuleHandle(L"Mfplat.dll"), "MFScheduleWorkItem"));

	return procMFScheduleWorkItem(pCallback, pState, Timeout, pKey);
}

HRESULT MFRegisterLocalByteStreamHandler(PCWSTR szFileExtension, PCWSTR szMimeType, IMFActivate* pActivate)
{
	typedef HRESULT(WINAPI *pMFRegisterLocalByteStreamHandler)(_In_ PCWSTR, _In_ PCWSTR, _In_ IMFActivate*);
	static const auto procMFRegisterLocalByteStreamHandler = reinterpret_cast<pMFRegisterLocalByteStreamHandler>(GetProcAddress(GetModuleHandle(L"Mfplat.dll"),
		"MFRegisterLocalByteStreamHandler"));

	return procMFRegisterLocalByteStreamHandler(szFileExtension, szMimeType, pActivate);
}