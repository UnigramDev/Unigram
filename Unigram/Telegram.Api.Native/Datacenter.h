#pragma once
#include <string>
#include <vector>
#include <memory>
#include <wrl.h>
#include <windows.foundation.h>
#include "Telegram.Api.Native.h"
#include "DatacenterServer.h"
#include "MultiThreadObject.h"
#include "ConnectionManager.h"

#define DEFAULT_DATACENTER_ID INT_MAX
#define DOWNLOAD_CONNECTIONS_COUNT 2
#define UPLOAD_CONNECTIONS_COUNT 2
#define CONNECTION_INDEX_AUTO UINT16_MAX

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::IDatacenter;
using ABI::Windows::Foundation::IClosable;
using ABI::Telegram::Api::Native::ConnectionType;
using ABI::Telegram::Api::Native::TL::ITLObject;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{
				namespace TL
				{

					struct ITLBinaryReaderEx;
					struct ITLBinaryWriterEx;

				}
			}
		}
	}
}


using ABI::Telegram::Api::Native::TL::ITLBinaryReaderEx;
using ABI::Telegram::Api::Native::TL::ITLBinaryWriterEx;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			enum class DatacenterFlag
			{
				None = 0,
				HandshakeState = 0x1F,
				AuthorizationState = 0x60,
				CDN = 0x80,
				ConnectionInitialized = 0x100,
				RequestingFutureSalts = 0x200,
				Closed = 0x400
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
				class TLBadServerSalt;
				class TLBadMessage;

			}


			class Datacenter WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IDatacenter, CloakedIid<IClosable>>, public MultiThreadObject
			{
				friend class TL::TLDHGenOk;
				friend class TL::TLDHGenFail;
				friend class TL::TLDHGenRetry;
				friend class TL::TLServerDHParamsFail;
				friend class TL::TLServerDHParamsOk;
				friend class TL::TLResPQ;
				friend class TL::TLBadServerSalt;
				friend class TL::TLBadMessage;
				friend class Connection;
				friend class ConnectionManager;

				InspectableClass(RuntimeClass_Telegram_Api_Native_Datacenter, BaseTrust);

			public:
				Datacenter();
				~Datacenter();

				//COM exported methods			
				IFACEMETHODIMP get_Id(_Out_ INT32* value);
				IFACEMETHODIMP get_Connections(_Out_ __FIVectorView_1_Telegram__CApi__CNative__CConnection** value);
				IFACEMETHODIMP GetCurrentAddress(ConnectionType connectionType, boolean ipv6, _Out_ HSTRING* value);
				IFACEMETHODIMP GetCurrentPort(ConnectionType connectionType, boolean ipv6, _Out_ UINT32* value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(_In_ ConnectionManager* connectionManager, INT32 id, bool isCdn);
				STDMETHODIMP RuntimeClassInitialize(_In_ ConnectionManager* connectionManager, _In_ ITLBinaryReaderEx* reader);
				HRESULT GetGenericConnection(boolean create, _Out_  ComPtr<Connection>& value);
				HRESULT GetDownloadConnection(boolean create, _Out_ ComPtr<Connection>& value, _Inout_ UINT16& index);
				HRESULT GetUploadConnection(boolean create, _Out_  ComPtr<Connection>& value, _Inout_ UINT16& index);

				inline INT32 GetId() const
				{
					return m_id;
				}

				inline ComPtr<ConnectionManager> const& GetConnectionManager() const
				{
					//auto lock = LockCriticalSection();
					return m_connectionManager;
				}

				inline bool IsCDN()
				{
					auto lock = LockCriticalSection();
					return (m_flags & DatacenterFlag::CDN) == DatacenterFlag::CDN;
				}

				inline bool IsHandshaking()
				{
					auto lock = LockCriticalSection();
					return static_cast<INT32>(m_flags & static_cast<DatacenterFlag>(0x11)) == 0x1;
				}

				inline bool IsAuthenticated()
				{
					auto lock = LockCriticalSection();
					return static_cast<HandshakeState>(m_flags & DatacenterFlag::HandshakeState) == HandshakeState::Authenticated;
				}

				inline bool IsConnectionInitialized()
				{
					auto lock = LockCriticalSection();
					return (m_flags & DatacenterFlag::ConnectionInitialized) == DatacenterFlag::ConnectionInitialized;
				}

				inline bool IsAuthorized()
				{
					auto lock = LockCriticalSection();
					return static_cast<AuthorizationState>(m_flags & DatacenterFlag::AuthorizationState) == AuthorizationState::Authorized;
				}

			private:
				enum class HandshakeState
				{
					None = 0x0,
					Started = 0x1,
					PQ = 0x3,
					ServerDH = 0x7,
					ClientDH = 0xF,
					Authenticated = 0x1F
				};

				enum class AuthorizationState
				{
					None = 0x0,
					Importing = 0x1 << 5,
					Authorized = 0x3 << 5
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
				void MergeServerSalts(_In_ std::vector<ServerSalt> const& salts);
				bool ContainsServerSalt(INT64 salt);
				void ClearServerSalts();
				HRESULT AddEndpoint(_In_ ServerEndpoint const& endpoint, ConnectionType connectionType, bool ipv6);
				HRESULT ReplaceEndpoints(_In_ std::vector<ServerEndpoint> const& endpoints, ConnectionType connectionType, bool ipv6);
				HRESULT NextEndpoint(ConnectionType connectionType, bool ipv6);
				void ResetEndpoint();
				void ResetConnections();
				IFACEMETHODIMP Close();
				HRESULT SaveSettings();
				HRESULT BeginHandshake(bool reconnect, bool reset);
				HRESULT ImportAuthorization();
				HRESULT RequestFutureSalts(UINT32 count);
				HRESULT GetCurrentEndpoint(ConnectionType connectionType, bool ipv6, _Out_ ServerEndpoint** endpoint);
				INT64 GetServerSalt();
				HRESULT OnConnectionOpened(_In_ Connection* connection);
				HRESULT OnConnectionClosed(_In_ Connection* connection);
				HRESULT OnHandshakePQResponse(_In_ Connection* connection, _In_ TL::TLResPQ* response);
				HRESULT OnHandshakeServerDHResponse(_In_ Connection* connection, _In_ TL::TLServerDHParamsOk* response);
				HRESULT OnHandshakeClientDHResponse(_In_ Connection* connection, _In_ TL::TLDHGenOk* response);
				HRESULT OnBadServerSaltResponse(_In_ Connection* connection, INT64 messageId, _In_ TL::TLBadServerSalt* response);
				HRESULT OnBadMessageResponse(_In_ Connection* connection, INT64 messageId, _In_ TL::TLBadMessage* response);
				HRESULT GetEndpointsForConnectionType(ConnectionType connectionType, bool ipv6, _Out_ std::vector<ServerEndpoint>** endpoints);
				HRESULT EncryptMessage(_Inout_updates_(length) BYTE* buffer, UINT32 length, UINT32 padding, _Out_opt_ INT32* quickAckId);
				HRESULT DecryptMessage(INT64 authKeyId, _Inout_updates_(length) BYTE* buffer, UINT32 length);

				inline void SetConnectionInitialized()
				{
					auto lock = LockCriticalSection();
					m_flags = m_flags | DatacenterFlag::ConnectionInitialized;
				}

				inline void SetImportingAuthorization()
				{
					auto lock = LockCriticalSection();
					m_flags = (m_flags & ~DatacenterFlag::AuthorizationState) | static_cast<DatacenterFlag>(AuthorizationState::Importing);
				}

				inline void SetAuthorized()
				{
					auto lock = LockCriticalSection();
					m_flags = m_flags | static_cast<DatacenterFlag>(AuthorizationState::Authorized);
				}

				inline void SetUnauthorized()
				{
					auto lock = LockCriticalSection();
					m_flags = m_flags & ~DatacenterFlag::AuthorizationState;
				}

				inline HRESULT OnHandshakeError(HRESULT error)
				{
					if (error == E_UNEXPECTED)
					{
						return S_OK;
					}

					return BeginHandshake(false, true);
				}

				inline HRESULT GetHandshakeContext(_Out_ HandshakeContext** handshakeContext, HandshakeState currentState)
				{
					if (static_cast<HandshakeState>(m_flags & DatacenterFlag::HandshakeState) != currentState)
					{
						return E_UNEXPECTED;
					}

					*handshakeContext = static_cast<HandshakeContext*>(m_authenticationContext.get());
					return S_OK;
				}

				static void GenerateMessageKey(_In_ BYTE const* authKey, _Inout_ BYTE* messageKey, BYTE* result, UINT32 x);
				static HRESULT ReadSettingsEndpoints(_In_ ITLBinaryReaderEx* reader, _Out_ std::vector<ServerEndpoint>& endpoints, _Out_ size_t* currentIndex);
				static HRESULT WriteSettingsEndpoints(_In_ ITLBinaryWriterEx* writer, _In_ std::vector<ServerEndpoint> const& endpoints, size_t currentIndex);
				static HRESULT SendAckRequest(_In_ Connection* connection, INT64 messageId);

				INT32 m_id;
				DatacenterFlag m_flags;
				std::unique_ptr<AuthenticationContext> m_authenticationContext;
				std::vector<ServerEndpoint> m_ipv4Endpoints;
				std::vector<ServerEndpoint> m_ipv4DownloadEndpoints;
				std::vector<ServerEndpoint> m_ipv6Endpoints;
				std::vector<ServerEndpoint> m_ipv6DownloadEndpoints;
				size_t m_currentIPv4EndpointIndex;
				size_t m_currentIPv4DownloadEndpointIndex;
				size_t m_currentIPv6EndpointIndex;
				size_t m_currentIPv6DownloadEndpointIndex;
				std::vector<ServerSalt> m_serverSalts;
				BYTE m_nextDownloadConnectionIndex;
				BYTE m_nextUploadConnectionIndex;
				ComPtr<ConnectionManager> m_connectionManager;
				ComPtr<Connection> m_genericConnection;
				ComPtr<Connection> m_downloadConnections[DOWNLOAD_CONNECTIONS_COUNT];
				ComPtr<Connection> m_uploadConnections[UPLOAD_CONNECTIONS_COUNT];
			};

		}
	}
}