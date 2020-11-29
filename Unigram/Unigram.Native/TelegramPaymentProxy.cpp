#include "pch.h"
#include "TelegramPaymentProxy.h"

#include <winrt/Windows.Data.Json.h>

using namespace winrt::Windows::Data::Json;

namespace winrt::Unigram::Native::implementation
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
} // namespace winrt::Unigram::Native::implementation