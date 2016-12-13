#include "pch.h"
#include "NotificationTask.h"

#include <iostream>  
#include <iomanip>

using namespace Windows::UI::Notifications;
using namespace Windows::ApplicationModel::Resources;
using namespace Windows::Data::Json;
using namespace Windows::Data::Xml::Dom;
using namespace Unigram::Native::Tasks;
using namespace Platform;

void NotificationTask::Run(IBackgroundTaskInstance^ taskInstance)
{
	auto deferral = taskInstance->GetDeferral();
	auto details = safe_cast<RawNotification^>(taskInstance->TriggerDetails);

	if (details != nullptr && details->Content != nullptr)
	{
		UpdateToastAndTiles(details->Content);
	}

	deferral->Complete();
}

void NotificationTask::UpdateToastAndTiles(String^ content)
{
	auto notification = JsonValue::Parse(content)->GetObject();
	auto data = notification->GetNamedObject("data");
	if (data == nullptr)
	{
		return;
	}

	auto loc_key = data->GetNamedString("loc_key");
	if (loc_key == nullptr)
	{
		auto custom = data->GetNamedObject("custom");
		auto group = GetGroup(custom);

		ToastNotificationManager::History->RemoveGroup(group);
		return;
	}


	bool muted = false;
	if (data->HasKey("mute"))
	{
		muted = data->GetNamedString("mute") == L"1";
	}

	if (!muted)
	{
		auto custom = data->GetNamedObject("custom");
		auto loc_args = data->GetNamedArray("loc_args");

		auto caption = GetCaption(loc_args, loc_key);
		auto message = GetMessage(loc_args, loc_key);
		auto sound = data->GetNamedString("sound");
		auto launch = GetLaunch(custom);
		auto tag = GetTag(custom);
		auto group = GetGroup(custom);

		UpdateToast(caption, message, sound, launch, tag, group);
		UpdateBadge(data->GetNamedNumber("badge"));

		if (loc_key != L"DC_UPDATE") 
		{
			UpdateTile(caption, message);
		}
	}
}

String^ NotificationTask::GetCaption(JsonArray^ loc_args, String^ loc_key)
{
	std::wstring key = loc_key->Data();

	if (key.find(L"CHAT") == 0 || key.find(L"GEOCHAT") == 0)
	{
		return loc_args->GetStringAt(1);
	}
	else if (key.find(L"MESSAGE") == 0)
	{
		return loc_args->GetStringAt(0);
	}
	else if (key.find(L"CHANNEL") == 0)
	{
		return loc_args->GetStringAt(0);
	}
	else if (key.find(L"PINNED") == 0)
	{
		return loc_args->GetStringAt(0);
	}
	else if (key.find(L"AUTH") == 0 || key.find(L"CONTACT") == 0 || key.find(L"ENCRYPTED") == 0 || key.find(L"ENCRYPTION") == 0)
	{
		return "Telegram";
	}

	return "Telegram";
}

String^ NotificationTask::GetMessage(JsonArray^ loc_args, String^ loc_key)
{
	auto resourceLoader = ResourceLoader::GetForViewIndependentUse("Unigram.Tasks/Resources");
	auto text = resourceLoader->GetString(loc_key);
	std::wstring wtext = text->Data();

	for (int i = 0; i < loc_args->Size; i++)
	{
		wchar_t* code = new wchar_t[4];
		swprintf_s(code, 4, L"{%d}", i);
		std::string::size_type index = wtext.find(code);
		if (index != std::string::npos)
		{
			wtext = wtext.replace(wtext.find(code), 3, loc_args->GetStringAt(i)->Data());
		}
	}

	return ref new String(wtext.c_str());
}

