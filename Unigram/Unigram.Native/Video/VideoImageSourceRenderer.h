#pragma once
#include <ppl.h>
#include <ppltasks.h>
#include <pplcancellation_token.h>
#include <wrl\wrappers\corewrappers.h>
#include "VirtualImageSourceRendererCallback.h"


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

using namespace concurrency;

namespace Unigram
{
	namespace Native
	{
		ref class VideoImageSourceRendererFactory;

		[Bindable]
		[WebHostHidden]
		public ref class VideoImageSourceRenderer sealed : public INotifyPropertyChanged
		{
		public:
			virtual	event PropertyChangedEventHandler^ PropertyChanged;

			property VirtualSurfaceImageSource^ ImageSource
			{
				VirtualSurfaceImageSource^ get();
			}

			[DefaultOverload]
			void SetSource(_In_ Windows::Foundation::Uri^ uri);
			void SetSource(_In_ Windows::Storage::Streams::IRandomAccessStream^ stream);
			void SetSource(_In_ Windows::Media::Core::IMediaSource^ mediaSource);

		internal:
			VideoImageSourceRenderer(int maximumWidth, int maximumHeight, _In_ VideoImageSourceRendererFactory^ owner);

			HRESULT OnUpdatesNeeded();
			HRESULT OnTimerTick();

		private:
			~VideoImageSourceRenderer();

			void Reset();
			void Initialize();
			void NotifyPropertyChanged(_In_ String^ propertyName);
			void OnSurfaceContentLost(_In_ Object^ sender, _In_ Object^ args);
			void OnEnteredBackground(_In_ Object^ sender, _In_ EnteredBackgroundEventArgs^ args);
			void OnLeavingBackground(_In_ Object^ sender, _In_ LeavingBackgroundEventArgs^ args);
			HRESULT Draw(_In_ RECT const& drawingBounds);
			HRESULT BeginDraw(_In_ RECT const& drawingBounds, _Out_ ID2D1DeviceContext** deviceContext);
			HRESULT EndDraw(_In_ ID2D1DeviceContext* deviceContext);
			HRESULT InitializeImageSource();
			HRESULT Invalidate(bool resize);
			task<void> UpdateFrames(cancellation_token& ct);
			static HRESULT InitializeSourceReader(_In_ IMFSourceReader* sourceReader, SIZE& frameSize, _Out_ float* frameRate);
			static HRESULT ConvertVideoTypeToUncompressedType(_In_ IMFMediaType* pType, _In_ const GUID& subtype, SIZE& frameSize, _Out_ IMFMediaType** ppType);

			VirtualSurfaceImageSource^ m_imageSource;
			VideoImageSourceRendererFactory^ m_owner;
			ComPtr<VirtualImageSourceRendererCallback> m_updatesCallback;
			ComPtr<IVirtualSurfaceImageSourceNative> m_imageSourceNative;
			ComPtr<ISurfaceImageSourceNativeWithD2D> m_imageSourceNativeD2D;
			ComPtr<IMFSourceReader> m_sourceReader;
			CriticalSection m_criticalSection;
			concurrency::cancellation_token_source m_cancellationTokenSource;
			Windows::Foundation::EventRegistrationToken m_eventTokens[3];
			std::vector<ComPtr<ID2D1Bitmap>> m_frames;
			bool m_isUpdatingFrames;
			int m_frameIndex;
			int64 m_hiddenTicks;
			int64 m_maximumHiddenTicks;
			SIZE m_size;
			const SIZE m_maximumSize;
		};
	}
}
