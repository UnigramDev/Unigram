#include "pch.h"
#include <iphlpapi.h>
#include "ConnectionManager.h"
#include "EventObject.h"
#include "Datacenter.h"
#include "Connection.h"
#include "TLUnprocessedMessage.h"
#include "Request.h"
#include "TLTypes.h"
#include "TLMethods.h"
#include "DefaultUserConfiguration.h"
#include "Collections.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "NativeBuffer.h"
#include "Helpers\COMHelper.h"

#include "GZip.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;
using Windows::Foundation::Collections::VectorView;

ActivatableStaticOnlyFactory(ConnectionManagerStatics);


ConnectionManager::ConnectionManager() :
	m_connectionState(ConnectionState::Connecting),
	m_currentNetworkType(ConnectionNeworkType::None),
	m_threadpool(nullptr),
	m_threadpoolCleanupGroup(nullptr),
	m_isIpv6Enabled(false),
	m_currentDatacenterId(0),
	m_movingToDatacenterId(0),
	m_timeDifference(0),
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

	m_requestEnqueuedEvent.Attach(CreateEvent(nullptr, FALSE, FALSE, nullptr));
	if (!m_requestEnqueuedEvent.IsValid())
	{
		return GetLastHRESULT();
	}

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

	auto requestEnqueuedWait = CreateThreadpoolWait(ConnectionManager::RequestEnqueuedCallback, this, &m_threadpoolEnvironment);
	if (requestEnqueuedWait == nullptr)
	{
		return GetLastHRESULT();
	}

	SetThreadpoolWait(requestEnqueuedWait, m_requestEnqueuedEvent.Get(), nullptr);

	ReturnIfFailed(result, Windows::Foundation::GetActivationFactory(HStringReference(RuntimeClass_Windows_Networking_Connectivity_NetworkInformation).Get(), &m_networkInformation));
	ReturnIfFailed(result, m_networkInformation->add_NetworkStatusChanged(Callback<INetworkStatusChangedEventHandler>(this, &ConnectionManager::OnNetworkStatusChanged).Get(), &m_networkChangedEventToken));

	return UpdateNetworkStatus(false);
}

HRESULT ConnectionManager::add_SessionCreated(__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_IInspectable* handler, EventRegistrationToken* token)
{
	return m_sessionCreatedEventSource.Add(handler, token);
}

HRESULT ConnectionManager::remove_SessionCreated(EventRegistrationToken token)
{
	return m_sessionCreatedEventSource.Remove(token);
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

HRESULT ConnectionManager::add_UnprocessedMessageReceived(__FITypedEventHandler_2_Telegram__CApi__CNative__CConnectionManager_Telegram__CApi__CNative__CTLUnprocessedMessage* handler, EventRegistrationToken* token)
{
	return m_unprocessedMessageReceivedEventSource.Add(handler, token);
}

HRESULT ConnectionManager::remove_UnprocessedMessageReceived(EventRegistrationToken token)
{
	return m_unprocessedMessageReceivedEventSource.Remove(token);
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

HRESULT ConnectionManager::get_CurrentDatacenter(IDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	ComPtr<Datacenter> datacenter;
	if (GetDatacenterById(DEFAULT_DATACENTER_ID, datacenter))
	{
		*value = datacenter.Detach();
	}

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

	auto lock = LockCriticalSection();

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

HRESULT ConnectionManager::get_Datacenters(_Out_ __FIVectorView_1_Telegram__CApi__CNative__CDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();
	auto vectorView = Make<VectorView<ABI::Telegram::Api::Native::Datacenter*>>();

	std::transform(m_datacenters.begin(), m_datacenters.end(), std::back_inserter(vectorView->GetItems()), [](auto& pair)
	{
		return static_cast<IDatacenter*>(pair.second.Get());
	});

	*value = vectorView.Detach();
	return S_OK;
}

HRESULT ConnectionManager::SendRequest(ITLObject* object, ISendRequestCompletedCallback* onCompleted, IRequestQuickAckReceivedCallback* onQuickAckReceived,
	INT32 datacenterId, ConnectionType connectionType, boolean immediate, INT32 requestToken)
{
	return SendRequestWithFlags(object, onCompleted, onQuickAckReceived, datacenterId, connectionType, immediate, requestToken, RequestFlag::None);
}

HRESULT ConnectionManager::SendRequestWithFlags(ITLObject* object, ISendRequestCompletedCallback* onCompleted, IRequestQuickAckReceivedCallback* onQuickAckReceived,
	INT32 datacenterId, ConnectionType connectionType, boolean immediate, INT32 requestToken, RequestFlag flags)
{
	if (object == nullptr || connectionType != ConnectionType::Generic || connectionType != ConnectionType::Download || connectionType != ConnectionType::Upload)
	{
		return E_INVALIDARG;
	}

	auto lock = LockCriticalSection();

	HRESULT result;
	ComPtr<MessageRequest> request;
	ReturnIfFailed(result, CreateRequest(object, onCompleted, onQuickAckReceived, datacenterId, connectionType, requestToken, flags, request));

	if (immediate)
	{
		I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement request immediate processing");
	}
	else
	{
		auto requestsLock = m_requestsCriticalSection.Lock();

		m_requestsQueue.push(request);
		SetEvent(m_requestEnqueuedEvent.Get());
	}

	return S_OK;
}

HRESULT ConnectionManager::CancelRequest(INT32 requestToken, boolean notifyServer)
{
	auto lock = LockCriticalSection();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("TODO");

	return S_OK;
}

HRESULT ConnectionManager::GetDatacenterById(INT32 id, IDatacenter** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	if (id == DEFAULT_DATACENTER_ID)
	{
		id = m_currentDatacenterId;
	}

	ComPtr<Datacenter> datacenter;
	if (!GetDatacenterById(id, datacenter))
	{
		return E_INVALIDARG;
	}

	*value = datacenter.Detach();
	return S_OK;
}

HRESULT ConnectionManager::InitializeDatacenters()
{
	HRESULT result;

#if _DEBUG

	if (m_datacenters.find(1) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(1);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.175.40", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:b28:f23d:f001:0000:0000:0000:000e", 443 }, ConnectionType::Generic, true));

		m_datacenters[1] = datacenter;
	}

	if (m_datacenters.find(2) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(2);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.167.40", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:67c:4e8:f002:0000:0000:0000:000e", 443 }, ConnectionType::Generic, true));

		m_datacenters[2] = datacenter;
	}

	if (m_datacenters.find(3) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(3);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.175.117", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:b28:f23d:f003:0000:0000:0000:000e", 443 }, ConnectionType::Generic, true));

		m_datacenters[3] = datacenter;
	}

