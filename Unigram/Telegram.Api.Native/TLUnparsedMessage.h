#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using ABI::Telegram::Api::Native::ConnectionType;
using ABI::Telegram::Api::Native::ITLUnparsedMessage;
using ABI::Telegram::Api::Native::TL::ITLBinaryReader;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class TLUnparsedMessage WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLUnparsedMessage>
			{
				InspectableClass(RuntimeClass_Telegram_Api_Native_TLUnparsedMessage, BaseTrust);

			public:
				TLUnparsedMessage(INT64 messageId, ConnectionType connectionType, _In_ ITLBinaryReader* reader);
				~TLUnparsedMessage();

				//COM exported methods
				IFACEMETHODIMP get_MessageId(_Out_ INT64* value);
				IFACEMETHODIMP get_ConnectionType(_Out_ ConnectionType* value);
				IFACEMETHODIMP get_Reader(_Out_ ITLBinaryReader** value);

			private:
				UINT64 m_messageId;
				ConnectionType m_connectionType;
				ComPtr<ITLBinaryReader> m_reader;
			};

		}
	}
}