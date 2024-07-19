#include "pch.h"
#include "ParticlesAnimation.h"
#if __has_include("ParticlesAnimation.g.cpp")
#include "ParticlesAnimation.g.cpp"
#endif

#include <random>
#include <algorithm>

#define IS_MOBILE false

namespace winrt::Telegram::Native::implementation
{
    inline int AlphaBlendColors(int pixel, int sa, int sr, int sg, int sb)
    {
        // Alpha blend
        int destPixel = pixel;
        int da = ((destPixel >> 24) & 0xff);
        int dr = ((destPixel >> 16) & 0xff);
        int dg = ((destPixel >> 8) & 0xff);
        int db = ((destPixel) & 0xff);

        destPixel = ((sa + (((da * (255 - sa)) * 0x8081) >> 23)) << 24) |
            ((sr + (((dr * (255 - sa)) * 0x8081) >> 23)) << 16) |
            ((sg + (((dg * (255 - sa)) * 0x8081) >> 23)) << 8) |
            ((sb + (((db * (255 - sa)) * 0x8081) >> 23)));

        return destPixel;
    }

    inline int ConvertColor(byte opacity)
    {
        auto col = 0;

        if (opacity != 0)
        {
            auto a = opacity + 1;
            col = (opacity << 24)
                | ((byte)((255 * a) >> 8) << 16)
                | ((byte)((255 * a) >> 8) << 8)
                | ((byte)((255 * a) >> 8));
        }

        return col;
    }

    inline void FillEllipseCentered(int32_t* pixels, int32_t w, int32_t h, int xc, int yc, int xr, int yr, int color, bool doAlphaBlend = true)
    {
        if (xr == 0 || yr == 0)
        {
            pixels[yc * w + xc] = color;
        }

        // Avoid endless loop
        if (xr < 1 || yr < 1)
        {
            return;
        }

        // Skip completly outside objects
        if (xc - xr >= w || xc + xr < 0 || yc - yr >= h || yc + yr < 0)
        {
            return;
        }

        // Init vars
        int uh, lh, uy, ly, lx, rx;
        int x = xr;
        int y = 0;
        int xrSqTwo = (xr * xr) << 1;
        int yrSqTwo = (yr * yr) << 1;
        int xChg = yr * yr * (1 - (xr << 1));
        int yChg = xr * xr;
        int err = 0;
        int xStopping = yrSqTwo * xr;
        int yStopping = 0;

        int sa = ((color >> 24) & 0xff);
        int sr = ((color >> 16) & 0xff);
        int sg = ((color >> 8) & 0xff);
        int sb = ((color) & 0xff);

        bool noBlending = !doAlphaBlend || sa == 255;

        // Draw first set of points counter clockwise where tangent line slope > -1.
        while (xStopping >= yStopping)
        {
            // Draw 4 quadrant points at once
            // Upper half
            uy = yc + y;
            // Lower half
            ly = yc - y - 1;

            // Clip
            if (uy < 0) uy = 0;
            if (uy >= h) uy = h - 1;
            if (ly < 0) ly = 0;
            if (ly >= h) ly = h - 1;

            // Upper half
            uh = uy * w;
            // Lower half
            lh = ly * w;

            rx = xc + x;
            lx = xc - x;

            // Clip
            if (rx < 0) rx = 0;
            if (rx >= w) rx = w - 1;
            if (lx < 0) lx = 0;
            if (lx >= w) lx = w - 1;

            // Draw line
            if (noBlending)
            {
                for (int i = lx; i <= rx; i++)
                {
                    pixels[i + uh] = color; // Quadrant II to I (Actually two octants)
                    pixels[i + lh] = color; // Quadrant III to IV
                }
            }
            else
            {
                for (int i = lx; i <= rx; i++)
                {
                    // Quadrant II to I (Actually two octants)
                    pixels[i + uh] = AlphaBlendColors(pixels[i + uh], sa, sr, sg, sb);

                    // Quadrant III to IV
                    pixels[i + lh] = AlphaBlendColors(pixels[i + lh], sa, sr, sg, sb);
                }
            }


            y++;
            yStopping += xrSqTwo;
            err += yChg;
            yChg += xrSqTwo;
            if ((xChg + (err << 1)) > 0)
            {
                x--;
                xStopping -= yrSqTwo;
                err += xChg;
                xChg += yrSqTwo;
            }
        }

        // ReInit vars
        x = 0;
        y = yr;

        // Upper half
        uy = yc + y;
        // Lower half
        ly = yc - y;

        // Clip
        if (uy < 0) uy = 0;
        if (uy >= h) uy = h - 1;
        if (ly < 0) ly = 0;
        if (ly >= h) ly = h - 1;

        // Upper half
        uh = uy * w;
        // Lower half
        lh = ly * w;

        xChg = yr * yr;
        yChg = xr * xr * (1 - (yr << 1));
        err = 0;
        xStopping = 0;
        yStopping = xrSqTwo * yr;

        // Draw second set of points clockwise where tangent line slope < -1.
        while (xStopping <= yStopping)
        {
            // Draw 4 quadrant points at once
            rx = xc + x;
            lx = xc - x;

            // Clip
            if (rx < 0) rx = 0;
            if (rx >= w) rx = w - 1;
            if (lx < 0) lx = 0;
            if (lx >= w) lx = w - 1;

            // Draw line
            if (noBlending)
            {
                for (int i = lx; i <= rx; i++)
                {
                    pixels[i + uh] = color; // Quadrant II to I (Actually two octants)
                    pixels[i + lh] = color; // Quadrant III to IV
                }
            }
            else
            {
                for (int i = lx; i <= rx; i++)
                {
                    // Quadrant II to I (Actually two octants)
                    pixels[i + uh] = AlphaBlendColors(pixels[i + uh], sa, sr, sg, sb);

                    // Quadrant III to IV
                    pixels[i + lh] = AlphaBlendColors(pixels[i + lh], sa, sr, sg, sb);
                }
            }

            x++;
            xStopping += yrSqTwo;
            err += xChg;
            xChg += yrSqTwo;
            if ((yChg + (err << 1)) > 0)
            {
                y--;
                uy = yc + y; // Upper half
                ly = yc - y; // Lower half
                if (uy < 0) uy = 0; // Clip
                if (uy >= h) uy = h - 1; // ...
                if (ly < 0) ly = 0;
                if (ly >= h) ly = h - 1;
                uh = uy * w; // Upper half
                lh = ly * w; // Lower half
                yStopping -= xrSqTwo;
                err += yChg;
                yChg += xrSqTwo;
            }
        }
    }

