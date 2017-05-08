#include "pch.h"
#include "TLBinaryReader.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


TLBinaryReader::TLBinaryReader()
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

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}


HRESULT TLBinaryReader::ReadInt16(INT16* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT TLBinaryReader::ReadUInt16(UINT16* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT TLBinaryReader::ReadInt32(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT TLBinaryReader::ReadUInt32(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT TLBinaryReader::ReadInt64(INT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT TLBinaryReader::ReadUInt64(UINT64* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT TLBinaryReader::ReadBool(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT TLBinaryReader::ReadString(HSTRING* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT TLBinaryReader::ReadByteArray(UINT32* __valueSize, BYTE** value)
{
	if (__valueSize == nullptr || value == nullptr)
	{
		return E_POINTER;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT TLBinaryReader::ReadDouble(double* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}