#pragma once 
#include <stdio.h>
#include <WinSock2.h>
#include <wrl\client.h>
#include <wrl\wrappers\corewrappers.h>

using ::Platform::Exception;
using namespace Microsoft::WRL::Wrappers;

#define ReturnIfFailed(result, method) \
	if(FAILED(result = method)) \
		return result

#define BreakIfFailed(result, method) \
	if(FAILED(result = method)) \
		break

inline HRESULT GetLastHRESULT()
{
	return HRESULT_FROM_WIN32(GetLastError());
}

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
	throw Exception::CreateException(HRESULT_FROM_WIN32(WSAGetLastError()));
}