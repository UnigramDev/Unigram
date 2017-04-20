#include "pch.h"
#include "VoipCallRtcTask.h"

using namespace Unigram::Native::Tasks;

void VoipCallRtcTask::Run(IBackgroundTaskInstance^ taskInstance)
{
	m_deferral = taskInstance->GetDeferral();
	
	// TODO

	taskInstance->Canceled += ref new BackgroundTaskCanceledEventHandler(this, &VoipCallRtcTask::OnCanceled);
}

void VoipCallRtcTask::OnCanceled(IBackgroundTaskInstance^ taskInstance, BackgroundTaskCancellationReason reason)
{
	if (m_deferral != nullptr) 
	{
		m_deferral->Complete();
	}
	
	// TODO
}