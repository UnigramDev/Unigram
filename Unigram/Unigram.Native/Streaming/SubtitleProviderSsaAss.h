#pragma once

#include "SubtitleProvider.h"

#include <string>
#include <codecvt>

namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			ref class SubtitleProviderSsaAss : SubtitleProvider
			{
			internal:
				SubtitleProviderSsaAss(FFmpegReader^ reader,
					AVFormatContext* avFormatCtx,
					AVCodecContext* avCodecCtx,
					FFmpegInteropConfig^ config,
					int index)
					: SubtitleProvider(reader, avFormatCtx, avCodecCtx, config, index, TimedMetadataKind::Subtitle)
				{
				}

				virtual HRESULT Initialize() override
				{
					auto hr = SubtitleProvider::Initialize();
					if (SUCCEEDED(hr))
					{
						ssaVersion = 4;
						if (m_pAvCodecCtx->subtitle_header && m_pAvCodecCtx->subtitle_header_size > 0)
						{
							auto str = std::string((char*)m_pAvCodecCtx->subtitle_header, m_pAvCodecCtx->subtitle_header_size);
							auto versionIndex = str.find("ScriptType: v4.0.0+");
							if (versionIndex != str.npos)
							{
								isAss = true;
							}
							else
							{
								versionIndex = str.find("ScriptType: v");
								if (versionIndex != str.npos && versionIndex + 13 < str.length())
								{
									auto version = str.at(versionIndex + 13) - '0';
									if (version > 0 && version < 9)
									{
										ssaVersion = version;
									}
								}
							}

							auto resx = str.find("\nPlayResX: ");
							auto resy = str.find("\nPlayResY: ");
							if (resx != str.npos && resy != str.npos)
							{
								int w, h;
								if (sscanf_s((char*)m_pAvCodecCtx->subtitle_header + resx, "\nPlayResX: %i\n", &w) == 1 &&
									sscanf_s((char*)m_pAvCodecCtx->subtitle_header + resy, "\nPlayResY: %i\n", &h) == 1)
								{
									width = w;
									height = h;
								}
							}

							if (isAss)
							{
								ReadStylesV4Plus(str);
							}
							else if (ssaVersion == 4)
							{
								ReadStylesV4(str);
							}
						}

						if (ssaVersion >= 3)
						{
							textIndex = 9;
						}
						else
						{
							textIndex = 8;
						}
					}

					return hr;
				}

				virtual IMediaCue^ CreateCue(AVPacket* packet, TimeSpan* position, TimeSpan *duration) override
				{
					AVSubtitle subtitle;
					int gotSubtitle = 0;
					auto result = avcodec_decode_subtitle2(m_pAvCodecCtx, &subtitle, &gotSubtitle, packet);
					if (result > 0 && gotSubtitle && subtitle.num_rects > 0)
					{
						auto str = utf8_to_wstring(std::string(subtitle.rects[0]->ass));

						int startStyle = -1;
						int endStyle = -1;
						int lastComma = -1;
						bool hasError = false;
						for (int i = 0; i < textIndex; i++)
						{
							auto nextComma = str.find(',', lastComma + 1);
							if (nextComma != str.npos)
							{
								if (i == styleIndex)
								{
									startStyle = (int)nextComma + 1;
								}
								else if (i == styleIndex + 1)
								{
									endStyle = (int)nextComma;
								}
								lastComma = (int)nextComma;
							}
							else
							{
								// this should not happen. still we try to be graceful. let's use what we found.
								hasError = true;
								break;
							}
						}

						SsaStyleDefinition^ style = nullptr;
						if (!hasError && startStyle > 0 && endStyle > 0)
						{
							auto styleName = convertFromString(str.substr(startStyle, endStyle - startStyle));
							if (styles.find(styleName) != styles.end())
							{
								style = styles[styleName];
							}
						}

						if (lastComma > 0 && lastComma < (int)str.length() - 1)
						{
							// get actual text
							str = str.substr(lastComma + 1);

							find_and_replace(str, L"\\N", L"\n");
							str.erase(str.find_last_not_of(L" \n\r") + 1);

							// strip effects from string
							// TODO we could parse effect and use at least bold and italic
							while (true)
							{
								auto nextEffect = str.find('{');
								if (nextEffect != str.npos)
								{
									auto endEffect = str.find('}', nextEffect);
									if (endEffect != str.npos)
									{
										if (endEffect < str.length() - 1)
										{
											str = str.substr(0, nextEffect).append(str.substr(endEffect + 1));
										}
										else
										{
											str = str.substr(0, nextEffect);
										}
									}
									else
									{
										break;
									}
								}
								else
								{
									break;
								}
							}


							auto timedText = convertFromString(str);

							TimedTextCue^ cue = ref new TimedTextCue();
							if (!m_config->OverrideSubtitleStyles && style)
							{
								cue->CueRegion = style->Region;
								cue->CueStyle = style->Style;
							}
							else
							{
								cue->CueRegion = m_config->SubtitleRegion;
								cue->CueStyle = m_config->SubtitleStyle;
							}

							TimedTextLine^ textLine = ref new TimedTextLine();
							textLine->Text = timedText;
							cue->Lines->Append(textLine);

							return cue;
						}
					}

					return nullptr;
				}

				void ReadStylesV4Plus(std::string str)
				{
					auto stylesV4plus = str.find("[V4+ Styles]");
					while (stylesV4plus != str.npos)
					{
						stylesV4plus = str.find("\nStyle: ", stylesV4plus);
						if (stylesV4plus != str.npos)
						{
							stylesV4plus += 8;
							/*
							[V4+ Styles]
							Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
							*/
							const unsigned int MAX_STYLE_NAME_CHARS = 256;
							char name[MAX_STYLE_NAME_CHARS];
							char font[MAX_STYLE_NAME_CHARS];
							int size, color, secondaryColor, outlineColor, backColor;
							int bold, italic, underline, strikeout;
							int scaleX, scaleY, spacing, angle, borderStyle;
							int outline, shadow, alignment;
							int marginL, marginR, marginV, encoding;

							auto count = sscanf_s((char*)m_pAvCodecCtx->subtitle_header + stylesV4plus,
								"%[^,],%[^,],%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i",
								name, MAX_STYLE_NAME_CHARS, font, MAX_STYLE_NAME_CHARS,
								&size, &color, &secondaryColor, &outlineColor, &backColor,
								&bold, &italic, &underline, &strikeout,
								&scaleX, &scaleY, &spacing, &angle, &borderStyle,
								&outline, &shadow, &alignment,
								&marginL, &marginR, &marginV, &encoding);

							if (count == 3)
							{
								// try with hex colors
								count = sscanf_s((char*)m_pAvCodecCtx->subtitle_header + stylesV4plus,
									"%[^,],%[^,],%i,&H%x,&H%x,&H%x,&H%x,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i",
									name, MAX_STYLE_NAME_CHARS, font, MAX_STYLE_NAME_CHARS,
									&size, &color, &secondaryColor, &outlineColor, &backColor,
									&bold, &italic, &underline, &strikeout,
									&scaleX, &scaleY, &spacing, &angle, &borderStyle,
									&outline, &shadow, &alignment,
									&marginL, &marginR, &marginV, &encoding);
							}

							if (count == 23)
							{
								auto verticalAlignment =
									alignment <= 3 ? TimedTextDisplayAlignment::After :
									alignment <= 6 ? TimedTextDisplayAlignment::Center :
									TimedTextDisplayAlignment::Before;

								auto horizontalAlignment =
									alignment == 2 || alignment == 5 || alignment == 8 ? TimedTextLineAlignment::Center :
									alignment == 1 || alignment == 4 || alignment == 7 ? TimedTextLineAlignment::Start :
									TimedTextLineAlignment::End;

								auto SubtitleRegion = ref new TimedTextRegion();

								TimedTextSize extent;
								extent.Unit = TimedTextUnit::Percentage;
								extent.Width = 100;
								extent.Height = 100;
								SubtitleRegion->Extent = extent;
								TimedTextPoint position;
								position.Unit = TimedTextUnit::Pixels;
								position.X = 0;
								position.Y = 0;
								SubtitleRegion->Position = position;
								SubtitleRegion->DisplayAlignment = verticalAlignment;
								SubtitleRegion->Background = Windows::UI::Colors::Transparent;
								SubtitleRegion->ScrollMode = TimedTextScrollMode::Rollup;
								SubtitleRegion->TextWrapping = TimedTextWrapping::Wrap;
								SubtitleRegion->WritingMode = TimedTextWritingMode::LeftRightTopBottom;
								SubtitleRegion->IsOverflowClipped = false;
								SubtitleRegion->ZIndex = 0;
								TimedTextDouble LineHeight;
								LineHeight.Unit = TimedTextUnit::Percentage;
								LineHeight.Value = 100;
								SubtitleRegion->LineHeight = LineHeight;
								TimedTextPadding padding;
								padding.Unit = TimedTextUnit::Percentage;
								padding.Start = 0;
								if (width > 0 && height > 0)
								{
									padding.Start = (double)marginL * 100 / width;
									padding.End = (double)marginR * 100 / width;
									padding.After = (double)marginV * 100 / height;
								}
								else
								{
									padding.After = 12;
								}
								SubtitleRegion->Padding = padding;
								SubtitleRegion->Name = "";

								auto SubtitleStyle = ref new TimedTextStyle();

								SubtitleStyle->FontFamily = ConvertString(font);
								TimedTextDouble fontSize;
								fontSize.Unit = TimedTextUnit::Pixels;
								fontSize.Value = size;
								SubtitleStyle->FontSize = fontSize;
								SubtitleStyle->LineAlignment = horizontalAlignment;
								if (Windows::Foundation::Metadata::ApiInformation::IsPropertyPresent("Windows.Media.Core.TimedTextStyle", "FontStyle"))
								{
									SubtitleStyle->FontStyle = italic ? TimedTextFontStyle::Italic : TimedTextFontStyle::Normal;
								}
								SubtitleStyle->FontWeight = bold ? TimedTextWeight::Bold : TimedTextWeight::Normal;
								SubtitleStyle->Foreground = ColorFromArgb(color << 8 | 0x000000FF);
								SubtitleStyle->Background = Windows::UI::Colors::Transparent; //ColorFromArgb(backColor);
								TimedTextDouble outlineRadius;
								outlineRadius.Unit = TimedTextUnit::Percentage;
								outlineRadius.Value = outline;
								SubtitleStyle->OutlineRadius = outlineRadius;
								TimedTextDouble outlineThickness;
								outlineThickness.Unit = TimedTextUnit::Percentage;
								outlineThickness.Value = outline;
								SubtitleStyle->OutlineThickness = outlineThickness;
								SubtitleStyle->FlowDirection = TimedTextFlowDirection::LeftToRight;
								SubtitleStyle->OutlineColor = ColorFromArgb(outlineColor << 8 | 0x000000FF);

								SubtitleStyle->IsUnderlineEnabled = underline;
								SubtitleStyle->IsLineThroughEnabled = strikeout;

								auto style = ref new SsaStyleDefinition();
								style->Name = ConvertString(name);
								style->Region = SubtitleRegion;
								style->Style = SubtitleStyle;

								styles[style->Name] = style;
							}
						}
						else
						{
							break;
						}
					}
				}

				void ReadStylesV4(std::string str)
				{
					auto stylesV4plus = str.find("[V4 Styles]");
					while (stylesV4plus != str.npos)
					{
						stylesV4plus = str.find("\nStyle: ", stylesV4plus);
						if (stylesV4plus != str.npos)
						{
							stylesV4plus += 8;
							/*
							[V4+ Styles]
							Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
							[V4 Styles]
							Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, TertiaryColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, AlphaLevel, Encoding
							*/
							const unsigned int MAX_STYLE_NAME_CHARS = 256;
							char name[MAX_STYLE_NAME_CHARS];
							char font[MAX_STYLE_NAME_CHARS];
							int size, color, secondaryColor, outlineColor, backColor;
							int bold, italic, borderstyle;
							int outline, shadow, alignment;
							int marginL, marginR, marginV, alpha, encoding;

							auto count = sscanf_s((char*)m_pAvCodecCtx->subtitle_header + stylesV4plus,
								"%[^,],%[^,],%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i",
								name, MAX_STYLE_NAME_CHARS, font, MAX_STYLE_NAME_CHARS,
								&size, &color, &secondaryColor, &outlineColor, &backColor,
								&bold, &italic, &borderstyle,
								&outline, &shadow, &alignment,
								&marginL, &marginR, &marginV, &alpha, &encoding);

							if (count == 3)
							{
								// try with hex colors
								count = sscanf_s((char*)m_pAvCodecCtx->subtitle_header + stylesV4plus,
									"%[^,],%[^,],%i,&H%x,&H%x,&H%x,&H%x,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i,%i",
									name, MAX_STYLE_NAME_CHARS, font, MAX_STYLE_NAME_CHARS,
									&size, &color, &secondaryColor, &outlineColor, &backColor,
									&bold, &italic, &borderstyle,
									&outline, &shadow, &alignment,
									&marginL, &marginR, &marginV, &alpha, &encoding);
							}

							if (count == 18)
							{
								auto verticalAlignment =
									alignment <= 3 ? TimedTextDisplayAlignment::After :
									alignment <= 7 ? TimedTextDisplayAlignment::Center :
									TimedTextDisplayAlignment::Before;

								auto horizontalAlignment =
									alignment == 2 || alignment == 6 || alignment == 10 ? TimedTextLineAlignment::Center :
									alignment == 1 || alignment == 5 || alignment == 9 ? TimedTextLineAlignment::Start :
									TimedTextLineAlignment::End;

								auto SubtitleRegion = ref new TimedTextRegion();

								TimedTextSize extent;
								extent.Unit = TimedTextUnit::Percentage;
								extent.Width = 100;
								extent.Height = 100;
								SubtitleRegion->Extent = extent;
								TimedTextPoint position;
								position.Unit = TimedTextUnit::Pixels;
								position.X = 0;
								position.Y = 0;
								SubtitleRegion->Position = position;
								SubtitleRegion->DisplayAlignment = verticalAlignment;
								SubtitleRegion->Background = Windows::UI::Colors::Transparent;
								SubtitleRegion->ScrollMode = TimedTextScrollMode::Rollup;
								SubtitleRegion->TextWrapping = TimedTextWrapping::Wrap;
								SubtitleRegion->WritingMode = TimedTextWritingMode::LeftRightTopBottom;
								SubtitleRegion->IsOverflowClipped = false;
								SubtitleRegion->ZIndex = 0;
								TimedTextDouble LineHeight;
								LineHeight.Unit = TimedTextUnit::Percentage;
								LineHeight.Value = 100;
								SubtitleRegion->LineHeight = LineHeight;
								TimedTextPadding padding;
								padding.Unit = TimedTextUnit::Percentage;
								padding.Start = 0;
								if (width > 0 && height > 0)
								{
									padding.Start = (double)marginL * 100 / width;
									padding.End = (double)marginR * 100 / width;
									padding.After = (double)marginV * 100 / height;
								}
								else
								{
									padding.After = 12;
								}
								SubtitleRegion->Padding = padding;
								SubtitleRegion->Name = "";

								auto SubtitleStyle = ref new TimedTextStyle();

								SubtitleStyle->FontFamily = ConvertString(font);
								TimedTextDouble fontSize;
								fontSize.Unit = TimedTextUnit::Pixels;
								fontSize.Value = size;
								SubtitleStyle->FontSize = fontSize;
								SubtitleStyle->LineAlignment = horizontalAlignment;
								if (Windows::Foundation::Metadata::ApiInformation::IsPropertyPresent("Windows.Media.Core.TimedTextStyle", "FontStyle"))
								{
									SubtitleStyle->FontStyle = italic ? TimedTextFontStyle::Italic : TimedTextFontStyle::Normal;
								}
								SubtitleStyle->FontWeight = bold ? TimedTextWeight::Bold : TimedTextWeight::Normal;
								SubtitleStyle->Foreground = ColorFromArgb(color << 8 | 0x000000FF);
								SubtitleStyle->Background = Windows::UI::Colors::Transparent; //ColorFromArgb(backColor);
								TimedTextDouble outlineRadius;
								outlineRadius.Unit = TimedTextUnit::Percentage;
								outlineRadius.Value = outline;
								SubtitleStyle->OutlineRadius = outlineRadius;
								TimedTextDouble outlineThickness;
								outlineThickness.Unit = TimedTextUnit::Percentage;
								outlineThickness.Value = outline;
								SubtitleStyle->OutlineThickness = outlineThickness;
								SubtitleStyle->FlowDirection = TimedTextFlowDirection::LeftToRight;
								SubtitleStyle->OutlineColor = ColorFromArgb(outlineColor << 8 | 0x000000FF);

								auto style = ref new SsaStyleDefinition();
								style->Name = ConvertString(name);
								style->Region = SubtitleRegion;
								style->Style = SubtitleStyle;

								styles[style->Name] = style;
							}
						}
						else
						{
							break;
						}
					}
				}

				Windows::UI::Color ColorFromArgb(int argb)
				{
					auto result = *reinterpret_cast<Windows::UI::Color*>(&argb);
					return result;
				}

				void find_and_replace(std::wstring& source, std::wstring const& find, std::wstring const& replace)
				{
					for (std::wstring::size_type i = 0; (i = source.find(find, i)) != std::wstring::npos;)
					{
						source.replace(i, find.length(), replace);
						i += replace.length();
					}
				}

				ref class SsaStyleDefinition
				{
				public:
					property String^ Name;
					property TimedTextRegion^ Region;
					property TimedTextStyle^ Style;
				};

			private:
				bool isAss;
				int ssaVersion;
				int textIndex;
				int width;
				int height;
				const int styleIndex = 2;
				std::map<String^, SsaStyleDefinition^> styles;
			};
		}
	}
}