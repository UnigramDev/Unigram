// Copyright (c) 2017 Lorenzo Rossoni

#pragma once
#include "pch.h"
#include "Helpers\COMHelper.h"
#include "FramesCacheStore.h"

using namespace Unigram::Native;

FramesCacheStore::FramesCacheStore() :
	m_frameEntries(nullptr)
{
}

FramesCacheStore::~FramesCacheStore()
{
	if (m_frameEntries != nullptr)
	{
		UnmapViewOfFile(m_frameEntries);
	}
}

HRESULT FramesCacheStore::RuntimeClassInitialize()
{
	return CreateTemporaryFile(m_cacheFile.ReleaseAndGetAddressOf());
}

HRESULT FramesCacheStore::Lock()
{
	if (m_mappedCacheFile.IsValid())
	{
		return E_NOT_VALID_STATE;
	}

	if (!SetEndOfFile(m_cacheFile.Get()))
	{
		return GetLastHRESULT();
	}

	m_mappedCacheFile.Attach(CreateFileMapping(m_cacheFile.Get(), nullptr, PAGE_READONLY, 0, 0, nullptr));
	if (!m_mappedCacheFile.IsValid())
	{
		return GetLastHRESULT();
	}

	m_frameEntries = reinterpret_cast<CachedFrameEntry*>(MapViewOfFile(m_mappedCacheFile.Get(), FILE_MAP_READ, 0, 0, 0));
	return S_OK;
}

HRESULT FramesCacheStore::WriteBitmapEntry(byte* buffer, DWORD bufferLength, DWORD rowPitch, LONGLONG delay)
{
	if (m_mappedCacheFile.IsValid())
	{
		return E_NOT_VALID_STATE;
	}

	CachedFrameEntry frameEntry = { delay, rowPitch, bufferLength };
	if (!WriteFile(m_cacheFile.Get(), &frameEntry, sizeof(CachedFrameEntry), nullptr, nullptr))
	{
		return GetLastHRESULT();
	}

	if (!WriteFile(m_cacheFile.Get(), buffer, bufferLength, nullptr, nullptr))
	{
		return GetLastHRESULT();
	}

	if (m_frameDefinitionOffsets.empty())
	{
		m_frameDefinitionOffsets.push_back(0);
	}
	else
	{
		m_frameDefinitionOffsets.push_back(m_frameDefinitionOffsets.back() + bufferLength + sizeof(CachedFrameEntry));
	}

	return S_OK;
}

HRESULT FramesCacheStore::ReadBitmapEntry(DWORD index, ID2D1Bitmap* bitmap, LONGLONG* delay)
{
	if (!m_mappedCacheFile.IsValid())
	{
		return E_NOT_VALID_STATE;
	}

	if (index >= m_frameDefinitionOffsets.size())
	{
		return E_BOUNDS;
	}

	auto frameEntry = reinterpret_cast<CachedFrameEntry*>(reinterpret_cast<byte*>(m_frameEntries) +
		m_frameDefinitionOffsets[index]);

	*delay = frameEntry->Delay;
	return bitmap->CopyFromMemory(nullptr, frameEntry->Data, frameEntry->RowPitch);
}

HRESULT FramesCacheStore::CreateTemporaryFile(HANDLE* temporaryFile)
{
	CREATEFILE2_EXTENDED_PARAMETERS extendedParams = {};
	extendedParams.dwSize = sizeof(CREATEFILE2_EXTENDED_PARAMETERS);
	extendedParams.dwFileAttributes = FILE_ATTRIBUTE_TEMPORARY;
	extendedParams.dwFileFlags = FILE_FLAG_SEQUENTIAL_SCAN | FILE_FLAG_DELETE_ON_CLOSE;
	extendedParams.dwSecurityQosFlags = SECURITY_ANONYMOUS;

	GUID guid;
	HRESULT result;
	ReturnIfFailed(result, CoCreateGuid(&guid));

	WCHAR fileName[MAX_PATH];
	auto temporaryPath = Windows::Storage::ApplicationData::Current->TemporaryFolder->Path;
	swprintf_s(fileName, ARRAYSIZE(fileName), L"%s\\%08X-%04X-%04X-%02X%02X-%02X%02X%02X%02X%02X%02X.tmp", temporaryPath->Data(),
		guid.Data1, guid.Data2, guid.Data3, guid.Data4[0], guid.Data4[1], guid.Data4[2], guid.Data4[3], guid.Data4[4], guid.Data4[5], guid.Data4[6], guid.Data4[7]);

	*temporaryFile = CreateFile2(fileName, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, CREATE_ALWAYS, &extendedParams);
	if (*temporaryFile == INVALID_HANDLE_VALUE)
	{
		return GetLastHRESULT();
	}

	return S_OK;
}