#pragma once

#include "TelegramWebviewProxy.g.h"

namespace winrt::Telegram::Native::implementation
{
	struct TelegramWebviewProxy : TelegramWebviewProxyT<TelegramWebviewProxy>
	{
	public:
		TelegramWebviewProxy(TelegramWebviewProxyDelegate delegate);

		void PostEvent(hstring eventName, hstring eventData);

	private:
		TelegramWebviewProxyDelegate _delegate;
	};
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
	struct TelegramWebviewProxy : TelegramWebviewProxyT<TelegramWebviewProxy, implementation::TelegramWebviewProxy>
	{
	};
} // namespace winrt::Telegram::Native::factory_implementation
