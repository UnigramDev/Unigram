#include "pch.h"
#include <algorithm>
#include <Ws2tcpip.h>
#include "Datacenter.h"
#include "DatacenterCryptography.h"
#include "NativeBuffer.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "Connection.h"
#include "ConnectionManager.h"
#include "TLTypes.h"
#include "TLMethods.h"
#include "MessageResponse.h"
#include "MessageRequest.h"
#include "MessageError.h"
#include "Collections.h"
#include "Wrappers\OpenSSL.h"
#include "Helpers\COMHelper.h"

#define ENCRYPT_KEY_IV_PARAM 0
#define DECRYPT_KEY_IV_PARAM 8
#define FLAGS_GET_HANDSHAKESTATE(flags) static_cast<Datacenter::HandshakeState>((flags) & DatacenterFlag::HandshakeState)
#define FLAGS_SET_HANDSHAKESTATE(flags, handshakeState) ((flags) & ~DatacenterFlag::HandshakeState) | static_cast<DatacenterFlag>(handshakeState)
#define FLAGS_GET_AUTHORIZATIONSTATE(flags) static_cast<Datacenter::AuthorizationState>((flags) & DatacenterFlag::AuthorizationState)
#define FLAGS_SET_AUTHORIZATIONSTATE(flags, authorizationState) ((flags) & ~DatacenterFlag::AuthorizationState) | static_cast<DatacenterFlag>(authorizationState)

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;
using Windows::Foundation::Collections::VectorView;


Datacenter::Datacenter() :
	m_id(0),
	m_flags(DatacenterFlag::None),
	m_nextDownloadConnectionIndex(0),
	m_nextUploadConnectionIndex(0),
	m_currentIPv4EndpointIndex(0),
	m_currentIPv4DownloadEndpointIndex(0),
	m_currentIPv6EndpointIndex(0),
	m_currentIPv6DownloadEndpointIndex(0)
{
}

Datacenter::~Datacenter()
{
}

HRESULT Datacenter::RuntimeClassInitialize(ConnectionManager* connectionManager, INT32 id, bool isCdn)
{
	/*if (connectionManager == nullptr)
	{
		return E_INVALIDARG;
	}*/

	if (isCdn)
	{
		m_flags = m_flags | DatacenterFlag::CDN;
	}

	m_id = id;
	m_connectionManager = connectionManager;
	return S_OK;
}

HRESULT Datacenter::RuntimeClassInitialize(ConnectionManager* connectionManager, ITLBinaryReaderEx* reader)
{
	/*if (connectionManager == nullptr || reader == nullptr)
	{
		return E_INVALIDARG;
	}*/

	m_connectionManager = connectionManager;

	HRESULT result;
	UINT32 version;
	ReturnIfFailed(result, reader->ReadUInt32(&version));

	if (version != TELEGRAM_API_NATIVE_SETTINGS_VERSION)
	{
		return E_FAIL;
	}

	UINT32 layer;
	ReturnIfFailed(result, reader->ReadUInt32(&layer));
	ReturnIfFailed(result, reader->ReadInt32(&m_id));
	ReturnIfFailed(result, reader->ReadInt32(reinterpret_cast<INT32*>(&m_flags)));

	if (layer != TELEGRAM_API_NATIVE_LAYER)
	{
		m_flags = m_flags & ~DatacenterFlag::ConnectionInitialized;
	}

	if (FLAGS_GET_HANDSHAKESTATE(m_flags) == HandshakeState::Authenticated)
	{
		auto authKeyContext = std::make_unique<AuthKeyContext>();
		ReturnIfFailed(result, reader->ReadInt64(&authKeyContext->AuthKeyId));
		ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(authKeyContext->AuthKey), authKeyContext->AuthKey));

		m_authenticationContext = std::move(authKeyContext);
	}
	else
	{
		m_flags = m_flags & ~DatacenterFlag::HandshakeState;
	}

	if (FLAGS_GET_AUTHORIZATIONSTATE(m_flags) != AuthorizationState::Authorized)
	{
		m_flags = m_flags & ~DatacenterFlag::AuthorizationState;
	}

	UINT32 serverSaltCount;
	ReturnIfFailed(result, reader->ReadUInt32(&serverSaltCount));

	m_serverSalts.resize(serverSaltCount);

	for (UINT32 i = 0; i < serverSaltCount; i++)
	{
		ReturnIfFailed(result, reader->ReadInt32(&m_serverSalts[i].ValidSince));
		ReturnIfFailed(result, reader->ReadInt32(&m_serverSalts[i].ValidUntil));
		ReturnIfFailed(result, reader->ReadInt64(&m_serverSalts[i].Salt));
	}

	ReturnIfFailed(result, ReadSettingsEndpoints(reader, m_ipv4Endpoints, &m_currentIPv4EndpointIndex));
	ReturnIfFailed(result, ReadSettingsEndpoints(reader, m_ipv4DownloadEndpoints, &m_currentIPv4DownloadEndpointIndex));
	ReturnIfFailed(result, ReadSettingsEndpoints(reader, m_ipv6Endpoints, &m_currentIPv6EndpointIndex));

	return ReadSettingsEndpoints(reader, m_ipv6DownloadEndpoints, &m_currentIPv6DownloadEndpointIndex);
}

HRESULT Datacenter::get_Id(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_id;
	return S_OK;
}

HRESULT Datacenter::get_Connections(__FIVectorView_1_Telegram__CApi__CNative__CConnection** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();
	auto vectorView = Make<VectorView<ABI::Telegram::Api::Native::Connection*>>();
	auto& connections = vectorView->GetItems();

	if (m_genericConnection != nullptr)
	{
		connections.emplace_back(m_genericConnection);
	}

	for (size_t i = 0; i < DOWNLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_downloadConnections[i] != nullptr)
		{
			connections.emplace_back(m_downloadConnections[i]);
		}
	}

	for (size_t i = 0; i < UPLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_uploadConnections[i] != nullptr)
		{
			connections.emplace_back(m_uploadConnections[i]);
		}
	}

	*value = vectorView.Detach();
	return S_OK;
}

HRESULT Datacenter::GetCurrentAddress(ConnectionType connectionType, boolean ipv6, HSTRING* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	HRESULT result;
	ServerEndpoint* endpoint;
	ReturnIfFailed(result, GetCurrentEndpoint(connectionType, ipv6, &endpoint));

	return WindowsCreateString(endpoint->Address, value);
}

HRESULT Datacenter::GetCurrentPort(ConnectionType connectionType, boolean ipv6, UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto lock = LockCriticalSection();

	HRESULT result;
	ServerEndpoint* endpoint;
	ReturnIfFailed(result, GetCurrentEndpoint(connectionType, ipv6, &endpoint));

	*value = endpoint->Port;
	return S_OK;
}

HRESULT Datacenter::Close()
{
	std::vector<ComPtr<Connection>> connectionsToClose;

	{
		auto lock = LockCriticalSection();

		if ((m_flags & DatacenterFlag::Closed) == DatacenterFlag::Closed)
		{
			return RO_E_CLOSED;
		}

		m_flags = DatacenterFlag::Closed;
		m_authenticationContext.reset();
		m_serverSalts.clear();
		m_connectionManager.Reset();

		if (m_genericConnection != nullptr)
		{
			connectionsToClose.push_back(m_genericConnection);
			m_genericConnection.Reset();
		}

		for (size_t i = 0; i < DOWNLOAD_CONNECTIONS_COUNT; i++)
		{
			if (m_downloadConnections[i] != nullptr)
			{
				connectionsToClose.push_back(m_downloadConnections[i]);
				m_downloadConnections[i].Reset();
			}
		}

		for (size_t i = 0; i < UPLOAD_CONNECTIONS_COUNT; i++)
		{
			if (m_uploadConnections[i] != nullptr)
			{
				connectionsToClose.push_back(m_uploadConnections[i]);
				m_uploadConnections[i].Reset();
			}
		}
	}

	for (auto& connection : connectionsToClose)
	{
		connection->Close();
	}

	return S_OK;
}

