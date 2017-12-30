#pragma once
#include <wrl.h>
#include <windows.storage.streams.h>
#include <robuffer.h>
#include "Helpers\COMHelper.h"

#define USE_COTASKMEM 0
#if USE_COTASKMEM
#define NATIVEBUFFER_ALLOC(length) CoTaskMemAlloc(capacity)
#define NATIVEBUFFER_REALLOC(buffer, length) CoTaskMemRealloc(buffer, capacity)
#define NATIVEBUFFER_FREE(buffer) CoTaskMemFree(buffer)
#else
#define NATIVEBUFFER_ALLOC(length) HeapAlloc(GetProcessHeap(), NULL, length)
#define NATIVEBUFFER_REALLOC(buffer, length) HeapReAlloc(GetProcessHeap(), NULL, buffer, length)
#define NATIVEBUFFER_FREE(buffer) HeapFree(GetProcessHeap(), NULL, buffer)
#endif

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Windows::Storage::Streams::IBuffer;
using Windows::Storage::Streams::IBufferByteAccess;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class NativeBuffer : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IBuffer, IBufferByteAccess>
			{
				//InspectableClass(L"Telegram.Api.Native.NativeBuffer", BaseTrust);
				InspectableClass(L"Windows.Storage.Streams.Buffer", BaseTrust);
				

			public:
				NativeBuffer() :
					m_capacity(0),
					m_buffer(nullptr)
				{
				}

				~NativeBuffer()
				{
					NATIVEBUFFER_FREE(m_buffer);
				}

				//COM exported methods
				IFACEMETHODIMP Buffer(_Out_ BYTE** value)
				{
					if (value == nullptr)
					{
						return E_POINTER;
					}

					*value = m_buffer;
					return S_OK;
				}

				IFACEMETHODIMP get_Capacity(_Out_ UINT32* value)
				{
					if (value == nullptr)
					{
						return E_POINTER;
					}

					*value = m_capacity;
					return S_OK;
				}

				IFACEMETHODIMP get_Length(_Out_ UINT32* value)
				{
					if (value == nullptr)
					{
						return E_POINTER;
					}

					*value = m_capacity;
					return S_OK;
				}

				IFACEMETHODIMP put_Length(UINT32 value)
				{
					return E_ILLEGAL_METHOD_CALL;
				}

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(UINT32 capacity)
				{
					if ((m_buffer = reinterpret_cast<BYTE*>(NATIVEBUFFER_ALLOC(capacity))) == nullptr)
					{
						return E_OUTOFMEMORY;
					}

					m_capacity = capacity;
					return S_OK;
				}

				STDMETHODIMP RuntimeClassInitialize(_In_reads_(length) BYTE const* buffer, UINT32 length)
				{
					if ((m_buffer = reinterpret_cast<BYTE*>(NATIVEBUFFER_ALLOC(length))) == nullptr)
					{
						return E_OUTOFMEMORY;
					}

					CopyMemory(m_buffer, buffer, length);

					m_capacity = length;
					return S_OK;
				}

				HRESULT Resize(UINT32 capacity)
				{
					auto buffer = reinterpret_cast<BYTE*>(NATIVEBUFFER_REALLOC(m_buffer, capacity));
					if (buffer == nullptr)
					{
						return E_OUTOFMEMORY;
					}

					m_buffer = buffer;
					m_capacity = capacity;
					return S_OK;
				}

				HRESULT Merge(_In_ IBuffer* buffer)
				{
					HRESULT result;
					ComPtr<IBufferByteAccess> bufferByteAccess;
					ReturnIfFailed(result, buffer->QueryInterface(IID_PPV_ARGS(&bufferByteAccess)));

					BYTE* bufferBytes;
					ReturnIfFailed(result, bufferByteAccess->Buffer(&bufferBytes));

					UINT32 capacity;
					ReturnIfFailed(result, buffer->get_Capacity(&capacity));

					auto currrentCapacity = m_capacity;
					ReturnIfFailed(result, Resize(m_capacity + capacity));

					CopyMemory(m_buffer + currrentCapacity, bufferBytes, capacity);
					return S_OK;
				}

				HRESULT Merge(_In_reads_(length) BYTE const* buffer, UINT32 length)
				{
					auto currrentCapacity = m_capacity;

					HRESULT result;
					ReturnIfFailed(result, Resize(m_capacity + length));

					CopyMemory(m_buffer + currrentCapacity, buffer, length);
					return S_OK;
				}

				inline BYTE* GetBuffer() const
				{
					return m_buffer;
				}

				inline UINT32 GetCapacity() const
				{
					return m_capacity;
				}

			private:
				UINT32 m_capacity;
				BYTE* m_buffer;
			};

			class MappedFileBuffer : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IBuffer, IBufferByteAccess>
			{
				//InspectableClass(L"Telegram.Api.Native.MappedFileBuffer", BaseTrust);
				InspectableClass(L"Windows.Storage.Streams.Buffer", BaseTrust);

			public:
				MappedFileBuffer() :
					m_capacity(0),
					m_buffer(nullptr)
				{
				}

				~MappedFileBuffer()
				{
					UnmapViewOfFile(m_buffer);
				}

				//COM exported methods
				IFACEMETHODIMP Buffer(_Out_ BYTE** value)
				{
					if (value == nullptr)
					{
						return E_POINTER;
					}

					*value = m_buffer;
					return S_OK;
				}

				IFACEMETHODIMP get_Capacity(_Out_ UINT32* value)
				{
					if (value == nullptr)
					{
						return E_POINTER;
					}

					*value = m_capacity;
					return S_OK;
				}

				IFACEMETHODIMP get_Length(_Out_ UINT32* value)
				{
					if (value == nullptr)
					{
						return E_POINTER;
					}

					*value = m_capacity;
					return S_OK;
				}

				IFACEMETHODIMP put_Length(UINT32 value)
				{
					return E_ILLEGAL_METHOD_CALL;
				}

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(_In_ HANDLE file, UINT32 capacity)
				{
					LARGE_INTEGER position;
					if (!SetFilePointerEx(file, { 0 }, &position, FILE_CURRENT))
					{
						return GetLastHRESULT();
					}

					m_fileMapping.Attach(CreateFileMapping(file, nullptr, PAGE_READWRITE, 0, 0, nullptr));
					if (!m_fileMapping.IsValid())
					{
						return GetLastHRESULT();
					}

					if ((m_buffer = reinterpret_cast<BYTE*>(MapViewOfFile(m_fileMapping.Get(), PAGE_READWRITE, position.HighPart, position.LowPart, capacity))) == nullptr)
					{
						return GetLastHRESULT();
					}

					m_capacity = capacity;
					return S_OK;
				}

			private:
				UINT32 m_capacity;
				BYTE* m_buffer;
				HandleT<HandleTraits::HANDLENullTraits> m_fileMapping;
			};

			class NativeBufferWrapper : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IBuffer, IBufferByteAccess>
			{
				friend class NativeBuffer;

				//InspectableClass(L"Telegram.Api.Native.NativeBufferWrapper", BaseTrust);
				InspectableClass(L"Windows.Storage.Streams.Buffer", BaseTrust);

			public:
				NativeBufferWrapper() :
					m_capacity(0),
					m_buffer(nullptr)
				{
				}

				~NativeBufferWrapper()
				{
				}

				//COM exported methods
				IFACEMETHODIMP Buffer(_Out_ BYTE** value)
				{
					if (value == nullptr)
					{
						return E_POINTER;
					}

					*value = m_buffer;
					return S_OK;
				}

				IFACEMETHODIMP get_Capacity(_Out_ UINT32* value)
				{
					if (value == nullptr)
					{
						return E_POINTER;
					}

					*value = m_capacity;
					return S_OK;
				}

				IFACEMETHODIMP get_Length(_Out_ UINT32* value)
				{
					if (value == nullptr)
					{
						return E_POINTER;
					}

					*value = m_capacity;
					return S_OK;
				}

				IFACEMETHODIMP put_Length(UINT32 value)
				{
					return E_ILLEGAL_METHOD_CALL;
				}

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(_In_ BYTE* buffer, UINT32 length)
				{
					if (buffer == nullptr)
					{
						return E_INVALIDARG;
					}

					m_capacity = length;
					m_buffer = buffer;
					return S_OK;
				}

				inline BYTE* GetBuffer() const
				{
					return m_buffer;
				}

				inline UINT32 GetCapacity() const
				{
					return m_capacity;
				}

			private:
				UINT32 m_capacity;
				BYTE* m_buffer;
			};

		}
	}
}