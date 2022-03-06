#pragma once

#include "CachedVideoAnimation.g.h"

#include <queue>
#include <mutex>

#include <winrt/Microsoft.Graphics.Canvas.h>

#include "VideoAnimation.h"

using namespace winrt::Microsoft::Graphics::Canvas;

#define CACHED_VERSION 1

namespace winrt::Unigram::Native::implementation
{
	class WorkQueue;

	struct CachedVideoAnimation : CachedVideoAnimationT<CachedVideoAnimation>
	{
		CachedVideoAnimation() = default;

		virtual ~CachedVideoAnimation() {
			Close();
		}

		void Close() {
			slim_lock_guard const guard(s_locks[m_cacheKey]);

			if (m_decompressBuffer) {
				delete[] m_decompressBuffer;
				m_decompressBuffer = nullptr;
			}

			if (m_animation) {
				m_animation->Close();
				m_animation = nullptr;
			}
		}

		static winrt::Unigram::Native::CachedVideoAnimation LoadFromFile(IVideoAnimationSource file, bool precache);


		void RenderSync(CanvasBitmap bitmap, int32_t& seconds);
		double FrameRate();

		int32_t TotalFrame();

		winrt::Windows::Foundation::Size Size();

		bool IsCaching();
		void IsCaching(bool value);

		int PixelWidth()
		{
			if (m_animation) {
				return m_animation->PixelWidth();
			}

			return m_pixelWidth;
		}

		int PixelHeight()
		{
			if (m_animation) {
				return m_animation->PixelHeight();
			}

			return m_pixelHeight;
		}

	private:
		bool LoadAnimation();
		void RenderSync(uint8_t* pixels, size_t w, size_t h, int32_t& seconds, bool* rendered);

		static void CompressThreadProc();

		static bool s_compressStarted;
		static std::thread s_compressWorker;
		static WorkQueue s_compressQueue;

		bool m_caching;

		static std::map<std::string, winrt::slim_mutex> s_locks;

		IVideoAnimationSource m_file{ nullptr };
		winrt::com_ptr<VideoAnimation> m_animation;
		size_t m_frameCount = 0;
		int32_t m_frameIndex = 0;
		int32_t m_fps = 30;
		int32_t m_pixelWidth = 0;
		int32_t m_pixelHeight = 0;
		bool m_precache = false;
		winrt::hstring m_path;
		std::wstring m_cacheFile;
		std::string m_data;
		std::string m_cacheKey;
		uint8_t* m_decompressBuffer = nullptr;
		uint32_t m_maxFrameSize = 0;
		uint32_t m_imageSize = 0;
		std::vector<uint32_t> m_fileOffsets;
		std::vector<std::pair<std::uint32_t, std::uint32_t>> m_colors;
	};

	class WorkItem {
	public:
		winrt::weak_ref<CachedVideoAnimation> animation;
		size_t w;
		size_t h;

		WorkItem(winrt::weak_ref<CachedVideoAnimation> animation, size_t w, size_t h)
			: animation(animation),
			w(w),
			h(h)
		{

		}
	};

	class WorkQueue
	{
		std::condition_variable work_available;
		std::mutex work_mutex;
		std::queue<WorkItem> work;

	public:
		void push_work(WorkItem item)
		{
			std::unique_lock<std::mutex> lock(work_mutex);

			bool was_empty = work.empty();
			work.push(item);

			//while (CACHE_QUEUE_SIZE < work.size())
			//{
			//	WorkItem tmp = std::move(work.front());
			//	work.pop();

			//	if (auto animation{ tmp.animation.get() }) {
			//		animation->IsCaching(false);
			//	}
			//}

			lock.unlock();

			if (was_empty)
			{
				work_available.notify_one();
			}
		}

		std::optional<WorkItem> wait_and_pop()
		{
			std::unique_lock<std::mutex> lock(work_mutex);
			while (work.empty())
			{
				const std::chrono::milliseconds timeout(3000);
				if (work_available.wait_for(lock, timeout) == std::cv_status::timeout) {
					return std::nullopt;
				}
			}

			WorkItem tmp = std::move(work.front());
			work.pop();
			return std::make_optional<WorkItem>(tmp);
		}
	};

}

namespace winrt::Unigram::Native::factory_implementation
{
	struct CachedVideoAnimation : CachedVideoAnimationT<CachedVideoAnimation, implementation::CachedVideoAnimation>
	{
	};
}
