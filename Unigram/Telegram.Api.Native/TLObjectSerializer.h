#pragma once
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "Telegram.Api.Native.h"

using ABI::Telegram::Api::Native::TL::ITLObject;
using ABI::Telegram::Api::Native::TL::ITLBinaryReader;
using ABI::Telegram::Api::Native::TL::ITLObjectSerializerStatics;
using ABI::Telegram::Api::Native::TL::ITLObjectConstructorDelegate;
using ABI::Windows::Storage::Streams::IBuffer;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{
			namespace TL
			{

				class TLObjectSerializerStatics WrlSealed : public AgileActivationFactory<ITLObjectSerializerStatics>
				{
					InspectableClassStatic(RuntimeClass_Telegram_Api_Native_TL_TLObjectSerializer, BaseTrust);

				public:
					//COM exported methods
					IFACEMETHODIMP Serialize(_In_ ITLObject* object, _Out_ IBuffer** value);
					IFACEMETHODIMP Deserialize(_In_ IBuffer* buffer, _Out_ ITLObject** value);
					IFACEMETHODIMP GetObjectSize(_In_ ITLObject* object, _Out_ UINT32* value);
					IFACEMETHODIMP RegisterObjectConstructor(UINT32 constructor, _In_ ITLObjectConstructorDelegate* constructorDelegate);
				};

			}
		}
	}
}