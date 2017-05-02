#pragma once
#include <string>
#include <Winsock2.h>
#include "WSAEvent.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			struct EventObjectEventContext;

			class ConnectionSocket abstract
			{
			public:
				ConnectionSocket();
				~ConnectionSocket();

			protected:
				inline SOCKET GetSocket() const
				{
					return m_socket;
				}

				inline HANDLE GetSocketEvent() const
				{
					return m_socketEvent.Get();
				}

				HRESULT OpenSocket(std::wstring address, UINT16 port, boolean ipv6);
				HRESULT CloseSocket();
				HRESULT OnEvent(_In_ PTP_CALLBACK_INSTANCE callbackInstance);

				virtual HRESULT OnSocketCreated() = 0;
				virtual HRESULT OnDataReceived() = 0;
				virtual HRESULT OnSocketClosed(int wsaError) = 0;

			private:
				HRESULT CloseSocket(int wsaError);

				SOCKET m_socket;
				WSAEvent m_socketEvent;
			};

		}
	}
}