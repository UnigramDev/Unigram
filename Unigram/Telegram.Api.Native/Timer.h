#pragma once
#include <vector>
#include <functional>
#include <wrl.h>
#include "EventObject.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace ABI
{
	namespace Telegram
	{
		namespace Api
		{
			namespace Native
			{

				MIDL_INTERFACE("9980B73C-F2A3-4F89-91A7-EC679F3B018B") ITimer : public IUnknown
				{
				public:
					virtual HRESULT STDMETHODCALLTYPE get_IsStarted(_Out_ boolean* value) = 0;
					virtual HRESULT STDMETHODCALLTYPE SetTimeout(UINT32 msTimeout, boolean repeat) = 0;
					virtual HRESULT STDMETHODCALLTYPE Start() = 0;
					virtual HRESULT STDMETHODCALLTYPE Stop() = 0;
				};

			}
		}
	}
}


using ABI::Telegram::Api::Native::ITimer;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class Timer WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, ITimer>, public EventObjectT<EventTraits::TimerTraits>
			{
				typedef std::function<HRESULT()> TimerCallback;

			public:
				Timer(TimerCallback callback);
				~Timer();

				//COM exported methods		
				STDMETHODIMP get_IsStarted(_Out_ boolean* value);
				STDMETHODIMP SetTimeout(UINT32 msTimeout, boolean repeat);
				STDMETHODIMP Start();
				STDMETHODIMP Stop();

			private:
				HRESULT OnEvent(_In_ PTP_CALLBACK_INSTANCE callbackInstance);
				HRESULT SetTimerTimeout();

				boolean m_started;
				boolean m_repeatable;
				UINT32 m_timeout;
				TimerCallback m_callback;
			};

		}
	}
}