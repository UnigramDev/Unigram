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
	SetThreadpoolCallbackCleanupGroup(&m_threadpoolEnvironment, m_threadpoolCleanupGroup, ThreadpoolManager::GroupCleanupCallback);
	return S_OK;
}

void ThreadpoolManager::CloseAllObjects(bool cancelPendingCallbacks)
{
	CloseThreadpoolCleanupGroupMembers(m_threadpoolCleanupGroup, cancelPendingCallbacks, nullptr);
}

HRESULT ThreadpoolManager::SubmitWork(ThreadpoolWorkCallback const& workHandler)
{
	std::unique_ptr<ThreadpoolWork> workContext(new ThreadpoolWork(workHandler));
	if (!TrySubmitThreadpoolCallback(ThreadpoolManager::WorkCallback, workContext.get(), &m_threadpoolEnvironment))
	{
		return GetLastHRESULT();
	}

	workContext.release();
	return S_OK;
}

void ThreadpoolManager::WorkCallback(PTP_CALLBACK_INSTANCE instance, PVOID context)
{
	std::unique_ptr<ThreadpoolWork> workHandler(reinterpret_cast<ThreadpoolWork*>(context));
	workHandler->Execute();
}

void ThreadpoolManager::GroupCleanupCallback(PVOID objectContext, PVOID cleanupContext)
{
	reinterpret_cast<ThreadpoolObject*>(objectContext)->OnGroupCleanupCallback();
}