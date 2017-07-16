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
using ABI::Windows::Foundation::IClosable;

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
						virtual HRESULT STDMETHODCALLTYPE ReadObjectAndConstructor(UINT32 objectSize, _Out_ UINT32* constructor, _Out_ ITLObject** value) = 0;
						virtual HRESULT STDMETHODCALLTYPE ReadBigEndianInt32(_Out_ INT32* value) = 0;
						virtual HRESULT STDMETHODCALLTYPE ReadWString(_Out_ std::wstring& string) = 0;
						virtual HRESULT STDMETHODCALLTYPE ReadBuffer(_Out_writes_(length) BYTE* buffer, UINT32 length) = 0;
						virtual HRESULT STDMETHODCALLTYPE ReadBuffer2(_Out_ BYTE const** buffer, _Out_ UINT32* length) = 0;
						virtual HRESULT STDMETHODCALLTYPE ReadRawBuffer2(_Out_ BYTE const** buffer, UINT32 length) = 0;
						virtual HRESULT STDMETHODCALLTYPE Reset() = 0;
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

				class TLBinaryReader abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLBinaryReaderEx>, ITLBinaryReader, IClosable>
				{
				public:
					virtual HRESULT STDMETHODCALLTYPE get_Position(_Out_ UINT32* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE put_Position(UINT32 value) = 0;
					virtual HRESULT STDMETHODCALLTYPE get_UnconsumedBufferLength(_Out_ UINT32* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE ReadByte(_Out_ BYTE* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE ReadInt16(_Out_ INT16* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE ReadInt32(_Out_ INT32* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE ReadInt64(_Out_ INT64* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE ReadObjectAndConstructor(UINT32 objectSize, _Out_ UINT32* constructor, _Out_ ITLObject** value) = 0;
					virtual HRESULT STDMETHODCALLTYPE ReadBigEndianInt32(_Out_ INT32* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE ReadRawBuffer(UINT32 __valueSize, _Out_writes_(__valueSize) BYTE* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE ReadBuffer2(_Out_ BYTE const** buffer, _Out_ UINT32* length) = 0;
					virtual HRESULT STDMETHODCALLTYPE ReadRawBuffer2(_Out_ BYTE const** buffer, UINT32 length) = 0;
					virtual HRESULT STDMETHODCALLTYPE Reset() = 0;
					virtual HRESULT STDMETHODCALLTYPE Close() = 0;
					IFACEMETHODIMP ReadUInt16(_Out_ UINT16* value);
					IFACEMETHODIMP ReadUInt32(_Out_ UINT32* value);
					IFACEMETHODIMP ReadUInt64(_Out_ UINT64* value);
					IFACEMETHODIMP ReadBoolean(_Out_ boolean* value);
					IFACEMETHODIMP ReadString(_Out_ HSTRING* value);
					IFACEMETHODIMP ReadByteArray(_Out_ UINT32* __valueSize, _Out_writes_(*__valueSize) BYTE** value);
					IFACEMETHODIMP ReadDouble(_Out_ double* value);
					IFACEMETHODIMP ReadFloat(_Out_ float* value);
					IFACEMETHODIMP ReadObject(_Out_ ITLObject** value);
					IFACEMETHODIMP ReadWString(_Out_ std::wstring& string);
					IFACEMETHODIMP ReadBuffer(_Out_writes_(length) BYTE* buffer, UINT32 length);
					IFACEMETHODIMP ReadVector(_Out_ UINT32* __valueSize, _Out_writes_(*__valueSize) ITLObject*** value);
				};

				class TLMemoryBinaryReader WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLBinaryReader>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLBinaryReader, BaseTrust);

				public:
					TLMemoryBinaryReader();
					~TLMemoryBinaryReader();

					//COM exported methods
					IFACEMETHODIMP get_Position(_Out_ UINT32* value);
					IFACEMETHODIMP put_Position(UINT32 value);
					IFACEMETHODIMP get_UnconsumedBufferLength(_Out_ UINT32* value);
					IFACEMETHODIMP ReadByte(_Out_ BYTE* value);
					IFACEMETHODIMP ReadInt16(_Out_ INT16* value);
					IFACEMETHODIMP ReadInt32(_Out_ INT32* value);
					IFACEMETHODIMP ReadInt64(_Out_ INT64* value);
					IFACEMETHODIMP ReadObject(_Out_ ITLObject** value);
					IFACEMETHODIMP ReadObjectAndConstructor(UINT32 objectSize, _Out_ UINT32* constructor, _Out_ ITLObject** value);
					IFACEMETHODIMP ReadBigEndianInt32(_Out_ INT32* value);
					IFACEMETHODIMP ReadRawBuffer(UINT32 __valueSize, _Out_writes_(__valueSize) BYTE* value);
					IFACEMETHODIMP ReadBuffer2(_Out_ BYTE const** buffer, _Out_ UINT32* length);
					IFACEMETHODIMP ReadRawBuffer2(_Out_ BYTE const** buffer, UINT32 length);
					IFACEMETHODIMP Reset();
					IFACEMETHODIMP Close();

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ IBuffer* underlyingBuffer);
					STDMETHODIMP RuntimeClassInitialize(_In_ TLMemoryBinaryReader* reader, UINT32 length);
					STDMETHODIMP RuntimeClassInitialize(UINT32 capacity);
					HRESULT SeekCurrent(INT32 bytes);

					inline BYTE* GetBuffer() const
					{
						return m_buffer;
					}

					inline BYTE* GetBufferAtPosition() const
					{
						return m_buffer + m_position;
					}

					inline UINT32 GetPosition() const
					{
						return m_position;
					}

					inline UINT32 GetCapacity() const
					{
						return m_capacity;
					}

					inline UINT32 GetUnconsumedBufferLength() const
					{
						return m_capacity - m_position;
					}

					inline bool HasUnconsumedBuffer() const
					{
						return m_position < m_capacity;
					}

					inline IBuffer* GetUnderlyingBuffer() const
					{
						return m_underlyingBuffer.Get();
					}

				private:
					BYTE* m_buffer;
					UINT32 m_position;
					UINT32 m_capacity;
					ComPtr<IBuffer> m_underlyingBuffer;
				};

				class TLFileBinaryReader WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLBinaryReader>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLBinaryReader, BaseTrust);

				public:
					//COM exported methods
					IFACEMETHODIMP get_Position(_Out_ UINT32* value);
					IFACEMETHODIMP put_Position(UINT32 value);
					IFACEMETHODIMP get_UnconsumedBufferLength(_Out_ UINT32* value);
					IFACEMETHODIMP ReadByte(_Out_ BYTE* value);
					IFACEMETHODIMP ReadInt16(_Out_ INT16* value);
					IFACEMETHODIMP ReadInt32(_Out_ INT32* value);
					IFACEMETHODIMP ReadInt64(_Out_ INT64* value);
					IFACEMETHODIMP ReadObjectAndConstructor(UINT32 objectSize, _Out_ UINT32* constructor, _Out_ ITLObject** value);
					IFACEMETHODIMP ReadBigEndianInt32(_Out_ INT32* value);
					IFACEMETHODIMP ReadRawBuffer(UINT32 __valueSize, _Out_writes_(__valueSize) BYTE* value);
					IFACEMETHODIMP ReadBuffer2(_Out_ BYTE const** buffer, _Out_ UINT32* length);
					IFACEMETHODIMP ReadRawBuffer2(_Out_ BYTE const** buffer, UINT32 length);
					IFACEMETHODIMP Reset();
					IFACEMETHODIMP Close();

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ LPCWSTR fileName, DWORD creationDisposition);

				private:
					std::vector<BYTE> m_buffer;
					FileHandle m_file;
				};

			}
		}
	}
}