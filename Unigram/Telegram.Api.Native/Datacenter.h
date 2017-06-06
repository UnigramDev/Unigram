#pragma once
#include <string>
#include <vector>
#include <memory>
#include <wrl.h>
#include <windows.foundation.h>
#include "Telegram.Api.Native.h"
#include "DatacenterServer.h"
#include "MultiThreadObject.h"

#define DOWNLOAD_CONNECTIONS_COUNT 2
#define UPLOAD_CONNECTIONS_COUNT 2
#define DATACENTER_UPDATE_TIME 60 * 60

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::IDatacenter;
using ABI::Windows::Foundation::IClosable;
using ABI::Telegram::Api::Native::ConnectionType;
using ABI::Telegram::Api::Native::TL::ITLObject;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			enum class DatacenterFlag
			{
				None = 0,
				Authenticated = 0x1F,
				ConnectionInitialized = 0x20,
				Authorized = 0x40,
				ImportingAuthorization = 0x80,
				RequestingSalt = 0x100,
				Closed = 0x200
			};

		}
	}
}

DEFINE_ENUM_FLAG_OPERATORS(Telegram::Api::Native::DatacenterFlag);


namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLDHGenOk;
				class TLDHGenFail;
				class TLDHGenRetry;
				class TLServerDHParamsFail;
				class TLServerDHParamsOk;
				class TLResPQ;
				class TLFutureSalts;

			}


			class Datacenter WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IDatacenter, CloakedIid<IClosable>>, public MultiThreadObject
			{
				friend class TL::TLDHGenOk;
				friend class TL::TLDHGenFail;
				friend class TL::TLDHGenRetry;
				friend class TL::TLServerDHParamsFail;
				friend class TL::TLServerDHParamsOk;
				friend class TL::TLResPQ;
				friend class TL::TLFutureSalts;
				friend class Connection;
				friend class ConnectionManager;

				InspectableClass(RuntimeClass_Telegram_Api_Native_Datacenter, BaseTrust);

			public:
				Datacenter(UINT32 id);
				Datacenter();
				~Datacenter();

				//COM exported methods			
				IFACEMETHODIMP get_Id(_Out_ INT32* value);
				IFACEMETHODIMP GetCurrentAddress(ConnectionType connectionType, boolean ipv6, _Out_ HSTRING* value);
				IFACEMETHODIMP GetCurrentPort(ConnectionType connectionType, boolean ipv6, _Out_ UINT32* value);

				//Internal methods
				HRESULT GetGenericConnection(boolean create, _Out_  ComPtr<Connection>& value);
				HRESULT GetDownloadConnection(UINT32 index, boolean create, _Out_ ComPtr<Connection>& value);
				HRESULT GetUploadConnection(UINT32 index, boolean create, _Out_  ComPtr<Connection>& value);
				
				inline INT32 GetId() const
				{
					return m_id;
				}

				inline boolean IsHandshaking()
				{
					auto lock = LockCriticalSection();
					return (static_cast<INT32>(m_flags) & 0x11) == 0x1;
				}

				inline boolean IsAuthenticated()
				{
					auto  lock = LockCriticalSection();
					return (m_flags & DatacenterFlag::Authenticated) == DatacenterFlag::Authenticated;
				}

				inline boolean IsConnectionInitialized()
				{
					auto  lock = LockCriticalSection();
					return (m_flags & DatacenterFlag::ConnectionInitialized) == DatacenterFlag::ConnectionInitialized;
				}

				inline boolean IsAuthorized()
				{
					auto  lock = LockCriticalSection();
					return (m_flags & DatacenterFlag::Authorized) == DatacenterFlag::Authorized;
				}

				inline boolean IsImportingAuthorization()
				{
					auto  lock = LockCriticalSection();
					return (m_flags & DatacenterFlag::ImportingAuthorization) == DatacenterFlag::ImportingAuthorization;
				}

				inline boolean IsRequestingSalt()
				{
					auto  lock = LockCriticalSection();
					return (m_flags & DatacenterFlag::RequestingSalt) == DatacenterFlag::RequestingSalt;
				}

			private:
				enum class AuthenticationState
				{
					HandshakeStarted = 1,
					HandshakePQ = 3,
					HandshakeServerDH = 7,
					HandshakeClientDH = 15
				};

				struct AuthenticationContext abstract
				{
					BYTE AuthKey[256];
				};

				struct HandshakeContext : AuthenticationContext
				{
					BYTE Nonce[16];
					BYTE NewNonce[32];
					BYTE ServerNonce[16];
					ServerSalt Salt;
					INT32 TimeDifference;
				};

				struct AuthKeyContext : AuthenticationContext
				{
					INT64 AuthKeyId;
				};

				void RecreateSessions();
				void GetSessionsIds(_Out_ std::vector<INT64>& sessionIds);
				void AddServerSalt(_In_ ServerSalt const& salt);
				HRESULT MergeServerSalts(_In_ std::vector<ServerSalt> const& salts);
				boolean ContainsServerSalt(INT64 salt);
				void ClearServerSalts();
				HRESULT AddEndpoint(_In_ ServerEndpoint const& endpoint, ConnectionType connectionType, boolean ipv6);
				HRESULT ReplaceEndpoints(_In_ std::vector<ServerEndpoint> const& endpoints, ConnectionType connectionType, boolean ipv6);
				void NextEndpoint(ConnectionType connectionType, boolean ipv6);
				void ResetEndpoint();
				IFACEMETHODIMP Close();
				HRESULT BeginHandshake(boolean reconnect, boolean reset);
				HRESULT ImportAuthorization();
				HRESULT GetCurrentEndpoint(ConnectionType connectionType, boolean ipv6, _Out_ ServerEndpoint** endpoint);
				INT64 GetServerSalt(_In_ ConnectionManager* connectionManager);
				boolean ContainsServerSalt(INT64 salt, size_t count);
				HRESULT OnConnectionOpened(_In_ ConnectionManager* connectionManager, _In_ Connection* connection);
				HRESULT OnConnectionClosed(_In_ ConnectionManager* connectionManager, _In_ Connection* connection);
				HRESULT HandleHandshakePQResponse(_In_ Connection* connection, _In_ TL::TLResPQ* response);
				HRESULT HandleHandshakeServerDHResponse(_In_ Connection* connection, _In_ TL::TLServerDHParamsOk* response);
				HRESULT HandleHandshakeClientDHResponse(_In_ ConnectionManager* connectionManager, _In_ Connection* connection, _In_ TL::TLDHGenOk* response);
				HRESULT HandleFutureSaltsResponse(_In_ TL::TLFutureSalts* response);
				HRESULT GetEndpointsForConnectionType(ConnectionType connectionType, boolean ipv6, _Out_ std::vector<ServerEndpoint>** endpoints);
				HRESULT EncryptMessage(_Inout_updates_(length) BYTE* buffer, UINT32 length, UINT32 padding, _Out_opt_ INT32* quickAckId);
				HRESULT DecryptMessage(INT64 authKeyId, _Inout_updates_(length) BYTE* buffer, UINT32 length);

				inline HRESULT HandleHandshakeError(HRESULT error)
				{
					if (error == E_UNEXPECTED)
					{
						return S_OK;
					}

					return BeginHandshake(false, true);
				}

				inline HRESULT GetHandshakeContext(_Out_ HandshakeContext** handshakeContext, AuthenticationState currentState)
				{
					if (static_cast<INT32>(m_flags & DatacenterFlag::Authenticated) != static_cast<INT32>(currentState))
					{
						return E_UNEXPECTED;
					}

					*handshakeContext = static_cast<HandshakeContext*>(m_authenticationContext.get());
					return S_OK;
				}


				HRESULT SendPing();

				static void GenerateMessageKey(_In_ BYTE const* authKey, _Inout_ BYTE* messageKey, BYTE* result, UINT32 x);
				static HRESULT SendAckRequest(_In_ Connection* connection, INT64 messageId);

				INT32 m_id;
				DatacenterFlag m_flags;
				std::unique_ptr<AuthenticationContext> m_authenticationContext;
				std::vector<ServerEndpoint> m_ipv4Endpoints;
				std::vector<ServerEndpoint> m_ipv4DownloadEndpoints;
				std::vector<ServerEndpoint> m_ipv6Endpoints;
				std::vector<ServerEndpoint> m_ipv6DownloadEndpoints;
				size_t m_currentIpv4EndpointIndex;
				size_t m_currentIpv4DownloadEndpointIndex;
				size_t m_currentIpv6EndpointIndex;
				size_t m_currentIpv6DownloadEndpointIndex;
				std::vector<ServerSalt> m_serverSalts;
				ComPtr<Connection> m_genericConnection;
				ComPtr<Connection> m_downloadConnections[DOWNLOAD_CONNECTIONS_COUNT];
				ComPtr<Connection> m_uploadConnections[UPLOAD_CONNECTIONS_COUNT];
			};

		}
	}
}