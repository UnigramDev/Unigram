#pragma once

#include <agile.h>

using namespace Platform;
using namespace Windows::Data::Json;
using namespace Windows::ApplicationModel::Background;
using namespace Windows::ApplicationModel::Calls;
using namespace Windows::Networking::PushNotifications;


namespace Unigram
{
	namespace Native
	{
		namespace Tasks
		{
			[Windows::Foundation::Metadata::WebHostHidden]
			public ref class VoIPCallTask sealed : public IBackgroundTask
			{
			public:
				VoipCallRtcTask() {}
				virtual void Run(IBackgroundTaskInstance^ taskInstance);

				void UpdatePhoneCall(String^ caption, String^ message, String^ sound, String^ launch, String^ tag, String^ group, String^ picture, String^ date, String^ loc_key);


				static property VoipCallRtcTask^ Current
				{
					VoipCallRtcTask^ get() 
					{
						return s_current;
					}
				}

			private:
				void OnCanceled(IBackgroundTaskInstance^ taskInstance, BackgroundTaskCancellationReason reason);

				void OnAnswerRequested(VoipPhoneCall^ phoneCall, CallAnswerEventArgs^ args);

			private:
				Agile<BackgroundTaskDeferral> m_deferral = nullptr;
				VoipPhoneCall^ m_systemCall;

				static VoipCallRtcTask^ s_current;
			};
		}
	}
}
