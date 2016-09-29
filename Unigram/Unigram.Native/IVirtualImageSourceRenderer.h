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

using namespace Platform;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Data;
using namespace Microsoft::WRL;
using Windows::Foundation::Metadata::WebHostHiddenAttribute;
using Windows::UI::Xaml::Data::BindableAttribute;
using Windows::UI::Xaml::Media::Imaging::VirtualSurfaceImageSource;

namespace Unigram
{
	namespace Native
	{
		[WebHostHidden]
		public interface class IVirtualImageSourceRenderer : public INotifyPropertyChanged
		{
			property VirtualSurfaceImageSource^ ImageSource
			{
				VirtualSurfaceImageSource^ get();
			}

			void NotifyUpdatesNeeded();
		};

		class VirtualImageSourceRendererCallback sealed : public RuntimeClass< RuntimeClassFlags<RuntimeClassType::ClassicCom>, IVirtualSurfaceUpdatesCallbackNative >
		{
		public:
			VirtualImageSourceRendererCallback(_In_ IVirtualImageSourceRenderer^ renderer) :
				m_renderer(renderer)
			{
			}

			~VirtualImageSourceRendererCallback() = default;

			virtual inline STDMETHODIMP UpdatesNeeded()
			{
				try
				{
					m_renderer->NotifyUpdatesNeeded();
				}
				catch (Exception^ ex)
				{
					return ex->HResult;
				}

				return S_OK;
			}

		private:
			IVirtualImageSourceRenderer^ m_renderer;
		};
	}
}