void Datacenter::ResetConnections()
{
	std::vector<ComPtr<Connection>> connectionsToClose;

	{
		if (m_genericConnection != nullptr)
		{
			connectionsToClose.push_back(m_genericConnection);
			m_genericConnection.Reset();
		}

		for (size_t i = 0; i < DOWNLOAD_CONNECTIONS_COUNT; i++)
		{
			if (m_downloadConnections[i] != nullptr)
			{
				connectionsToClose.push_back(m_downloadConnections[i]);
				m_downloadConnections[i].Reset();
			}
		}

		for (size_t i = 0; i < UPLOAD_CONNECTIONS_COUNT; i++)
		{
			if (m_uploadConnections[i] != nullptr)
			{
				connectionsToClose.push_back(m_uploadConnections[i]);
				m_uploadConnections[i].Reset();
			}
		}

		m_nextDownloadConnectionIndex = 0;
		m_nextUploadConnectionIndex = 0;
	}

	for (auto& connection : connectionsToClose)
	{
		connection->Close();
	}
}

void Datacenter::RecreateSessions()
{
	auto lock = LockCriticalSection();

	if (m_genericConnection != nullptr)
	{
		m_genericConnection->RecreateSession();
	}

	for (size_t i = 0; i < UPLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_uploadConnections[i] != nullptr)
		{
			m_uploadConnections[i]->RecreateSession();
		}
	}

	for (size_t i = 0; i < DOWNLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_downloadConnections[i] != nullptr)
		{
			m_downloadConnections[i]->RecreateSession();
		}
	}
}

void Datacenter::GetSessionsIds(std::vector<INT64>& sessionIds)
{
	auto lock = LockCriticalSection();

	if (m_genericConnection != nullptr)
	{
		sessionIds.push_back(m_genericConnection->GetSessionId());
	}

	for (size_t i = 0; i < UPLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_uploadConnections[i] != nullptr)
		{
			sessionIds.push_back(m_uploadConnections[i]->GetSessionId());
		}
	}

	for (size_t i = 0; i < DOWNLOAD_CONNECTIONS_COUNT; i++)
	{
		if (m_downloadConnections[i] != nullptr)
		{
			sessionIds.push_back(m_downloadConnections[i]->GetSessionId());
		}
	}
}

void Datacenter::ResetEndpoint()
{
	auto lock = LockCriticalSection();

	m_currentIPv4EndpointIndex = 0;
	m_currentIPv4DownloadEndpointIndex = 0;
	m_currentIPv6EndpointIndex = 0;
	m_currentIPv6DownloadEndpointIndex = 0;
}

void Datacenter::AddServerSalt(ServerSalt const& salt)
{
	auto lock = LockCriticalSection();

	if (!ContainsServerSalt(salt.Salt))
	{
		m_serverSalts.push_back(salt);

		std::sort(m_serverSalts.begin(), m_serverSalts.end(), [](ServerSalt const& x, ServerSalt const& y)
		{
			return x.ValidSince < y.ValidSince;
		});
	}
}

void Datacenter::MergeServerSalts(std::vector<ServerSalt> const& salts)
{
	auto serverSaltCount = m_serverSalts.size();
	auto timeStamp = m_connectionManager->GetCurrentTime();

	for (auto& serverSalt : salts)
	{
		if (serverSalt.ValidUntil > timeStamp && !ContainsServerSalt(serverSalt.Salt))
		{
			m_serverSalts.push_back(serverSalt);
		}
	}

	if (m_serverSalts.size() > serverSaltCount)
	{
		std::sort(m_serverSalts.begin(), m_serverSalts.end(), [](ServerSalt const& x, ServerSalt const& y)
		{
			return x.ValidSince < y.ValidSince;
		});
	}
}

bool Datacenter::ContainsServerSalt(INT64 salt)
{
	for (auto& serverSalt : m_serverSalts)
	{
		if (serverSalt.Salt == salt)
		{
			return true;
		}
	}

	return false;
}

INT64 Datacenter::GetServerSalt()
{
	auto lock = LockCriticalSection();

	INT32 maxOffset = -1;
	INT64 salt = 0;
	auto timeStamp = m_connectionManager->GetCurrentTime();

	auto serverSaltIterator = m_serverSalts.begin();
	while (serverSaltIterator != m_serverSalts.end())
	{
		auto& serverSalt = *serverSaltIterator;

		if (serverSalt.ValidUntil < timeStamp)
		{
			serverSaltIterator = m_serverSalts.erase(serverSaltIterator);
		}
		else
		{
			if (serverSalt.ValidSince <= timeStamp && serverSalt.ValidUntil > timeStamp)
			{
				auto currentOffset = std::abs(serverSalt.ValidUntil - timeStamp);
				if (currentOffset > maxOffset)
				{
					maxOffset = currentOffset;
					salt = serverSalt.Salt;
				}
			}

			serverSaltIterator++;
		}
	}

	return salt;
}

void Datacenter::ClearServerSalts()
{
	auto lock = LockCriticalSection();

	m_serverSalts.clear();
}

HRESULT Datacenter::NextEndpoint(ConnectionType connectionType, bool ipv6)
{
	auto lock = LockCriticalSection();

	switch (connectionType)
	{
	case ConnectionType::Generic:
	case ConnectionType::Upload:
		if (ipv6)
		{
			if (m_ipv6Endpoints.empty())
			{
				return E_NOT_VALID_STATE;
			}

			m_currentIPv6EndpointIndex = (m_currentIPv6EndpointIndex + 1) % m_ipv6Endpoints.size();
		}
		else
		{
			if (m_ipv4Endpoints.empty())
			{
				return E_NOT_VALID_STATE;
			}

			m_currentIPv4EndpointIndex = (m_currentIPv4EndpointIndex + 1) % m_ipv4Endpoints.size();
		}
		break;
	case ConnectionType::Download:
		if (ipv6)
		{
			if (m_ipv6DownloadEndpoints.empty())
			{
				return NextEndpoint(ConnectionType::Generic, true);
			}

			m_currentIPv6DownloadEndpointIndex = (m_currentIPv6DownloadEndpointIndex + 1) % m_ipv6DownloadEndpoints.size();
		}
		else
		{
			if (m_ipv4DownloadEndpoints.empty())
			{
				return NextEndpoint(ConnectionType::Generic, false);
			}

			m_currentIPv4DownloadEndpointIndex = (m_currentIPv4DownloadEndpointIndex + 1) % m_ipv4DownloadEndpoints.size();
		}
		break;
	}

	return S_OK;
}

HRESULT Datacenter::AddEndpoint(ServerEndpoint const& endpoint, ConnectionType connectionType, bool ipv6)
{
#if _DEBUG
	ADDRINFOW* addressInfo;
	if (GetAddrInfo(endpoint.Address.data(), nullptr, nullptr, &addressInfo) != NO_ERROR)
	{
		return WS_E_ENDPOINT_NOT_FOUND;
	}

	FreeAddrInfo(addressInfo);
#endif

	auto lock = LockCriticalSection();

	HRESULT result;
	std::vector<ServerEndpoint>* endpoints;
	ReturnIfFailed(result, GetEndpointsForConnectionType(connectionType, ipv6, &endpoints));

	for (size_t i = 0; i < endpoints->size(); i++)
	{
		if ((*endpoints)[i].Address.compare(endpoint.Address) == 0) // && endpoint.Port == port)
		{
			return PLA_E_NO_DUPLICATES;
		}
	}

	endpoints->push_back(endpoint);
	return S_OK;
}

