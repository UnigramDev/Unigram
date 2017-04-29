#pragma once
#include <WinSock2.h>
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

				struct EventTraits
				{
					inline static HANDLE Create() throw()
					{
						return ::CreateEvent(nullptr, TRUE, FALSE, nullptr);
					}

					inline static bool Reset(_In_ HANDLE h) throw()
					{
						return ::ResetEvent(h) != FALSE;
					}

					inline static bool Close(_In_ HANDLE h) throw()
					{
						return ::CloseHandle(h) != FALSE;
					}

					inline static HANDLE GetInvalidValue() throw()
					{
						return nullptr;
					}

					inline static HRESULT GetLastHRESULT() throw()
					{
						return ::GetLastHRESULT();
					}
				};

				struct WSAEventTraits
				{
					inline static HANDLE Create() throw()
					{
						return ::WSACreateEvent();
					}

					inline static bool Reset(_In_ HANDLE h) throw()
					{
						return ::WSAResetEvent(h) != FALSE;
					}

					inline static bool Close(_In_ HANDLE h) throw()
					{
						return ::WSACloseEvent(h) != FALSE;
					}

					inline static HANDLE GetInvalidValue() throw()
					{
						return WSA_INVALID_EVENT;
					}

					inline static HRESULT GetLastHRESULT() throw()
					{
						return ::GetWSALastHRESULT();
					}
				};

			}


			struct EventObjectEventContext
			{
				OVERLAPPED Overlapped;
				DWORD Operation;
				LPVOID UserData;
			};


			MIDL_INTERFACE("5D8D896C-235D-47CD-8643-A776A17AB2F0") IEventObject : public IUnknown
			{
			public:
				virtual HRESULT STDMETHODCALLTYPE CreateEventContext(DWORD operation, _In_opt_ LPVOID userData, _In_opt_ LPOVERLAPPED overlapped, _Out_ EventObjectEventContext** context) = 0;
				virtual HRESULT STDMETHODCALLTYPE FreeEventContext(_In_ EventObjectEventContext* context) = 0;
				virtual HRESULT STDMETHODCALLTYPE OnEvent(_In_ EventObjectEventContext const* context) = 0;
			};

			class EventObject abstract : public Implements<RuntimeClassFlags<ClassicCom>, IEventObject>
			{
			private:
				virtual STDMETHODIMP CreateEventContext(DWORD operation, _In_opt_ LPVOID userData, _In_opt_ LPOVERLAPPED overlapped, _Out_ EventObjectEventContext** context) = 0;
				virtual STDMETHODIMP FreeEventContext(_In_ EventObjectEventContext* context) = 0;
			};

			template<typename EventTraits>
			class EventObjectT abstract : public EventObject
			{
				friend class ConnectionManager;

			private:
				virtual STDMETHODIMP CreateEventContext(DWORD operation, _In_opt_ LPVOID userData, _In_opt_ LPOVERLAPPED overlapped, _Out_ EventObjectEventContext** pContext) final
				{
					if (pContext == nullptr)
					{
						return E_POINTER;
					}

					auto context = reinterpret_cast<EventObjectEventContext*>(GlobalAlloc(GMEM_FIXED | GMEM_ZEROINIT, sizeof(EventObjectEventContext)));
					if (context == nullptr)
					{
						return GetLastHRESULT();
					}

					if (overlapped != nullptr)
					{
						CopyMemory(&context->Overlapped, overlapped, sizeof(OVERLAPPED));
					}

					if (context->Overlapped.hEvent == nullptr && (context->Overlapped.hEvent = EventTraits::Create()) == EventTraits::GetInvalidValue())
					{
						HRESULT result = EventTraits::GetLastHRESULT();
						GlobalFree(context);

						return result;
					}

					context->Operation = operation;
					context->UserData = userData;

					*pContext = context;
					return S_OK;
				}

				virtual STDMETHODIMP FreeEventContext(_In_ EventObjectEventContext* context) final
				{
					if (context == nullptr)
					{
						return E_POINTER;
					}

					if (!EventTraits::Close(context->Overlapped.hEvent))
					{
						return EventTraits::GetLastHRESULT();
					}

					GlobalFree(context);
					return S_OK;
				}
			};

		}
	}
}