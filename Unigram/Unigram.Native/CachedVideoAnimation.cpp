#include "pch.h"
#include "CachedVideoAnimation.h"
#if __has_include("CachedVideoAnimation.g.cpp")
#include "CachedVideoAnimation.g.cpp"
#endif

#define QOI_IMPLEMENTATION
#define QOI_NO_STDIO

#include <lz4.h>
#include <qoi.h>

namespace winrt::Unigram::Native::implementation
{
	std::map<std::string, winrt::slim_mutex> CachedVideoAnimation::s_locks;

	bool CachedVideoAnimation::s_compressStarted;
	std::thread CachedVideoAnimation::s_compressWorker;
	WorkQueue CachedVideoAnimation::s_compressQueue;

	winrt::Unigram::Native::CachedVideoAnimation CachedVideoAnimation::LoadFromFile(IVideoAnimationSource file, bool createCache)
	{
		auto info = winrt::make_self<CachedVideoAnimation>();
		file.SeekCallback(0);

		if (createCache) {
			auto path = file.FilePath();
			if (path.size()) {
				info->m_cacheFile = path + L".cache";
				info->m_cacheKey = to_string(path);
				info->m_precache = true;

				slim_lock_guard const guard(s_locks[info->m_cacheKey]);

				FILE* precacheFile = _wfopen(info->m_cacheFile.c_str(), L"r+b");
				if (precacheFile != nullptr) {
					uint8_t temp;
					size_t read = fread(&temp, sizeof(uint8_t), 1, precacheFile);
					if (read == 1 && temp == CACHED_VERSION) {
						uint32_t headerOffset;
						fread(&headerOffset, sizeof(uint32_t), 1, precacheFile);
						if (headerOffset != 0)
						{
							fseek(precacheFile, headerOffset, SEEK_SET);
							fread(&info->m_maxFrameSize, sizeof(uint32_t), 1, precacheFile);
							fread(&info->m_imageSize, sizeof(uint32_t), 1, precacheFile);
							fread(&info->m_pixelWidth, sizeof(int32_t), 1, precacheFile);
							fread(&info->m_pixelHeight, sizeof(int32_t), 1, precacheFile);
							fread(&info->m_fps, sizeof(int32_t), 1, precacheFile);
							fread(&info->m_frameCount, sizeof(size_t), 1, precacheFile);
							info->m_fileOffsets = std::vector<uint32_t>(info->m_frameCount, 0);
							fread(&info->m_fileOffsets[0], sizeof(uint32_t), info->m_frameCount, precacheFile);

							createCache = false;
						}
					}

					fclose(precacheFile);
				}

				if (createCache) {
					info->m_animation = VideoAnimation::LoadFromFile(file, false, false).as<VideoAnimation>();
					if (info->m_animation == nullptr) {
						return nullptr;
					}

					info->m_pixelWidth = info->m_animation->PixelWidth();
					info->m_pixelHeight = info->m_animation->PixelHeight();
					info->m_fps = info->m_animation->FrameRate();
					info->m_precache = true;

					FILE* precacheFile = _wfopen(info->m_cacheFile.c_str(), L"w+b");
					if (precacheFile != nullptr) {
						uint8_t version = CACHED_VERSION;
						uint32_t offset = 0;
						fseek(precacheFile, 0, SEEK_SET);
						fwrite(&version, sizeof(uint8_t), 1, precacheFile);
						fwrite(&offset, sizeof(uint32_t), 1, precacheFile);

						fflush(precacheFile);
						fclose(precacheFile);
					}
				}
			}
		}
		else {
			info->m_animation = VideoAnimation::LoadFromFile(file, false, false).as<VideoAnimation>();
			if (info->m_animation == nullptr) {
				return nullptr;
			}

			info->m_pixelWidth = info->m_animation->PixelWidth();
			info->m_pixelHeight = info->m_animation->PixelHeight();
		}

		return info.as<winrt::Unigram::Native::CachedVideoAnimation>();
	}

	void CachedVideoAnimation::Stop() {
		if (m_animation != nullptr) {
			m_animation->SeekToMilliseconds(0, false);
		}

		m_frameIndex = 0;
	}

