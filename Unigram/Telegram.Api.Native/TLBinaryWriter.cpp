#include "pch.h"
#include <memory>
#include "TLBinaryWriter.h"
#include "NativeBuffer.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;
using Windows::Storage::Streams::IBufferByteAccess;


TLBinaryWriter::TLBinaryWriter() :
	m_buffer(nullptr),
	m_position(0),
	m_capacity(0)
{
}

HRESULT TLBinaryWriter::RuntimeClassInitialize(IBuffer* underlyingBuffer)
{
	if (underlyingBuffer == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	ComPtr<IBufferByteAccess> bufferByteAccess;
	ReturnIfFailed(result, underlyingBuffer->QueryInterface(IID_PPV_ARGS(&bufferByteAccess)));
	ReturnIfFailed(result, bufferByteAccess->Buffer(&m_buffer));
	ReturnIfFailed(result, underlyingBuffer->get_Capacity(&m_capacity));

	m_underlyingBuffer = underlyingBuffer;
	return S_OK;
}

HRESULT TLBinaryWriter::RuntimeClassInitialize(TLBinaryWriter* writer)
{
	if (writer == nullptr)
	{
		return E_INVALIDARG;
	}

	if (writer->m_position > writer->m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer = writer->m_buffer + writer->m_position;
	m_capacity = writer->m_capacity - writer->m_position;
	m_underlyingBuffer = writer->m_underlyingBuffer;
	return S_OK;
}

HRESULT TLBinaryWriter::RuntimeClassInitialize(UINT32 capacity)
{
	HRESULT result;
	ComPtr<NativeBuffer> nativeBuffer;
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&nativeBuffer, capacity));

	m_buffer = nativeBuffer->GetBuffer();
	m_capacity = nativeBuffer->GetCapacity();
	m_underlyingBuffer = nativeBuffer;
	return S_OK;
}

TLBinaryWriter::~TLBinaryWriter()
{
}

HRESULT TLBinaryWriter::get_Position(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_position;
	return S_OK;
}

HRESULT TLBinaryWriter::put_Position(UINT32 value)
{
	if (value > m_capacity)
	{
		return E_BOUNDS;
	}

	/*if (value > m_position)
	{
		ZeroMemory(m_buffer + m_position, value - m_position);
	}*/

	m_position = value;
	return S_OK;
}

HRESULT TLBinaryWriter::get_UnstoredBufferLength(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_capacity - m_position;
	return S_OK;
}

