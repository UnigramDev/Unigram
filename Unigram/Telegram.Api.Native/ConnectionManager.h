#pragma once
#include <vector>
#include <map>
#include <queue>
#include <functional>
#include <list>
#include <wrl.h>
#include <Windows.Networking.Connectivity.h>
#include "ThreadpoolObject.h"
#include "Telegram.Api.Native.h"

#define THREAD_COUNT 1
#define TELEGRAM_API_NATIVE_PROTOVERSION 2
#define TELEGRAM_API_NATIVE_VERSION 1
#define TELEGRAM_API_NATIVE_LAYER 66
#define TELEGRAM_API_NATIVE_APIID 6

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Windows::Networking::Connectivity::INetworkInformationStatics;
using ABI::Telegram::Api::Native::IConnectionManager;
using ABI::Telegram::Api::Native::IConnectionManagerStatics;
using ABI::Telegram::Api::Native::IUserConfiguration;
using ABI::Telegram::Api::Native::Version;
using ABI::Telegram::Api::Native::ConnectionState;
using ABI::Telegram::Api::Native::ConnectionNeworkType;
using ABI::Telegram::Api::Native::ConnectionType;
using ABI::Telegram::Api::Native::ISendRequestCompletedCallback;
using ABI::Telegram::Api::Native::IRequestQuickAckReceivedCallback;
using ABI::Telegram::Api::Native::IDatacenter;
using ABI::Telegram::Api::Native::IConnection;
using ABI::Telegram::Api::Native::RequestFlag;
using ABI::Telegram::Api::Native::TL::ITLObject;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLBinaryReader;
				class TLObject;
				class TLUnparsedObject;
				class TLMessage;
				class TLConfig;
				class TLAuthExportedAuthorization;

			}


			struct DatacenterRequestContext;
			struct MessageContext;
			class MessageRequest;

			class ConnectionManager WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnectionManager>, public ThreadpoolManager
			{
				friend class Datacenter;
				friend class Connection;
				friend class TL::TLObject;
				friend class TL::TLUnparsedObject;
				friend class TL::TLConfig;
				friend class TL::TLAuthExportedAuthorization;

				InspectableClass(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				ConnectionManager();
				~ConnectionManager();

				//COM exported methods
				IFACEMETHODIMP add_SessionCreated(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_SessionCreated(EventRegistrationToken token);
				IFACEMETHODIMP add_CurrentNetworkTypeChanged(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_CurrentNetworkTypeChanged(EventRegistrationToken token);
				IFACEMETHODIMP add_ConnectionStateChanged(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_ConnectionStateChanged(EventRegistrationToken token);
				IFACEMETHODIMP add_UnprocessedMessageReceived(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnprocessedMessage* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_UnprocessedMessageReceived(EventRegistrationToken token);
				IFACEMETHODIMP get_CurrentDatacenter(_Out_ IDatacenter** value);
				IFACEMETHODIMP get_ConnectionState(_Out_ ConnectionState* value);
				IFACEMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				IFACEMETHODIMP get_IsIpv6Enabled(_Out_ boolean* value);
				IFACEMETHODIMP get_IsNetworkAvailable(_Out_ boolean* value);
				IFACEMETHODIMP get_UserConfiguration(_Out_ IUserConfiguration** value);
				IFACEMETHODIMP put_UserConfiguration(_In_ IUserConfiguration* value);
				IFACEMETHODIMP get_UserId(_Out_ INT32* value);
				IFACEMETHODIMP put_UserId(INT32 value);
				IFACEMETHODIMP get_Datacenters(_Out_ __FIVectorView_1_Telegram__CApi__CNative__CDatacenter** value);
				IFACEMETHODIMP SendRequest(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived, ConnectionType connectionType, _Out_ INT32* value);
				IFACEMETHODIMP SendRequestWithDatacenter(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived,
					INT32 datacenterId, ConnectionType connectionType, _Out_ INT32* value);
				IFACEMETHODIMP SendRequestWithFlags(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived,
					INT32 datacenterId, ConnectionType connectionType, RequestFlag flags, _Out_ INT32* value);
				IFACEMETHODIMP CancelRequest(INT32 requestToken, boolean notifyServer, _Out_ boolean* value);
				IFACEMETHODIMP GetDatacenterById(INT32 id, _Out_ IDatacenter** value);

				IFACEMETHODIMP BoomBaby(_In_ IUserConfiguration* userConfiguration, _Out_ ITLObject** object, _Out_ IConnection** value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(DWORD minimumThreadCount = THREAD_COUNT, DWORD maximumThreadCount = THREAD_COUNT);
				boolean IsNetworkAvailable();
				INT32 GetCurrentTime();
				INT64 GenerateMessageId();

				static HRESULT GetInstance(_Out_ ComPtr<ConnectionManager>& value);

				inline static UINT64 GetCurrentRealTime()
				{
					FILETIME time;
					GetSystemTimePreciseAsFileTime(&time);

					return ((static_cast<UINT64>(time.dwHighDateTime) << 32) | static_cast<UINT64>(time.dwLowDateTime)) / 1000000ULL;
				}

				inline static UINT64 GetCurrentMonotonicTime()
				{
					return GetTickCount64();
				}

			private:
				HRESULT InitializeDatacenters();
				HRESULT UpdateNetworkStatus(boolean raiseEvent);
				HRESULT MoveToDatacenter(INT32 datacenterId);
				HRESULT CreateTransportMessage(_In_ MessageRequest* request, _Inout_ INT64& lastRpcMessageId, _Inout_ boolean& requiresLayer, _Out_ TL::TLMessage** message);
				HRESULT ProcessDatacenterRequests(_In_ Datacenter* datacenter, ConnectionType connectionType);
				HRESULT ProcessRequest(_In_ MessageRequest* request, INT32 currentTime, _In_ std::map<UINT32, DatacenterRequestContext>& datacentersContexts);
				HRESULT ProcessRequests(_In_ std::map<UINT32, DatacenterRequestContext> const& datacentersContexts);
				HRESULT ProcessDatacenterRequests(_In_ DatacenterRequestContext const& datacenterContext);
				void ResetRequests(std::function<boolean(INT32, ComPtr<MessageRequest> const&)> selector);
				HRESULT CompleteMessageRequest(INT64 requestMessageId, _In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _In_ Connection* connection);
				HRESULT OnUnprocessedMessageResponse(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _In_ Connection* connection);
				HRESULT OnConfigResponse(_In_ TL::TLConfig* response);
				HRESULT OnExportedAuthorizationResponse(_In_ TL::TLAuthExportedAuthorization* response);
				HRESULT OnNetworkStatusChanged(_In_ IInspectable* sender);
				HRESULT OnConnectionOpened(_In_ Connection* connection);
				HRESULT OnConnectionQuickAckReceived(_In_ Connection* connection, INT32 ack);
				HRESULT OnConnectionClosed(_In_ Connection* connection);
				HRESULT OnDatacenterHandshakeComplete(_In_ Datacenter* datacenter, INT32 timeDifference);
				HRESULT OnConnectionSessionCreated(_In_ Connection* connection, INT64 firstMessageId);
				HRESULT OnRequestEnqueued(_In_ PTP_CALLBACK_INSTANCE instance);
				boolean GetDatacenterById(UINT32 id, _Out_ ComPtr<Datacenter>& datacenter);
				boolean GetRequestByMessageId(INT64 messageId, _Out_ ComPtr<MessageRequest>& request);

				inline boolean IsCurrentDatacenter(INT32 datacenterId)
				{
					auto lock = LockCriticalSection();
					return datacenterId == m_currentDatacenterId || datacenterId == m_movingToDatacenterId;
				}

				static void NTAPI RequestEnqueuedCallback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ PTP_WAIT wait, _In_ TP_WAIT_RESULT waitResult);

				ComPtr<INetworkInformationStatics> m_networkInformation;
				EventRegistrationToken m_networkChangedEventToken;
				ConnectionState m_connectionState;
				ConnectionNeworkType m_currentNetworkType;
				boolean m_isIpv6Enabled;
				INT32 m_currentDatacenterId;
				INT32 m_movingToDatacenterId;
				std::map<INT32, ComPtr<Datacenter>> m_datacenters;
				Event m_requestEnqueuedEvent;
				CriticalSection m_requestsCriticalSection;
				std::list<ComPtr<MessageRequest>> m_requestsQueue;
				std::list<std::pair<INT32, ComPtr<MessageRequest>>> m_runningRequests;
				std::map<INT32, std::vector<ComPtr<MessageRequest>>> m_quickAckRequests;
				INT32 m_lastRequestToken;
				INT64 m_lastOutgoingMessageId;
				INT32 m_timeDifference;
				INT32 m_userId;
				ComPtr<IUserConfiguration> m_userConfiguration;

				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_sessionCreatedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_currentNetworkTypeChangedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_connectionStateChangedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnprocessedMessage> m_unprocessedMessageReceivedEventSource;
			};


			class ConnectionManagerStatics WrlSealed : public AgileActivationFactory<IConnectionManagerStatics>
			{
				InspectableClassStatic(RuntimeClass_Telegram_Api_Native_ConnectionManager, BaseTrust);

			public:
				IFACEMETHODIMP get_Instance(_Out_ IConnectionManager** value);
				IFACEMETHODIMP get_Version(_Out_ Version* value);
				IFACEMETHODIMP get_DefaultDatacenterId(_Out_ INT32* value);
			};

		}
	}
}