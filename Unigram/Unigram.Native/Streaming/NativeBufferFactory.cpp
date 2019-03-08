#include "pch.h"

#include "NativeBuffer.h"
#include "NativeBufferFactory.h"

using namespace NativeBuffer;

Windows::Storage::Streams::IBuffer^ NativeBufferFactory::CreateNativeBuffer(DWORD nNumberOfBytes)
{
	auto lpBuffer = (byte*)malloc(nNumberOfBytes);
	return CreateNativeBuffer(lpBuffer, nNumberOfBytes, &free, lpBuffer);
}

Windows::Storage::Streams::IBuffer^ NativeBufferFactory::CreateNativeBuffer(LPVOID lpBuffer, DWORD nNumberOfBytes)
{
	return CreateNativeBuffer(lpBuffer, nNumberOfBytes, NULL, NULL);
}

Windows::Storage::Streams::IBuffer^ NativeBufferFactory::CreateNativeBuffer(LPVOID lpBuffer, DWORD nNumberOfBytes, void(*free)(void *opaque), void *opaque)
{
	Microsoft::WRL::ComPtr<NativeBuffer> nativeBuffer;
	Microsoft::WRL::Details::MakeAndInitialize<NativeBuffer>(&nativeBuffer, (byte *)lpBuffer, nNumberOfBytes, free, opaque);
	auto iinspectable = (IInspectable *)reinterpret_cast<IInspectable *>(nativeBuffer.Get());
	Windows::Storage::Streams::IBuffer ^buffer = reinterpret_cast<Windows::Storage::Streams::IBuffer ^>(iinspectable);

	return buffer;
}

Windows::Storage::Streams::IBuffer^ NativeBufferFactory::CreateNativeBuffer(LPVOID lpBuffer, DWORD nNumberOfBytes, Platform::Object^ pObject)
{
	Microsoft::WRL::ComPtr<NativeBuffer> nativeBuffer;
	Microsoft::WRL::Details::MakeAndInitialize<NativeBuffer>(&nativeBuffer, (byte *)lpBuffer, nNumberOfBytes, pObject);
	auto iinspectable = (IInspectable *)reinterpret_cast<IInspectable *>(nativeBuffer.Get());
	Windows::Storage::Streams::IBuffer ^buffer = reinterpret_cast<Windows::Storage::Streams::IBuffer ^>(iinspectable);

	return buffer;
}