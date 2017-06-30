#pragma once
#include <type_traits>
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using ABI::Telegram::Api::Native::ConnectionType;
using ABI::Telegram::Api::Native::IMessageResponse;
using ABI::Telegram::Api::Native::TL::ITLObject;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class MessageResponse WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IMessageResponse>
			{
				InspectableClass(RuntimeClass_Telegram_Api_Native_MessageResponse, BaseTrust);

			public:
				MessageResponse(INT64 messageId, ConnectionType connectionType, _In_ ITLObject* object);
				~MessageResponse();

				//COM exported methods
				IFACEMETHODIMP get_MessageId(_Out_ INT64* value);
				IFACEMETHODIMP get_ConnectionType(_Out_ ConnectionType* value);
				IFACEMETHODIMP get_Object(_Out_ ITLObject** value);

				//Internal methods
				inline ComPtr<ITLObject> const& GetObject() const
				{
					return m_object;
				}

				inline INT64 GetMessageId() const
				{
					return m_messageId;
				}

				inline ConnectionType GetConnectionType() const
				{
					return m_connectionType;
				}

			private:
				INT64 m_messageId;
				ConnectionType m_connectionType;
				ComPtr<ITLObject> m_object;
			};

			template<typename TLObjectType>
			inline typename std::enable_if<std::is_base_of<ITLObject, TLObjectType>::value, TLObjectType>::type* GetMessageResponseObject(_In_ IMessageResponse* response)
			{
				return static_cast<typename TLObjectType*>(static_cast<MessageResponse*>(response)->GetObject().Get());
			}

		}
	}
}