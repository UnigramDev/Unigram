#pragma once

#include "pch.h"

namespace Widgets
{
    // One D3D11/D2D device, DirectWrite and WIC factory for the process, and the composition
    // graphics device built on it. Surfaces are drawn from every widget's clock thread, which is
    // why the D2D factory is multithreaded: it serialises the draws, nothing else does.
    class GraphicsDevice
    {
    public:
        static GraphicsDevice& Current();

        IDWriteFactory* DWrite() const { return m_dwrite.get(); }
        IWICImagingFactory* Wic() const { return m_wic.get(); }
        ID2D1Device* D2DDevice() const { return m_d2dDevice.get(); }

        // Held from BeginDraw to EndDraw: a composition graphics device allows one open draw at a
        // time across all its surfaces, and every widget draws from its own clock thread.
        std::mutex& DrawLock() { return m_drawLock; }

        // Per compositor: a CoreWindow view has its own, and a surface only draws for the one it came from.
        winrt::UIComposition::CompositionGraphicsDevice CompositionDevice(winrt::UIComposition::Compositor const& compositor);

    private:
        GraphicsDevice();

        std::mutex m_lock;
        std::mutex m_drawLock;
        winrt::com_ptr<ID2D1Factory1> m_d2dFactory;
        winrt::com_ptr<ID2D1Device> m_d2dDevice;
        winrt::com_ptr<IDWriteFactory> m_dwrite;
        winrt::com_ptr<IWICImagingFactory> m_wic;
        // A process has a compositor per view, so a handful at most; they are kept for its lifetime.
        std::vector<std::pair<winrt::UIComposition::Compositor, winrt::UIComposition::CompositionGraphicsDevice>> m_devices;
    };
}
