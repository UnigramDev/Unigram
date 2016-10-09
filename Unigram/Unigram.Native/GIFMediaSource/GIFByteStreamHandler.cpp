#include "pch.h"
#include "GIFByteStreamHandler.h"

using namespace Unigram::Native;

ActivatableClass(GIFByteStreamHandler)

HRESULT GIFByteStreamHandler::SetProperties(ABI::Windows::Foundation::Collections::IPropertySet* pConfiguration)
{
	return S_OK;
}

HRESULT GIFByteStreamHandler::CancelObjectCreation(IUnknown* pIUnknownCancelCookie)
{
	return E_NOTIMPL;
}

HRESULT GIFByteStreamHandler::GetMaxNumberOfBytesRequiredForResolution(QWORD* pqwBytes)
{
	if (pqwBytes == nullptr)
		return E_POINTER;

	*pqwBytes = 6;
	return S_OK;
}

HRESULT GIFByteStreamHandler::BeginCreateObject(IMFByteStream* pByteStream, LPCWSTR pwszURL, DWORD dwFlags, IPropertyStore* pProps,
	IUnknown** ppIUnknownCancelCookie, IMFAsyncCallback* pCallback, IUnknown* pUnkState)
{
	if (pByteStream == nullptr)
		return E_POINTER;

	if (pCallback == NULL)
		return E_POINTER;

	if ((dwFlags & MF_RESOLUTION_MEDIASOURCE) == 0)
		return E_INVALIDARG;

	if (ppIUnknownCancelCookie)
		*ppIUnknownCancelCookie = nullptr;

	if (!(IsValidURL(pwszURL) && IsValidByteStream(pByteStream)))
		return MF_E_UNSUPPORTED_BYTESTREAM_TYPE;

	HRESULT result;
	ComPtr<GIFMediaSource> mediaSource;
	ReturnIfFailed(result, MakeAndInitialize<GIFMediaSource>(&mediaSource, pByteStream));

	ComPtr<IMFAsyncResult> asyncResult;
	ReturnIfFailed(result, MFCreateAsyncResult(reinterpret_cast<IUnknown*>(mediaSource.Get()), pCallback, pUnkState, &asyncResult));

	return MFInvokeCallback(asyncResult.Get());
}

HRESULT GIFByteStreamHandler::EndCreateObject(IMFAsyncResult *pResult, MF_OBJECT_TYPE* pObjectType, IUnknown** ppObject)
{
	if (pResult == nullptr || pObjectType == nullptr || ppObject == nullptr)
		return E_POINTER;

	*pObjectType = MF_OBJECT_MEDIASOURCE;
	return pResult->GetObject(ppObject);
}

bool GIFByteStreamHandler::IsValidURL(LPCWSTR url)
{
	if (url == nullptr)
		return true;

	auto pathLength = wcslen(url);
	if (pathLength < 4)
		return false;

	return _wcsnicmp(url + pathLength - 4, L".gif", 4) == 0;
}

bool GIFByteStreamHandler::IsValidByteStream(IMFByteStream* byteStream)
{
	DWORD capabilities;
	if (FAILED(byteStream->GetCapabilities(&capabilities)) || (capabilities & (MFBYTESTREAM_IS_READABLE | MFBYTESTREAM_IS_SEEKABLE)) == 0)
		return false;

	ULONG readBytes;
	char magicNumber[6];
	if (FAILED(byteStream->Read(reinterpret_cast<byte*>(magicNumber), ARRAYSIZE(magicNumber), &readBytes)) || FAILED(byteStream->SetCurrentPosition(0)))
		return false;

	return strncmp(magicNumber, "GIF87a", 6) == 0 || strncmp(magicNumber, "GIF89a", 6) == 0;
}