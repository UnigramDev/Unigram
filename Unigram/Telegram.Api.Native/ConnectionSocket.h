#pragma once
#include <vector>
#include <memory>
#include <string>
#include <Winsock2.h>
#include <wrl.h>
#include "Wrappers\WSAEvent.h"
#include "ThreadpoolObject.h"

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
				void SetTimeout(UINT32 timeout);
				HRESULT Close();		
				HRESULT ConnectSocket(_In_ ConnectionManager* connectionManager, _In_ ServerEndpoint const* endpoint, boolean ipv6);
				HRESULT DisconnectSocket(boolean immediate);
				HRESULT SendData(_In_reads_(length) BYTE const* buffer, UINT32 length);

				virtual HRESULT OnSocketConnected() = 0;
				virtual HRESULT OnDataReceived(_In_reads_(length) BYTE const* buffer, UINT32 length) = 0;
				virtual HRESULT OnSocketDisconnected(int wsaError) = 0;

			private:
				virtual HRESULT OnEvent(_In_ PTP_CALLBACK_INSTANCE callbackInstance, ULONG_PTR waitResult) override;
				HRESULT CloseSocket(int wsaError, BYTE flags);
				HRESULT GetLastErrorAndCloseSocket(BYTE flags);

				SOCKET m_socket;
				WSAEvent m_socketEvent;
				//Event m_socketConnectedEvent;
				FILETIME m_timeout;
				std::vector<BYTE> m_sendBuffer;
				std::unique_ptr<BYTE[]> m_receiveBuffer;
			};

		}
	}
}