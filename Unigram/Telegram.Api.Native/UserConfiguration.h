#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::IUserConfiguration;


namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class UserConfiguration WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IUserConfiguration>
			{
				InspectableClass(RuntimeClass_Telegram_Api_Native_UserConfiguration, BaseTrust);

			public:
				UserConfiguration();
				~UserConfiguration();

				//COM exported methods		
				IFACEMETHODIMP get_AppId(_Out_ INT32* value);
				IFACEMETHODIMP put_AppId(INT32 value);
				IFACEMETHODIMP get_DeviceModel(_Out_ HSTRING* value);
				IFACEMETHODIMP put_DeviceModel(_In_ HSTRING value);
				IFACEMETHODIMP get_SystemVersion(_Out_ HSTRING* value);
				IFACEMETHODIMP put_SystemVersion(_In_ HSTRING value);
				IFACEMETHODIMP get_AppVersion(_Out_ HSTRING* value);
				IFACEMETHODIMP put_AppVersion(_In_ HSTRING value);
				IFACEMETHODIMP get_Language(_Out_ HSTRING* value);
				IFACEMETHODIMP put_Language(_In_ HSTRING value);
				IFACEMETHODIMP get_LangPack(_Out_ HSTRING* value);
				IFACEMETHODIMP put_LangPack(_In_ HSTRING value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize();

				inline INT32 GetAppId() const
				{
					return m_appId;
				}

				inline HString const& GetDeviceModel() const
				{
					return m_deviceModel;
				}

				inline HString const& GetSystemVersion() const
				{
					return m_systemVersion;
				}

				inline HString const& GetAppVersion() const
				{
					return m_appVersion;
				}

				inline HString const& GetLanguage() const
				{
					return m_language;
				}

				inline HString const& GetLangPack() const
				{
					return m_langPack;
				}

			private:
				static HRESULT FormatVersion(UINT64 major, UINT64 minor, UINT64 build, UINT64 revision, _Out_ HString& version);

				INT32 m_appId;
				HString m_deviceModel;
				HString m_systemVersion;
				HString m_appVersion;
				HString m_language;
				HString m_langPack;
			};

		}
	}
}