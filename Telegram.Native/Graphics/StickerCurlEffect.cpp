#include "pch.h"
#include "StickerCurlEffect.h"

#include <d2d1effecthelpers.h>

#include "StickerCurlPixel.h"

namespace winrt::Telegram::Native::Graphics::implementation
{
    // {0E3A8C71-95D4-4F26-B7A0-5C1E9D24F6B8}
    static constexpr GUID GUID_StickerCurlPixelShader = { 0x0e3a8c71, 0x95d4, 0x4f26, { 0xb7, 0xa0, 0x5c, 0x1e, 0x9d, 0x24, 0xf6, 0xb8 } };

    HRESULT StickerCurlEffect::Register(ID2D1Factory1* factory)
    {
        static constexpr PCWSTR xml =
            LR"(<?xml version='1.0'?>
<Effect>
    <Property name='DisplayName' type='string' value='Sticker Curl'/>
    <Property name='Author' type='string' value='Telegram'/>
    <Property name='Category' type='string' value='Distort'/>
    <Property name='Description' type='string' value='A sticker rolling onto a surface or peeled off it.'/>
    <Inputs>
        <Input name='Source'/>
    </Inputs>
    <Property name='Sheet' type='vector4'>
        <Property name='DisplayName' type='string' value='Sheet'/>
        <Property name='Default' type='vector4' value='(0.0, 0.0, 1.0, 0.0)'/>
    </Property>
    <Property name='Motion' type='vector4'>
        <Property name='DisplayName' type='string' value='Motion'/>
        <Property name='Default' type='vector4' value='(0.0, 1.0, 0.72, 0.3)'/>
    </Property>
    <Property name='View' type='vector2'>
        <Property name='DisplayName' type='string' value='View'/>
        <Property name='Default' type='vector2' value='(3.2, 0.15)'/>
    </Property>
</Effect>)";

        static const D2D1_PROPERTY_BINDING bindings[] =
        {
            D2D1_VALUE_TYPE_BINDING(L"Sheet", &StickerCurlEffect::SetSheet, &StickerCurlEffect::GetSheet),
            D2D1_VALUE_TYPE_BINDING(L"Motion", &StickerCurlEffect::SetMotion, &StickerCurlEffect::GetMotion),
            D2D1_VALUE_TYPE_BINDING(L"View", &StickerCurlEffect::SetView, &StickerCurlEffect::GetView)
        };

