#pragma once

using namespace Platform;
using namespace Windows::Data::Json;
using namespace Windows::ApplicationModel::Background;
using namespace Windows::Networking::PushNotifications;


namespace Unigram
{
	namespace Native
	{
		namespace Tasks
		{
			[Windows::Foundation::Metadata::WebHostHidden]
			public ref class VoipCallRtcTask sealed : public IBackgroundTask
			{
			public:
				VoipCallRtcTask() {}
				virtual void Run(IBackgroundTaskInstance^ taskInstance);

			private:
				void OnCanceled(Windows::ApplicationModel::Background::IBackgroundTaskInstance^ taskInstance, Windows::ApplicationModel::Background::BackgroundTaskCancellationReason reason);

			private:
				Platform::Agile<Windows::ApplicationModel::Background::BackgroundTaskDeferral> m_deferral = nullptr;
			};
		}
	}
}
