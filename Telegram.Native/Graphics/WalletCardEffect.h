#pragma once

#include <d2d1_1.h>
#include <d2d1effectauthor.h>

#include <winrt/base.h>

namespace winrt::Telegram::Native::Graphics::implementation
{
    // {3B7E52A1-6C0D-4E8F-9A14-82D5C3F0B6E7}
    inline constexpr GUID CLSID_WalletCardEffect = { 0x3b7e52a1, 0x6c0d, 0x4e8f, { 0x9a, 0x14, 0x82, 0xd5, 0xc3, 0xf0, 0xb6, 0xe7 } };

    enum WalletCardProperty : UINT32
    {
        // WalletCardConstants, as a blob.
        WalletCardProperty_Constants = 0,

        // The output's left, top, right and bottom, in pixels.
        WalletCardProperty_Bounds = 1
    };

    // The constant buffer of WalletCard.hlsl, field for field.
    struct WalletCardConstants
    {
        float CardSize[2];
        float Scale;
        float CornerRadius;

        float FaceOrigin[2];
        float EdgeOrigin[2];

        float Time;
        float ReflectionRotation;
        float HighlightTilt[2];

        float SurfaceTilt[2];
        float QrAlpha;
        float QrBlur;

        float QrRect[4];

        float Bevel;
        float Padding[3];

        // Centre x and y, radius, mix: see WalletCardFrame.
        float Reveal[4];

        // WalletCardPalette's eight colours in order, each padded to four floats.
        float PaletteFrom[8][4];
        float PaletteTo[8][4];
    };

    static_assert(sizeof(WalletCardConstants) == 368, "The layout fxc reports for WalletCard.hlsl.");

    // The card drawn from nothing: no inputs, just the constants and the three textures the shader
    // samples, which live in the effect context and are shared by every instance on it.
    class WalletCardEffect final : public ID2D1EffectImpl, public ID2D1DrawTransform
    {
    public:
        static HRESULT Register(ID2D1Factory1* factory);

        HRESULT SetConstants(BYTE const* data, UINT32 dataSize);
        HRESULT GetConstants(BYTE* data, UINT32 dataSize, UINT32* actualSize) const;

        HRESULT SetBounds(D2D1_VECTOR_4F value);
        D2D1_VECTOR_4F GetBounds() const;

        // ID2D1EffectImpl
        IFACEMETHODIMP Initialize(ID2D1EffectContext* context, ID2D1TransformGraph* graph) override;
        IFACEMETHODIMP PrepareForRender(D2D1_CHANGE_TYPE changeType) override;
        IFACEMETHODIMP SetGraph(ID2D1TransformGraph* graph) override;

        // ID2D1DrawTransform
        IFACEMETHODIMP SetDrawInfo(ID2D1DrawInfo* drawInfo) override;

        // ID2D1Transform
        IFACEMETHODIMP MapOutputRectToInputRects(const D2D1_RECT_L* outputRect, D2D1_RECT_L* inputRects, UINT32 inputRectsCount) const override;
        IFACEMETHODIMP MapInputRectsToOutputRect(const D2D1_RECT_L* inputRects, const D2D1_RECT_L* inputOpaqueSubRects, UINT32 inputRectCount, D2D1_RECT_L* outputRect, D2D1_RECT_L* outputOpaqueSubRect) override;
        IFACEMETHODIMP MapInvalidRect(UINT32 inputIndex, D2D1_RECT_L invalidInputRect, D2D1_RECT_L* invalidOutputRect) const override;

        // ID2D1TransformNode
        IFACEMETHODIMP_(UINT32) GetInputCount() const override;

        // IUnknown
        IFACEMETHODIMP_(ULONG) AddRef() override;
        IFACEMETHODIMP_(ULONG) Release() override;
        IFACEMETHODIMP QueryInterface(REFIID riid, void** output) override;

    private:
        WalletCardEffect() = default;

        static HRESULT __stdcall Create(IUnknown** effect);

        LONG m_refCount = 1;
        winrt::com_ptr<ID2D1DrawInfo> m_drawInfo;
        winrt::com_ptr<ID2D1ResourceTexture> m_stars;
        winrt::com_ptr<ID2D1ResourceTexture> m_noise;
        winrt::com_ptr<ID2D1ResourceTexture> m_qr;
        D2D1_RECT_L m_bounds{ 0, 0, 1, 1 };
        WalletCardConstants m_constants{};
    };
}
