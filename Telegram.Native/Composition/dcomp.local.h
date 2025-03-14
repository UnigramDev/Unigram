//---------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//---------------------------------------------------------------------------
#pragma once

#include <d2dbasetypes.h>   // for D2D_MATRIX_3X2_F
#ifndef D3DMATRIX_DEFINED
#include <d3d9types.h>      // for D3DMATRIX
#endif
#include <d2d1_1.h>         // for D2D1_COMPOSITE_MODE
#include <winapifamily.h>

#include <dcompanimation.h> // for IDirectCompositionAnimation interface
#include <dcomptypes.h>

#include <WindowsNumerics.h>

#if (NTDDI_VERSION >= NTDDI_WIN8)


//
// DirectComposition types
//

enum DCOMPOSITION_BITMAP_INTERPOLATION_MODE
{
    DCOMPOSITION_BITMAP_INTERPOLATION_MODE_NEAREST_NEIGHBOR = 0,
    DCOMPOSITION_BITMAP_INTERPOLATION_MODE_LINEAR = 1,

    DCOMPOSITION_BITMAP_INTERPOLATION_MODE_INHERIT = 0xffffffff
};

enum DCOMPOSITION_BORDER_MODE
{
    DCOMPOSITION_BORDER_MODE_SOFT = 0,
    DCOMPOSITION_BORDER_MODE_HARD = 1,

    DCOMPOSITION_BORDER_MODE_INHERIT = 0xffffffff
};

enum DCOMPOSITION_COMPOSITE_MODE
{
    DCOMPOSITION_COMPOSITE_MODE_SOURCE_OVER = 0,
    DCOMPOSITION_COMPOSITE_MODE_DESTINATION_INVERT = 1,
#if (_WIN32_WINNT >= _WIN32_WINNT_WINBLUE)
    DCOMPOSITION_COMPOSITE_MODE_MIN_BLEND = 2,
#endif  // (_WIN32_WINNT >= _WIN32_WINNT_WINBLUE)

    DCOMPOSITION_COMPOSITE_MODE_INHERIT = 0xffffffff
};

#if (_WIN32_WINNT >= _WIN32_WINNT_WINBLUE)
enum DCOMPOSITION_BACKFACE_VISIBILITY
{
    DCOMPOSITION_BACKFACE_VISIBILITY_VISIBLE = 0,
    DCOMPOSITION_BACKFACE_VISIBILITY_HIDDEN = 1,

    DCOMPOSITION_BACKFACE_VISIBILITY_INHERIT = 0xffffffff
};

enum DCOMPOSITION_OPACITY_MODE
{
    DCOMPOSITION_OPACITY_MODE_LAYER = 0,
    DCOMPOSITION_OPACITY_MODE_MULTIPLY = 1,

    DCOMPOSITION_OPACITY_MODE_INHERIT = 0xffffffff
};
#endif  // (_WIN32_WINNT >= _WIN32_WINNT_WINBLUE)

#if (_WIN32_WINNT >= _WIN32_WINNT_WINTHRESHOLD)
enum DCOMPOSITION_DEPTH_MODE
{
    DCOMPOSITION_DEPTH_MODE_TREE = 0,
    DCOMPOSITION_DEPTH_MODE_SPATIAL = 1,
    DCOMPOSITION_DEPTH_MODE_SORTED = 3,

    DCOMPOSITION_DEPTH_MODE_INHERIT = 0xffffffff
};
#endif  // (_WIN32_WINNT >= _WIN32_WINNT_WINTHRESHOLD)

typedef struct
{
    LARGE_INTEGER lastFrameTime;
    DXGI_RATIONAL currentCompositionRate;
    LARGE_INTEGER currentTime;
    LARGE_INTEGER timeFrequency;
    LARGE_INTEGER nextEstimatedFrameTime;
} DCOMPOSITION_FRAME_STATISTICS;


//
// Composition object specific access flags
//

#define COMPOSITIONOBJECT_READ          0x0001L
#define COMPOSITIONOBJECT_WRITE         0x0002L

#define COMPOSITIONOBJECT_ALL_ACCESS    (COMPOSITIONOBJECT_READ | COMPOSITIONOBJECT_WRITE)

#endif // NTDDI_WIN8

#pragma region Desktop Family
#if WINAPI_FAMILY_PARTITION(WINAPI_PARTITION_APP)

#if (NTDDI_VERSION >= NTDDI_WIN8)

