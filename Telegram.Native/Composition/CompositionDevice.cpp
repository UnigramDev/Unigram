#include "pch.h"
#include "CompositionDevice.h"
#include "DirectRectangleClip2.h"
#if __has_include("Composition/CompositionDevice.g.cpp")
#include "Composition/CompositionDevice.g.cpp"
#endif

#include <detours.h>

#include <winrt/Windows.Foundation.Metadata.h>
#include <winrt/Windows.UI.Xaml.Hosting.h>

using namespace winrt::Windows::Foundation::Metadata;
using namespace winrt::Windows::UI::Xaml::Hosting;

struct __declspec(uuid("7cc8cb07-5a0d-46bb-8c54-9beafe63b476"))
	IUIElementStaticsPrivate : ::IUnknown
{
	// IInspectable methods
	virtual HRESULT __stdcall GetIids(
		uint32_t* iidCount,
		GUID** iids) = 0;

	virtual HRESULT __stdcall GetRuntimeClassName(
		HSTRING* className) = 0;

	virtual HRESULT __stdcall GetTrustLevel(
		TrustLevel* trustLevel) = 0;

	// IUIElementStaticsPrivate methods
	virtual HRESULT __stdcall add_PopupOpening(
	/* parameters needed */) = 0;

	virtual HRESULT __stdcall remove_PopupOpening(
		EventRegistrationToken token) = 0;

	virtual HRESULT __stdcall add_PopupPlacement(
	/* parameters needed */) = 0;

	virtual HRESULT __stdcall remove_PopupPlacement(
		EventRegistrationToken token) = 0;

	virtual HRESULT __stdcall InternalGetIsEnabled(
	/* parameters needed */) = 0;

	virtual HRESULT __stdcall InternalPutIsEnabled(
	/* parameters needed */) = 0;

	virtual HRESULT __stdcall GetRasterizationScale(
		float* value) = 0;

	virtual HRESULT __stdcall PutRasterizationScale(
		float value) = 0;

	virtual HRESULT __stdcall PutPopupRootLightDismissBounds(
	/* parameters needed */) = 0;

	virtual HRESULT __stdcall EnablePopupZIndexSorting(
	/* parameters needed */) = 0;

	virtual HRESULT __stdcall GetElementLayerVisual(
		void* pElement,
		void** result) = 0;
};

namespace winrt::Telegram::Native::Composition::implementation
{
	std::mutex CompositionDevice::s_lock;
	winrt::com_ptr<CompositionDevice> CompositionDevice::s_current{ nullptr };

	CompositionDevice::CompositionDevice()
	{
		HRESULT hr = ::CoCreateInstance(
			CLSID_UIAnimationManager2,
			nullptr,
			CLSCTX_INPROC_SERVER,
			IID_IUIAnimationManager2,
			reinterpret_cast<LPVOID*>(&_manager));

		if (SUCCEEDED(hr))
		{
			hr = ::CoCreateInstance(
				CLSID_UIAnimationTransitionLibrary2,
				nullptr,
				CLSCTX_INPROC_SERVER,
				IID_IUIAnimationTransitionLibrary2,
				reinterpret_cast<LPVOID*>(&_transitionLibrary));
		}
	}

	bool CompositionDevice::s_Hooked = false;
	DWORD CompositionDevice::s_ThreadId = NULL;
	std::mutex CompositionDevice::s_Mutex = { };
	PFN_CreateVisual CompositionDevice::s_CreateVisual = nullptr;

	static bool IsOnWindows11OrHigher()
	{
		bool isWin11 = ApiInformation::IsApiContractPresent(L"Windows.Foundation.UniversalApiContract", 14);
		return isWin11;
	}

	// Courtesy of @ahmed605
	LayerVisual CompositionDevice::GetElementLayerVisual(UIElement const& element)
	{
		const static bool windows11 = IsOnWindows11OrHigher();

		// On Windows 11 b22000 and higher, we can use the IUIElementStaticsPrivate interface to get the LayerVisual directly
		com_ptr<IUIElementStaticsPrivate> uiElementPrivate;
		if (windows11 && (uiElementPrivate = try_get_activation_factory<UIElement, IUIElementStaticsPrivate>()))
		{
			LayerVisual layerVisual{ nullptr };
			check_hresult(uiElementPrivate->GetElementLayerVisual(winrt::get_abi(element), put_abi(layerVisual)));
			return layerVisual;
		}

		// The code below definitely works on late Windows 10 builds, but it definitely crashes on 1909 and earlier
		// Thus, for now we just disable bubble tails on Windows 10.
		return nullptr;

		// We are using the thread ID to verify and ensure that we aren't hooking any other ElementCompositionPreview::GetElementVisual call
		// that happened to be going in another thread at the same time we are hooking the function to return a LayerVisual,
		// and we use a lock to ensure that only one thread can be hooking at a time so that thread ID doesn't get changed mid-hook.
		std::scoped_lock lock(s_Mutex);
		EnsureHooked();

		s_ThreadId = GetCurrentThreadId();
		auto visual = ElementCompositionPreview::GetElementVisual(element);
		s_ThreadId = NULL;
		return visual.as<LayerVisual>();
	}

	void CompositionDevice::EnsureHooked()
	{
		if (!s_Hooked)
		{
			// assuming we are on the UI thread, it wouldn't work otherwise anyway
			auto compositor = Window::Current().Compositor();
			auto device3 = compositor.as<IDCompositionDevice3>();
			auto vtbl = *reinterpret_cast<void***>(device3.get());
			s_CreateVisual = reinterpret_cast<PFN_CreateVisual>(vtbl[6]);

			DetourTransactionBegin();
			DetourUpdateThread(GetCurrentThread());
			DetourAttach(reinterpret_cast<PVOID*>(&s_CreateVisual), &CompositionDevice::CreateVisualHook);
			DetourTransactionCommit();
			s_Hooked = true;
		}
	}

	HRESULT WINAPI CompositionDevice::CreateVisualHook(IDCompositionDevice2* pThis, IDCompositionVisual2** ppVisual)
	{
		// Ensure that we are only hooking our own calls to ElementCompositionPreview::GetElementVisual / IDCompositionDevice2::CreateVisual
		if (s_ThreadId != GetCurrentThreadId())
			return s_CreateVisual(pThis, ppVisual);

		Compositor compositor{ nullptr };
		copy_from_abi(compositor, pThis);

		LayerVisual layerVisual = compositor.CreateLayerVisual();
		copy_to_abi(layerVisual, *(void**&)ppVisual);

		return S_OK;
	}

	winrt::Telegram::Native::Composition::DirectRectangleClip2 CompositionDevice::CreateRectangleClip2(UIElement const& element)
	{
		return CreateRectangleClip2(winrt::Windows::UI::Xaml::Hosting::ElementCompositionPreview::GetElementVisual(element));
	}

	winrt::Telegram::Native::Composition::DirectRectangleClip2 CompositionDevice::CreateRectangleClip2(Visual const& visual)
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


	void CompositionDevice::SetClip(Visual const& visual, winrt::Telegram::Native::Composition::DirectRectangleClip2 const& clip)
	{
		HRESULT hr;

		auto impl = winrt::get_self<implementation::DirectRectangleClip2>(clip);

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
