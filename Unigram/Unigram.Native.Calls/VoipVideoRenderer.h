// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

// clang-format off
#include "pch.h"
// clang-format on

#include <stddef.h>

#include <memory>

#include "api/media_stream_interface.h"
#include "api/create_peerconnection_factory.h"
#include "api/peer_connection_interface.h"
#include "api/audio_codecs/builtin_audio_decoder_factory.h"
#include "api/audio_codecs/builtin_audio_encoder_factory.h"
#include "api/video_codecs/builtin_video_decoder_factory.h"
#include "api/video_codecs/builtin_video_encoder_factory.h"
#include "pc/video_track_source.h"
#include "rtc_base/rtc_certificate_generator.h"
#include "rtc_base/ssl_adapter.h"

#include "api/video/i420_buffer.h"
#include "modules/video_capture/video_capture_factory.h"
#include "modules/video_capture/windows/device_info_winrt.h"
#include "libyuv.h"

#include "api/video/video_frame.h"
#include "api/video/video_source_interface.h"
#include "media/base/video_adapter.h"
#include "media/base/video_broadcaster.h"
#include "rtc_base/critical_section.h"

struct VoipVideoRenderer : public rtc::VideoSinkInterface<webrtc::VideoFrame>
{
    const winrt::Windows::UI::Core::CoreDispatcher _uiThread;

    VoipVideoRenderer(winrt::Windows::UI::Xaml::UIElement canvas) : _uiThread(canvas.Dispatcher()) {
        VoipVideoRendererAsync(canvas);
    }

    ~VoipVideoRenderer() {
        m_disposed = true;
    }

    bool m_disposed{ false };

    winrt::Microsoft::Graphics::Canvas::CanvasDevice _canvasDevice;
    winrt::Windows::UI::Composition::CompositionDrawingSurface _surface{ nullptr };

    winrt::Windows::Foundation::IAsyncAction
        VoipVideoRendererAsync(winrt::Windows::UI::Xaml::UIElement canvas)
    {
        co_await winrt::resume_foreground(_uiThread);

        if (m_disposed) {
            co_return;
        }

        winrt::Windows::UI::Composition::Compositor compositor = winrt::Windows::UI::Xaml::Window::Current().Compositor();

        co_await winrt::resume_background();

        if (m_disposed) {
            co_return;
        }

        winrt::Windows::UI::Composition::CompositionGraphicsDevice compositionGraphicsDevice =
            winrt::Microsoft::Graphics::Canvas::UI::Composition::CanvasComposition::CreateCompositionGraphicsDevice(
                compositor, _canvasDevice);
        _surface = compositionGraphicsDevice.CreateDrawingSurface(
            { 0, 0 }, winrt::Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,
            winrt::Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);

        winrt::Windows::UI::Composition::CompositionSurfaceBrush brush = compositor.CreateSurfaceBrush(_surface);
        brush.HorizontalAlignmentRatio(.5);
        brush.VerticalAlignmentRatio(.5);
        brush.Stretch(winrt::Windows::UI::Composition::CompositionStretch::Uniform);

        winrt::Windows::UI::Composition::SpriteVisual visual = compositor.CreateSpriteVisual();
        visual.Brush(brush);
        visual.RelativeSizeAdjustment(winrt::Windows::Foundation::Numerics::float2::one());

        co_await winrt::resume_foreground(_uiThread);

        winrt::Windows::UI::Xaml::Hosting::ElementCompositionPreview::SetElementChildVisual(canvas, visual);
    }

    void
        OnFrame(const webrtc::VideoFrame& frame) override
    {
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

        PaintFrameAsync(std::move(data), size, width, height);
    }

    winrt::Windows::Foundation::IAsyncAction PaintFrameAsync(std::unique_ptr<uint8_t[]> data, size_t length, int32_t width, int32_t height)
    {
        if (m_disposed) {
            return;
        }

        auto raw = data.get();
        auto view = winrt::array_view<uint8_t const>(raw, raw + length);
        auto bitmap = winrt::Microsoft::Graphics::Canvas::CanvasBitmap::CreateFromBytes(
            _canvasDevice, view, width, height,
            winrt::Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized);

        if (_surface.Size() != bitmap.Size())
            winrt::Microsoft::Graphics::Canvas::UI::Composition::CanvasComposition::Resize(_surface, bitmap.Size());

        co_await winrt::resume_foreground(_uiThread);

        if (m_disposed) {
            co_return;
        }

        winrt::Microsoft::Graphics::Canvas::CanvasDrawingSession drawingSession =
            winrt::Microsoft::Graphics::Canvas::UI::Composition::CanvasComposition::CreateDrawingSession(_surface);
        {
            drawingSession.Clear(winrt::Windows::UI::Colors::Transparent());
            drawingSession.DrawImage(bitmap);
        }
        drawingSession.Close();
    }
};