#else

	if (m_datacenters.find(1) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(1);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.175.50", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:b28:f23d:f001:0000:0000:0000:000a", 443 }, ConnectionType::Generic, true));

		m_datacenters[1] = datacenter;
	}

	if (m_datacenters.find(2) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(2);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.167.51", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:67c:4e8:f002:0000:0000:0000:000a", 443 }, ConnectionType::Generic, true));

		m_datacenters[2] = datacenter;
	}

	if (m_datacenters.find(3) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(3);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.175.100", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:b28:f23d:f003:0000:0000:0000:000a", 443 }, ConnectionType::Generic, true));

		m_datacenters[3] = datacenter;
	}

	if (m_datacenters.find(4) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(4);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.167.91", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:67c:4e8:f004:0000:0000:0000:000a", 443 }, ConnectionType::Generic, true));

		m_datacenters[4] = datacenter;
	}

	if (m_datacenters.find(5) == m_datacenters.end())
	{
		auto datacenter = Make<Datacenter>(5);
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"149.154.171.5", 443 }, ConnectionType::Generic, false));
		ReturnIfFailed(result, datacenter->AddEndpoint({ L"2001:b28:f23f:f005:0000:0000:0000:000a", 443 }, ConnectionType::Generic, true));

		m_datacenters[5] = datacenter;
	}

#endif

	m_currentDatacenterId = 2;
	m_movingToDatacenterId = DEFAULT_DATACENTER_ID;
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
	INT32 datacenterId, ConnectionType connectionType, INT32 requestToken, RequestFlag flags, ComPtr<MessageRequest>& request)
{
	HRESULT result;
	boolean isLayerNeeded;
	ReturnIfFailed(result, object->get_IsLayerNeeded(&isLayerNeeded));

	if (isLayerNeeded)
	{
		ComPtr<Datacenter> datacenter;
		if (!(GetDatacenterById(datacenterId, datacenter) && datacenter->IsConnectionInitialized()))
		{
			ComPtr<Methods::TLInitConnection> initConnectionObject;
			ReturnIfFailed(result, MakeAndInitialize<Methods::TLInitConnection>(&initConnectionObject, m_userConfiguration.Get(), object));

			ComPtr<Methods::TLInvokeWithLayer> invokeWithLayer;
			ReturnIfFailed(result, MakeAndInitialize<Methods::TLInvokeWithLayer>(&invokeWithLayer, invokeWithLayer.Get()));

			return MakeAndInitialize<MessageRequest>(&request, invokeWithLayer.Get(), requestToken, connectionType, datacenterId, onCompleted, onQuickAckReceived, flags);
		}
	}

	return MakeAndInitialize<MessageRequest>(&request, object, requestToken, connectionType, datacenterId, onCompleted, onQuickAckReceived, flags);
}

