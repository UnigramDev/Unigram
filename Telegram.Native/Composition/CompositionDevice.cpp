#include "pch.h"
#include "CompositionDevice.h"
#include "DirectRectangleClip.h"
#include "DirectRectangleClip2.h"
#if __has_include("Composition/CompositionDevice.g.cpp")
#include "Composition/CompositionDevice.g.cpp"
#endif

#include <winrt/Windows.UI.Xaml.Hosting.h>

namespace winrt::Telegram::Native::Composition::implementation
{
	std::mutex CompositionDevice::s_lock;
	winrt::com_ptr<CompositionDevice> CompositionDevice::s_current{ nullptr };

	CompositionDevice::CompositionDevice() {
		HRESULT hr = ::CoCreateInstance(
			CLSID_UIAnimationManager2,
			nullptr,
			CLSCTX_INPROC_SERVER,
			IID_IUIAnimationManager2,
			reinterpret_cast<LPVOID*>(&_manager));

		if (SUCCEEDED(hr)) {
			hr = ::CoCreateInstance(
				CLSID_UIAnimationTransitionLibrary2,
				nullptr,
				CLSCTX_INPROC_SERVER,
				IID_IUIAnimationTransitionLibrary2,
				reinterpret_cast<LPVOID*>(&_transitionLibrary));
		}
	}

	winrt::Telegram::Native::Composition::DirectRectangleClip CompositionDevice::CreateRectangleClip(UIElement element)
	{
		return CreateRectangleClip(winrt::Windows::UI::Xaml::Hosting::ElementCompositionPreview::GetElementVisual(element));
	}

	winrt::Telegram::Native::Composition::DirectRectangleClip CompositionDevice::CreateRectangleClip(Visual visual)
	{
		HRESULT hr;
		auto compositor = visual.Compositor();
		auto device = compositor.as<IDCompositionDesktopDevice>();

		winrt::com_ptr<IDCompositionRectangleClip> clip;
		hr = device->CreateRectangleClip(clip.put());

		auto abi = visual.as<IDCompositionVisual2>();
		hr = abi->SetClip(clip.get());

		auto result = winrt::make_self<implementation::DirectRectangleClip>(clip);
		return *result;
	}

	winrt::Telegram::Native::Composition::DirectRectangleClip2 CompositionDevice::CreateRectangleClip2(UIElement element)
	{
		return CreateRectangleClip2(winrt::Windows::UI::Xaml::Hosting::ElementCompositionPreview::GetElementVisual(element));
	}

	winrt::Telegram::Native::Composition::DirectRectangleClip2 CompositionDevice::CreateRectangleClip2(Visual visual)
	{
		HRESULT hr;
		auto compositor = visual.Compositor();
		auto device = compositor.as<IDCompositionDesktopDevice>();

		winrt::com_ptr<IDCompositionRectangleClip> clip;
		hr = device->CreateRectangleClip(clip.put());

		auto abi = visual.as<IDCompositionVisual2>();
		hr = abi->SetClip(clip.get());

		auto result = winrt::make_self<implementation::DirectRectangleClip2>(clip);
		return *result;
	}


	void CompositionDevice::SetClip(Visual visual, winrt::Telegram::Native::Composition::DirectRectangleClip clip)
	{
		HRESULT hr;

		auto impl = winrt::get_self<implementation::DirectRectangleClip>(clip);

		auto abi = visual.as<IDCompositionVisual2>();
		hr = abi->SetClip(impl->m_impl.get());
	}


	HRESULT CompositionDevice::CreateCubicBezierAnimation(Compositor compositor, float from, float to, double duration, IDCompositionAnimation** slideAnimation)
	{
		HRESULT hr = (slideAnimation == nullptr) ? E_POINTER : S_OK;
		auto device = compositor.as<IDCompositionDesktopDevice>();

		if (SUCCEEDED(hr))
		{
			*slideAnimation = nullptr;
		}

		//WAM propagates curves to DirectComposition using the IDCompositionAnimation object
		winrt::com_ptr<IDCompositionAnimation> animation;
		if (SUCCEEDED(hr))
		{
			hr = device->CreateAnimation(animation.put());
		}

		//Create a storyboard for the slide animation
		winrt::com_ptr<IUIAnimationStoryboard2> storyboard;
		if (SUCCEEDED(hr))
		{
			hr = _manager->CreateStoryboard(storyboard.put());
		}

		// Synchronizing WAM and DirectComposition time such that when WAM Update is called, 
		// the value reflects the DirectComposition value at the given time.
		DCOMPOSITION_FRAME_STATISTICS frameStatistics = { 0 };
		if (SUCCEEDED(hr))
		{
			hr = device->GetFrameStatistics(&frameStatistics);
		}

		UI_ANIMATION_SECONDS nextEstimatedFrameTime = 0.0;
		if (SUCCEEDED(hr))
		{
			nextEstimatedFrameTime = static_cast<double>(frameStatistics.nextEstimatedFrameTime.QuadPart) / static_cast<double>(frameStatistics.timeFrequency.QuadPart);
		}

		//Upating the WAM time 
		if (SUCCEEDED(hr))
		{
			hr = _manager->Update(nextEstimatedFrameTime);
		}

		winrt::com_ptr<IUIAnimationVariable2> animationVariable;
		if (SUCCEEDED(hr))
		{
			hr = _manager->CreateAnimationVariable(from, animationVariable.put());
		}

		winrt::com_ptr<IUIAnimationTransition2> transition;
		if (SUCCEEDED(hr))
		{
			hr = _transitionLibrary->CreateCubicBezierLinearTransition(duration, to, .41F, .51999998F, .00F, .94F, transition.put());
		}

		//Add above transition to storyboard
		if (SUCCEEDED(hr))
		{
			hr = storyboard->AddTransition(animationVariable.get(), transition.get());
		}

		//schedule the storyboard for play at the next estimate vblank
		if (SUCCEEDED(hr))
		{
			hr = storyboard->Schedule(nextEstimatedFrameTime);
		}

		//Giving WAM varialbe the IDCompositionAnimation object to receive the animation curves
		if (SUCCEEDED(hr))
		{
			hr = animationVariable->GetCurve(animation.get());
		}

		if (SUCCEEDED(hr))
		{
			*slideAnimation = animation.detach();
		}

		return hr;
	}
}