HRESULT TLBinaryWriter::WriteByte(BYTE value)
{
	if (m_position + sizeof(BYTE) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer[m_position++] = value;
	return S_OK;
}

HRESULT TLBinaryWriter::WriteInt16(INT16 value)
{
	if (m_position + sizeof(INT16) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer[m_position++] = value & 0xff;
	m_buffer[m_position++] = (value >> 8) & 0xff;
	return S_OK;
}

HRESULT TLBinaryWriter::WriteUInt16(UINT16 value)
{
	return WriteInt16(*reinterpret_cast<INT16*>(&value));
}

HRESULT TLBinaryWriter::WriteInt32(INT32 value)
{
	if (m_position + sizeof(INT32) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer[m_position++] = value & 0xff;
	m_buffer[m_position++] = (value >> 8) & 0xff;
	m_buffer[m_position++] = (value >> 16) & 0xff;
	m_buffer[m_position++] = (value >> 24) & 0xff;

	return S_OK;
}

HRESULT TLBinaryWriter::WriteUInt32(UINT32 value)
{
	return WriteInt32(*reinterpret_cast<INT32*>(&value));
}

HRESULT TLBinaryWriter::WriteInt64(INT64 value)
{
	if (m_position + sizeof(INT64) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer[m_position++] = value & 0xff;
	m_buffer[m_position++] = (value >> 8) & 0xff;
	m_buffer[m_position++] = (value >> 16) & 0xff;
	m_buffer[m_position++] = (value >> 24) & 0xff;
	m_buffer[m_position++] = (value >> 32) & 0xff;
	m_buffer[m_position++] = (value >> 40) & 0xff;
	m_buffer[m_position++] = (value >> 48) & 0xff;
	m_buffer[m_position++] = (value >> 56) & 0xff;

	return S_OK;
}

HRESULT TLBinaryWriter::WriteUInt64(UINT64 value)
{
	return WriteInt64(*reinterpret_cast<INT64*>(&value));
}

HRESULT TLBinaryWriter::WriteBool(boolean value)
{
	if (value)
	{
		return WriteUInt32(0x997275b5);
	}
	else
	{
		return WriteUInt32(0xbc799737);
	}
}

HRESULT TLBinaryWriter::WriteString(HSTRING value)
{
	UINT32 length;
	auto buffer = WindowsGetStringRawBuffer(value, &length);
	return WriteString(buffer, length);
}

HRESULT TLBinaryWriter::WriteByteArray(UINT32 __valueSize, BYTE* value)
{
	return WriteBuffer(value, __valueSize);
}

HRESULT TLBinaryWriter::WriteDouble(double value)
{
	return WriteInt64(*reinterpret_cast<INT64*>(&value));
}

HRESULT TLBinaryWriter::WriteFloat(float value)
{
	return WriteInt32(*reinterpret_cast<INT32*>(&value));
}

HRESULT TLBinaryWriter::WriteObject(ITLObject* value)
{
	if (value == nullptr)
	{
		return WriteUInt32(0x56730BCC);
	}
	else
	{
		HRESULT result;
		UINT32 constructor;
		ReturnIfFailed(result, value->get_Constructor(&constructor));
		ReturnIfFailed(result, WriteUInt32(constructor));

		return value->Write(static_cast<ITLBinaryWriterEx*>(this));
	}
}

HRESULT TLBinaryWriter::WriteRawBuffer(UINT32 __valueSize, BYTE* value)
{
	if (value == nullptr)
	{
		return E_INVALIDARG;
	}

	if (m_position + __valueSize > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	CopyMemory(m_buffer + m_position, value, __valueSize);

	m_position += __valueSize;
	return S_OK;
}

HRESULT TLBinaryWriter::WriteBigEndianInt32(INT32 value)
{
	if (m_position + sizeof(INT32) > m_capacity)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer[m_position++] = (value >> 24) & 0xff;
	m_buffer[m_position++] = (value >> 16) & 0xff;
	m_buffer[m_position++] = (value >> 8) & 0xff;
	m_buffer[m_position++] = value & 0xff;

	return S_OK;
}

HRESULT TLBinaryWriter::WriteWString(std::wstring const& string)
{
	return WriteString(string.data(), static_cast<UINT32>(string.size()));
}

HRESULT TLBinaryWriter::WriteString(LPCWCHAR buffer, UINT32 length)
{
	auto mbLength = WideCharToMultiByte(CP_UTF8, 0, buffer, length, nullptr, 0, nullptr, nullptr);
	auto mbString = std::make_unique<char[]>(mbLength);
	WideCharToMultiByte(CP_UTF8, 0, buffer, length, mbString.get(), mbLength, nullptr, nullptr);

	return WriteBuffer(reinterpret_cast<BYTE*>(mbString.get()), mbLength);
}

HRESULT TLBinaryWriter::WriteBuffer(BYTE const* buffer, UINT32 length)
{
	UINT32 padding;

	if (length < 254)
	{
		padding = (length + 1) % 4;
		if (padding != 0)
		{
			padding = 4 - padding;
		}

		if (m_position + 1 + length + padding > m_capacity)
		{
			return E_NOT_SUFFICIENT_BUFFER;
		}

		m_buffer[m_position++] = length;
	}
	else
	{
		padding = (length + 4) % 4;
		if (padding != 0)
		{
			padding = 4 - padding;
		}

		if (m_position + 4 + length + padding > m_capacity)
		{
			return E_NOT_SUFFICIENT_BUFFER;
		}

		m_buffer[m_position++] = 254;
		m_buffer[m_position++] = length & 0xff;
		m_buffer[m_position++] = (length >> 8) & 0xff;
		m_buffer[m_position++] = (length >> 16) & 0xff;
	}

	CopyMemory(m_buffer + m_position, buffer, length);

	m_position += length + padding;

	/*m_position += length;

	ZeroMemory(m_buffer + m_position, padding);

	m_position += padding;*/
	return S_OK;
}

void TLBinaryWriter::Reset()
{
	m_position = 0;
}


thread_local ComPtr<TLObjectSizeCalculator> TLObjectSizeCalculator::s_instance = nullptr;

TLObjectSizeCalculator::TLObjectSizeCalculator() :
	m_position(0),
	m_length(0)
{
}

TLObjectSizeCalculator::~TLObjectSizeCalculator()
{
}

HRESULT TLObjectSizeCalculator::get_TotalLength(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = max(m_position, m_length);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::get_Position(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_position;
	return S_OK;
}

HRESULT TLObjectSizeCalculator::put_Position(UINT32 value)
{
	if (value > m_position)
	{
		m_length = value;
	}
	else
	{
		m_length = m_position;
	}

	m_position = value;
	return S_OK;
}

HRESULT TLObjectSizeCalculator::get_UnstoredBufferLength(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = UINT32_MAX - max(m_position, m_length);;
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteByte(BYTE value)
{
	m_position += sizeof(BYTE);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteInt16(INT16 value)
{
	m_position += sizeof(INT16);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteUInt16(UINT16 value)
{
	m_position += sizeof(UINT16);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteInt32(INT32 value)
{
	m_position += sizeof(INT32);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteUInt32(UINT32 value)
{
	m_position += sizeof(UINT32);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteInt64(INT64 value)
{
	m_position += sizeof(INT64);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteUInt64(UINT64 value)
{
	m_position += sizeof(UINT64);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteBool(boolean value)
{
	m_position += sizeof(UINT32);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteString(HSTRING value)
{
	UINT32 length;
	auto buffer = WindowsGetStringRawBuffer(value, &length);
	auto mbLength = WideCharToMultiByte(CP_UTF8, 0, buffer, length, nullptr, 0, nullptr, nullptr);
	return WriteBuffer(nullptr, mbLength);
}

HRESULT TLObjectSizeCalculator::WriteByteArray(UINT32 __valueSize, BYTE* value)
{
	return WriteBuffer(value, __valueSize);
}

HRESULT TLObjectSizeCalculator::WriteDouble(double value)
{
	m_position += sizeof(double);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteFloat(float value)
{
	m_position += sizeof(float);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteObject(ITLObject* value)
{
	if (value == nullptr)
	{
		m_position += sizeof(UINT32);
		return S_OK;
	}
	else
	{
		m_position += sizeof(UINT32);
		return value->Write(static_cast<ITLBinaryWriterEx*>(this));
	}
}

HRESULT TLObjectSizeCalculator::WriteRawBuffer(UINT32 __valueSize, BYTE* value)
{
	m_position += __valueSize;
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteWString(std::wstring const& string)
{
	auto mbLength = WideCharToMultiByte(CP_UTF8, 0, string.data(), static_cast<UINT32>(string.size()), nullptr, 0, nullptr, nullptr);
	return WriteBuffer(nullptr, mbLength);
}

HRESULT TLObjectSizeCalculator::WriteBigEndianInt32(INT32 value)
{
	m_position += sizeof(INT32);
	return S_OK;
}

HRESULT TLObjectSizeCalculator::WriteBuffer(BYTE const* buffer, UINT32 length)
{
	if (length < 254)
	{
		UINT32 padding = (length + 1) % 4;
		if (padding != 0)
		{
			padding = 4 - padding;
		}

		m_position += 1 + length + padding;
	}
	else
	{
		UINT32 padding = (length + 4) % 4;
		if (padding != 0)
		{
			padding = 4 - padding;
		}

		m_position += 4 + length + padding;
	}

	return S_OK;
}

void TLObjectSizeCalculator::Reset()
{
	m_position = 0;
	m_length = 0;
}

HRESULT TLObjectSizeCalculator::GetSize(ITLObject* object, UINT32* value)
{
	if (s_instance == nullptr)
	{
		s_instance = Make<TLObjectSizeCalculator>();
	}
	else
	{
		s_instance->Reset();
	}

	HRESULT result;
	ReturnIfFailed(result, s_instance->WriteObject(object));

	*value = max(s_instance->m_position, s_instance->m_length);
	return S_OK;
}