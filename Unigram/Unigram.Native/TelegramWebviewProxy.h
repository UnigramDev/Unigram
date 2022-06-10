#pragma once

#include "TelegramWebviewProxy.g.h"

namespace winrt::Unigram::Native::implementation
{
	struct TelegramWebviewProxy : TelegramWebviewProxyT<TelegramWebviewProxy>
	{
	public:
		TelegramWebviewProxy(TelegramWebviewProxyDelegate delegate);

		void PostEvent(hstring eventName, hstring eventData);

	private:
		TelegramWebviewProxyDelegate _delegate;
	};
} // namespace winrt::Unigram::Native::implementation

namespace winrt::Unigram::Native::factory_implementation
{
	struct TelegramWebviewProxy : TelegramWebviewProxyT<TelegramWebviewProxy, implementation::TelegramWebviewProxy>
	{
	};
} // namespace winrt::Unigram::Native::factory_implementation
