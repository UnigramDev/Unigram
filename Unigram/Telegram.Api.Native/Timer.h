#pragma once
#include <functional>
#include "IEventObject.h"

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			ref class Timer sealed : IEventObject
			{
			public:
				property bool Started
				{
					bool get();
				}

				property bool Repeatable
				{
					bool get();
				}

				property uint32 Timeout
				{
					uint32 get();
				}

				void Start();
				void Stop();
				void SetTimeout(uint32 timeout, bool repeat);

			internal:
				Timer(std::function<void()> callback);

			private:
				~Timer();

				virtual void OnEvent(uint32 events) sealed = IEventObject::OnEvent;

				bool m_started;
				bool m_repeatable;
				uint32 m_timeout;
				CriticalSection m_criticalSection;
				std::function<void()> m_callback;
			};

		}
	}
}