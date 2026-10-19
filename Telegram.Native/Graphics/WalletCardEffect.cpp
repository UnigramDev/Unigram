#include "pch.h"
#include "WalletCardEffect.h"

#include <d2d1effecthelpers.h>

#include "WalletCardTextures.h"
#include "WalletCardPixel.h"

namespace winrt::Telegram::Native::Graphics::implementation
{
    // {8D1F6A42-37B9-4C05-B2E8-1A6C94D07F35}
    static constexpr GUID GUID_WalletCardPixelShader = { 0x8d1f6a42, 0x37b9, 0x4c05, { 0xb2, 0xe8, 0x1a, 0x6c, 0x94, 0xd0, 0x7f, 0x35 } };

    // Found by these in the effect context, so every card on a device shares one upload of each.
    static constexpr GUID GUID_WalletCardStars = { 0x8d1f6a43, 0x37b9, 0x4c05, { 0xb2, 0xe8, 0x1a, 0x6c, 0x94, 0xd0, 0x7f, 0x35 } };
    static constexpr GUID GUID_WalletCardNoise = { 0x8d1f6a44, 0x37b9, 0x4c05, { 0xb2, 0xe8, 0x1a, 0x6c, 0x94, 0xd0, 0x7f, 0x35 } };
    static constexpr GUID GUID_WalletCardQr = { 0x8d1f6a45, 0x37b9, 0x4c05, { 0xb2, 0xe8, 0x1a, 0x6c, 0x94, 0xd0, 0x7f, 0x35 } };

    static HRESULT FindOrCreateTexture(ID2D1EffectContext* context, GUID const& id, WalletCardTextures::Texture const& texture, D2D1_EXTEND_MODE extend, ID2D1ResourceTexture** result)
    {
        if (SUCCEEDED(context->FindResourceTexture(&id, result)))
        {
            return S_OK;
        }

        UINT32 extents[] = { texture.Width, texture.Height };
        D2D1_EXTEND_MODE extendModes[] = { extend, extend };
        UINT32 strides[] = { texture.Width * 4 };

        D2D1_RESOURCE_TEXTURE_PROPERTIES properties =
        {
            extents,
            2,
            D2D1_BUFFER_PRECISION_8BPC_UNORM,
            D2D1_CHANNEL_DEPTH_4,
            D2D1_FILTER_MIN_MAG_MIP_LINEAR,
            extendModes
        };

        return context->CreateResourceTexture(&id, &properties, texture.Pixels.data(), strides, static_cast<UINT32>(texture.Pixels.size()), result);
    }

    HRESULT WalletCardEffect::Register(ID2D1Factory1* factory)
    {
        static constexpr PCWSTR xml =
            LR"(<?xml version='1.0'?>
<Effect>
    <Property name='DisplayName' type='string' value='Wallet Card'/>
    <Property name='Author' type='string' value='Telegram'/>
    <Property name='Category' type='string' value='Source'/>
    <Property name='Description' type='string' value='The wallet card face and edge.'/>
    <Inputs/>
    <Property name='Constants' type='blob'>
        <Property name='DisplayName' type='string' value='Constants'/>
    </Property>
    <Property name='Bounds' type='vector4'>
        <Property name='DisplayName' type='string' value='Bounds'/>
        <Property name='Default' type='vector4' value='(0.0, 0.0, 1.0, 1.0)'/>
    </Property>
</Effect>)";

        static const D2D1_PROPERTY_BINDING bindings[] =
        {
            D2D1_BLOB_TYPE_BINDING(L"Constants", &WalletCardEffect::SetConstants, &WalletCardEffect::GetConstants),
            D2D1_VALUE_TYPE_BINDING(L"Bounds", &WalletCardEffect::SetBounds, &WalletCardEffect::GetBounds)
        };

