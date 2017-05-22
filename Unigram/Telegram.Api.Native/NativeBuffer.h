#pragma once
#include <wrl.h>
#include <windows.storage.streams.h>
#include <robuffer.h>
#include "Helpers\COMHelper.h"

using namespace Microsoft::WRL;
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
				InspectableClass(L"Telegram.Api.Native.NativeBuffer", BaseTrust);

			public:
				NativeBuffer() :
					m_capacity(0),
					m_buffer(nullptr)
				{
				}

				~NativeBuffer()
				{
					CoTaskMemFree(m_buffer);
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
					if ((m_buffer = reinterpret_cast<BYTE*>(CoTaskMemAlloc(capacity))) == nullptr)
					{
						return E_OUTOFMEMORY;
					}

					m_capacity = capacity;
					return S_OK;
				}

				STDMETHODIMP RuntimeClassInitialize(_In_reads_(length) BYTE const* buffer, UINT32 length)
				{
					if ((m_buffer = reinterpret_cast<BYTE*>(CoTaskMemAlloc(length))) == nullptr)
					{
						return E_OUTOFMEMORY;
					}

					CopyMemory(m_buffer, buffer, length);

					m_capacity = length;
					return S_OK;
				}

				HRESULT Resize(UINT32 capacity)
				{
					auto buffer = reinterpret_cast<BYTE*>(CoTaskMemRealloc(m_buffer, capacity));
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
					ReturnIfFailed(result, Resize(m_capacity + capacity));

					CopyMemory(m_buffer + m_capacity, bufferBytes, capacity);
					return S_OK;
				}

				HRESULT Merge(_In_reads_(length) BYTE const* buffer, UINT32 length)
				{
					HRESULT result;
					ReturnIfFailed(result, Resize(m_capacity + length));

					CopyMemory(m_buffer + m_capacity, buffer, length);
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

			class NativeBufferWrapper : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IBuffer, IBufferByteAccess>
			{
				friend class NativeBuffer;

				InspectableClass(L"Telegram.Api.Native.NativeBufferWrapper", BaseTrust);

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
				STDMETHODIMP RuntimeClassInitialize(_In_ NativeBuffer* wrappedBuffer, UINT32 offset, UINT32 length)
				{
					if (offset + length > wrappedBuffer->GetCapacity())
					{
						return E_INVALIDARG;
					}

					m_capacity = length;
					m_buffer = wrappedBuffer->GetBuffer() + offset;
					m_wrappedBuffer = wrappedBuffer;
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
				ComPtr<NativeBuffer> m_wrappedBuffer;
			};

		}
	}
}