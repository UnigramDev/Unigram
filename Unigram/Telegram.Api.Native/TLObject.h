#pragma once
#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using ABI::Telegram::Api::Native::ITLObject;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class TLObject abstract : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLObject>
			{
			public:
				TLObject();
				~TLObject();
			};

		}
	}
}