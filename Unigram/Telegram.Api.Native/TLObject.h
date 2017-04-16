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
				void Read(TLBinaryReader^ reader);
				void Write(TLBinaryWriter^ writer);
			};

		}
	}
}