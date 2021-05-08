#include "pch.h"
#include "VoipScreenCapture.h"
#if __has_include("VoipScreenCapture.g.cpp")
#include "VoipScreenCapture.g.cpp"
#endif

#include "StaticThreads.h"
#include "platform/uwp/UwpContext.h"

namespace winrt::Unigram::Native::Calls::implementation
{
    VoipScreenCapture::VoipScreenCapture(GraphicsCaptureItem item)
    {
        m_impl = tgcalls::VideoCaptureInterface::Create(
            tgcalls::StaticThreads::getThreads(),
            "GraphicsCaptureItem",
            std::make_shared<tgcalls::UwpContext>(item));
    }
}
