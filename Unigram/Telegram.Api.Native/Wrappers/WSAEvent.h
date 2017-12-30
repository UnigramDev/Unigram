#pragma once
#include <Windows.h>
#include <Winsock2.h>
#include <wrl\wrappers\corewrappers.h>

namespace Microsoft
{
	namespace WRL
	{
		namespace Wrappers
		{
			namespace HandleTraits
			{

				struct WSAEVENTTraits
				{
					typedef HANDLE Type;

					inline static bool Close(_In_ Type h) throw()
					{
						return ::WSACloseEvent(h) != FALSE;
					}

					inline static Type GetInvalidValue() throw()
					{
						return INVALID_HANDLE_VALUE;
					}
				};

			}

			class WSAEvent : public HandleT<HandleTraits::WSAEVENTTraits>
			{
			public:
				explicit WSAEvent(HANDLE h = HandleT::Traits::GetInvalidValue()) throw() : HandleT(h)
				{
				}

				WSAEvent(_Inout_ WSAEvent&& h) throw() : HandleT(::Microsoft::WRL::Details::Move(h))
				{
				}

				WSAEvent& operator=(_Inout_ WSAEvent&& h) throw()
				{
					*static_cast<HandleT*>(this) = ::Microsoft::WRL::Details::Move(h);
					return *this;
				}
			};

		}
	}
}
