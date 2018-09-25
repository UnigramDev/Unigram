#pragma once

#include <iostream>  
#include <iomanip>
#include <sstream>
#include <vector>
#include <windows.h>
#include "Shlwapi.h"

using namespace Platform;
using namespace Windows::Data::Json;
using namespace Windows::Foundation::Metadata;

namespace Unigram
{
	namespace Native
	{
		public delegate void TelegramPaymentProxyDelegate(String^ title, String^ credentials);

		[AllowForWeb]
		public ref class TelegramPaymentProxy sealed
		{
		public:
			TelegramPaymentProxy(TelegramPaymentProxyDelegate^ delegate);

			void PostEvent(String^ eventName, String^ eventData);

		private:
			TelegramPaymentProxyDelegate^ _delegate;
		};
	}
}