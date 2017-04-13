// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include <wrl.h>
#include <wrl\wrappers\corewrappers.h>
#include <d2d1.h>
#include <vector>

using namespace Microsoft::WRL;
using namespace Microsoft::WRL::Wrappers;

namespace Unigram
{
	namespace Native
	{

		class FramesCacheStore WrlSealed : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IUnknown>
		{
		public:
			FramesCacheStore();
			~FramesCacheStore();

			const bool IsLocked() const
			{
				return m_mappedCacheFile.IsValid();
			}

			const DWORD GetFrameCount() const
			{
				return static_cast<DWORD>(m_frameDefinitionOffsets.size());
			}

			STDMETHODIMP RuntimeClassInitialize();
			HRESULT Lock();
			HRESULT WriteBitmapEntry(_In_ byte* buffer, DWORD bufferLength, DWORD rowPitch, LONGLONG delay);
			HRESULT ReadBitmapEntry(DWORD index, _In_ ID2D1Bitmap* bitmap, _Out_ LONGLONG* delay);

		private:

#pragma warning(push)
#pragma warning(disable : 4200)
			struct CachedFrameEntry
			{
				LONGLONG Delay;
				DWORD RowPitch;
				DWORD DataLength;
				byte Data[];
			};
#pragma warning(pop)

			static HRESULT CreateTemporaryFile(_Out_ HANDLE* temporaryFile);

			std::vector<DWORD> m_frameDefinitionOffsets;
			CachedFrameEntry* m_frameEntries;
			FileHandle m_cacheFile;
			FileHandle m_mappedCacheFile;
		};

	}
}