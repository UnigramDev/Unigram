#pragma once
#include <wrl.h>
#include <windows.storage.streams.h>
#include <robuffer.h>

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