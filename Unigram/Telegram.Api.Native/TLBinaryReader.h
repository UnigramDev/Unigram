#pragma once
#include <string>
#include <vector>
#include <wrl.h>
#include <robuffer.h>
#include <windows.storage.streams.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::TL::ITLBinaryReader;
using ABI::Telegram::Api::Native::TL::ITLObject;
using ABI::Windows::Storage::Streams::IBuffer;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{
				namespace TL
				{

					MIDL_INTERFACE("AAFAB0A9-17F1-42D0-87DF-E188B2BE5BC7") ITLBinaryReaderEx : public ITLBinaryReader
					{
					public:
						virtual HRESULT STDMETHODCALLTYPE ReadBigEndianInt32(_Out_ INT32* value) = 0;
						virtual HRESULT STDMETHODCALLTYPE ReadWString(_Out_ std::wstring& string) = 0;
						virtual HRESULT STDMETHODCALLTYPE ReadBuffer(_Out_writes_(length) BYTE* buffer, UINT32 length) = 0;
						virtual void STDMETHODCALLTYPE Skip(UINT32 length) = 0;
						virtual void STDMETHODCALLTYPE Reset() = 0;
					};

				}
			}
		}
	}
}


using ABI::Telegram::Api::Native::TL::ITLBinaryReaderEx;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLBinaryReader WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLBinaryReaderEx>, ITLBinaryReader>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLBinaryReader, BaseTrust);

				public:
					TLBinaryReader();
					~TLBinaryReader();

					//COM exported methods
					IFACEMETHODIMP get_Position(_Out_ UINT32* value);
					IFACEMETHODIMP put_Position(UINT32 value);
					IFACEMETHODIMP get_UnconsumedBufferLength(_Out_ UINT32* value);
					IFACEMETHODIMP ReadByte(_Out_ BYTE* value);
					IFACEMETHODIMP ReadInt16(_Out_ INT16* value);
					IFACEMETHODIMP ReadUInt16(_Out_ UINT16* value);
					IFACEMETHODIMP ReadInt32(_Out_ INT32* value);
					IFACEMETHODIMP ReadUInt32(_Out_ UINT32* value);
					IFACEMETHODIMP ReadInt64(_Out_ INT64* value);
					IFACEMETHODIMP ReadUInt64(_Out_ UINT64* value);
					IFACEMETHODIMP ReadBool(_Out_ boolean* value);
					IFACEMETHODIMP ReadString(_Out_ HSTRING* value);
					IFACEMETHODIMP ReadByteArray(_Out_ UINT32* __valueSize, _Out_writes_(*__valueSize) BYTE** value);
					IFACEMETHODIMP ReadDouble(_Out_ double* value);
					IFACEMETHODIMP ReadFloat(_Out_ float* value);
					IFACEMETHODIMP ReadObject(_Out_ ITLObject** value);
					IFACEMETHODIMP ReadBigEndianInt32(_Out_ INT32* value);
					IFACEMETHODIMP ReadWString(_Out_ std::wstring& string);
					IFACEMETHODIMP ReadBuffer(_Out_writes_(length) BYTE* buffer, UINT32 length);
					IFACEMETHODIMP_(void) Skip(UINT32 length);
					IFACEMETHODIMP_(void) Reset();

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ IBuffer* underlyingBuffer);

					inline BYTE* GetBuffer() const
					{
						return m_buffer;
					}

					inline UINT32 GetCapacity() const
					{
						return m_capacity;
					}

				private:
					HRESULT ReadBuffer(_Out_ BYTE const** buffer, _Out_ UINT32* length);

					BYTE* m_buffer;
					UINT32 m_position;
					UINT32 m_capacity;
					ComPtr<IBuffer> m_underlyingBuffer;
				};

			}
		}
	}
}