typedef interface IDCompositionAffineTransform2DEffect   IDCompositionAffineTransform2DEffect;
typedef interface IDCompositionAnimation                 IDCompositionAnimation;
typedef interface IDCompositionArithmeticCompositeEffect IDCompositionArithmeticCompositeEffect;
typedef interface IDCompositionBlendEffect               IDCompositionBlendEffect;
typedef interface IDCompositionBrightnessEffect          IDCompositionBrightnessEffect;
typedef interface IDCompositionClip                      IDCompositionClip;
typedef interface IDCompositionColorMatrixEffect         IDCompositionColorMatrixEffect;
typedef interface IDCompositionCompositeEffect           IDCompositionCompositeEffect;
typedef interface IDCompositionDevice                    IDCompositionDevice;
typedef interface IDCompositionEffect                    IDCompositionEffect;
typedef interface IDCompositionEffectGroup               IDCompositionEffectGroup;
typedef interface IDCompositionFilterEffect              IDCompositionFilterEffect;
typedef interface IDCompositionGaussianBlurEffect        IDCompositionGaussianBlurEffect;
typedef interface IDCompositionHueRotationEffect         IDCompositionHueRotationEffect;
typedef interface IDCompositionLinearTransferEffect      IDCompositionLinearTransferEffect;
typedef interface IDCompositionMatrixTransform           IDCompositionMatrixTransform;
typedef interface IDCompositionMatrixTransform3D         IDCompositionMatrixTransform3D;
typedef interface IDCompositionRectangleClip             IDCompositionRectangleClip;
typedef interface IDCompositionRotateTransform           IDCompositionRotateTransform;
typedef interface IDCompositionRotateTransform3D         IDCompositionRotateTransform3D;
typedef interface IDCompositionSaturationEffect          IDCompositionSaturationEffect;
typedef interface IDCompositionScaleTransform            IDCompositionScaleTransform;
typedef interface IDCompositionScaleTransform3D          IDCompositionScaleTransform3D;
typedef interface IDCompositionShadowEffect              IDCompositionShadowEffect;
typedef interface IDCompositionSkewTransform             IDCompositionSkewTransform;
typedef interface IDCompositionSurface                   IDCompositionSurface;
typedef interface IDCompositionTableTransferEffect       IDCompositionTableTransferEffect;
typedef interface IDCompositionTarget                    IDCompositionTarget;
typedef interface IDCompositionTransform                 IDCompositionTransform;
typedef interface IDCompositionTransform3D               IDCompositionTransform3D;
typedef interface IDCompositionTranslateTransform        IDCompositionTranslateTransform;
typedef interface IDCompositionTranslateTransform3D      IDCompositionTranslateTransform3D;
typedef interface IDCompositionTurbulenceEffect          IDCompositionTurbulenceEffect;
typedef interface IDCompositionVirtualSurface            IDCompositionVirtualSurface;
typedef interface IDCompositionVisual                    IDCompositionVisual;

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionDevice
//
//  Synopsis:
//      Serves as the root factory for all other DirectComposition objects and
//      controls transactional composition.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionDevice
DECLARE_INTERFACE_IID_(IDCompositionDevice, IUnknown, "C37EA93A-E7AA-450D-B16F-9746CB0407F3")
{
    // Commits all DirectComposition commands pending on this device.
    STDMETHOD(Commit)(THIS
        ) PURE;

    // Waits for the last Commit to be processed by the composition engine
    STDMETHOD(WaitForCommitCompletion)(THIS
        ) PURE;

    // Gets timing information about the composition engine.
    STDMETHOD(GetFrameStatistics)(THIS_
        _Out_ DCOMPOSITION_FRAME_STATISTICS * statistics
        ) PURE;

    // Creates a composition target bound to a window represented by an HWND.
    STDMETHOD(CreateTargetForHwnd)(THIS_
        _In_ HWND hwnd,
        BOOL topmost,
        _Outptr_ IDCompositionTarget * *target
        ) PURE;

    // Creates a new visual object.
    STDMETHOD(CreateVisual)(THIS_
        _Outptr_ IDCompositionVisual * *visual
        ) PURE;

    // Creates a DirectComposition surface object
    STDMETHOD(CreateSurface)(THIS_
        _In_ UINT width,
        _In_ UINT height,
        _In_ DXGI_FORMAT pixelFormat,
        _In_ DXGI_ALPHA_MODE alphaMode,
        _Outptr_ IDCompositionSurface * *surface
        ) PURE;

    // Creates a DirectComposition virtual surface object
    STDMETHOD(CreateVirtualSurface)(THIS_
        _In_ UINT initialWidth,
        _In_ UINT initialHeight,
        _In_ DXGI_FORMAT pixelFormat,
        _In_ DXGI_ALPHA_MODE alphaMode,
        _Outptr_ IDCompositionVirtualSurface * *virtualSurface
        ) PURE;

    // Creates a surface wrapper around a pre-existing surface that can be associated with one or more visuals for composition.
    STDMETHOD(CreateSurfaceFromHandle)(THIS_
        _In_ HANDLE handle,
        _Outptr_ IUnknown * *surface
        ) PURE;

    // Creates a wrapper object that represents the rasterization of a layered window and which can be associated with a visual for composition.
    STDMETHOD(CreateSurfaceFromHwnd)(THIS_
        _In_ HWND hwnd,
        _Outptr_ IUnknown * *surface
        ) PURE;

    // Creates a 2D translation transform object.
    STDMETHOD(CreateTranslateTransform)(THIS_
        _Outptr_ IDCompositionTranslateTransform * *translateTransform
        ) PURE;

    // Creates a 2D scale transform object.
    STDMETHOD(CreateScaleTransform)(THIS_
        _Outptr_ IDCompositionScaleTransform * *scaleTransform
        ) PURE;

    // Creates a 2D rotation transform object.
    STDMETHOD(CreateRotateTransform)(THIS_
        _Outptr_ IDCompositionRotateTransform * *rotateTransform
        ) PURE;

    // Creates a 2D skew transform object.
    STDMETHOD(CreateSkewTransform)(THIS_
        _Outptr_ IDCompositionSkewTransform * *skewTransform
        ) PURE;

    // Creates a 2D 3x2 matrix transform object.
    STDMETHOD(CreateMatrixTransform)(THIS_
        _Outptr_ IDCompositionMatrixTransform * *matrixTransform
        ) PURE;

    // Creates a 2D transform object that holds an array of 2D transform objects.
    STDMETHOD(CreateTransformGroup)(THIS_
        _In_reads_(elements) IDCompositionTransform * *transforms,
        UINT elements,
        _Outptr_ IDCompositionTransform * *transformGroup
        ) PURE;

    // Creates a 3D translation transform object.
    STDMETHOD(CreateTranslateTransform3D)(THIS_
        _Outptr_ IDCompositionTranslateTransform3D * *translateTransform3D
        ) PURE;

    // Creates a 3D scale transform object.
    STDMETHOD(CreateScaleTransform3D)(THIS_
        _Outptr_ IDCompositionScaleTransform3D * *scaleTransform3D
        ) PURE;

    // Creates a 3D rotation transform object.
    STDMETHOD(CreateRotateTransform3D)(THIS_
        _Outptr_ IDCompositionRotateTransform3D * *rotateTransform3D
        ) PURE;

    // Creates a 3D 4x4 matrix transform object.
    STDMETHOD(CreateMatrixTransform3D)(THIS_
        _Outptr_ IDCompositionMatrixTransform3D * *matrixTransform3D
        ) PURE;

    // Creates a 3D transform object that holds an array of 3D transform objects.
    STDMETHOD(CreateTransform3DGroup)(THIS_
        _In_reads_(elements) IDCompositionTransform3D * *transforms3D,
        UINT elements,
        _Outptr_ IDCompositionTransform3D * *transform3DGroup
        ) PURE;

    // Creates an effect group
    STDMETHOD(CreateEffectGroup)(THIS_
        _Outptr_ IDCompositionEffectGroup * *effectGroup
        ) PURE;

    // Creates a clip object that can be used to clip the contents of a visual subtree.
    STDMETHOD(CreateRectangleClip)(THIS_
        _Outptr_ IDCompositionRectangleClip * *clip
        ) PURE;

    // Creates an animation object
    STDMETHOD(CreateAnimation)(THIS_
        _Outptr_ IDCompositionAnimation * *animation
        ) PURE;

    // Returns the states of the app's DX device and DWM's dx devices
    STDMETHOD(CheckDeviceState)(THIS_
        _Out_ BOOL * pfValid
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionTarget
//
//  Synopsis:
//      An IDCompositionTarget interface represents a binding between a
//      DirectComposition visual tree and a destination on top of which the
//      visual tree should be composed.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionTarget
DECLARE_INTERFACE_IID_(IDCompositionTarget, IUnknown, "eacdd04c-117e-4e17-88f4-d1b12b0e3d89")
{
    // Sets the root visual
    STDMETHOD(SetRoot)(THIS_
        _In_opt_ IDCompositionVisual * visual
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionVisual
//
//  Synopsis:
//      An IDCompositionVisual interface represents a visual that participates in
//      a visual tree.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionVisual
DECLARE_INTERFACE_IID_(IDCompositionVisual, IUnknown, "4d93059d-097b-4651-9a60-f0f25116e2f3")
{
    // Changes the value of OffsetX property
    STDMETHOD(SetOffsetX)(THIS_
        float offsetX
        ) PURE;

    // Animates the value of the OffsetX property.
    STDMETHOD(SetOffsetX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of OffsetY property
    STDMETHOD(SetOffsetY)(THIS_
        float offsetY
        ) PURE;

    // Animates the value of the OffsetY property.
    STDMETHOD(SetOffsetY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Sets the matrix that modifies the coordinate system of this visual.
    STDMETHOD(SetTransform)(THIS_
        const D2D_MATRIX_3X2_F & matrix
        ) PURE;

    // Sets the transformation object that modifies the coordinate system of this visual.
    STDMETHOD(SetTransform)(THIS_
        _In_opt_ IDCompositionTransform * transform
        ) PURE;

    // Sets the visual that should act as this visual's parent for the
    // purpose of establishing a base coordinate system.
    STDMETHOD(SetTransformParent)(THIS_
        _In_opt_ IDCompositionVisual * visual
        ) PURE;

    // Sets the effect object that is applied during the rendering of this visual
    STDMETHOD(SetEffect)(THIS_
        _In_opt_ IDCompositionEffect * effect
        ) PURE;

    // Sets the mode to use when interpolating pixels from bitmaps drawn not
    // exactly at scale and axis-aligned.
    STDMETHOD(SetBitmapInterpolationMode)(THIS_
        _In_ DCOMPOSITION_BITMAP_INTERPOLATION_MODE interpolationMode
        ) PURE;

    // Sets the mode to use when drawing the edge of bitmaps that are not
    // exactly axis-aligned and at precise pixel boundaries.
    STDMETHOD(SetBorderMode)(THIS_
        _In_ DCOMPOSITION_BORDER_MODE borderMode
        ) PURE;

    // Sets the clip object that restricts the rendering of this visual to a D2D rectangle.
    STDMETHOD(SetClip)(THIS_
        const D2D_RECT_F & rect
        ) PURE;

    // Sets the clip object that restricts the rendering of this visual to a rectangle.
    STDMETHOD(SetClip)(THIS_
        _In_opt_ IDCompositionClip * clip
        ) PURE;

    // Associates a bitmap with a visual
    STDMETHOD(SetContent)(THIS_
        _In_opt_ IUnknown * content
        ) PURE;

    // Adds a visual to the children list of another visual.
    STDMETHOD(AddVisual)(THIS_
        _In_ IDCompositionVisual * visual,
        BOOL insertAbove,
        _In_opt_ IDCompositionVisual * referenceVisual
        ) PURE;

    // Removes a visual from the children list of another visual.
    STDMETHOD(RemoveVisual)(THIS_
        _In_ IDCompositionVisual * visual
        ) PURE;

    // Removes all visuals from the children list of another visual.
    STDMETHOD(RemoveAllVisuals)(THIS_
        ) PURE;

    // Sets the mode to use when composing the bitmap against the render target.
    STDMETHOD(SetCompositeMode)(THIS_
        _In_ DCOMPOSITION_COMPOSITE_MODE compositeMode
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionEffect
//
//  Synopsis:
//      An IDCompositionEffect interface represents an effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionEffect
DECLARE_INTERFACE_IID_(IDCompositionEffect, IUnknown, "EC81B08F-BFCB-4e8d-B193-A915587999E8")
{
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionTransform3D
//
//  Synopsis:
//      An IDCompositionTransform3D interface represents a 3D transformation.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionTransform3D
DECLARE_INTERFACE_IID_(IDCompositionTransform3D, IDCompositionEffect, "71185722-246B-41f2-AAD1-0443F7F4BFC2")
{
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionTransform
//
//  Synopsis:
//      An IDCompositionTransform interface represents a 2D transformation that
//      can be used to modify the coordinate space of a visual subtree.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionTransform
DECLARE_INTERFACE_IID_(IDCompositionTransform, IDCompositionTransform3D, "FD55FAA7-37E0-4c20-95D2-9BE45BC33F55")
{
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionTranslateTransform
//
//  Synopsis:
//      An IDCompositionTranslateTransform interface represents a 2D transformation
//      that affects only the offset of a visual along the x and y axes.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionTranslateTransform
DECLARE_INTERFACE_IID_(IDCompositionTranslateTransform, IDCompositionTransform, "06791122-C6F0-417d-8323-269E987F5954")
{
    // Changes the value of the OffsetX property.
    STDMETHOD(SetOffsetX)(THIS_
        float offsetX
        ) PURE;

    // Animates the value of the OffsetX property.
    STDMETHOD(SetOffsetX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the OffsetY property.
    STDMETHOD(SetOffsetY)(THIS_
        float offsetY
        ) PURE;

    // Animates the value of the OffsetY property.
    STDMETHOD(SetOffsetY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionScaleTransform
//
//  Synopsis:
//      An IDCompositionScaleTransform interface represents a 2D transformation that
//      affects the scale of a visual along the x and y axes. The coordinate system
//      is scaled from the specified center point.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionScaleTransform
DECLARE_INTERFACE_IID_(IDCompositionScaleTransform, IDCompositionTransform, "71FDE914-40EF-45ef-BD51-68B037C339F9")
{
    // Changes the value of the ScaleX property.
    STDMETHOD(SetScaleX)(THIS_
        float scaleX
        ) PURE;

    // Animates the value of the ScaleX property.
    STDMETHOD(SetScaleX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the ScaleY property.
    STDMETHOD(SetScaleY)(THIS_
        float scaleY
        ) PURE;

    // Animates the value of the ScaleY property.
    STDMETHOD(SetScaleY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the CenterX property.
    STDMETHOD(SetCenterX)(THIS_
        float centerX
        ) PURE;

    // Animates the value of the CenterX property.
    STDMETHOD(SetCenterX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the CenterY property.
    STDMETHOD(SetCenterY)(THIS_
        float centerY
        ) PURE;

    // Animates the value of the CenterY property.
    STDMETHOD(SetCenterY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionRotateTransform
//
//  Synopsis:
//      An IDCompositionRotateTransform interface represents a 2D transformation
//      that affects the rotation of a visual along the z axis. The coordinate system
//      is rotated around the specified center point.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionRotateTransform
DECLARE_INTERFACE_IID_(IDCompositionRotateTransform, IDCompositionTransform, "641ED83C-AE96-46c5-90DC-32774CC5C6D5")
{
    // Changes the value of the Angle property.
    STDMETHOD(SetAngle)(THIS_
        float angle
        ) PURE;

    // Animates the value of the Angle property.
    STDMETHOD(SetAngle)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the CenterX property.
    STDMETHOD(SetCenterX)(THIS_
        float centerX
        ) PURE;

    // Animates the value of the CenterX property.
    STDMETHOD(SetCenterX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the CenterY property.
    STDMETHOD(SetCenterY)(THIS_
        float centerY
        ) PURE;

    // Animates the value of the CenterY property.
    STDMETHOD(SetCenterY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionSkewTransform
//
//  Synopsis:
//      An IDCompositionSkewTransform interface represents a 2D transformation that
//      affects the skew of a visual along the x and y axes. The coordinate system
//      is skewed around the specified center point.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionSkewTransform
DECLARE_INTERFACE_IID_(IDCompositionSkewTransform, IDCompositionTransform, "E57AA735-DCDB-4c72-9C61-0591F58889EE")
{
    // Changes the value of the AngleX property.
    STDMETHOD(SetAngleX)(THIS_
        float angleX
        ) PURE;

    // Animates the value of the AngleX property.
    STDMETHOD(SetAngleX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the AngleY property.
    STDMETHOD(SetAngleY)(THIS_
        float angleY
        ) PURE;

    // Animates the value of the AngleY property.
    STDMETHOD(SetAngleY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the CenterX property.
    STDMETHOD(SetCenterX)(THIS_
        float centerX
        ) PURE;

    // Animates the value of the CenterX property.
    STDMETHOD(SetCenterX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the CenterY property.
    STDMETHOD(SetCenterY)(THIS_
        float centerY
        ) PURE;

    // Animates the value of the CenterY property.
    STDMETHOD(SetCenterY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionMatrixTransform
//
//  Synopsis:
//      An IDCompositionMatrixTransform interface represents an arbitrary affine
//      2D transformation defined by a 3x2 matrix.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionMatrixTransform
DECLARE_INTERFACE_IID_(IDCompositionMatrixTransform, IDCompositionTransform, "16CDFF07-C503-419c-83F2-0965C7AF1FA6")
{
    // Changes all values of the matrix of this transform.
    STDMETHOD(SetMatrix)(THIS_
        const D2D_MATRIX_3X2_F & matrix
        ) PURE;

    // Changes a single element of the matrix of this transform.
    STDMETHOD(SetMatrixElement)(THIS_
        _In_ int row,
        _In_ int column,
        _In_ float value
        ) PURE;

    // Animates a single element of the matrix of this transform.
    STDMETHOD(SetMatrixElement)(THIS_
        _In_ int row,
        _In_ int column,
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionEffectGroup
//
//  Synopsis:
//      An IDCompositionEffectGroup holds effects, inluding 3D transforms that can
//      be applied to a visual.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionEffectGroup
DECLARE_INTERFACE_IID_(IDCompositionEffectGroup, IDCompositionEffect, "A7929A74-E6B2-4bd6-8B95-4040119CA34D")
{
    // Changes the opacity property.
    STDMETHOD(SetOpacity)(THIS_
        float opacity
        ) PURE;

    // Animates the opacity property
    STDMETHOD(SetOpacity)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Sets the 3D transform
    STDMETHOD(SetTransform3D)(THIS_
        _In_opt_ IDCompositionTransform3D * transform3D
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionTranslateTransform3D
//
//  Synopsis:
//      An IDCompositionTranslateTransform3D interface represents a 3D transformation
//      that affects the offset of a visual along the x,y and z axes.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionTranslateTransform3D
DECLARE_INTERFACE_IID_(IDCompositionTranslateTransform3D, IDCompositionTransform3D, "91636D4B-9BA1-4532-AAF7-E3344994D788")
{
    // Changes the value of the OffsetX property.
    STDMETHOD(SetOffsetX)(THIS_
        float offsetX
        ) PURE;

    // Animates the value of the OffsetX property.
    STDMETHOD(SetOffsetX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the OffsetY property.
    STDMETHOD(SetOffsetY)(THIS_
        float offsetY
        ) PURE;

    // Animates the value of the OffsetY property.
    STDMETHOD(SetOffsetY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the OffsetZ property.
    STDMETHOD(SetOffsetZ)(THIS_
        float offsetZ
        ) PURE;

    // Animates the value of the OffsetZ property.
    STDMETHOD(SetOffsetZ)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionScaleTransform3D
//
//  Synopsis:
//      An IDCompositionScaleTransform3D interface represents a 3D transformation that
//      affects the scale of a visual along the x, y and z axes. The coordinate system
//      is scaled from the specified center point.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionScaleTransform3D
DECLARE_INTERFACE_IID_(IDCompositionScaleTransform3D, IDCompositionTransform3D, "2A9E9EAD-364B-4b15-A7C4-A1997F78B389")
{
    // Changes the value of the ScaleX property.
    STDMETHOD(SetScaleX)(THIS_
        float scaleX
        ) PURE;

    // Animates the value of the ScaleX property.
    STDMETHOD(SetScaleX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the ScaleY property.
    STDMETHOD(SetScaleY)(THIS_
        float scaleY
        ) PURE;

    // Animates the value of the ScaleY property.
    STDMETHOD(SetScaleY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the ScaleZ property.
    STDMETHOD(SetScaleZ)(THIS_
        float scaleZ
        ) PURE;

    // Animates the value of the ScaleZ property.
    STDMETHOD(SetScaleZ)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the CenterX property.
    STDMETHOD(SetCenterX)(THIS_
        float centerX
        ) PURE;

    // Animates the value of the CenterX property.
    STDMETHOD(SetCenterX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the CenterY property.
    STDMETHOD(SetCenterY)(THIS_
        float centerY
        ) PURE;

    // Animates the value of the CenterY property.
    STDMETHOD(SetCenterY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the CenterZ property.
    STDMETHOD(SetCenterZ)(THIS_
        float centerZ
        ) PURE;

    // Animates the value of the CenterZ property.
    STDMETHOD(SetCenterZ)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionRotateTransform3D
//
//  Synopsis:
//      An IDCompositionRotateTransform3D interface represents a 3D transformation
//      that affects the rotation of a visual along the specified axis at the
//      specified center point.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionRotateTransform3D
DECLARE_INTERFACE_IID_(IDCompositionRotateTransform3D, IDCompositionTransform3D, "D8F5B23F-D429-4a91-B55A-D2F45FD75B18")
{
    // Changes the value of the Angle property.
    STDMETHOD(SetAngle)(THIS_
        float angle
        ) PURE;

    // Animates the value of the Angle property.
    STDMETHOD(SetAngle)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the AxisX property.
    STDMETHOD(SetAxisX)(THIS_
        float axisX
        ) PURE;

    // Animates the value of the AxisX property.
    STDMETHOD(SetAxisX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the AxisY property.
    STDMETHOD(SetAxisY)(THIS_
        float axisY
        ) PURE;

    // Animates the value of the AxisY property.
    STDMETHOD(SetAxisY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the AxisZ property.
    STDMETHOD(SetAxisZ)(THIS_
        float axisZ
        ) PURE;

    // Animates the value of the AxisZ property.
    STDMETHOD(SetAxisZ)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the CenterX property.
    STDMETHOD(SetCenterX)(THIS_
        float centerX
        ) PURE;

    // Animates the value of the CenterX property.
    STDMETHOD(SetCenterX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the CenterY property.
    STDMETHOD(SetCenterY)(THIS_
        float centerY
        ) PURE;

    // Animates the value of the CenterY property.
    STDMETHOD(SetCenterY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the CenterZ property.
    STDMETHOD(SetCenterZ)(THIS_
        float centerZ
        ) PURE;

    // Animates the value of the CenterZ property.
    STDMETHOD(SetCenterZ)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionMatrixTransform3D
//
//  Synopsis:
//      An IDCompositionMatrixTransform3D interface represents an arbitrary
//      3D transformation defined by a 4x4 matrix.
//
//------------------------------------------------------------------------------
#pragma warning(push)
#pragma warning(disable : 4995)    // D3DMATRIX': name was marked as #pragma deprecated
#undef INTERFACE
#define INTERFACE IDCompositionMatrixTransform3D
DECLARE_INTERFACE_IID_(IDCompositionMatrixTransform3D, IDCompositionTransform3D, "4B3363F0-643B-41b7-B6E0-CCF22D34467C")
{
    // Changes all values of the matrix of this transform.
    STDMETHOD(SetMatrix)(THIS_
        const D3DMATRIX & matrix
        ) PURE;

    // Changes a single element of the matrix of this transform.
    STDMETHOD(SetMatrixElement)(THIS_
        _In_ int row,
        _In_ int column,
        _In_ float value
        ) PURE;

    // Animates a single element of the matrix of this transform.
    STDMETHOD(SetMatrixElement)(THIS_
        _In_ int row,
        _In_ int column,
        _In_ IDCompositionAnimation * animation
        ) PURE;
};
#pragma warning(pop)

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionClip
//
//  Synopsis:
//      An IDCompositionClip interface represents a rectangle that restricts the
//      rasterization of a visual subtree.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionClip
DECLARE_INTERFACE_IID_(IDCompositionClip, IUnknown, "64AC3703-9D3F-45ec-A109-7CAC0E7A13A7")
{
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionRectangleClip
//
//  Synopsis:
//      An IDCompositionRectangleClip interface represents a rectangle that restricts
//      the rasterization of a visual subtree.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionRectangleClip
DECLARE_INTERFACE_IID_(IDCompositionRectangleClip, IDCompositionClip, "9842AD7D-D9CF-4908-AED7-48B51DA5E7C2")
{
    // Changes the value of the Left property.
    STDMETHOD(SetLeft)(THIS_
        float left
        ) PURE;

    // Animates the value of the Left property.
    STDMETHOD(SetLeft)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the Top property.
    STDMETHOD(SetTop)(THIS_
        float top
        ) PURE;

    // Animates the value of the Top property.
    STDMETHOD(SetTop)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the Right property.
    STDMETHOD(SetRight)(THIS_
        float right
        ) PURE;

    // Animates the value of the Right property.
    STDMETHOD(SetRight)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the Bottom property.
    STDMETHOD(SetBottom)(THIS_
        float bottom
        ) PURE;

    // Animates the value of the Bottom property.
    STDMETHOD(SetBottom)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the x radius of the ellipse that rounds the
    // top-left corner of the clip.
    STDMETHOD(SetTopLeftRadiusX)(THIS_
        float radius
        ) PURE;

    // Animates the value of the x radius of the ellipse that rounds the
    // top-left corner of the clip.
    STDMETHOD(SetTopLeftRadiusX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the y radius of the ellipse that rounds the
    // top-left corner of the clip.
    STDMETHOD(SetTopLeftRadiusY)(THIS_
        float radius
        ) PURE;

    // Animates the value of the y radius of the ellipse that rounds the
    // top-left corner of the clip.
    STDMETHOD(SetTopLeftRadiusY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the x radius of the ellipse that rounds the
    // top-right corner of the clip.
    STDMETHOD(SetTopRightRadiusX)(THIS_
        float radius
        ) PURE;

    // Animates the value of the x radius of the ellipse that rounds the
    // top-right corner of the clip.
    STDMETHOD(SetTopRightRadiusX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the y radius of the ellipse that rounds the
    // top-right corner of the clip.
    STDMETHOD(SetTopRightRadiusY)(THIS_
        float radius
        ) PURE;

    // Animates the value of the y radius of the ellipse that rounds the
    // top-right corner of the clip.
    STDMETHOD(SetTopRightRadiusY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the x radius of the ellipse that rounds the
    // bottom-left corner of the clip.
    STDMETHOD(SetBottomLeftRadiusX)(THIS_
        float radius
        ) PURE;

    // Animates the value of the x radius of the ellipse that rounds the
    // bottom-left corner of the clip.
    STDMETHOD(SetBottomLeftRadiusX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the y radius of the ellipse that rounds the
    // bottom-left corner of the clip.
    STDMETHOD(SetBottomLeftRadiusY)(THIS_
        float radius
        ) PURE;

    // Animates the value of the y radius of the ellipse that rounds the
    // bottom-left corner of the clip.
    STDMETHOD(SetBottomLeftRadiusY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the x radius of the ellipse that rounds the
    // bottom-right corner of the clip.
    STDMETHOD(SetBottomRightRadiusX)(THIS_
        float radius
        ) PURE;

    // Animates the value of the x radius of the ellipse that rounds the
    // bottom-right corner of the clip.
    STDMETHOD(SetBottomRightRadiusX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the y radius of the ellipse that rounds the
    // bottom-right corner of the clip.
    STDMETHOD(SetBottomRightRadiusY)(THIS_
        float radius
        ) PURE;

    // Animates the value of the y radius of the ellipse that rounds the
    // bottom-right corner of the clip.
    STDMETHOD(SetBottomRightRadiusY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionSurface
//
//  Synopsis:
//      An IDCompositionSurface interface represents a wrapper around a DirectX
//      object, or a sub-rectangle of one of those objects.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionSurface
DECLARE_INTERFACE_IID_(IDCompositionSurface, IUnknown, "BB8A4953-2C99-4F5A-96F5-4819027FA3AC")
{
    STDMETHOD(BeginDraw)(THIS_
        _In_opt_ const RECT * updateRect,
        _In_ REFIID iid,
        _Outptr_ void** updateObject,
        _Out_ POINT * updateOffset
        ) PURE;

    STDMETHOD(EndDraw)(THIS
        ) PURE;

    STDMETHOD(SuspendDraw)(THIS
        ) PURE;

    STDMETHOD(ResumeDraw)(THIS
        ) PURE;

    STDMETHOD(Scroll)(THIS_
        _In_opt_ const RECT * scrollRect,
        _In_opt_ const RECT * clipRect,
        _In_ int offsetX,
        _In_ int offsetY
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionVirtualSurface
//
//  Synopsis:
//      An IDCompositionVirtualSurface interface represents a sparsely
//      allocated surface.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionVirtualSurface
DECLARE_INTERFACE_IID_(IDCompositionVirtualSurface, IDCompositionSurface, "AE471C51-5F53-4A24-8D3E-D0C39C30B3F0")
{
    STDMETHOD(Resize)(THIS_
        _In_ UINT width,
        _In_ UINT height
        ) PURE;

    STDMETHOD(Trim)(THIS_
        _In_reads_opt_(count) const RECT * rectangles,
        _In_ UINT count
        ) PURE;
};

#if (_WIN32_WINNT >= _WIN32_WINNT_WINBLUE)
typedef interface IDCompositionDesktopDevice        IDCompositionDesktopDevice;
typedef interface IDCompositionDevice2              IDCompositionDevice2;
typedef interface IDCompositionDeviceDebug          IDCompositionDeviceDebug;
typedef interface IDCompositionSurfaceFactory       IDCompositionSurfaceFactory;
typedef interface IDCompositionVisual2              IDCompositionVisual2;
typedef interface IDCompositionVisualDebug          IDCompositionVisualDebug;

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionDevice2
//
//  Synopsis:
//      Serves as the root factory for all other DirectComposition2 objects and
//      controls transactional composition.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionDevice2
DECLARE_INTERFACE_IID_(IDCompositionDevice2, IUnknown, "75F6468D-1B8E-447C-9BC6-75FEA80B5B25")
{
    // Commits all DirectComposition commands pending on this device.
    STDMETHOD(Commit)(THIS
        ) PURE;

    // Waits for the last Commit to be processed by the composition engine
    STDMETHOD(WaitForCommitCompletion)(THIS
        ) PURE;

    // Gets timing information about the composition engine.
    STDMETHOD(GetFrameStatistics)(THIS_
        _Out_ DCOMPOSITION_FRAME_STATISTICS * statistics
        ) PURE;

    // Creates a new visual object.
    STDMETHOD(CreateVisual)(THIS_
        _Outptr_ IDCompositionVisual2 * *visual
        ) PURE;

    // Creates a factory for surface objects
    STDMETHOD(CreateSurfaceFactory)(THIS_
        _In_ IUnknown * renderingDevice,
        _Outptr_ IDCompositionSurfaceFactory * *surfaceFactory
        ) PURE;

    // Creates a DirectComposition surface object
    STDMETHOD(CreateSurface)(THIS_
        _In_ UINT width,
        _In_ UINT height,
        _In_ DXGI_FORMAT pixelFormat,
        _In_ DXGI_ALPHA_MODE alphaMode,
        _Outptr_ IDCompositionSurface * *surface
        ) PURE;

    // Creates a DirectComposition virtual surface object
    STDMETHOD(CreateVirtualSurface)(THIS_
        _In_ UINT initialWidth,
        _In_ UINT initialHeight,
        _In_ DXGI_FORMAT pixelFormat,
        _In_ DXGI_ALPHA_MODE alphaMode,
        _Outptr_ IDCompositionVirtualSurface * *virtualSurface
        ) PURE;

    // Creates a 2D translation transform object.
    STDMETHOD(CreateTranslateTransform)(THIS_
        _Outptr_ IDCompositionTranslateTransform * *translateTransform
        ) PURE;

    // Creates a 2D scale transform object.
    STDMETHOD(CreateScaleTransform)(THIS_
        _Outptr_ IDCompositionScaleTransform * *scaleTransform
        ) PURE;

    // Creates a 2D rotation transform object.
    STDMETHOD(CreateRotateTransform)(THIS_
        _Outptr_ IDCompositionRotateTransform * *rotateTransform
        ) PURE;

    // Creates a 2D skew transform object.
    STDMETHOD(CreateSkewTransform)(THIS_
        _Outptr_ IDCompositionSkewTransform * *skewTransform
        ) PURE;

    // Creates a 2D 3x2 matrix transform object.
    STDMETHOD(CreateMatrixTransform)(THIS_
        _Outptr_ IDCompositionMatrixTransform * *matrixTransform
        ) PURE;

    // Creates a 2D transform object that holds an array of 2D transform objects.
    STDMETHOD(CreateTransformGroup)(THIS_
        _In_reads_(elements) IDCompositionTransform * *transforms,
        UINT elements,
        _Outptr_ IDCompositionTransform * *transformGroup
        ) PURE;

    // Creates a 3D translation transform object.
    STDMETHOD(CreateTranslateTransform3D)(THIS_
        _Outptr_ IDCompositionTranslateTransform3D * *translateTransform3D
        ) PURE;

    // Creates a 3D scale transform object.
    STDMETHOD(CreateScaleTransform3D)(THIS_
        _Outptr_ IDCompositionScaleTransform3D * *scaleTransform3D
        ) PURE;

    // Creates a 3D rotation transform object.
    STDMETHOD(CreateRotateTransform3D)(THIS_
        _Outptr_ IDCompositionRotateTransform3D * *rotateTransform3D
        ) PURE;

    // Creates a 3D 4x4 matrix transform object.
    STDMETHOD(CreateMatrixTransform3D)(THIS_
        _Outptr_ IDCompositionMatrixTransform3D * *matrixTransform3D
        ) PURE;

    // Creates a 3D transform object that holds an array of 3D transform objects.
    STDMETHOD(CreateTransform3DGroup)(THIS_
        _In_reads_(elements) IDCompositionTransform3D * *transforms3D,
        UINT elements,
        _Outptr_ IDCompositionTransform3D * *transform3DGroup
        ) PURE;

    // Creates an effect group
    STDMETHOD(CreateEffectGroup)(THIS_
        _Outptr_ IDCompositionEffectGroup * *effectGroup
        ) PURE;

    // Creates a clip object that can be used to clip the contents of a visual subtree.
    STDMETHOD(CreateRectangleClip)(THIS_
        _Outptr_ IDCompositionRectangleClip * *clip
        ) PURE;

    // Creates an animation object
    STDMETHOD(CreateAnimation)(THIS_
        _Outptr_ IDCompositionAnimation * *animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionDesktopDevice
//
//  Synopsis:
//      Serves as the root factory for all other desktop DirectComposition
//      objects.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionDesktopDevice
DECLARE_INTERFACE_IID_(IDCompositionDesktopDevice, IDCompositionDevice2, "5F4633FE-1E08-4CB8-8C75-CE24333F5602")
{
    STDMETHOD(CreateTargetForHwnd)(THIS_
        _In_ HWND hwnd,
        BOOL topmost,
        _Outptr_ IDCompositionTarget * *target
        ) PURE;

    // Creates a surface wrapper around a pre-existing surface that can be associated with one or more visuals for composition.
    STDMETHOD(CreateSurfaceFromHandle)(THIS_
        _In_ HANDLE handle,
        _Outptr_ IUnknown * *surface
        ) PURE;

    // Creates a wrapper object that represents the rasterization of a layered window and which can be associated with a visual for composition.
    STDMETHOD(CreateSurfaceFromHwnd)(THIS_
        _In_ HWND hwnd,
        _Outptr_ IUnknown * *surface
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionDeviceDebug
//
//  Synopsis:
//      IDCompositionDeviceDebug serves as a debug interface
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionDeviceDebug
DECLARE_INTERFACE_IID_(IDCompositionDeviceDebug, IUnknown, "A1A3C64A-224F-4A81-9773-4F03A89D3C6C")
{
    // Enables debug counters
    STDMETHOD(EnableDebugCounters)(THIS_
        ) PURE;

    // Enables debug counters
    STDMETHOD(DisableDebugCounters)(THIS_
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionSurfaceFactory
//
//  Synopsis:
//      An IDCompositionSurfaceFactory interface represents an object that can
//      create surfaces suitable for composition.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionSurfaceFactory
DECLARE_INTERFACE_IID_(IDCompositionSurfaceFactory, IUnknown, "E334BC12-3937-4E02-85EB-FCF4EB30D2C8")
{
    // Creates a DirectComposition surface object
    STDMETHOD(CreateSurface)(THIS_
        _In_ UINT width,
        _In_ UINT height,
        _In_ DXGI_FORMAT pixelFormat,
        _In_ DXGI_ALPHA_MODE alphaMode,
        _Outptr_ IDCompositionSurface * *surface
        ) PURE;

    // Creates a DirectComposition virtual surface object
    STDMETHOD(CreateVirtualSurface)(THIS_
        _In_ UINT initialWidth,
        _In_ UINT initialHeight,
        _In_ DXGI_FORMAT pixelFormat,
        _In_ DXGI_ALPHA_MODE alphaMode,
        _Outptr_ IDCompositionVirtualSurface * *virtualSurface
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionVisual2
//
//  Synopsis:
//      An IDCompositionVisual2 interface represents a visual that participates in
//      a visual tree.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionVisual2
DECLARE_INTERFACE_IID_(IDCompositionVisual2, IDCompositionVisual, "E8DE1639-4331-4B26-BC5F-6A321D347A85")
{
    // Changes the interpretation of the opacity property of an effect group
    // associated with this visual
    STDMETHOD(SetOpacityMode)(THIS_
        _In_ DCOMPOSITION_OPACITY_MODE mode
        ) PURE;

    // Sets back face visibility
    STDMETHOD(SetBackFaceVisibility)(THIS_
        _In_ DCOMPOSITION_BACKFACE_VISIBILITY visibility
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionVisualDebug
//
//  Synopsis:
//      An IDCompositionVisualDebug interface represents a debug visual
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionVisualDebug
DECLARE_INTERFACE_IID_(IDCompositionVisualDebug, IDCompositionVisual2, "FED2B808-5EB4-43A0-AEA3-35F65280F91B")
{
    // Enable heat map
    STDMETHOD(EnableHeatMap)(THIS_
        _In_ const D2D1_COLOR_F & color
        ) PURE;

    // Disable heat map
    STDMETHOD(DisableHeatMap)(THIS_
        ) PURE;

    // Enable redraw regions
    STDMETHOD(EnableRedrawRegions)(THIS_
        ) PURE;

    // Disable redraw regions
    STDMETHOD(DisableRedrawRegions)(THIS_
        ) PURE;
};
#endif  // (_WIN32_WINNT >= _WIN32_WINNT_WINBLUE)

#if (_WIN32_WINNT >= _WIN32_WINNT_WINTHRESHOLD)
typedef interface IDCompositionDevice3                      IDCompositionDevice3;
typedef interface IDCompositionVisual3                      IDCompositionVisual3;

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionVisual3
//
//  Synopsis:
//      An IDCompositionVisual3 interface represents a visual that participates in
//      a visual tree.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionVisual3
DECLARE_INTERFACE_IID_(IDCompositionVisual3, IDCompositionVisualDebug, "2775F462-B6C1-4015-B0BE-B3E7D6A4976D")
{
    // Sets depth mode property associated with this visual
    STDMETHOD(SetDepthMode)(THIS_
        _In_ DCOMPOSITION_DEPTH_MODE mode
        ) PURE;

    // Changes the value of OffsetZ property.
    STDMETHOD(SetOffsetZ)(THIS_
        float offsetZ
        ) PURE;

    // Animates the value of the OffsetZ property.
    STDMETHOD(SetOffsetZ)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the value of the Opacity property.
    STDMETHOD(SetOpacity)(THIS_
        float opacity
        ) PURE;

    // Animates the value of the Opacity property.
    STDMETHOD(SetOpacity)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Sets the matrix that modifies the coordinate system of this visual.
    STDMETHOD(SetTransform)(THIS_
        const D2D_MATRIX_4X4_F & matrix
        ) PURE;

    // Sets the transformation object that modifies the coordinate system of this visual.
    STDMETHOD(SetTransform)(THIS_
        _In_opt_ IDCompositionTransform3D * transform
        ) PURE;

    // Changes the value of the Visible property
    STDMETHOD(SetVisible)(THIS_
        BOOL visible
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionDevice3
//
//  Synopsis:
//      Serves as the root factory for all other DirectComposition3 objects and
//      controls transactional composition.
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionDevice3
DECLARE_INTERFACE_IID_(IDCompositionDevice3, IDCompositionDevice2, "0987CB06-F916-48BF-8D35-CE7641781BD9")
{
    // Effect creation calls, each creates an interface around a D2D1Effect
    STDMETHOD(CreateGaussianBlurEffect)(THIS_
        _Outptr_ IDCompositionGaussianBlurEffect * *gaussianBlurEffect
        ) PURE;

    STDMETHOD(CreateBrightnessEffect)(THIS_
        _Outptr_ IDCompositionBrightnessEffect * *brightnessEffect
        ) PURE;

    STDMETHOD(CreateColorMatrixEffect)(THIS_
        _Outptr_ IDCompositionColorMatrixEffect * *colorMatrixEffect
        ) PURE;

    STDMETHOD(CreateShadowEffect)(THIS_
        _Outptr_ IDCompositionShadowEffect * *shadowEffect
        ) PURE;

    STDMETHOD(CreateHueRotationEffect)(THIS_
        _Outptr_ IDCompositionHueRotationEffect * *hueRotationEffect
        ) PURE;

    STDMETHOD(CreateSaturationEffect)(THIS_
        _Outptr_ IDCompositionSaturationEffect * *saturationEffect
        ) PURE;

    STDMETHOD(CreateTurbulenceEffect)(THIS_
        _Outptr_ IDCompositionTurbulenceEffect * *turbulenceEffect
        ) PURE;

    STDMETHOD(CreateLinearTransferEffect)(THIS_
        _Outptr_ IDCompositionLinearTransferEffect * *linearTransferEffect
        ) PURE;

    STDMETHOD(CreateTableTransferEffect)(THIS_
        _Outptr_ IDCompositionTableTransferEffect * *tableTransferEffect
        ) PURE;

    STDMETHOD(CreateCompositeEffect)(THIS_
        _Outptr_ IDCompositionCompositeEffect * *compositeEffect
        ) PURE;

    STDMETHOD(CreateBlendEffect)(THIS_
        _Outptr_ IDCompositionBlendEffect * *blendEffect
        ) PURE;

    STDMETHOD(CreateArithmeticCompositeEffect)(THIS_
        _Outptr_ IDCompositionArithmeticCompositeEffect * *arithmeticCompositeEffect
        ) PURE;

    STDMETHOD(CreateAffineTransform2DEffect)(THIS_
        _Outptr_ IDCompositionAffineTransform2DEffect * *affineTransform2dEffect
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionFilterEffect
//
//  Synopsis:
//      An IDCompositionFilterEffect interface represents a filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionFilterEffect
DECLARE_INTERFACE_IID_(IDCompositionFilterEffect, IDCompositionEffect, "30C421D5-8CB2-4E9F-B133-37BE270D4AC2")
{
    // Sets the input at the given index to the filterEffect (NULL will use source visual, unless flagged otherwise)
    STDMETHOD(SetInput)(THIS_
        _In_ UINT index,
        _In_opt_ IUnknown * input,
        _In_ UINT flags
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionGaussianBlurEffect
//
//  Synopsis:
//      An IDCompositionGaussianBlurEffect interface represents a gaussian blur filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionGaussianBlurEffect
DECLARE_INTERFACE_IID_(IDCompositionGaussianBlurEffect, IDCompositionFilterEffect, "45D4D0B7-1BD4-454E-8894-2BFA68443033")
{
    // Changes the amount of blur to be applied.
    STDMETHOD(SetStandardDeviation)(THIS_
        _In_ float amount
        ) PURE;

    STDMETHOD(SetStandardDeviation)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes border mode (see D2D1_GAUSSIANBLUR)
    STDMETHOD(SetBorderMode)(THIS_
        _In_ D2D1_BORDER_MODE mode
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionBrightnessEffect
//
//  Synopsis:
//      An IDCompositionBrightnessEffect interface represents a brightness filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionBrightnessEffect
DECLARE_INTERFACE_IID_(IDCompositionBrightnessEffect, IDCompositionFilterEffect, "6027496E-CB3A-49AB-934F-D798DA4F7DA6")
{
    // Changes the value of white point property.
    STDMETHOD(SetWhitePoint)(THIS_
        _In_ const D2D1_VECTOR_2F & whitePoint
        ) PURE;

    // Changes the value of black point property
    STDMETHOD(SetBlackPoint)(THIS_
        _In_ const D2D1_VECTOR_2F & blackPoint
        ) PURE;

    // Changes the X value of the white point property.
    STDMETHOD(SetWhitePointX)(THIS_
        _In_ float whitePointX
        ) PURE;

    STDMETHOD(SetWhitePointX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the Y value of the white point property.
    STDMETHOD(SetWhitePointY)(THIS_
        _In_ float whitePointY
        ) PURE;

    STDMETHOD(SetWhitePointY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the X value of the black point property.
    STDMETHOD(SetBlackPointX)(THIS_
        _In_ float blackPointX
        ) PURE;

    STDMETHOD(SetBlackPointX)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the Y value of the black point property.
    STDMETHOD(SetBlackPointY)(THIS_
        _In_ float blackPointY
        ) PURE;

    STDMETHOD(SetBlackPointY)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionColorMatrixEffect
//
//  Synopsis:
//      An IDCompositionColorMatrixEffect interface represents a color matrix filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionColorMatrixEffect
DECLARE_INTERFACE_IID_(IDCompositionColorMatrixEffect, IDCompositionFilterEffect, "C1170A22-3CE2-4966-90D4-55408BFC84C4")
{
    // Changes all values of the matrix for a color transform
    STDMETHOD(SetMatrix)(THIS_
        _In_ const D2D1_MATRIX_5X4_F & matrix
        ) PURE;

    // Changes a single element of the matrix of this color transform.
    STDMETHOD(SetMatrixElement)(THIS_
        _In_ int row,
        _In_ int column,
        _In_ float value
        ) PURE;

    // Animates a single element of the matrix of this color transform.
    STDMETHOD(SetMatrixElement)(THIS_
        _In_ int row,
        _In_ int column,
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes the alpha mode
    STDMETHOD(SetAlphaMode)(THIS_
        _In_ D2D1_COLORMATRIX_ALPHA_MODE mode
        ) PURE;

    // Sets the clamp output property
    STDMETHOD(SetClampOutput)(THIS_
        _In_ BOOL clamp
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionShadowEffect
//
//  Synopsis:
//      An IDCompositionShadowEffect interface represents a shadow filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionShadowEffect
DECLARE_INTERFACE_IID_(IDCompositionShadowEffect, IDCompositionFilterEffect, "4AD18AC0-CFD2-4C2F-BB62-96E54FDB6879")
{
    // Changes the amount of blur to be applied.
    STDMETHOD(SetStandardDeviation)(THIS_
        _In_ float amount
        ) PURE;

    STDMETHOD(SetStandardDeviation)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    // Changes shadow color
    STDMETHOD(SetColor)(THIS_
        _In_ const D2D1_VECTOR_4F & color
        ) PURE;

    STDMETHOD(SetRed)(THIS_
        _In_ float amount
        ) PURE;

    STDMETHOD(SetRed)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetGreen)(THIS_
        _In_ float amount
        ) PURE;

    STDMETHOD(SetGreen)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetBlue)(THIS_
        _In_ float amount
        ) PURE;

    STDMETHOD(SetBlue)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetAlpha)(THIS_
        _In_ float amount
        ) PURE;

    STDMETHOD(SetAlpha)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionHueRotationEffect
//
//  Synopsis:
//      An IDCompositionHueRotationEffect interface represents a hue rotation filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionHueRotationEffect
DECLARE_INTERFACE_IID_(IDCompositionHueRotationEffect, IDCompositionFilterEffect, "6DB9F920-0770-4781-B0C6-381912F9D167")
{
    // Changes the angle of rotation
    STDMETHOD(SetAngle)(THIS_
        _In_ float amountDegrees
        ) PURE;

    STDMETHOD(SetAngle)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionSaturationEffect
//
//  Synopsis:
//      An IDCompositionSaturationEffect interface represents a saturation filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionSaturationEffect
DECLARE_INTERFACE_IID_(IDCompositionSaturationEffect, IDCompositionFilterEffect, "A08DEBDA-3258-4FA4-9F16-9174D3FE93B1")
{
    // Changes the amount of saturation to be applied.
    STDMETHOD(SetSaturation)(THIS_
        _In_ float ratio
        ) PURE;

    STDMETHOD(SetSaturation)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionTurbulenceEffect
//
//  Synopsis:
//      An IDCompositionTurbulenceEffect interface represents a turbulence filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionTurbulenceEffect
DECLARE_INTERFACE_IID_(IDCompositionTurbulenceEffect, IDCompositionFilterEffect, "A6A55BDA-C09C-49F3-9193-A41922C89715")
{
    // Changes the starting offset of the turbulence
    STDMETHOD(SetOffset)(THIS_
        _In_ const D2D1_VECTOR_2F & offset
        ) PURE;

    // Changes the base frequency of the turbulence
    STDMETHOD(SetBaseFrequency)(THIS_
        _In_ const D2D1_VECTOR_2F & frequency
        ) PURE;

    // Changes the output size of the turbulence
    STDMETHOD(SetSize)(THIS_
        _In_ const D2D1_VECTOR_2F & size
        ) PURE;

    // Sets the number of octaves
    STDMETHOD(SetNumOctaves)(THIS_
        _In_ UINT numOctaves
        ) PURE;

    // Set the random number seed
    STDMETHOD(SetSeed)(THIS_
        _In_ UINT seed
        ) PURE;

    // Set the noise mode
    STDMETHOD(SetNoise)(THIS_
        _In_ D2D1_TURBULENCE_NOISE noise
        ) PURE;

    // Set stitchable
    STDMETHOD(SetStitchable)(THIS_
        _In_ BOOL stitchable
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionLinearTransferEffect
//
//  Synopsis:
//      An IDCompositionLinearTransferEffect interface represents a linear transfer filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionLinearTransferEffect
DECLARE_INTERFACE_IID_(IDCompositionLinearTransferEffect, IDCompositionFilterEffect, "4305EE5B-C4A0-4C88-9385-67124E017683")
{

    STDMETHOD(SetRedYIntercept)(THIS_
        _In_ float redYIntercept
        ) PURE;

    STDMETHOD(SetRedYIntercept)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetRedSlope)(THIS_
        _In_ float redSlope
        ) PURE;

    STDMETHOD(SetRedSlope)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetRedDisable)(THIS_
        _In_ BOOL redDisable
        ) PURE;

    STDMETHOD(SetGreenYIntercept)(THIS_
        _In_ float greenYIntercept
        ) PURE;

    STDMETHOD(SetGreenYIntercept)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetGreenSlope)(THIS_
        _In_ float greenSlope
        ) PURE;

    STDMETHOD(SetGreenSlope)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetGreenDisable)(THIS_
        _In_ BOOL greenDisable
        ) PURE;

    STDMETHOD(SetBlueYIntercept)(THIS_
        _In_ float blueYIntercept
        ) PURE;

    STDMETHOD(SetBlueYIntercept)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetBlueSlope)(THIS_
        _In_ float blueSlope
        ) PURE;

    STDMETHOD(SetBlueSlope)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetBlueDisable)(THIS_
        _In_ BOOL blueDisable
        ) PURE;

    STDMETHOD(SetAlphaYIntercept)(THIS_
        _In_ float alphaYIntercept
        ) PURE;

    STDMETHOD(SetAlphaYIntercept)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetAlphaSlope)(THIS_
        _In_ float alphaSlope
        ) PURE;

    STDMETHOD(SetAlphaSlope)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetAlphaDisable)(THIS_
        _In_ BOOL alphaDisable
        ) PURE;

    STDMETHOD(SetClampOutput)(THIS_
        _In_ BOOL clampOutput
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionTableTransferEffect
//
//  Synopsis:
//      An IDCompositionTableTransferEffect interface represents a Table transfer filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionTableTransferEffect
DECLARE_INTERFACE_IID_(IDCompositionTableTransferEffect, IDCompositionFilterEffect, "9B7E82E2-69C5-4EB4-A5F5-A7033F5132CD")
{
    STDMETHOD(SetRedTable)(THIS_
        _In_count_(count) const float* tableValues,
        _In_ UINT count
        ) PURE;

    STDMETHOD(SetGreenTable)(THIS_
        _In_count_(count) const float* tableValues,
        _In_ UINT count
        ) PURE;

    STDMETHOD(SetBlueTable)(THIS_
        _In_count_(count) const float* tableValues,
        _In_ UINT count
        ) PURE;

    STDMETHOD(SetAlphaTable)(THIS_
        _In_count_(count) const float* tableValues,
        _In_ UINT count
        ) PURE;

    STDMETHOD(SetRedDisable)(THIS_
        _In_ BOOL redDisable
        ) PURE;

    STDMETHOD(SetGreenDisable)(THIS_
        _In_ BOOL greenDisable
        ) PURE;

    STDMETHOD(SetBlueDisable)(THIS_
        _In_ BOOL blueDisable
        ) PURE;

    STDMETHOD(SetAlphaDisable)(THIS_
        _In_ BOOL alphaDisable
        ) PURE;

    STDMETHOD(SetClampOutput)(THIS_
        _In_ BOOL clampOutput
        ) PURE;

    // Note:  To set individual values, the table must have already been initialized
    //        with a buffer of values of the appropriate size, or these calls will fail
    STDMETHOD(SetRedTableValue)(THIS_
        _In_ UINT index,
        _In_ float value
        ) PURE;

    STDMETHOD(SetRedTableValue)(THIS_
        _In_ UINT index,
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetGreenTableValue)(THIS_
        _In_ UINT index,
        _In_ float value
        ) PURE;

    STDMETHOD(SetGreenTableValue)(THIS_
        _In_ UINT index,
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetBlueTableValue)(THIS_
        _In_ UINT index,
        _In_ float value
        ) PURE;

    STDMETHOD(SetBlueTableValue)(THIS_
        _In_ UINT index,
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetAlphaTableValue)(THIS_
        _In_ UINT index,
        _In_ float value
        ) PURE;

    STDMETHOD(SetAlphaTableValue)(THIS_
        _In_ UINT index,
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionCompositeEffect
//
//  Synopsis:
//      An IDCompositionCompositeEffect interface represents a composite filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionCompositeEffect
DECLARE_INTERFACE_IID_(IDCompositionCompositeEffect, IDCompositionFilterEffect, "576616C0-A231-494D-A38D-00FD5EC4DB46")
{
    // Changes the composite mode.
    STDMETHOD(SetMode)(THIS_
        _In_ D2D1_COMPOSITE_MODE mode
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionBlendEffect
//
//  Synopsis:
//      An IDCompositionBlendEffect interface represents a blend filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionBlendEffect
DECLARE_INTERFACE_IID_(IDCompositionBlendEffect, IDCompositionFilterEffect, "33ECDC0A-578A-4A11-9C14-0CB90517F9C5")
{
    STDMETHOD(SetMode)(THIS_
        _In_ D2D1_BLEND_MODE mode
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionArithmeticCompositeEffect
//
//  Synopsis:
//      An IDCompositionArithmeticCompositeEffect interface represents an arithmetic composite filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionArithmeticCompositeEffect
DECLARE_INTERFACE_IID_(IDCompositionArithmeticCompositeEffect, IDCompositionFilterEffect, "3B67DFA8-E3DD-4E61-B640-46C2F3D739DC")
{
    STDMETHOD(SetCoefficients)(THIS_
        _In_ const D2D1_VECTOR_4F & coefficients
        ) PURE;

    STDMETHOD(SetClampOutput)(THIS_
        _In_ BOOL clampoutput
        ) PURE;

    STDMETHOD(SetCoefficient1)(THIS_
        _In_ float Coeffcient1
        ) PURE;

    STDMETHOD(SetCoefficient1)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetCoefficient2)(THIS_
        _In_ float Coefficient2
        ) PURE;

    STDMETHOD(SetCoefficient2)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetCoefficient3)(THIS_
        _In_ float Coefficient3
        ) PURE;

    STDMETHOD(SetCoefficient3)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetCoefficient4)(THIS_
        _In_ float Coefficient4
        ) PURE;

    STDMETHOD(SetCoefficient4)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

//+-----------------------------------------------------------------------------
//
//  Interface:
//      IDCompositionAffineTransform2DEffect
//
//  Synopsis:
//      An IDCompositionAffineTransform2DEffect interface represents a affine transform 2D filter effect
//
//------------------------------------------------------------------------------
#undef INTERFACE
#define INTERFACE IDCompositionAffineTransform2DEffect
DECLARE_INTERFACE_IID_(IDCompositionAffineTransform2DEffect, IDCompositionFilterEffect, "0B74B9E8-CDD6-492F-BBBC-5ED32157026D")
{
    STDMETHOD(SetInterpolationMode)(THIS_
        _In_ D2D1_2DAFFINETRANSFORM_INTERPOLATION_MODE interpolationMode
        ) PURE;

    STDMETHOD(SetBorderMode)(THIS_
        _In_ D2D1_BORDER_MODE borderMode
        ) PURE;

    STDMETHOD(SetTransformMatrix)(THIS_
        _In_ const D2D1_MATRIX_3X2_F & transformMatrix
        ) PURE;

    STDMETHOD(SetTransformMatrixElement)(THIS_
        _In_ int row,
        _In_ int column,
        _In_ float value
        ) PURE;

    STDMETHOD(SetTransformMatrixElement)(THIS_
        _In_ int row,
        _In_ int column,
        _In_ IDCompositionAnimation * animation
        ) PURE;

    STDMETHOD(SetSharpness)(THIS_
        _In_ float sharpness
        ) PURE;

    STDMETHOD(SetSharpness)(THIS_
        _In_ IDCompositionAnimation * animation
        ) PURE;
};

#undef INTERFACE
#define INTERFACE IDCompositionFloodEffect
DECLARE_INTERFACE_IID_(IDCompositionFloodEffect, IDCompositionFilterEffect, "949758CA-BC9C-4C51-907D-A12613639331")
{
    // Changes the color to be applied.
    STDMETHOD(SetColor)(THIS_
        _In_ const D2D1_VECTOR_4F & color
        ) PURE;

};

#undef INTERFACE
#define INTERFACE IDCompositionDesktopDeviceRestricted
DECLARE_INTERFACE_IID_(IDCompositionDesktopDeviceRestricted, IUnknown, "6083f66a-8533-4f7e-a37e-6c222134280b")
{
    STDMETHOD(Dummy1)(THIS_) PURE;
    STDMETHOD(Dummy2)(THIS_) PURE;
    STDMETHOD(CreateFloodEffect)(THIS_
        _Outptr_ IDCompositionFloodEffect **floodEffect
        ) PURE;
};

//#pragma pack (8)
//[uuid("F26DA89E-683D-4C67-AEA7-BA29B2217A7F")]
//interface class ICompositionVisualSurfaceLegacy
//{
//public:
//    property Windows::Foundation::Numerics::float2 RealizationSize;
//    property Windows::UI::Composition::Visual^ Source;
//    property float SourceRectangleBottom;
//    property float SourceRectangleLeft;
//    property float SourceRectangleRight;
//    property float SourceRectangleTop;
//    property Windows::UI::Composition::CompositionStretch Stretch;
//
//    void Freeze();
//};

#endif  // (_WIN32_WINNT >= _WIN32_WINNT_WINTHRESHOLD)

#undef INTERFACE
#endif // NTDDI_WIN8

#endif /* WINAPI_FAMILY_PARTITION(WINAPI_PARTITION_DESKTOP) */
#pragma endregion