HRESULT Datacenter::ReplaceEndpoints(std::vector<ServerEndpoint> const& newEndpoints, ConnectionType connectionType, bool ipv6)
{
#if _DEBUG
	for (size_t i = 0; i < newEndpoints.size(); i++)
	{
		ADDRINFOW* addressInfo;
		if (GetAddrInfo(newEndpoints[i].Address.data(), nullptr, nullptr, &addressInfo) != NO_ERROR)
		{
			return WS_E_ENDPOINT_NOT_FOUND;
		}

		FreeAddrInfo(addressInfo);
	}
#endif

	auto lock = LockCriticalSection();

	HRESULT result;
	std::vector<ServerEndpoint>* endpoints;
	ReturnIfFailed(result, GetEndpointsForConnectionType(connectionType, ipv6, &endpoints));

	*endpoints = newEndpoints;
	return S_OK;
		}

HRESULT Datacenter::GetGenericConnection(boolean create, ComPtr<Connection>& value)
{
	auto lock = LockCriticalSection();

	if ((m_flags & DatacenterFlag::Closed) == DatacenterFlag::Closed)
	{
		return RO_E_CLOSED;
	}

	if (m_genericConnection == nullptr && create)
	{
		HRESULT result;
		ReturnIfFailed(result, MakeAndInitialize<Connection>(&m_genericConnection, this, ConnectionType::Generic));
	}

	value = m_genericConnection;
	return S_OK;
}

HRESULT Datacenter::GetDownloadConnection(boolean create, ComPtr<Connection>& value, UINT16& index)
{
	auto lock = LockCriticalSection();

	if ((m_flags & DatacenterFlag::Closed) == DatacenterFlag::Closed)
	{
		return RO_E_CLOSED;
	}

	if (index == CONNECTION_INDEX_AUTO)
	{
		index = m_nextDownloadConnectionIndex;
		m_nextDownloadConnectionIndex = (m_nextDownloadConnectionIndex + 1) % DOWNLOAD_CONNECTIONS_COUNT;
	}
	else if (index >= UPLOAD_CONNECTIONS_COUNT)
	{
		return E_BOUNDS;
	}

	auto& connection = m_downloadConnections[index];
	if (connection == nullptr && create)
	{
		HRESULT result;
		ReturnIfFailed(result, MakeAndInitialize<Connection>(&connection, this, ConnectionType::Download));
	}

	value = connection;
	return S_OK;
}

HRESULT Datacenter::GetUploadConnection(boolean create, ComPtr<Connection>& value, UINT16& index)
{
	auto lock = LockCriticalSection();

	if ((m_flags & DatacenterFlag::Closed) == DatacenterFlag::Closed)
	{
		return RO_E_CLOSED;
	}

	if (index == CONNECTION_INDEX_AUTO)
	{
		index = m_nextUploadConnectionIndex;
		m_nextUploadConnectionIndex = (m_nextUploadConnectionIndex + 1) % UPLOAD_CONNECTIONS_COUNT;
	}
	else if (index >= UPLOAD_CONNECTIONS_COUNT)
	{
		return E_BOUNDS;
	}

	auto& connection = m_uploadConnections[index];
	if (connection == nullptr && create)
	{
		HRESULT result;
		ReturnIfFailed(result, MakeAndInitialize<Connection>(&connection, this, ConnectionType::Upload));
	}

	value = connection;
	return S_OK;
}

HRESULT Datacenter::BeginHandshake(bool reconnect, bool reset)
{
	auto lock = LockCriticalSection();

	if (reset)
	{
		m_authenticationContext.reset();
		m_flags = m_flags & ~DatacenterFlag::HandshakeState;
	}
	else if (static_cast<INT32>(static_cast<DatacenterFlag>(m_flags)) & static_cast<INT32>(HandshakeState::Started))
	{
		return S_FALSE;
	}

	HRESULT result;
	if ((m_flags & DatacenterFlag::CDN) == DatacenterFlag::CDN && !m_connectionManager->HasCDNPublicKey(m_id))
	{
		ReturnIfFailed(result, m_connectionManager->UpdateCDNPublicKeys());
		return S_FALSE;
	}

	ComPtr<Connection> genericConnection;
	ReturnIfFailed(result, GetGenericConnection(true, genericConnection));

	genericConnection->RecreateSession();

	if (genericConnection->IsConnected())
	{
		if (reconnect)
		{
			ReturnIfFailed(result, genericConnection->Reconnect());

			return S_FALSE;
		}
	}
	else
	{
		boolean ipv6;
		m_connectionManager->get_IsIPv6Enabled(&ipv6);

		ReturnIfFailed(result, genericConnection->Connect(ipv6));

		return S_FALSE;
	}

	/*genericConnection->RecreateSession();

	if (reconnect && genericConnection->IsConnected())
	{
		ReturnIfFailed(result, genericConnection->Reconnect());
	}*/

	auto handshakeContext = std::make_unique<HandshakeContext>();
	RAND_bytes(handshakeContext->Nonce, sizeof(TLInt128));

	ComPtr<Methods::TLReqPQ> pqRequest;
	ReturnIfFailed(result, MakeAndInitialize<Methods::TLReqPQ>(&pqRequest, handshakeContext->Nonce));

	m_authenticationContext = std::move(handshakeContext);
	m_flags = m_flags | static_cast<DatacenterFlag>(HandshakeState::Started);

	ReturnIfFailed(result, genericConnection->SendUnencryptedMessage(pqRequest.Get(), false));

	LOG_TRACE(m_connectionManager.Get(), LogLevel::Information, L"Handshake for datacenter=%d started\n", m_id);

	return S_OK;
}

HRESULT Datacenter::ImportAuthorization()
{
	auto lock = LockCriticalSection();

	if (static_cast<INT32>(static_cast<DatacenterFlag>(m_flags)) & static_cast<INT32>(AuthorizationState::Importing))
	{
		return S_FALSE;
	}
	else if ((m_flags & DatacenterFlag::CDN) == DatacenterFlag::CDN)
	{
		return E_ILLEGAL_METHOD_CALL;
	}

	auto authExportAuthorization = Make<Methods::TLAuthExportAuthorization>(m_id);

	m_flags = m_flags | static_cast<DatacenterFlag>(AuthorizationState::Importing);

	HRESULT result;
	INT32 requestToken;
	ComPtr<Datacenter> datacenter = this;
	if (FAILED(result = m_connectionManager->SendRequestWithFlags(authExportAuthorization.Get(),
		Callback<ISendRequestCompletedCallback>([datacenter](IMessageResponse* response, IMessageError* error) -> HRESULT
	{
		auto lock = datacenter->LockCriticalSection();

		if (error == nullptr)
		{
			auto authExportedAuthorization = GetMessageResponseObject<TLAuthExportedAuthorization>(response);

			HRESULT result;
			ComPtr<Methods::TLAuthImportAuthorization> authImportAuthorization;
			ReturnIfFailed(result, MakeAndInitialize<Methods::TLAuthImportAuthorization>(&authImportAuthorization, authExportedAuthorization->GetId(), authExportedAuthorization->GetBytes().Get()));

			INT32 requestToken;
			return datacenter->m_connectionManager->SendRequestWithFlags(authImportAuthorization.Get(), Callback<ISendRequestCompletedCallback>([datacenter](IMessageResponse* response, IMessageError* error) -> HRESULT
			{
				auto lock = datacenter->LockCriticalSection();

				if (error == nullptr)
				{
					datacenter->m_flags = datacenter->m_flags | static_cast<DatacenterFlag>(AuthorizationState::Authorized);

					HRESULT result;
					ReturnIfFailed(result, datacenter->m_connectionManager->OnDatacenterImportAuthorizationCompleted(datacenter.Get()));

					LOG_TRACE(datacenter->m_connectionManager.Get(), LogLevel::Information, L"Authorization for datacenter=%d imported\n", datacenter->GetId());

					return datacenter->SaveSettings();
				}
				else
				{
					datacenter->m_flags = datacenter->m_flags & ~DatacenterFlag::AuthorizationState;
					return S_OK;
				}
			}).Get(), nullptr, datacenter->m_id, ConnectionType::Generic, RequestFlag::EnableUnauthorized | RequestFlag::Immediate, &requestToken);
		}
		else
		{
			datacenter->m_flags = datacenter->m_flags & ~DatacenterFlag::AuthorizationState;
			return S_OK;
		}
	}).Get(), nullptr, DEFAULT_DATACENTER_ID, ConnectionType::Generic, RequestFlag::Immediate, &requestToken)))
	{
		m_flags = m_flags & ~DatacenterFlag::AuthorizationState;
		return result;
	}

	LOG_TRACE(m_connectionManager.Get(), LogLevel::Information, L"Importing authorization for datacenter=%d\n", m_id);

	return S_OK;
}

