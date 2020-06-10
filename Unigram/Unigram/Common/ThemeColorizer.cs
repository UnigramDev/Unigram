using System;
using System.Collections.Generic;
using Unigram.Services.Settings;
using Windows.UI;

namespace Unigram.Common
{
    public class ThemeColorizer
    {
        private double _hueThreshold = 0;
        private double _lightnessMin = 0;
        private double _lightnessMax = 1;

        private HSV _was;
        private HSV _now;

        private List<string> _ignoreKeys;
        private Dictionary<string, Tuple<HSV, HSV>> _keepContrast;

        public static explicit operator bool(ThemeColorizer colorizer)
        {
            return colorizer._hueThreshold > 0;
        }

        HSV? Colorize(HSV color)
        {
            var changeColor = Math.Abs(color.H - _was.H) < _hueThreshold;
            if (!changeColor)
            {
                return null;
            }

            var nowHue = color.H + (_now.H - _was.H);
            var nowSaturation = ((color.S > _was.S)
                && (_now.S > _was.S))
                ? (((_now.S * (1 - _was.S))
                    + ((color.S - _was.S)
                        * (1 - _now.S)))
                    / (1 - _was.S))
                : ((color.S != _was.S)
                    && (_was.S != 0))
                ? ((color.S * _now.S)
                    / _was.S)
                : _now.S;
            var nowValue = (color.V > _was.V)
                ? (((_now.V * (1 - _was.V))
                    + ((color.V - _was.V)
                        * (1 - _now.V)))
                    / (1 - _was.V))
                : (color.V < _was.V)
                ? ((color.V * _now.V)
                    / _was.V)
                : _now.V;

            return new HSV((nowHue + 360) % 360, nowSaturation, nowValue);
        }

        //[[nodiscard]] std::optional<QColor> Colorize(
        //		const QColor &color,
        //		const Colorizer &colorizer)
        //{
        //    auto hue = 0;
        //    auto saturation = 0;
        //    auto lightness = 0;
        //    color.getHsv(&hue, &saturation, &lightness);
        //    const auto result = Colorize(
        //        Colorizer::Color{ hue, saturation, lightness },
        //		colorizer);
        //    if (!result)
        //    {
        //        return std::nullopt;
        //    }
        //    const auto &fields = *result;
        //    return QColor::fromHsv(fields.hue, fields.saturation, fields.value);
        //}

        public Color Colorize(Color color)
        {
            var hsv = color.ToHSV();
            var result = Colorize(hsv);

            if (result == null)
            {
                return color;
            }

            return result.Value.ToRGB();
        }

        //void Colorize(string name, ref byte r, ref byte g, ref byte b)
        //{
        //    if (ignoreKeys.Contains(name))
        //    {
        //        return;
        //    }

        //    const auto i = keepContrast.find(name);
        //    if (i == end(colorizer.keepContrast))
        //    {
        //        Colorize(r, g, b);
        //        return;
        //    }
        //    var check = i->second.first;
        //    var rgb = QColor(int(r), int(g), int(b));
        //    var changed = Colorize(rgb, colorizer);
        //    var checkez = Colorize(check, colorizer).value_or(check);
        //    double lightness(HSV hsv)
        //    {
        //        return hsv.V - (hsv.V * hsv.S) / 511;
        //    };
        //    var changedLightness = lightness(changed.value_or(rgb).toHsv());
        //    var checkedLightness = lightness(
        //        new HSV(checkez.hue, checkez.saturation, checkez.value));
        //    var delta = Math.Abs(changedLightness - checkedLightness);
        //    if (delta >= kEnoughLightnessForContrast)
        //    {
        //        if (changed)
        //        {
        //            FillColorizeResult(r, g, b, *changed);
        //        }
        //        return;
        //    }
        //    const auto replace = i->second.second;
        //    const auto result = Colorize(replace, colorizer).value_or(replace);
        //    FillColorizeResult(
        //        r,
        //        g,
        //        b,
        //        QColor::fromHsv(result.hue, result.saturation, result.value));
        //}

