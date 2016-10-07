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

namespace Mp4ImageSourceRenderer
{

	ref class VideoImageSourceRenderer;

	class VirtualImageSourceRendererCallback WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IVirtualSurfaceUpdatesCallbackNative, IMFAsyncCallback>
	{
	public:
		VirtualImageSourceRendererCallback(_In_ VideoImageSourceRenderer^ renderer);
		~VirtualImageSourceRendererCallback();

		HRESULT StartTimer(int64 duration);
		HRESULT ResumeTimer();
		HRESULT StopTimer();
		STDMETHODIMP UpdatesNeeded();
		STDMETHODIMP Invoke(_In_ IMFAsyncResult* pAsyncResult);
		STDMETHODIMP GetParameters(_Out_ DWORD* pdwFlags, _Out_ DWORD* pdwQueue);

		inline const bool IsTimerRunning() const
		{
			return m_timerKey != NULL;
		}

	private:
		int64 m_duration;
		MFWORKITEM_KEY m_timerKey;
		VideoImageSourceRenderer^ m_renderer;
		DispatchedHandler^ m_timerDispatchedHandler;
	};

}