	void CachedVideoAnimation::RenderSync(CanvasBitmap bitmap, int32_t& seconds)
	{
		auto size = bitmap.SizeInPixels();
		auto w = size.Width;
		auto h = size.Height;

		uint8_t* pixels = new uint8_t[w * h * 4];
		bool rendered;
		RenderSync(pixels, w, h, seconds, &rendered);

		if (rendered) {
			bitmap.SetPixelBytes(winrt::array_view(pixels, w * h * 4));
		}

		delete[] pixels;
	}

	void CachedVideoAnimation::RenderSync(uint8_t* pixels, size_t w, size_t h, int32_t& seconds, bool* rendered)
	{
		bool loadedFromCache = false;
		if (rendered) {
			*rendered = false;
		}

		if (m_precache && m_maxFrameSize <= w * h * 4 && m_imageSize == w * h * 4) {
			uint32_t offset = m_fileOffsets[m_frameIndex];
			if (offset > 0) {
				slim_lock_guard const guard(s_locks[m_cacheKey]);

				FILE* precacheFile = _wfopen(m_cacheFile.c_str(), L"rb");
				if (precacheFile != nullptr) {
					fseek(precacheFile, offset, SEEK_SET);
					if (m_decompressBuffer == nullptr) {
						m_decompressBuffer = new uint8_t[m_maxFrameSize];
					}
					uint32_t frameSize;
					fread(&frameSize, sizeof(uint32_t), 1, precacheFile);
					if (frameSize <= m_maxFrameSize) {
						fread(m_decompressBuffer, sizeof(uint8_t), frameSize, precacheFile);
						//LZ4_decompress_safe((const char*)m_decompressBuffer, (char*)pixels, frameSize, w * h * 4);
						qoi_desc desc;
						qoi_decode_2((const void*)m_decompressBuffer, frameSize, &desc, 4, pixels);
						loadedFromCache = true;

						if (rendered) {
							*rendered = true;
						}
					}
					fclose(precacheFile);
					int framesPerUpdate = /*limitFps ? fps < 60 ? 1 : 2 :*/ 1;
					if (m_frameIndex + framesPerUpdate >= m_frameCount) {
						m_frameIndex = 0;
					}
					else {
						m_frameIndex += framesPerUpdate;
					}
				}
			}
		}

		if (!loadedFromCache && !m_caching) {
			if (m_animation == nullptr) {
				return;
			}

			bool completed;
			auto result = m_animation->RenderSync(pixels, false, seconds, completed);

			if (result && rendered) {
				*rendered = true;
			}

			if (m_precache) {
				m_caching = true;
				s_compressQueue.push_work(WorkItem(get_weak(), w, h));

				if (!s_compressStarted) {
					if (s_compressWorker.joinable()) {
						s_compressWorker.join();
					}

					s_compressStarted = true;
					s_compressWorker = std::thread(&CachedVideoAnimation::CompressThreadProc);
				}
			}
		}
	}

