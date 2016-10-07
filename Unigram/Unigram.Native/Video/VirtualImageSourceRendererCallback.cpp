#include "pch.h"
#include "VideoImageSourceRenderer.h"
#include "VirtualImageSourceRendererCallback.h"

using namespace Mp4ImageSourceRenderer;

static HMODULE GetModuleHandle(LPCTSTR libFileName)
{
	typedef HMODULE(WINAPI *pGetModuleHandle)(__in_opt LPCTSTR);

	MEMORY_BASIC_INFORMATION mbi;
	if (VirtualQuery(VirtualQuery, &mbi, sizeof(MEMORY_BASIC_INFORMATION)) == 0)
		throw ref new COMException(HRESULT_FROM_WIN32(GetLastError()));

	return reinterpret_cast<pGetModuleHandle>(GetProcAddress(reinterpret_cast<HMODULE>(mbi.AllocationBase),
		"GetModuleHandleW"))(libFileName);
}

static HRESULT MFScheduleWorkItem(_In_ IMFAsyncCallback* pCallback, _In_ IUnknown* pState, _In_ INT64 Timeout, _Out_ MFWORKITEM_KEY* pKey)
{
	typedef HRESULT(WINAPI *pMFScheduleWorkItem)(_In_ IMFAsyncCallback*, _In_ IUnknown*, _In_ INT64, _Out_ MFWORKITEM_KEY*);
	static const auto procMFScheduleWorkItem = reinterpret_cast<pMFScheduleWorkItem>(GetProcAddress(GetModuleHandle(L"Mfplat.dll"), "MFScheduleWorkItem"));

	return procMFScheduleWorkItem(pCallback, pState, Timeout, pKey);
}

VirtualImageSourceRendererCallback::VirtualImageSourceRendererCallback(VideoImageSourceRenderer^ renderer) :
	m_renderer(renderer),
	m_timerKey(NULL),
	m_timerDispatchedHandler(ref new DispatchedHandler([this] {  m_renderer->OnTimerTick(); }, CallbackContext::Any))
{
}

VirtualImageSourceRendererCallback::~VirtualImageSourceRendererCallback()
{
	MFCancelWorkItem(m_timerKey);
}

HRESULT VirtualImageSourceRendererCallback::StartTimer(int64 duration)
{
	HRESULT result;
	if (m_timerKey != NULL)
		ReturnIfFailed(result, MFCancelWorkItem(m_timerKey));

	m_duration = -(duration / 10000);
	if (FAILED(result = MFScheduleWorkItem(this, nullptr, m_duration, &m_timerKey)))
		m_duration = 0;

	return result;
}

HRESULT VirtualImageSourceRendererCallback::ResumeTimer()
{
	if (m_duration >= 0)
		return S_FALSE;

	HRESULT result;
	if (m_timerKey != NULL)
		ReturnIfFailed(result, MFCancelWorkItem(m_timerKey));

	return MFScheduleWorkItem(this, nullptr, m_duration, &m_timerKey);
}

HRESULT VirtualImageSourceRendererCallback::StopTimer()
{
	if (m_timerKey == NULL)
		return S_FALSE;

	HRESULT result;
	ReturnIfFailed(result, MFCancelWorkItem(m_timerKey));

	m_timerKey = NULL;
	return S_OK;
}

HRESULT VirtualImageSourceRendererCallback::UpdatesNeeded()
{
	return m_renderer->OnUpdatesNeeded();
}

HRESULT VirtualImageSourceRendererCallback::Invoke(IMFAsyncResult* pAsyncResult)
{
	HRESULT result;
	ReturnIfFailed(result, MFScheduleWorkItem(this, nullptr, m_duration, &m_timerKey));

	m_renderer->ImageSource->Dispatcher->RunAsync(CoreDispatcherPriority::Normal, m_timerDispatchedHandler);
	return S_OK;
}

HRESULT VirtualImageSourceRendererCallback::GetParameters(DWORD* pdwFlags, DWORD* pdwQueue)
{
	*pdwFlags = MFASYNC_SIGNAL_CALLBACK;
	*pdwQueue = MFASYNC_CALLBACK_QUEUE_TIMER;
	return S_OK;
}