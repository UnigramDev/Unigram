#pragma once
#include <vector>
#include <memory>
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::ConnectionType;
using ABI::Telegram::Api::Native::ISendRequestCompletedCallback;
using ABI::Telegram::Api::Native::IRequestQuickAckReceivedCallback;
using ABI::Telegram::Api::Native::TL::ITLObject;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			struct MessageContext
			{
				INT64 Id;
				UINT32 SequenceNumber;
			};

		}
	}
}


using Telegram::Api::Native::MessageContext;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{

				struct ITLBinaryWriterEx;

				MIDL_INTERFACE("AF4AE7B6-02DD-4242-B0EE-92A1F2A9E7D0") IMessageRequest : public IUnknown
				{
				public:
					virtual HRESULT STDMETHODCALLTYPE get_Object(_Out_ ITLObject** value) = 0;
					virtual HRESULT STDMETHODCALLTYPE get_MessageContext(_Out_ MessageContext const** value) = 0;
					virtual HRESULT STDMETHODCALLTYPE get_Token(_Out_ INT32* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE get_ConnectionType(_Out_ ConnectionType* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE get_DatacenterId(_Out_ UINT32* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE get_Flags(_Out_ RequestFlag* value) = 0;
				};

			}
		}
	}
}


using ABI::Telegram::Api::Native::RequestFlag;
using ABI::Telegram::Api::Native::IMessageRequest;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLMessage;

			}


			class Datacenter;

			class MessageRequest WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IMessageRequest>
			{
				friend class ConnectionManager;

			public:
				//COM exported methods
				IFACEMETHODIMP get_MessageContext(_Out_ MessageContext const** value);
				IFACEMETHODIMP get_Object(_Out_ ITLObject** value);
				IFACEMETHODIMP get_Token(_Out_ INT32* value);
				IFACEMETHODIMP get_ConnectionType(_Out_ ConnectionType* value);
				IFACEMETHODIMP get_DatacenterId(_Out_ UINT32* value);
				IFACEMETHODIMP get_Flags(_Out_ RequestFlag* value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(_In_ ITLObject* object, INT32 token, ConnectionType connectionType, UINT32 datacenterId, _In_ ISendRequestCompletedCallback* sendCompletedCallback,
					_In_ IRequestQuickAckReceivedCallback* quickAckReceivedCallback, RequestFlag flags);

				inline ComPtr<ITLObject> const& GetObject() const
				{
					return m_object;
				}

				inline MessageContext const* GetMessageContext() const
				{
					return m_messageContext.get();
				}

				inline INT32 GetToken() const
				{
					return m_token;
				}

				inline ConnectionType GetConnectionType() const
				{
					return m_connectionType;
				}

				inline UINT32 GetDatacenterId() const
				{
					return m_datacenterId;
				}

				inline INT64 GetStartTime() const
				{
					return m_startTime;
				}

				inline void AddMessageId(INT64 messageId)
				{
					m_messagesIds.push_back(messageId);
				}

				inline boolean HasMessageId(INT64 messageId)
				{
					return  (m_messageContext != nullptr && m_messageContext->Id == messageId) || std::find(m_messagesIds.begin(), m_messagesIds.end(), messageId) != m_messagesIds.end();
				}

				inline boolean EnableUnauthorized() const
				{
					return (m_flags & RequestFlag::EnableUnauthorized) == RequestFlag::EnableUnauthorized;
				}

				inline boolean TryDifferentDc() const
				{
					return (m_flags & RequestFlag::TryDifferentDc) == RequestFlag::TryDifferentDc;
				}

				inline boolean RequiresQuickAck() const
				{
					return (m_flags & RequestFlag::RequiresQuickAck) == RequestFlag::RequiresQuickAck;
				}

			private:
				HRESULT CreateTransportMessage(_Out_ TL::TLMessage** message);
				HRESULT OnQuickAckReceived();
				HRESULT OnSendCompleted(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody);
				void Reset();

				inline void SetMessageContext(MessageContext const& mesageContext)
				{
					m_messageContext = std::make_unique<MessageContext>(mesageContext);
				}

				inline void SetStartTime(INT64 startTime)
				{
					m_startTime = startTime;
				}

				ComPtr<ITLObject> m_object;
				INT32 m_token;
				ConnectionType m_connectionType;
				UINT32 m_datacenterId;
				INT64 m_startTime;
				std::unique_ptr<MessageContext> m_messageContext;
				ComPtr<ISendRequestCompletedCallback> m_sendCompletedCallback;
				ComPtr<IRequestQuickAckReceivedCallback> m_quickAckReceivedCallback;
				RequestFlag m_flags;

				std::vector<INT64> m_messagesIds;
			};

		}
	}
}