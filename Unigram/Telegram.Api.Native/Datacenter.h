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
				IFACEMETHODIMP get_ServerSalt(_Out_ INT64* value);
				IFACEMETHODIMP GetCurrentAddress(ConnectionType connectionType, boolean ipv6, _Out_ HSTRING* value);
				IFACEMETHODIMP GetCurrentPort(ConnectionType connectionType, boolean ipv6, _Out_ UINT32* value);
				//IFACEMETHODIMP GetDownloadConnection(UINT32 index, boolean create, _Out_ IConnection** value);
				//IFACEMETHODIMP GetUploadConnection(UINT32 index, boolean create, _Out_ IConnection** value);
				//IFACEMETHODIMP GetGenericConnection(boolean create, _Out_ IConnection** value);

				//Internal methods
				void Clear();
				void SwitchTo443Port();
				void RecreateSessions();
				void GetSessionsIds(_Out_ std::vector<INT64>& sessionIds);
				HRESULT AddServerSalt(_In_ ServerSalt const& salt);
				HRESULT MergeServerSalts(_In_ std::vector<ServerSalt> const& salts);
				boolean ContainsServerSalt(INT64 salt);
				void ClearServerSalts();
				HRESULT AddEndpoint(_In_ ServerEndpoint const& endpoint, ConnectionType connectionType, boolean ipv6);
				HRESULT ReplaceEndpoints(_In_ std::vector<ServerEndpoint> const& endpoints, ConnectionType connectionType, boolean ipv6);
				void NextEndpoint(ConnectionType connectionType, boolean ipv6);
				void ResetEndpoint();
				HRESULT GetDownloadConnection(UINT32 index, boolean create, _Out_ Connection** value);
				HRESULT GetUploadConnection(UINT32 index, boolean create, _Out_ Connection** value);
				HRESULT GetGenericConnection(boolean create, _Out_ Connection** value);
				HRESULT SuspendConnections();
				HRESULT BeginHandshake(boolean reconnect);

				inline INT32 GetId() const
				{
					return m_id;
				}

				inline boolean HasAuthKey()
				{
					auto  lock = LockCriticalSection();
					return m_authenticationContext != nullptr && m_authenticationContext->GetState() == AuthenticationState::Completed;
				}

			private:
				enum class AuthenticationState
				{
					None = 0,
					HandshakeStarted = 1,
					HandshakePQ = 2,
					HandshakeServerDH = 3,
					HandshakeClientDH = 4,
					Completed = 5
				};

				struct AuthenticationContext abstract
				{
					virtual AuthenticationState GetState() const = 0;

					BYTE AuthKey[256];
				};

				struct HandshakeContext : AuthenticationContext
				{
					virtual AuthenticationState GetState() const override
					{
						return State;
					}

					AuthenticationState State;
					BYTE Nonce[16];
					BYTE NewNonce[32];
					BYTE ServerNonce[16];
					ServerSalt Salt;
					INT32 TimeDifference;
				};

				struct AuthKeyContext : AuthenticationContext
				{
					virtual AuthenticationState GetState() const override
					{
						return AuthenticationState::Completed;
					}

					INT64 AuthKeyId;
				};

				IFACEMETHODIMP Close();
				HRESULT GetCurrentEndpoint(ConnectionType connectionType, boolean ipv6, _Out_ ServerEndpoint** endpoint);
				boolean ContainsServerSalt(INT64 salt, size_t count);
				HRESULT OnHandshakeConnectionClosed(_In_ Connection* connection);
				HRESULT OnHandshakeConnectionConnected(_In_ Connection* connection);

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

					return BeginHandshake(false);
				}

				inline HRESULT GetHandshakeContext(_Out_ HandshakeContext** handshakeContext, AuthenticationState currentState)
				{
					if (m_authenticationContext == nullptr || m_authenticationContext->GetState() != currentState)
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