HRESULT Datacenter::RequestFutureSalts(UINT32 count)
{
	auto lock = LockCriticalSection();

	if ((m_flags & DatacenterFlag::RequestingFutureSalts) == DatacenterFlag::RequestingFutureSalts)
	{
		return S_OK;
	}

	auto getFutureSalts = Make<Methods::TLGetFutureSalts>(count);

	m_flags = m_flags | DatacenterFlag::RequestingFutureSalts;

	HRESULT result;
	INT32 requestToken;
	ComPtr<Datacenter> datacenter = this;
	if (FAILED(result = m_connectionManager->SendRequestWithFlags(getFutureSalts.Get(),
		Callback<ISendRequestCompletedCallback>([datacenter](IMessageResponse* response, IMessageError* error) -> HRESULT
	{
		auto lock = datacenter->LockCriticalSection();

		datacenter->m_flags = datacenter->m_flags & ~DatacenterFlag::RequestingFutureSalts;

		if (error == nullptr)
		{
			auto futureSalts = GetMessageResponseObject<TLFutureSalts>(response);
			datacenter->MergeServerSalts(futureSalts->GetSalts());

			LOG_TRACE(datacenter->m_connectionManager.Get(), LogLevel::Information, L"Server salts for datacenter=%d updated\n", datacenter->m_id);

			return datacenter->SaveSettings();
		}
		else
		{
			return S_OK;
		}
	}).Get(), nullptr, m_id, ConnectionType::Generic, RequestFlag::WithoutLogin | RequestFlag::EnableUnauthorized | RequestFlag::Immediate, &requestToken)))
	{
		m_flags = m_flags & ~DatacenterFlag::RequestingFutureSalts;
		return result;
	}

	LOG_TRACE(m_connectionManager.Get(), LogLevel::Information, L"Requesting future salts for datacenter=%d\n", m_id);

	return S_OK;
}

HRESULT Datacenter::GetCurrentEndpoint(ConnectionType connectionType, bool ipv6, ServerEndpoint** endpoint)
{
	size_t currentEndpointIndex;
	std::vector<ServerEndpoint>* endpoints;

	switch (connectionType)
	{
	case ConnectionType::Generic:
	case ConnectionType::Upload:
		if (ipv6)
		{
			currentEndpointIndex = m_currentIPv6EndpointIndex;
			endpoints = &m_ipv6Endpoints;
		}
		else
		{
			currentEndpointIndex = m_currentIPv4EndpointIndex;
			endpoints = &m_ipv4Endpoints;
		}

		if (currentEndpointIndex >= endpoints->size())
		{
			return E_BOUNDS;
		}
		break;
	case ConnectionType::Download:
		if (ipv6)
		{
			currentEndpointIndex = m_currentIPv6DownloadEndpointIndex;
			endpoints = &m_ipv6DownloadEndpoints;
		}
		else
		{
			currentEndpointIndex = m_currentIPv4DownloadEndpointIndex;
			endpoints = &m_ipv4DownloadEndpoints;
		}

		if (currentEndpointIndex >= endpoints->size())
		{
			return GetCurrentEndpoint(ConnectionType::Generic, ipv6, endpoint);
		}
		break;
	default:
		return E_INVALIDARG;
	}

	*endpoint = &(*endpoints)[currentEndpointIndex];
	return S_OK;
}

HRESULT Datacenter::GetEndpointsForConnectionType(ConnectionType connectionType, bool ipv6, std::vector<ServerEndpoint>** endpoints)
{
	switch (connectionType)
	{
	case ConnectionType::Generic:
	case ConnectionType::Upload:
		if (ipv6)
		{
			*endpoints = &m_ipv6Endpoints;
		}
		else
		{
			*endpoints = &m_ipv4Endpoints;
		}
		break;
	case ConnectionType::Download:
		if (ipv6)
		{
			*endpoints = &m_ipv6DownloadEndpoints;
		}
		else
		{
			*endpoints = &m_ipv4DownloadEndpoints;
		}
		break;
	default:
		return E_INVALIDARG;
	}

	return S_OK;
}

HRESULT Datacenter::OnConnectionOpened(Connection* connection)
{
	if (connection->GetType() == ConnectionType::Generic)
	{
		auto lock = LockCriticalSection();

		if (FLAGS_GET_HANDSHAKESTATE(m_flags) == HandshakeState::None)
		{
			return BeginHandshake(false, true);
		}
	}

	return S_OK;
}

HRESULT Datacenter::OnConnectionClosed(Connection* connection)
{
	if (connection->GetType() == ConnectionType::Generic)
	{
		auto lock = LockCriticalSection();

		if (FLAGS_GET_HANDSHAKESTATE(m_flags) != HandshakeState::Authenticated)
		{
			m_authenticationContext.reset();
			m_flags = m_flags & ~DatacenterFlag::HandshakeState;
		}
	}

	return S_OK;
}

