#include "pch.h"
#include <iphlpapi.h>
#include "ConnectionManager.h"
#include "EventObject.h"
#include "Datacenter.h"
#include "Connection.h"
#include "TLUnparsedMessage.h"
#include "Request.h"
#include "TLProtocolScheme.h"
#include "DefaultUserConfiguration.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;

ActivatableStaticOnlyFactory(ConnectionManagerStatics);


ComPtr<ConnectionManager> ConnectionManager::s_instance = nullptr;

ConnectionManager::ConnectionManager() :
	m_connectionState(ConnectionState::Connecting),
	m_currentNetworkType(ConnectionNeworkType::None),
	m_threadpool(nullptr),
	m_threadpoolCleanupGroup(nullptr),
	m_isIpv6Enabled(false),
	m_currentDatacenterId(0),
	m_timeDelta(0),
	m_lastOutgoingMessageId(0),
	m_userId(0)
{
}

ConnectionManager::~ConnectionManager()
{
	m_networkInformation->remove_NetworkStatusChanged(m_networkChangedEventToken);

	if (m_threadpoolCleanupGroup != nullptr)
	{
		CloseThreadpoolCleanupGroupMembers(m_threadpoolCleanupGroup, TRUE, nullptr);
		CloseThreadpoolCleanupGroup(m_threadpoolCleanupGroup);
	}

	if (m_threadpool != nullptr)
	{
		CloseThreadpool(m_threadpool);
	}

	DestroyThreadpoolEnvironment(&m_threadpoolEnvironment);

	WSACleanup();
}

HRESULT ConnectionManager::RuntimeClassInitialize(DWORD minimumThreadCount, DWORD maximumThreadCount)
{
	if (minimumThreadCount == 0 || minimumThreadCount > maximumThreadCount)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	ReturnIfFailed(result, MakeAndInitialize<DefaultUserConfiguration>(&m_userConfiguration));

	WSADATA wsaData;
	if (WSAStartup(MAKEWORD(2, 2), &wsaData) != NO_ERROR)
	{
		return WSAGetLastHRESULT();
	}

	InitializeThreadpoolEnvironment(&m_threadpoolEnvironment);

	m_threadpool = CreateThreadpool(nullptr);
	if (m_threadpool == nullptr)
	{
		return GetLastHRESULT();
	}

	SetThreadpoolThreadMaximum(m_threadpool, maximumThreadCount);
	if (!SetThreadpoolThreadMinimum(m_threadpool, minimumThreadCount))
	{
		return GetLastHRESULT();
	}

	m_threadpoolCleanupGroup = CreateThreadpoolCleanupGroup();
	if (m_threadpoolCleanupGroup == nullptr)
	{
		return GetLastHRESULT();
	}

	SetThreadpoolCallbackPool(&m_threadpoolEnvironment, m_threadpool);
	SetThreadpoolCallbackCleanupGroup(&m_threadpoolEnvironment, m_threadpoolCleanupGroup, nullptr);


	ReturnIfFailed(result, Windows::Foundation::GetActivationFactory(HStringReference(RuntimeClass_Windows_Networking_Connectivity_NetworkInformation).Get(), &m_networkInformation));
	ReturnIfFailed(result, m_networkInformation->add_NetworkStatusChanged(Callback<INetworkStatusChangedEventHandler>(this, &ConnectionManager::OnNetworkStatusChanged).Get(), &m_networkChangedEventToken));

	return UpdateNetworkStatus(false);
}

HRESULT ConnectionManager::add_CurrentNetworkTypeChanged(__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, EventRegistrationToken* token)
{
	return m_currentNetworkTypeChangedEventSource.Add(handler, token);
}

HRESULT ConnectionManager::remove_CurrentNetworkTypeChanged(EventRegistrationToken token)
{
	return m_currentNetworkTypeChangedEventSource.Remove(token);
}

HRESULT ConnectionManager::add_ConnectionStateChanged(__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, EventRegistrationToken* token)
{
	return m_connectionStateChangedEventSource.Add(handler, token);
}

