#include "pch.h"
#include "VideoTranscoder.h"
#if __has_include("Media/VideoTranscoder.g.cpp")
#include "Media/VideoTranscoder.g.cpp"
#endif

#include "H264Encoder.h"
#include "VideoTranscoderOptions.h"
#include "VideoAnimation.h"

#include <winrt/Telegram.Native.h>
#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.Storage.Streams.h>

#include <d3d10.h>
#include <fileapifromapp.h>
#include <libyuv.h>

#include <cmath>
#include <string>
#include <vector>

extern "C"
{
#include <libavcodec/avcodec.h>
#include <libavformat/avformat.h>
#include <libavutil/audio_fifo.h>
#include <libavutil/display.h>
#include <libswscale/swscale.h>
}

using namespace winrt::Windows::Foundation::Numerics;

namespace winrt::Telegram::Native::Media::implementation
{
    namespace
    {
        constexpr int IoBufferSize = 64 * 1024;
        constexpr int AudioBitrate = 128000;
        constexpr int DefaultFrameRate = 30;
        constexpr int64_t Second = 10000000;

        [[noreturn]] void Fail(std::wstring_view stage, int error = 0)
        {
            std::wstring message{ stage };

            if (error < 0)
            {
                char buffer[AV_ERROR_MAX_STRING_SIZE]{};
                av_strerror(error, buffer, sizeof(buffer));

                message += L": ";
                message += winrt::to_hstring(buffer);
            }

            throw winrt::hresult_error(E_FAIL, message);
        }

        void Check(int result, std::wstring_view stage)
        {
            if (result < 0)
            {
                Fail(stage, result);
            }
        }

        // The source is read through a handle of our own, since a file the person picked is only
        // reachable through the FromApp functions, which ffmpeg's file protocol does not use.
        struct InputFile
        {
            HANDLE handle{ INVALID_HANDLE_VALUE };

            ~InputFile()
            {
                if (handle != INVALID_HANDLE_VALUE)
                {
                    CloseHandle(handle);
                }
            }
        };

        int ReadInput(void* opaque, uint8_t* buffer, int size)
        {
            auto file = static_cast<InputFile*>(opaque);
            DWORD read = 0;

            if (!ReadFile(file->handle, buffer, static_cast<DWORD>(size), &read, nullptr))
            {
                return AVERROR(EIO);
            }

            return read > 0 ? static_cast<int>(read) : AVERROR_EOF;
        }

        int64_t SeekInput(void* opaque, int64_t offset, int whence)
        {
            auto file = static_cast<InputFile*>(opaque);

            if (whence & AVSEEK_SIZE)
            {
                LARGE_INTEGER size{};
                return GetFileSizeEx(file->handle, &size) ? size.QuadPart : AVERROR(EIO);
            }

            DWORD method;

            switch (whence & ~AVSEEK_FORCE)
            {
            case SEEK_CUR:
                method = FILE_CURRENT;
                break;
            case SEEK_END:
                method = FILE_END;
                break;
            default:
                method = FILE_BEGIN;
                break;
            }

            LARGE_INTEGER distance{};
            LARGE_INTEGER position{};
            distance.QuadPart = offset;

            return SetFilePointerEx(file->handle, distance, &position, method) ? position.QuadPart : AVERROR(EIO);
        }

        // A packet written before the MP4's header, which waits for the encoder's first keyframe.
        struct PendingPacket
        {
            AVPacket* packet;
            AVRational timeBase;
        };

        // Everything ffmpeg hands out, freed in the right order however the transcode ends.
        struct Session
        {
            InputFile input;
            AVIOContext* inputIo{ nullptr };
            AVFormatContext* inputFormat{ nullptr };
            AVFormatContext* outputFormat{ nullptr };

            AVCodecContext* videoDecoder{ nullptr };
            AVCodecContext* audioDecoder{ nullptr };
            AVCodecContext* audioEncoder{ nullptr };

            AVAudioFifo* fifo{ nullptr };
            SwsContext* scaler{ nullptr };

            AVPacket* packet{ nullptr };
            AVPacket* encoded{ nullptr };
            AVFrame* frame{ nullptr };
            AVFrame* picture{ nullptr };
            AVFrame* samples{ nullptr };

            std::vector<PendingPacket> pending;

            ~Session()
            {
                for (auto& item : pending)
                {
                    av_packet_free(&item.packet);
                }

                av_frame_free(&samples);
                av_frame_free(&picture);
                av_frame_free(&frame);
                av_packet_free(&encoded);
                av_packet_free(&packet);

                sws_freeContext(scaler);

                if (fifo != nullptr)
                {
                    av_audio_fifo_free(fifo);
                }

                avcodec_free_context(&audioEncoder);
                avcodec_free_context(&audioDecoder);
                avcodec_free_context(&videoDecoder);

                if (outputFormat != nullptr)
                {
                    if (outputFormat->pb != nullptr)
                    {
                        avio_closep(&outputFormat->pb);
                    }

                    avformat_free_context(outputFormat);
                }

                if (inputFormat != nullptr)
                {
                    // Opened with our own pb, which avformat_close_input leaves for us.
                    avformat_close_input(&inputFormat);
                }

                if (inputIo != nullptr)
                {
                    av_freep(&inputIo->buffer);
                    avio_context_free(&inputIo);
                }
            }
        };

        // Every colour white, every alpha kept: premultiplied, white is the alpha in each channel.
        void Whiten(uint8_t* pixels, size_t count)
        {
            for (size_t i = 0; i < count; i += 4)
            {
                pixels[i] = pixels[i + 1] = pixels[i + 2] = pixels[i + 3];
            }
        }

        // A layer ready to draw: its bitmap, and for an animation what it has got to.
        struct Overlay
        {
            winrt::Telegram::Native::LottieAnimation lottie{ nullptr };
            winrt::Telegram::Native::VideoAnimation video{ nullptr };

            winrt::com_ptr<ID2D1Bitmap1> bitmap;
            D2D1_MATRIX_3X2_F transform{};
            D2D1_INTERPOLATION_MODE interpolation{ D2D1_INTERPOLATION_MODE_LINEAR };
            uint32_t width{ 0 };
            uint32_t height{ 0 };
            bool white{ false };

            D2D1_RECT_F crop{};
            bool cropped{ false };

            winrt::Windows::Storage::Streams::Buffer buffer{ nullptr };
            std::vector<uint8_t> pixels;

            int32_t frames{ 0 };
            int32_t frame{ -1 };
            double frameRate{ 0 };

            // A video goes by its frames' timestamps, in seconds into its loop.
            double duration{ 0 };
            double position{ -1 };
            double target{ 0 };
        };

        // A device of its own: the app's Direct2DDevice belongs to a view and draws for it, and a
        // D2D context takes one BeginDraw at a time. It supports video, so a hardware encoder can
        // take its textures, and is protected for the encoder's own threads.
        struct Compositor
        {
            winrt::com_ptr<ID3D11Device> d3d;
            winrt::com_ptr<ID3D11DeviceContext> immediate;
            winrt::com_ptr<ID2D1DeviceContext> context;
            winrt::com_ptr<ID2D1Bitmap1> source;
            winrt::com_ptr<ID3D11Texture2D> targetTexture;
            winrt::com_ptr<ID2D1Bitmap1> target;
            winrt::com_ptr<ID2D1Bitmap1> readback;

