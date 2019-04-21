#pragma once
#include "AvEffectDefinition.h"
#include "IAvEffect.h"

using namespace Windows::Foundation::Collections;


namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			ref class AbstractEffectFactory abstract
			{
			internal:
				virtual IAvEffect^ CreateEffect(IVectorView<AvEffectDefinition^>^ definitions) abstract;
			};
		}
	}
}
