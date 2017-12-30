#pragma once
#include <string>
#include <Windows.h>
#include "Wrappers\OpenSSL.h"

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			struct ServerSalt
			{
				INT32 ValidSince;
				INT32 ValidUntil;
				INT64 Salt;
			};

			struct ServerEndpoint
			{
				std::wstring Address;
				UINT32 Port;
			};

			struct ServerPublicKey
			{
				//std::string Key;
				Wrappers::RSA Key;
				INT64 Fingerprint;
			};

		}
	}
}