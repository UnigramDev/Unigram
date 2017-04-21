#pragma once
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			public interface class ITLObject
			{
				void Read(_In_ TLBinaryReader^ reader);
				void Write(_In_ TLBinaryWriter^ writer);
			};

		}
	}
}