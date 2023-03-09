#pragma once

#include "TelegramPaymentProxy.g.h"

namespace winrt::Telegram::Native::implementation
{
	struct TelegramPaymentProxy : TelegramPaymentProxyT<TelegramPaymentProxy>
	{
	public:
		TelegramPaymentProxy(TelegramPaymentProxyDelegate delegate);

		void PostEvent(hstring eventName, hstring eventData);

	private:
		TelegramPaymentProxyDelegate _delegate;
	};
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
	struct TelegramPaymentProxy : TelegramPaymentProxyT<TelegramPaymentProxy, implementation::TelegramPaymentProxy>
	{
	};
} // namespace winrt::Telegram::Native::factory_implementation