        return factory->RegisterEffectFromString(CLSID_WalletCardEffect, xml, bindings, ARRAYSIZE(bindings), Create);
    }

    HRESULT __stdcall WalletCardEffect::Create(IUnknown** effect)
    {
        *effect = static_cast<ID2D1EffectImpl*>(new (std::nothrow) WalletCardEffect());
        return *effect ? S_OK : E_OUTOFMEMORY;
    }

    HRESULT WalletCardEffect::SetConstants(BYTE const* data, UINT32 dataSize)
    {
        if (dataSize != sizeof(m_constants))
        {
            return E_INVALIDARG;
        }

        memcpy(&m_constants, data, sizeof(m_constants));
        return S_OK;
    }

    HRESULT WalletCardEffect::GetConstants(BYTE* data, UINT32 dataSize, UINT32* actualSize) const
    {
        if (actualSize)
        {
            *actualSize = sizeof(m_constants);
        }

        if (data == nullptr)
        {
            return S_OK;
        }

        if (dataSize < sizeof(m_constants))
        {
            return E_NOT_SUFFICIENT_BUFFER;
        }

        memcpy(data, &m_constants, sizeof(m_constants));
        return S_OK;
    }

    HRESULT WalletCardEffect::SetBounds(D2D1_VECTOR_4F value)
    {
        m_bounds = { static_cast<LONG>(value.x), static_cast<LONG>(value.y), static_cast<LONG>(value.z), static_cast<LONG>(value.w) };
        return S_OK;
    }

    D2D1_VECTOR_4F WalletCardEffect::GetBounds() const
    {
        return D2D1::Vector4F(static_cast<float>(m_bounds.left), static_cast<float>(m_bounds.top), static_cast<float>(m_bounds.right), static_cast<float>(m_bounds.bottom));
    }

    IFACEMETHODIMP WalletCardEffect::Initialize(ID2D1EffectContext* context, ID2D1TransformGraph* graph)
    {
        HRESULT result = context->LoadPixelShader(GUID_WalletCardPixelShader, g_WalletCardPixel, sizeof(g_WalletCardPixel));
        if (SUCCEEDED(result))
        {
            result = FindOrCreateTexture(context, GUID_WalletCardStars, WalletCardTextures::Stars(), D2D1_EXTEND_MODE_CLAMP, m_stars.put());
        }

        if (SUCCEEDED(result))
        {
            result = FindOrCreateTexture(context, GUID_WalletCardNoise, WalletCardTextures::Noise(), D2D1_EXTEND_MODE_WRAP, m_noise.put());
        }

        if (SUCCEEDED(result))
        {
            result = FindOrCreateTexture(context, GUID_WalletCardQr, WalletCardTextures::Qr(), D2D1_EXTEND_MODE_CLAMP, m_qr.put());
        }

        if (SUCCEEDED(result))
        {
            result = graph->SetSingleTransformNode(this);
        }

        return result;
    }

    IFACEMETHODIMP WalletCardEffect::PrepareForRender(D2D1_CHANGE_TYPE)
    {
        return m_drawInfo->SetPixelShaderConstantBuffer(reinterpret_cast<BYTE const*>(&m_constants), sizeof(m_constants));
    }

    IFACEMETHODIMP WalletCardEffect::SetGraph(ID2D1TransformGraph*)
    {
        return E_NOTIMPL;
    }

    IFACEMETHODIMP WalletCardEffect::SetDrawInfo(ID2D1DrawInfo* drawInfo)
    {
        m_drawInfo.copy_from(drawInfo);

        // With no inputs, the resource textures take the shader's registers from t0.
        HRESULT result = drawInfo->SetPixelShader(GUID_WalletCardPixelShader);
        if (SUCCEEDED(result))
        {
            result = drawInfo->SetResourceTexture(0, m_stars.get());
        }

        if (SUCCEEDED(result))
        {
            result = drawInfo->SetResourceTexture(1, m_noise.get());
        }

        if (SUCCEEDED(result))
        {
            result = drawInfo->SetResourceTexture(2, m_qr.get());
        }

        return result;
    }

    IFACEMETHODIMP WalletCardEffect::MapOutputRectToInputRects(const D2D1_RECT_L*, D2D1_RECT_L*, UINT32 inputRectsCount) const
    {
        return inputRectsCount == 0 ? S_OK : E_INVALIDARG;
    }

    IFACEMETHODIMP WalletCardEffect::MapInputRectsToOutputRect(const D2D1_RECT_L*, const D2D1_RECT_L*, UINT32 inputRectCount, D2D1_RECT_L* outputRect, D2D1_RECT_L* outputOpaqueSubRect)
    {
        if (inputRectCount != 0)
        {
            return E_INVALIDARG;
        }

        *outputRect = m_bounds;

        // The rounded corners and the margin round the card are transparent.
        *outputOpaqueSubRect = {};
        return S_OK;
    }

    IFACEMETHODIMP WalletCardEffect::MapInvalidRect(UINT32, D2D1_RECT_L, D2D1_RECT_L* invalidOutputRect) const
    {
        *invalidOutputRect = m_bounds;
        return S_OK;
    }

    IFACEMETHODIMP_(UINT32) WalletCardEffect::GetInputCount() const
    {
        return 0;
    }

    IFACEMETHODIMP_(ULONG) WalletCardEffect::AddRef()
    {
        return InterlockedIncrement(&m_refCount);
    }

    IFACEMETHODIMP_(ULONG) WalletCardEffect::Release()
    {
        auto count = InterlockedDecrement(&m_refCount);
        if (count == 0)
        {
            delete this;
        }

        return count;
    }

    IFACEMETHODIMP WalletCardEffect::QueryInterface(REFIID riid, void** output)
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
