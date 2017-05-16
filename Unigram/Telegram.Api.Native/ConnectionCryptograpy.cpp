#include "pch.h"
#include "ConnectionCryptograpy.h"

#define CONNECTION_MAX_ATTEMPTS 5 

using namespace Telegram::Api::Native;


ConnectionCryptograpy::ConnectionCryptograpy() :
	m_encryptNum(0),
	m_decryptNum(0)
{
	ZeroMemory(&m_encryptKey, sizeof(m_encryptKey));
	ZeroMemory(&m_encryptIv, sizeof(m_encryptIv));
	ZeroMemory(&m_encryptCount, sizeof(m_encryptCount));
	ZeroMemory(&m_decryptKey, sizeof(m_decryptKey));
	ZeroMemory(&m_decryptIv, sizeof(m_decryptIv));
	ZeroMemory(&m_decryptCount, sizeof(m_decryptCount));
}

ConnectionCryptograpy::~ConnectionCryptograpy()
{
}

HRESULT ConnectionCryptograpy::SetEncryptKey(BYTE const* key, BYTE const* iv)
{
	/*if (key == nullptr || iv == nullptr)
	{
		return E_INVALIDARG;
	}*/

	if (AES_set_encrypt_key(key, 256, &m_encryptKey) < 0)
	{
		return E_FAIL;
	}

	CopyMemory(&m_encryptIv, iv, 16);
	ZeroMemory(&m_encryptCount, sizeof(m_encryptCount));

	m_encryptNum = 0;
	return S_OK;
}

HRESULT ConnectionCryptograpy::SetDecryptKey(BYTE const* key, BYTE const* iv)
{
	/*if (key == nullptr || iv == nullptr)
	{
		return E_INVALIDARG;
	}*/

	if (AES_set_encrypt_key(key, 256, &m_decryptKey) < 0)
	{
		return E_FAIL;
	}

	CopyMemory(&m_decryptIv, iv, 16);
	ZeroMemory(&m_decryptCount, sizeof(m_decryptCount));

	m_decryptNum = 0;
	return S_OK;
}

HRESULT ConnectionCryptograpy::EncryptBuffer(BYTE const* inputBuffer, BYTE* outputBuffer, UINT32 length)
{
	/*if (inputBuffer == nullptr || outputBuffer == nullptr)
	{
		return E_POINTER;
	}*/

	AES_ctr128_encrypt(inputBuffer, outputBuffer, length, &m_encryptKey, m_encryptIv, m_encryptCount, &m_encryptNum);

	return S_OK;
}

HRESULT ConnectionCryptograpy::DecryptBuffer(BYTE const* inputBuffer, BYTE* outputBuffer, UINT32 length)
{
	/*if (inputBuffer == nullptr || outputBuffer == nullptr)
	{
		return E_POINTER;
	}*/

	AES_ctr128_encrypt(inputBuffer, outputBuffer, length, &m_decryptKey, m_decryptIv, m_decryptCount, &m_decryptNum);

	return S_OK;
}