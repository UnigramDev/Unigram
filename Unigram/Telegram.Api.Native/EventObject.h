#pragma once
#include <wrl.h>
#include "Helpers\COMHelper.h"

using namespace Microsoft::WRL;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace EventTraits
			{

				struct TimerTraits;
				struct WaitTraits;
				struct WorkerTraits;

			}


			class EventObject abstract 
			{
				friend class ConnectionManager;
				friend struct EventTraits::TimerTraits;
				friend struct EventTraits::WaitTraits;
				friend struct EventTraits::WorkerTraits;

			protected:
				virtual HRESULT AttachToThreadpool(_In_ PTP_CALLBACK_ENVIRON threadpoolEnvironment) = 0;
				virtual HRESULT DetachFromThreadpool() = 0;
				virtual HRESULT OnEvent(_In_ PTP_CALLBACK_INSTANCE callbackInstance) = 0;

			private:
				void OnThreadpoolCallback(_In_ PTP_CALLBACK_INSTANCE callbackInstance);
			};


			namespace EventTraits
			{

				struct TimerTraits
				{
				public:
					typedef PTP_TIMER Handle;

					inline static Handle Create(_In_ EventObject* eventObject, _In_ PTP_CALLBACK_ENVIRON threadpoolEnvironment) throw()
					{
						return ::CreateThreadpoolTimer(TimerTraits::Callback, eventObject, threadpoolEnvironment);
					}

					inline static void Wait(_In_ Handle timer, BOOL cancelPendingCallbacks) throw()
					{
						::WaitForThreadpoolTimerCallbacks(timer, cancelPendingCallbacks);
					}

					inline static void Close(_In_ Handle timer) throw()
					{
						::CloseThreadpoolTimer(timer);
					}

					inline static void Reset(_In_ Handle timer) throw()
					{
						::SetThreadpoolTimer(timer, nullptr, 0, 0);
					}

				private:
					static void NTAPI Callback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ Handle work)
					{
						reinterpret_cast<EventObject*>(context)->OnThreadpoolCallback(instance);
					}
				};

				struct WaitTraits
				{
				public:
					typedef PTP_WAIT Handle;

					inline static Handle Create(_In_ EventObject* eventObject, _In_ PTP_CALLBACK_ENVIRON threadpoolEnvironment) throw()
					{
						return ::CreateThreadpoolWait(WaitTraits::Callback, eventObject, threadpoolEnvironment);
					}

					inline static void Wait(_In_ Handle wait, BOOL cancelPendingCallbacks) throw()
					{
						::WaitForThreadpoolWaitCallbacks(wait, cancelPendingCallbacks);
					}

					inline static void Close(_In_ Handle wait) throw()
					{
						::CloseThreadpoolWait(wait);
					}

					inline static void Reset(_In_ Handle wait) throw()
					{
						::SetThreadpoolWait(wait, nullptr, nullptr);
					}

				private:
					static void NTAPI Callback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ Handle wait, _In_ TP_WAIT_RESULT waitResult)
					{
						if (waitResult == WAIT_OBJECT_0)
						{
							reinterpret_cast<EventObject*>(context)->OnThreadpoolCallback(instance);
						}
					}
				};

				struct WorkerTraits
				{
				public:
					typedef PTP_WORK Handle;

					inline static Handle Create(_In_ EventObject* eventObject, _In_ PTP_CALLBACK_ENVIRON threadpoolEnvironment) throw()
					{
						return ::CreateThreadpoolWork(WorkerTraits::Callback, eventObject, threadpoolEnvironment);
					}

					inline static void Wait(_In_ Handle work, BOOL cancelPendingCallbacks) throw()
					{
						::WaitForThreadpoolWorkCallbacks(work, cancelPendingCallbacks);
					}

					inline static void Close(_In_ Handle work) throw()
					{
						::CloseThreadpoolWork(work);
					}

					inline static void Reset(_In_ Handle work) throw()
					{
						UNREFERENCED_PARAMETER(work);
					}

				private:
					static void NTAPI Callback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ Handle work)
					{
						reinterpret_cast<EventObject*>(context)->OnThreadpoolCallback(instance);
					}
				};

			}


			template<typename EventTraits>
			class EventObjectT abstract : public EventObject
			{
				friend class ConnectionManager;

			public:
				EventObjectT() :
					m_handle(nullptr)
				{
				}

				~EventObjectT()
				{
					DetachFromThreadpool();
				}

			protected:
				inline typename EventTraits::Handle GetThreadpoolObjectHandle() const
				{
					return m_handle;
				}

				inline HRESULT WaitForThreadpoolCallback(boolean cancelPendingCallbacks)
				{
					if (m_handle == nullptr)
					{
						return E_NOT_VALID_STATE;
					}

					EventTraits::Wait(m_handle, cancelPendingCallbacks);
					return S_OK;
				}

				inline HRESULT ResetThreadpoolObject()
				{
					if (m_handle == nullptr)
					{
						return E_NOT_VALID_STATE;
					}

					EventTraits::Wait(m_handle, true);
					EventTraits::Reset(m_handle);
					return S_OK;
				}

				virtual HRESULT AttachToThreadpool(_In_ PTP_CALLBACK_ENVIRON threadpoolEnvironment) override final
				{
					if (threadpoolEnvironment == nullptr)
					{
						return E_POINTER;
					}

					if (m_handle != nullptr)
					{
						return E_NOT_VALID_STATE;
					}

					m_handle = EventTraits::Create(this, threadpoolEnvironment);
					if (m_handle == nullptr)
					{
						return GetLastHRESULT();
					}

					return S_OK;
				}

				virtual HRESULT DetachFromThreadpool() override final
				{
					if (m_handle == nullptr)
					{
						return E_NOT_VALID_STATE;
					}

					EventTraits::Wait(m_handle, TRUE);
					EventTraits::Close(m_handle);

					m_handle = nullptr;
					return S_OK;
				}

			private:
				typename EventTraits::Handle m_handle;
			};

		}
	}
}