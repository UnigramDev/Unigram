#pragma once 
#include <string>
#include <stdio.h>
#include <WinSock2.h>
#include <wrl\client.h>
#include <wrl\wrappers\corewrappers.h>

using namespace Microsoft::WRL::Wrappers;

#define ReturnIfFailed(result, method) \
	if(FAILED(result = method)) \
		return result

#define BreakIfFailed(result, method) \
	if(FAILED(result = method)) \
		break

inline HRESULT WindowsCreateString(std::wstring const& wstring, _Out_ HSTRING* hstring)
{
	return WindowsCreateString(wstring.c_str(), wstring.length(), hstring);
}

inline HRESULT GetLastHRESULT()
{
	return HRESULT_FROM_WIN32(GetLastError());
}

inline HRESULT GetWSALastHRESULT()
{
	return HRESULT_FROM_WIN32(WSAGetLastError());
}


#ifdef __cplusplus_winrt

using ::Platform::Exception;

inline void ThrowIfFailed(HRESULT hr)
{
	if (FAILED(hr))
		throw Exception::CreateException(hr);
}

inline void ThrowException(HRESULT hr)
{
	throw Exception::CreateException(hr);
}

inline void ThrowLastError()
{
	throw Exception::CreateException(GetLastHRESULT());
}

inline void ThrowWSALastError()
{
	throw Exception::CreateException(GetWSALastHRESULT());
}

#endif