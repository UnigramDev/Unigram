#pragma once
#include <vector>
#include <memory>
#include <wrl.h>
#include "Telegram.Api.Native.h"
#include "Datacenter.h"

#define REQUEST_TIMEOUT 30
#define REQUEST_FLAG_NO_LAYER static_cast<RequestFlag>(0x4000)
#define REQUEST_FLAG_INIT_CONNECTION static_cast<RequestFlag>(0x8000)
#define REQUEST_FLAG_CONNECTION_INDEX static_cast<RequestFlag>(0xFFFF0000)

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

			class MessageRequest;

			enum class DatacenterRequestContextFlag
			{
				None = 0x0,
				RequiresHandshake = 0x1,
				RequiresAuthorization = 0x2
			};

			struct DatacenterRequestContext
			{
				DatacenterRequestContext(Datacenter* datacenter) :
					Datacenter(datacenter),
					Flags(DatacenterRequestContextFlag::None)
				{
				}

				const ComPtr<Datacenter> Datacenter;
				DatacenterRequestContextFlag Flags;
				std::vector<ComPtr<MessageRequest>> GenericRequests;
			};

			struct MessageContext
			{
				INT64 Id;
				UINT32 SequenceNumber;
			};

		}
	}
}

DEFINE_ENUM_FLAG_OPERATORS(Telegram::Api::Native::DatacenterRequestContextFlag);


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
				struct IMessageResponse;
				struct IMessageError;

				MIDL_INTERFACE("AF4AE7B6-02DD-4242-B0EE-92A1F2A9E7D0") IMessageRequest : public IUnknown
				{
				public:
					virtual HRESULT STDMETHODCALLTYPE get_Object(_Out_ ITLObject** value) = 0;
					virtual HRESULT STDMETHODCALLTYPE get_MessageContext(_Out_ MessageContext const** value) = 0;
					virtual HRESULT STDMETHODCALLTYPE get_Token(_Out_ INT32* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE get_ConnectionType(_Out_ ConnectionType* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE get_DatacenterId(_Out_ INT32* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE get_Flags(_Out_ RequestFlag* value) = 0;
				};

			}
		}
	}
}


using ABI::Telegram::Api::Native::RequestFlag;
using ABI::Telegram::Api::Native::IMessageRequest;
using ABI::Telegram::Api::Native::IMessageResponse;
using ABI::Telegram::Api::Native::IMessageError;

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


			class MessageRequest WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IMessageRequest>
			{
				friend class ConnectionManager;
				friend class Connection;

			public:
				//COM exported methods
				IFACEMETHODIMP get_MessageContext(_Out_ MessageContext const** value);
				IFACEMETHODIMP get_Object(_Out_ ITLObject** value);
				IFACEMETHODIMP get_Token(_Out_ INT32* value);
				IFACEMETHODIMP get_ConnectionType(_Out_ ConnectionType* value);
				IFACEMETHODIMP get_DatacenterId(_Out_ INT32* value);
				IFACEMETHODIMP get_Flags(_Out_ RequestFlag* value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(_In_ ITLObject* object, INT32 token, ConnectionType connectionType, INT32 datacenterId, _In_ ISendRequestCompletedCallback* sendCompletedCallback,
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

				inline INT32 GetDatacenterId() const
				{
					return m_datacenterId;
				}

				inline INT32 GetStartTime() const
				{
					return m_startTime;
				}

				inline UINT32 GetAttemptCount() const
				{
					return m_attemptCount;
				}

				inline bool MatchesMessage(INT64 messageId)
				{
					return m_messageContext->Id == messageId;
				}

				inline bool MatchesConnection(ConnectionType connectionType)
				{
					return (m_connectionType & connectionType) == m_connectionType;
				}

				inline bool EnableUnauthorized() const
				{
					return (m_flags & RequestFlag::EnableUnauthorized) == RequestFlag::EnableUnauthorized;
				}

				inline bool CanCompress() const
				{
					return (m_flags & RequestFlag::CanCompress) == RequestFlag::CanCompress;
				}

				inline bool InvokeAfter() const
				{
					return (m_flags & RequestFlag::InvokeAfter) == RequestFlag::InvokeAfter;
				}

				inline bool TryDifferentDc() const
				{
					return (m_flags & RequestFlag::TryDifferentDc) == RequestFlag::TryDifferentDc;
				}

				inline bool FailOnServerError() const
				{
					return (m_flags & RequestFlag::FailOnServerError) == RequestFlag::FailOnServerError;
				}

				inline bool RequiresQuickAck() const
				{
					return (m_flags & RequestFlag::RequiresQuickAck) == RequestFlag::RequiresQuickAck;
				}

				inline bool IsInitConnection() const
				{
					return (m_flags & REQUEST_FLAG_INIT_CONNECTION) == REQUEST_FLAG_INIT_CONNECTION;
				}

				inline bool IsLayerRequired() const
				{
					return (m_flags & REQUEST_FLAG_NO_LAYER) == RequestFlag::None;
				}

				inline bool IsTimedOut(INT32 currentTime)
				{
					return m_startTime > 0 && (currentTime - m_startTime) >= REQUEST_TIMEOUT;
				}

				inline ComPtr<IRequestQuickAckReceivedCallback> const& GetQuickAckReceivedCallback() const
				{
					return m_quickAckReceivedCallback;
				}

				inline ComPtr<ISendRequestCompletedCallback> const& GetSendCompletedCallback() const
				{
					return m_sendCompletedCallback;
				}

				inline UINT16 GetConnectionIndex() const
				{
					return static_cast<UINT32>(m_flags & REQUEST_FLAG_CONNECTION_INDEX) >> 16;
				}

			private:
				void Reset(bool resetStartTime);

				inline void SetMessageContext(MessageContext const& mesageContext)
				{
					m_messageContext = std::make_unique<MessageContext>(mesageContext);
				}

				inline void IncrementAttemptCount()
				{
					m_attemptCount++;
				}

				inline void SetStartTime(INT32 startTime)
				{
					m_startTime = startTime;
				}

				inline void SetInitConnection()
				{
					m_flags = m_flags | REQUEST_FLAG_INIT_CONNECTION;
				}

				inline void SetConnectionIndex(UINT16 connectionIndex)
				{
					if (m_connectionType == ConnectionType::Download || m_connectionType == ConnectionType::Upload)
					{
						m_flags = m_flags | static_cast<RequestFlag>(static_cast<UINT32>(connectionIndex) << 16);
					}
				}

				ComPtr<ITLObject> m_object;
				INT32 m_token;
				ConnectionType m_connectionType;
				INT32 m_datacenterId;
				INT32 m_startTime;
				UINT32 m_attemptCount;
				std::unique_ptr<MessageContext> m_messageContext;
				ComPtr<ISendRequestCompletedCallback> m_sendCompletedCallback;
				ComPtr<IRequestQuickAckReceivedCallback> m_quickAckReceivedCallback;
				RequestFlag m_flags;
			};

		}
	}
}