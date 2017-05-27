#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using ABI::Telegram::Api::Native::ConnectionType;
using ABI::Telegram::Api::Native::ITLUnprocessedMessage;
using ABI::Telegram::Api::Native::TL::ITLObject;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class TLUnprocessedMessage WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLUnprocessedMessage>
			{
				InspectableClass(RuntimeClass_Telegram_Api_Native_TLUnprocessedMessage, BaseTrust);

			public:
				TLUnprocessedMessage(INT64 messageId, ConnectionType connectionType, _In_ ITLObject* object);
				~TLUnprocessedMessage();

				//COM exported methods
				IFACEMETHODIMP get_MessageId(_Out_ INT64* value);
				IFACEMETHODIMP get_ConnectionType(_Out_ ConnectionType* value);
				IFACEMETHODIMP get_Object(_Out_ ITLObject** value);

			private:
				UINT64 m_messageId;
				ConnectionType m_connectionType;
				ComPtr<ITLObject> m_object;
			};

		}
	}
}