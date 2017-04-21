#pragma once

using namespace Platform;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			public ref class TLBinaryReader sealed
			{
			public:
				int32 ReadInt32();
				int64 ReadInt64();
				bool ReadBool();
				uint8 ReadByte();
				String^ ReadString();
				Array<uint8>^ ReadByteArray();
				double ReadDouble();

			internal:
				TLBinaryReader();
			};

		}
	}
}