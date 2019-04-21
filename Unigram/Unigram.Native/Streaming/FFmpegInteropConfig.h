#pragma once

using namespace Windows::Foundation::Collections;
using namespace Windows::Media::Core;

namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			public ref class FFmpegInteropConfig sealed
			{
			public:
				FFmpegInteropConfig()
				{
					PassthroughAudioMP3 = false;
					PassthroughAudioAAC = true;

					PassthroughVideoH264 = true;
					PassthroughVideoH264Hi10P = false; // neither Windows codecs nor known HW decoders support Hi10P
					PassthroughVideoHEVC = true;

					VideoOutputAllowIyuv = false;
					VideoOutputAllow10bit = false;
					VideoOutputAllowBgra8 = false;
					VideoOutputAllowNv12 = true;

					SkipErrors = 5;
					MaxAudioThreads = 2;

					MaxSupportedPlaybackRate = 1.0;
					StreamBufferSize = 4096;

					FFmpegOptions = ref new PropertySet();

					AutoSelectForcedSubtitles = true;
					UseAntiFlickerForSubtitles = true;
					OverrideSubtitleStyles = false;

					TimedTextSize extent;
					extent.Unit = TimedTextUnit::Percentage;
					extent.Width = 100;
					extent.Height = 88;

					TimedTextPoint position;
					position.Unit = TimedTextUnit::Pixels;
					position.X = 0;
					position.Y = 0;

					TimedTextDouble LineHeight;
					LineHeight.Unit = TimedTextUnit::Percentage;
					LineHeight.Value = 100;

					TimedTextPadding padding;
					padding.Unit = TimedTextUnit::Percentage;
					padding.Start = 0;

					SubtitleRegion = ref new TimedTextRegion();
					SubtitleRegion->Extent = extent;
					SubtitleRegion->Position = position;
					SubtitleRegion->DisplayAlignment = TimedTextDisplayAlignment::After;
					SubtitleRegion->Background = Windows::UI::Colors::Transparent;
					SubtitleRegion->ScrollMode = TimedTextScrollMode::Rollup;
					SubtitleRegion->TextWrapping = TimedTextWrapping::Wrap;
					SubtitleRegion->WritingMode = TimedTextWritingMode::LeftRightTopBottom;
					SubtitleRegion->IsOverflowClipped = false;
					SubtitleRegion->ZIndex = 0;
					SubtitleRegion->LineHeight = LineHeight;
					SubtitleRegion->Padding = padding;
					SubtitleRegion->Name = "";

					TimedTextDouble fontSize;
					fontSize.Unit = TimedTextUnit::Pixels;
					fontSize.Value = 44;

					TimedTextDouble outlineThickness;
					outlineThickness.Unit = TimedTextUnit::Percentage;
					outlineThickness.Value = 3.5;

					SubtitleStyle = ref new TimedTextStyle();
					SubtitleStyle->FontFamily = "default";
					SubtitleStyle->FontSize = fontSize;
					SubtitleStyle->LineAlignment = TimedTextLineAlignment::Center;

					if (Windows::Foundation::Metadata::ApiInformation::IsPropertyPresent("Windows.Media.Core.TimedTextStyle", "FontStyle"))
					{
						SubtitleStyle->FontStyle = TimedTextFontStyle::Normal;
					}

					SubtitleStyle->FontWeight = TimedTextWeight::Normal;
					SubtitleStyle->Foreground = Windows::UI::Colors::White;
					SubtitleStyle->Background = Windows::UI::Colors::Transparent;
					SubtitleStyle->OutlineThickness = outlineThickness;
					SubtitleStyle->FlowDirection = TimedTextFlowDirection::LeftToRight;
					SubtitleStyle->OutlineColor = { 0x80, 0, 0, 0 };
				};

				property bool PassthroughAudioMP3;
				property bool PassthroughAudioAAC;

				property bool PassthroughVideoH264;
				property bool PassthroughVideoH264Hi10P;
				property bool PassthroughVideoHEVC;

				property bool VideoOutputAllowIyuv;
				property bool VideoOutputAllow10bit;
				property bool VideoOutputAllowBgra8;
				property bool VideoOutputAllowNv12;

				property unsigned int SkipErrors;

				property unsigned int MaxVideoThreads;
				property unsigned int MaxAudioThreads;

				property double MaxSupportedPlaybackRate;
				property unsigned int StreamBufferSize;

				property PropertySet^ FFmpegOptions;

				property TimedTextRegion^ SubtitleRegion;
				property TimedTextStyle^ SubtitleStyle;
				property bool AutoSelectForcedSubtitles;
				property bool UseAntiFlickerForSubtitles;
				property bool OverrideSubtitleStyles;

			internal:
				property bool IsFrameGrabber;
			};
		}
	}
}