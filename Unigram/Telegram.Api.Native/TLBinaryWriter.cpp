#include "pch.h"
#include "TLBinaryWriter.h"

using namespace Telegram::Api::Native;

TLBinaryWriter::TLBinaryWriter()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

void TLBinaryWriter::WriteInt32(int32 value)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

void TLBinaryWriter::WriteInt64(int64 value)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

void TLBinaryWriter::WriteBool(bool value)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

void TLBinaryWriter::WriteByte(uint8 value)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

void TLBinaryWriter::WriteString(String^ value)
{
	if (value == nullptr)
		throw ref new InvalidArgumentException(L"The 'value' argument cannot be null");

	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

void TLBinaryWriter::WriteByteArray(const Array<uint8>^ value)
{
	if (value == nullptr)
		throw ref new InvalidArgumentException(L"The 'value' argument cannot be null");

	I_WANT_TO_DIE_IS_THE_NEW_TODO
}

void TLBinaryWriter::WriteDouble(double value)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO
}
