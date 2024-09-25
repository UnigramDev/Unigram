#pragma once

#include "VoipDescriptor.g.h"
#include "Instance.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    struct VoipDescriptor : VoipDescriptorT<VoipDescriptor>
    {
        VoipDescriptor() = default;

        hstring m_version;
        hstring Version();
        void Version(hstring value);

        hstring m_customParameters;
        hstring CustomParameters();
        void CustomParameters(hstring value);

        double m_initializationTimeout{ 0 };
        double InitializationTimeout();
        void InitializationTimeout(double value);

        double m_receiveTimeout{ 0 };
        double ReceiveTimeout();
        void ReceiveTimeout(double value);

        VoipDataSaving m_dataSaving{ VoipDataSaving::Never };
        VoipDataSaving DataSaving();
        void DataSaving(VoipDataSaving value);

        winrt::Windows::Foundation::Collections::IVector<uint8_t> m_persistentState{ nullptr };
        winrt::Windows::Foundation::Collections::IVector<uint8_t> PersistentState();
        void PersistentState(winrt::Windows::Foundation::Collections::IVector<uint8_t> value);

        winrt::Windows::Foundation::Collections::IVector<Telegram::Td::Api::CallServer> m_servers{ nullptr };
        winrt::Windows::Foundation::Collections::IVector<Telegram::Td::Api::CallServer> Servers();
        void Servers(winrt::Windows::Foundation::Collections::IVector<Telegram::Td::Api::CallServer> value);

        winrt::Windows::Foundation::Collections::IVector<uint8_t> m_encryptionKey{ nullptr };
        winrt::Windows::Foundation::Collections::IVector<uint8_t> EncryptionKey();
        void EncryptionKey(winrt::Windows::Foundation::Collections::IVector<uint8_t> value);

        bool m_isOutgoing;
        bool IsOutgoing();
        void IsOutgoing(bool value);

        bool m_enableP2p;
        bool EnableP2p();
        void EnableP2p(bool value);

        hstring m_audioInputId{ L"" };
        hstring AudioInputId();
        void AudioInputId(hstring value);

        hstring m_audioOutputId{ L"" };
        hstring AudioOutputId();
        void AudioOutputId(hstring value);

        VoipCaptureBase m_videoCapture{ nullptr };
        VoipCaptureBase VideoCapture();
        void VideoCapture(VoipCaptureBase value);
    };
} // namespace winrt::Telegram::Native::Calls::implementation

namespace winrt::Telegram::Native::Calls::factory_implementation
{
    struct VoipDescriptor : VoipDescriptorT<VoipDescriptor, implementation::VoipDescriptor>
    {
    };
} // namespace winrt::Telegram::Native::Calls::factory_implementation
