#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"

#define DEBUG_CRITICAL_SECTION 0

#if DEBUG_CRITICAL_SECTION
#include "DebugCriticalSection.h"
#endif

using namespace Microsoft::WRL::Wrappers;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class MultiThreadObject
			{
			public:
#if DEBUG_CRITICAL_SECTION
				typedef Microsoft::WRL::Wrappers::DebugCriticalSection CriticalSection;
#else
				typedef Microsoft::WRL::Wrappers::CriticalSection CriticalSection;
#endif

			protected:
				inline CriticalSection::SyncLock LockCriticalSection()
				{
					return m_criticalSection.Lock();
				}

				inline CriticalSection::SyncLock TryLockCriticalSection()
				{
					return m_criticalSection.TryLock();
				}

				inline CriticalSection* GetCriticalSection()
				{
					return &m_criticalSection;
				}

			private:
				CriticalSection m_criticalSection;
			};

		}
	}
}