#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::IProxySettings;
using ABI::Telegram::Api::Native::IProxyCredentials;
using ABI::Telegram::Api::Native::IProxySettingsStatics;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class ProxyCredentials WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IProxyCredentials>
			{
				InspectableClass(RuntimeClass_Telegram_Api_Native_ProxyCredentials, BaseTrust);

			public:
				//COM exported methods
				IFACEMETHODIMP get_UserName(_Out_ HSTRING* value);
				IFACEMETHODIMP get_Password(_Out_ HSTRING* value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(_In_ HSTRING userName, _In_ HSTRING password);

			private:
				HString m_userName;
				HString m_password;
			};

			class ProxySettings WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IProxySettings>
			{
				InspectableClass(RuntimeClass_Telegram_Api_Native_ProxySettings, BaseTrust);

			public:
				ProxySettings();
				~ProxySettings();

				//COM exported methods
				IFACEMETHODIMP get_Host(_Out_ HSTRING* value);
				IFACEMETHODIMP get_Port(_Out_ UINT32* value);
				IFACEMETHODIMP get_Credentials(_Out_ IProxyCredentials** value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(_In_ HSTRING host, UINT32 port, _In_ IProxyCredentials* credentials);

			private:
				HString m_host;
				UINT32 m_port;
				ComPtr<IProxyCredentials> m_credentials;
			};

			class ProxySettingsStatics WrlSealed : public AgileActivationFactory<IProxySettingsStatics>
			{
				InspectableClassStatic(RuntimeClass_Telegram_Api_Native_ProxySettings, BaseTrust);

			public:
				//COM exported methods
				IFACEMETHODIMP Create(_In_ HSTRING host, UINT32 port, _Out_ IProxySettings** value);
				IFACEMETHODIMP CreateWithCredentials(_In_ HSTRING host, UINT32 port, _In_ HSTRING userName, _In_ HSTRING password, _Out_ IProxySettings** value);
			};

		}
	}
}