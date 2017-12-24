#include "pch.h"
#include "NotificationTask.h"
#include "VoIPCallTask.h"

#include <ios>
#include <fstream>

#include <ppltasks.h>
#include <iostream>  
#include <iomanip>
#include <sstream>
#include <windows.h>
#include "Shlwapi.h"

using namespace concurrency;
using namespace Platform;
using namespace Unigram::Native::Tasks;
using namespace Windows::ApplicationModel::Calls;
using namespace Windows::ApplicationModel::Resources;
using namespace Windows::Data::Json;
using namespace Windows::Data::Xml::Dom;
using namespace Windows::Foundation;
using namespace Windows::Storage;
using namespace Windows::UI::Notifications;
using namespace Windows::UI::StartScreen;

void NotificationTask::Run(IBackgroundTaskInstance^ taskInstance)
{
	//auto temp = ApplicationData::Current->LocalFolder->Path;
	//std::wstringstream path;
	//path << temp->Data()
	//	<< L"\\background_log.txt";

	//std::wofstream log(path.str(), std::ios_base::app | std::ios_base::out);

	//time_t rawtime = time(NULL);
	//struct tm timeinfo;
	//wchar_t buffer[80];

	//time(&rawtime);
	//localtime_s(&timeinfo, &rawtime);

	//wcsftime(buffer, sizeof(buffer), L"%d-%m-%Y %I:%M:%S", &timeinfo);
	//std::wstring str(buffer);

	//log << L"[";
	//log << str;
	//log << L"] Starting background task\n";

	auto deferral = taskInstance->GetDeferral();
	auto details = safe_cast<RawNotification^>(taskInstance->TriggerDetails);

	if (details != nullptr && details->Content != nullptr)
	{
		try
		{
			UpdateToastAndTiles(details->Content /*, &log*/);
		}
		catch (Exception^ ex)
		{
			//time(&rawtime);
			//localtime_s(&timeinfo, &rawtime);

			//wcsftime(buffer, sizeof(buffer), L"%d-%m-%Y %I:%M:%S", &timeinfo);
			//std::wstring str3(buffer);

			//log << L"[";
			//log << str3;
			//log << "] Exception while processing notification";
		}
	}

	//time(&rawtime);
	//localtime_s(&timeinfo, &rawtime);

	//wcsftime(buffer, sizeof(buffer), L"%d-%m-%Y %I:%M:%S", &timeinfo);
	//std::wstring str2(buffer);

	//log << L"[";
	//log << str2;
	//log << L"] Quitting background task\n\n";

	deferral->Complete();
}