HRESULT ConnectionManager::remove_ConnectionStateChanged(EventRegistrationToken token)
{
	return m_connectionStateChangedEventSource.Remove(token);
}

HRESULT ConnectionManager::add_UnparsedMessageReceived(__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnparsedMessage* handler, EventRegistrationToken* token)
{
	return m_unparsedMessageReceivedEventSource.Add(handler, token);
}

HRESULT ConnectionManager::remove_UnparsedMessageReceived(EventRegistrationToken token)
{
	return m_unparsedMessageReceivedEventSource.Remove(token);
}

HRESULT ConnectionManager::get_ConnectionState(ConnectionState* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_connectionState;
	return S_OK;
}

HRESULT ConnectionManager::get_CurrentNetworkType(ConnectionNeworkType* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_currentNetworkType;
	return S_OK;
}

HRESULT ConnectionManager::get_IsIpv6Enabled(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_isIpv6Enabled;
	return S_OK;
}

HRESULT ConnectionManager::get_IsNetworkAvailable(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	*value = m_currentNetworkType != ConnectionNeworkType::None;
	return S_OK;
}

HRESULT ConnectionManager::get_UserConfiguration(IUserConfiguration** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();
	return m_userConfiguration.CopyTo(value);
}

HRESULT ConnectionManager::put_UserConfiguration(IUserConfiguration* value)
{
	auto lock = LockCriticalSection();

	if (value != m_userConfiguration.Get())
	{
		if (value == nullptr)
		{
			ComPtr<IDefaultUserConfiguration> defaultUserConfiguration;
			if (FAILED(m_userConfiguration.As(&defaultUserConfiguration)))
			{
				return MakeAndInitialize<DefaultUserConfiguration>(&m_userConfiguration);
			}
		}
		else
		{
			m_userConfiguration = value;

			I_WANT_TO_DIE_IS_THE_NEW_TODO("Handle UserConfiguration changes");
		}
	}
	 
	return S_OK;
}

HRESULT ConnectionManager::get_UserId(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_userId;
	return S_OK;
}

HRESULT ConnectionManager::put_UserId(INT32 value)
{
	auto lock = LockCriticalSection();

	if (value != m_userId)
	{
		m_userId = value;

		I_WANT_TO_DIE_IS_THE_NEW_TODO("Handle UserId changes");
	}

	return S_OK;
}