	void CachedVideoAnimation::CompressThreadProc() {
		while (s_compressStarted) {
			auto work = s_compressQueue.wait_and_pop();
			if (work == std::nullopt) {
				s_compressStarted = false;
				return;
			}

			auto oldW = 0;
			auto oldH = 0;

			int bound;
			uint8_t* compressBuffer = nullptr;
			uint8_t* pixels = nullptr;

			if (auto item{ work->animation.get() }) {
				auto w = work->w;
				auto h = work->h;

				slim_lock_guard const guard(s_locks[item->m_cacheKey]);

				FILE* precacheFile = _wfopen(item->m_cacheFile.c_str(), L"r+b");
				if (precacheFile != nullptr) {
					uint8_t temp;
					size_t read = fread(&temp, sizeof(uint8_t), 1, precacheFile);
					if (read == 1 && temp == CACHED_VERSION) {
						uint32_t headerOffset;
						fread(&headerOffset, sizeof(uint32_t), 1, precacheFile);
						if (headerOffset != 0)
						{
							fseek(precacheFile, headerOffset, SEEK_SET);
							fread(&item->m_maxFrameSize, sizeof(uint32_t), 1, precacheFile);
							fread(&item->m_imageSize, sizeof(uint32_t), 1, precacheFile);
							fread(&item->m_pixelWidth, sizeof(int32_t), 1, precacheFile);
							fread(&item->m_pixelHeight, sizeof(int32_t), 1, precacheFile);
							fread(&item->m_fps, sizeof(int32_t), 1, precacheFile);
							fread(&item->m_frameCount, sizeof(size_t), 1, precacheFile);
							item->m_fileOffsets = std::vector<uint32_t>(item->m_frameCount, 0);
							fread(&item->m_fileOffsets[0], sizeof(uint32_t), item->m_frameCount, precacheFile);

							item->m_caching = false;
							continue;
						}
					}

					fseek(precacheFile, sizeof(uint8_t) + sizeof(uint32_t), SEEK_SET);
					size_t totalSize = ftell(precacheFile);

					if (w + h > oldW + oldH) {
						bound = w * h * (4 + 1) + QOI_HEADER_SIZE + sizeof(qoi_padding);
						//bound = LZ4_compressBound(w * h * 4);
						compressBuffer = new uint8_t[bound];
						pixels = new uint8_t[w * h * 4];
					}

					int32_t seconds = 0;
					bool completed = false;
					std::vector<uint32_t> offsets;

					do
					{
						offsets.push_back(totalSize);

						item->m_animation->RenderSync(pixels, false, seconds, completed);

						qoi_desc desc;
						desc.width = w;
						desc.height = h;
						desc.channels = 4;
						desc.colorspace = QOI_SRGB;

						uint32_t size;
						qoi_encode_2((const void*)pixels, &desc, compressBuffer, &size);
						//uint32_t size = (uint32_t)LZ4_compress_default((const char*)pixels, (char*)compressBuffer, w * h * 4, bound);

						if (size > item->m_maxFrameSize && item->m_decompressBuffer != nullptr) {
							delete[] item->m_decompressBuffer;
							item->m_decompressBuffer = nullptr;
						}

						item->m_maxFrameSize = std::max(item->m_maxFrameSize, size);

						fwrite(&size, sizeof(uint32_t), 1, precacheFile);
						fwrite(compressBuffer, sizeof(uint8_t), size, precacheFile);
						totalSize += size;
						totalSize += 4;
					} while (!completed);

					fseek(precacheFile, 0, SEEK_SET);
					uint8_t version = CACHED_VERSION;
					item->m_fileOffsets = offsets;
					item->m_frameCount = offsets.size();
					item->m_imageSize = (uint32_t)w * h * 4;
					fwrite(&version, sizeof(uint8_t), 1, precacheFile);
					fwrite(&totalSize, sizeof(uint32_t), 1, precacheFile);
					fseek(precacheFile, 0, SEEK_END);
					fwrite(&item->m_maxFrameSize, sizeof(uint32_t), 1, precacheFile);
					fwrite(&item->m_imageSize, sizeof(uint32_t), 1, precacheFile);
					fwrite(&item->m_pixelWidth, sizeof(int32_t), 1, precacheFile);
					fwrite(&item->m_pixelHeight, sizeof(int32_t), 1, precacheFile);
					fwrite(&item->m_fps, sizeof(int32_t), 1, precacheFile);
					fwrite(&item->m_frameCount, sizeof(size_t), 1, precacheFile);
					fwrite(&item->m_fileOffsets[0], sizeof(uint32_t), item->m_frameCount, precacheFile);

					fflush(precacheFile);
					fclose(precacheFile);
				}

				item->m_caching = false;

				oldW = w;
				oldH = h;
			}

			if (compressBuffer) {
				delete[] compressBuffer;
			}

			if (pixels) {
				delete[] pixels;
			}
		}
	}

#pragma region Properties

	double CachedVideoAnimation::FrameRate()
	{
		if (m_animation) {
			return m_animation->FrameRate();
		}

		return m_fps;
	}

	int32_t CachedVideoAnimation::TotalFrame()
	{
		return INT_MAX;
		return m_frameCount;
	}

	winrt::Windows::Foundation::Size CachedVideoAnimation::Size()
	{
		size_t width = m_animation->PixelWidth();
		size_t height = m_animation->PixelHeight();

		return winrt::Windows::Foundation::Size(width, height);
	}

	bool CachedVideoAnimation::IsCaching() {
		return m_caching;
	}

	void CachedVideoAnimation::IsCaching(bool value) {
		m_caching = value;
	}

#pragma endregion

}