    void ParticlesAnimation::RenderSync(IBuffer bitmap)
    {
        auto add = 0.04;
        auto pixels = (int32_t*)bitmap.data();

        //memset(pixels, 0, m_width * m_height * 4);

        for (int i = 0; i < m_width * m_height; i++)
        {
            pixels[i] = 0x54000000;
        }

        for (int i = 0, length = m_particles.size(); i < length; ++i)
        {
            auto dot = &m_particles[i];
            auto addOpacity = dot->Adding ? add : -add;

            dot->Opacity += addOpacity;
            // if(dot.mOpacity <= 0) dot.mOpacity = dot.opacity;

            // const easedOpacity = easing(dot.mOpacity);
            auto easedOpacity = (byte)(std::clamp(dot->Opacity, 0., 1.) * 255);
            //context.globalAlpha = easedOpacity;
            //context.fill(dot.path);

            FillEllipseCentered(pixels, m_width, m_height, dot->X, dot->Y, dot->Radius, dot->Radius, ConvertColor(easedOpacity));

            if (dot->Opacity <= 0)
            {
                dot->Adding = true;
                m_particles[i] = GenerateParticle(dot->Adding);
            }
            else if (dot->Opacity >= 1)
            {
                dot->Adding = false;
            }
        }
    }

    inline double min(double x, double y)
    {
        return x > y ? y : x;
    }

    void ParticlesAnimation::Prepare()
    {
        auto w = m_width * (1 / m_rasterizationScale);
        auto h = m_height * (1 / m_rasterizationScale);

        auto count = round(w * h / (35 * (IS_MOBILE ? 2 : 1)));
        count *= /*this.multiply ||*/ 1;
        count = min(/*!liteMode.isAvailable('chat_spoilers') ? 400 :*/ IS_MOBILE ? 1000 : 2200, count);
        //count = Math.Round(count);

        for (int i = 0; i < count; ++i)
        {
            m_particles.push_back(GenerateParticle(-1));
        }
    }

    inline double NextDouble()
    {
        //std::uniform_real_distribution<double> unif(0, 1);
        //std::default_random_engine re;
        //return unif(re);
        static std::random_device rd;  // Will be used to obtain a seed for the random number engine
        static std::mt19937 gen(rd()); // Standard mersenne_twister_engine seeded with rd()
        static std::uniform_real_distribution<> dis(0.0, 1.0);
        return dis(gen);
    }

    Particle ParticlesAnimation::GenerateParticle(int32_t type)
    {
        auto x = floor(NextDouble() * m_width);
        auto y = floor(NextDouble() * m_height);
        auto opacity = type == 1 ? 0 : NextDouble();
        auto radius = (NextDouble() >= .8 ? 1 : 0.5) * m_rasterizationScale;
        auto adding = type == -1
            ? NextDouble() >= .5
            : type;
        //var path = new Path2D();
        //path.arc(x, y, radius, 0, 2 * Math.PI, false);
        return Particle(
            (float)x,
            (float)y,
            (float)radius,
            opacity,
            adding);
    }
}
