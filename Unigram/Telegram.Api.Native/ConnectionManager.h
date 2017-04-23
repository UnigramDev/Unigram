#pragma once
#include <queue>
#include <functional>
#include <map>
#include <atomic>
#include <wrl.h>
#include "IEventObject.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			public enum class ConnectionState
			{
				NotInitialized = 0,
				Connecting = 1,
				WaitingForNetwork = 2,
				Connected = 3
			};


			public ref class ConnectionManager sealed
			{
				friend ref class Connection;

			public:
				static property ConnectionManager^ Instance
				{
					ConnectionManager^ get();
				}

				property Telegram::Api::Native::ConnectionState ConnectionState
				{
					Telegram::Api::Native::ConnectionState get();
				}

				property bool IsNetworkAvailable
				{
					bool get();
				}

			internal:
				void ScheduleEvent(_In_ IEventObject^ eventObject, uint32 timeout);
				void RemoveEvent(_In_ IEventObject^ eventObject);

			private:
				ConnectionManager();
				~ConnectionManager();
				
				CriticalSection m_criticalSection;
				Telegram::Api::Native::ConnectionState m_connectionState;
			
				static ConnectionManager^ s_instance;
			};

		}
	}
}