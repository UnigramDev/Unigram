#pragma once

#include "pch.h"
#include <time.h>

namespace Telegram
{
	namespace Intro
	{
		class SimpleRenderer
		{
		public:
			SimpleRenderer(float scale);
			~SimpleRenderer();
			void Draw();
			void UpdateWindowSize(GLsizei width, GLsizei height);

			void SetCurrentPage(int page);
			void SetCurrentScroll(float scroll);
			void SetDarkTheme(int theme);

		private:
			GLsizei mWindowWidth;
			GLsizei mWindowHeight;

			float mCurrentScroll;
			int mCurrentPage;
			int mDarkTheme;

			double CFAbsoluteTimeGetCurrent();
		};
	}
}