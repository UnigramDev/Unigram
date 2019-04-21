#include "pch.h"
#include "Helpers\LibraryHelper.h"
#include "TelegramPaymentProxy.h"

using namespace Unigram::Native;
using namespace Platform;

TelegramPaymentProxy::TelegramPaymentProxy(TelegramPaymentProxyDelegate^ delegate)
{
	_delegate = delegate;
}

void TelegramPaymentProxy::PostEvent(String^ eventName, String^ eventData)
{
	if (eventName->Equals(L"payment_form_submit"))
	{
		try
		{
			auto json = JsonObject::Parse(eventData);
			auto response = json->GetNamedValue("credentials");
			auto title = json->GetNamedString("title", L"");

			_delegate(title, response->Stringify());
		}
		catch (Exception^ ex)
		{
			_delegate(L"", eventData);
		}
	}
}