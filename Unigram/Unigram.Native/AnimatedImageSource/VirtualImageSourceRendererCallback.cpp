// Copyright (c) 2017 Lorenzo Rossoni

#include "pch.h"
#include "MediaFoundationExtensions.h"
#include "AnimatedImageSourceRenderer.h"
#include "VirtualImageSourceRendererCallback.h"
#include "Helpers\COMHelper.h"

using namespace Unigram::Native;


VirtualImageSourceRendererCallback::VirtualImageSourceRendererCallback(AnimatedImageSourceRenderer^ renderer) :
	m_renderer(renderer),
	m_timerKey(NULL),
	m_timerDispatchedHandler(ref new DispatchedHandler([this] {  m_renderer->OnTimerTick(); }, CallbackContext::Any))
{
}

VirtualImageSourceRendererCallback::~VirtualImageSourceRendererCallback()
{
	MFCancelWorkItem(m_timerKey);
}

HRESULT VirtualImageSourceRendererCallback::StartTimer(LONGLONG delay)
{
	if (m_timerKey != NULL)
	{
		HRESULT result;
		ReturnIfFailed(result, MFCancelWorkItem(m_timerKey));
	}

	return MFScheduleWorkItem(this, nullptr, -(delay / 10000), &m_timerKey);
}

HRESULT VirtualImageSourceRendererCallback::StopTimer()
{
	if (m_timerKey == NULL)
	{
		return S_FALSE;
	}

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
	m_timerKey = NULL;
	m_renderer->ImageSource->Dispatcher->RunAsync(CoreDispatcherPriority::Normal, m_timerDispatchedHandler);
	return S_OK;
}

HRESULT VirtualImageSourceRendererCallback::GetParameters(DWORD* pdwFlags, DWORD* pdwQueue)
{
	*pdwFlags = MFASYNC_SIGNAL_CALLBACK;
	*pdwQueue = MFASYNC_CALLBACK_QUEUE_TIMER;
	return S_OK;
}