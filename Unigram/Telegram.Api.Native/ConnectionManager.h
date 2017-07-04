#pragma once
#include <vector>
#include <map>
#include <queue>
#include <functional>
#include <list>
#include <wrl.h>
#include <Windows.Networking.Connectivity.h>
#include "EventObject.h"
#include "DatacenterServer.h"
#include "Telegram.Api.Native.h"

#define MIN_THREAD_COUNT UINT32_MAX
#define MAX_THREAD_COUNT UINT32_MAX
#define TELEGRAM_API_NATIVE_PROTOVERSION 2
#define TELEGRAM_API_NATIVE_VERSION 1
#define TELEGRAM_API_NATIVE_LAYER 68
#define TELEGRAM_API_NATIVE_APIID 6

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Windows::Networking::Connectivity::INetworkInformationStatics;
using ABI::Telegram::Api::Native::IConnectionManager;
using ABI::Telegram::Api::Native::IConnectionManagerStatics;
using ABI::Telegram::Api::Native::IUserConfiguration;
using ABI::Telegram::Api::Native::IProxySettings;
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

			enum class ConnectionManagerFlag
			{
				None = 0,
				ConnectionState = 0x3,
				NetworkType = 0xC,
				UseIPv6 = 0x10,
				UpdatingDatacenters = 0x20,
				UpdatingCDNPublicKeys = 0x40
			};

		}
	}
}

