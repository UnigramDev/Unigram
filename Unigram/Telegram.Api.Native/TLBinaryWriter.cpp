#include "pch.h"
#include "TLBinaryWriter.h"

using namespace Telegram::Api::Native;

TLBinaryWriter::TLBinaryWriter()
{
}


void TLBinaryWriter::WriteInt32(int32 value)
{
}

void TLBinaryWriter::WriteInt64(int64 value)
{
}

void TLBinaryWriter::WriteBool(bool value)
{
}

void TLBinaryWriter::WriteByte(uint8 value)
{
}

void TLBinaryWriter::WriteString(String^ value)
{
	if (value == nullptr)
		throw ref new InvalidArgumentException(L"The 'value' argument cannot be null");
}

void TLBinaryWriter::WriteByteArray(const Array<uint8>^ value)
{
	if (value == nullptr)
		throw ref new InvalidArgumentException(L"The 'value' argument cannot be null");
}

void TLBinaryWriter::WriteDouble(double value)
{
}
