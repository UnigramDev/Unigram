#pragma once

#define IUnknown ::IUnknown
#include <UIAnimation.h>

typedef struct
{
	LARGE_INTEGER lastFrameTime;
	DXGI_RATIONAL currentCompositionRate;
	LARGE_INTEGER currentTime;
	LARGE_INTEGER timeFrequency;
	LARGE_INTEGER nextEstimatedFrameTime;
} DCOMPOSITION_FRAME_STATISTICS;

MIDL_INTERFACE("CBFD91D9-51B2-45e4-B3DE-D19CCFB863C5")
IDCompositionAnimation : public IUnknown
{
public:
	virtual HRESULT STDMETHODCALLTYPE Reset(void) = 0;

	virtual HRESULT STDMETHODCALLTYPE SetAbsoluteBeginTime(
		LARGE_INTEGER beginTime) = 0;

	virtual HRESULT STDMETHODCALLTYPE AddCubic(
		double beginOffset,
		float constantCoefficient,
		float linearCoefficient,
		float quadraticCoefficient,
		float cubicCoefficient) = 0;

	virtual HRESULT STDMETHODCALLTYPE AddSinusoidal(
		double beginOffset,
		float bias,
		float amplitude,
		float frequency,
		float phase) = 0;

	virtual HRESULT STDMETHODCALLTYPE AddRepeat(
		double beginOffset,
		double durationToRepeat) = 0;

	virtual HRESULT STDMETHODCALLTYPE End(
		double endOffset,
		float endValue) = 0;

};

#undef INTERFACE
#define INTERFACE IDCompositionClip
DECLARE_INTERFACE_IID_(IDCompositionClip, IUnknown, "64AC3703-9D3F-45ec-A109-7CAC0E7A13A7")
{
};

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
		) PURE;

	// Sets the visual that should act as this visual's parent for the
	// purpose of establishing a base coordinate system.
	STDMETHOD(SetTransformParent)(THIS_
		_In_opt_ IDCompositionVisual * visual
		) PURE;

	// Sets the effect object that is applied during the rendering of this visual
	STDMETHOD(SetEffect)(THIS_
		) PURE;

	// Sets the mode to use when interpolating pixels from bitmaps drawn not
	// exactly at scale and axis-aligned.
	STDMETHOD(SetBitmapInterpolationMode)(THIS_
		) PURE;

	// Sets the mode to use when drawing the edge of bitmaps that are not
	// exactly axis-aligned and at precise pixel boundaries.
	STDMETHOD(SetBorderMode)(THIS_
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
		) PURE;
};

#undef INTERFACE
#define INTERFACE IDCompositionVisual2
DECLARE_INTERFACE_IID_(IDCompositionVisual2, IDCompositionVisual, "E8DE1639-4331-4B26-BC5F-6A321D347A85")
{
	// Changes the interpretation of the opacity property of an effect group
	// associated with this visual
	STDMETHOD(SetOpacityMode)(THIS_
		) PURE;

	// Sets back face visibility
	STDMETHOD(SetBackFaceVisibility)(THIS_
		) PURE;
};

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
        _Out_ DCOMPOSITION_FRAME_STATISTICS *statistics
		) PURE;

	// Creates a new visual object.
	STDMETHOD(CreateVisual)(THIS_
		) PURE;

	// Creates a factory for surface objects
	STDMETHOD(CreateSurfaceFactory)(THIS_
		) PURE;

	// Creates a DirectComposition surface object
	STDMETHOD(CreateSurface)(THIS_
		) PURE;

	// Creates a DirectComposition virtual surface object
	STDMETHOD(CreateVirtualSurface)(THIS_
		) PURE;

	// Creates a 2D translation transform object.
	STDMETHOD(CreateTranslateTransform)(THIS_
		) PURE;

	// Creates a 2D scale transform object.
	STDMETHOD(CreateScaleTransform)(THIS_
		) PURE;

	// Creates a 2D rotation transform object.
	STDMETHOD(CreateRotateTransform)(THIS_
		) PURE;

	// Creates a 2D skew transform object.
	STDMETHOD(CreateSkewTransform)(THIS_
		) PURE;

	// Creates a 2D 3x2 matrix transform object.
	STDMETHOD(CreateMatrixTransform)(THIS_
		) PURE;

	// Creates a 2D transform object that holds an array of 2D transform objects.
	STDMETHOD(CreateTransformGroup)(THIS_
		) PURE;

	// Creates a 3D translation transform object.
	STDMETHOD(CreateTranslateTransform3D)(THIS_
		) PURE;

	// Creates a 3D scale transform object.
	STDMETHOD(CreateScaleTransform3D)(THIS_
		) PURE;

	// Creates a 3D rotation transform object.
	STDMETHOD(CreateRotateTransform3D)(THIS_
		) PURE;

	// Creates a 3D 4x4 matrix transform object.
	STDMETHOD(CreateMatrixTransform3D)(THIS_
		) PURE;

	// Creates a 3D transform object that holds an array of 3D transform objects.
	STDMETHOD(CreateTransform3DGroup)(THIS_
		) PURE;

	// Creates an effect group
	STDMETHOD(CreateEffectGroup)(THIS_
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

#undef INTERFACE
#define INTERFACE IDCompositionDesktopDevice
DECLARE_INTERFACE_IID_(IDCompositionDesktopDevice, IDCompositionDevice2, "5F4633FE-1E08-4CB8-8C75-CE24333F5602")
{
	STDMETHOD(CreateTargetForHwnd)(THIS_
		) PURE;

	// Creates a surface wrapper around a pre-existing surface that can be associated with one or more visuals for composition.
	STDMETHOD(CreateSurfaceFromHandle)(THIS_
		) PURE;

	// Creates a wrapper object that represents the rasterization of a layered window and which can be associated with a visual for composition.
	STDMETHOD(CreateSurfaceFromHwnd)(THIS_
		) PURE;
};

#undef IUnknown