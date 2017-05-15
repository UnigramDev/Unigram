#include "pch.h"
#include <memory>
#include "TLBinaryWriter.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


TLBinaryWriter::TLBinaryWriter(BYTE* buffer, UINT32 length) :
	m_buffer(buffer),
	m_position(0),
	m_length(length)
{
}

TLBinaryWriter::~TLBinaryWriter()
{
}

HRESULT TLBinaryWriter::get_UnstoredBufferLength(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_length - m_position;
	return S_OK;
}

HRESULT TLBinaryWriter::WriteByte(BYTE value)
{
	if (m_position + sizeof(value) > m_length)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer[m_position++] = value;
	return S_OK;
}

HRESULT TLBinaryWriter::WriteInt16(INT16 value)
{
	if (m_position + sizeof(value) > m_length)
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
	if (m_position + sizeof(value) > m_length)
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
	if (m_position + sizeof(value) > m_length)
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

HRESULT TLBinaryWriter::WriteString(std::wstring string)
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

		if (m_position + 1 + length + padding > m_length)
		{
			return E_NOT_SUFFICIENT_BUFFER;
		}

		m_buffer[m_position++] = length;
	}
	else
	{
		padding = (length + 3) % 4;
		if (padding != 0)
		{
			padding = 4 - padding;
		}

		if (m_position + 4 + length + padding > m_length)
		{
			return E_NOT_SUFFICIENT_BUFFER;
		}

		m_buffer[m_position++] = 254;
		m_buffer[m_position++] = length & 0xff;
		m_buffer[m_position++] = (length >> 8) & 0xff;
		m_buffer[m_position++] = (length >> 16) & 0xff;
	}

	CopyMemory(&m_buffer[m_position], buffer, length);
	ZeroMemory(&m_buffer[m_position += length], padding);

	m_position += padding;
	return S_OK;
}

void TLBinaryWriter::Reset()
{
	m_position = 0;
}

void TLBinaryWriter::Skip(UINT32 length)
{
	m_position += length;
}