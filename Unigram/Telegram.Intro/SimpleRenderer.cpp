//
// This file is used by the template to render a basic scene using GL.
//

#include "pch.h"
#include "SimpleRenderer.h"
#include "texture_helper.h"
#include "WICTextureLoader.h"
#include <core\animations.h>

// These are used by the shader compilation methods.
#include <vector>
#include <iostream>
#include <fstream>
#include <inttypes.h>
#include <time.h>
#include <stdio.h>

using namespace Platform;

using namespace Telegram::Intro;

SimpleRenderer::SimpleRenderer(float scale) :
	mWindowWidth(0),
	mWindowHeight(0),
	mCurrentPage(0)
{
	mWindowWidth = 200 * scale;
	mWindowHeight = 200 * scale;

	set_telegram_textures(setup_texture(L"telegram_sphere.png"), setup_texture(L"telegram_plane.png"));
	set_ic_textures(setup_texture(L"ic_bubble_dot.png"), setup_texture(L"ic_bubble.png"), setup_texture(L"ic_cam_lens.png"), setup_texture(L"ic_cam.png"), setup_texture(L"ic_pencil.png"), setup_texture(L"ic_pin.png"), setup_texture(L"ic_smile_eye.png"), setup_texture(L"ic_smile.png"), setup_texture(L"ic_videocam.png"));
	set_fast_textures(setup_texture(L"fast_body.png"), setup_texture(L"fast_spiral.png"), setup_texture(L"fast_arrow.png"), setup_texture(L"fast_arrow_shadow.png"));
	set_free_textures(setup_texture(L"knot_up.png"), setup_texture(L"knot_down.png"));
	set_powerful_textures(setup_texture(L"powerful_mask.png"), setup_texture(L"powerful_star.png"), setup_texture(L"powerful_infinity.png"), setup_texture(L"powerful_infinity_white.png"));
	set_private_textures(setup_texture(L"private_door.png"), setup_texture(L"private_screw.png"));

	set_need_pages(0);

	on_surface_created();
	on_surface_changed(mWindowWidth, mWindowHeight, scale, 0, 0, 0, 0, 0);
}

SimpleRenderer::~SimpleRenderer()
{
}

double SimpleRenderer::CFAbsoluteTimeGetCurrent()
{
	LONG time_ms = GetTickCount64();
	return (static_cast<double>(time_ms) / 1000);
}

void SimpleRenderer::Draw()
{
	glDisable(GL_MULTISAMPLE_EXT);
	glEnable(GL_SAMPLE_ALPHA_TO_ONE_EXT);

	glEnable(GL_BLEND);
	glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

	//glEnable(GL_DEPTH_TEST);
	//glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

	set_dark_theme(mDarkTheme);
	set_page(mCurrentPage);
	set_date(CFAbsoluteTimeGetCurrent());

	on_draw_frame();
}

void SimpleRenderer::SetCurrentPage(int page)
{
	mCurrentPage = page;
}

void SimpleRenderer::SetCurrentScroll(float scroll)
{
	mCurrentScroll = scroll;
	set_scroll_offset(scroll);
}

void SimpleRenderer::SetDarkTheme(int theme)
{
	mDarkTheme = theme;
}

void SimpleRenderer::UpdateWindowSize(GLsizei width, GLsizei height)
{
	glViewport(0, 0, mWindowWidth, mWindowHeight);
}
