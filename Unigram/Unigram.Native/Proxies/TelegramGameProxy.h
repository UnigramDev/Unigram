#pragma once

#include <iostream>  
#include <iomanip>
#include <sstream>
#include <vector>
#include <windows.h>
#include "Shlwapi.h"

using namespace Platform;
using namespace Windows::Foundation::Metadata;

namespace Unigram
{
	namespace Native
	{
		public delegate void TelegramGameProxyDelegate(bool withMyScore);

		[AllowForWeb]
		public ref class TelegramGameProxy sealed
		{
		public:
			TelegramGameProxy(TelegramGameProxyDelegate^ delegate);

			void PostEvent(String^ eventName, String^ eventData);

		private:
			TelegramGameProxyDelegate^ _delegate;
		};
	}
}