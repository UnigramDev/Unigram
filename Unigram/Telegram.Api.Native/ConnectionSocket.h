#pragma once
#include <vector>
#include <memory>
#include <string>
#include <Winsock2.h>
#include <wrl.h>
#include "Wrappers\WSAEvent.h"
#include "EventObject.h"

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			struct ServerEndpoint;
			class ConnectionManager;

			class ConnectionSocket abstract : virtual EventObjectT<EventTraits::WaitTraits>
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

				HRESULT ConnectSocket(_In_ ConnectionManager* connectionManager, _In_ ServerEndpoint const* endpoint, boolean ipv6);
				HRESULT DisconnectSocket();
				HRESULT SendData(_In_reads_(length) BYTE const* buffer, UINT32 length);
				HRESULT OnEvent(_In_ PTP_CALLBACK_INSTANCE callbackInstance);

				virtual HRESULT OnSocketConnected() = 0;
				virtual HRESULT OnDataReceived(_In_reads_(length) BYTE const* buffer, UINT32 length) = 0;
				virtual HRESULT OnSocketDisconnected(int wsaError) = 0;

			private:
				HRESULT CloseSocket(int wsaError, BYTE flags);
				HRESULT GetLastErrorAndCloseSocket(BYTE flags);

				SOCKET m_socket;
				WSAEvent m_socketEvent;
				Event m_socketConnectedEvent;
				std::vector<BYTE> m_sendBuffer;
				std::unique_ptr<BYTE[]> m_receiveBuffer;
			};

		}
	}
}