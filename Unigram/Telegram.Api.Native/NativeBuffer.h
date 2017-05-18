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
					//m_length(0),
					m_buffer(nullptr)
				{
				}

				~NativeBuffer()
				{
					CoTaskMemFree(m_buffer);
				}

				STDMETHODIMP RuntimeClassInitialize(UINT32 capacity)
				{
					if ((m_buffer = reinterpret_cast<BYTE*>(CoTaskMemAlloc(capacity))) == nullptr)
					{
						return E_OUTOFMEMORY;
					}

					m_capacity = capacity;
					return S_OK;
				}

				STDMETHODIMP Buffer(_Out_ BYTE** value)
				{
					if (value == nullptr)
					{
						return E_POINTER;
					}

					*value = m_buffer;
					return S_OK;
				}

				STDMETHODIMP get_Capacity(_Out_ UINT32* value)
				{
					if (value == nullptr)
					{
						return E_POINTER;
					}

					*value = m_capacity;
					return S_OK;
				}

				STDMETHODIMP get_Length(_Out_ UINT32* value)
				{
					if (value == nullptr)
					{
						return E_POINTER;
					}

					*value = m_capacity;
					return S_OK;
				}

				STDMETHODIMP put_Length(UINT32 value)
				{
					/*if (value > m_capacity)
					{
						return E_BOUNDS;
					}

					m_length = value;
					return S_OK;*/

					return E_ILLEGAL_METHOD_CALL;
				}

			private:
				//UINT32 m_length;
				UINT32 m_capacity;
				BYTE* m_buffer;
			};

		}
	}
}