#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::IUserConfiguration;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{

				MIDL_INTERFACE("7A370BE4-415B-4471-8940-09A01F2F0E88") IDefaultUserConfiguration : public IUnknown
				{
				};

			}
		}
	}
}


using ABI::Telegram::Api::Native::IDefaultUserConfiguration;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class DefaultUserConfiguration WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, CloakedIid<IDefaultUserConfiguration>, IUserConfiguration>
			{
				InspectableClass(L"Telegram.Api.Native.DefaultUserConfiguration", BaseTrust);

			public:
				//COM exported methods		
				IFACEMETHODIMP get_DeviceModel(_Out_ HSTRING* value);
				IFACEMETHODIMP get_SystemVersion(_Out_ HSTRING* value);
				IFACEMETHODIMP get_AppVersion(_Out_ HSTRING* value);
				IFACEMETHODIMP get_Language(_Out_ HSTRING* value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize();

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