HRESULT Datacenter::OnHandshakePQResponse(Connection* connection, TLResPQ* response)
{
	auto lock = LockCriticalSection();

	HRESULT result;
	HandshakeContext* handshakeContext;
	ReturnIfFailed(result, GetHandshakeContext(&handshakeContext, HandshakeState::Started));

	m_flags = m_flags | static_cast<DatacenterFlag>(HandshakeState::PQ);

	if (!DatacenterCryptography::CheckNonces(handshakeContext->Nonce, response->GetNonce()))
	{
		return E_INVALIDARG;
	}

	ServerPublicKey const* serverPublicKey;
	if ((m_flags & DatacenterFlag::CDN) == DatacenterFlag::CDN)
	{
		if (!m_connectionManager->GetCDNPublicKey(m_id, response->GetServerPublicKeyFingerprints(), &serverPublicKey))
		{
			return E_FAIL;
		}
	}
	else if (!DatacenterCryptography::GetDatacenterPublicKey(response->GetServerPublicKeyFingerprints(), &serverPublicKey))
	{
		return E_FAIL;
	}

	auto pq = response->GetPQ();
	UINT64 pq64 = (static_cast<UINT64>(pq[0]) << 56ULL) | (static_cast<UINT64>(pq[1]) << 48ULL) |
		(static_cast<UINT64>(pq[2]) << 40ULL) | (static_cast<UINT64>(pq[3]) << 32ULL) |
		(static_cast<UINT64>(pq[4]) << 24ULL) | (static_cast<UINT64>(pq[5]) << 16ULL) |
		(static_cast<UINT64>(pq[6]) << 8ULL) | static_cast<UINT64>(pq[7]);

	UINT32 p32;
	UINT32 q32;
	if (!DatacenterCryptography::FactorizePQ(pq64, p32, q32))
	{
		return E_FAIL;
	}

	CopyMemory(handshakeContext->ServerNonce, response->GetServerNonce(), sizeof(TLInt128));
	RAND_bytes(handshakeContext->NewNonce, sizeof(TLInt128));

	ComPtr<Methods::TLReqDHParams> dhParams;
	ReturnIfFailed(result, MakeAndInitialize<Methods::TLReqDHParams>(&dhParams, handshakeContext->Nonce, handshakeContext->ServerNonce,
		handshakeContext->NewNonce, p32, q32, serverPublicKey->Fingerprint, 256));

	ComPtr<TLMemoryBinaryWriter> innerDataWriter;
	ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryWriter>(&innerDataWriter, dhParams->GetEncryptedData()));
	ReturnIfFailed(result, innerDataWriter->put_Position(SHA_DIGEST_LENGTH));
	ReturnIfFailed(result, innerDataWriter->WriteUInt32(0x83c95aec));
	ReturnIfFailed(result, innerDataWriter->WriteBuffer(pq, sizeof(TLInt64)));
	ReturnIfFailed(result, innerDataWriter->WriteBuffer(dhParams->GetP(), sizeof(TLInt32)));
	ReturnIfFailed(result, innerDataWriter->WriteBuffer(dhParams->GetQ(), sizeof(TLInt32)));
	ReturnIfFailed(result, innerDataWriter->WriteRawBuffer(sizeof(TLInt128), handshakeContext->Nonce));
	ReturnIfFailed(result, innerDataWriter->WriteRawBuffer(sizeof(TLInt128), handshakeContext->ServerNonce));
	ReturnIfFailed(result, innerDataWriter->WriteRawBuffer(sizeof(TLInt256), handshakeContext->NewNonce));

	constexpr UINT32 innerDataLength = sizeof(UINT32) + 28 + 2 * sizeof(TLInt128) + sizeof(TLInt256);
	SHA1(innerDataWriter->GetBuffer() + SHA_DIGEST_LENGTH, innerDataLength, innerDataWriter->GetBuffer());
	RAND_bytes(innerDataWriter->GetBuffer() + SHA_DIGEST_LENGTH + innerDataLength, 255 - innerDataLength - SHA_DIGEST_LENGTH);

	Wrappers::BigNum a(BN_bin2bn(innerDataWriter->GetBuffer(), 255, nullptr));
	if (!a.IsValid())
	{
		return E_INVALIDARG;
	}

	Wrappers::BigNum r(BN_new());
	if (!r.IsValid())
	{
		return E_INVALIDARG;
	}

	BN_mod_exp(r.Get(), a.Get(), serverPublicKey->Key->e, serverPublicKey->Key->n, DatacenterCryptography::GetBNContext());

	auto encryptedDataLength = BN_bn2bin(r.Get(), innerDataWriter->GetBuffer());
	if (encryptedDataLength < 256)
	{
		ZeroMemory(innerDataWriter->GetBuffer() + encryptedDataLength, 256 - encryptedDataLength);
	}

	return connection->SendUnencryptedMessage(dhParams.Get(), false);
}

