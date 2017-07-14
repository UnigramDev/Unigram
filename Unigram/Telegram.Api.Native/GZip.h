#pragma once
#include <zlib.h>
#include "NativeBuffer.h"

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			inline HRESULT GZipCompressBuffer(_In_reads_(inputBufferLength) const BYTE* inputBuffer, UINT32 inputBufferLength, _Out_ NativeBuffer** outputBuffer)
			{
				/*if (inputBuffer == nullptr)
				{
					return E_INVALIDARG;
				}

				if (outputBuffer == nullptr)
				{
					return E_POINTER;
				}*/

				z_stream stream = {};
				stream.avail_in = inputBufferLength;
				stream.next_in = const_cast<BYTE*>(inputBuffer);

				if (deflateInit2(&stream, Z_BEST_COMPRESSION, Z_DEFLATED, 15 + 16, 8, Z_DEFAULT_STRATEGY) != Z_OK)
				{
					return E_FAIL;
				}

				HRESULT result;
				int zlibResult;
				ComPtr<NativeBuffer> compressedBuffer;

				do
				{
					BreakIfFailed(result, MakeAndInitialize<NativeBuffer>(&compressedBuffer, deflateBound(&stream, stream.avail_in)));

					stream.avail_out = compressedBuffer->GetCapacity();
					stream.next_out = compressedBuffer->GetBuffer();

					zlibResult = deflate(&stream, Z_FINISH);
					if (zlibResult != Z_OK && zlibResult != Z_STREAM_END)
					{
						result = E_FAIL;
						break;
					}

					if (stream.total_out >= inputBufferLength - 4)
					{
						result = S_FALSE;
					}
					else
					{
						result = compressedBuffer->Resize(stream.total_out);
					}
				} while (false);

				if (result == S_OK)
				{
					*outputBuffer = compressedBuffer.Detach();
				}

				deflateEnd(&stream);
				return result;
			}

			inline HRESULT GZipDecompressBuffer(_In_reads_(inputBufferLength) const BYTE* inputBuffer, UINT32 inputBufferLength, _Out_ NativeBuffer** outputBuffer)
			{
				/*if (inputBuffer == nullptr)
				{
					return E_INVALIDARG;
				}

				if (outputBuffer == nullptr)
				{
					return E_POINTER;
				}*/

				UINT32 bufferLengthIncrement = inputBufferLength;

				HRESULT result;
				ComPtr<NativeBuffer> uncompressedBuffer;
				ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&uncompressedBuffer, bufferLengthIncrement));

				z_stream stream = {};
				stream.avail_in = inputBufferLength;
				stream.next_in = const_cast<BYTE*>(inputBuffer);

				if (inflateInit2(&stream, 15 + 32) != Z_OK)
				{
					return E_FAIL;
				}

				stream.avail_out = bufferLengthIncrement;
				stream.next_out = uncompressedBuffer->GetBuffer();

				int zlibResult;
				UINT32 totalBufferIncrement = 0;

				do
				{
					zlibResult = inflate(&stream, Z_NO_FLUSH);
					if (zlibResult == Z_STREAM_END)
					{
						break;
					}
					else if (zlibResult == Z_OK)
					{
						totalBufferIncrement += bufferLengthIncrement;

						BreakIfFailed(result, uncompressedBuffer->Resize(bufferLengthIncrement + totalBufferIncrement));

						stream.avail_out = bufferLengthIncrement;
						stream.next_out = uncompressedBuffer->GetBuffer() + totalBufferIncrement;
					}
					else
					{
						result = E_FAIL;
						break;
					}
				} while (true);

				if (SUCCEEDED(result) && SUCCEEDED(result = uncompressedBuffer->Resize(stream.total_out)))
				{
					*outputBuffer = uncompressedBuffer.Detach();
				}

				inflateEnd(&stream);
				return result;
			}

		}
	}
}