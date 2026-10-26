#pragma once

#include "FrameSurface.g.h"

#include <mutex>

#include <winrt/Windows.Storage.Streams.h>

namespace winrt::Telegram::Native::implementation
{
    // An animation frame shown through Composition rather than XAML. A WriteableBitmap behind an
    // ImageBrush has to be invalidated on the UI thread, and each invalidation costs a full XAML
    // render pass; a frame drawn here reaches the screen with the compositor's next commit.
    struct FrameSurface : FrameSurfaceT<FrameSurface>
    {
        // pixelWidth and pixelHeight are the frame's, before rotation; the surface is the rotated
        // size.
        FrameSurface(winrt::UIComposition::CompositionDrawingSurface surface, winrt::com_ptr<ID2D1Factory1> const& factory, int32_t pixelWidth, int32_t pixelHeight, int32_t rotation);

        winrt::UIComposition::CompositionDrawingSurface Surface();

        // The frame is uploaded into a texture shared by the thread's surfaces and copied from there
        // into the surface's texture on the GPU: no Direct2D context is set up for the session. A
        // rotated frame is drawn through Direct2D instead, which turns it on the GPU.
        bool Draw(winrt::Windows::Storage::Streams::IBuffer buffer);

        void Close();

    private:
        bool DrawRotated(winrt::Windows::Storage::Streams::IBuffer const& buffer);

        std::mutex m_lock;
        winrt::UIComposition::CompositionDrawingSurface m_surface{ nullptr };
        winrt::com_ptr<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop> m_interop;

        // Reused across frames and recreated when the session's device is not the one it was made
        // on, which is how a device loss shows up here: the surface survives it, the bitmap not.
        winrt::com_ptr<ID2D1Bitmap1> m_bitmap;
        winrt::com_ptr<ID2D1Device> m_bitmapDevice;

        // Direct2D's own lock over the device it shares with this code: the immediate context is
        // not thread safe, and Direct2D uses it from other threads under this lock.
        winrt::com_ptr<ID2D1Multithread> m_multithread;

        int32_t m_pixelWidth;
        int32_t m_pixelHeight;
        int32_t m_rotation;
        bool m_closed = false;
    };
}
