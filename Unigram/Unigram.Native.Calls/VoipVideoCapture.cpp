// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

// clang-format off
#include "pch.h"
#include "VoipVideoCapture.h"
#include "VoipVideoCapture.g.cpp"

#include "winrt/Windows.Graphics.Imaging.h"
// clang-format on

struct __declspec(uuid("5b0d3235-4dba-4d44-865e-8f1d0e4fd04d")) __declspec(novtable) IMemoryBufferByteAccess : ::IUnknown
{
	virtual HRESULT __stdcall GetBuffer(uint8_t** value, uint32_t* capacity) = 0;
};

namespace winrt::Unigram::Native::Calls::implementation
{
	VoipVideoCapture::VoipVideoCapture(hstring id)
	{
		m_impl = tgcalls::VideoCaptureInterface::Create(string_to_unmanaged(id));
	}

	VoipVideoCapture::~VoipVideoCapture()
	{
		m_impl = nullptr;
	}

	void VoipVideoCapture::Close() {
		m_impl = nullptr;
	}

	void VoipVideoCapture::SwitchToDevice(hstring deviceId) {
		if (m_impl) {
			m_impl->switchToDevice(string_to_unmanaged(deviceId));
		}
	}

	void VoipVideoCapture::SetState(VoipVideoState state) {
		if (m_impl) {
			m_impl->setState((tgcalls::VideoState)state);
		}
	}

	void VoipVideoCapture::SetPreferredAspectRatio(float aspectRatio) {
		if (m_impl) {
			m_impl->setPreferredAspectRatio(aspectRatio);
		}
	}

	void VoipVideoCapture::SetOutput(Windows::UI::Xaml::UIElement canvas) {
		if (m_impl) {
			if (canvas != nullptr) {
				m_impl->setOutput(std::make_shared<VoipVideoRenderer>(canvas));
			}
			else {
				m_impl->setOutput(nullptr);
			}
		}
	}

	void VoipVideoCapture::FeedBytes(winrt::Windows::Graphics::Imaging::SoftwareBitmap software_bitmap) {
		if (m_test == nullptr) {
		}

		m_test = software_bitmap;
		software_bitmap = m_test;

		if (m_impl) {
			winrt::Windows::Graphics::Imaging::BitmapPlaneDescription bitmap_plane_description_y;
			winrt::Windows::Graphics::Imaging::BitmapPlaneDescription bitmap_plane_description_uv;
			uint8_t* bitmap_content;
			uint32_t bitmap_capacity;

			winrt::Windows::Graphics::Imaging::BitmapBuffer bitmap_buffer = software_bitmap.LockBuffer(
				winrt::Windows::Graphics::Imaging::BitmapBufferAccessMode::Read);

			int32_t plane_count = bitmap_buffer.GetPlaneCount();

			if (plane_count >= 1) {
				bitmap_plane_description_y = bitmap_buffer.GetPlaneDescription(0);
			}

			if (plane_count == 2) {
				bitmap_plane_description_uv = bitmap_buffer.GetPlaneDescription(1);
			}

			winrt::Windows::Foundation::IMemoryBuffer memory_buffer = bitmap_buffer.as<winrt::Windows::Foundation::IMemoryBuffer>();

			winrt::Windows::Foundation::IMemoryBufferReference memory_buffer_reference = memory_buffer.CreateReference();

			auto memory_buffer_byte_access = memory_buffer_reference.as<IMemoryBufferByteAccess>();

			memory_buffer_byte_access->GetBuffer(&bitmap_content,
				&bitmap_capacity);


			m_impl->feedBytes(software_bitmap.PixelWidth(), software_bitmap.PixelHeight(), bitmap_content, bitmap_capacity);
		}
	}

} // namespace winrt::Unigram::Native::Calls::implementation
