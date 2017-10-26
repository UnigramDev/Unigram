#include "pch.h"
#include "TLIntroRenderer.h"
#include "texture_helper.h"
#include "WICTextureLoader.h"
#include <core\animations.h>
#include <core\objects.h>

using namespace Telegram::Intro;
using namespace Platform;
using namespace Concurrency;
using namespace Windows::Foundation;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::ViewManagement;
using namespace Windows::UI::Core;

TLIntroRenderer::TLIntroRenderer(SwapChainPanel^ swapChainPanel) :
	TLIntroRenderer(&mOpenGLESHolder, swapChainPanel)
{
}

TLIntroRenderer::TLIntroRenderer(OpenGLES* openGLES, SwapChainPanel^ swapChainPanel) :
	mOpenGLES(openGLES),
	mRenderSurface(EGL_NO_SURFACE),
	mCurrentPage(0),
	mCurrentScroll(0),
	mCurrentScale(1),
	mSwapChainPanel(swapChainPanel),
	mSettings(ref new UISettings())
{
	auto color = mSettings->GetColorValue(UIColorType::Background);
	mDarkTheme = color.R == 0 && color.G == 0 && color.B == 0;

	mSettings->ColorValuesChanged +=
		ref new Windows::Foundation::TypedEventHandler<UISettings^, Object^>(this, &TLIntroRenderer::OnColorValuesChanged);

	auto window = Windows::UI::Xaml::Window::Current->CoreWindow;
	window->VisibilityChanged +=
		ref new Windows::Foundation::TypedEventHandler<CoreWindow^, VisibilityChangedEventArgs^>(this, &TLIntroRenderer::OnVisibilityChanged);
}

TLIntroRenderer::~TLIntroRenderer()
{
	StopRenderLoop();
	DestroyRenderSurface();
}

void TLIntroRenderer::Loaded()
{
	// The SwapChainPanel has been created and arranged in the page layout, so EGL can be initialized.
	CreateRenderSurface();
	StartRenderLoop();

	//auto scroll = Flip->GetScrollingHost();
	//scroll->ViewChanging += ref new Windows::Foundation::EventHandler<Windows::UI::Xaml::Controls::ScrollViewerViewChangingEventArgs ^>(this, &AngleTestLib::TLIntroRenderer::OnViewChanging);
}

void TLIntroRenderer::OnColorValuesChanged(UISettings^ sender, Object^ args)
{
	auto color = sender->GetColorValue(UIColorType::Background);
	mDarkTheme = color.R == 0 && color.G == 0 && color.B == 0;
}

void TLIntroRenderer::OnVisibilityChanged(CoreWindow^ sender, VisibilityChangedEventArgs^ args)
{
	if (args->Visible && mRenderSurface != EGL_NO_SURFACE)
	{
		StartRenderLoop();
	}
	else
	{
		StopRenderLoop();
	}
}

void TLIntroRenderer::CreateRenderSurface()
{
	if (mOpenGLES && mRenderSurface == EGL_NO_SURFACE)
	{
		// The app can configure the the SwapChainPanel which may boost performance.
		// By default, this template uses the default configuration.
		//mRenderSurface = mOpenGLES->CreateSurface(mSwapChainPanel, nullptr, nullptr);

		Windows::Graphics::Display::DisplayInformation^ info = Windows::Graphics::Display::DisplayInformation::GetForCurrentView();
		mCurrentScale = info->LogicalDpi / 96.0f;

		// You can configure the SwapChainPanel to render at a lower resolution and be scaled up to
		// the swapchain panel size. This scaling is often free on mobile hardware.
		//
		// One way to configure the SwapChainPanel is to specify precisely which resolution it should render at.
		// Size customRenderSurfaceSize = Size(800, 600);
		// mRenderSurface = mOpenGLES->CreateSurface(swapChainPanel, &customRenderSurfaceSize, nullptr);
		//
		// Another way is to tell the SwapChainPanel to render at a certain scale factor compared to its size.
		// e.g. if the SwapChainPanel is 1920x1280 then setting a factor of 0.5f will make the app render at 960x640
		// float customResolutionScale = 0.5f;
		mRenderSurface = mOpenGLES->CreateSurface(mSwapChainPanel, nullptr, &mCurrentScale);
		// 
	}
}

void TLIntroRenderer::DestroyRenderSurface()
{
	if (mOpenGLES)
	{
		mOpenGLES->DestroySurface(mRenderSurface);
	}
	mRenderSurface = EGL_NO_SURFACE;
}

void TLIntroRenderer::RecoverFromLostDevice()
{
	// Stop the render loop, reset OpenGLES, recreate the render surface
	// and start the render loop again to recover from a lost device.

	StopRenderLoop();

	{
		critical_section::scoped_lock lock(mRenderSurfaceCriticalSection);

		DestroyRenderSurface();
		mOpenGLES->Reset();
		CreateRenderSurface();
	}

	StartRenderLoop();
}

void TLIntroRenderer::StartRenderLoop()
{
	// If the render loop is already running then do not start another thread.
	if (mRenderLoopWorker != nullptr && mRenderLoopWorker->Status == Windows::Foundation::AsyncStatus::Started)
	{
		return;
	}

	// Create a task for rendering that will be run on a background thread.
	auto workItemHandler = ref new Windows::System::Threading::WorkItemHandler([this](Windows::Foundation::IAsyncAction ^ action)
	{
		critical_section::scoped_lock lock(mRenderSurfaceCriticalSection);

		mOpenGLES->MakeCurrent(mRenderSurface);
		SimpleRenderer renderer = SimpleRenderer(mCurrentScale);

		while (action->Status == Windows::Foundation::AsyncStatus::Started)
		{
			EGLint panelWidth = 0;
			EGLint panelHeight = 0;
			mOpenGLES->GetSurfaceDimensions(mRenderSurface, &panelWidth, &panelHeight);

			//mRenderer = renderer;

			renderer.SetDarkTheme(mDarkTheme);
			renderer.SetCurrentPage(mCurrentPage);
			renderer.SetCurrentScroll(mCurrentScroll);

			// Logic to update the scene could go here
			renderer.UpdateWindowSize(panelWidth, panelHeight);
			renderer.Draw();

			// The call to eglSwapBuffers might not be successful (i.e. due to Device Lost)
			// If the call fails, then we must reinitialize EGL and the GL resources.
			if (mOpenGLES->SwapBuffers(mRenderSurface) != GL_TRUE)
			{
				// XAML objects like the SwapChainPanel must only be manipulated on the UI thread.
				mSwapChainPanel->Dispatcher->RunAsync(Windows::UI::Core::CoreDispatcherPriority::High, ref new Windows::UI::Core::DispatchedHandler([=]()
				{
					RecoverFromLostDevice();
				}, CallbackContext::Any));

				return;
			}
		}
	});

	// Run task on a dedicated high priority background thread.
	mRenderLoopWorker = Windows::System::Threading::ThreadPool::RunAsync(workItemHandler, Windows::System::Threading::WorkItemPriority::High, Windows::System::Threading::WorkItemOptions::TimeSliced);
}

void TLIntroRenderer::StopRenderLoop()
{
	if (mRenderLoopWorker)
	{
		mRenderLoopWorker->Cancel();
		mRenderLoopWorker = nullptr;
	}
}