HRESULT Datacenter::OnHandshakeServerDHResponse(Connection* connection, TLServerDHParamsOk* response)
{
	auto lock = LockCriticalSection();

	HRESULT result;
	HandshakeContext* handshakeContext;
	ReturnIfFailed(result, GetHandshakeContext(&handshakeContext, HandshakeState::PQ));

	m_flags = m_flags | static_cast<DatacenterFlag>(HandshakeState::ServerDH);

	BYTE ivBuffer[32];
	BYTE aesKeyAndIvBuffer[104];
	CopyMemory(aesKeyAndIvBuffer, handshakeContext->NewNonce, sizeof(TLInt256));
	CopyMemory(aesKeyAndIvBuffer + sizeof(TLInt256), handshakeContext->ServerNonce, sizeof(TLInt128));
	SHA1(aesKeyAndIvBuffer, sizeof(TLInt256) + sizeof(TLInt128), aesKeyAndIvBuffer);

	CopyMemory(aesKeyAndIvBuffer + SHA_DIGEST_LENGTH, handshakeContext->ServerNonce, sizeof(TLInt128));
	CopyMemory(aesKeyAndIvBuffer + SHA_DIGEST_LENGTH + sizeof(TLInt128), handshakeContext->NewNonce, sizeof(TLInt256));
	SHA1(aesKeyAndIvBuffer + SHA_DIGEST_LENGTH, sizeof(TLInt128) + sizeof(TLInt256), aesKeyAndIvBuffer + SHA_DIGEST_LENGTH);

	CopyMemory(aesKeyAndIvBuffer + 2 * SHA_DIGEST_LENGTH, handshakeContext->NewNonce, sizeof(TLInt256));
	CopyMemory(aesKeyAndIvBuffer + 2 * SHA_DIGEST_LENGTH + sizeof(TLInt256), handshakeContext->NewNonce, sizeof(TLInt256));
	SHA1(aesKeyAndIvBuffer + 2 * SHA_DIGEST_LENGTH, 2 * sizeof(TLInt256), aesKeyAndIvBuffer + 2 * SHA_DIGEST_LENGTH);

	CopyMemory(aesKeyAndIvBuffer + 3 * SHA_DIGEST_LENGTH, handshakeContext->NewNonce, 4);

	ComPtr<TLMemoryBinaryReader> innerDataReader;
	ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryReader>(&innerDataReader, response->GetEncryptedData()));

	AES_KEY aesDecryptKey;
	CopyMemory(ivBuffer, aesKeyAndIvBuffer + 32, sizeof(ivBuffer));
	AES_set_decrypt_key(aesKeyAndIvBuffer, 32 * 8, &aesDecryptKey);
	AES_ige_encrypt(innerDataReader->GetBuffer(), innerDataReader->GetBuffer(), innerDataReader->GetCapacity(), &aesDecryptKey, ivBuffer, AES_DECRYPT);

	bool hashVerified = false;
	for (UINT16 i = 0; i < 16; i++)
	{
		SHA1(innerDataReader->GetBuffer() + SHA_DIGEST_LENGTH, innerDataReader->GetCapacity() - i - SHA_DIGEST_LENGTH, aesKeyAndIvBuffer + 64);

		if (memcmp(aesKeyAndIvBuffer + 64, innerDataReader->GetBuffer(), SHA_DIGEST_LENGTH) == 0)
		{
			hashVerified = true;
			break;
		}
	}

	if (!hashVerified)
	{
		return CRYPT_E_HASH_VALUE;
	}

	innerDataReader->put_Position(SHA_DIGEST_LENGTH);

	UINT32 constructor;
	ReturnIfFailed(result, innerDataReader->ReadUInt32(&constructor));

	if (constructor != 0xb5890dba)
	{
		return E_INVALIDARG;
	}

	BYTE const* nonce;
	ReturnIfFailed(result, innerDataReader->ReadRawBuffer2(&nonce, sizeof(TLInt128)));

	if (!DatacenterCryptography::CheckNonces(handshakeContext->Nonce, nonce))
	{
		return E_INVALIDARG;
	}

	BYTE const* serverNonce;
	ReturnIfFailed(result, innerDataReader->ReadRawBuffer2(&serverNonce, sizeof(TLInt128)));

	if (!DatacenterCryptography::CheckNonces(handshakeContext->ServerNonce, serverNonce))
	{
		return E_INVALIDARG;
	}

	UINT32 g32;
	ReturnIfFailed(result, innerDataReader->ReadUInt32(&g32));

	Wrappers::BigNum g(BN_new());
	BN_set_word(g.Get(), g32);

	BYTE const* dhPrimeBytes;
	UINT32 dhPrimeLength;
	ReturnIfFailed(result, innerDataReader->ReadBuffer2(&dhPrimeBytes, &dhPrimeLength));

	Wrappers::BigNum p(BN_bin2bn(dhPrimeBytes, dhPrimeLength, nullptr));
	if (!p.IsValid() && DatacenterCryptography::IsGoodPrime(p.Get(), g32))
	{
		return E_INVALIDARG;
	}

	BYTE const* gaBytes;
	UINT32 gaLength;
	ReturnIfFailed(result, innerDataReader->ReadBuffer2(&gaBytes, &gaLength));

	Wrappers::BigNum ga(BN_bin2bn(gaBytes, gaLength, nullptr));
	if (!(p.IsValid() && DatacenterCryptography::IsGoodGaAndGb(ga.Get(), p.Get())))
	{
		return E_INVALIDARG;
	}

	BYTE bBytes[256];
	RAND_bytes(bBytes, sizeof(bBytes));

	Wrappers::BigNum b(BN_bin2bn(bBytes, sizeof(bBytes), nullptr));
	if (!b.IsValid())
	{
		return E_INVALIDARG;
	}

	Wrappers::BigNum gb(BN_new());
	if (!gb.IsValid())
	{
		return E_INVALIDARG;
	}

	BN_mod_exp(gb.Get(), g.Get(), b.Get(), p.Get(), DatacenterCryptography::GetBNContext());

	INT32 serverTime;
	ReturnIfFailed(result, innerDataReader->ReadInt32(&serverTime));

	UINT32 gbLenght = BN_num_bytes(gb.Get());
	auto gbBytes = std::make_unique<BYTE[]>(gbLenght);
	BN_bn2bin(gb.Get(), gbBytes.get());

	UINT32 innerDataLength = sizeof(UINT32) + 2 * sizeof(TLInt128) + sizeof(INT64) + TLMemoryBinaryWriter::GetByteArrayLength(gbLenght);
	UINT32 encryptedBufferLength = SHA_DIGEST_LENGTH + innerDataLength;

	UINT32 padding = encryptedBufferLength % 16;
	if (padding != 0)
	{
		padding = 16 - padding;
	}

	ComPtr<Methods::TLSetClientDHParams> setClientDHParams;
	ReturnIfFailed(result, MakeAndInitialize<Methods::TLSetClientDHParams>(&setClientDHParams, handshakeContext->Nonce,
		handshakeContext->ServerNonce, encryptedBufferLength + padding));

	ComPtr<TLMemoryBinaryWriter> innerDataWriter;
	ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryWriter>(&innerDataWriter, setClientDHParams->GetEncryptedData()));
	ReturnIfFailed(result, innerDataWriter->put_Position(SHA_DIGEST_LENGTH));
	ReturnIfFailed(result, innerDataWriter->WriteUInt32(0x6643b654));
	ReturnIfFailed(result, innerDataWriter->WriteRawBuffer(sizeof(TLInt128), handshakeContext->Nonce));
	ReturnIfFailed(result, innerDataWriter->WriteRawBuffer(sizeof(TLInt128), handshakeContext->ServerNonce));
	ReturnIfFailed(result, innerDataWriter->WriteInt64(0));
	ReturnIfFailed(result, innerDataWriter->WriteByteArray(gbLenght, gbBytes.get()));

	SHA1(innerDataWriter->GetBuffer() + SHA_DIGEST_LENGTH, innerDataLength, innerDataWriter->GetBuffer());

	if (padding != 0)
	{
		RAND_bytes(innerDataWriter->GetBuffer() + SHA_DIGEST_LENGTH + innerDataLength, padding);
	}

	AES_KEY aesEncryptKey;
	CopyMemory(ivBuffer, aesKeyAndIvBuffer + 32, sizeof(ivBuffer));
	AES_set_encrypt_key(aesKeyAndIvBuffer, 32 * 8, &aesEncryptKey);
	AES_ige_encrypt(innerDataWriter->GetBuffer(), innerDataWriter->GetBuffer(), innerDataWriter->GetCapacity(), &aesEncryptKey, ivBuffer, AES_ENCRYPT);

	Wrappers::BigNum authKeyNum(BN_new());
	BN_mod_exp(authKeyNum.Get(), ga.Get(), b.Get(), p.Get(), DatacenterCryptography::GetBNContext());
	BN_bn2bin(authKeyNum.Get(), handshakeContext->AuthKey);

	auto authKeyNumLength = BN_num_bytes(authKeyNum.Get());
	if (authKeyNumLength < 256)
	{
		MoveMemory(handshakeContext->AuthKey + 256 - authKeyNumLength, handshakeContext->AuthKey, authKeyNumLength);
		ZeroMemory(handshakeContext->AuthKey, 256 - authKeyNumLength);
	}

	handshakeContext->TimeDifference = serverTime - static_cast<INT32>(ConnectionManager::GetCurrentSystemTime() / 1000);
	handshakeContext->Salt.ValidSince = serverTime - 5;
	handshakeContext->Salt.ValidUntil = handshakeContext->Salt.ValidSince + 30 * 60;

	for (INT16 i = 7; i >= 0; i--)
	{
		handshakeContext->Salt.Salt <<= 8;
		handshakeContext->Salt.Salt |= (handshakeContext->NewNonce[i] ^ handshakeContext->ServerNonce[i]);
	}

	return connection->SendUnencryptedMessage(setClientDHParams.Get(), false);
}

HRESULT Datacenter::OnHandshakeClientDHResponse(Connection* connection, TLDHGenOk* response)
{
	HRESULT result;
	INT32 timeDifference;

	{
		auto lock = LockCriticalSection();

		HandshakeContext* handshakeContext;
		ReturnIfFailed(result, GetHandshakeContext(&handshakeContext, HandshakeState::ServerDH));

		m_flags = m_flags | static_cast<DatacenterFlag>(HandshakeState::ClientDH);

		if (!(DatacenterCryptography::CheckNonces(handshakeContext->Nonce, response->GetNonce()) &&
			DatacenterCryptography::CheckNonces(handshakeContext->ServerNonce, response->GetServerNonce())))
		{
			return E_INVALIDARG;
		}

		constexpr UINT32 authKeyAuxHashLength = sizeof(TLInt256) + 1 + SHA_DIGEST_LENGTH;
		BYTE authKeyAuxHash[authKeyAuxHashLength];
		CopyMemory(authKeyAuxHash, handshakeContext->NewNonce, sizeof(TLInt256));

		authKeyAuxHash[sizeof(TLInt256)] = 1;

		SHA1(handshakeContext->AuthKey, sizeof(handshakeContext->AuthKey), authKeyAuxHash + sizeof(TLInt256) + 1);

		constexpr UINT32 authKeyIdOffset = sizeof(TLInt256) + 1 + SHA_DIGEST_LENGTH - 8;

		INT64 authKeyId = (authKeyAuxHash[authKeyIdOffset] & 0xffLL) | ((authKeyAuxHash[authKeyIdOffset + 1] & 0xffLL) << 8LL) |
			((authKeyAuxHash[authKeyIdOffset + 2] & 0xffLL) << 16LL) | ((authKeyAuxHash[authKeyIdOffset + 3] & 0xffLL) << 24LL) |
			((authKeyAuxHash[authKeyIdOffset + 4] & 0xffLL) << 32LL) | ((authKeyAuxHash[authKeyIdOffset + 5] & 0xffLL) << 40LL) |
			((authKeyAuxHash[authKeyIdOffset + 6] & 0xffLL) << 48LL) | ((authKeyAuxHash[authKeyIdOffset + 7] & 0xffLL) << 56LL);

		SHA1(authKeyAuxHash, authKeyAuxHashLength - SHA_DIGEST_LENGTH + sizeof(TLInt64), authKeyAuxHash);

		if (memcmp(response->GetNewNonceHash(), authKeyAuxHash + SHA_DIGEST_LENGTH - 16, sizeof(TLInt128)) != 0)
		{
			return CRYPT_E_HASH_VALUE;
		}

		auto authKeyContext = std::make_unique<AuthKeyContext>();
		CopyMemory(authKeyContext->AuthKey, handshakeContext->AuthKey, sizeof(authKeyContext->AuthKey));

		authKeyContext->AuthKeyId = authKeyId;
		AddServerSalt(handshakeContext->Salt);

		timeDifference = handshakeContext->TimeDifference;
		m_authenticationContext = std::move(authKeyContext);
		m_flags = m_flags | static_cast<DatacenterFlag>(HandshakeState::Authenticated);
	}

	ReturnIfFailed(result, m_connectionManager->OnDatacenterHandshakeCompleted(this, timeDifference));

	LOG_TRACE(m_connectionManager.Get(), LogLevel::Information, L"Handshake for datacenter id=%d completed\n", m_id);

	return SaveSettings();
}

