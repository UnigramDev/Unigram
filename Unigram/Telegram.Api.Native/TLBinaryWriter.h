#pragma once
#include <Windows.h>

using namespace Platform;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			public ref class TLBinaryWriter sealed
			{
			public:
				void WriteInt32(int32 value);
				void WriteInt64(int64 value);
				void WriteBool(bool value);
				void WriteByte(uint8 value);
				void WriteString(_In_ String^ value);
				void WriteByteArray(_In_ const Array<uint8>^ value);
				void WriteDouble(double value);

			internal:
				TLBinaryWriter();
			};

		}
	}
}