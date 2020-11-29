#pragma once

#include "TelegramGameProxy.g.h"

namespace winrt::Unigram::Native::implementation
{
	struct TelegramGameProxy : TelegramGameProxyT<TelegramGameProxy>
	{
	public:
		TelegramGameProxy(TelegramGameProxyDelegate delegate);

		void PostEvent(hstring eventName, hstring eventData);

	private:
		TelegramGameProxyDelegate _delegate;
	};
} // namespace winrt::Unigram::Native::implementation

namespace winrt::Unigram::Native::factory_implementation
{
	struct TelegramGameProxy : TelegramGameProxyT<TelegramGameProxy, implementation::TelegramGameProxy>
	{
	};
} // namespace winrt::Unigram::Native::factory_implementation
