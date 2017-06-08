#pragma once
#include "ThreadpoolManager.h"
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


			class EventObject abstract : public virtual MultiThreadObject
			{
				friend class ThreadpoolManager;
				template<typename EventTraits>
				friend class EventObjectT;
				friend struct EventTraits::TimerTraits;
				friend struct EventTraits::WaitTraits;
				friend struct EventTraits::WorkerTraits;

			protected:
				virtual HRESULT AttachToThreadpool(_In_ ThreadpoolManager* threadpoolManager) = 0;
				virtual HRESULT DetachFromThreadpool(boolean waitCallback) = 0;
				virtual HRESULT OnEvent(_In_ PTP_CALLBACK_INSTANCE callbackInstance, _In_ ULONG_PTR param) = 0;
			};


			namespace EventTraits
			{

				struct TimerTraits
				{
				public:
					typedef PTP_TIMER Handle;

					inline static Handle Create(_In_ EventObject* eventObject, _In_ PTP_CALLBACK_ENVIRON threadpoolEnvironment) throw()
					{
						return ::CreateThreadpoolTimer(TimerTraits::EventCallback, eventObject, threadpoolEnvironment);
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
					static void NTAPI EventCallback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ Handle work)
					{
						reinterpret_cast<EventObject*>(context)->OnEvent(instance, NULL);
					}
				};

				struct WaitTraits
				{
				public:
					typedef PTP_WAIT Handle;

					inline static Handle Create(_In_ EventObject* eventObject, _In_ PTP_CALLBACK_ENVIRON threadpoolEnvironment) throw()
					{
						return ::CreateThreadpoolWait(WaitTraits::EventCallback, eventObject, threadpoolEnvironment);
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
					static void NTAPI EventCallback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ Handle wait, _In_ TP_WAIT_RESULT waitResult)
					{
						reinterpret_cast<EventObject*>(context)->OnEvent(instance, waitResult);
					}
				};

				struct WorkerTraits
				{
				public:
					typedef PTP_WORK Handle;

					inline static Handle Create(_In_ EventObject* eventObject, _In_ PTP_CALLBACK_ENVIRON threadpoolEnvironment) throw()
					{
						return ::CreateThreadpoolWork(WorkerTraits::EventCallback, eventObject, threadpoolEnvironment);
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
					static void NTAPI EventCallback(_Inout_ PTP_CALLBACK_INSTANCE instance, _Inout_opt_ PVOID context, _Inout_ Handle work)
					{
						reinterpret_cast<EventObject*>(context)->OnEvent(instance, NULL);
					}
				};

			}


			template<typename EventTraits>
			class EventObjectT abstract : public EventObject
			{
				friend class ThreadpoolManager;

			public:
				EventObjectT() :
					m_handle(nullptr)
				{
				}

				~EventObjectT()
				{
					DetachFromThreadpool(true);
				}

			protected:
				inline typename EventTraits::Handle GetHandle() const
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

				inline HRESULT ResetThreadpoolObject(boolean waitCallback)
				{
					if (m_handle == nullptr)
					{
						return E_NOT_VALID_STATE;
					}

					if (waitCallback)
					{
						EventTraits::Wait(m_handle, TRUE);
					}

					EventTraits::Reset(m_handle);
					return S_OK;
				}

				virtual HRESULT AttachToThreadpool(_In_ ThreadpoolManager* threadpoolManager) override final
				{
					if (threadpoolManager == nullptr)
					{
						return E_INVALIDARG;
					}

					if (m_handle != nullptr)
					{
						return E_NOT_VALID_STATE;
					}

					m_handle = EventTraits::Create(this, threadpoolManager->GetEnvironment());
					if (m_handle == nullptr)
					{
						return GetLastHRESULT();
					}

					return S_OK;
				}

				virtual HRESULT DetachFromThreadpool(boolean waitCallback) override final
				{
					if (m_handle == nullptr)
					{
						return E_NOT_VALID_STATE;
					}

					if (waitCallback)
					{
						EventTraits::Wait(m_handle, TRUE);
					}

					EventTraits::Close(m_handle);

					m_handle = nullptr;
					return S_OK;
				}

			private:
				typename EventTraits::Handle m_handle;
			};


			inline void TimeoutToFileTime(UINT32 timeoutMs, _Out_ FILETIME& filetime)
			{
				ULARGE_INTEGER timeout;
				timeout.QuadPart = timeoutMs * -10000LL;

				filetime.dwHighDateTime = timeout.HighPart;
				filetime.dwLowDateTime = timeout.LowPart;
			}

		}
	}
}