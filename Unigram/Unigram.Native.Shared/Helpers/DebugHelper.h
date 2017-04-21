#pragma once
#include <windows.h> 
#include <stdio.h>
#include <dxgi.h> 
#include <dxgi1_2.h> 
#include <d2d1_1.h> 
#include <d2d1_2.h>
#include <d3d11.h> 
#include <d3d11_2.h> 
#include <dwrite.h>
#include <DirectXMath.h> 

inline void OutputDebugStringFormat(LPCWSTR pwhFormat, ...)
{
	va_list args;
	va_start(args, pwhFormat);
	WCHAR buffer[1024];
	vswprintf_s(buffer, 1024, pwhFormat, args);
	OutputDebugStringW(buffer);
	va_end(args);
}

template<UINT TNameLength>
inline void SetDebugObjectName(_In_ ID3D11DeviceChild* resource, _In_z_ const char(&name)[TNameLength])
{
	ThrowIfFailed(resource->SetPrivateData(WKPDID_D3DDebugObjectName, TNameLength - 1, name));
}

template<UINT TNameLength>
inline void SetDebugObjectName(_In_ ID3D11Device* device, _In_z_ const char(&name)[TNameLength])
{
	ThrowIfFailed(device->SetPrivateData(WKPDID_D3DDebugObjectName, TNameLength - 1, name));
}

template<UINT TNameLength>
inline void SetDebugObjectName(_In_ IDXGIObject* object, _In_z_ const char(&name)[TNameLength])
{
	ThrowIfFailed(object->SetPrivateData(WKPDID_D3DDebugObjectName, TNameLength - 1, name));
}