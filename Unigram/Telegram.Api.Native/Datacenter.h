#pragma once
#include <string>
#include <vector>
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class Datacenter WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IDatacenter, FtmBase>
			{
				friend class Connection;

				InspectableClass(RuntimeClass_Telegram_Api_Native_Datacenter, BaseTrust);

			public:
				Datacenter();
				~Datacenter();

				STDMETHODIMP RuntimeClassInitialize(UINT32 id);
				STDMETHODIMP get_Id(_Out_ UINT32* value);
				STDMETHODIMP GetCurrentAddress(ConnectionType connectionType, boolean ipv6, _Out_ HSTRING* value);
				STDMETHODIMP GetCurrentPort(ConnectionType connectionType, boolean ipv6, _Out_ UINT32* value);
				STDMETHODIMP GetDownloadConnection(UINT32 index, boolean create, _Out_ IConnection** value);
				STDMETHODIMP GetUploadConnection(UINT32 index, boolean create, _Out_ IConnection** value);
				STDMETHODIMP GetGenericConnection(boolean create, _Out_ IConnection** value);
				STDMETHODIMP GetPushConnection(boolean create, _Out_ IConnection** value);

			private:
				struct DatacenterEndpoint
				{
					std::wstring Address;
					UINT32 Port;
				};

				HRESULT SwitchTo443Port();
				HRESULT GetCurrentEndpoint(ConnectionType connectionType, boolean ipv6, _Out_ DatacenterEndpoint** endpoint);

				CriticalSection m_criticalSection;
				UINT32 m_id;
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
