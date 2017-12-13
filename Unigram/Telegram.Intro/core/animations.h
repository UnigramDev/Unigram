//
//  animations.h
//  IntroOpenGL
//
//  Created by Ilya Rimchikov on 29/03/14.
//  Copyright (c) 2014 IntroOpenGL. All rights reserved.
//

// Enable function definitions in the GL headers below
#pragma once
#include "opengl.h"

#ifdef __cplusplus
extern "C" {
#endif
	extern void on_surface_created();

	extern void on_surface_changed(int a_width_px, int a_height_px, float a_scale_factor, int a1, int a2, int a3, int a4, int a5);
	extern void on_draw_frame();


	extern void set_touch_x(int a);

	extern void set_date(double a);
	extern void set_date0(double a);

	extern void set_page(int page);


	extern void set_dark_theme(int a);


	extern void set_pages_textures(int a1, int a2, int a3, int a4, int a5, int a6);

	extern void set_ic_textures(int a_ic_bubble_dot, int a_ic_bubble, int a_ic_cam_lens, int a_ic_cam, int a_ic_pencil, int a_ic_pin, int a_ic_smile_eye, int a_ic_smile, int a_ic_videocam);

	extern void set_telegram_textures(int a_telegram_sphere, int a_telegram_plane);
	extern void set_fast_textures(int a_fast_body, int a_fast_spiral, int a_fast_arrow, int a_fast_arrow_shadow);
	extern void set_free_textures(int a_knot_up, int a_knot_down);
	extern void set_powerful_textures(int a_powerful_mask, int a_powerful_star, int a_powerful_infinity, int a_powerful_infinity_white);
	extern void set_private_textures(int a_private_door, int a_private_screw);

	extern void set_ui_textures(int a_pagination_dot);

	extern void set_need_pages(int a);
	void set_y_offset(float a);


	extern void set_scroll_offset(float a_offset);

	extern void inc_stars_rendered();


	void set_elements_top_margins(int a_icon_y, int a_text_y, int a_button_y);

#ifdef __cplusplus
}
#endif
