#pragma once
#include <openssl\aes.h>
#include <wrl.h>

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class ConnectionCryptography abstract
			{
			public:
				ConnectionCryptography();
				~ConnectionCryptography();

			protected:
				HRESULT Initialize(_In_ BYTE* buffer);
				//HRESULT EncryptBuffer(_In_reads_(length) BYTE const* inputBuffer, _Out_writes_(length) BYTE* outputBuffer, _In_ UINT32 length);
				//HRESULT DecryptBuffer(_In_reads_(length) BYTE const* inputBuffer, _Out_writes_(length) BYTE* outputBuffer, _In_ UINT32 length);
				void EncryptBuffer(_In_reads_(length) BYTE const* inputBuffer, _Out_writes_(length) BYTE* outputBuffer, _In_ UINT32 length);
				void DecryptBuffer(_In_reads_(length) BYTE const* inputBuffer, _Out_writes_(length) BYTE* outputBuffer, _In_ UINT32 length);
				void Reset();

				inline boolean IsInitialized() const
				{
					return m_initialized;
				}

			private:
				boolean m_initialized;
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