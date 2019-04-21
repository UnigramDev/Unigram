#pragma once

namespace NativeBuffer
{
	ref class NativeBufferFactory
	{
	internal:
		static Windows::Storage::Streams::IBuffer ^CreateNativeBuffer(DWORD nNumberOfBytes);
		static Windows::Storage::Streams::IBuffer ^CreateNativeBuffer(LPVOID lpBuffer, DWORD nNumberOfBytes);
		static Windows::Storage::Streams::IBuffer ^CreateNativeBuffer(LPVOID lpBuffer, DWORD nNumberOfBytes, void(*free)(void *opaque), void *opaque);
		static Windows::Storage::Streams::IBuffer ^CreateNativeBuffer(LPVOID lpBuffer, DWORD nNumberOfBytes, Platform::Object^ pObject);
	};
}