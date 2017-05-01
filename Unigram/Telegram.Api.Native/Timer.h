#pragma once
#include <vector>
#include <functional>
#include <wrl.h>
#include "EventObject.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			MIDL_INTERFACE("9980B73C-F2A3-4F89-91A7-EC679F3B018B") ITimer : public IUnknown
			{
			public:
				virtual HRESULT STDMETHODCALLTYPE SetTimeout(UINT32 msTimeout, boolean repeat) = 0;
				virtual HRESULT STDMETHODCALLTYPE Start() = 0;
				virtual HRESULT STDMETHODCALLTYPE Stop() = 0;
			};

			class Timer WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, ITimer, EventObjectT<EventTraits::EventTraits>>
			{
				typedef std::function<HRESULT()> TimerCallback;

			public:
				Timer();
				~Timer();

				STDMETHODIMP RuntimeClassInitialize(TimerCallback callback);
				STDMETHODIMP SetTimeout(UINT32 msTimeout, boolean repeat);
				STDMETHODIMP Start();
				STDMETHODIMP Stop();

			private:
				STDMETHODIMP OnEvent(_In_ EventObjectEventContext const* context);
				HRESULT SetTimerTimeout();

				CriticalSection m_criticalSection;
				Event m_waitableTimer;
				boolean m_started;
				boolean m_repeatable;
				UINT32 m_timeout;
				TimerCallback m_callback;
			};

		}
	}
}
