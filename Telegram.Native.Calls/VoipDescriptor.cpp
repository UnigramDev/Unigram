#include "pch.h"
#include "VoipDescriptor.h"
#include "VoipDescriptor.g.cpp"

namespace winrt::Telegram::Native::Calls::implementation
{
    hstring VoipDescriptor::Version()
    {
        return m_version;
    }

    void VoipDescriptor::Version(hstring value)
    {
        m_version = value;
    }

    hstring VoipDescriptor::CustomParameters()
    {
        return m_customParameters;
    }

    void VoipDescriptor::CustomParameters(hstring value)
    {
        m_customParameters = value;
    }

    double VoipDescriptor::InitializationTimeout()
    {
        return m_initializationTimeout;
    }

    void VoipDescriptor::InitializationTimeout(double value)
    {
        m_initializationTimeout = value;
    }

    double VoipDescriptor::ReceiveTimeout()
    {
        return m_receiveTimeout;
    }

    void VoipDescriptor::ReceiveTimeout(double value)
    {
        m_receiveTimeout = value;
    }

    VoipDataSaving VoipDescriptor::DataSaving()
    {
        return m_dataSaving;
    }

    void VoipDescriptor::DataSaving(VoipDataSaving value)
    {
        m_dataSaving = value;
    }

    winrt::Windows::Foundation::Collections::IVector<uint8_t> VoipDescriptor::PersistentState()
    {
        return m_persistentState;
    }

    void VoipDescriptor::PersistentState(winrt::Windows::Foundation::Collections::IVector<uint8_t> value)
    {
        m_persistentState = value;
    }

    winrt::Windows::Foundation::Collections::IVector<Telegram::Td::Api::CallServer> VoipDescriptor::Servers()
    {
        return m_servers;
    }

    void VoipDescriptor::Servers(winrt::Windows::Foundation::Collections::IVector<Telegram::Td::Api::CallServer> value)
    {
        m_servers = value;
    }

    winrt::Windows::Foundation::Collections::IVector<uint8_t> VoipDescriptor::EncryptionKey()
    {
        return m_encryptionKey;
    }

    void VoipDescriptor::EncryptionKey(winrt::Windows::Foundation::Collections::IVector<uint8_t> value)
    {
        m_encryptionKey = value;
    }

    bool VoipDescriptor::IsOutgoing()
    {
        return m_isOutgoing;
    }

    void VoipDescriptor::IsOutgoing(bool value)
    {
        m_isOutgoing = value;
    }

    bool VoipDescriptor::EnableP2p()
    {
        return m_enableP2p;
    }

    void VoipDescriptor::EnableP2p(bool value)
    {
        m_enableP2p = value;
    }

    hstring VoipDescriptor::AudioInputId()
    {
        return m_audioInputId;
    }

    void VoipDescriptor::AudioInputId(hstring value)
    {
        m_audioInputId = value;
    }

    hstring VoipDescriptor::AudioOutputId()
    {
        return m_audioOutputId;
    }

    void VoipDescriptor::AudioOutputId(hstring value)
    {
        m_audioOutputId = value;
    }

    VoipCaptureBase VoipDescriptor::VideoCapture()
    {
        return m_videoCapture;
    }

    void VoipDescriptor::VideoCapture(VoipCaptureBase value)
    {
        m_videoCapture = value;
    }

} // namespace winrt::Telegram::Native::Calls::implementation
