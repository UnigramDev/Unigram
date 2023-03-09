#pragma once

#include "TelegramGameProxy.g.h"

namespace winrt::Telegram::Native::implementation
{
	struct TelegramGameProxy : TelegramGameProxyT<TelegramGameProxy>
	{
	public:
		TelegramGameProxy(TelegramGameProxyDelegate delegate);

		void PostEvent(hstring eventName, hstring eventData);

	private:
		TelegramGameProxyDelegate _delegate;
	};
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
	struct TelegramGameProxy : TelegramGameProxyT<TelegramGameProxy, implementation::TelegramGameProxy>
	{
	};
} // namespace winrt::Telegram::Native::factory_implementation
