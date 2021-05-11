#include "pch.h"
#include "Composition.CompositionDevice.h"
#include "Composition.DirectRectangleClip.h"

#include "dcompex.h"

namespace winrt::Unigram::Native::Composition::implementation
{
	winrt::Unigram::Native::Composition::DirectRectangleClip CompositionDevice::CreateRectangleClip(Visual visual)
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
}
