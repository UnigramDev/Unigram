#pragma once
#include <vector>
#include <functional>
#include <wrl.h>
#include "ThreadpoolObject.h"

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
			public:
				typedef std::function<HRESULT()> TimerCallback;

				Timer(TimerCallback callback);
				~Timer();

				//COM exported methods		
				IFACEMETHODIMP get_IsStarted(_Out_ boolean* value);
				IFACEMETHODIMP SetTimeout(UINT32 msTimeout, boolean repeat);
				IFACEMETHODIMP Start();
				IFACEMETHODIMP Stop();

			private:
				virtual HRESULT OnEvent(_In_ PTP_CALLBACK_INSTANCE callbackInstance, _In_ ULONG_PTR param) override;
				HRESULT SetTimerTimeout();

				boolean m_started;
				boolean m_repeatable;
				UINT32 m_timeout;
				TimerCallback m_callback;
			};

		}
	}
}