#include "pch.h"
#include "TelegramPaymentProxy.h"
#if __has_include("TelegramPaymentProxy.g.cpp")
#include "TelegramPaymentProxy.g.cpp"
#endif

#include <winrt/Windows.Data.Json.h>

using namespace winrt::Windows::Data::Json;

namespace winrt::Telegram::Native::implementation
{
	TelegramPaymentProxy::TelegramPaymentProxy(TelegramPaymentProxyDelegate delegate)
	{
		_delegate = delegate;
	}

	void TelegramPaymentProxy::PostEvent(hstring eventName, hstring eventData)
	{
		if (eventName == L"payment_form_submit")
		{
			try
			{
				auto json = JsonObject::Parse(eventData);
				auto response = json.GetNamedValue(L"credentials");
				auto title = json.GetNamedString(L"title", L"");

				_delegate(title, response.Stringify());
			}
			catch (winrt::hresult_error const& ex)
			{
				_delegate(L"", eventData);
			}
		}
	}
} // namespace winrt::Telegram::Native::implementation