HRESULT Datacenter::OnBadServerSaltResponse(Connection* connection, INT64 messageId, TLBadServerSalt* response)
{
	LOG_TRACE(m_connectionManager.Get(), LogLevel::Information, L"BadServerSalt error=%d for message with id=%llu on connection with type=%d in datacenter=%d\n",
		response->GetErrorCode(), response->GetBadMessageContext()->Id, connection->GetType(), m_id);

	{
		auto lock = LockCriticalSection();

		ClearServerSalts();

		ServerSalt salt;
		salt.ValidSince = m_connectionManager->GetCurrentTime();
		salt.ValidUntil = salt.ValidSince + 30 * 60;
		salt.Salt = response->GetNewServerSalt();

		AddServerSalt(salt);
	}

	HRESULT result;
	ReturnIfFailed(result, m_connectionManager->OnDatacenterBadServerSalt(this, response->GetBadMessageContext()->Id, messageId));

	return SaveSettings();
}

HRESULT Datacenter::OnBadMessageResponse(Connection* connection, INT64 messageId, TLBadMessage* response)
{
	LOG_TRACE(m_connectionManager.Get(), LogLevel::Information, L"BadMessage error=%d for message with id=%llu on connection with type=%d in datacenter=%d\n",
		response->GetErrorCode(), response->GetBadMessageContext()->Id, connection->GetType(), m_id);

	switch (response->GetErrorCode())
	{
	case 16:
	case 17:
	case 19:
	case 32:
	case 33:
	case 64:
	{
		RecreateSessions();

		//connection->RecreateSession();

		return m_connectionManager->OnDatacenterBadMessage(this, response->GetBadMessageContext()->Id, messageId);
	}
	case 20:
		connection->ConfirmAndResetRequest(response->GetBadMessageContext()->Id);
		break;
	}

	return S_OK;
}

HRESULT Datacenter::EncryptMessage(BYTE* buffer, UINT32 length, UINT32 padding, INT32* quickAckId)
{
	auto lock = LockCriticalSection();

	if (static_cast<HandshakeState>(m_flags & DatacenterFlag::HandshakeState) != HandshakeState::Authenticated)
	{
		return E_UNEXPECTED;
	}

	auto authKeyContext = static_cast<AuthKeyContext*>(m_authenticationContext.get());

	buffer[0] = authKeyContext->AuthKeyId & 0xff;
	buffer[1] = (authKeyContext->AuthKeyId >> 8) & 0xff;
	buffer[2] = (authKeyContext->AuthKeyId >> 16) & 0xff;
	buffer[3] = (authKeyContext->AuthKeyId >> 24) & 0xff;
	buffer[4] = (authKeyContext->AuthKeyId >> 32) & 0xff;
	buffer[5] = (authKeyContext->AuthKeyId >> 40) & 0xff;
	buffer[6] = (authKeyContext->AuthKeyId >> 48) & 0xff;
	buffer[7] = (authKeyContext->AuthKeyId >> 56) & 0xff;

	BYTE messageKey[96];

#if TELEGRAM_API_NATIVE_PROTOVERSION == 2

	SHA256_CTX sha256Context;
	SHA256_Init(&sha256Context);
	SHA256_Update(&sha256Context, authKeyContext->AuthKey + 88, 32);
	SHA256_Update(&sha256Context, buffer + 24, length - 24);
	SHA256_Final(messageKey, &sha256Context);

	if (quickAckId != nullptr)
	{
		*quickAckId = ((messageKey[0] & 0xff) | ((messageKey[1] & 0xff) << 8) |
			((messageKey[2] & 0xff) << 16) | ((messageKey[3] & 0xff) << 24)) & 0x7fffffff;
	}

#else

	SHA1(buffer + 24, length - padding - 24, messageKey + 4);

	if (quickAckId != nullptr)
	{
		*quickAckId = ((messageKey[4] & 0xff) | ((messageKey[5] & 0xff) << 8) |
			((messageKey[6] & 0xff) << 16) | ((messageKey[7] & 0xff) << 24)) & 0x7fffffff;
	}

#endif

	CopyMemory(buffer + 8, messageKey + 8, 16);
	GenerateMessageKey(authKeyContext->AuthKey, messageKey + 8, messageKey + 32, ENCRYPT_KEY_IV_PARAM);

	AES_KEY aesEncryptKey;
	AES_set_encrypt_key(messageKey + 32, 32 * 8, &aesEncryptKey);
	AES_ige_encrypt(buffer + 24, buffer + 24, length - 24, &aesEncryptKey, messageKey + 64, AES_ENCRYPT);

	return S_OK;
}

HRESULT Datacenter::DecryptMessage(INT64 authKeyId, BYTE* buffer, UINT32 length)
{
	auto lock = LockCriticalSection();

	if (static_cast<HandshakeState>(m_flags & DatacenterFlag::HandshakeState) != HandshakeState::Authenticated)
	{
		return E_UNEXPECTED;
	}

	auto authKeyContext = static_cast<AuthKeyContext*>(m_authenticationContext.get());
	if (authKeyId != authKeyContext->AuthKeyId)
	{
		return E_INVALIDARG;
	}

	BYTE messageKey[96];
	GenerateMessageKey(authKeyContext->AuthKey, buffer + 8, messageKey + 32, DECRYPT_KEY_IV_PARAM);

	AES_KEY aesDecryptKey;
	AES_set_decrypt_key(messageKey + 32, 32 * 8, &aesDecryptKey);
	AES_ige_encrypt(buffer + 24, buffer + 24, length - 24, &aesDecryptKey, messageKey + 64, AES_DECRYPT);

	constexpr UINT32 messageLengthOffset = 24 + 3 * sizeof(INT64) + sizeof(UINT32);
	UINT32 messageLength = (buffer[messageLengthOffset] & 0xff) | ((buffer[messageLengthOffset + 1] & 0xff) << 8) |
		((buffer[messageLengthOffset + 2] & 0xff) << 16) | ((buffer[messageLengthOffset + 3] & 0xff) << 24);

	if ((messageLength += 3 * sizeof(INT64) + 2 * sizeof(UINT32)) + 24 > length)
	{
		return E_INVALIDARG;
	}

#if TELEGRAM_API_NATIVE_PROTOVERSION == 2

	SHA256_CTX sha256Context;
	SHA256_Init(&sha256Context);
	SHA256_Update(&sha256Context, authKeyContext->AuthKey + 88 + DECRYPT_KEY_IV_PARAM, 32);
	SHA256_Update(&sha256Context, buffer + 24, length - 24);
	SHA256_Final(messageKey, &sha256Context);

#else

	SHA1(buffer + 24, messageLength, messageKey + 4);

#endif

	if (memcmp(messageKey + 8, buffer + 8, 16) != 0)
	{
		return CRYPT_E_HASH_VALUE;
	}

	return S_OK;
}

