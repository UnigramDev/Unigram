#include "pch.h"
#include "WalletCardSurface.h"
#if __has_include("Graphics/WalletCardSurface.g.cpp")
#include "Graphics/WalletCardSurface.g.cpp"
#endif

#include "WalletCardEffect.h"

#include <cmath>
#include <mutex>
#include <vector>

using namespace winrt::GraphicsDirectX;
using namespace winrt::UIComposition;

namespace winrt::Telegram::Native::Graphics::implementation
{
    // Each factory takes the registration once; a view thread has a device, and a factory, of its
    // own.
    static std::mutex s_registeredLock;
    static std::vector<winrt::com_ptr<ID2D1Factory1>> s_registered;

    static bool EnsureRegistered(ID2D1DeviceContext* context)
    {
        winrt::com_ptr<ID2D1Factory> factory;
        context->GetFactory(factory.put());

        auto factory1 = factory.try_as<ID2D1Factory1>();
        if (factory1 == nullptr)
        {
            return false;
        }

        std::lock_guard const guard(s_registeredLock);

        for (auto const& registered : s_registered)
        {
            if (registered == factory1)
            {
                return true;
            }
        }

        if (FAILED(WalletCardEffect::Register(factory1.get())))
        {
            return false;
        }

        s_registered.push_back(factory1);
        return true;
    }

    static void CopyPalette(WalletCardPalette const& palette, float (&destination)[8][4])
    {
        winrt::Windows::Foundation::Numerics::float3 const colors[] =
        {
            palette.GradientFrom, palette.GradientTo, palette.Reflection, palette.Spark,
            palette.Bevel, palette.Edge, palette.Streak, palette.Ring
        };

        for (int i = 0; i < 8; i++)
        {
            destination[i][0] = colors[i].x;
            destination[i][1] = colors[i].y;
            destination[i][2] = colors[i].z;
            destination[i][3] = 1;
        }
    }

    WalletCardSurface::WalletCardSurface(CompositionGraphicsDevice const& device)
    {
        m_surface = device.CreateDrawingSurface2({ 1, 1 }, DirectXPixelFormat::B8G8R8A8UIntNormalized, DirectXAlphaMode::Premultiplied);
        m_interop = m_surface.as<winrt::UICompositionInterop::ICompositionDrawingSurfaceInterop>();
    }

    float WalletCardSurface::Margin()
    {
        return 2.0f;
    }

    CompositionDrawingSurface WalletCardSurface::Surface()
    {
        return m_surface;
    }

    float WalletCardSurface::HalfHeight()
    {
        return m_halfHeight / m_scale;
    }

    void WalletCardSurface::SetLayout(winrt::Windows::Foundation::Size const& cardSize, float cornerRadius, winrt::Windows::Foundation::Rect const& qrRect, float scale)
    {
        if (m_closed)
        {
            return;
        }

        m_cardSize = cardSize;
        m_cornerRadius = cornerRadius;
        m_qrRect = qrRect;
        m_scale = std::max(scale, 0.01f);

        auto halfWidth = static_cast<int32_t>(std::ceil((cardSize.Width + 2 * Margin()) * m_scale));
        auto halfHeight = static_cast<int32_t>(std::ceil((cardSize.Height + 2 * Margin()) * m_scale));

        if (halfWidth != m_halfWidth || halfHeight != m_halfHeight)
        {
            m_halfWidth = halfWidth;
            m_halfHeight = halfHeight;
            m_surface.Resize({ std::max(halfWidth, 1), std::max(halfHeight * 2, 1) });
        }
    }

    void WalletCardSurface::SetPalettes(WalletCardPalette const& from, WalletCardPalette const& to)
    {
        m_from = from;
        m_to = to;
    }

    bool WalletCardSurface::EnsureEffect(ID2D1DeviceContext* context)
    {
        winrt::com_ptr<ID2D1Device> device;
        context->GetDevice(device.put());

        if (device != m_device)
        {
            m_effect = nullptr;
            m_device = device;
        }

        if (m_effect == nullptr && (!EnsureRegistered(context) || FAILED(context->CreateEffect(CLSID_WalletCardEffect, m_effect.put()))))
        {
            m_effect = nullptr;
            return false;
        }

        return true;
    }

    bool WalletCardSurface::Draw(WalletCardFrame const& frame)
    {
        if (m_closed || m_halfWidth <= 0 || m_halfHeight <= 0)
        {
            return false;
        }

        winrt::com_ptr<ID2D1DeviceContext> context;
        POINT offset;

        if (FAILED(m_interop->BeginDraw(nullptr, __uuidof(ID2D1DeviceContext), context.put_void(), &offset)))
        {
            return false;
        }

        bool drawn = false;

        if (EnsureEffect(context.get()))
        {
            // The effect is drawn untransformed, at the session's offset, so that its scene
            // positions are surface pixels and the origins below say where the card is in them.
            auto margin = Margin() * m_scale;
            auto left = static_cast<float>(offset.x);
            auto top = static_cast<float>(offset.y);

            WalletCardConstants constants{};
            constants.CardSize[0] = m_cardSize.Width;
            constants.CardSize[1] = m_cardSize.Height;
            constants.Scale = m_scale;
            constants.CornerRadius = m_cornerRadius;
            constants.FaceOrigin[0] = left + margin;
            constants.FaceOrigin[1] = top + margin;
            constants.EdgeOrigin[0] = left + margin;
            constants.EdgeOrigin[1] = top + m_halfHeight + margin;
            constants.Time = frame.Time;
            constants.HighlightTilt[0] = frame.Pitch;
            constants.HighlightTilt[1] = frame.Yaw;
            constants.SurfaceTilt[0] = frame.Pitch;
            constants.SurfaceTilt[1] = frame.Yaw;
            constants.QrAlpha = 1;
            constants.QrRect[0] = m_qrRect.X;
            constants.QrRect[1] = m_qrRect.Y;
            constants.QrRect[2] = m_qrRect.Width;
            constants.QrRect[3] = m_qrRect.Height;
            constants.Bevel = 1;
            constants.Reveal[0] = frame.RevealCenter.x;
            constants.Reveal[1] = frame.RevealCenter.y;
            constants.Reveal[2] = frame.RevealRadius;
            constants.Reveal[3] = frame.RevealMix;
            CopyPalette(m_from, constants.PaletteFrom);
            CopyPalette(m_to, constants.PaletteTo);

            m_effect->SetValue(WalletCardProperty_Constants, D2D1_PROPERTY_TYPE_BLOB, reinterpret_cast<BYTE const*>(&constants), sizeof(constants));
            m_effect->SetValue(WalletCardProperty_Bounds, D2D1::Vector4F(left, top, left + m_halfWidth, top + m_halfHeight * 2));

            context->SetDpi(96, 96);
            context->SetTransform(D2D1::Matrix3x2F::Identity());
            context->Clear(D2D1::ColorF(0, 0, 0, 0));
            context->DrawImage(m_effect.get());

            drawn = true;
        }

        return SUCCEEDED(m_interop->EndDraw()) && drawn;
    }

    void WalletCardSurface::Close()
    {
        if (m_closed)
        {
            return;
        }

        m_closed = true;
        m_effect = nullptr;
        m_device = nullptr;
        m_interop = nullptr;

        if (m_surface)
        {
            m_surface.Close();
            m_surface = nullptr;
        }
    }
}
