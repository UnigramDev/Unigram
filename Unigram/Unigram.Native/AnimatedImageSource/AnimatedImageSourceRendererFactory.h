// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include "AnimatedImageSourceRenderer.h"

using Windows::ApplicationModel::Core::CoreApplication;

namespace Unigram
{
	namespace Native
	{

		[WebHostHidden]
		public ref class AnimatedImageSourceRendererFactory sealed
		{
		public:
			AnimatedImageSourceRendererFactory();

			AnimatedImageSourceRenderer^ CreateRenderer(int maximumWidth, int maximumHeight);

		internal:
			event Windows::Foundation::EventHandler<Object^>^ SurfaceContentLost;

			inline ID3D11Device* GetD3DDevice()
			{
				auto lock = m_criticalSection.Lock();
				return m_d3dDevice.Get();
			}

			inline ID3D11DeviceContext* GetD3DDeviceContext()
			{
				auto lock = m_criticalSection.Lock();
				return m_d3dDeviceContext.Get();
			}

			inline ID2D1Device* GetD2DDevice()
			{
				auto lock = m_criticalSection.Lock();
				return m_d2dDevice.Get();
			}

			inline ID2D1DeviceContext* GetD2DDeviceContext()
			{
				auto lock = m_criticalSection.Lock();
				return m_d2dDeviceContext.Get();
			}

			HRESULT NotifyDeviceContentLost();
			HRESULT DrawFrame(_In_ IVirtualSurfaceImageSourceNative* imageSourceNative, _In_ RECT const& drawingBounds,
				_In_ ID2D1Bitmap* frameBitmap);

		private:
			~AnimatedImageSourceRendererFactory();

			void OnSuspending(_In_ Object^ sender, _In_ SuspendingEventArgs^ args);
			void OnSurfaceContentLost(_In_ Object^ sender, _In_ Object^ args);
			HRESULT CreateDeviceResources();

			ComPtr<ID3D11Device> m_d3dDevice;
			ComPtr<ID3D11DeviceContext> m_d3dDeviceContext;
			ComPtr<ID2D1Device> m_d2dDevice;
			ComPtr<ID2D1DeviceContext> m_d2dDeviceContext;
			CriticalSection m_criticalSection;
			Windows::Foundation::EventRegistrationToken m_eventTokens[2];
		};

	}
}