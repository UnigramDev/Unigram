// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <ppl.h>
#include <ppltasks.h>
#include <pplcancellation_token.h>
#include <wrl\wrappers\corewrappers.h>
#include "VirtualImageSourceRendererCallback.h"
#include "ReadFramesAsyncOperation.h"

using namespace Platform;
using namespace Windows::Storage;
using namespace Windows::UI::Xaml::Media::Imaging;
using namespace Windows::Storage::Streams;
using namespace Windows::Foundation;
using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;
using namespace Windows::ApplicationModel;
using namespace Windows::UI::Xaml::Data;
using Windows::Foundation::Metadata::WebHostHiddenAttribute;
using Windows::Foundation::Metadata::DefaultOverloadAttribute;

using namespace Concurrency;

namespace Unigram
{
	namespace Native
	{

		ref class AnimatedImageSourceRendererFactory;

		[Bindable]
		[WebHostHidden]
		public ref class AnimatedImageSourceRenderer sealed : public INotifyPropertyChanged
		{
		public:
			virtual	event PropertyChangedEventHandler^ PropertyChanged;

			property VirtualSurfaceImageSource^ ImageSource
			{
				VirtualSurfaceImageSource^ get();
			}

			[DefaultOverload]
			IAsyncAction^ SetSourceAsync(_In_ Windows::Foundation::Uri^ uri);
			IAsyncAction^ SetSourceAsync(_In_ Windows::Storage::Streams::IRandomAccessStream^ stream);
			IAsyncAction^ SetSourceAsync(_In_ Windows::Media::Core::IMediaSource^ mediaSource);

		internal:
			AnimatedImageSourceRenderer(int maximumWidth, int maximumHeight, _In_ AnimatedImageSourceRendererFactory^ owner);

			HRESULT OnUpdatesNeeded();
			HRESULT OnTimerTick();

		private:
			~AnimatedImageSourceRenderer();

			task<void> Reset();
			task<void> Initialize(ComPtr<ReadFramesAsyncOperation>& asyncOperation);
			void NotifyPropertyChanged(_In_ String^ propertyName);
			void OnSurfaceContentLost(_In_ Object^ sender, _In_ Object^ args);
			void OnEnteredBackground(_In_ Object^ sender, _In_ EnteredBackgroundEventArgs^ args);
			void OnLeavingBackground(_In_ Object^ sender, _In_ LeavingBackgroundEventArgs^ args);
			HRESULT Draw(_In_ RECT const& drawingBounds);
			HRESULT InitializeImageSource();
			HRESULT Invalidate(bool resize);

			ComPtr<ID2D1Bitmap> m_frameBitmap;
			ComPtr<FramesCacheStore> m_framesCacheStore;
			VirtualSurfaceImageSource^ m_imageSource;
			AnimatedImageSourceRendererFactory^ m_owner;
			ComPtr<VirtualImageSourceRendererCallback> m_updatesCallback;
			ComPtr<IVirtualSurfaceImageSourceNative> m_imageSourceNative;

			//Not using ISurfaceImageSourceNativeWithD2D due to a bug that makes the VirtualSurfaceImageSource unusable after resuming from background
			//ComPtr<ISurfaceImageSourceNativeWithD2D> m_imageSourceNativeD2D;
			CriticalSection m_criticalSection;
			concurrency::cancellation_token_source m_cancellationTokenSource;
			Windows::Foundation::EventRegistrationToken m_eventTokens[3];
			int m_frameIndex;
			D2D1_SIZE_U m_size;
			const D2D1_SIZE_U m_maximumSize;
		};

	}
}