#include "pch.h"
#include <memory>
#include "TLBinaryWriter.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;

ActivatableStaticOnlyFactory(TLBinarySizeCalculatorStatics);


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
	if (m_position + sizeof(BYTE) > m_length)
	{
		return E_NOT_SUFFICIENT_BUFFER;
	}

	m_buffer[m_position++] = value;
	return S_OK;
}

HRESULT TLBinaryWriter::WriteInt16(INT16 value)
{
	if (m_position + sizeof(INT16) > m_length)
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
	if (m_position + sizeof(INT32) > m_length)
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
	if (m_position + sizeof(INT64) > m_length)
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


TLBinarySizeCalculator::TLBinarySizeCalculator() :
	m_length(0)
{
}

TLBinarySizeCalculator::~TLBinarySizeCalculator()
{
}

HRESULT TLBinarySizeCalculator::get_TotalLength(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_length;
	return S_OK;
}

HRESULT TLBinarySizeCalculator::get_UnstoredBufferLength(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = UINT32_MAX - m_length;
	return S_OK;
}

HRESULT TLBinarySizeCalculator::WriteByte(BYTE value)
{
	m_length += sizeof(BYTE);
	return S_OK;
}

HRESULT TLBinarySizeCalculator::WriteInt16(INT16 value)
{
	m_length += sizeof(INT16);
	return S_OK;
}

HRESULT TLBinarySizeCalculator::WriteUInt16(UINT16 value)
{
	m_length += sizeof(UINT16);
	return S_OK;
}

HRESULT TLBinarySizeCalculator::WriteInt32(INT32 value)
{
	m_length += sizeof(INT32);
	return S_OK;
}

HRESULT TLBinarySizeCalculator::WriteUInt32(UINT32 value)
{
	m_length += sizeof(UINT32);
	return S_OK;
}

HRESULT TLBinarySizeCalculator::WriteInt64(INT64 value)
{
	m_length += sizeof(INT64);
	return S_OK;
}

HRESULT TLBinarySizeCalculator::WriteUInt64(UINT64 value)
{
	m_length += sizeof(UINT64);
	return S_OK;
}

HRESULT TLBinarySizeCalculator::WriteBool(boolean value)
{
	m_length += sizeof(UINT32);
	return S_OK;
}

HRESULT TLBinarySizeCalculator::WriteString(HSTRING value)
{
	UINT32 length;
	auto buffer = WindowsGetStringRawBuffer(value, &length);
	auto mbLength = WideCharToMultiByte(CP_UTF8, 0, buffer, length, nullptr, 0, nullptr, nullptr);
	return WriteBuffer(nullptr, mbLength);
}

HRESULT TLBinarySizeCalculator::WriteByteArray(UINT32 __valueSize, BYTE* value)
{
	return WriteBuffer(value, __valueSize);
}

HRESULT TLBinarySizeCalculator::WriteDouble(double value)
{
	m_length += sizeof(double);
	return S_OK;
}

HRESULT TLBinarySizeCalculator::WriteFloat(float value)
{
	m_length += sizeof(float);
	return S_OK;
}

HRESULT TLBinarySizeCalculator::WriteString(std::wstring string)
{
	auto mbLength = WideCharToMultiByte(CP_UTF8, 0, string.data(), static_cast<UINT32>(string.size()), nullptr, 0, nullptr, nullptr);
	return WriteBuffer(nullptr, mbLength);
}

HRESULT TLBinarySizeCalculator::WriteBuffer(BYTE const* buffer, UINT32 length)
{
	if (length < 254)
	{
		UINT32 padding = (length + 1) % 4;
		if (padding != 0)
		{
			padding = 4 - padding;
		}

		m_length += 1 + length + padding;
	}
	else
	{
		UINT32 padding = (length + 3) % 4;
		if (padding != 0)
		{
			padding = 4 - padding;
		}

		m_length += 4 + length + padding;
	}

	return S_OK;
}

void TLBinarySizeCalculator::Reset()
{
	m_length = 0;
}

void TLBinarySizeCalculator::Skip(UINT32 length)
{
	m_length += length;
}

HRESULT TLBinarySizeCalculator::GetInstance(ComPtr<TLBinarySizeCalculator>& value)
{
	if (TLBinarySizeCalculatorStatics::s_instance == nullptr)
	{
		TLBinarySizeCalculatorStatics::s_instance = Make<TLBinarySizeCalculator>();
	}
	else
	{
		TLBinarySizeCalculatorStatics::s_instance->Reset();
	}

	value = TLBinarySizeCalculatorStatics::s_instance;
	return S_OK;
}


thread_local ComPtr<TLBinarySizeCalculator> TLBinarySizeCalculatorStatics::s_instance = nullptr;

TLBinarySizeCalculatorStatics::TLBinarySizeCalculatorStatics()
{
}

TLBinarySizeCalculatorStatics::~TLBinarySizeCalculatorStatics()
{
}

HRESULT TLBinarySizeCalculatorStatics::get_Instance(ITLBinarySizeCalculator** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ComPtr<TLBinarySizeCalculator> binarySizeCalculator;
	ReturnIfFailed(result, TLBinarySizeCalculator::GetInstance(binarySizeCalculator));

	*value = binarySizeCalculator.Detach();
	return S_OK;
}