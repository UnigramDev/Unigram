#pragma once 
#include <wrl\client.h>

using ::Platform::Exception;

#define ReturnIfFailed(result, method) \
	if(FAILED(result = method)) \
		return result

inline void ThrowIfFailed(HRESULT hr)
{
	if (FAILED(hr))
		throw Exception::CreateException(hr);
}

inline void ThrowException(HRESULT hr)
{
	throw Exception::CreateException(hr);
}