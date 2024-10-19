#pragma once

#include <Windows.h>
#include <dwmapi.h>
#include "dcomp.local.h"

enum THUMBNAIL_TYPE
{
	TT_DEFAULT = 0x0,
	TT_SNAPSHOT = 0x1,
	TT_ICONIC = 0x2,
	TT_BITMAPPENDING = 0x3,
	TT_BITMAP = 0x4
};

typedef struct _DWM_THUMBNAIL_PROPERTIES
{
	DWORD dwFlags;              // Specifies which members of this struct have been specified
	RECT rcDestination;         // The area in the destination window where the thumbnail will be rendered
	RECT rcSource;              // The region of the source window to use as the thumbnail.  By default, the entire window is used as the thumbnail
	BYTE opacity;               // The opacity with which to render the thumbnail.  0 is fully transparent, while 255 is fully opaque.  The default value is 255
	BOOL fVisible;              // Whether the thumbnail should be visible.  The default is FALSE
	BOOL fSourceClientAreaOnly; // Whether only the client area of the source window should be included in the thumbnail.  The default is FALSE
} DWM_THUMBNAIL_PROPERTIES, * PDWM_THUMBNAIL_PROPERTIES;

typedef HANDLE      HTHUMBNAIL;
typedef HTHUMBNAIL* PHTHUMBNAIL;

typedef HRESULT(WINAPI* DwmpCreateSharedThumbnailVisual)(
	IN HWND hwndDestination,
	IN HWND hwndSource,
	IN DWORD dwThumbnailFlags,
	IN DWM_THUMBNAIL_PROPERTIES* pThumbnailProperties,
	IN VOID* pDCompDevice,
	OUT VOID** ppVisual,
	OUT PHTHUMBNAIL phThumbnailId);

typedef HRESULT(WINAPI* DwmpQueryWindowThumbnailSourceSize)(
	IN HWND hwndSource,
	IN BOOL fSourceClientAreaOnly,
	OUT SIZE* pSize);

typedef HRESULT(WINAPI* DwmQueryThumbnailSourceSize)(
	IN HTHUMBNAIL hThumbnail,
	OUT SIZE* pSize);

typedef HRESULT(WINAPI* DwmUpdateThumbnailProperties)(
	IN HTHUMBNAIL hThumbnailId,
	IN const DWM_THUMBNAIL_PROPERTIES* ptnProperties);

typedef HRESULT(WINAPI* DwmpQueryThumbnailType)(
	IN HTHUMBNAIL hThumbnailId,
	OUT THUMBNAIL_TYPE* thumbType);

typedef HRESULT(WINAPI* DwmUnregisterThumbnail)(
	IN HTHUMBNAIL hThumbnailId);

#define DWM_TNP_FREEZE            0x100000
#define DWM_TNP_ENABLE3D          0x4000000
#define DWM_TNP_DISABLE3D         0x8000000
#define DWM_TNP_FORCECVI          0x40000000
#define DWM_TNP_DISABLEFORCECVI   0x80000000

#pragma region Flags for DWM_THUMBNAIL_PROPERTIES
#define DWM_TNP_RECTDESTINATION                  0x00000001 // A value for the "rcDestination" member has been specified.
#define DWM_TNP_RECTSOURCE                       0x00000002 // A value for the "rcSource" member has been specified.
#define DWM_TNP_OPACITY                          0x00000004 // A value for the "opacity" member has been specified.
#define DWM_TNP_VISIBLE                          0x00000008 // A value for the "fVisible" member has been specified.
#define DWM_TNP_SOURCECLIENTAREAONLY             0x00000010 // A value for the "fSourceClientAreaOnly" member has been specified.
#pragma endregion

typedef LRESULT(CALLBACK* WNDPROC)(HWND, UINT, WPARAM, LPARAM);

typedef struct tagWNDCLASSW {
	UINT        style;
	WNDPROC     lpfnWndProc;
	int         cbClsExtra;
	int         cbWndExtra;
	HINSTANCE   hInstance;
	HICON       hIcon;
	HCURSOR     hCursor;
	HBRUSH      hbrBackground;
	LPCWSTR     lpszMenuName;
	LPCWSTR     lpszClassName;
} WNDCLASSW, * PWNDCLASSW, NEAR* NPWNDCLASSW, FAR* LPWNDCLASSW, WNDCLASS;

MIDL_INTERFACE("45D64A29-A63E-4CB6-B498-5781D298CB4F")
ICoreWindowInterop : public IUnknown
{
public:
	virtual /* [propget] */ HRESULT STDMETHODCALLTYPE get_WindowHandle(
		/* [retval][out] */ __RPC__deref_out_opt HWND * hwnd) = 0;

	virtual /* [propput] */ HRESULT STDMETHODCALLTYPE put_MessageHandled(
		/* [in] */ boolean value) = 0;

};

typedef HWND
(WINAPI*
	FindWindowWProto)(
		_In_opt_ LPCWSTR lpClassName,
		_In_opt_ LPCWSTR lpWindowName);

typedef BOOL
(WINAPI*
GetWindowRectProto)(
	_In_ HWND hWnd,
	_Out_ LPRECT lpRect);

typedef HWND
(WINAPI*
GetParentProto)(
	_In_ HWND hWnd);

enum WINDOWCOMPOSITIONATTRIB
{
	WCA_UNDEFINED = 0x0,
	WCA_NCRENDERING_ENABLED = 0x1,
	WCA_NCRENDERING_POLICY = 0x2,
	WCA_TRANSITIONS_FORCEDISABLED = 0x3,
	WCA_ALLOW_NCPAINT = 0x4,
	WCA_CAPTION_BUTTON_BOUNDS = 0x5,
	WCA_NONCLIENT_RTL_LAYOUT = 0x6,
	WCA_FORCE_ICONIC_REPRESENTATION = 0x7,
	WCA_EXTENDED_FRAME_BOUNDS = 0x8,
	WCA_HAS_ICONIC_BITMAP = 0x9,
	WCA_THEME_ATTRIBUTES = 0xA,
	WCA_NCRENDERING_EXILED = 0xB,
	WCA_NCADORNMENTINFO = 0xC,
	WCA_EXCLUDED_FROM_LIVEPREVIEW = 0xD,
	WCA_VIDEO_OVERLAY_ACTIVE = 0xE,
	WCA_FORCE_ACTIVEWINDOW_APPEARANCE = 0xF,
	WCA_DISALLOW_PEEK = 0x10,
	WCA_CLOAK = 0x11,
	WCA_CLOAKED = 0x12,
	WCA_ACCENT_POLICY = 0x13,
	WCA_FREEZE_REPRESENTATION = 0x14,
	WCA_EVER_UNCLOAKED = 0x15,
	WCA_VISUAL_OWNER = 0x16,
	WCA_HOLOGRAPHIC = 0x17,
	WCA_EXCLUDED_FROM_DDA = 0x18,
	WCA_PASSIVEUPDATEMODE = 0x19,
	WCA_LAST = 0x1A,
};

typedef struct WINDOWCOMPOSITIONATTRIBDATA
{
	WINDOWCOMPOSITIONATTRIB Attrib;
	void* pvData;
	DWORD cbData;
};

typedef BOOL(WINAPI* SetWindowCompositionAttribute)(
	IN HWND hwnd,
	IN WINDOWCOMPOSITIONATTRIBDATA* pwcad);