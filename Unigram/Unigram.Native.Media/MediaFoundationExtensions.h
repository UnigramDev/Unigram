// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <mfobjects.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <Mferror.h>
#include <mfapi.h>

HRESULT MFScheduleWorkItem(_In_ IMFAsyncCallback* pCallback, _In_ IUnknown* pState, _In_ INT64 Timeout, _Out_ MFWORKITEM_KEY* pKey);
HRESULT MFRegisterLocalByteStreamHandler(_In_ PCWSTR szFileExtension, _In_ PCWSTR szMimeType, _In_ IMFActivate* pActivate);

MIDL_INTERFACE("EAECB74A-9A50-42ce-9541-6A7F57AA4AD7")
IMFFinalizableMediaSink : public IMFMediaSink
{
public:
	virtual HRESULT STDMETHODCALLTYPE BeginFinalize(
		/* [in] */ IMFAsyncCallback *pCallback,
		/* [in] */ IUnknown *punkState) = 0;

	virtual HRESULT STDMETHODCALLTYPE EndFinalize(
		/* [in] */ IMFAsyncResult *pResult) = 0;

};