void NotificationTask::UpdateToastAndTiles(String^ content /*, std::wofstream* log*/)
{
	auto notification = JsonValue::Parse(content)->GetObject();
	auto data = notification->GetNamedObject("data");
	if (data == nullptr)
	{
		return;
	}

	if (data->HasKey("loc_key") == false)
	{
		//time_t rawtime = time(NULL);
		//struct tm timeinfo;
		//wchar_t buffer[80];

		//time(&rawtime);
		//localtime_s(&timeinfo, &rawtime);

		//wcsftime(buffer, sizeof(buffer), L"%d-%m-%Y %I:%M:%S", &timeinfo);
		//std::wstring str(buffer);

		//*log << L"[";
		//*log << str;
		//*log << L"] Removing a toast notification\n";

		auto custom = data->GetNamedObject("custom");
		auto group = GetGroup(custom);

		ToastNotificationManager::History->RemoveGroup(group, L"App");
		return;
	}

	auto muted = data->GetNamedString("mute", "0") == L"1";
	if (!muted)
	{
		auto loc_key = data->GetNamedString("loc_key");
		auto loc_args = data->GetNamedArray("loc_args");
		auto custom = data->GetNamedObject("custom", nullptr);

		//time_t rawtime = time(NULL);
		//struct tm timeinfo;
		//wchar_t buffer[80];

		//time(&rawtime);
		//localtime_s(&timeinfo, &rawtime);

		//wcsftime(buffer, sizeof(buffer), L"%d-%m-%Y %I:%M:%S", &timeinfo);
		//std::wstring str(buffer);

		//*log << L"[";
		//*log << str;
		//*log << L"] Received notification with loc_key ";
		//*log << loc_key->Data();
		//*log << L"\n";

		auto caption = GetCaption(loc_args, loc_key);
		auto message = GetMessage(loc_args, loc_key);
		auto sound = data->GetNamedString("sound", "Default");
		auto launch = GetLaunch(custom, loc_key);
		auto group = GetGroup(custom);
		auto picture = GetPicture(custom, group);
		auto date = GetDate(notification);

		if (message == nullptr)
		{
			message = data->GetNamedString("text", "New Notification");
		}

		//if (loc_key->Equals(L"PHONE_CALL_MISSED"))
		//{
		//	ToastNotificationManager::History->Remove(L"phoneCall");
		//}

		if (loc_key->Equals(L"PHONE_CALL_REQUEST"))
		{
			UpdateToast(caption, message, sound, launch, L"phoneCall", group, picture, date, loc_key);
			UpdatePhoneCall(caption, message, sound, launch, L"phoneCall", group, picture, date, loc_key);
		}
		else
		{
			auto tag = GetTag(custom);
			UpdateToast(caption, message, sound, launch, tag, group, picture, date, loc_key);
			UpdatePrimaryBadge(data->GetNamedNumber("badge"));

			if (loc_key != L"DC_UPDATE")
			{
				UpdatePrimaryTile(caption, message, picture);
			}
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
	else if (key.find(L"PHONE_CALL") == 0)
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
	if (text->Length())
	{
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

	return nullptr;
}

String^ NotificationTask::GetLaunch(JsonObject^ custom, String^ loc_key)
{
	std::wstring launch = L"";
	if (custom)
	{
		if (custom->HasKey("msg_id"))
		{
			launch += L"msg_id=";
			launch += custom->GetNamedString("msg_id")->Data();
			launch += L"&amp;";
		}
		if (custom->HasKey("chat_id"))
		{
			launch += L"chat_id=";
			launch += custom->GetNamedString("chat_id")->Data();
			launch += L"&amp;";
		}
		if (custom->HasKey("channel_id"))
		{
			launch += L"channel_id=";
			launch += custom->GetNamedString("channel_id")->Data();
			launch += L"&amp;";
		}
		if (custom->HasKey("from_id"))
		{
			launch += L"from_id=";
			launch += custom->GetNamedString("from_id")->Data();
			launch += L"&amp;";
		}
		if (custom->HasKey("mtpeer"))
		{
			auto mtpeer = custom->GetNamedObject("mtpeer");
			if (mtpeer->HasKey("ah"))
			{
				launch += L"access_hash=";
				launch += mtpeer->GetNamedString("ah")->Data();
				launch += L"&amp;";
			}
		}
	}

	launch += L"Action=";
	launch += loc_key->Data();

	return ref new String(launch.c_str());
}

String^ NotificationTask::GetTag(JsonObject^ custom)
{
	if (custom)
	{
		return custom->GetNamedString("msg_id");
	}

	return nullptr;
}

String^ NotificationTask::GetGroup(JsonObject^ custom)
{
	if (custom)
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
		else if (custom->HasKey("contact_id"))
		{
			return String::Concat("u", custom->GetNamedString("contact_id"));
		}
	}

	return nullptr;
}

String^ NotificationTask::GetPicture(JsonObject^ custom, String^ group)
{
	if (custom && custom->HasKey("mtpeer"))
	{
		auto mtpeer = custom->GetNamedObject("mtpeer");
		if (mtpeer->HasKey("ph"))
		{
			auto ph = mtpeer->GetNamedObject("ph");
			auto volume_id = ph->GetNamedString("volume_id");
			auto local_id = ph->GetNamedString("local_id");
			auto secret = ph->GetNamedString("secret");

			std::wstring volumeSTR = volume_id->Data();
			auto volumeULL = wcstoull(volumeSTR.c_str(), NULL, 0);
			auto volumeLL = static_cast<signed long long>(volumeULL);

			std::wstring secretSTR = secret->Data();
			auto secretULL = wcstoull(secretSTR.c_str(), NULL, 0);
			auto secretLL = static_cast<signed long long>(secretULL);

			auto temp = ApplicationData::Current->LocalFolder->Path;

			std::wstringstream path;
			path << temp->Data()
				<< L"\\temp\\"
				<< volumeLL
				<< L"_"
				<< local_id->Data()
				<< L"_"
				<< secretLL
				<< L".jpg";

			WIN32_FIND_DATA FindFileData;
			HANDLE handle = FindFirstFile(path.str().c_str(), &FindFileData);
			int found = handle != INVALID_HANDLE_VALUE;
			if (found)
			{
				FindClose(handle);

				std::wstringstream almost;
				almost << L"ms-appdata:///local/temp/"
					<< volumeLL
					<< L"_"
					<< local_id->Data()
					<< L"_"
					<< secretLL
					<< L".jpg";

				return ref new String(almost.str().c_str());
			}
		}
	}

	std::wstringstream almost;
	almost << L"ms-appdata:///local/temp/placeholders/"
		<< group->Data()
		<< L"_placeholder.png";

	return ref new String(almost.str().c_str());
}

String^ NotificationTask::GetDate(JsonObject^ notification)
{
	const time_t rawtime = notification->GetNamedNumber(L"date");
	struct tm dt;
	wchar_t buffer[30];
	localtime_s(&dt, &rawtime);
	wcsftime(buffer, sizeof(buffer), L"%FT%T%zZ", &dt);
	return ref new String(buffer);
}

void NotificationTask::UpdatePrimaryBadge(int badgeNumber)
{
	auto updater = BadgeUpdateManager::CreateBadgeUpdaterForApplication(L"App");

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

//void NotificationTask::UpdateSecondaryBadge(String^ group, bool resetBadge)
//{
//	auto updater = BadgeUpdateManager::CreateBadgeUpdaterForSecondaryTile(group);
//
//	if (resetBadge)
//	{
//		updater->Clear();
//		return;
//	}
//
//	auto document = BadgeUpdateManager::GetTemplateContent(BadgeTemplateType::BadgeNumber);
//	auto element = safe_cast<XmlElement^>(document->SelectSingleNode("/badge"));
//	auto badgeValue = element->GetAttribute("value");
//	auto badgeNumber = badgeValue == nullptr || badgeValue->IsEmpty() ? 0 : std::stoi(badgeValue->Data());
//	badgeNumber++;
//	element->SetAttribute("value", badgeNumber.ToString());
//
//	updater->Update(ref new BadgeNotification(document));
//}

std::wstring NotificationTask::Escape(std::wstring data)
{
	std::wstring buffer;
	buffer.reserve(data.size());
	for (size_t pos = 0; pos != data.size(); ++pos)
	{
		switch (data[pos])
		{
		case '&':  buffer.append(L"&amp;");       break;
		case '\"': buffer.append(L"&quot;");      break;
		case '\'': buffer.append(L"&apos;");      break;
		case '<':  buffer.append(L"&lt;");        break;
		case '>':  buffer.append(L"&gt;");        break;
		default:   buffer.append(&data[pos], 1); break;
		}
	}
	return buffer;
}

void NotificationTask::ResetSecondaryTile(String^ caption, String^ picture, String^ group)
{
	if (group == nullptr)
	{
		return;
	}

	auto existsSecondaryTile = SecondaryTile::Exists(group);
	if (!existsSecondaryTile)
	{
		return;
	}

	auto escapedCaption = NotificationTask::Escape(caption->Data());

	std::wstring xml = L"<tile><visual>";
	xml += L"<binding template='TileMedium' displayName='";
	xml += escapedCaption;
	xml += L"' branding='name'>";
	if (picture != nullptr)
	{
		xml += L"<image hint-crop='circle' src='";
		xml += picture->Data();
		xml += L"'/>";
	}
	xml += L"</binding>";
	xml += L"<binding template='TileWide' displayName='";
	xml += escapedCaption;
	xml += L"' branding='nameAndLogo'>";
	if (picture != nullptr)
	{
		xml += L"<image hint-crop='circle' src='";
		xml += picture->Data();
		xml += L"'/>";
	}
	xml += L"</binding>";
	xml += L"<binding template='TileLarge' displayName='";
	xml += escapedCaption;
	xml += L"' branding='nameAndLogo'>";
	if (picture != nullptr)
	{
		xml += L"<image hint-crop='circle' src='";
		xml += picture->Data();
		xml += L"'/>";
	}
	xml += L"</binding>";
	xml += L"</visual></tile>";

	auto updater = TileUpdateManager::CreateTileUpdaterForSecondaryTile(group);

	auto document = ref new XmlDocument();
	document->LoadXml(ref new String(xml.c_str()));

	auto notification = ref new TileNotification(document);

	updater->Update(notification);
}

String^ NotificationTask::CreateTileMessageBody(String^ message)
{
	std::wstring body = L"<text hint-style='captionSubtle' hint-wrap='true'><![CDATA[";
	body += message->Data();
	body += L"]]></text>";

	return ref new String(body.c_str());
}

String^ NotificationTask::CreateTileMessageBodyWithCaption(String^ caption, String^ message)
{
	std::wstring body = L"<text hint-style='body'><![CDATA[";
	body += caption->Data();
	body += L"]]></text>";
	body += L"<text hint-style='captionSubtle' hint-wrap='true'><![CDATA[";
	body += message->Data();
	body += L"]]></text>";

	return ref new String(body.c_str());
}

void NotificationTask::UpdatePrimaryTile(String^ caption, String^ message, String^ picture)
{
	auto body = NotificationTask::CreateTileMessageBodyWithCaption(caption, message);

	std::wstring xml = L"<tile><visual>";
	xml += L"<binding template='TileMedium' branding='name'>";
	if (picture != nullptr)
	{
		xml += L"<image placement='peek' hint-crop='circle' src='";
		xml += picture->Data();
		xml += L"'/>";
	}
	xml += body->Data();
	xml += L"</binding>";
	xml += L"<binding template='TileWide' branding='nameAndLogo'>";
	xml += L"<group>";
	xml += L"<subgroup hint-weight='18'>";
	if (picture != nullptr)
	{
		xml += L"<image hint-crop='circle' src='";
		xml += picture->Data();
		xml += L"'/>";
	}
	xml += L"</subgroup>";
	xml += L"<subgroup>";
	xml += body->Data();
	xml += L"</subgroup>";
	xml += L"</group>";
	xml += L"</binding>";
	xml += L"<binding template='TileLarge' branding='nameAndLogo'>";
	xml += L"<group>";
	xml += L"<subgroup hint-weight='18'>";
	if (picture != nullptr)
	{
		xml += L"<image hint-crop='circle' src='";
		xml += picture->Data();
		xml += L"'/>";
	}
	xml += L"</subgroup>";
	xml += L"<subgroup>";
	xml += body->Data();
	xml += L"</subgroup>";
	xml += L"</group>";
	xml += L"</binding>";
	xml += L"</visual></tile>";

	auto updater = TileUpdateManager::CreateTileUpdaterForApplication(L"App");

	auto document = ref new XmlDocument();
	document->LoadXml(ref new String(xml.c_str()));

	auto notification = ref new TileNotification(document);

	updater->Update(notification);
}

//void NotificationTask::UpdateSecondaryTile(String^ caption, String^ message, String^ picture, String^ group)
//{
//	auto body = NotificationTask::CreateTileMessageBody(message);
//
//	auto escapedCaption = NotificationTask::Escape(caption->Data());
//
//	std::wstring xml = L"<tile><visual>";
//	xml += L"<binding template='TileMedium' displayName='";
//	xml += escapedCaption;
//	xml += L"' branding='name'>";
//	if (picture != nullptr)
//	{
//		xml += L"<image placement='peek' hint-crop='circle' src='";
//		xml += picture->Data();
//		xml += L"'/>";
//	}
//	xml += body->Data();
//	xml += L"</binding>";
//	xml += L"<binding template='TileWide' displayName='";
//	xml += escapedCaption;
//	xml += L"' branding='nameAndLogo'>";
//	xml += L"<group>";
//	xml += L"<subgroup hint-weight='18'>";
//	if (picture != nullptr)
//	{
//		xml += L"<image hint-crop='circle' src='";
//		xml += picture->Data();
//		xml += L"'/>";
//	}
//	xml += L"</subgroup>";
//	xml += L"<subgroup>";
//	xml += body->Data();
//	xml += L"</subgroup>";
//	xml += L"</group>";
//	xml += L"</binding>";
//	xml += L"<binding template='TileLarge' displayName='";
//	xml += escapedCaption;
//	xml += L"' branding='nameAndLogo'>";
//	xml += L"<group>";
//	xml += L"<subgroup hint-weight='18'>";
//	if (picture != nullptr)
//	{
//		xml += L"<image hint-crop='circle' src='";
//		xml += picture->Data();
//		xml += L"'/>";
//	}
//	xml += L"</subgroup>";
//	xml += L"<subgroup>";
//	xml += body->Data();
//	xml += L"</subgroup>";
//	xml += L"</group>";
//	xml += L"</binding>";
//	xml += L"</visual></tile>";
//
//	auto updater = TileUpdateManager::CreateTileUpdaterForSecondaryTile(group);
//
//	auto document = ref new XmlDocument();
//	document->LoadXml(ref new String(xml.c_str()));
//
//	auto notification = ref new TileNotification(document);
//
//	updater->Update(notification);
//}

void NotificationTask::UpdateToast(String^ caption, String^ message, String^ sound, String^ launch, String^ tag, String^ group, String^ picture, String^ date, String^ loc_key)
{
	bool allow = true;
	//auto settings = ApplicationData::Current->LocalSettings;
	//if (settings->Values->HasKey("SessionGuid"))
	//{
	//	auto guid = safe_cast<String^>(settings->Values->Lookup("SessionGuid"));

	//	std::wstringstream path;
	//	path << temp->Data()
	//		<< L"\\"
	//		<< guid->Data()
	//		<< L"\\passcode_params.dat";

	//	WIN32_FIND_DATA FindFileData;
	//	HANDLE handle = FindFirstFile(path.str().c_str(), &FindFileData);
	//	int found = handle != INVALID_HANDLE_VALUE;
	//	if (found)
	//	{
	//		FindClose(handle);

	//		allow = false;
	//	}
	//}

	std::wstring key = loc_key->Data();
	std::wstring actions = L"";
	if (group != nullptr && key.find(L"CHANNEL") && allow)
	{
		actions = L"<actions><input id='QuickMessage' type='text' placeHolderContent='Type a message...' /><action activationType='background' arguments='";
		actions += launch->Data();
		actions += L"' hint-inputId='QuickMessage' content='Send' imageUri='ms-appx:///Assets/Icons/Toast/Send.png'/></actions>";
	}

	std::wstring audio = L"";
	if (sound->Equals("silent"))
	{
		audio = L"<audio silent='true'/>";
	}

	std::wstring xml = L"<toast launch='";
	xml += launch->Data();
	xml += L"' displaytimestamp='";
	xml += date->Data();
	//xml += L"' hint-people='remoteid:";
	//xml += group->Data();
	xml += L"'><visual><binding template='ToastGeneric'>";

	if (picture != nullptr)
	{
		xml += L"<image placement='appLogoOverride' hint-crop='circle' src='";
		xml += picture->Data();
		xml += L"'/>";
	}

	xml += L"<text><![CDATA[";
	xml += caption->Data();
	xml += L"]]></text><text><![CDATA[";
	xml += message->Data();
	//xml += L"]]></text><text placement='attribution'>Unigram</text></binding></visual>";
	xml += L"]]></text></binding></visual>";
	xml += actions;
	xml += audio;
	xml += L"</toast>";

	auto notifier = ToastNotificationManager::CreateToastNotifier(L"App");

	auto document = ref new XmlDocument();
	document->LoadXml(ref new String(xml.c_str()));

	auto notification = ref new ToastNotification(document);

	if (tag != nullptr) notification->Tag = tag;
	if (group != nullptr) notification->Group = group;

	notifier->Show(notification);
}

void NotificationTask::UpdatePhoneCall(String^ caption, String^ message, String^ sound, String^ launch, String^ tag, String^ group, String^ picture, String^ date, String^ loc_key)
{
	auto coordinator = VoipCallCoordinator::GetDefault();
	create_task(coordinator->ReserveCallResourcesAsync("Unigram.Tasks.VoIPCallTask")).then([this, coordinator, caption, message, sound, launch, tag, group, picture, date, loc_key](VoipPhoneCallResourceReservationStatus status)
	{
		Sleep(1000000);
		//VoIPCallTask::Current->UpdatePhoneCall(caption, message, sound, launch, tag, group, picture, date, loc_key);
	});
}