            winrt::com_ptr<ID3D11VideoDevice> videoDevice;
            winrt::com_ptr<ID3D11VideoContext> videoContext;
            winrt::com_ptr<ID3D11VideoProcessorEnumerator> enumerator;
            winrt::com_ptr<ID3D11VideoProcessor> processor;
            winrt::com_ptr<ID3D11VideoProcessorInputView> inputView;

            // Only with RoundBlur: the sharp frame, drawn first so that it can be both shrunk
            // and kept inside the circle.
            winrt::com_ptr<ID2D1Bitmap1> frame;
            winrt::com_ptr<ID2D1Bitmap1> reduced;
            winrt::com_ptr<ID2D1Bitmap1> blurred;
            winrt::com_ptr<ID2D1Effect> border;
            winrt::com_ptr<ID2D1Effect> blur;
            winrt::com_ptr<ID2D1BitmapBrush1> sharp;
            winrt::com_ptr<ID2D1SolidColorBrush> shade;

            // What the frame does not cover: one colour, or a gradient when the two ends differ.
            D2D1_COLOR_F clear{ 0, 0, 0, 1 };
            winrt::com_ptr<ID2D1LinearGradientBrush> background;

            bool video{ false };
            uint32_t width{ 0 };
            uint32_t height{ 0 };

            std::vector<Overlay> overlays;

            void Create(uint32_t sourceWidth, uint32_t sourceHeight, uint32_t outputWidth, uint32_t outputHeight)
            {
                width = outputWidth;
                height = outputHeight;

                const UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT | D3D11_CREATE_DEVICE_VIDEO_SUPPORT;

                HRESULT hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags,
                    nullptr, 0, D3D11_SDK_VERSION, d3d.put(), nullptr, immediate.put());

                video = SUCCEEDED(hr);

                if (FAILED(hr))
                {
                    // No video support: the frames are encoded in software.
                    hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                        nullptr, 0, D3D11_SDK_VERSION, d3d.put(), nullptr, immediate.put());
                }

                if (FAILED(hr))
                {
                    winrt::check_hresult(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                        nullptr, 0, D3D11_SDK_VERSION, d3d.put(), nullptr, immediate.put()));
                }

                if (auto multithread = d3d.try_as<ID3D10Multithread>())
                {
                    multithread->SetMultithreadProtected(TRUE);
                }

