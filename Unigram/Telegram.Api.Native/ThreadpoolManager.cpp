#include "pch.h"
#include <memory>
#include "ThreadpoolManager.h"
#include "ThreadpoolObject.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;


ThreadpoolManager::ThreadpoolManager() :
	m_threadpool(nullptr),
	m_threadpoolCleanupGroup(nullptr)
{
}

ThreadpoolManager::~ThreadpoolManager()
{
	if (m_threadpoolCleanupGroup != nullptr)
	{
		CloseThreadpoolCleanupGroupMembers(m_threadpoolCleanupGroup, TRUE, nullptr);
		CloseThreadpoolCleanupGroup(m_threadpoolCleanupGroup);
	}

	DestroyThreadpoolEnvironment(&m_threadpoolEnvironment);

	if (m_threadpool != nullptr)
	{
		CloseThreadpool(m_threadpool);
	}
}

HRESULT ThreadpoolManager::RuntimeClassInitialize(UINT32 minimumThreadCount, UINT32 maximumThreadCount)
{
	if (minimumThreadCount == 0 || minimumThreadCount > maximumThreadCount)
	{
		return E_INVALIDARG;
	}

	if (minimumThreadCount == UINT32_MAX && maximumThreadCount == UINT32_MAX)
	{
		SYSTEM_INFO systemInfo;
		GetNativeSystemInfo(&systemInfo);

		minimumThreadCount = 1;
		maximumThreadCount = systemInfo.dwNumberOfProcessors + 1;
	}

	InitializeThreadpoolEnvironment(&m_threadpoolEnvironment);

	if ((m_threadpool = CreateThreadpool(nullptr)) == nullptr)
	{
		return GetLastHRESULT();
	}

	SetThreadpoolThreadMaximum(m_threadpool, maximumThreadCount);
	if (!SetThreadpoolThreadMinimum(m_threadpool, minimumThreadCount))
	{
		return GetLastHRESULT();
	}

	if ((m_threadpoolCleanupGroup = CreateThreadpoolCleanupGroup()) == nullptr)
	{
		return GetLastHRESULT();
	}

	SetThreadpoolCallbackPool(&m_threadpoolEnvironment, m_threadpool);
	SetThreadpoolCallbackCleanupGroup(&m_threadpoolEnvironment, m_threadpoolCleanupGroup, nullptr);

	return S_OK;
}

HRESULT ThreadpoolManager::AttachEventObject(EventObject* object)
{
	if (object == nullptr)
	{
		return E_INVALIDARG;
	}

	return object->AttachToThreadpool(this);
}

HRESULT ThreadpoolManager::DetachEventObject(EventObject* object, boolean waitCallback)
{
	if (object == nullptr)
	{
		return E_INVALIDARG;
	}

	return object->DetachFromThreadpool(waitCallback);
}

HRESULT ThreadpoolManager::SubmitWork(std::function<void()> workHandler)
{
	auto workContext = std::make_unique<std::function<void()>>(workHandler);
	auto workHandle = CreateThreadpoolWork(ThreadpoolManager::WorkCallback, workContext.get(), &m_threadpoolEnvironment);
	if (workHandle == nullptr)
	{
		return GetLastHRESULT();
	}

	SubmitThreadpoolWork(workHandle);

	workContext.release();
	return S_OK;
}

void ThreadpoolManager::WorkCallback(PTP_CALLBACK_INSTANCE instance, PVOID context, PTP_WORK work)
{
	auto workHandler = std::unique_ptr<std::function<void()>>(reinterpret_cast<std::function<void()>*>(context));
	(*workHandler)();

	CloseThreadpoolWork(work);
}