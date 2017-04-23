#pragma once
#include "Connection.h"

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			public enum class DatacenterEndpointType
			{
				Ipv4 = 0,
				Ipv6 = 1,
				Ipv4Download = 2,
				Ipv6Download = 3
			};

			DEFINE_ENUM_FLAG_OPERATORS(DatacenterEndpointType);


			ref class Datacenter sealed
			{
				friend ref class ConnectionsManager;

			public:
				property uint32 Id
				{
					uint32 get();
				}

				property bool IsHandshaking
				{
					bool get();
				}

				property bool HasAuthKey
				{
					bool get();
				}

				property bool IsExportingAuthorization
				{
					bool get();
				}

				property int64 ServerSalt
				{
					int64 get();
				}

				void SwitchTo443Port();
				String^ GetCurrentAddress(DatacenterEndpointType endpointType);
				int32 GetCurrentPort(DatacenterEndpointType endpointType);
				void AddAddressAndPort(_In_ String^ address, uint32 port, DatacenterEndpointType endpointType);
				void NextAddressOrPort(DatacenterEndpointType endpointType);
				void StoreCurrentAddressAndPort();
				//void ReplaceAddressesAndPorts(std::vector<std::wstring> &newAddresses, std::map<std::wstring, uint32> &newPorts, DatacenterEndpointType endpointType);
				//void SerializeToStream(NativeByteBuffer *stream);
				void Clear();
				void ClearServerSalts();
				//void MergeServerSalts(std::vector<std::unique_ptr<TL_future_salt>> &salts);
				//void AddServerSalt(std::unique_ptr<TL_future_salt> &serverSalt);
				bool ContainsServerSalt(int64 value);
				void SuspendConnections();
				//void GetSessions(std::vector<int64> &sessions);
				void RecreateSessions();
				void ResetAddressAndPort();
				Connection^ GetDownloadConnection(uint32 index, bool create);
				Connection^ GetUploadConnection(uint32 index, bool create);
				Connection^ GetGenericConnection(bool create);
				Connection^ GetPushConnection(bool create);

			internal:
				Datacenter(uint32 id);

			private:
				struct DatacenterEndpoint
				{
					std::wstring Address;
					uint32 Port;
				};

				DatacenterEndpoint* GetCurrentEndpoint(DatacenterEndpointType addressType);
				Connection^ EnsureDownloadConnection(uint32 index);
				Connection^ EnsureUploadConnection(uint32 index);
				Connection^ EnsureGenericConnection();
				Connection^ EnsurePushConnection();
	
				CriticalSection m_criticalSection;
				uint32 m_id;
				Connection^ m_genericConnection;
				Connection^ m_downloadConnections[DOWNLOAD_CONNECTIONS_COUNT];
				Connection^ m_uploadConnections[UPLOAD_CONNECTIONS_COUNT];
				Connection^ m_pushConnection;
				std::vector<DatacenterEndpoint> m_ipv4Endpoints;
				std::vector<DatacenterEndpoint> m_ipv4DownloadEndpoints;
				std::vector<DatacenterEndpoint> m_ipv6Endpoints;
				std::vector<DatacenterEndpoint> m_ipv6DownloadEndpoints;
				size_t m_currentIpv4EndpointIndex;
				size_t m_currentIpv4DownloadEndpointIndex;
				size_t m_currentIpv6EndpointIndex;
				size_t m_currentIpv6DownloadEndpointIndex;
			};

		}
	}
}