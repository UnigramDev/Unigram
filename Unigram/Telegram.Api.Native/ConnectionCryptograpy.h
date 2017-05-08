#pragma once
#include <openssl\aes.h>

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class ConnectionCryptograpy abstract
			{
			public:
				ConnectionCryptograpy();
				~ConnectionCryptograpy();

			protected:
				HRESULT SetEncryptKey(_In_ BYTE const* key, _In_ BYTE const* iv);
				HRESULT SetDecryptKey(_In_ BYTE const* key, _In_ BYTE const* iv);
				HRESULT EncryptBuffer(_In_reads_(length) BYTE const* inputBuffer, _Out_writes_(length) BYTE* outputBuffer, _In_ UINT32 length);
				HRESULT DecryptBuffer(_In_reads_(length) BYTE const* inputBuffer, _Out_writes_(length) BYTE* outputBuffer, _In_ UINT32 length);

			private:
				AES_KEY m_encryptKey;
				UINT8 m_encryptIv[AES_BLOCK_SIZE];
				UINT32 m_encryptNum;
				UINT8 m_encryptCount[AES_BLOCK_SIZE];
				AES_KEY m_decryptKey;
				UINT8 m_decryptIv[AES_BLOCK_SIZE];
				UINT32 m_decryptNum;
				UINT8 m_decryptCount[AES_BLOCK_SIZE];
			};

		}
	}
}