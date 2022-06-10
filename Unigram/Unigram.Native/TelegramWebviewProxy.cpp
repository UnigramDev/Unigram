#include "pch.h"
#include "TelegramWebviewProxy.h"
#if __has_include("TelegramWebviewProxy.g.cpp")
#include "TelegramWebviewProxy.g.cpp"
#endif

#include <winrt/Windows.Data.Json.h>

using namespace winrt::Windows::Data::Json;

namespace winrt::Unigram::Native::implementation
{
	TelegramWebviewProxy::TelegramWebviewProxy(TelegramWebviewProxyDelegate delegate)
	{
		_delegate = delegate;
	}

	void TelegramWebviewProxy::PostEvent(hstring eventName, hstring eventData)
	{
		_delegate(eventName, eventData);
		return;

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