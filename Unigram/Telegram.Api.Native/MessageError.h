#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using ABI::Telegram::Api::Native::IMessageError;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class MessageError WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, IMessageError>
			{
				InspectableClass(RuntimeClass_Telegram_Api_Native_MessageError, BaseTrust);

			public:
				MessageError(INT32 code, _In_ HString&& text);
				MessageError();
				~MessageError();

				//COM exported methods
				IFACEMETHODIMP get_Code(_Out_ INT32* value);
				IFACEMETHODIMP get_Text(_Out_ HSTRING* value);
				IFACEMETHODIMP get_Exception(_Out_ HRESULT* value);

				//Internal methods
				STDMETHODIMP RuntimeClassInitialize(INT32 code, _In_ HSTRING text);
				STDMETHODIMP RuntimeClassInitialize(HRESULT error);

				template<size_t sizeDest>
				STDMETHODIMP RuntimeClassInitialize(INT32 code, _In_ const WCHAR(&text)[sizeDest])
				{
					m_code = code;
					return m_text.Set<sizeDest>(text);
				}

				inline INT32 GetCode() const
				{
					return m_code;
				}

				inline HString const& GetText() const
				{
					return m_text;
				}

			private:
				INT32 m_code;
				HString m_text;
			};

		}
	}
}