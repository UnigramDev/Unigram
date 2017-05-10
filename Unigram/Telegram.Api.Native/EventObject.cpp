#include "pch.h"
#include "EventObject.h"
#include "ConnectionManager.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


void EventObject::OnThreadpoolCallback(PTP_CALLBACK_INSTANCE callbackInstance)
{
	HRESULT result;
	if (FAILED(result = OnEvent(callbackInstance)))
	{
		ComPtr<ConnectionManager> connectionManager;
		if (SUCCEEDED(ConnectionManager::GetInstance(connectionManager)))
		{
			connectionManager->OnEventObjectError(this, result);
		}
	}
}