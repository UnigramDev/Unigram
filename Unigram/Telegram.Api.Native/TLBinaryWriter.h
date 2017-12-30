#pragma once
#include <string>
#include <vector>
#include <wrl.h>
#include <robuffer.h>
#include <windows.storage.streams.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::TL::ITLBinaryWriter;
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

					MIDL_INTERFACE("8A2AC333-54FD-4AF4-B7F3-9A049A3E73E8") ITLBinaryWriterEx : public ITLBinaryWriter
					{
					public:
						virtual HRESULT STDMETHODCALLTYPE WriteBigEndianInt32(INT32 value) = 0;
						virtual HRESULT STDMETHODCALLTYPE WriteWString(_In_ std::wstring const& string) = 0;
						virtual HRESULT STDMETHODCALLTYPE WriteBuffer(_In_reads_(length) BYTE const* buffer, UINT32 length) = 0;
						virtual HRESULT STDMETHODCALLTYPE Reset() = 0;
					};

				}
			}
		}
	}
}


using ABI::Telegram::Api::Native::TL::ITLBinaryWriterEx;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLBinaryWriter abstract : public Implements<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLBinaryWriterEx>, ITLBinaryWriter, IClosable>
				{
				public:
					//COM exported methods
					virtual HRESULT STDMETHODCALLTYPE get_Position(_Out_ UINT32* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE put_Position(UINT32 value) = 0;
					virtual HRESULT STDMETHODCALLTYPE get_UnstoredBufferLength(_Out_ UINT32* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE WriteByte(BYTE value) = 0;
					virtual HRESULT STDMETHODCALLTYPE WriteInt16(INT16 value) = 0;
					virtual HRESULT STDMETHODCALLTYPE WriteInt32(INT32 value) = 0;
					virtual HRESULT STDMETHODCALLTYPE WriteInt64(INT64 value) = 0;
					virtual HRESULT STDMETHODCALLTYPE WriteRawBuffer(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE WriteBigEndianInt32(INT32 value) = 0;
					virtual HRESULT STDMETHODCALLTYPE WriteBuffer(_In_reads_(length) BYTE const* buffer, UINT32 length) = 0;
					virtual HRESULT STDMETHODCALLTYPE Reset() = 0;
					virtual HRESULT STDMETHODCALLTYPE Close() = 0;
					IFACEMETHODIMP WriteUInt16(UINT16 value);
					IFACEMETHODIMP WriteUInt32(UINT32 value);
					IFACEMETHODIMP WriteUInt64(UINT64 value);
					IFACEMETHODIMP WriteBoolean(boolean value);
					IFACEMETHODIMP WriteString(HSTRING value);
					IFACEMETHODIMP WriteByteArray(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value);
					IFACEMETHODIMP WriteDouble(double value);
					IFACEMETHODIMP WriteFloat(float value);
					IFACEMETHODIMP WriteObject(_In_ ITLObject* value);
					IFACEMETHODIMP WriteWString(_In_ std::wstring const& string);
					IFACEMETHODIMP WriteVector(UINT32 __valueSize, _In_reads_(__valueSize) ITLObject** value);

					//Internal methods
					inline static UINT32 GetByteArrayLength(UINT32 length)
					{
						if (length < 254)
						{
							UINT32 padding = (length + 1) % 4;
							if (padding != 0)
							{
								padding = 4 - padding;
							}

							return 1 + length + padding;
						}
						else
						{
							UINT32 padding = (length + 4) % 4;
							if (padding != 0)
							{
								padding = 4 - padding;
							}

							return 4 + length + padding;
						}
					}

				private:
					HRESULT WriteString(_In_ LPCWCHAR buffer, UINT32 length);
				};

				class TLMemoryBinaryWriter WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLBinaryWriter>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLBinaryWriter, BaseTrust);

				public:
					TLMemoryBinaryWriter();
					~TLMemoryBinaryWriter();

					//COM exported methods
					IFACEMETHODIMP get_Position(_Out_ UINT32* value);
					IFACEMETHODIMP put_Position(UINT32 value);
					IFACEMETHODIMP get_UnstoredBufferLength(_Out_ UINT32* value);
					IFACEMETHODIMP WriteByte(BYTE value);
					IFACEMETHODIMP WriteInt16(INT16 value);
					IFACEMETHODIMP WriteInt32(INT32 value);
					IFACEMETHODIMP WriteInt64(INT64 value);
					IFACEMETHODIMP WriteRawBuffer(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value);
					IFACEMETHODIMP WriteBigEndianInt32(INT32 value);
					IFACEMETHODIMP WriteBuffer(_In_reads_(length) BYTE const* buffer, UINT32 length);
					IFACEMETHODIMP Reset();
					IFACEMETHODIMP Close();

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ IBuffer* underlyingBuffer);
					STDMETHODIMP RuntimeClassInitialize(_In_ TLMemoryBinaryWriter* writer, UINT32 length);
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

					inline UINT32 GetUnstoredBufferLength() const
					{
						return m_capacity - m_position;
					}

					inline bool HasUnstoredBuffer() const
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

				class TLFileBinaryWriter WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLBinaryWriter>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLBinaryWriter, BaseTrust);

				public:
					//COM exported methods
					IFACEMETHODIMP get_Position(_Out_ UINT32* value);
					IFACEMETHODIMP put_Position(UINT32 value);
					IFACEMETHODIMP get_UnstoredBufferLength(_Out_ UINT32* value);
					IFACEMETHODIMP WriteByte(BYTE value);
					IFACEMETHODIMP WriteInt16(INT16 value);
					IFACEMETHODIMP WriteInt32(INT32 value);
					IFACEMETHODIMP WriteInt64(INT64 value);
					IFACEMETHODIMP WriteRawBuffer(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value);
					IFACEMETHODIMP WriteBigEndianInt32(INT32 value);
					IFACEMETHODIMP WriteBuffer(_In_reads_(length) BYTE const* buffer, UINT32 length);
					IFACEMETHODIMP Reset();
					IFACEMETHODIMP Close();

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ LPCWSTR fileName, DWORD creationDisposition);

				private:
					FileHandle m_file;
				};

				class TLObjectSizeCalculator WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLBinaryWriterEx>, ITLBinaryWriter, IClosable>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLBinaryWriter, BaseTrust);

				public:
					TLObjectSizeCalculator();
					~TLObjectSizeCalculator();

					//COM exported methods
					IFACEMETHODIMP get_Position(_Out_ UINT32* value);
					IFACEMETHODIMP put_Position(UINT32 value);
					IFACEMETHODIMP get_UnstoredBufferLength(_Out_ UINT32* value);
					IFACEMETHODIMP WriteByte(BYTE value);
					IFACEMETHODIMP WriteInt16(INT16 value);
					IFACEMETHODIMP WriteUInt16(UINT16 value);
					IFACEMETHODIMP WriteInt32(INT32 value);
					IFACEMETHODIMP WriteUInt32(UINT32 value);
					IFACEMETHODIMP WriteInt64(INT64 value);
					IFACEMETHODIMP WriteUInt64(UINT64 value);
					IFACEMETHODIMP WriteBoolean(boolean value);
					IFACEMETHODIMP WriteString(HSTRING value);
					IFACEMETHODIMP WriteByteArray(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value);
					IFACEMETHODIMP WriteDouble(double value);
					IFACEMETHODIMP WriteFloat(float value);
					IFACEMETHODIMP WriteObject(_In_ ITLObject* value);
					IFACEMETHODIMP WriteVector(UINT32 __valueSize, _In_reads_(__valueSize) ITLObject** value);
					IFACEMETHODIMP WriteRawBuffer(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value);
					IFACEMETHODIMP WriteBigEndianInt32(INT32 value);
					IFACEMETHODIMP WriteWString(_In_ std::wstring const& string);
					IFACEMETHODIMP WriteBuffer(_In_reads_(length) BYTE const* buffer, UINT32 length);
					IFACEMETHODIMP Reset();
					IFACEMETHODIMP Close();

					//Internal methods
					HRESULT get_TotalLength(_Out_  UINT32* value);

					static HRESULT GetSize(_In_ ITLObject* object, _Out_ UINT32* value);

				private:
					UINT32 m_position;
					UINT32 m_length;
				};

			}
		}
	}
}