HRESULT ConnectionManager::MoveToDatacenter(INT32 datacenterId)
{
	if (datacenterId == m_movingToDatacenterId)
	{
		return S_OK;
	}

	auto authExportAuthorization = Make<Methods::TLAuthExportAuthorization>(datacenterId);

	return S_OK;
}

HRESULT ConnectionManager::ProcessRequest(MessageRequest* request)
{
	auto datacenterId = request->GetDatacenterId();
	if (datacenterId == DEFAULT_DATACENTER_ID)
	{
		if (m_movingToDatacenterId != DEFAULT_DATACENTER_ID)
		{
			return E_FAIL;
		}

		datacenterId = m_currentDatacenterId;
	}

	ComPtr<Datacenter> datacenter;
	if (!(GetDatacenterById(datacenterId, datacenter) && datacenter->HasAuthKey()))
	{
		return E_UNEXPECTED;
	}

	if (!(request->EnableUnauthorized() || datacenterId == m_currentDatacenterId || datacenter->IsAuthorized()))
	{
		return E_FAIL;
	}

	/*if (request-TryDifferentDc())
	{

	}*/

	if (m_currentNetworkType == ConnectionNeworkType::None)
	{
		return E_FAIL;
	}

	ComPtr<Connection> connection;

	if (request->GetMessageContext() == nullptr)
	{
		request->SetMessageContext({ GenerateMessageId(), connection->GenerateMessageSequenceNumber(true) });
	}

	return S_OK;
}

HRESULT ConnectionManager::HandleMessageResponse(MessageContext const* messageContext, ITLObject* messageObject, Connection* connection)
{
	auto unprocessedMessage = Make<TLUnprocessedMessage>(messageContext->Id, connection->GetType(), messageObject);
	return m_unprocessedMessageReceivedEventSource.InvokeAll(this, unprocessedMessage.Get());
}

void ConnectionManager::ResetRequests(std::function<boolean(ComPtr<MessageRequest> const&)>& selector)
{
	auto requestsLock = m_requestsCriticalSection.Lock();

	std::list<ComPtr<MessageRequest>>::iterator requestIterator = m_runningRequests.begin();
	while (requestIterator != m_runningRequests.end())
	{
		auto& request = *requestIterator;
		if (selector(request))
		{
			request->Reset();
			m_requestsQueue.push(request);

			requestIterator = m_runningRequests.erase(requestIterator);
		}
		else
		{
			requestIterator++;
		}
	}
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

		//HandshakeState handshakeState;
		//ReturnIfFailed(result, datacenter->get_HandshakeState(&handshakeState));

		//if (handshakeState != HandshakeState::None)
		//{
		//	ReturnIfFailed(result, datacenter->OnHandshakeConnectionClosed(connection));
		//}

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

HRESULT ConnectionManager::OnRequestEnqueued(PTP_CALLBACK_INSTANCE instance)
{
	auto requestsLock = m_requestsCriticalSection.Lock();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement request queue processing");

	return S_OK;
}

HRESULT ConnectionManager::BoomBaby(IUserConfiguration* userConfiguration, ITLObject** object, IConnection** value)
{
	if (object == nullptr || value == nullptr)
	{
		return E_POINTER;
	}

	HRESULT result;
	ReturnIfFailed(result, InitializeDatacenters());

	ComPtr<TLError> errorObject;
	ReturnIfFailed(result, MakeAndInitialize<TLError>(&errorObject, E_POINTER));

	/*ComPtr<TLInitConnection> initConnectionObject;
	ReturnIfFailed(result, MakeAndInitialize<TLInitConnection>(&initConnectionObject, userConfiguration, errorObject.Get()));
	ReturnIfFailed(result, initConnectionObject->get_Query(object));*/

	*object = errorObject.Detach();

	ComPtr<Datacenter> datacenter;
	GetDatacenterById(DEFAULT_DATACENTER_ID, datacenter);
	ReturnIfFailed(result, datacenter->BeginHandshake(true));

	const WCHAR buffer[] = L"Old Macdougal had a farm in Ohio-i-o,"
		"And on that farm he had some dogs in Ohio - i - o,"
		"With a bow - wow here, and a bow - wow there,"
		"Here a bow, there a wow, everywhere a bow - wow.";

	ComPtr<NativeBuffer> binaryReaderBuffer;
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&binaryReaderBuffer, sizeof(buffer)));

	CopyMemory(binaryReaderBuffer->GetBuffer(), buffer, sizeof(buffer));

	ComPtr<TLBinaryReader> binaryReader;
	ReturnIfFailed(result, MakeAndInitialize<TLBinaryReader>(&binaryReader, binaryReaderBuffer.Get()));

	ComPtr<TLUnparsedObject> unparsedObject;
	ReturnIfFailed(result, MakeAndInitialize<TLUnparsedObject>(&unparsedObject, 0x0, binaryReader.Get()));

	auto unparsedMessage = Make<TLUnprocessedMessage>(0, ConnectionType::Generic, unparsedObject.Get());
	ReturnIfFailed(result, m_unprocessedMessageReceivedEventSource.InvokeAll(this, unparsedMessage.Get()));

	return S_OK;
}

