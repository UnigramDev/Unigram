<p align="center">
  <a href="http://unigram.me">
    <img src="https://raw.githubusercontent.com/UnigramDev/Unigram/develop/Unigram/Assets/StoreLogo/Release/StoreLogo.scale-200.png" width=80 height=80>
  </a>

  <h3 align="center">Unigram</h3>

  <p align="center">
    The Telegram client for the Windows 10 platform, built by the community for the community.
    <br>
    <br>
    <a href="https://www.microsoft.com/store/apps/9n97zckpd60q">Download</a>
    &middot;
    <a href="http://unigram.me">Website</a>
    &middot;
    <a href="https://t.me/joinchat/AAAAAD851oqVwhp9oy9WbQ">Insiders Group</a>
  </p>
</p>

<br>

## Build instructions
1. First, check that you have the [necessary tools](#requirements) installed.
2. Go to <https://my.telegram.org/apps> and register a new app.
3. Clone the repository __*recursively*__ `git clone --recursive https://github.com/UnigramDev/Unigram.git`.
4. Create a new file inside `Unigram/Unigram/Unigram.Api` and name it `Constants.Secret.cs`: 
```csharp
namespace Telegram.Api
{
    public static partial class Constants
    {
        static Constants()
        {
            ApiId = your_api_id;
            ApiHash = "your_api_hash";
        }
    }
}
```
5. Replace `your_api_id` and `your_api_hash` with the data obtained from step 2.

## Requirements
The following tools and SDKs are mandatory for the project development:
* Visual Studio 2017, with
    * .NET Native
    * .NET Framework 4.7 SDK
    * NuGet package manager
    * Universal Windows Platform tools
    * Windows 10 SDK 16299

[Can I use Visual Studio 2015?](https://github.com/UnigramDev/Unigram/wiki/FAQ:-Development#why-do-i-have-to-use-visual-studio-2017-cant-i-use-vs2015)

## Dependencies
The app uses the following NuGet packages to work:
* [Autofac](https://www.nuget.org/packages/Autofac/)
* [HockeySDK.UWP](https://www.nuget.org/packages/HockeySDK.UWP/)
* [Microsoft.NETCore.UniversalWindowsPlatform](https://www.nuget.org/packages/Microsoft.NETCore.UniversalWindowsPlatform/)
* [Microsoft.Xaml.Behaviors.Uwp.Managed](https://www.nuget.org/packages/Microsoft.Xaml.Behaviors.Uwp.Managed/)
* [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/)
* [Portable.BouncyCastle](https://www.nuget.org/packages/Portable.BouncyCastle/)
* [System.Reactive](https://www.nuget.org/packages/System.Reactive/)
* [Template10](https://www.nuget.org/packages/Template10/)
* [Universal.WinSQLite](https://www.nuget.org/packages/Universal.WinSQLite/)
* [Win2D.uwp](https://www.nuget.org/packages/Win2D.uwp/)

The project also relies on `libogg`, `libopus`, `libopusfile` and `libwebp` that are included in the repository.

## Current and planned features
Check out the [Features list](https://github.com/UnigramDev/Unigram/wiki/Features) and see what Unigram has to offer and what is yet to come.

[Is there a release schedule?](https://github.com/UnigramDev/Unigram/wiki/FAQ:-General#when-will-i-have-a-new-build-release)

## Bugs and feature requests
Have a bug or a feature request? Please first read the [issue guidelines](https://github.com/UnigramDev/Unigram/blob/develop/CONTRIBUTING.md#using-the-issue-tracker) and search for existing and closed issues. If your problem or idea is not addressed yet, please [open a new issue](https://github.com/UnigramDev/Unigram/issues/new).

## Contributing
Please read through our [contributing guidelines](https://github.com/UnigramDev/Unigram/blob/develop/CONTRIBUTING.md). Included are directions for opening issues, bug and feature requests, and notes on pull requests.

## Changelog
See the [Releases section of our GitHub project](https://github.com/UnigramDev/Unigram/releases) for changelogs for each release version of Unigram. Release announcement posts on the official [Unigram News channel](https://t.me/unigram) contain summaries of the most noteworthy changes made in each release.

## Community
Get updates on Unigram's development and chat with the project maintainers and community members.

* Follow [@UnigramApp on Twitter](https://twitter.com/UnigramApp).
* Like and follow [Unigram on Facebook](https://www.facebook.com/UnigramApp/).
* Join the official group [Unigram Insiders](https://t.me/joinchat/AAAAAD851oqVwhp9oy9WbQ).
* Join the official channel [Unigram News](https://t.me/unigram).

## Special thanks
We want to especially thank these people for their help and knowledge while building Unigram

**Telegram Desktop**

- <https://twitter.com/telegramdesktop>

**Gregory Klyushnikov**

- <https://github.com/grishka>

## License
Copyright © 2016-2017 [Unigram Authors](https://github.com/UnigramDev/Unigram/graphs/contributors).

Unigram is free software: you can redistribute it and / or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

Unigram is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with Unigram. If not, see http://www.gnu.org/licenses/.
