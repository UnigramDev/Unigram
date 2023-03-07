#include "pch.h"
#include "ScreenshotManager.h"
#if __has_include("ScreenshotManager.g.cpp")
#include "ScreenshotManager.g.cpp"
#endif

#include "Helpers\LibraryHelper.h"

#include <winrt/Windows.UI.Core.h>
#include <winrt/Microsoft.UI.Xaml.Media.h>
#include <winrt/Microsoft.UI.Xaml.Media.Imaging.h>

using namespace winrt::Windows::UI::Core;
using namespace winrt::Microsoft::UI::Xaml::Media;
using namespace winrt::Microsoft::UI::Xaml::Media::Imaging;

struct
	__declspec(uuid("45D64A29-A63E-4CB6-B498-5781D298CB4F"))
	__declspec(novtable)
	ICoreWindowInterop : public IUnknown
{
	virtual HRESULT STDMETHODCALLTYPE get_WindowHandle(HWND* hwnd) = 0;
	virtual HRESULT STDMETHODCALLTYPE put_MessageHandled(unsigned char value) = 0;
};

typedef struct tagMONITORINFO
{
	DWORD   cbSize;
	RECT    rcMonitor;
	RECT    rcWork;
	DWORD   dwFlags;
} MONITORINFO, * LPMONITORINFO;

namespace winrt::Unigram::Native::implementation
{
	ImageSource ScreenshotManager::Capture()
	{
		typedef HBITMAP(WINAPI* pCreateCompatibleBitmap)(_In_ HDC hdc, _In_ int cx, _In_ int cy);
		typedef HDC(WINAPI* pCreateCompatibleDC)(_In_opt_ HDC hdc);
		typedef BOOL(WINAPI* pDeleteObject)(_In_ HGDIOBJ ho);
		typedef int(WINAPI* pGetDIBits)(_In_ HDC hdc, _In_ HBITMAP hbm, _In_ UINT start, _In_ UINT cLines,
			_Out_opt_ LPVOID lpvBits, _At_((LPBITMAPINFOHEADER)lpbmi, _Inout_) LPBITMAPINFO lpbmi, _In_ UINT usage);  // SAL actual size of lpbmi is computed from structure elements
		typedef HGDIOBJ(WINAPI* pSelectObject)(_In_ HDC hdc, _In_ HGDIOBJ h);

		typedef HDC(WINAPI* pGetDC)(_In_opt_ HWND hWnd);
		typedef BOOL(WINAPI* pGetWindowRect)(_In_ HWND hWnd, _Out_ LPRECT lpRect);
		typedef BOOL(WINAPI* pPrintWindow)(_In_ HWND hwnd, _In_ HDC hdcBlt, _In_ UINT nFlags);
		typedef int(WINAPI* pReleaseDC)(_In_opt_ HWND hWnd, _In_ HDC hDC);

		static const LibraryInstance gdi32(L"gdi32.dll", 0x00000001);
		static const auto createCompatibleBitmap = gdi32.GetMethod<pCreateCompatibleBitmap>("CreateCompatibleBitmap");
		static const auto createCompatibleDC = gdi32.GetMethod<pCreateCompatibleDC>("CreateCompatibleDC");
		static const auto deleteObject = gdi32.GetMethod<pDeleteObject>("DeleteObject");
		static const auto getDIBits = gdi32.GetMethod<pGetDIBits>("GetDIBits");
		static const auto selectObject = gdi32.GetMethod<pSelectObject>("SelectObject");

		static const LibraryInstance user32(L"User32.dll", 0x00000001);
		static const auto getDC = user32.GetMethod<pGetDC>("GetDC");
		static const auto getWindowRect = user32.GetMethod<pGetWindowRect>("GetWindowRect");
		static const auto printWindow = user32.GetMethod<pPrintWindow>("PrintWindow");
		static const auto releaseDC = user32.GetMethod<pReleaseDC>("ReleaseDC");

		auto window = CoreWindow::GetForCurrentThread();
		winrt::com_ptr<ICoreWindowInterop> interop{};
		winrt::check_hresult(winrt::get_unknown(window)->QueryInterface(interop.put()));
		HWND hWnd{};
		winrt::check_hresult(interop->get_WindowHandle(&hWnd));

		RECT rect;
		getWindowRect(hWnd, &rect);

		HDC hDC = getDC(hWnd);

		HDC hDCMem = createCompatibleDC(NULL);
		HBITMAP hBmp = createCompatibleBitmap(hDC, rect.right - rect.left, rect.bottom - rect.top);

		HGDIOBJ hOld = selectObject(hDCMem, hBmp);
		printWindow(hWnd, hDCMem, 0x00000002);

		selectObject(hDCMem, hOld);
		deleteObject(hDCMem);

		BITMAPINFO lpbi = { 0 };
		lpbi.bmiHeader.biSize = sizeof(lpbi);

		if (0 == getDIBits(hDC, hBmp, 0, 0, NULL, &lpbi, DIB_RGB_COLORS))
		{
			releaseDC(hWnd, hDC);
			return nullptr;
		}

		lpbi.bmiHeader.biBitCount = 32;
		lpbi.bmiHeader.biCompression = BI_RGB;
		lpbi.bmiHeader.biHeight = abs(lpbi.bmiHeader.biHeight);

		//BYTE* lpPixels = new BYTE[lpbi.bmiHeader.biSizeImage];
		WriteableBitmap lpBitmap(lpbi.bmiHeader.biWidth, lpbi.bmiHeader.biHeight);
		uint8_t* lpPixels = lpBitmap.PixelBuffer().data();

		// Call GetDIBits a second time, this time to (format and) store the actual
		// bitmap data (the "pixels") in the buffer lpPixels
		if (0 == getDIBits(hDC, hBmp, 0, lpbi.bmiHeader.biHeight, lpPixels, &lpbi, DIB_RGB_COLORS))
		{
			releaseDC(hWnd, hDC);
			return nullptr;
		}

		return lpBitmap;
	}

	winrt::Windows::Foundation::Rect ScreenshotManager::GetWorkingArea()
	{
		typedef HMONITOR(WINAPI* pMonitorFromWindow)(_In_ HWND hwnd, _In_ DWORD dwFlags);
		typedef BOOL(WINAPI* pGetMonitorInfoW)(_In_ HMONITOR hMonitor, _Inout_ LPMONITORINFO lpmi);

		static const LibraryInstance user32(L"User32.dll", 0x00000001);
		static const auto monitorFromWindow = user32.GetMethod<pMonitorFromWindow>("MonitorFromWindow");
		static const auto getMonitorInfoW = user32.GetMethod<pGetMonitorInfoW>("GetMonitorInfoW");

		if (monitorFromWindow && getMonitorInfoW)
		{
			auto window = CoreWindow::GetForCurrentThread();
			winrt::com_ptr<ICoreWindowInterop> interop{};
			winrt::check_hresult(winrt::get_unknown(window)->QueryInterface(interop.put()));
			HWND hWnd{};
			winrt::check_hresult(interop->get_WindowHandle(&hWnd));

			MONITORINFO mi;
			HMONITOR monitor = monitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
			mi.cbSize = sizeof(mi);

			if (getMonitorInfoW(monitor, &mi))
			{
				return winrt::Windows::Foundation::Rect(mi.rcWork.left, mi.rcWork.top, mi.rcWork.right - mi.rcWork.left, mi.rcWork.bottom - mi.rcWork.top);
			}
		}

		return winrt::Windows::Foundation::Rect();
	}
}
