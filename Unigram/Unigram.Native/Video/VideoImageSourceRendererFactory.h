#pragma once
#include "VideoImageSourceRenderer.h"

using Windows::ApplicationModel::Core::CoreApplication;

namespace Unigram
{
	namespace Native
	{
		[WebHostHidden]
		public ref class VideoImageSourceRendererFactory sealed
		{
		public:
			VideoImageSourceRendererFactory();

			VideoImageSourceRenderer^ CreateRenderer(int maximumWidth, int maximumHeight);

		internal:
			event Windows::Foundation::EventHandler<Object^>^ SurfaceContentLost;

			ID2D1Device* GetDevice()
			{
				auto lock = m_criticalSection.Lock();
				return m_d2dDevice.Get();
			}

			ID2D1DeviceContext* GetDeviceContext()
			{
				auto lock = m_criticalSection.Lock();
				return m_d2dDeviceContext.Get();
			}

			HRESULT NotifyDeviceContentLost();

		private:
			~VideoImageSourceRendererFactory();

			void OnSuspending(_In_ Object^ sender, _In_ SuspendingEventArgs^ args);
			void OnSurfaceContentLost(_In_ Object^ sender, _In_ Object^ args);
			HRESULT CreateDeviceResources();

			ComPtr<ID3D11Device> m_d3dDevice;
			ComPtr<ID2D1Device> m_d2dDevice;
			ComPtr<ID2D1DeviceContext> m_d2dDeviceContext;
			CriticalSection m_criticalSection;
			Windows::Foundation::EventRegistrationToken m_eventTokens[2];
		};
	}
}