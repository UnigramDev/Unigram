// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <dxgi.h> 
#include <dxgi1_2.h> 
#include <d2d1_1.h> 
#include <d2d1_2.h>
#include <d3d11.h> 
#include <d3d11_2.h> 
#include <dwrite.h>
#include <wrl\module.h>
#include <wrl\client.h>
#include <DirectXMath.h> 
#include <Wincodec.h>
#include <mfobjects.h>
#include <mfapi.h>
#include <windows.ui.xaml.media.dxinterop.h>

#pragma comment(lib, "d2d1.lib")
#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "dxguid.lib")
#pragma comment(lib, "mf.lib")
#pragma comment(lib, "mfplat.lib")
#pragma comment(lib, "mfuuid.lib")
#pragma comment(lib, "mfreadwrite.lib")

using namespace Platform;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Data;
using namespace Microsoft::WRL;
using namespace Windows::UI::Core;
using Windows::Foundation::Metadata::WebHostHiddenAttribute;
using Windows::UI::Xaml::Data::BindableAttribute;
using Windows::UI::Xaml::Media::Imaging::VirtualSurfaceImageSource;

namespace Unigram
{
	namespace Native
	{

		ref class AnimatedImageSourceRenderer;

		class VirtualImageSourceRendererCallback WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IVirtualSurfaceUpdatesCallbackNative, IMFAsyncCallback>
		{
		public:
			VirtualImageSourceRendererCallback(_In_ AnimatedImageSourceRenderer^ renderer);
			~VirtualImageSourceRendererCallback();

			HRESULT StartTimer(LONGLONG duration);
			HRESULT StopTimer();
			IFACEMETHODIMP UpdatesNeeded();
			IFACEMETHODIMP Invoke(_In_ IMFAsyncResult* pAsyncResult);
			IFACEMETHODIMP GetParameters(_Out_ DWORD* pdwFlags, _Out_ DWORD* pdwQueue);

			inline const bool IsTimerRunning() const
			{
				return m_timerKey != NULL;
			}

		private:
			MFWORKITEM_KEY m_timerKey;
			AnimatedImageSourceRenderer^ m_renderer;
			DispatchedHandler^ m_timerDispatchedHandler;
		};

	}
}