#pragma once
#include <string>
#include <Winsock2.h>
#include <rpc.h>
#include <rpcndr.h>

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

				HRESULT OpenSocket(std::wstring address, UINT16 port, boolean ipv6);
				HRESULT CloseSocket();
				HRESULT HandleEvent(_In_ EventObjectEventContext const* context);

				virtual HRESULT OnSocketOpened() = 0;
				virtual HRESULT OnDataReceived() = 0;
				virtual HRESULT OnSocketClosed() = 0;

			private:
				HRESULT CloseSocket(boolean error);

				SOCKET m_socket;
			};

		}
	}
}