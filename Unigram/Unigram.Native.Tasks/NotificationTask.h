#pragma once

#include <ios>
#include <fstream>
#include <ppltasks.h>

using namespace Platform;
using namespace Windows::Data::Json;
using namespace Windows::ApplicationModel::Background;
using namespace Windows::ApplicationModel::Calls;
using namespace Windows::Networking::PushNotifications;
using namespace Windows::Foundation;

namespace Unigram
{
	namespace Native 
	{
		namespace Tasks
		{
			[Windows::Foundation::Metadata::WebHostHidden]
			public ref class NotificationTask sealed : public IBackgroundTask
			{
			public:
				NotificationTask() {}
				virtual void Run(IBackgroundTaskInstance^ taskInstance);

				static void UpdatePrimaryBadge(int badgeNumber);
				static IAsyncAction^ UpdateToast(String^ caption, String^ message, String^ attribution, String^ session, String^ sound, String^ launch, String^ tag, String^ group, String^ picture, String^ hero, String^ date, String^ loc_key);

			private:
				concurrency::task<void> UpdateToastAndTiles(String^ content /*, std::wofstream* log*/);
				String^ GetCaption(JsonArray^ loc_args, String^ loc_key);
				String^ GetMessage(JsonArray^ loc_args, String^ loc_key);
				String^ GetLaunch(JsonObject^ custom, String^ loc_key);
				String^ GetTag(JsonObject^ custom);
				String^ GetGroup(JsonObject^ custom);
				String^ GetPicture(JsonObject^ custom, String^ group, String^ session);
				String^ GetDate(JsonObject^ notification);
				String^ GetSession(JsonObject^ data);

				static std::wstring Escape(std::wstring data);
			};
		}
	}
}
