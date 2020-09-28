// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#pragma once

#include "VoipDescriptor.g.h"
#include "Instance.h"

namespace winrt::Unigram::Native::Calls::implementation
{
	struct VoipDescriptor : VoipDescriptorT<VoipDescriptor>
	{
		VoipDescriptor();

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

		winrt::Windows::Foundation::Collections::IVector<VoipServer> m_servers{ nullptr };
		winrt::Windows::Foundation::Collections::IVector<VoipServer> Servers();
		void Servers(winrt::Windows::Foundation::Collections::IVector<VoipServer> value);

		winrt::Windows::Foundation::Collections::IVector<uint8_t> m_encryptionKey{ nullptr };
		winrt::Windows::Foundation::Collections::IVector<uint8_t> EncryptionKey();
		void EncryptionKey(winrt::Windows::Foundation::Collections::IVector<uint8_t> value);

		bool m_isOutgoing;
		bool IsOutgoing();
		void IsOutgoing(bool value);

		hstring m_audioInputId{ L"" };
		hstring AudioInputId();
		void AudioInputId(hstring value);

		hstring m_audioOutputId{ L"" };
		hstring AudioOutputId();
		void AudioOutputId(hstring value);

		VoipVideoCapture m_videoCapture{ nullptr };
		VoipVideoCapture VideoCapture();
		void VideoCapture(VoipVideoCapture value);
	};
} // namespace winrt::Unigram::Native::Calls::implementation

namespace winrt::Unigram::Native::Calls::factory_implementation
{
	struct VoipDescriptor : VoipDescriptorT<VoipDescriptor, implementation::VoipDescriptor>
	{
	};
} // namespace winrt::Unigram::Native::Calls::factory_implementation
