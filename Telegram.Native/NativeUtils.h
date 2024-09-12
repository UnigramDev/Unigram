#pragma once

#include "NativeUtils.g.h"

#include <iostream>  
#include <iomanip>
#include <sstream>
#include <vector>

namespace winrt::Telegram::Native::implementation
{
    struct NativeUtils : NativeUtilsT<NativeUtils>
    {
    public:
        static bool FileExists(hstring path);

        static int64_t GetDirectorySize(hstring path);
        static int64_t GetDirectorySize(hstring path, hstring filter);
        static void CleanDirectory(hstring path, int days);
        static void Delete(hstring path);

        static int32_t GetLastInputTime();

        //[DefaultOverload]
        static winrt::Telegram::Native::TextDirectionality GetDirectionality(hstring value);
        static winrt::Telegram::Native::TextDirectionality GetDirectionality(hstring value, int32_t offset);
        static winrt::Telegram::Native::TextDirectionality GetDirectionality(hstring value, int32_t offset, int32_t length);
        //static int32_t GetDirectionality(char16 value);

        static hstring GetCurrentCulture();
        static hstring GetKeyboardCulture();

        static hstring FormatTime(winrt::Windows::Foundation::DateTime value);
        static hstring FormatDate(winrt::Windows::Foundation::DateTime value, hstring format);

        static hstring FormatTime(int value);
        static hstring FormatDate(int value, hstring format);

        static bool IsFileReadable(hstring path);
        static bool IsFileReadable(hstring path, int64_t& fileSize, int64_t& fileTime);

        static bool IsMediaSupported();

        static void OverrideScaleForCurrentView(int32_t value);
        static int32_t GetScaleForCurrentView();

        static void SetFatalErrorCallback(FatalErrorCallback action);
        static winrt::Telegram::Native::FatalError GetFatalError(bool onlyNative);
        static winrt::Telegram::Native::FatalError GetBackTrace(DWORD code);

        static void Crash();

        static FatalErrorCallback Callback;



        static void GenerateGradient(winrt::Microsoft::UI::Xaml::Media::Imaging::WriteableBitmap context, winrt::array_view<winrt::Windows::UI::Color const> colors, winrt::array_view<winrt::Windows::Foundation::Numerics::float2 const> positions)
        {
            auto width = context.PixelWidth();
            auto height = context.PixelHeight();
            auto imageBytes = context.PixelBuffer().data();

            for (int y = 0; y < height; y++)
            {
                auto directPixelY = y / (float)height;
                auto centerDistanceY = directPixelY - 0.5f;
                auto centerDistanceY2 = centerDistanceY * centerDistanceY;

                auto lineBytes = imageBytes + width * 4 * y;
                for (int x = 0; x < width; x++)
                {
                    auto directPixelX = x / (float)width;

                    auto centerDistanceX = directPixelX - 0.5f;
                    auto centerDistance = sqrtf(centerDistanceX * centerDistanceX + centerDistanceY2);

                    auto swirlFactor = 0.35f * centerDistance;
                    auto theta = swirlFactor * swirlFactor * 0.8f * 8.0f;
                    auto sinTheta = sinf(theta);
                    auto cosTheta = cosf(theta);

                    auto pixelX = std::max(0.0f, std::min(1.0f, 0.5f + centerDistanceX * cosTheta - centerDistanceY * sinTheta));
                    auto pixelY = std::max(0.0f, std::min(1.0f, 0.5f + centerDistanceX * sinTheta + centerDistanceY * cosTheta));

                    auto distanceSum = 0.0f;

                    auto r = 0.0f;
                    auto g = 0.0f;
                    auto b = 0.0f;

                    for (int i = 0; i < colors.size(); i++)
                    {
                        auto colorX = positions[i].x;
                        auto colorY = positions[i].y;

                        auto distanceX = pixelX - colorX;
                        auto distanceY = pixelY - colorY;

                        auto distance = std::max(0.0f, 0.9f - sqrtf(distanceX * distanceX + distanceY * distanceY));
                        distance = distance * distance * distance * distance;
                        distanceSum += distance;

                        r += distance * colors[i].R / 255.f;
                        g += distance * colors[i].G / 255.f;
                        b += distance * colors[i].B / 255.f;
                    }

                    auto pixelBytes = lineBytes + x * 4;
                    pixelBytes[0] = (byte)(b / distanceSum * 255.0f);
                    pixelBytes[1] = (byte)(g / distanceSum * 255.0f);
                    pixelBytes[2] = (byte)(r / distanceSum * 255.0f);
                    pixelBytes[3] = 0xff;
                }
            }
        }


    private:
        static uint64_t GetDirectorySizeInternal(const std::wstring& path, const std::wstring& filter, uint64_t size);
        static void CleanDirectoryInternal(const std::wstring& path, int days);
        static bool IsBrowsePath(const std::wstring& path);
        static ULONGLONG FileTimeToSeconds(FILETIME& ft);
        static bool IsFileReadableInternal(hstring path, int64_t* fileSize, int64_t* fileTime);
    };
} // namespace winrt::Telegram::Native::implementation

namespace winrt::Telegram::Native::factory_implementation
{
    struct NativeUtils : NativeUtilsT<NativeUtils, implementation::NativeUtils>
    {
    };
} // namespace winrt::Telegram::Native::factory_implementation