                winrt::com_ptr<ID2D1Factory1> factory;
                D2D1_FACTORY_OPTIONS options{};
                winrt::check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_MULTI_THREADED, __uuidof(ID2D1Factory1), &options, factory.put_void()));

                winrt::com_ptr<ID2D1Device> device;
                winrt::check_hresult(factory->CreateDevice(d3d.as<IDXGIDevice>().get(), device.put()));
                winrt::check_hresult(device->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, context.put()));

                auto format = D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED);

                auto plain = D2D1::BitmapProperties1(D2D1_BITMAP_OPTIONS_NONE, format);
                winrt::check_hresult(context->CreateBitmap(D2D1::SizeU(sourceWidth, sourceHeight), nullptr, 0, plain, source.put()));

                // A texture of our own, so that the video processor can read what D2D drew.
                D3D11_TEXTURE2D_DESC description{};
                description.Width = width;
                description.Height = height;
                description.MipLevels = 1;
                description.ArraySize = 1;
                description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
                description.SampleDesc.Count = 1;
                description.Usage = D3D11_USAGE_DEFAULT;
                description.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
                winrt::check_hresult(d3d->CreateTexture2D(&description, nullptr, targetTexture.put()));

                auto drawable = D2D1::BitmapProperties1(D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW, format);
                winrt::check_hresult(context->CreateBitmapFromDxgiSurface(targetTexture.as<IDXGISurface>().get(), drawable, target.put()));

                auto readable = D2D1::BitmapProperties1(D2D1_BITMAP_OPTIONS_CPU_READ | D2D1_BITMAP_OPTIONS_CANNOT_DRAW, format);
                winrt::check_hresult(context->CreateBitmap(D2D1::SizeU(width, height), nullptr, 0, readable, readback.put()));

                context->SetTarget(target.get());
                context->SetUnitMode(D2D1_UNIT_MODE_PIXELS);
            }

            // The GPU's conversion to NV12 for a hardware encoder, from full range RGB to studio
            // range BT.709. False when the device has none.
            bool CreateConverter()
            {
                if (!video)
                {
                    return false;
                }

                try
                {
                    videoDevice = d3d.as<ID3D11VideoDevice>();
                    videoContext = immediate.as<ID3D11VideoContext>();

                    D3D11_VIDEO_PROCESSOR_CONTENT_DESC content{};
                    content.InputFrameFormat = D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE;
                    content.InputWidth = width;
                    content.InputHeight = height;
                    content.OutputWidth = width;
                    content.OutputHeight = height;
                    content.Usage = D3D11_VIDEO_USAGE_OPTIMAL_QUALITY;

                    winrt::check_hresult(videoDevice->CreateVideoProcessorEnumerator(&content, enumerator.put()));
                    winrt::check_hresult(videoDevice->CreateVideoProcessor(enumerator.get(), 0, processor.put()));

                    D3D11_VIDEO_PROCESSOR_INPUT_VIEW_DESC inputDescription{};
                    inputDescription.ViewDimension = D3D11_VPIV_DIMENSION_TEXTURE2D;
                    winrt::check_hresult(videoDevice->CreateVideoProcessorInputView(targetTexture.get(), enumerator.get(), &inputDescription, inputView.put()));

                    if (auto videoContext1 = videoContext.try_as<ID3D11VideoContext1>())
                    {
                        videoContext1->VideoProcessorSetStreamColorSpace1(processor.get(), 0, DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709);
                        videoContext1->VideoProcessorSetOutputColorSpace1(processor.get(), DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P709);
                    }
                    else
                    {
                        D3D11_VIDEO_PROCESSOR_COLOR_SPACE input{};
                        input.RGB_Range = 0;

                        D3D11_VIDEO_PROCESSOR_COLOR_SPACE output{};
                        output.YCbCr_Matrix = 1;
                        output.Nominal_Range = D3D11_VIDEO_PROCESSOR_NOMINAL_RANGE_16_235;

                        videoContext->VideoProcessorSetStreamColorSpace(processor.get(), 0, &input);
                        videoContext->VideoProcessorSetOutputColorSpace(processor.get(), &output);
                    }

                    // No frame rate conversion and no auto processing: one frame in, one out.
                    videoContext->VideoProcessorSetStreamFrameFormat(processor.get(), 0, D3D11_VIDEO_FRAME_FORMAT_PROGRESSIVE);
                    videoContext->VideoProcessorSetStreamAutoProcessingMode(processor.get(), 0, FALSE);

                    return true;
                }
                catch (winrt::hresult_error const& error)
                {
                    LOGGER_WARNING(L"The GPU cannot convert frames for the encoder ({:#x})", static_cast<uint32_t>(error.code()));

                    inputView = nullptr;
                    processor = nullptr;
                    enumerator = nullptr;
                    videoContext = nullptr;
                    videoDevice = nullptr;

                    return false;
                }
            }

            void SetBackground(winrt::Windows::UI::Color top, winrt::Windows::UI::Color bottom)
            {
                auto ToColor = [](winrt::Windows::UI::Color color)
                    {
                        return D2D1::ColorF(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
                    };

                clear = ToColor(top);

                if (top == bottom)
                {
                    return;
                }

                D2D1_GRADIENT_STOP stops[2]{ { 0, ToColor(top) }, { 1, ToColor(bottom) } };

                winrt::com_ptr<ID2D1GradientStopCollection> collection;
                winrt::check_hresult(context->CreateGradientStopCollection(stops, 2, collection.put()));

                auto properties = D2D1::LinearGradientBrushProperties(D2D1::Point2F(0, 0), D2D1::Point2F(0, static_cast<float>(height)));
                winrt::check_hresult(context->CreateLinearGradientBrush(properties, collection.get(), background.put()));
            }

            // A layer that cannot be read is left out of the video rather than failing it.
            void AddLayers(const std::vector<winrt::Telegram::Native::Media::VideoTranscoderLayer>& layers)
            {
                auto format = D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED);
                auto plain = D2D1::BitmapProperties1(D2D1_BITMAP_OPTIONS_NONE, format);

                for (auto const& layer : layers)
                {
                    Overlay item;
                    item.width = static_cast<uint32_t>(std::max(0, layer.PixelWidth()));
                    item.height = static_cast<uint32_t>(std::max(0, layer.PixelHeight()));
                    item.white = layer.White();

                    const auto matrix = layer.Transform();
                    item.transform = D2D1_MATRIX_3X2_F{ matrix.m11, matrix.m12, matrix.m21, matrix.m22, matrix.m31, matrix.m32 };

                    const auto crop = layer.Crop();
                    if (crop.Width > 0 && crop.Height > 0)
                    {
                        item.crop = D2D1::RectF(crop.X, crop.Y, crop.X + crop.Width, crop.Y + crop.Height);
                        item.cropped = true;
                    }

                    const size_t size = static_cast<size_t>(item.width) * item.height * 4;
                    if (size == 0)
                    {
                        continue;
                    }

                    if (auto pixels = layer.Pixels())
                    {
                        if (pixels.Length() < size)
                        {
                            continue;
                        }

                        if (FAILED(context->CreateBitmap(D2D1::SizeU(item.width, item.height), pixels.data(), item.width * 4, plain, item.bitmap.put())))
                        {
                            continue;
                        }
                    }
                    else if (auto lottie = layer.Lottie())
                    {
                        item.lottie = lottie;
                        item.frames = lottie.TotalFrame();
                        item.frameRate = lottie.FrameRate();

                        if (item.frames <= 0 || item.frameRate <= 0 || lottie.PixelWidth() != layer.PixelWidth() || lottie.PixelHeight() != layer.PixelHeight())
                        {
                            continue;
                        }

                        item.buffer = winrt::Windows::Storage::Streams::Buffer(static_cast<uint32_t>(size));
                        item.buffer.Length(static_cast<uint32_t>(size));
                        item.interpolation = D2D1_INTERPOLATION_MODE_HIGH_QUALITY_CUBIC;
                    }
                    else if (auto video = layer.Video())
                    {
                        // Not FrameRate, which is the rate the animation is polled at.
                        item.video = video;
                        item.duration = video.Duration() / 1000.0;
                        item.pixels.resize(size);
                        item.interpolation = D2D1_INTERPOLATION_MODE_HIGH_QUALITY_CUBIC;
                    }
                    else
                    {
                        continue;
                    }

                    if (!item.bitmap && FAILED(context->CreateBitmap(D2D1::SizeU(item.width, item.height), nullptr, 0, plain, item.bitmap.put())))
                    {
                        continue;
                    }

                    overlays.push_back(std::move(item));
                }
            }

            // The animations as they are at this time into the output.
            void UpdateLayers(double seconds)
            {
                for (auto& item : overlays)
                {
                    if (item.lottie)
                    {
                        const auto frame = static_cast<int32_t>(static_cast<int64_t>(std::floor(seconds * item.frameRate)) % item.frames);
                        if (frame == item.frame)
                        {
                            continue;
                        }

                        item.frame = frame;
                        item.lottie.RenderSync(item.buffer, frame);

                        auto data = item.buffer.data();
                        if (item.white)
                        {
                            Whiten(data, item.buffer.Length());
                        }

                        item.bitmap->CopyFromMemory(nullptr, data, item.width * 4);
                    }
                    else if (item.video)
                    {
                        constexpr double Epsilon = 0.001;

                        // Where the loop should be, and whether it has come round since the last
                        // frame: the decoder then has to come round too before it catches up.
                        const auto target = item.duration > 0 ? std::fmod(seconds, item.duration) : seconds;
                        auto wrapping = item.frame >= 0 && target + Epsilon < item.target;
                        item.target = target;

                        auto video = winrt::get_self<winrt::Telegram::Native::implementation::VideoAnimation>(item.video);
                        auto rendered = false;

                        // Frames are taken until one is at or past the time; the first comes at once.
                        // Bounded, in case the timestamps never get there.
                        for (int i = 0; i < 600 && (item.frame < 0 || wrapping || item.position + Epsilon < target); i++)
                        {
                            double position = 0;
                            bool completed = false;

                            if (!video->RenderSync(item.pixels.data(), static_cast<int32_t>(item.width), static_cast<int32_t>(item.height), false, position, completed))
                            {
                                break;
                            }

                            if (position + Epsilon < item.position)
                            {
                                wrapping = false;
                            }

                            item.position = position;
                            item.frame = 0;
                            rendered = true;
                        }

                        if (rendered)
                        {
                            if (item.white)
                            {
                                Whiten(item.pixels.data(), item.pixels.size());
                            }

                            item.bitmap->CopyFromMemory(nullptr, item.pixels.data(), item.width * 4);
                        }
                    }
                }
            }

            // The frame shrunk this far, blurred, and drawn at a quarter of its brightness: the
            // official apps' corners, which their white marks stay readable on.
            static constexpr uint32_t RoundBlurSide = 48;

            // The standard deviation of the official apps' 9-tap kernel, in pixels of the shrunk
            // frame.
            static constexpr float RoundBlurDeviation = 1.7f;

            void CreateRoundBlur()
            {
                auto format = D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED);
                auto drawable = D2D1::BitmapProperties1(D2D1_BITMAP_OPTIONS_TARGET, format);

                winrt::check_hresult(context->CreateBitmap(D2D1::SizeU(width, height), nullptr, 0, drawable, frame.put()));
                winrt::check_hresult(context->CreateBitmap(D2D1::SizeU(RoundBlurSide, RoundBlurSide), nullptr, 0, drawable, reduced.put()));
                winrt::check_hresult(context->CreateBitmap(D2D1::SizeU(RoundBlurSide, RoundBlurSide), nullptr, 0, drawable, blurred.put()));

                // Clamped, so that the edges blur with their own colour rather than with transparency.
                winrt::check_hresult(context->CreateEffect(CLSID_D2D1Border, border.put()));
                border->SetInput(0, reduced.get());
                winrt::check_hresult(border->SetValue(D2D1_BORDER_PROP_EDGE_MODE_X, D2D1_BORDER_EDGE_MODE_CLAMP));
                winrt::check_hresult(border->SetValue(D2D1_BORDER_PROP_EDGE_MODE_Y, D2D1_BORDER_EDGE_MODE_CLAMP));

                winrt::check_hresult(context->CreateEffect(CLSID_D2D1GaussianBlur, blur.put()));
                blur->SetInputEffect(0, border.get());
                winrt::check_hresult(blur->SetValue(D2D1_GAUSSIANBLUR_PROP_STANDARD_DEVIATION, RoundBlurDeviation));
                winrt::check_hresult(blur->SetValue(D2D1_GAUSSIANBLUR_PROP_OPTIMIZATION, D2D1_GAUSSIANBLUR_OPTIMIZATION_QUALITY));
                winrt::check_hresult(blur->SetValue(D2D1_GAUSSIANBLUR_PROP_BORDER_MODE, D2D1_BORDER_MODE_HARD));

                winrt::check_hresult(context->CreateBitmapBrush(frame.get(), sharp.put()));
                winrt::check_hresult(context->CreateSolidColorBrush(D2D1::ColorF(0, 0, 0, 0.75f), shade.put()));
            }

            // Draws a BGRA frame through the transform onto the background.
            void Draw(const uint8_t* pixels, uint32_t stride, const D2D1_MATRIX_3X2_F& transform)
            {
                winrt::check_hresult(source->CopyFromMemory(nullptr, pixels, stride));

                context->BeginDraw();
                context->SetTarget(frame ? frame.get() : target.get());
                context->Clear(clear);

                if (background)
                {
                    context->SetTransform(D2D1::Matrix3x2F::Identity());
                    context->FillRectangle(D2D1::RectF(0, 0, static_cast<float>(width), static_cast<float>(height)), background.get());
                }

                context->SetTransform(transform);
                context->DrawBitmap(source.get(), nullptr, 1.0f, D2D1_INTERPOLATION_MODE_HIGH_QUALITY_CUBIC, nullptr, nullptr);

                if (frame)
                {
                    const auto side = static_cast<float>(RoundBlurSide);
                    const auto bounds = D2D1::RectF(0, 0, side, side);

                    context->SetTarget(reduced.get());
                    context->SetTransform(D2D1::Matrix3x2F::Scale(side / width, side / height));
                    context->DrawBitmap(frame.get(), nullptr, 1.0f, D2D1_INTERPOLATION_MODE_HIGH_QUALITY_CUBIC, nullptr, nullptr);

                    context->SetTarget(blurred.get());
                    context->SetTransform(D2D1::Matrix3x2F::Identity());
                    context->DrawImage(blur.get(), nullptr, &bounds, D2D1_INTERPOLATION_MODE_LINEAR, D2D1_COMPOSITE_MODE_SOURCE_COPY);

                    context->SetTarget(target.get());
                    context->SetTransform(D2D1::Matrix3x2F::Scale(width / side, height / side));
                    context->DrawBitmap(blurred.get(), nullptr, 1.0f, D2D1_INTERPOLATION_MODE_LINEAR, nullptr, nullptr);

                    context->SetTransform(D2D1::Matrix3x2F::Identity());
                    context->FillRectangle(D2D1::RectF(0, 0, static_cast<float>(width), static_cast<float>(height)), shade.get());
                    context->FillEllipse(D2D1::Ellipse(D2D1::Point2F(width / 2.0f, height / 2.0f), width / 2.0f, height / 2.0f), sharp.get());
                }

                for (auto const& item : overlays)
                {
                    const auto crop = item.cropped ? &item.crop : nullptr;

                    context->SetTransform(item.transform);
                    context->DrawBitmap(item.bitmap.get(), crop, 1.0f, item.interpolation, crop, nullptr);
                }

                context->SetTransform(D2D1::Matrix3x2F::Identity());
                winrt::check_hresult(context->EndDraw());
            }

            // A new texture each frame: an asynchronous encoder may still hold the last one.
            winrt::com_ptr<ID3D11Texture2D> ConvertToNV12()
            {
                D3D11_TEXTURE2D_DESC description{};
                description.Width = width;
                description.Height = height;
                description.MipLevels = 1;
                description.ArraySize = 1;
                description.Format = DXGI_FORMAT_NV12;
                description.SampleDesc.Count = 1;
                description.Usage = D3D11_USAGE_DEFAULT;
                description.BindFlags = D3D11_BIND_RENDER_TARGET;

                winrt::com_ptr<ID3D11Texture2D> texture;
                winrt::check_hresult(d3d->CreateTexture2D(&description, nullptr, texture.put()));

                D3D11_VIDEO_PROCESSOR_OUTPUT_VIEW_DESC outputDescription{};
                outputDescription.ViewDimension = D3D11_VPOV_DIMENSION_TEXTURE2D;

                winrt::com_ptr<ID3D11VideoProcessorOutputView> outputView;
                winrt::check_hresult(videoDevice->CreateVideoProcessorOutputView(texture.get(), enumerator.get(), &outputDescription, outputView.put()));

                D3D11_VIDEO_PROCESSOR_STREAM stream{};
                stream.Enable = TRUE;
                stream.pInputSurface = inputView.get();

                winrt::check_hresult(videoContext->VideoProcessorBlt(processor.get(), outputView.get(), 0, 1, &stream));
                return texture;
            }

            // What D2D drew, where the CPU can read it, for the software encoder.
            void CopyToReadback()
            {
                D2D1_POINT_2U origin{ 0, 0 };
                D2D1_RECT_U rect{ 0, 0, width, height };
                winrt::check_hresult(readback->CopyFromBitmap(&origin, target.get(), &rect));
            }
        };

        // Clockwise, as the source's display matrix asks it to be shown.
        int GetRotation(const AVStream* stream)
        {
            const AVPacketSideData* matrix = av_packet_side_data_get(
                stream->codecpar->coded_side_data, stream->codecpar->nb_coded_side_data, AV_PKT_DATA_DISPLAYMATRIX);

            if (matrix == nullptr)
            {
                return 0;
            }

            auto degrees = static_cast<int>(std::lround(-av_display_rotation_get(reinterpret_cast<const int32_t*>(matrix->data))));
            return ((degrees % 360) + 360) % 360 / 90 * 90;
        }

        // The decoded frame's pixels to the upright ones the transform starts from.
        float3x2 GetOrientation(int rotation, float width, float height)
        {
            switch (rotation)
            {
            case 90:
                return make_float3x2_rotation(3.14159265f / 2) * make_float3x2_translation(height, 0);
            case 180:
                return make_float3x2_rotation(3.14159265f) * make_float3x2_translation(width, height);
            case 270:
                return make_float3x2_rotation(3.14159265f * 3 / 2) * make_float3x2_translation(0, width);
            default:
                return float3x2::identity();
            }
        }

        bool IsAacSampleRate(int rate)
        {
            switch (rate)
            {
            case 96000:
            case 88200:
            case 64000:
            case 48000:
            case 44100:
            case 32000:
            case 24000:
            case 22050:
            case 16000:
            case 12000:
            case 11025:
            case 8000:
            case 7350:
                return true;
            default:
                return false;
            }
        }

        // One sample as a float, whatever the decoder produced: done here rather than by linking
        // swresample and shipping another DLL, as SoundPlayer does too.
        float ReadSample(const AVFrame* frame, int channel, int index)
        {
            const auto format = static_cast<AVSampleFormat>(frame->format);
            const auto packed = av_get_packed_sample_fmt(format);
            const auto planar = av_sample_fmt_is_planar(format) != 0;
            const auto channels = frame->ch_layout.nb_channels;

            const auto data = frame->extended_data[planar ? channel : 0];
            const auto position = planar ? index : index * channels + channel;

            switch (packed)
            {
            case AV_SAMPLE_FMT_U8:
                return (reinterpret_cast<const uint8_t*>(data)[position] - 128) / 128.0f;
            case AV_SAMPLE_FMT_S16:
                return reinterpret_cast<const int16_t*>(data)[position] / 32768.0f;
            case AV_SAMPLE_FMT_S32:
                return reinterpret_cast<const int32_t*>(data)[position] / 2147483648.0f;
            case AV_SAMPLE_FMT_FLT:
                return reinterpret_cast<const float*>(data)[position];
            case AV_SAMPLE_FMT_DBL:
                return static_cast<float>(reinterpret_cast<const double*>(data)[position]);
            default:
                return 0;
            }
        }

        // The SPS and PPS from a keyframe, still Annex B: the MP4 writer turns them into its avcC,
        // as it does the frames that follow.
        std::vector<uint8_t> GetParameterSets(const uint8_t* data, size_t size)
        {
            std::vector<uint8_t> sets;
            size_t index = 0;

            auto next = [&](size_t from) -> size_t
            {
                for (size_t i = from; i + 3 <= size; i++)
                {
                    if (data[i] == 0 && data[i + 1] == 0 && data[i + 2] == 1)
                    {
                        return i;
                    }
                }

                return size;
            };

            index = next(0);

            while (index < size)
            {
                const size_t start = index + 3;
                size_t end = next(start);

                const size_t following = end;

                // A four byte start code leaves its leading zero on the unit before it.
                while (end > start && data[end - 1] == 0)
                {
                    end--;
                }

                if (start < end)
                {
                    const uint8_t type = data[start] & 0x1F;
                    if (type == 7 || type == 8)
                    {
                        static const uint8_t startCode[]{ 0, 0, 0, 1 };

                        sets.insert(sets.end(), startCode, startCode + 4);
                        sets.insert(sets.end(), data + start, data + end);
                    }
                }

                index = following;
            }

            return sets;
        }
    }

    void VideoTranscoder::Cancel()
    {
        m_canceled = true;
    }

    void VideoTranscoder::Transcode(winrt::Telegram::Native::Media::VideoTranscoderOptions const& options, winrt::Telegram::Native::Media::VideoTranscoderProgressHandler const& progress)
    {
        Session session;

        const int64_t start = std::max<int64_t>(0, options.StartTime());
        const int64_t requestedStop = options.StopTime();

        const int width = options.OutputWidth();
        const int height = options.OutputHeight();

        if (width <= 0 || height <= 0 || width % 2 != 0 || height % 2 != 0)
        {
            Fail(L"The output size must be positive and even");
        }

        const auto self = winrt::get_self<implementation::VideoTranscoderOptions>(options);

        // A still stands in for the video: nothing is read or decoded, and there is no audio.
        const auto still = self->StillPixels();

        int videoIndex = -1;
        int audioIndex = -1;
        AVStream* videoIn = nullptr;
        AVStream* audioIn = nullptr;
        int sourceWidth = 0;
        int sourceHeight = 0;
        int frameRate = DefaultFrameRate;
        double length = 1;

        if (still)
        {
            sourceWidth = self->StillWidth();
            sourceHeight = self->StillHeight();

            if (sourceWidth <= 0 || sourceHeight <= 0 || still.Length() < static_cast<uint32_t>(sourceWidth) * sourceHeight * 4)
            {
                Fail(L"The still's pixels do not match its size");
            }

            if (self->StillDuration() <= 0)
            {
                Fail(L"The still has no duration");
            }

            frameRate = options.MaxFrameRate() > 0 ? options.MaxFrameRate() : DefaultFrameRate;
            length = static_cast<double>(self->StillDuration());
        }
        else
        {
            // Input.
            session.input.handle = CreateFile2FromAppW(options.InputPath().c_str(), GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, OPEN_EXISTING, nullptr);

            if (session.input.handle == INVALID_HANDLE_VALUE)
            {
                Fail(L"Opening the source");
            }

            auto ioBuffer = static_cast<uint8_t*>(av_malloc(IoBufferSize));
            if (ioBuffer == nullptr)
            {
                Fail(L"Allocating the source's buffer", AVERROR(ENOMEM));
            }

            session.inputIo = avio_alloc_context(ioBuffer, IoBufferSize, 0, &session.input, ReadInput, nullptr, SeekInput);
            if (session.inputIo == nullptr)
            {
                av_free(ioBuffer);
                Fail(L"Allocating the source's reader", AVERROR(ENOMEM));
            }

            session.inputFormat = avformat_alloc_context();
            if (session.inputFormat == nullptr)
            {
                Fail(L"Allocating the demuxer", AVERROR(ENOMEM));
            }

            session.inputFormat->pb = session.inputIo;

            // The name is never opened, but the probe takes its extension as a hint.
            auto inputName = winrt::to_string(options.InputPath());
            Check(avformat_open_input(&session.inputFormat, inputName.c_str(), nullptr, nullptr), L"Demuxing the source");
            Check(avformat_find_stream_info(session.inputFormat, nullptr), L"Reading the source's streams");

            videoIndex = av_find_best_stream(session.inputFormat, AVMEDIA_TYPE_VIDEO, -1, -1, nullptr, 0);
            if (videoIndex < 0)
            {
                Fail(L"The source has no video", videoIndex);
            }

            audioIndex = options.Mute()
                ? -1
                : av_find_best_stream(session.inputFormat, AVMEDIA_TYPE_AUDIO, -1, videoIndex, nullptr, 0);

            videoIn = session.inputFormat->streams[videoIndex];
            audioIn = audioIndex >= 0 ? session.inputFormat->streams[audioIndex] : nullptr;

            const int64_t sourceDuration = session.inputFormat->duration != AV_NOPTS_VALUE
                ? session.inputFormat->duration / 1000
                : 0;

            const int64_t stop = requestedStop > 0 ? requestedStop : sourceDuration;
            length = static_cast<double>(std::max<int64_t>(1, stop - start));

            // Video decoder.
            const AVCodec* videoCodec = avcodec_find_decoder(videoIn->codecpar->codec_id);
            if (videoCodec == nullptr)
            {
                Fail(L"No decoder for the source's video");
            }

            session.videoDecoder = avcodec_alloc_context3(videoCodec);
            if (session.videoDecoder == nullptr)
            {
                Fail(L"Allocating the video decoder", AVERROR(ENOMEM));
            }

            Check(avcodec_parameters_to_context(session.videoDecoder, videoIn->codecpar), L"Configuring the video decoder");
            session.videoDecoder->thread_count = 0;
            Check(avcodec_open2(session.videoDecoder, videoCodec, nullptr), L"Opening the video decoder");

            sourceWidth = session.videoDecoder->width;
            sourceHeight = session.videoDecoder->height;

            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                Fail(L"The source's video has no size");
            }

            // The source's own rate up to the cap: each frame then lands on a slot of it, and frames
            // that come too quick for one are dropped.
            AVRational sourceRate = videoIn->avg_frame_rate.num > 0 && videoIn->avg_frame_rate.den > 0
                ? videoIn->avg_frame_rate
                : videoIn->r_frame_rate;

            frameRate = sourceRate.num > 0 && sourceRate.den > 0
                ? static_cast<int>(std::lround(av_q2d(sourceRate)))
                : DefaultFrameRate;

            frameRate = std::clamp(frameRate, 1, options.MaxFrameRate() > 0 ? options.MaxFrameRate() : 60);
        }

        const AVRational frameTimeBase{ 1, frameRate };

        // Output.
        auto outputName = winrt::to_string(options.OutputPath());
        Check(avformat_alloc_output_context2(&session.outputFormat, nullptr, "mp4", outputName.c_str()), L"Creating the MP4");

        // The video stream is described now and given its parameter sets with the first keyframe.
        AVStream* videoOut = avformat_new_stream(session.outputFormat, nullptr);
        if (videoOut == nullptr)
        {
            Fail(L"Adding the video stream", AVERROR(ENOMEM));
        }

        videoOut->codecpar->codec_type = AVMEDIA_TYPE_VIDEO;
        videoOut->codecpar->codec_id = AV_CODEC_ID_H264;
        videoOut->codecpar->width = width;
        videoOut->codecpar->height = height;
        videoOut->codecpar->bit_rate = options.VideoBitrate();
        videoOut->time_base = frameTimeBase;
        videoOut->avg_frame_rate = AVRational{ frameRate, 1 };

        // Audio: AAC is copied packet by packet, anything else decoded and encoded again.
        AVStream* audioOut = nullptr;
        const bool copyAudio = audioIn != nullptr && audioIn->codecpar->codec_id == AV_CODEC_ID_AAC;

        if (audioIn != nullptr)
        {
            audioOut = avformat_new_stream(session.outputFormat, nullptr);
            if (audioOut == nullptr)
            {
                Fail(L"Adding the audio stream", AVERROR(ENOMEM));
            }

            if (copyAudio)
            {
                Check(avcodec_parameters_copy(audioOut->codecpar, audioIn->codecpar), L"Describing the audio stream");
                audioOut->codecpar->codec_tag = 0;
                audioOut->time_base = audioIn->time_base;
            }
            else
            {
                const AVCodec* audioCodec = avcodec_find_decoder(audioIn->codecpar->codec_id);
                if (audioCodec == nullptr)
                {
                    Fail(L"No decoder for the source's audio");
                }

                session.audioDecoder = avcodec_alloc_context3(audioCodec);
                if (session.audioDecoder == nullptr)
                {
                    Fail(L"Allocating the audio decoder", AVERROR(ENOMEM));
                }

                Check(avcodec_parameters_to_context(session.audioDecoder, audioIn->codecpar), L"Configuring the audio decoder");
                Check(avcodec_open2(session.audioDecoder, audioCodec, nullptr), L"Opening the audio decoder");

                const int sampleRate = session.audioDecoder->sample_rate;
                if (!IsAacSampleRate(sampleRate))
                {
                    Fail(L"The source's audio sample rate cannot be AAC");
                }

                const AVCodec* aac = avcodec_find_encoder(AV_CODEC_ID_AAC);
                if (aac == nullptr)
                {
                    Fail(L"No AAC encoder");
                }

                session.audioEncoder = avcodec_alloc_context3(aac);
                if (session.audioEncoder == nullptr)
                {
                    Fail(L"Allocating the audio encoder", AVERROR(ENOMEM));
                }

                // More than two channels are folded into their first two.
                const int audioChannels = session.audioDecoder->ch_layout.nb_channels >= 2 ? 2 : 1;

                session.audioEncoder->sample_fmt = AV_SAMPLE_FMT_FLTP;
                session.audioEncoder->sample_rate = sampleRate;
                session.audioEncoder->bit_rate = AudioBitrate;
                session.audioEncoder->time_base = AVRational{ 1, sampleRate };
                av_channel_layout_default(&session.audioEncoder->ch_layout, audioChannels);

                if (session.outputFormat->oformat->flags & AVFMT_GLOBALHEADER)
                {
                    session.audioEncoder->flags |= AV_CODEC_FLAG_GLOBAL_HEADER;
                }

                Check(avcodec_open2(session.audioEncoder, aac, nullptr), L"Opening the AAC encoder");
                Check(avcodec_parameters_from_context(audioOut->codecpar, session.audioEncoder), L"Describing the audio stream");
                audioOut->time_base = session.audioEncoder->time_base;

                session.fifo = av_audio_fifo_alloc(AV_SAMPLE_FMT_FLTP, audioChannels, session.audioEncoder->frame_size);
                session.samples = av_frame_alloc();

                if (session.fifo == nullptr || session.samples == nullptr)
                {
                    Fail(L"Allocating the audio buffers", AVERROR(ENOMEM));
                }

                session.samples->format = AV_SAMPLE_FMT_FLTP;
                session.samples->sample_rate = sampleRate;
                session.samples->nb_samples = session.audioEncoder->frame_size;
                Check(av_channel_layout_copy(&session.samples->ch_layout, &session.audioEncoder->ch_layout), L"Describing the audio frames");
                Check(av_frame_get_buffer(session.samples, 0), L"Allocating the audio frames");
            }
        }

        // The muxer writes to TDLib's destination, which ffmpeg's file protocol can reach; it has
        // to, for faststart, which reopens the file to move the index to the front so that the
        // video can be streamed.
        Check(avio_open(&session.outputFormat->pb, outputName.c_str(), AVIO_FLAG_WRITE), L"Opening the output");

        session.packet = av_packet_alloc();
        session.encoded = av_packet_alloc();
        session.frame = av_frame_alloc();
        session.picture = av_frame_alloc();

        if (session.packet == nullptr || session.encoded == nullptr || session.frame == nullptr || session.picture == nullptr)
        {
            Fail(L"Allocating the frames", AVERROR(ENOMEM));
        }

        bool headerWritten = false;

        // Packets wait, in the time base they were made in, until the header is out.
        auto mux = [&](AVPacket* packet, AVRational timeBase)
        {
            if (!headerWritten)
            {
                AVPacket* copy = av_packet_alloc();
                if (copy == nullptr)
                {
                    Fail(L"Queueing a packet", AVERROR(ENOMEM));
                }

                av_packet_move_ref(copy, packet);
                session.pending.push_back(PendingPacket{ copy, timeBase });
                return;
            }

            av_packet_rescale_ts(packet, timeBase, session.outputFormat->streams[packet->stream_index]->time_base);
            Check(av_interleaved_write_frame(session.outputFormat, packet), L"Writing the MP4");
        };

        auto writeHeader = [&](const uint8_t* data, size_t size)
        {
            auto sets = GetParameterSets(data, size);
            if (sets.empty())
            {
                Fail(L"The encoder's first frame has no parameter sets");
            }

            videoOut->codecpar->extradata = static_cast<uint8_t*>(av_mallocz(sets.size() + AV_INPUT_BUFFER_PADDING_SIZE));
            if (videoOut->codecpar->extradata == nullptr)
            {
                Fail(L"Allocating the parameter sets", AVERROR(ENOMEM));
            }

            std::memcpy(videoOut->codecpar->extradata, sets.data(), sets.size());
            videoOut->codecpar->extradata_size = static_cast<int>(sets.size());

            AVDictionary* muxerOptions = nullptr;
            av_dict_set(&muxerOptions, "movflags", "+faststart", 0);

            const int header = avformat_write_header(session.outputFormat, &muxerOptions);
            av_dict_free(&muxerOptions);
            Check(header, L"Writing the MP4's header");

            headerWritten = true;

            for (auto& item : session.pending)
            {
                av_packet_rescale_ts(item.packet, item.timeBase, session.outputFormat->streams[item.packet->stream_index]->time_base);
                Check(av_interleaved_write_frame(session.outputFormat, item.packet), L"Writing the MP4");
                av_packet_free(&item.packet);
            }

            session.pending.clear();
        };

        Compositor compositor;
        compositor.Create(sourceWidth, sourceHeight, width, height);
        compositor.SetBackground(options.BackgroundTop(), options.BackgroundBottom());

        if (options.RoundBlur())
        {
            compositor.CreateRoundBlur();
        }
        compositor.AddLayers(winrt::get_self<implementation::VideoTranscoderOptions>(options)->Layers());

        // Video encoder: a hardware one when allowed and the GPU can convert frames for it, Windows'
        // software one otherwise.
        H264Encoder encoder;

        const bool converter = options.HardwareEncoding() && compositor.CreateConverter();

        encoder.Open(converter ? compositor.d3d.get() : nullptr, converter, width, height, frameRate, options.VideoBitrate(),
            [&](const uint8_t* data, size_t size, int64_t time, bool key)
            {
                if (!headerWritten)
                {
                    writeHeader(data, size);
                }

                Check(av_new_packet(session.encoded, static_cast<int>(size)), L"Allocating a frame's packet");
                std::memcpy(session.encoded->data, data, size);

                session.encoded->pts = std::llround(static_cast<double>(time) * frameRate / Second);
                session.encoded->dts = session.encoded->pts;
                session.encoded->duration = 1;
                session.encoded->stream_index = videoOut->index;

                if (key)
                {
                    session.encoded->flags |= AV_PKT_FLAG_KEY;
                }

                mux(session.encoded, frameTimeBase);
                av_packet_unref(session.encoded);
            });

        LOGGER_INFO(L"Transcoding {}x{} to {}x{} at {} fps with {} ({})",
            sourceWidth, sourceHeight, width, height, frameRate, encoder.Name(), encoder.IsHardware() ? L"hardware" : L"software");

        if (!encoder.IsHardware())
        {
            session.picture->format = AV_PIX_FMT_NV12;
            session.picture->width = width;
            session.picture->height = height;
            Check(av_frame_get_buffer(session.picture, 0), L"Allocating the encoder's frames");
        }

        const float3x2 transform = GetOrientation(still ? 0 : GetRotation(videoIn), static_cast<float>(sourceWidth), static_cast<float>(sourceHeight))
            * options.Transform();

        const D2D1_MATRIX_3X2_F drawTransform{ transform.m11, transform.m12, transform.m21, transform.m22, transform.m31, transform.m32 };

        // What each decoded frame is converted to; a still is drawn from its own pixels.
        std::vector<uint8_t> bgra(still ? 0 : static_cast<size_t>(sourceWidth) * sourceHeight * 4);

        if (start > 0 && !still)
        {
            // The key frame before the start; the frames up to it are decoded and dropped.
            av_seek_frame(session.inputFormat, -1, start * 1000, AVSEEK_FLAG_BACKWARD);
        }

        int64_t lastPts = -1;
        int64_t audioSamples = 0;
        double reported = -1;
        bool videoDone = false;
        bool audioDone = audioOut == nullptr;

        auto write = [&](AVCodecContext* encoderContext, AVStream* stream)
        {
            while (true)
            {
                const int result = avcodec_receive_packet(encoderContext, session.encoded);
                if (result == AVERROR(EAGAIN) || result == AVERROR_EOF)
                {
                    return;
                }

                Check(result, L"Encoding the audio");

                session.encoded->stream_index = stream->index;
                mux(session.encoded, encoderContext->time_base);
                av_packet_unref(session.encoded);
            }
        };

        auto encodeAudio = [&](AVFrame* input)
        {
            Check(avcodec_send_frame(session.audioEncoder, input), L"Encoding the audio");
            write(session.audioEncoder, audioOut);
        };

        // Draws a frame over the background and under the layers, encodes it, and reports how far
        // through the output it is.
        auto encodeFrame = [&](const uint8_t* pixels, uint32_t stride, int64_t pts, double done)
        {
            compositor.UpdateLayers(static_cast<double>(pts) / frameRate);
            compositor.Draw(pixels, stride, drawTransform);

            const int64_t sampleTime = pts * Second / frameRate;
            const int64_t sampleDuration = Second / frameRate;

            if (encoder.IsHardware())
            {
                auto texture = compositor.ConvertToNV12();
                encoder.EncodeTexture(texture.get(), sampleTime, sampleDuration);
            }
            else
            {
                compositor.CopyToReadback();
                Check(av_frame_make_writable(session.picture), L"Preparing the encoder's frame");

                D2D1_MAPPED_RECT mapped{};
                winrt::check_hresult(compositor.readback->Map(D2D1_MAP_OPTIONS_READ, &mapped));

                // D2D's B8G8R8A8 is libyuv's ARGB: it names the bytes from the high end.
                libyuv::ARGBToNV12(mapped.bits, static_cast<int>(mapped.pitch),
                    session.picture->data[0], session.picture->linesize[0],
                    session.picture->data[1], session.picture->linesize[1],
                    width, height);

                winrt::check_hresult(compositor.readback->Unmap());

                encoder.EncodeMemory(session.picture->data[0], session.picture->linesize[0],
                    session.picture->data[1], session.picture->linesize[1], sampleTime, sampleDuration);
            }

            done = std::clamp(done, 0.0, 1.0);
            if (progress && done - reported >= 0.01)
            {
                reported = done;
                progress(done);
            }
        };

        auto receiveVideo = [&]()
        {
            while (true)
            {
                const int result = avcodec_receive_frame(session.videoDecoder, session.frame);
                if (result == AVERROR(EAGAIN) || result == AVERROR_EOF)
                {
                    return;
                }

                Check(result, L"Decoding the video");

                if (m_canceled)
                {
                    throw winrt::hresult_canceled();
                }

                auto timestamp = session.frame->best_effort_timestamp != AV_NOPTS_VALUE
                    ? session.frame->best_effort_timestamp
                    : session.frame->pts;

                const double time = timestamp != AV_NOPTS_VALUE
                    ? timestamp * av_q2d(videoIn->time_base) * 1000
                    : start + (lastPts + 1) * 1000.0 / frameRate;

                if (time < start || videoDone)
                {
                    av_frame_unref(session.frame);
                    continue;
                }

                if (requestedStop > 0 && time >= requestedStop)
                {
                    videoDone = true;
                    av_frame_unref(session.frame);
                    continue;
                }

                const int64_t pts = std::llround((time - start) * frameRate / 1000);
                if (pts <= lastPts)
                {
                    av_frame_unref(session.frame);
                    continue;
                }

                lastPts = pts;

                session.scaler = sws_getCachedContext(session.scaler,
                    session.frame->width, session.frame->height, static_cast<AVPixelFormat>(session.frame->format),
                    sourceWidth, sourceHeight, AV_PIX_FMT_BGRA, SWS_BILINEAR, nullptr, nullptr, nullptr);

                if (session.scaler == nullptr)
                {
                    Fail(L"Converting the video's pixels");
                }

                uint8_t* planes[4]{ bgra.data(), nullptr, nullptr, nullptr };
                int strides[4]{ sourceWidth * 4, 0, 0, 0 };
                sws_scale(session.scaler, session.frame->data, session.frame->linesize, 0, session.frame->height, planes, strides);

                av_frame_unref(session.frame);

                encodeFrame(bgra.data(), sourceWidth * 4, pts, (time - start) / length);
            }
        };

        // Whole AAC frames are taken from the queue; at the end, whatever is left.
        auto flushSamples = [&](bool last)
        {
            const int frameSize = session.audioEncoder->frame_size;

            while (av_audio_fifo_size(session.fifo) >= frameSize || (last && av_audio_fifo_size(session.fifo) > 0))
            {
                const int count = std::min(frameSize, av_audio_fifo_size(session.fifo));

                Check(av_frame_make_writable(session.samples), L"Preparing the audio frame");
                session.samples->nb_samples = count;

                if (av_audio_fifo_read(session.fifo, reinterpret_cast<void**>(session.samples->data), count) < count)
                {
                    Fail(L"Reading the audio queue");
                }

                session.samples->pts = audioSamples;
                audioSamples += count;

                encodeAudio(session.samples);
            }
        };

        std::vector<float> left;
        std::vector<float> right;

        auto receiveAudio = [&]()
        {
            while (true)
            {
                const int result = avcodec_receive_frame(session.audioDecoder, session.frame);
                if (result == AVERROR(EAGAIN) || result == AVERROR_EOF)
                {
                    return;
                }

                Check(result, L"Decoding the audio");

                const auto timestamp = session.frame->best_effort_timestamp;
                const double time = timestamp != AV_NOPTS_VALUE
                    ? timestamp * av_q2d(audioIn->time_base) * 1000
                    : start + audioSamples * 1000.0 / session.audioEncoder->sample_rate;

                if (time < start || audioDone)
                {
                    av_frame_unref(session.frame);
                    continue;
                }

                if (requestedStop > 0 && time >= requestedStop)
                {
                    audioDone = true;
                    av_frame_unref(session.frame);
                    continue;
                }

                const int count = session.frame->nb_samples;
                const int channels = session.frame->ch_layout.nb_channels;

                left.resize(count);
                right.resize(count);

                for (int index = 0; index < count; index++)
                {
                    left[index] = ReadSample(session.frame, 0, index);
                    right[index] = channels > 1 ? ReadSample(session.frame, 1, index) : left[index];
                }

                av_frame_unref(session.frame);

                void* planes[2]{ left.data(), right.data() };
                if (av_audio_fifo_write(session.fifo, planes, count) < count)
                {
                    Fail(L"Queueing the audio");
                }

                flushSamples(false);
            }
        };

        if (still)
        {
            const int64_t frames = std::max<int64_t>(1, std::llround(length * frameRate / 1000));

            for (int64_t pts = 0; pts < frames; pts++)
            {
                if (m_canceled)
                {
                    throw winrt::hresult_canceled();
                }

                encodeFrame(still.data(), static_cast<uint32_t>(sourceWidth) * 4, pts, static_cast<double>(pts + 1) / frames);
                lastPts = pts;
            }
        }
        else
        {
            while (!videoDone || !audioDone)
            {
                if (m_canceled)
                {
                    throw winrt::hresult_canceled();
                }

                const int read = av_read_frame(session.inputFormat, session.packet);
                if (read == AVERROR_EOF)
                {
                    break;
                }

                Check(read, L"Reading the source");

                if (session.packet->stream_index == videoIndex && !videoDone)
                {
                    // A damaged packet costs its frame, not the video.
                    if (avcodec_send_packet(session.videoDecoder, session.packet) >= 0)
                    {
                        receiveVideo();
                    }
                }
                else if (audioIn != nullptr && session.packet->stream_index == audioIndex && !audioDone)
                {
                    if (copyAudio)
                    {
                        const auto timestamp = session.packet->pts != AV_NOPTS_VALUE ? session.packet->pts : session.packet->dts;
                        const double time = timestamp != AV_NOPTS_VALUE ? timestamp * av_q2d(audioIn->time_base) * 1000 : start;

                        if (requestedStop > 0 && time >= requestedStop)
                        {
                            audioDone = true;
                        }
                        else if (time >= start)
                        {
                            const auto offset = av_rescale_q(start, AVRational{ 1, 1000 }, audioIn->time_base);

                            if (session.packet->pts != AV_NOPTS_VALUE)
                            {
                                session.packet->pts -= offset;
                            }

                            if (session.packet->dts != AV_NOPTS_VALUE)
                            {
                                session.packet->dts -= offset;
                            }

                            session.packet->stream_index = audioOut->index;
                            session.packet->pos = -1;

                            mux(session.packet, audioIn->time_base);
                        }
                    }
                    else if (avcodec_send_packet(session.audioDecoder, session.packet) >= 0)
                    {
                        receiveAudio();
                    }
                }

                av_packet_unref(session.packet);
            }

            // Drain the decoders, then the encoders.
            if (avcodec_send_packet(session.videoDecoder, nullptr) >= 0)
            {
                receiveVideo();
            }

            if (session.audioDecoder != nullptr && avcodec_send_packet(session.audioDecoder, nullptr) >= 0)
            {
                receiveAudio();
            }
        }

        if (lastPts < 0)
        {
            Fail(L"No frame of the video could be decoded");
        }

        encoder.Drain();

        if (session.audioEncoder != nullptr)
        {
            flushSamples(true);
            encodeAudio(nullptr);
        }

        if (!headerWritten)
        {
            Fail(L"The encoder produced no frames");
        }

        Check(av_write_trailer(session.outputFormat), L"Finishing the MP4");

        if (progress)
        {
            progress(1);
        }
    }
}
