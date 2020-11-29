#include "pch.h"
#include "TelegramGameProxy.h"

namespace winrt::Unigram::Native::implementation
{
	TelegramGameProxy::TelegramGameProxy(TelegramGameProxyDelegate delegate)
	{
		_delegate = delegate;
	}

	void TelegramGameProxy::PostEvent(hstring eventName, hstring eventData)
	{
		if (eventName == L"share_game")
		{
			_delegate(false);
		}
		else if (eventName == L"share_score")
		{
			_delegate(true);
		}
	}
} // namespace winrt::Unigram::Native::implementation