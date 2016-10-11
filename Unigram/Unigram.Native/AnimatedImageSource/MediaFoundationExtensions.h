#pragma once
#include <mfobjects.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <Mferror.h>
#include <mfapi.h>

HRESULT MFScheduleWorkItem(_In_ IMFAsyncCallback* pCallback, _In_ IUnknown* pState, _In_ INT64 Timeout, _Out_ MFWORKITEM_KEY* pKey);
HRESULT MFRegisterLocalByteStreamHandler(_In_ PCWSTR szFileExtension, _In_ PCWSTR szMimeType, _In_ IMFActivate* pActivate);