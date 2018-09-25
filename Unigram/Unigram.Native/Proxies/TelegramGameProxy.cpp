#include "pch.h"
#include "Helpers\LibraryHelper.h"
#include "TelegramGameProxy.h"

using namespace Unigram::Native;
using namespace Platform;

TelegramGameProxy::TelegramGameProxy(TelegramGameProxyDelegate^ delegate)
{
	_delegate = delegate;
}

void TelegramGameProxy::PostEvent(String^ eventName, String^ eventData)
{
	if (eventName->Equals(L"share_game"))
	{
		_delegate(false);
	}
	else if (eventName->Equals("share_score"))
	{
		_delegate(true);
	}
}