void Datacenter::GenerateMessageKey(BYTE const* authKey, BYTE* messageKey, BYTE* result, UINT32 x)
{
	BYTE sha[68];

#if TELEGRAM_API_NATIVE_PROTOVERSION == 2

	SHA256_CTX sha256Context;
	SHA256_Init(&sha256Context);
	SHA256_Update(&sha256Context, messageKey, 16);
	SHA256_Update(&sha256Context, authKey + x, 36);
	SHA256_Final(sha, &sha256Context);

	SHA256_Init(&sha256Context);
	SHA256_Update(&sha256Context, authKey + 40 + x, 36);
	SHA256_Update(&sha256Context, messageKey, 16);
	SHA256_Final(sha + 32, &sha256Context);

	CopyMemory(result, sha, 8);
	CopyMemory(result + 8, sha + 32 + 8, 16);
	CopyMemory(result + 8 + 16, sha + 24, 8);

	CopyMemory(result + 32, sha + 32, 8);
	CopyMemory(result + 32 + 8, sha + 8, 16);
	CopyMemory(result + 32 + 8 + 16, sha + 32 + 24, 8);

#else

	CopyMemory(sha + SHA_DIGEST_LENGTH, messageKey, 16);
	CopyMemory(sha + SHA_DIGEST_LENGTH + 16, authKey + x, 32);
	SHA1(sha + SHA_DIGEST_LENGTH, 48, sha);
	CopyMemory(result, sha, 8);
	CopyMemory(result + 32, sha + 8, 12);

	CopyMemory(sha + SHA_DIGEST_LENGTH, authKey + 32 + x, 16);
	CopyMemory(sha + SHA_DIGEST_LENGTH + 16, messageKey, 16);
	CopyMemory(sha + SHA_DIGEST_LENGTH + 16 + 16, authKey + 48 + x, 16);
	SHA1(sha + SHA_DIGEST_LENGTH, 48, sha);
	CopyMemory(result + 8, sha + 8, 12);
	CopyMemory(result + 32 + 12, sha, 8);

	CopyMemory(sha + SHA_DIGEST_LENGTH, authKey + 64 + x, 32);
	CopyMemory(sha + SHA_DIGEST_LENGTH + 32, messageKey, 16);
	SHA1(sha + SHA_DIGEST_LENGTH, 48, sha);
	CopyMemory(result + 8 + 12, sha + 4, 12);
	CopyMemory(result + 32 + 12 + 8, sha + 16, 4);

	CopyMemory(sha + SHA_DIGEST_LENGTH, messageKey, 16);
	CopyMemory(sha + SHA_DIGEST_LENGTH + 16, authKey + 96 + x, 32);
	SHA1(sha + SHA_DIGEST_LENGTH, 48, sha);
	CopyMemory(result + 32 + 12 + 8 + 4, sha, 8);

#endif
}

HRESULT Datacenter::SaveSettings()
{
	auto lock = LockCriticalSection();

	std::wstring settingsFileName;
	m_connectionManager->GetDatacenterSettingsFileName(m_id, settingsFileName);

	HRESULT result;
	ComPtr<TLFileBinaryWriter> settingsWriter;
	ReturnIfFailed(result, MakeAndInitialize<TLFileBinaryWriter>(&settingsWriter, settingsFileName.data(), CREATE_ALWAYS));
	ReturnIfFailed(result, settingsWriter->WriteUInt32(TELEGRAM_API_NATIVE_SETTINGS_VERSION));
	ReturnIfFailed(result, settingsWriter->WriteUInt32(TELEGRAM_API_NATIVE_LAYER));
	ReturnIfFailed(result, settingsWriter->WriteInt32(m_id));
	ReturnIfFailed(result, settingsWriter->WriteInt32(static_cast<INT32>(m_flags & (DatacenterFlag::HandshakeState |
		DatacenterFlag::AuthorizationState | DatacenterFlag::CDN | DatacenterFlag::ConnectionInitialized))));

	if (FLAGS_GET_HANDSHAKESTATE(m_flags) == HandshakeState::Authenticated)
	{
		auto authKeyContext = static_cast<AuthKeyContext*>(m_authenticationContext.get());
		ReturnIfFailed(result, settingsWriter->WriteInt64(authKeyContext->AuthKeyId));
		ReturnIfFailed(result, settingsWriter->WriteRawBuffer(sizeof(authKeyContext->AuthKey), authKeyContext->AuthKey));
	}

	ReturnIfFailed(result, settingsWriter->WriteUInt32(static_cast<UINT32>(m_serverSalts.size())));

	for (auto& serverSalt : m_serverSalts)
	{
		ReturnIfFailed(result, settingsWriter->WriteInt32(serverSalt.ValidSince));
		ReturnIfFailed(result, settingsWriter->WriteInt32(serverSalt.ValidUntil));
		ReturnIfFailed(result, settingsWriter->WriteInt64(serverSalt.Salt));
	}

	ReturnIfFailed(result, WriteSettingsEndpoints(settingsWriter.Get(), m_ipv4Endpoints, m_currentIPv4EndpointIndex));
	ReturnIfFailed(result, WriteSettingsEndpoints(settingsWriter.Get(), m_ipv4DownloadEndpoints, m_currentIPv4DownloadEndpointIndex));
	ReturnIfFailed(result, WriteSettingsEndpoints(settingsWriter.Get(), m_ipv6Endpoints, m_currentIPv6EndpointIndex));

	return WriteSettingsEndpoints(settingsWriter.Get(), m_ipv6DownloadEndpoints, m_currentIPv6DownloadEndpointIndex);
}

HRESULT Datacenter::ReadSettingsEndpoints(ITLBinaryReaderEx* reader, std::vector<ServerEndpoint>& endpoints, size_t* currentIndex)
{
	HRESULT result;
	UINT32 endpointCount;
	ReturnIfFailed(result, reader->ReadUInt32(&endpointCount));

	endpoints.resize(endpointCount);

	for (UINT32 i = 0; i < endpointCount; i++)
	{
		ReturnIfFailed(result, reader->ReadWString(endpoints[i].Address));
		ReturnIfFailed(result, reader->ReadUInt32(&endpoints[i].Port));
	}

#ifdef _WIN64
	return reader->ReadUInt64(currentIndex);
#else
	return reader->ReadUInt32(currentIndex);
#endif
}

HRESULT Datacenter::WriteSettingsEndpoints(ITLBinaryWriterEx* writer, std::vector<ServerEndpoint> const& endpoints, size_t currentIndex)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteUInt32(static_cast<UINT32>(endpoints.size())));

	for (auto& endpoint : endpoints)
	{
		ReturnIfFailed(result, writer->WriteWString(endpoint.Address));
		ReturnIfFailed(result, writer->WriteUInt32(endpoint.Port));
	}

#ifdef _WIN64
	return writer->WriteUInt64(currentIndex);
#else
	return writer->WriteUInt32(currentIndex);
#endif
}

HRESULT Datacenter::SendAckRequest(Connection* connection, INT64 messageId)
{
	auto msgsAck = Make<TLMsgsAck>();
	msgsAck->GetMessagesIds().push_back(messageId);

	return connection->SendUnencryptedMessage(msgsAck.Get(), false);
}