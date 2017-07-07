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
				//COM exported methods		
				IFACEMETHODIMP get_DeviceModel(_Out_ HSTRING* value);
				IFACEMETHODIMP get_SystemVersion(_Out_ HSTRING* value);
				IFACEMETHODIMP get_AppVersion(_Out_ HSTRING* value);
				IFACEMETHODIMP get_Language(_Out_ HSTRING* value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize();

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

			private:
				static HRESULT FormatVersion(UINT64 major, UINT64 minor, UINT64 build, UINT64 revision, _Out_ HString& version);

				HString m_deviceModel;
				HString m_systemVersion;
				HString m_appVersion;
				HString m_language;
			};

		}
	}
}