HRESULT ConnectionManager::SendRequest(ITLObject* object, ISendRequestCompletedCallback* onCompleted, IRequestQuickAckReceivedCallback* onQuickAckReceived,
	UINT32 datacenterId, ConnectionType connectionType, boolean immediate, INT32 requestToken)
{
	if (object == nullptr)
	{
		return E_INVALIDARG;
	}

	auto lock = LockCriticalSection();
	/*auto datacenter = GetDatacenterById(datacenterId);
	if (datacenter == nullptr)
	{
		return E_INVALIDARG;
	}*/

	HRESULT result;
	ComPtr<Request> request;
	ReturnIfFailed(result, CreateRequest(object, onCompleted, onQuickAckReceived, datacenterId, connectionType, requestToken, RequestFlag::None, request));

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::CancelRequest(INT32 requestToken, boolean notifyServer)
{
	auto lock = LockCriticalSection();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::GetDatacenterById(UINT32 id, IDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	if (id == DEFAULT_DATACENTER_ID)
	{
		return m_datacenters[m_currentDatacenterId].CopyTo(value);
	}

	auto datacenter = GetDatacenterById(id);
	if (datacenter == nullptr)
	{
		return E_INVALIDARG;
	}

	*value = datacenter;
	(*value)->AddRef();
	return S_OK;
}

HRESULT ConnectionManager::UpdateNetworkStatus(boolean raiseEvent)
{
	HRESULT result;
	ComPtr<IConnectionProfile> connectionProfile;
	ReturnIfFailed(result, m_networkInformation->GetInternetConnectionProfile(&connectionProfile));

	ConnectionNeworkType currentNetworkType;
	if (connectionProfile == nullptr)
	{
		currentNetworkType = ConnectionNeworkType::None;
	}
	else
	{
		ComPtr<IConnectionCost> connectionCost;
		ReturnIfFailed(result, connectionProfile->GetConnectionCost(&connectionCost));

		NetworkCostType networkCostType;
		ReturnIfFailed(result, connectionCost->get_NetworkCostType(&networkCostType));

		boolean isRoaming;
		ReturnIfFailed(result, connectionCost->get_Roaming(&isRoaming));

		if (isRoaming)
		{
			currentNetworkType = ConnectionNeworkType::Roaming;
		}
		else
		{
			ComPtr<INetworkAdapter> networkAdapter;
			ReturnIfFailed(result, connectionProfile->get_NetworkAdapter(&networkAdapter));

			UINT32 interfaceIanaType;
			ReturnIfFailed(result, networkAdapter->get_IanaInterfaceType(&interfaceIanaType));

			switch (interfaceIanaType)
			{
			case IF_TYPE_ETHERNET_CSMACD:
			case IF_TYPE_IEEE80211:
				currentNetworkType = ConnectionNeworkType::WiFi;
				break;
			case IF_TYPE_WWANPP:
			case IF_TYPE_WWANPP2:
				currentNetworkType = ConnectionNeworkType::Mobile;
				break;
			default:
				currentNetworkType = ConnectionNeworkType::None;
				break;
			}
		}
	}

	if (currentNetworkType != m_currentNetworkType)
	{
		m_currentNetworkType = currentNetworkType;
		return m_currentNetworkTypeChangedEventSource.InvokeAll(this, nullptr);
	}

	return S_OK;
}

HRESULT ConnectionManager::CreateRequest(ITLObject* object, ISendRequestCompletedCallback* onCompleted, IRequestQuickAckReceivedCallback* onQuickAckReceived,
	UINT32 datacenterId, ConnectionType connectionType, INT32 requestToken, RequestFlag flags, ComPtr<Request>& request)
{
	HRESULT result;
	boolean isLayerNeeded;
	ReturnIfFailed(result, object->get_IsLayerNeeded(&isLayerNeeded));

	if (isLayerNeeded)
	{
		ComPtr<TLInvokeWithLayer> invokeWithLayer;
		ReturnIfFailed(result, MakeAndInitialize<TLInvokeWithLayer>(&invokeWithLayer, object));

		ComPtr<TLInitConnection> initConnectionObject;
		ReturnIfFailed(result, MakeAndInitialize<TLInitConnection>(&initConnectionObject, m_userConfiguration.Get(), invokeWithLayer.Get()));

		request = Make<Request>(initConnectionObject.Get(), requestToken, connectionType, datacenterId, onCompleted, onQuickAckReceived, flags);
	}
	else
	{
		request = Make<Request>(object, requestToken, connectionType, datacenterId, onCompleted, onQuickAckReceived, flags);
	}

	return S_OK;
}

HRESULT ConnectionManager::OnNetworkStatusChanged(IInspectable* sender)
{
	auto lock = LockCriticalSection();
	return UpdateNetworkStatus(true);
}

HRESULT ConnectionManager::OnConnectionOpened(Connection* connection)
{
	HRESULT result;
	auto lock = LockCriticalSection();
	auto datacenter = connection->GetDatacenter();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionDataReceived(Connection* connection)
{
	HRESULT result;
	auto lock = LockCriticalSection();
	auto datacenter = connection->GetDatacenter();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionQuickAckReceived(Connection* connection, INT32 ack)
{
	HRESULT result;
	auto lock = LockCriticalSection();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionClosed(Connection* connection)
{
	HRESULT result;
	auto lock = LockCriticalSection();

	if (connection->GetType() == ConnectionType::Generic)
	{
		auto datacenter = connection->GetDatacenter();
		if (datacenter->GetHandshakeState() != HandshakeState::None)
		{
			ReturnIfFailed(result, datacenter->OnHandshakeConnectionClosed(connection));
		}

		if (datacenter->GetId() == m_currentDatacenterId)
		{
			if (m_currentNetworkType == ConnectionNeworkType::None)
			{
				if (m_connectionState != ConnectionState::WaitingForNetwork)
				{
					m_connectionState = ConnectionState::WaitingForNetwork;
					return m_connectionStateChangedEventSource.InvokeAll(this, nullptr);
				}
			}
			else
			{
				if (m_connectionState != ConnectionState::Connecting)
				{
					m_connectionState = ConnectionState::Connecting;
					return m_connectionStateChangedEventSource.InvokeAll(this, nullptr);
				}
			}
		}
	}

	return S_OK;
}

HRESULT ConnectionManager::BoomBaby(IUserConfiguration* userConfiguration, ITLObject** object, IConnection** value)
{
	if (object == nullptr || value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ComPtr<TLError> errorObject;
	ReturnIfFailed(result, MakeAndInitialize<TLError>(&errorObject, 0, L"Ciao bellezza"));

	/*ComPtr<TLInitConnection> initConnectionObject;
	ReturnIfFailed(result, MakeAndInitialize<TLInitConnection>(&initConnectionObject, userConfiguration, errorObject.Get()));
	ReturnIfFailed(result, initConnectionObject->get_Query(object));*/

	*object = errorObject.Detach();

	auto datacenter = Make<Datacenter>();
	ReturnIfFailed(result, datacenter->AddEndpoint(L"192.168.1.1", 80, ConnectionType::Generic, false));

	ComPtr<Connection> connection;
	ReturnIfFailed(result, datacenter->GetGenericConnection(true, &connection));
	ReturnIfFailed(result, connection->AttachToThreadpool(&m_threadpoolEnvironment));
	ReturnIfFailed(result, connection->Connect());

	*value = connection.Detach();
	return S_OK;
}

void ConnectionManager::OnEventObjectError(EventObject const* eventObject, HRESULT error)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement EventObject callback error tracing");
}

Datacenter* ConnectionManager::GetDatacenterById(UINT32 id)
{
	if (id == DEFAULT_DATACENTER_ID)
	{
		return m_datacenters[m_currentDatacenterId].Get();
	}

	auto& datacenter = m_datacenters.find(id);
	if (datacenter == m_datacenters.end())
	{
		return nullptr;;
	}

	return datacenter->second.Get();
}

INT64 ConnectionManager::GenerateMessageId()
{
	auto lock = LockCriticalSection();
	auto messageId = static_cast<INT64>(((static_cast<double>(GetCurrentRealTime()) + static_cast<double>(m_timeDelta) * 1000) * 4294967296.0) / 1000.0);
	if (messageId <= m_lastOutgoingMessageId)
	{
		messageId = m_lastOutgoingMessageId + 1;
	}

	while ((messageId % 4) != 0)
	{
		messageId++;
	}

	m_lastOutgoingMessageId = messageId;
	return messageId;
}

boolean ConnectionManager::IsNetworkAvailable()
{
	auto lock = LockCriticalSection();
	return m_currentNetworkType != ConnectionNeworkType::None;
}

HRESULT ConnectionManager::GetInstance(ComPtr<ConnectionManager>& value)
{
	if (s_instance == nullptr)
	{
		HRESULT result;
		ReturnIfFailed(result, MakeAndInitialize<ConnectionManager>(&s_instance));
	}

	value = s_instance;
	return S_OK;
}


HRESULT ConnectionManagerStatics::get_Instance(IConnectionManager** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	*value = connectionManager.Detach();
	return S_OK;
}

HRESULT ConnectionManagerStatics::get_Version(Version* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	//value->ConfigurationVersion = TELEGRAM_API_NATIVE_CONFIGVERSION;
	value->ProtocolVersion = TELEGRAM_API_NATIVE_PROTOVERSION;
	value->Layer = TELEGRAM_API_NATIVE_LAYER;
	value->ApiId = TELEGRAM_API_NATIVE_APIID;
	return S_OK;
}