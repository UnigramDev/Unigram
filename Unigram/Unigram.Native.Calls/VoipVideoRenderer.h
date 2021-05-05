#include "pch.h"

#include <stddef.h>
#include <memory>

#include "api/video/i420_buffer.h"
#include "libyuv.h"

#include "api/video/video_frame.h"
#include "api/video/video_source_interface.h"
#include "media/base/video_adapter.h"
#include "media/base/video_broadcaster.h"

#include <winrt/Microsoft.Graphics.Canvas.h>

#include <Microsoft.Graphics.Canvas.h>
#include <Microsoft.Graphics.Canvas.native.h>

using namespace winrt::Microsoft::Graphics::Canvas;
using namespace winrt::Microsoft::Graphics::Canvas::UI::Xaml;

struct VoipVideoRenderer : public rtc::VideoSinkInterface<webrtc::VideoFrame>
{
	bool m_disposed{ false };
	bool m_readyToDraw;

	winrt::event_token m_eventToken;

	CanvasControl m_canvasControl{ nullptr };
	CanvasBitmap m_canvasBitmap{ nullptr };

	VoipVideoRenderer(CanvasControl canvas, bool fill) {
		m_canvasControl = canvas;
		m_readyToDraw = canvas.ReadyToDraw();

		m_eventToken = canvas.Draw([this, fill](const CanvasControl sender, CanvasDrawEventArgs const args) {
			m_readyToDraw = true;

			if (m_canvasBitmap != nullptr) {
				float width = m_canvasBitmap.SizeInPixels().Width;
				float height = m_canvasBitmap.SizeInPixels().Height;
				float x = 0;
				float y = 0;

				float ratioX = sender.Size().Width / width;
				float ratioY = sender.Size().Height / height;

				if (fill && ratioX > ratioY || !fill && ratioX < ratioY)
				{
					width = sender.Size().Width;
					height *= ratioX;
					y = (sender.Size().Height - height) / 2;
				}
				else
				{
					width *= ratioY;
					height = sender.Size().Height;
					x = (sender.Size().Width - width) / 2;
				}

				args.DrawingSession().DrawImage(m_canvasBitmap, winrt::Windows::Foundation::Rect(x, y, width, height));
			}
			});
	}

	~VoipVideoRenderer() {
		m_disposed = true;

		m_canvasControl.Draw(m_eventToken);
		m_canvasControl = nullptr;

		if (m_canvasBitmap != nullptr)
		{
			m_canvasBitmap.Close();
			m_canvasBitmap = nullptr;
		}
	}

	void OnFrame(const webrtc::VideoFrame& frame) override
	{
		if (m_disposed || !m_readyToDraw) {
			return;
		}

		rtc::scoped_refptr<webrtc::I420BufferInterface> buffer(frame.video_frame_buffer()->ToI420());

		webrtc::VideoRotation rotation = frame.rotation();
		if (rotation != webrtc::kVideoRotation_0)
		{
			buffer = webrtc::I420Buffer::Rotate(*buffer, rotation);
		}

		int32_t width = buffer->width();
		int32_t height = buffer->height();

		size_t bits = 32;
		size_t size = width * height * (bits >> 3);

		std::unique_ptr<uint8_t[]> data(new uint8_t[size]);
		libyuv::I420ToARGB(buffer->DataY(), buffer->StrideY(), buffer->DataU(), buffer->StrideU(), buffer->DataV(),
			buffer->StrideV(), data.get(), width * bits / 8, width, height);

		auto raw = data.get();

		if (m_canvasBitmap == nullptr || m_canvasBitmap.SizeInPixels().Width != width || m_canvasBitmap.SizeInPixels().Height != height)
		{
			auto view = winrt::array_view<uint8_t const>(raw, raw + size);
			m_canvasBitmap = winrt::Microsoft::Graphics::Canvas::CanvasBitmap::CreateFromBytes(
				m_canvasControl.Device(), view, width, height,
				winrt::Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized);
		}
		else
		{
			auto bitmapAbi = m_canvasBitmap.as<ABI::Microsoft::Graphics::Canvas::ICanvasBitmap>();
			bitmapAbi->SetPixelBytes(size, (BYTE*)raw);
		}

		if (m_canvasControl)
		{
			m_canvasControl.Invalidate();
		}
	}
};
