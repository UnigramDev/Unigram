#include "pch.h"
#include "VoIPCallTask.h"

#include <ios>
#include <fstream>

#include <ppltasks.h>
#include <iostream>  
#include <iomanip>
#include <sstream>
#include <windows.h>
#include "Shlwapi.h"

using namespace Platform;
using namespace concurrency;
using namespace Windows::UI::Notifications;
using namespace Windows::ApplicationModel::Resources;
using namespace Windows::Data::Json;
using namespace Windows::Data::Xml::Dom;
using namespace Windows::Storage;
using namespace Windows::ApplicationModel::Calls;
using namespace Windows::Foundation;
using namespace Unigram::Native::Tasks;

VoIPCallTask^ VoIPCallTask::s_current = nullptr;

void VoIPCallTask::Run(IBackgroundTaskInstance^ taskInstance)
{
	m_deferral = taskInstance->GetDeferral();
	
	// TODO
	s_current = this;

	taskInstance->Canceled += ref new BackgroundTaskCanceledEventHandler(this, &VoIPCallTask::OnCanceled);
}

void VoIPCallTask::OnCanceled(IBackgroundTaskInstance^ taskInstance, BackgroundTaskCancellationReason reason)
{
	if (m_deferral != nullptr) 
	{
		m_deferral->Complete();
	}
	
	// TODO
	s_current = nullptr;
}

void VoIPCallTask::UpdatePhoneCall(String^ caption, String^ message, String^ sound, String^ launch, String^ tag, String^ group, String^ picture, String^ date, String^ loc_key)
{
	auto coordinator = VoipCallCoordinator::GetDefault();
	TimeSpan timeout = { 128000000 };

	m_systemCall = coordinator->RequestNewIncomingCall("Unigram", caption, message, ref new Windows::Foundation::Uri(picture), "Unigram", nullptr, nullptr, nullptr, VoipPhoneCallMedia::Audio, timeout);
	m_systemCall->AnswerRequested += ref new TypedEventHandler<VoipPhoneCall^, CallAnswerEventArgs^>(this, &VoIPCallTask::OnAnswerRequested);
}

void VoIPCallTask::OnAnswerRequested(VoipPhoneCall^ phoneCall, CallAnswerEventArgs^ args)
{
	m_systemCall->NotifyCallActive();
}