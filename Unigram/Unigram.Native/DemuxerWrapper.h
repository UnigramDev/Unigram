#pragma once
#include <vector>
#include <memory>
#include <webp\decode.h>
#include <webp\demux.h>

class WebPDemuxerWrapper
{
public:
	WebPDemuxerWrapper(
		std::unique_ptr<WebPDemuxer, decltype(&WebPDemuxDelete)>&& pDemuxer,
		std::vector<uint8_t>&& pBuffer) :
		m_pDemuxer(std::move(pDemuxer)),
		m_pBuffer(std::move(pBuffer)) {
	}

	virtual ~WebPDemuxerWrapper() {
		//FBLOGD("Deleting Demuxer");
	}

	WebPDemuxer* get() {
		return m_pDemuxer.get();
	}

	size_t getBufferSize() {
		return m_pBuffer.size();
	}

private:
	std::unique_ptr<WebPDemuxer, decltype(&WebPDemuxDelete)> m_pDemuxer;
	std::vector<uint8_t> m_pBuffer;
};