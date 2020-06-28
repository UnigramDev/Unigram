1. First, check that you have the [necessary tools](#requirements) installed.
2. Go to <https://my.telegram.org/apps> and register a new app.
3. Clone the repository __*recursively*__ `git clone --recursive https://github.com/UnigramDev/Unigram.git`.
4. Create a new file inside `Unigram/Unigram/Unigram` and name it `Constants.Secret.cs`:
```csharp
namespace Unigram
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
* Visual Studio 2017/2019, with
    * .NET Native
    * .NET Framework 4.7 SDK
    * NuGet package manager
    * Universal Windows Platform tools
    * Windows 10 SDK 17134
    * [TDLib for Universal Windows Platform](https://tdlib.github.io/td/build.html?language=C%23)

## Dependencies
The app uses the following NuGet packages to work:
* [Autofac](https://www.nuget.org/packages/Autofac/)
* [HockeySDK.UWP](https://www.nuget.org/packages/HockeySDK.UWP/)
* [Microsoft.NETCore.UniversalWindowsPlatform](https://www.nuget.org/packages/Microsoft.NETCore.UniversalWindowsPlatform/)
* [Microsoft.Xaml.Behaviors.Uwp.Managed](https://www.nuget.org/packages/Microsoft.Xaml.Behaviors.Uwp.Managed/)
* [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/)
* [System.Reactive](https://www.nuget.org/packages/System.Reactive/)
* [Win2D.uwp](https://www.nuget.org/packages/Win2D.uwp/)

The project also relies on `libogg`, `libopus`, `libopusfile`, `libwebp` and `libtgvoip` that are included in the repository.
