#include "pch.h"
#include <openssl/rand.h>
#include "ConnectionCryptography.h"

using namespace Telegram::Api::Native;


void ConnectionCryptography::EncryptBuffer(BYTE const* inputBuffer, BYTE* outputBuffer, UINT32 length)
{
	AES_ctr128_encrypt(inputBuffer, outputBuffer, length, &m_encryptKey, m_encryptIv, m_encryptCount, &m_encryptNum);
}

void ConnectionCryptography::DecryptBuffer(BYTE const* inputBuffer, BYTE* outputBuffer, UINT32 length)
{
	AES_ctr128_encrypt(inputBuffer, outputBuffer, length, &m_decryptKey, m_decryptIv, m_decryptCount, &m_decryptNum);
}

HRESULT ConnectionCryptography::Initialize(BYTE* buffer)
{
	while (true)
	{
		RAND_bytes(buffer, 64);

		UINT32 lowPart = (buffer[3] << 24) | (buffer[2] << 16) | (buffer[1] << 8) | (buffer[0]);
		UINT32 hiPart = (buffer[7] << 24) | (buffer[6] << 16) | (buffer[5] << 8) | (buffer[4]);
		if (buffer[0] != 0xef && lowPart != 0x44414548 && lowPart != 0x54534f50 && lowPart != 0x20544547 && lowPart != 0x4954504f && lowPart != 0xeeeeeeee && hiPart != 0x00000000)
		{
			buffer[56] = buffer[57] = buffer[58] = buffer[59] = 0xef;
			break;
		}
	}

	BYTE temporaryBuffer[64];
	for (size_t i = 0; i < 48; i++)
	{
		temporaryBuffer[i] = buffer[55 - i];
	}

	m_encryptNum = 0;
	m_decryptNum = 0;

	ZeroMemory(m_encryptCount, 16);
	ZeroMemory(m_decryptCount, 16);

	if (AES_set_encrypt_key(buffer + 8, 256, &m_encryptKey) < 0)
	{
		return E_FAIL;
	}

	CopyMemory(m_encryptIv, buffer + 40, 16);

	if (AES_set_encrypt_key(temporaryBuffer, 256, &m_decryptKey) < 0)
	{
		return E_FAIL;
	}

	CopyMemory(m_decryptIv, temporaryBuffer + 32, 16);

	AES_ctr128_encrypt(buffer, temporaryBuffer, 64, &m_encryptKey, m_encryptIv, m_encryptCount, &m_encryptNum);

	CopyMemory(buffer + 56, temporaryBuffer + 56, 8);
	return S_OK;
}