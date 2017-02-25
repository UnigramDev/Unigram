/****************************** Module Header ******************************\
* Module Name:  DirectXSample.h
* Project:      WindowsRuntimeComponent1
* Copyright (c) Microsoft Corporation.
*
* This header defines helper utilities to make DirectX APIs work with exceptions.
*
* This source is subject to the Microsoft Public License.
* See http://www.microsoft.com/en-us/openness/licenses.aspx#MPL
* All other rights reserved.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND,
* EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
\***************************************************************************/

#pragma once

// This header defines helper utilities to make DirectX APIs work with exceptions.
namespace DX
{
	inline void ThrowIfFailed(HRESULT hr)
	{
		if (FAILED(hr))
		{
			// Set a breakpoint on this line to catch DX API errors.
			throw Platform::Exception::CreateException(hr);
		}
	}
}