String^ NotificationTask::GetLaunch(JsonObject^ custom)
{
	std::wstring launch = L"";
	if (custom->HasKey("msg_id"))
	{
		launch += custom->GetNamedString("msg_id")->Data();
		launch += L"&amp;";
	}
	if (custom->HasKey("chat_id"))
	{
		launch += custom->GetNamedString("chat_id")->Data();
		launch += L"&amp;";
	}
	if (custom->HasKey("channel_id"))
	{
		launch += custom->GetNamedString("channel_id")->Data();
		launch += L"&amp;";
	}
	if (custom->HasKey("from_id"))
	{
		launch += custom->GetNamedString("from_id")->Data();
		launch += L"&amp;";
	}

	return ref new String(launch.substr(0, launch.length() - 5).c_str());
}

String^ NotificationTask::GetTag(JsonObject^ custom)
{
	return custom->GetNamedString("msg_id");
}

String^ NotificationTask::GetGroup(JsonObject^ custom)
{
	if (custom->HasKey("chat_id"))
	{
		return String::Concat("c", custom->GetNamedString("chat_id"));
	}
	else if (custom->HasKey("channel_id"))
	{
		return String::Concat("c", custom->GetNamedString("channel_id"));
	}
	else if (custom->HasKey("from_id"))
	{
		return String::Concat("u", custom->GetNamedString("from_id"));
	}

	return nullptr;
}

void NotificationTask::UpdateBadge(int badgeNumber)
{
	auto updater = BadgeUpdateManager::CreateBadgeUpdaterForApplication();
	if (badgeNumber == 0)
	{
		updater->Clear();
		return;
	}

	auto document = BadgeUpdateManager::GetTemplateContent(BadgeTemplateType::BadgeNumber);
	auto element = safe_cast<XmlElement^>(document->SelectSingleNode("/badge"));
	element->SetAttribute("value", badgeNumber.ToString());

	updater->Update(ref new BadgeNotification(document));
}

void NotificationTask::UpdateTile(String^ caption, String^ message)
{
	std::wstring body =  L"<text hint-style='body'><![CDATA[";
	body += caption->Data();
	body += L"]]></text>";
	body += L"<text hint-style='captionSubtle' hint-wrap='true'><![CDATA[";
	body += message->Data();
	body += L"]]></text>";

	std::wstring xml = L"<tile><visual><binding template='TileMedium' branding='nameAndLogo'>";
	xml += body;
	xml += L"</binding><binding template='TileWide' branding='nameAndLogo'>";
	xml += body;
	xml += L"</binding><binding template='TileLarge' branding='nameAndLogo'>";
	xml += body;
	xml += L"</binding></visual></tile>";

	auto updater = TileUpdateManager::CreateTileUpdaterForApplication();
	updater->EnableNotificationQueue(false);
	updater->EnableNotificationQueueForSquare150x150(false);

	auto document = ref new XmlDocument();
	document->LoadXml(ref new String(xml.c_str()));

	auto notification = ref new TileNotification(document);

	updater->Update(notification);
}

void NotificationTask::UpdateToast(String^ caption, String^ message, String^ sound, String^ launch, String^ tag, String^ group)
{
	std::wstring actions = L"";
	if (group != nullptr)
	{
		actions = L"<actions><input id='QuickMessage' type='text' placeHolderContent='Type a message...' /><action activationType='background' arguments='";
		actions += launch->Data();
		actions += L"' hint-inputId='QuickMessage' content='Send' imageUri='ms-appx:///Assets/Icons/Toast/Send.png'/></actions>";
	}

	std::wstring xml = L"<toast launch='";
	xml += launch->Data();
	xml += L"'><visual><binding template='ToastGeneric'><text><![CDATA[";
	xml += caption->Data();
	xml += L"]]></text><text><![CDATA[";
	xml += message->Data();
	xml += L"</text><text placement='attribution'>Unigram</text></binding></visual>";
	xml += actions;
	xml += L"</toast>";

	auto notifier = ToastNotificationManager::CreateToastNotifier();

	auto document = ref new XmlDocument();
	document->LoadXml(ref new String(xml.c_str()));

	auto notification = ref new ToastNotification(document);

	if (tag != nullptr) notification->Tag = tag;
	if (group != nullptr) notification->Group = group;

	notifier->Show(notification);
}