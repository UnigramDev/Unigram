#pragma once
#include <string>
#include <vector>
#include <memory>
#include <wrl.h>
#include <windows.foundation.h>
#include "Telegram.Api.Native.h"
#include "MultiThreadObject.h"

#define DOWNLOAD_CONNECTIONS_COUNT 2
#define UPLOAD_CONNECTIONS_COUNT 2

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::IDatacenter;
using ABI::Windows::Foundation::IClosable;
using ABI::Telegram::Api::Native::ConnectionType;
using ABI::Telegram::Api::Native::HandshakeState;
using ABI::Telegram::Api::Native::TL::ITLObject;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class TLBinaryReader;


			struct ServerSalt
			{
				INT32 ValidSince;
				INT32 ValidUntil;
				INT64 Salt;
			};

			struct ServerEndpoint
			{
				std::wstring Address;
				UINT32 Port;
			};


			class Datacenter WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IDatacenter, CloakedIid<IClosable>>, public MultiThreadObject
			{
				friend class Connection;
				friend class ConnectionManager;

				InspectableClass(RuntimeClass_Telegram_Api_Native_Datacenter, BaseTrust);

			public:
				Datacenter(UINT32 id);
				Datacenter();
				~Datacenter();

				//COM exported methods			
				IFACEMETHODIMP get_Id(_Out_ UINT32* value);
				IFACEMETHODIMP get_HandshakeState(_Out_ HandshakeState* value);
				IFACEMETHODIMP get_ServerSalt(_Out_ INT64* value);
				IFACEMETHODIMP GetCurrentAddress(ConnectionType connectionType, boolean ipv6, _Out_ HSTRING* value);
				IFACEMETHODIMP GetCurrentPort(ConnectionType connectionType, boolean ipv6, _Out_ UINT32* value);
				//IFACEMETHODIMP GetDownloadConnection(UINT32 index, boolean create, _Out_ IConnection** value);
				//IFACEMETHODIMP GetUploadConnection(UINT32 index, boolean create, _Out_ IConnection** value);
				//IFACEMETHODIMP GetGenericConnection(boolean create, _Out_ IConnection** value);

				//Internal methods
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

				inline HRESULT GetCurrentEndpoint(ConnectionType connectionType, boolean ipv6, _Out_ ServerEndpoint const** endpoint)
				{
					return GetCurrentEndpoint(connectionType, ipv6, const_cast<ServerEndpoint**>(endpoint));
				}

				inline UINT32 GetId() const
				{
					return m_id;
				}

			private:
				struct HandshakeContext
				{
					HandshakeContext() :
						State(HandshakeState::None)
					{
					}

					HandshakeState State;
					ComPtr<ITLObject> Request;
				};

				IFACEMETHODIMP Close();
				HRESULT GetCurrentEndpoint(ConnectionType connectionType, boolean ipv6, _Out_ ServerEndpoint** endpoint);
				HRESULT OnHandshakeConnectionClosed(_In_ Connection* connection);
				HRESULT OnHandshakeConnectionConnected(_In_ Connection* connection);
				HRESULT OnHandshakeResponseReceived(_In_ Connection* connection, INT64 messageId);
				HRESULT GetEndpointsForConnectionType(ConnectionType connectionType, boolean ipv6, _Out_ std::vector<ServerEndpoint>** endpoints);
				HRESULT SendRequest(_In_ ITLObject* object, _In_ Connection* connection);
				boolean ContainsServerSalt(INT64 salt, size_t count);

				UINT32 m_id;
				std::unique_ptr<HandshakeContext> m_handshakeContext;
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