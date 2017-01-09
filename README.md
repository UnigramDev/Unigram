# Unigram
Unigram – An Universal take on Telegram. Made by the community, for the community

Windows 10 (Mobile) introduced with the Universal Windows Platform a whole list of features to make your (chatting) life easier. However, the current official Telegram-app has no signs of releasing an UWP-version with these capabilities soon. That’s why we decided to build our own.

Build instructions:
-
* Install [SQLite] Visual Studio extension.
* Go to https://my.telegram.org/apps and register a new app.
* Create a new file inside `Unigram/Unigram/Unigram.Api` called `Constants.Secret.cs`:
```csharp
namespace Telegram.Api
{
    public static partial class Constants
    {
        static Constants()
        {
            FirstServerIpAddress = "your_server_ip";

            ApiId = your_api_id;
            ApiHash = "your_api_hash";
        }
    }
}
```

Our main goal:
-
Make chatting via Telegram fun and engaging. How? By building an app that’s fast, functional and easy to use.


At first we’ll aim to build an experience that’s feature wise on par with the current WP-app. After that we’ll have more time to implement extra functions to make the app even better to use!

You can check out our current designs of Unigram here:
https://www.behance.net/gallery/37507573/Unigram-for-Windows-10-Mobile

Want to contribute? Join the Unigram Insiders-chat on Telegram or join the discussion on Reddit: 

https://telegram [dot] me/joinchat/ARAF8z851oqv-9A1XXlHJw

https://www.reddit.com/r/windowsphone/comments/4kkcjc/no_uwp_version_of_telegram_lets_make_one/

We look forward to your ideas and or contributions! Together we can make this a great Telegram client for all of us!

=================================================================

Milestones:
-
(These are subject to change!)

Completed:
-	Design UI and build XAML-mockups
-	Build code to communicate with Telegram-servers
-	Implement Quick Notifications
-	Make the mockups functional
-	Build Live-Tiles
-	Allow for muting chats

Currently:
-   Send medias
-   Stickers panel

Future:
-	Build background handlers
-	Set user settings
-	Allow for sharing pictures
-	Future updates…

=================================================================

Features:
-	Basic chat functionality
-	Use of emoticons
-	Stickers
-	Group chats
-	Show profile pictures
-	Share pictures
-	Search through notes
-	Mute chats
-	See if the message has been sent
-	See if the message has been checked
-	Start new chats
-	See if an user has been online
-	Set a profile picture
-	Use your phone number
-	Show notifications
-	And more…

UWP-specific:
-	Quick reply from notifications
-	Support for Continuum
-	Adaptive design
-	Share contracts
-	Integrate with the People Hub
-	And more…

[SQLite]:https://marketplace.visualstudio.com/items?itemName=SQLiteDevelopmentTeam.SQLiteforUniversalWindowsPlatform
