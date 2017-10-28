#pragma once

#include "OpenGLES.h"
#include "SimpleRenderer.h"

using namespace Windows::UI::Xaml::Controls;

namespace Telegram
{
	namespace Intro
	{
		public ref class TLIntroRenderer sealed
		{
		public:
			TLIntroRenderer(SwapChainPanel^ swapChainPanel);
			virtual ~TLIntroRenderer();

			void Loaded();

			void SetPage(int value) {
				mCurrentPage = value;
			}

			void SetScroll(float value) {
				mCurrentScroll = value;
			}

		internal:
			TLIntroRenderer(OpenGLES* openGLES, SwapChainPanel^ swapChainPanel);

		private:
			void OnColorValuesChanged(Windows::UI::ViewManagement::UISettings^ sender, Platform::Object^ args);
			void OnVisibilityChanged(Windows::UI::Core::CoreWindow^ sender, Windows::UI::Core::VisibilityChangedEventArgs^ args);
			void CreateRenderSurface();
			void DestroyRenderSurface();
			void RecoverFromLostDevice();
			void StartRenderLoop();
			void StopRenderLoop();

			float mCurrentScale;

			float mCurrentScroll;
			int mCurrentPage;
			int mDarkTheme;

			OpenGLES* mOpenGLES;
			OpenGLES mOpenGLESHolder;

			SwapChainPanel^ mSwapChainPanel;

			EGLSurface mRenderSurface;     // This surface is associated with a swapChainPanel on the page
			Concurrency::critical_section mRenderSurfaceCriticalSection;
			Windows::Foundation::IAsyncAction^ mRenderLoopWorker;

			Windows::UI::ViewManagement::UISettings^ mSettings;
		};
	}
}
