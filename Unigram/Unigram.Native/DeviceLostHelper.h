#pragma once

using namespace Windows::Graphics::DirectX::Direct3D11;

namespace Unigram
{
	namespace Native 
	{
		ref class DeviceLostHelper;

		public ref class DeviceLostEventArgs sealed
		{
		public:
			property IDirect3DDevice^ Device
			{
				IDirect3DDevice^ get() { return m_device; }
			}

		internal:
			static DeviceLostEventArgs^ Create(IDirect3DDevice^ device) { return ref new DeviceLostEventArgs(device); }
		private:
			DeviceLostEventArgs(IDirect3DDevice^ device) : m_device(device) {}

			IDirect3DDevice^ m_device;
		};

		public delegate void DeviceLostEventHandler(DeviceLostHelper^ sender, DeviceLostEventArgs^ args);

		public ref class DeviceLostHelper sealed
		{
		public:
			property IDirect3DDevice^ CurrentlyWatchedDevice
			{
				IDirect3DDevice^ get() { return m_device; }
			}

			DeviceLostHelper();

			void WatchDevice(IDirect3DDevice^ device);
			void StopWatchingCurrentDevice();

			event DeviceLostEventHandler^ DeviceLost;

		private:
			~DeviceLostHelper();
			void RaiseDeviceLostEvent(IDirect3DDevice^ oldDevice);

			static void CALLBACK OnDeviceLost(PTP_CALLBACK_INSTANCE instance, PVOID context, PTP_WAIT wait, TP_WAIT_RESULT waitResult);

		private:
			IDirect3DDevice^ m_device;

			PTP_WAIT OnDeviceLostHandler;
			HANDLE m_eventHandle;
			DWORD m_cookie;
		};
	}
};