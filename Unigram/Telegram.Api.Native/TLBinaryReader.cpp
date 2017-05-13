#include "pch.h"
#include "TLBinaryReader.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


TLBinaryReader::TLBinaryReader() :
	m_buffer(nullptr),
	m_position(0),
	m_length(0)
{
}

TLBinaryReader::~TLBinaryReader()
{
}

HRESULT TLBinaryReader::RuntimeClassInitialize()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT TLBinaryReader::ReadByte(BYTE* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_position + sizeof(value) > m_length)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = m_buffer[m_position++];
	return S_OK;
}

HRESULT TLBinaryReader::ReadInt16(INT16* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_position + sizeof(value) > m_length)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = ((m_buffer[m_position++] & 0xff)) | ((m_buffer[m_position++] & 0xff) << 8);
	return S_OK;
}

HRESULT TLBinaryReader::ReadUInt16(UINT16* value)
{
	return ReadInt16(reinterpret_cast<INT16*>(value));
}

HRESULT TLBinaryReader::ReadInt32(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_position + sizeof(value) > m_length)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = ((m_buffer[m_position++] & 0xff)) | ((m_buffer[m_position++] & 0xff) << 8) |
		((m_buffer[m_position++] & 0xff) << 16) | ((m_buffer[m_position++] & 0xff) << 24);
	return S_OK;
}

HRESULT TLBinaryReader::ReadUInt32(UINT32* value)
{
	return ReadInt32(reinterpret_cast<INT32*>(value));
}

HRESULT TLBinaryReader::ReadInt64(INT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_position + sizeof(value) > m_length)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = ((m_buffer[m_position++] & 0xffLL)) | ((m_buffer[m_position++] & 0xffLL) << 8LL) |
		((m_buffer[m_position++] & 0xffLL) << 16LL) | ((m_buffer[m_position++] & 0xffLL) << 24LL) |
		((m_buffer[m_position++] & 0xffLL) << 32LL) | ((m_buffer[m_position++] & 0xffLL) << 40LL) |
		((m_buffer[m_position++] & 0xffLL) << 48LL) | ((m_buffer[m_position++] & 0xffLL) << 56LL);
	return S_OK;
}

HRESULT TLBinaryReader::ReadUInt64(UINT64* value)
{
	return ReadInt64(reinterpret_cast<INT64*>(value));
}

HRESULT TLBinaryReader::ReadBool(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	UINT32 constructor;
	ReturnIfFailed(result, ReadUInt32(&constructor));

	if (constructor == 0x997275b5)
	{
		*value = true;
	}
	else if (constructor == 0xbc799737)
	{
		*value = false;
	}
	else
	{
		return E_FAIL;
	}

	return S_OK;
}

HRESULT TLBinaryReader::ReadString(HSTRING* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	std::wstring string;
	ReturnIfFailed(result, ReadString(string));

	return WindowsCreateString(string, value);
}

HRESULT TLBinaryReader::ReadByteArray(UINT32* __valueSize, BYTE** value)
{
	if (__valueSize == nullptr || value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	UINT32 length;
	BYTE const* buffer;
	ReturnIfFailed(result, ReadBuffer(&buffer, &length));

	*value = reinterpret_cast<BYTE*>(CoTaskMemAlloc(length));

	CopyMemory(*value, buffer, length);
	return S_OK;
}

HRESULT TLBinaryReader::ReadDouble(double* value)
{
	return ReadInt64(reinterpret_cast<INT64*>(value));
}

HRESULT TLBinaryReader::ReadFloat(float* value)
{
	return ReadInt32(reinterpret_cast<INT32*>(value));
}

HRESULT TLBinaryReader::ReadBigEndianInt32(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_position + sizeof(value) > m_length)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*value = ((m_buffer[m_position++] & 0xff) << 24) | ((m_buffer[m_position++] & 0xff) << 16) |
		((m_buffer[m_position++] & 0xff) << 8) | ((m_buffer[m_position++] & 0xff));
	return S_OK;
}

HRESULT TLBinaryReader::ReadString(std::wstring& string)
{
	HRESULT result;
	UINT32 mbLength;
	LPCCH mbString;
	ReturnIfFailed(result, ReadBuffer(reinterpret_cast<BYTE const**>(&mbString), &mbLength));

	auto length = MultiByteToWideChar(CP_UTF8, 0, mbString, mbLength, nullptr, 0);
	string.resize(length);

	MultiByteToWideChar(CP_UTF8, 0, mbString, mbLength, &string[0], length);
	return S_OK;
}

HRESULT TLBinaryReader::ReadBuffer(BYTE* buffer, UINT32 length)
{
	HRESULT result;
	UINT32 sourceLength;
	BYTE const* sourceBuffer;
	ReturnIfFailed(result, ReadBuffer(&sourceBuffer, &sourceLength));

	CopyMemory(buffer, sourceBuffer, min(length, sourceLength));
	return S_OK;
}

HRESULT TLBinaryReader::ReadBuffer(BYTE const** buffer, UINT32* length)
{
	if (m_position + 1 > m_length)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	UINT32 sl = 1;
	UINT32 l = m_buffer[m_position++];

	if (l >= 254)
	{
		if (m_position + 3 > m_length)
		{
			return E_NOT_SUFFICIENT_BUFFER;
		}

		l = m_buffer[m_position++] | (m_buffer[m_position++] << 8) | (m_buffer[m_position++] << 16);
		sl = 4;
	}

	UINT32 addition = (l + sl) % 4;
	if (addition != 0)
	{
		addition = 4 - addition;
	}

	if (m_position + l + addition > m_length)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	*length = l;
	*buffer = &m_buffer[m_position];

	m_position += l + addition;
	return S_OK;
}

void TLBinaryReader::Reset()
{
	m_position = 0;
}

void TLBinaryReader::Skip(UINT32 length)
{
	m_position += length;
}