        public static ThemeColorizer FromTheme(TelegramThemeType type, Color accent, Color color)
        {
            var temp = (RGB)color;

            var result = new ThemeColorizer();
            //result.ignoreKeys = kColorizeIgnoredKeys;
            result._hueThreshold = 15;
            result._was = accent.ToHSV();
            result._now = color.ToHSV();
            switch (type)
            {
                case TelegramThemeType.Day:
                    result._lightnessMax = 160d / 255d;
                    break;
                case TelegramThemeType.Night:
                    //                  result.keepContrast = base::flat_map<QLatin1String, Pair>{
                    //                      {
                    //                          //{ qstr("windowFgActive"), Pair{ cColor("5288c1"), cColor("17212b") } }, // windowBgActive
                    //                          {
                    //                              qstr("activeButtonFg"), Pair{ cColor("2f6ea5"), cColor("17212b") }
                    //                          }, // activeButtonBg
                    //	{ qstr("profileVerifiedCheckFg"), Pair{ cColor("5288c1"), cColor("17212b") } }, // profileVerifiedCheckBg
                    //	{ qstr("overviewCheckFgActive"), Pair{ cColor("5288c1"), cColor("17212b") } }, // overviewCheckBgActive
                    //	{ qstr("historyFileInIconFg"), Pair{ cColor("3f96d0"), cColor("182533") } }, // msgFileInBg, msgInBg
                    //	{ qstr("historyFileInIconFgSelected"), Pair{ cColor("6ab4f4"), cColor("2e70a5") } }, // msgFileInBgSelected, msgInBgSelected
                    //	{ qstr("historyFileInRadialFg"), Pair{ cColor("3f96d0"), cColor("182533") } }, // msgFileInBg, msgInBg
                    //	{ qstr("historyFileInRadialFgSelected"), Pair{ cColor("6ab4f4"), cColor("2e70a5") } }, // msgFileInBgSelected, msgInBgSelected
                    //	{ qstr("historyFileOutIconFg"), Pair{ cColor("4c9ce2"), cColor("2b5278") } }, // msgFileOutBg, msgOutBg
                    //	{ qstr("historyFileOutIconFgSelected"), Pair{ cColor("58abf3"), cColor("2e70a5") } }, // msgFileOutBgSelected, msgOutBgSelected
                    //	{ qstr("historyFileOutRadialFg"), Pair{ cColor("4c9ce2"), cColor("2b5278") } }, // msgFileOutBg, msgOutBg
                    //	{ qstr("historyFileOutRadialFgSelected"), Pair{ cColor("58abf3"), cColor("2e70a5") } }, // msgFileOutBgSelected, msgOutBgSelected
                    //}
                    //                  };
                    result._lightnessMin = 64d / 255d;
                    break;
                case TelegramThemeType.Tinted:
                    //                  result.keepContrast = base::flat_map<QLatin1String, Pair>{
                    //                      {
                    //                          //{ qstr("windowFgActive"), Pair{ cColor("3fc1b0"), cColor("282e33") } }, // windowBgActive, windowBg
                    //                          { qstr("activeButtonFg"), Pair{ cColor("2da192"), cColor("282e33") } }, // activeButtonBg, windowBg
                    //	{ qstr("profileVerifiedCheckFg"), Pair{ cColor("3fc1b0"), cColor("282e33") } }, // profileVerifiedCheckBg, windowBg
                    //	{ qstr("overviewCheckFgActive"), Pair{ cColor("3fc1b0"), cColor("282e33") } }, // overviewCheckBgActive
                    //	{ qstr("callIconFg"), Pair{ cColor("5ad1c1"), cColor("26282c") } }, // callAnswerBg, callBg
                    //}
                    //                  };
                    result._lightnessMin = 64d / 255d;
                    break;
            }
            var nowLightness = temp.ToHSL().L;
            var limitedLightness = Math.Clamp(
                nowLightness,
                result._lightnessMin,
                result._lightnessMax);
            if (limitedLightness != nowLightness)
            {
                var temp1 = temp.ToHSL();
                var temp2 = new HSL(temp1.H, temp1.S, limitedLightness);
                var temp3 = temp2.ToRGB();

                result._now = temp3.ToHSV();
            }

            return result;
        }
    }
}