DEFINE_ENUM_FLAG_OPERATORS(Telegram::Api::Native::ConnectionManagerFlag);


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
				class TLMessage;
				class TLUnparsedObject;
				class TLConfig;

			}


			struct DatacenterRequestContext;
			struct MessageContext;
			class MessageRequest;

			class ConnectionManager WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IConnectionManager>, public virtual ThreadpoolManager, public virtual EventObjectT<EventTraits::TimerTraits>
			{
				friend class Datacenter;
				friend class Connection;
				friend class TL::TLObject;
				friend class TL::TLUnparsedObject;

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
				IFACEMETHODIMP add_UnprocessedMessageReceived(_In_ __FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CMessageResponse* handler, _Out_ EventRegistrationToken* token);
				IFACEMETHODIMP remove_UnprocessedMessageReceived(EventRegistrationToken token);
				IFACEMETHODIMP get_CurrentDatacenter(_Out_ IDatacenter** value);
				IFACEMETHODIMP get_ConnectionState(_Out_ ConnectionState* value);
				IFACEMETHODIMP get_CurrentNetworkType(_Out_ ConnectionNeworkType* value);
				IFACEMETHODIMP get_IsIPv6Enabled(_Out_ boolean* value);
				IFACEMETHODIMP get_IsNetworkAvailable(_Out_ boolean* value);
				IFACEMETHODIMP get_UserConfiguration(_Out_ IUserConfiguration** value);
				IFACEMETHODIMP put_UserConfiguration(_In_ IUserConfiguration* value);
				IFACEMETHODIMP get_UserId(_Out_ INT32* value);
				IFACEMETHODIMP put_UserId(INT32 value);
				IFACEMETHODIMP get_Proxy(_Out_ IProxySettings** value);
				IFACEMETHODIMP put_Proxy(_In_ IProxySettings* value);
				IFACEMETHODIMP get_Datacenters(_Out_ __FIVectorView_1_Telegram__CApi__CNative__CDatacenter** value);
				IFACEMETHODIMP SendRequest(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived, ConnectionType connectionType, _Out_ INT32* value);
				IFACEMETHODIMP SendRequestWithDatacenter(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived,
					INT32 datacenterId, ConnectionType connectionType, _Out_ INT32* value);
				IFACEMETHODIMP SendRequestWithFlags(_In_ ITLObject* object, _In_ ISendRequestCompletedCallback* onCompleted, _In_ IRequestQuickAckReceivedCallback* onQuickAckReceived,
					INT32 datacenterId, ConnectionType connectionType, RequestFlag flags, _Out_ INT32* value);
				IFACEMETHODIMP CancelRequest(INT32 requestToken, boolean notifyServer, _Out_ boolean* value);
				IFACEMETHODIMP GetDatacenterById(INT32 id, _Out_ IDatacenter** value);
				IFACEMETHODIMP UpdateDatacenters();
				IFACEMETHODIMP Reset();

				IFACEMETHODIMP BoomBaby(_In_ IUserConfiguration* userConfiguration, _Out_ ITLObject** object, _Out_ IConnection** value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(UINT32 minimumThreadCount = MIN_THREAD_COUNT, UINT32 maximumThreadCount = MAX_THREAD_COUNT);
				INT32 GetCurrentTime();
				INT64 GenerateMessageId();

				inline const std::wstring& GetSettingsFolderPath() const
				{
					return m_settingsFolderPath;
				}

				inline bool IsNetworkAvailable()
				{
					auto lock = LockCriticalSection();
					return static_cast<ConnectionNeworkType>(m_flags & ConnectionManagerFlag::NetworkType) != ConnectionNeworkType::None;
				}

				static HRESULT GetInstance(_Out_ ComPtr<ConnectionManager>& value);

				inline static UINT64 GetCurrentSystemTime()
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
				HRESULT InitializeSettings();
				HRESULT UpdateNetworkStatus(bool raiseEvent);
				HRESULT MoveToDatacenter(INT32 datacenterId);
				HRESULT UpdateCDNPublicKeys();
				HRESULT CreateTransportMessage(_In_ MessageRequest* request, _Inout_ INT64& lastRpcMessageId, _Inout_ bool& requiresLayer, _Out_ TL::TLMessage** message);
				HRESULT ProcessRequests();
				HRESULT ProcessRequestsForDatacenter(_In_ Datacenter* datacenter, ConnectionType connectionType);
				HRESULT ProcessRequest(_In_ MessageRequest* request, INT32 currentTime, _In_ std::map<UINT32, DatacenterRequestContext>& datacentersContexts);
				HRESULT ProcessRequests(_In_ std::map<UINT32, DatacenterRequestContext>& datacentersContexts);
				HRESULT ProcessDatacenterRequests(_In_ DatacenterRequestContext const& datacenterContext);
				HRESULT ProcessConnectionRequest(_In_ Connection* connection, _In_ MessageRequest* request);
				void ResetRequests(_In_ std::map<UINT32, DatacenterRequestContext> const& datacentersContexts);
				void ResetRequests(std::function<bool(INT32, ComPtr<MessageRequest> const&)> selector, bool resetStartTime);
				HRESULT CompleteMessageRequest(INT64 requestMessageId, _In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _In_ Connection* connection);
				HRESULT HandleRequestError(_In_ Datacenter* datacenter, _In_ MessageRequest* request, INT32 code, _In_ HString const& text);
				HRESULT OnUnprocessedMessageResponse(_In_ MessageContext const* messageContext, _In_ ITLObject* messageBody, _In_ Connection* connection);
				HRESULT OnNetworkStatusChanged(_In_ IInspectable* sender);
				HRESULT OnConnectionOpened(_In_ Connection* connection);
				HRESULT OnConnectionQuickAckReceived(_In_ Connection* connection, INT32 ack);
				HRESULT OnConnectionClosed(_In_ Connection* connection);
				HRESULT OnDatacenterHandshakeComplete(_In_ Datacenter* datacenter, INT32 timeDifference);
				HRESULT OnDatacenterImportAuthorizationComplete(_In_ Datacenter* datacenter);
				HRESULT OnDatacenterBadServerSalt(_In_ Datacenter* datacenter, INT64 requestMessageId, INT64 responseMessageId);
				HRESULT OnDatacenterBadMessage(_In_ Datacenter* datacenter, INT64 requestMessageId, INT64 responseMessageId);
				HRESULT OnConnectionSessionCreated(_In_ Connection* connection, INT64 firstMessageId);
				bool GetDatacenterById(UINT32 id, _Out_ ComPtr<Datacenter>& datacenter);
				bool GetRequestByMessageId(INT64 messageId, _Out_ ComPtr<MessageRequest>& request);
				bool GetCDNPublicKey(INT32 datacenterId, _In_ std::vector<INT64> const& fingerprints, _Out_ ServerPublicKey const** publicKey);
				void AdjustCurrentTime(INT64 messageId);
				virtual HRESULT OnEvent(_In_ PTP_CALLBACK_INSTANCE callbackInstance, _In_ ULONG_PTR param) override;

				inline bool IsCurrentDatacenter(INT32 datacenterId)
				{
					auto lock = LockCriticalSection();
					return datacenterId == m_currentDatacenterId || datacenterId == m_movingToDatacenterId;
				}

				inline bool HasCDNPublicKey(INT32 datacenterId)
				{
					auto lock = LockCriticalSection();
					return m_cdnPublicKeys.find(datacenterId) != m_cdnPublicKeys.end();
				}

				ComPtr<INetworkInformationStatics> m_networkInformation;
				EventRegistrationToken m_networkChangedEventToken;
				ConnectionManagerFlag m_flags;
				INT32 m_currentDatacenterId;
				INT32 m_movingToDatacenterId;
				INT32 m_datacentersExpirationTime;
				std::map<INT32, ComPtr<Datacenter>> m_datacenters;
				std::map<INT32, ServerPublicKey> m_cdnPublicKeys;
				CriticalSection m_requestsCriticalSection;
				std::list<ComPtr<MessageRequest>> m_requestsQueue;
				std::list<std::pair<INT32, ComPtr<MessageRequest>>> m_runningRequests;
				std::map<INT32, std::vector<ComPtr<MessageRequest>>> m_quickAckRequests;
				UINT32 m_runningRequestCount[3];
				INT32 m_lastRequestToken;
				INT64 m_lastOutgoingMessageId;
				INT32 m_timeDifference;
				INT32 m_userId;
				ComPtr<IUserConfiguration> m_userConfiguration;
				ComPtr<IProxySettings> m_proxySettings;
				std::wstring m_settingsFolderPath;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_sessionCreatedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_currentNetworkTypeChangedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable> m_connectionStateChangedEventSource;
				EventSource<__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CMessageResponse> m_unprocessedMessageReceivedEventSource;
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