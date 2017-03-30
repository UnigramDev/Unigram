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
			public ref class NotificationTask sealed : public IBackgroundTask
			{
			public:
				NotificationTask() {}
				virtual void Run(IBackgroundTaskInstance^ taskInstance);

			private:
				static void UpdateToastAndTiles(String^ content);
				static String^ GetCaption(JsonArray^ loc_args, String^ loc_key);
				static String^ GetMessage(JsonArray^ loc_args, String^ loc_key);
				static String^ GetLaunch(JsonObject^ custom, String^ loc_key);
				static String^ GetTag(JsonObject^ custom);
				static String^ GetGroup(JsonObject^ custom);
				static String^ GetPicture(JsonObject^ custom, String^ group);
				static String^ GetDate(JsonObject^ notification);

				static void UpdateBadge(int badgeNumber);
				static void UpdateTile(String^ caption, String^ message);
				static void UpdateToast(String^ caption, String^ message, String^ sound, String^ launch, String^ tag, String^ group, String^ picture, String^ date, String^ loc_key);
				static void UpdatePhoneCall(String^ caption, String^ message, String^ sound, String^ launch, String^ tag, String^ group, String^ picture, String^ date, String^ loc_key);
			};
		}
	}
}
