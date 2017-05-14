#pragma once
#include <string>
#include <vector>
#include <wrl.h>
#include <windows.foundation.h>
#include "Telegram.Api.Native.h"
#include "MultiThreadObject.h"

#define DOWNLOAD_CONNECTIONS_COUNT 2
#define UPLOAD_CONNECTIONS_COUNT 2

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::ConnectionType;
using ABI::Telegram::Api::Native::HandshakeState;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class Datacenter WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ABI::Telegram::Api::Native::IDatacenter, CloakedIid<ABI::Windows::Foundation::IClosable>, FtmBase>, public MultiThreadObject
			{
				friend class Connection;
				friend class ConnectionManager;

				InspectableClass(RuntimeClass_Telegram_Api_Native_Datacenter, BaseTrust);

			public:
				Datacenter();
				~Datacenter();

				//COM exported methods
				STDMETHODIMP RuntimeClassInitialize(UINT32 id);
				STDMETHODIMP get_Id(_Out_ UINT32* value);
				STDMETHODIMP get_HandshakeState(_Out_ HandshakeState* value);
				STDMETHODIMP GetCurrentAddress(ConnectionType connectionType, boolean ipv6, _Out_ HSTRING* value);
				STDMETHODIMP GetCurrentPort(ConnectionType connectionType, boolean ipv6, _Out_ UINT32* value);
				//STDMETHODIMP GetDownloadConnection(UINT32 index, boolean create, _Out_ IConnection** value);
				//STDMETHODIMP GetUploadConnection(UINT32 index, boolean create, _Out_ IConnection** value);
				//STDMETHODIMP GetGenericConnection(boolean create, _Out_ IConnection** value);

				//Internal methods
				void SwitchTo443Port();
				void RecreateSessions();
				void GetSessionsIds(_Out_ std::vector<INT64>& sessionIds);
				void NextEndpoint(ConnectionType connectionType, boolean ipv6);
				void ResetEndpoint();
				HandshakeState GetHandshakeState();
				HRESULT AddEndpoint(_In_ std::wstring address, UINT32 port, ConnectionType connectionType, boolean ipv6);
				HRESULT GetDownloadConnection(UINT32 index, boolean create, _Out_ Connection** value);
				HRESULT GetUploadConnection(UINT32 index, boolean create, _Out_ Connection** value);
				HRESULT GetGenericConnection(boolean create, _Out_ Connection** value);
				HRESULT SuspendConnections();

				inline UINT32 GetId() const
				{
					return m_id;
				}

			private:
				struct DatacenterEndpoint
				{
					std::wstring Address;
					UINT32 Port;
				};

				STDMETHODIMP Close();
				HRESULT GetCurrentEndpoint(ConnectionType connectionType, boolean ipv6, _Out_ DatacenterEndpoint** endpoint);
				HRESULT OnHandshakeConnectionClosed(_In_ Connection* connection);
				HRESULT OnHandshakeConnectionConnected(_In_ Connection* connection);

				UINT32 m_id;
				HandshakeState m_handshakeState;
				std::vector<DatacenterEndpoint> m_ipv4Endpoints;
				std::vector<DatacenterEndpoint> m_ipv4DownloadEndpoints;
				std::vector<DatacenterEndpoint> m_ipv6Endpoints;
				std::vector<DatacenterEndpoint> m_ipv6DownloadEndpoints;
				size_t m_currentIpv4EndpointIndex;
				size_t m_currentIpv4DownloadEndpointIndex;
				size_t m_currentIpv6EndpointIndex;
				size_t m_currentIpv6DownloadEndpointIndex;

				ComPtr<Connection> m_genericConnection;
				ComPtr<Connection> m_downloadConnections[DOWNLOAD_CONNECTIONS_COUNT];
				ComPtr<Connection> m_uploadConnections[UPLOAD_CONNECTIONS_COUNT];
			};

		}
	}
}