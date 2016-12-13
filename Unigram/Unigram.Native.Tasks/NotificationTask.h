#pragma once

using namespace Windows::ApplicationModel::Background;
using namespace Windows::Networking::PushNotifications;


namespace Unigram
{
	namespace Native 
	{
		namespace Tasks
		{
			public ref class NotificationTask sealed : public IBackgroundTask
			{
			public:
				virtual void Run(IBackgroundTaskInstance^ taskInstance);
			};
		}
	}
}
