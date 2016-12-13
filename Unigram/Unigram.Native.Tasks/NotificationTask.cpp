#include "pch.h"
#include "NotificationTask.h"

using namespace Windows::Data::Json;
using namespace Unigram::Native::Tasks;
using namespace Platform;

void NotificationTask::Run(IBackgroundTaskInstance^ taskInstance)
{
	auto deferral = taskInstance->GetDeferral();
}