HRESULT ConnectionManager::OnDatacenterHandshakeComplete(Datacenter* datacenter, INT32 timeDifference)
{
	auto lock = LockCriticalSection();

	if (datacenter->GetId() == m_currentDatacenterId || datacenter->GetId() == m_movingToDatacenterId)
	{
		m_timeDifference = timeDifference;

		datacenter->RecreateSessions();
	}

	return S_OK;
}

HRESULT ConnectionManager::OnConnectionSessionCreated(Connection* connection)
{
	auto lock = LockCriticalSection();
	auto datacenter = connection->GetDatacenter();

	I_WANT_TO_DIE_IS_THE_NEW_TODO("Clear requests for datacenter");

	if (connection->GetType() == ConnectionType::Generic && datacenter->GetId() == m_currentDatacenterId && m_userId != 0)
	{
		return m_sessionCreatedEventSource.InvokeAll(this, nullptr);
	}

	return S_OK;
}

void ConnectionManager::OnEventObjectError(EventObject const* eventObject, HRESULT error)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement EventObject callback error tracing");
}

boolean ConnectionManager::GetDatacenterById(UINT32 id, ComPtr<Datacenter>& datacenter)
{
	if (id == DEFAULT_DATACENTER_ID)
	{
		id = m_currentDatacenterId;
	}

	auto datacenterIterator = m_datacenters.find(id);
	if (datacenterIterator == m_datacenters.end())
	{
		return false;
	}

	datacenter = datacenterIterator->second;
	return true;
}

boolean ConnectionManager::GetRequestByMessageId(INT64 messageId, ComPtr<MessageRequest>& request)
{
	auto requestIterator = std::find_if(m_runningRequests.begin(), m_runningRequests.end(), [&messageId](auto const& request)
	{
		return request->HasMessageId(messageId);
	});

	if (requestIterator == m_runningRequests.end())
	{
		return false;
	}

	request = *requestIterator;
	m_runningRequests.erase(requestIterator);
	return true;
}

INT64 ConnectionManager::GenerateMessageId()
{
	auto lock = LockCriticalSection();
	auto messageId = static_cast<INT64>(((static_cast<double>(GetCurrentRealTime()) + static_cast<double>(m_timeDifference) * 1000) * 4294967296.0) / 1000.0);
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

INT32 ConnectionManager::GetCurrentTime()
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Check if CriticalSection is really required");

	auto lock = LockCriticalSection();
	return static_cast<INT32>(ConnectionManager::GetCurrentRealTime() / 1000) + m_timeDifference;
}

HRESULT ConnectionManager::AttachEventObject(EventObject* eventObject)
{
	if (eventObject == nullptr)
	{
		return E_INVALIDARG;
	}

	return eventObject->AttachToThreadpool(&m_threadpoolEnvironment);
}

HRESULT ConnectionManager::GetInstance(ComPtr<ConnectionManager>& value)
{
	static ComPtr<ConnectionManager> instance;
	if (instance == nullptr)
	{
		HRESULT result;
		ReturnIfFailed(result, MakeAndInitialize<ConnectionManager>(&instance));
	}

	value = instance;
	return S_OK;
}

void ConnectionManager::RequestEnqueuedCallback(PTP_CALLBACK_INSTANCE instance, PVOID context, PTP_WAIT wait, TP_WAIT_RESULT waitResult)
{
	if (waitResult == WAIT_OBJECT_0)
	{
		auto connectionManager = reinterpret_cast<ConnectionManager*>(context);
		connectionManager->OnRequestEnqueued(instance);

		SetThreadpoolWait(wait, connectionManager->m_requestEnqueuedEvent.Get(), nullptr);
	}
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

	value->ProtocolVersion = TELEGRAM_API_NATIVE_PROTOVERSION;
	value->Layer = TELEGRAM_API_NATIVE_LAYER;
	value->ApiId = TELEGRAM_API_NATIVE_APIID;
	return S_OK;
}