        return factory->RegisterEffectFromString(CLSID_StickerCurlEffect, xml, bindings, ARRAYSIZE(bindings), Create);
    }

    HRESULT __stdcall StickerCurlEffect::Create(IUnknown** effect)
    {
        *effect = static_cast<ID2D1EffectImpl*>(new (std::nothrow) StickerCurlEffect());
        return *effect ? S_OK : E_OUTOFMEMORY;
    }

    HRESULT StickerCurlEffect::SetSheet(D2D1_VECTOR_4F value)
    {
        m_constants.Center = D2D1::Point2F(value.x, value.y);
        m_constants.Side = value.z;
        m_constants.Curl = value.w;
        return S_OK;
    }

    D2D1_VECTOR_4F StickerCurlEffect::GetSheet() const
    {
        return D2D1::Vector4F(m_constants.Center.x, m_constants.Center.y, m_constants.Side, m_constants.Curl);
    }

    HRESULT StickerCurlEffect::SetMotion(D2D1_VECTOR_4F value)
    {
        m_constants.Direction = D2D1::Point2F(value.x, value.y);
        m_constants.Bend = value.z;
        m_constants.Radius = value.w;
        return S_OK;
    }

    D2D1_VECTOR_4F StickerCurlEffect::GetMotion() const
    {
        return D2D1::Vector4F(m_constants.Direction.x, m_constants.Direction.y, m_constants.Bend, m_constants.Radius);
    }

    HRESULT StickerCurlEffect::SetView(D2D1_VECTOR_2F value)
    {
        m_constants.Eye = value.x;
        m_constants.Rise = value.y;
        return S_OK;
    }

    D2D1_VECTOR_2F StickerCurlEffect::GetView() const
    {
        return D2D1::Vector2F(m_constants.Eye, m_constants.Rise);
    }

    IFACEMETHODIMP StickerCurlEffect::Initialize(ID2D1EffectContext* context, ID2D1TransformGraph* graph)
    {
        HRESULT result = context->LoadPixelShader(GUID_StickerCurlPixelShader, g_StickerCurlPixel, sizeof(g_StickerCurlPixel));
        if (SUCCEEDED(result))
        {
            result = graph->SetSingleTransformNode(this);
        }

        return result;
    }

    IFACEMETHODIMP StickerCurlEffect::PrepareForRender(D2D1_CHANGE_TYPE)
    {
        return m_drawInfo->SetPixelShaderConstantBuffer(reinterpret_cast<BYTE const*>(&m_constants), sizeof(m_constants));
    }

    IFACEMETHODIMP StickerCurlEffect::SetGraph(ID2D1TransformGraph*)
    {
        // The input count is fixed, so the graph never has to change.
        return E_NOTIMPL;
    }

    IFACEMETHODIMP StickerCurlEffect::SetDrawInfo(ID2D1DrawInfo* drawInfo)
    {
        m_drawInfo.copy_from(drawInfo);

        HRESULT result = drawInfo->SetPixelShader(GUID_StickerCurlPixelShader);
        if (SUCCEEDED(result))
        {
            result = drawInfo->SetInputDescription(0, { D2D1_FILTER_MIN_MAG_MIP_LINEAR, 0 });
        }

        return result;
    }

    IFACEMETHODIMP StickerCurlEffect::MapOutputRectToInputRects(const D2D1_RECT_L*, D2D1_RECT_L* inputRects, UINT32 inputRectsCount) const
    {
        if (inputRectsCount != 1)
        {
            return E_INVALIDARG;
        }

        // Any pixel can sample anywhere in the input, so all of it is needed for any part of the
        // output.
        inputRects[0] = m_inputRect;
        return S_OK;
    }

    IFACEMETHODIMP StickerCurlEffect::MapInputRectsToOutputRect(const D2D1_RECT_L* inputRects, const D2D1_RECT_L*, UINT32 inputRectCount, D2D1_RECT_L* outputRect, D2D1_RECT_L* outputOpaqueSubRect)
    {
        if (inputRectCount != 1)
        {
            return E_INVALIDARG;
        }

        m_inputRect = inputRects[0];
        *outputRect = m_inputRect;

        // A sticker has transparent edges whatever it is curled into.
        *outputOpaqueSubRect = {};
        return S_OK;
    }

    IFACEMETHODIMP StickerCurlEffect::MapInvalidRect(UINT32, D2D1_RECT_L, D2D1_RECT_L* invalidOutputRect) const
    {
        *invalidOutputRect = m_inputRect;
        return S_OK;
    }

    IFACEMETHODIMP_(UINT32) StickerCurlEffect::GetInputCount() const
    {
        return 1;
    }

    IFACEMETHODIMP_(ULONG) StickerCurlEffect::AddRef()
    {
        return InterlockedIncrement(&m_refCount);
    }

    IFACEMETHODIMP_(ULONG) StickerCurlEffect::Release()
    {
        auto count = InterlockedDecrement(&m_refCount);
        if (count == 0)
        {
            delete this;
        }

        return count;
    }

    IFACEMETHODIMP StickerCurlEffect::QueryInterface(REFIID riid, void** output)
    {
        if (riid == __uuidof(ID2D1EffectImpl))
        {
            *output = static_cast<ID2D1EffectImpl*>(this);
        }
        else if (riid == __uuidof(ID2D1DrawTransform))
        {
            *output = static_cast<ID2D1DrawTransform*>(this);
        }
        else if (riid == __uuidof(ID2D1Transform))
        {
            *output = static_cast<ID2D1Transform*>(this);
        }
        else if (riid == __uuidof(ID2D1TransformNode))
        {
            *output = static_cast<ID2D1TransformNode*>(this);
        }
        else if (riid == __uuidof(IUnknown))
        {
            *output = static_cast<ID2D1EffectImpl*>(this);
        }
        else
        {
            *output = nullptr;
            return E_NOINTERFACE;
        }

        AddRef();
        return S_OK;
    }
}
