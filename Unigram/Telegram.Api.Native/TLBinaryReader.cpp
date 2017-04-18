#include "pch.h"
#include "TLBinaryReader.h"

using namespace Telegram::Api::Native;

TLBinaryReader::TLBinaryReader()
{
}

int32 TLBinaryReader::ReadInt32()
{
	return 0;
}

int64 TLBinaryReader::ReadInt64()
{
	return 0;
}

bool TLBinaryReader::ReadBool()
{
	return false;
}

uint8 TLBinaryReader::ReadByte()
{
	return 0;
}

String^ TLBinaryReader::ReadString()
{
	return nullptr;
}

Array<uint8>^ TLBinaryReader::ReadByteArray()
{
	return nullptr;
}

double TLBinaryReader::ReadDouble()
{
	return 0.0;
}
