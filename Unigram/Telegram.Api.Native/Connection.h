#pragma once
#include <memory>
#include <openssl/aes.h>
#include "Timer.h"

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			public enum class ConnectionType
			{
				Generic = 1,
				Download = 2,
				Upload = 4,
				Push = 8
			};

			public ref class Connection sealed
			{
				friend ref class ConnectionManager;

			public:
				property Telegram::Api::Native::ConnectionType ConnectionType
				{
					Telegram::Api::Native::ConnectionType get();
				}

				property uint32 ConnectionToken
				{
					uint32 get();
				}

				void Connect();

			internal:
				Connection(Telegram::Api::Native::ConnectionType connectionType);

			private:
				Telegram::Api::Native::ConnectionType m_connectionType;
				uint32 m_connectionToken;
				Timer^ m_reconnectTimer;

				AES_KEY m_encryptKey;
				uint8_t m_encryptIv[16];
				uint32_t m_encryptNum;
				uint8_t m_encryptCount[16];

				AES_KEY m_decryptKey;
				uint8_t m_decryptIv[16];
				uint32_t m_decryptNum;
				uint8_t m_decryptCount[16];
			};

		}
	}
}