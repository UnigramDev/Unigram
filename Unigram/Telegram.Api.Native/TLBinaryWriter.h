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
						virtual void STDMETHODCALLTYPE Reset() = 0;
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

				class TLBinaryWriter WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLBinaryWriterEx>, ITLBinaryWriter>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLBinaryWriter, BaseTrust);

				public:
					TLBinaryWriter();
					~TLBinaryWriter();

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
					IFACEMETHODIMP WriteBool(boolean value);
					IFACEMETHODIMP WriteString(HSTRING value);
					IFACEMETHODIMP WriteByteArray(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value);
					IFACEMETHODIMP WriteDouble(double value);
					IFACEMETHODIMP WriteFloat(float value);
					IFACEMETHODIMP WriteObject(_In_ ITLObject* value);
					IFACEMETHODIMP WriteRawBuffer(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value);
					IFACEMETHODIMP WriteBigEndianInt32(INT32 value);
					IFACEMETHODIMP WriteWString(_In_ std::wstring const& string);
					IFACEMETHODIMP WriteBuffer(_In_reads_(length) BYTE const* buffer, UINT32 length);
					IFACEMETHODIMP_(void) Reset();

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ IBuffer* underlyingBuffer);
					STDMETHODIMP RuntimeClassInitialize(_In_ TLBinaryWriter* writer);
					STDMETHODIMP RuntimeClassInitialize(UINT32 capacity);

					inline BYTE* GetBuffer() const
					{
						return m_buffer;
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

					inline boolean HasUnstoredBuffer() const
					{
						return m_position < m_capacity;
					}

					inline IBuffer* GetUnderlyingBuffer() const
					{
						return m_underlyingBuffer.Get();
					}

				private:
					HRESULT WriteString(_In_ LPCWCHAR buffer, UINT32 length);

					BYTE* m_buffer;
					UINT32 m_position;
					UINT32 m_capacity;
					ComPtr<IBuffer> m_underlyingBuffer;
				};

				class TLObjectSizeCalculator : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<ITLBinaryWriterEx>>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLBinaryWriter, BaseTrust);

				public:
					TLObjectSizeCalculator();
					~TLObjectSizeCalculator();

					//COM exported methods
					IFACEMETHODIMP get_TotalLength(_Out_  UINT32* value);
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
					IFACEMETHODIMP WriteBool(boolean value);
					IFACEMETHODIMP WriteString(HSTRING value);
					IFACEMETHODIMP WriteByteArray(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value);
					IFACEMETHODIMP WriteDouble(double value);
					IFACEMETHODIMP WriteFloat(float value);
					IFACEMETHODIMP WriteObject(_In_ ITLObject* value);
					IFACEMETHODIMP WriteRawBuffer(UINT32 __valueSize, _In_reads_(__valueSize) BYTE* value);
					IFACEMETHODIMP WriteBigEndianInt32(INT32 value);
					IFACEMETHODIMP WriteWString(_In_ std::wstring const& string);
					IFACEMETHODIMP WriteBuffer(_In_reads_(length) BYTE const* buffer, UINT32 length);
					IFACEMETHODIMP_(void) Reset();

					//Internal methods
					static HRESULT GetSize(_In_ ITLObject* object, _Out_ UINT32* value);

				private:
					UINT32 m_position;
					UINT32 m_length;

					static thread_local ComPtr<TLObjectSizeCalculator> s_instance;
				};

			}
		}
	}
}