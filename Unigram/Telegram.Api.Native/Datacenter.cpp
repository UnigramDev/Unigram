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
#include "Wrappers\OpenSSL.h"
#include "Helpers\COMHelper.h"

#include "MethodDebug.h"

#define ENCRYPT_KEY_IV_PARAM 0
#define DECRYPT_KEY_IV_PARAM 8

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;


Datacenter::Datacenter(UINT32 id) :
	m_id(id),
	m_flags(DatacenterFlag::None),
	m_currentIpv4EndpointIndex(0),
	m_currentIpv4DownloadEndpointIndex(0),
	m_currentIpv6EndpointIndex(0),
	m_currentIpv6DownloadEndpointIndex(0)
{
}

Datacenter::Datacenter() :
	Datacenter(0)
{
}

Datacenter::~Datacenter()
{
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

void Datacenter::NextEndpoint(ConnectionType connectionType, boolean ipv6)
{
	auto lock = LockCriticalSection();

	switch (connectionType)
	{
	case ConnectionType::Generic:
	case ConnectionType::Upload:
		if (ipv6)
		{
			m_currentIpv6EndpointIndex = (m_currentIpv6EndpointIndex + 1) % m_ipv6Endpoints.size();
		}
		else
		{
			m_currentIpv4EndpointIndex = (m_currentIpv4EndpointIndex + 1) % m_ipv4Endpoints.size();
		}
		break;
	case ConnectionType::Download:
		if (ipv6)
		{
			m_currentIpv6DownloadEndpointIndex = (m_currentIpv6DownloadEndpointIndex + 1) % m_ipv6DownloadEndpoints.size();
		}
		else
		{
			m_currentIpv4DownloadEndpointIndex = (m_currentIpv4DownloadEndpointIndex + 1) % m_ipv4DownloadEndpoints.size();
		}
		break;
	}
}

void Datacenter::ResetEndpoint()
{
	auto lock = LockCriticalSection();

	m_currentIpv4EndpointIndex = 0;
	m_currentIpv4DownloadEndpointIndex = 0;
	m_currentIpv6EndpointIndex = 0;
	m_currentIpv6DownloadEndpointIndex = 0;
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

void Datacenter::MergeServerSalts(ConnectionManager* connectionManager, std::vector<ServerSalt> const& salts)
{
	auto serverSaltCount = m_serverSalts.size();
	auto timeStamp = connectionManager->GetCurrentTime();

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

boolean Datacenter::ContainsServerSalt(INT64 salt)
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

INT64 Datacenter::GetServerSalt(ConnectionManager* connectionManager)
{
	auto lock = LockCriticalSection();

	INT32 maxOffset = -1;
	INT64 salt = 0;
	auto timeStamp = connectionManager->GetCurrentTime();

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

HRESULT Datacenter::AddEndpoint(ServerEndpoint const& endpoint, ConnectionType connectionType, boolean ipv6)
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

HRESULT Datacenter::ReplaceEndpoints(std::vector<ServerEndpoint> const& newEndpoints, ConnectionType connectionType, boolean ipv6)
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
		m_genericConnection = Make<Connection>(this, ConnectionType::Generic);
	}

	value = m_genericConnection;
	return S_OK;
}

HRESULT Datacenter::GetDownloadConnection(UINT32 index, boolean create, ComPtr<Connection>& value)
{
	if (index >= DOWNLOAD_CONNECTIONS_COUNT)
	{
		return E_BOUNDS;
	}

	auto lock = LockCriticalSection();

	if ((m_flags & DatacenterFlag::Closed) == DatacenterFlag::Closed)
	{
		return RO_E_CLOSED;
	}

	if (m_downloadConnections[index] == nullptr && create)
	{
		m_downloadConnections[index] = Make<Connection>(this, ConnectionType::Download);
	}

	value = m_downloadConnections[index];
	return S_OK;
}

HRESULT Datacenter::GetUploadConnection(UINT32 index, boolean create, ComPtr<Connection>& value)
{
	if (index >= UPLOAD_CONNECTIONS_COUNT)
	{
		return E_BOUNDS;
	}

	auto lock = LockCriticalSection();

	if ((m_flags & DatacenterFlag::Closed) == DatacenterFlag::Closed)
	{
		return RO_E_CLOSED;
	}

	if (m_uploadConnections[index] == nullptr && create)
	{
		m_uploadConnections[index] = Make<Connection>(this, ConnectionType::Upload);
	}

	value = m_uploadConnections[index];
	return S_OK;
}

HRESULT Datacenter::BeginHandshake(boolean reconnect, boolean reset)
{
	auto lock = LockCriticalSection();

	if (reset)
	{
		m_authenticationContext.reset();
		m_flags &= ~DatacenterFlag::HandshakeState;
	}
	else if (static_cast<INT32>(m_flags) & 0xf)
	{
		return S_OK;
	}

	HRESULT result;
	ComPtr<Connection> genericConnection;
	ReturnIfFailed(result, GetGenericConnection(true, genericConnection));

	genericConnection->RecreateSession();

	if (reconnect && genericConnection->IsConnected())
	{
		ReturnIfFailed(result, genericConnection->Reconnect());
	}

	auto handshakeContext = std::make_unique<HandshakeContext>();
	RAND_bytes(handshakeContext->Nonce, sizeof(TLInt128));

	ComPtr<Methods::TLReqPQ> pqRequest;
	ReturnIfFailed(result, MakeAndInitialize<Methods::TLReqPQ>(&pqRequest, handshakeContext->Nonce));

	m_authenticationContext = std::move(handshakeContext);
	m_flags |= static_cast<DatacenterFlag>(HandshakeState::Started);

	return genericConnection->SendUnencryptedMessage(pqRequest.Get(), false);
}

HRESULT Datacenter::ImportAuthorization()
{
	auto lock = LockCriticalSection();

	if ((m_flags & DatacenterFlag::ImportingAuthorization) == DatacenterFlag::ImportingAuthorization)
	{
		return S_OK;
	}

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	auto authExportAuthorization = Make<Methods::TLAuthExportAuthorization>(m_id);

	m_flags |= DatacenterFlag::ImportingAuthorization;

	INT32 requestToken;
	ComPtr<Datacenter> datacenter = this;
	if (FAILED(result = connectionManager->SendRequestWithFlags(authExportAuthorization.Get(),
		Callback<ISendRequestCompletedCallback>([datacenter, connectionManager](IMessageResponse* response, IMessageError* error) -> HRESULT
	{
		auto lock = datacenter->LockCriticalSection();

		if (error == nullptr)
		{
			HRESULT result;
			ComPtr<Methods::TLAuthImportAuthorization> authImportAuthorization;
			auto authExportedAuthorization = static_cast<TLAuthExportedAuthorization*>(static_cast<MessageResponse*>(response)->GetObject().Get());
			ReturnIfFailed(result, MakeAndInitialize<Methods::TLAuthImportAuthorization>(&authImportAuthorization, authExportedAuthorization->GetId(), authExportedAuthorization->GetBytes().Get()));

			INT32 requestToken;
			return connectionManager->SendRequestWithFlags(authImportAuthorization.Get(), Callback<ISendRequestCompletedCallback>([datacenter, connectionManager](IMessageResponse* response, IMessageError* error) -> HRESULT
			{
				auto lock = datacenter->LockCriticalSection();

				datacenter->m_flags &= ~DatacenterFlag::ImportingAuthorization;

				if (error == nullptr)
				{
					datacenter->m_flags |= DatacenterFlag::Authorized;
					return connectionManager->SubmitWork([connectionManager, datacenter]() -> void
					{
						connectionManager->OnDatacenterImportAuthorizationComplete(datacenter.Get());
					});
				}
				else
				{
					return S_OK;
				}
			}).Get(), nullptr, datacenter->m_id, ConnectionType::Generic, RequestFlag::EnableUnauthorized | RequestFlag::Immediate, &requestToken);
		}
		else
		{
			datacenter->m_flags &= ~DatacenterFlag::ImportingAuthorization;
			return S_OK;
		}
	}).Get(), nullptr, DEFAULT_DATACENTER_ID, ConnectionType::Generic, RequestFlag::Immediate, &requestToken)))
	{
		m_flags &= ~DatacenterFlag::ImportingAuthorization;
		return result;
	}

	return S_OK;
}

HRESULT Datacenter::RequestFutureSalts(UINT32 count)
{
	auto lock = LockCriticalSection();

	if ((m_flags & DatacenterFlag::RequestingFutureSalts) == DatacenterFlag::RequestingFutureSalts)
	{
		return S_OK;
	}

	HRESULT result;
	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	auto getFutureSalts = Make<Methods::TLGetFutureSalts>(count);

	m_flags |= DatacenterFlag::RequestingFutureSalts;

	INT32 requestToken;
	ComPtr<Datacenter> datacenter = this;
	if (FAILED(result = connectionManager->SendRequestWithFlags(getFutureSalts.Get(),
		Callback<ISendRequestCompletedCallback>([datacenter, connectionManager](IMessageResponse* response, IMessageError* error) -> HRESULT
	{
		auto lock = datacenter->LockCriticalSection();

		datacenter->m_flags &= ~DatacenterFlag::RequestingFutureSalts;

		if (error == nullptr)
		{
			auto futureSalts = static_cast<TLFutureSalts*>(static_cast<MessageResponse*>(response)->GetObject().Get());
			datacenter->MergeServerSalts(connectionManager.Get(), futureSalts->GetSalts());
		}

		return S_OK;
	}).Get(), nullptr, m_id, ConnectionType::Generic, RequestFlag::WithoutLogin | RequestFlag::EnableUnauthorized | RequestFlag::Immediate, &requestToken)))
	{
		m_flags &= ~DatacenterFlag::RequestingFutureSalts;
		return result;
	}

	return S_OK;
}

HRESULT Datacenter::GetCurrentEndpoint(ConnectionType connectionType, boolean ipv6, ServerEndpoint** endpoint)
{
	size_t currentEndpointIndex;
	std::vector<ServerEndpoint>* endpoints;

	switch (connectionType)
	{
	case ConnectionType::Generic:
	case ConnectionType::Upload:
		if (ipv6)
		{
			currentEndpointIndex = m_currentIpv6EndpointIndex;
			endpoints = &m_ipv6Endpoints;
		}
		else
		{
			currentEndpointIndex = m_currentIpv4EndpointIndex;
			endpoints = &m_ipv4Endpoints;
		}
		break;
	case ConnectionType::Download:
		if (ipv6)
		{
			currentEndpointIndex = m_currentIpv6DownloadEndpointIndex;
			endpoints = &m_ipv6DownloadEndpoints;
		}
		else
		{
			currentEndpointIndex = m_currentIpv4DownloadEndpointIndex;
			endpoints = &m_ipv4DownloadEndpoints;
		}
		break;
	default:
		return E_INVALIDARG;
	}

	if (currentEndpointIndex >= endpoints->size())
	{
		return E_BOUNDS;
	}

	*endpoint = &(*endpoints)[currentEndpointIndex];
	return S_OK;
}

HRESULT Datacenter::GetEndpointsForConnectionType(ConnectionType connectionType, boolean ipv6, std::vector<ServerEndpoint>** endpoints)
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
	return S_OK;
}

HRESULT Datacenter::OnConnectionClosed(Connection* connection)
{
	if (connection->GetType() == ConnectionType::Generic)
	{
		auto lock = LockCriticalSection();

		if (static_cast<HandshakeState> (m_flags & DatacenterFlag::HandshakeState) != HandshakeState::Authenticated)
		{
			m_authenticationContext.reset();
			m_flags &= ~DatacenterFlag::HandshakeState;
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

	m_flags |= static_cast<DatacenterFlag>(HandshakeState::PQ);

	if (!DatacenterCryptography::CheckNonces(handshakeContext->Nonce, response->GetNonce()))
	{
		return E_INVALIDARG;
	}

	ServerPublicKey const* serverPublicKey;
	if (!DatacenterCryptography::SelectPublicKey(response->GetServerPublicKeyFingerprints(), &serverPublicKey))
	{
		return E_FAIL;
	}

	auto pq = response->GetPQ();
	UINT64 pq64 = ((pq[0] & 0xffULL) << 56ULL) | ((pq[1] & 0xffULL) << 48ULL) | ((pq[2] & 0xffULL) << 40ULL) | ((pq[3] & 0xffULL) << 32ULL) |
		((pq[4] & 0xffULL) << 24ULL) | ((pq[5] & 0xffULL) << 16ULL) | ((pq[6] & 0xffULL) << 8ULL) | ((pq[7] & 0xffULL));

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

	ComPtr<TLBinaryWriter> innerDataWriter;
	ReturnIfFailed(result, MakeAndInitialize<TLBinaryWriter>(&innerDataWriter, dhParams->GetEncryptedData()));
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

	Wrappers::BIO keyBio(BIO_new(BIO_s_mem()));
	if (!keyBio.IsValid())
	{
		return E_INVALIDARG;
	}

	BIO_write(keyBio.Get(), serverPublicKey->Key.c_str(), static_cast<int>(serverPublicKey->Key.size()));

	Wrappers::RSA rsaKey(PEM_read_bio_RSAPublicKey(keyBio.Get(), nullptr, nullptr, nullptr));
	if (!rsaKey.IsValid())
	{
		return E_INVALIDARG;
	}

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

	BN_mod_exp(r.Get(), a.Get(), rsaKey->e, rsaKey->n, DatacenterCryptography::GetBNContext());

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

	m_flags |= static_cast<DatacenterFlag>(HandshakeState::ServerDH);

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

	ComPtr<TLBinaryReader> innerDataReader;
	ReturnIfFailed(result, MakeAndInitialize<TLBinaryReader>(&innerDataReader, response->GetEncryptedData()));

	AES_KEY aesDecryptKey;
	CopyMemory(ivBuffer, aesKeyAndIvBuffer + 32, sizeof(ivBuffer));
	AES_set_decrypt_key(aesKeyAndIvBuffer, 32 * 8, &aesDecryptKey);
	AES_ige_encrypt(innerDataReader->GetBuffer(), innerDataReader->GetBuffer(), innerDataReader->GetCapacity(), &aesDecryptKey, ivBuffer, AES_DECRYPT);

	boolean hashVerified = false;
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

	UINT32 innerDataLength = sizeof(UINT32) + 2 * sizeof(TLInt128) + sizeof(INT64) + TLBinaryWriter::GetByteArrayLength(gbLenght);
	UINT32 encryptedBufferLength = SHA_DIGEST_LENGTH + innerDataLength;

	UINT32 padding = encryptedBufferLength % 16;
	if (padding != 0)
	{
		padding = 16 - padding;
	}

	ComPtr<Methods::TLSetClientDHParams> setClientDHParams;
	ReturnIfFailed(result, MakeAndInitialize<Methods::TLSetClientDHParams>(&setClientDHParams, handshakeContext->Nonce,
		handshakeContext->ServerNonce, encryptedBufferLength + padding));

	ComPtr<TLBinaryWriter> innerDataWriter;
	ReturnIfFailed(result, MakeAndInitialize<TLBinaryWriter>(&innerDataWriter, setClientDHParams->GetEncryptedData()));
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

	handshakeContext->TimeDifference = serverTime - static_cast<INT32>(ConnectionManager::GetCurrentRealTime() / 1000);
	handshakeContext->Salt.ValidSince = serverTime - 5;
	handshakeContext->Salt.ValidUntil = handshakeContext->Salt.ValidSince + 30 * 60;

	for (INT16 i = 7; i >= 0; i--)
	{
		handshakeContext->Salt.Salt <<= 8;
		handshakeContext->Salt.Salt |= (handshakeContext->NewNonce[i] ^ handshakeContext->ServerNonce[i]);
	}

	return connection->SendUnencryptedMessage(setClientDHParams.Get(), false);
}

HRESULT Datacenter::OnHandshakeClientDHResponse(ConnectionManager* connectionManager, Connection* connection, TLDHGenOk* response)
{
	INT32 timeDifference;

	{
		auto lock = LockCriticalSection();

		HRESULT result;
		HandshakeContext* handshakeContext;
		ReturnIfFailed(result, GetHandshakeContext(&handshakeContext, HandshakeState::ServerDH));

		m_flags |= static_cast<DatacenterFlag>(HandshakeState::ClientDH);

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
		m_flags |= static_cast<DatacenterFlag>(HandshakeState::Authenticated);
	}

	return connectionManager->OnDatacenterHandshakeComplete(this, timeDifference);
}

HRESULT Datacenter::OnBadServerSaltResponse(ConnectionManager* connectionManager, INT64 messageId, TLBadServerSalt* response)
{
	auto lock = LockCriticalSection();

	ClearServerSalts();

	ServerSalt salt;
	salt.ValidSince = connectionManager->GetCurrentTime();
	salt.ValidUntil = salt.ValidSince + 30 * 60;
	salt.Salt = response->GetNewServerSalt();

	AddServerSalt(salt);

	ComPtr<Datacenter> datacenter = this;
	auto requestMessageId = response->GetBadMessageContext()->Id;
	return connectionManager->SubmitWork([connectionManager, requestMessageId, messageId, datacenter]() -> void
	{
		connectionManager->OnDatacenterBadServerSalt(datacenter.Get(), requestMessageId, messageId);
	});
}

HRESULT Datacenter::OnBadMessageResponse(ConnectionManager* connectionManager, INT64 messageId, TLBadMessage* response)
{
	switch (response->GetErrorCode())
	{
	case 16:
	case 17:
	case 19:
	case 32:
	case 33:
	case 64:
		RecreateSessions();

		return connectionManager->OnDatacenterBadMessage(this, response->GetBadMessageContext()->Id, messageId);
	default:
		return S_OK;
	}
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

HRESULT Datacenter::SendAckRequest(Connection* connection, INT64 messageId)
{
	auto msgsAck = Make<TLMsgsAck>();
	msgsAck->GetMessagesIds().push_back(messageId);

	return connection->SendUnencryptedMessage(msgsAck.Get(), false);
}


HRESULT Datacenter::SendPing()
{
	HRESULT result;
	ComPtr<Connection> genericConnection;
	ReturnIfFailed(result, GetGenericConnection(true, genericConnection));

	ComPtr<ConnectionManager> connectionManager;
	ReturnIfFailed(result, ConnectionManager::GetInstance(connectionManager));

	//auto ping = Make<Methods::TLPing>(1);
	//auto getFutureSalts = Make<Methods::TLGetFutureSalts>(5);

	auto helpGetConfig = Make<Methods::TLHelpGetConfig>();

	INT32 requestToken;
	ReturnIfFailed(result, connectionManager->SendRequestWithFlags(helpGetConfig.Get(), Callback<ISendRequestCompletedCallback>([connectionManager](IMessageResponse* response, IMessageError* error) -> HRESULT
	{
		return connectionManager->m_unprocessedMessageReceivedEventSource.InvokeAll(connectionManager.Get(), response);
	}).Get(), nullptr, m_id, ConnectionType::Generic, RequestFlag::WithoutLogin | RequestFlag::CanCompress, &requestToken));

	return connectionManager->ProcessRequestsForDatacenter(this, ConnectionType::Generic);

	//return genericConnection->SendEncryptedMessage(invokeWithLayer.Get(), false, nullptr);
}