﻿#pragma once

#include <ios>
#include <fstream>

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
			public ref class NotificationTask sealed : public IBackgroundTask
			{
			public:
				NotificationTask() {}
				virtual void Run(IBackgroundTaskInstance^ taskInstance);

			private:
				void UpdateToastAndTiles(String^ content, std::wofstream* log);
				String^ GetCaption(JsonArray^ loc_args, String^ loc_key);
				String^ GetMessage(JsonArray^ loc_args, String^ loc_key);
				String^ GetLaunch(JsonObject^ custom, String^ loc_key);
				String^ GetTag(JsonObject^ custom);
				String^ GetGroup(JsonObject^ custom);
				String^ GetPicture(JsonObject^ custom, String^ group);
				String^ GetDate(JsonObject^ notification);

				void UpdateBadge(int badgeNumber);
				void UpdateTile(String^ caption, String^ message);
				void UpdateToast(String^ caption, String^ message, String^ sound, String^ launch, String^ tag, String^ group, String^ picture, String^ date, String^ loc_key);
				void UpdatePhoneCall(String^ caption, String^ message, String^ sound, String^ launch, String^ tag, String^ group, String^ picture, String^ date, String^ loc_key);
			};
		}
	}
}
