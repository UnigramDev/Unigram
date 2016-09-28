#pragma once

#include <collection.h>
#include <ppltasks.h>

#include <wrl.h>
#include <robuffer.h>

#include <webp\decode.h>
#include <webp\demux.h>

#include <mfobjects.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>

#include "COMHelper.h"

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