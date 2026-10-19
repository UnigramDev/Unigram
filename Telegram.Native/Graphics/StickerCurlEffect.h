#pragma once

#include <d2d1_1.h>
#include <d2d1effectauthor.h>

#include <winrt/base.h>

namespace winrt::Telegram::Native::Graphics::implementation
{
    // {6C7B1E4A-3F2D-4B8E-9A51-2D7F0C9E8B13}
    inline constexpr GUID CLSID_StickerCurlEffect = { 0x6c7b1e4a, 0x3f2d, 0x4b8e, { 0x9a, 0x51, 0x2d, 0x7f, 0x0c, 0x9e, 0x8b, 0x13 } };

    enum StickerCurlProperty : UINT32
    {
        // Centre x and y, side, curl.
        StickerCurlProperty_Sheet = 0,

        // Direction x and y, bend, radius.
        StickerCurlProperty_Motion = 1,

        // Eye distance, rise.
        StickerCurlProperty_View = 2
    };

    // A sticker as a sheet rolling onto the card or peeled off it: StickerCurl.hlsl. One input,
    // drawn into an output of the same bounds, so the input has to carry the room the lifted part
    // grows into - and the scene position of a pixel is its position in the input.
    class StickerCurlEffect final : public ID2D1EffectImpl, public ID2D1DrawTransform
    {
    public:
        static HRESULT Register(ID2D1Factory1* factory);

        HRESULT SetSheet(D2D1_VECTOR_4F value);
        D2D1_VECTOR_4F GetSheet() const;

        HRESULT SetMotion(D2D1_VECTOR_4F value);
        D2D1_VECTOR_4F GetMotion() const;

        HRESULT SetView(D2D1_VECTOR_2F value);
        D2D1_VECTOR_2F GetView() const;

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
        StickerCurlEffect() = default;

        static HRESULT __stdcall Create(IUnknown** effect);

        // The constant buffer of StickerCurl.hlsl, field for field.
        struct Constants
        {
            D2D1_POINT_2F Center;
            float Side;
            float Curl;
            D2D1_POINT_2F Direction;
            float Bend;
            float Radius;
            float Eye;
            float Rise;
            float Padding[2];
        };

        static_assert(sizeof(Constants) == 48, "The layout fxc reports for StickerCurl.hlsl.");

        LONG m_refCount = 1;
        winrt::com_ptr<ID2D1DrawInfo> m_drawInfo;
        D2D1_RECT_L m_inputRect{};
        Constants m_constants{};
    };
}
