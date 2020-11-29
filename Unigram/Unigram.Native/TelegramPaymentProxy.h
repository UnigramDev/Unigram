#pragma once

#include "TelegramPaymentProxy.g.h"

namespace winrt::Unigram::Native::implementation
{
	struct TelegramPaymentProxy : TelegramPaymentProxyT<TelegramPaymentProxy>
	{
	public:
		TelegramPaymentProxy(TelegramPaymentProxyDelegate delegate);

		void PostEvent(hstring eventName, hstring eventData);

	private:
		TelegramPaymentProxyDelegate _delegate;
	};
} // namespace winrt::Unigram::Native::implementation

namespace winrt::Unigram::Native::factory_implementation
{
	struct TelegramPaymentProxy : TelegramPaymentProxyT<TelegramPaymentProxy, implementation::TelegramPaymentProxy>
	{
	};
} // namespace winrt::Unigram::Native::factory_implementation
