// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

// clang-format off
#include "pch.h"
#include "VoipDescriptor.h"
#include "VoipDescriptor.g.cpp"
// clang-format on

namespace winrt::Unigram::Native::Calls::implementation
{
    VoipDescriptor::VoipDescriptor()
    {
    }

	double VoipDescriptor::InitializationTimeout() {
		return m_initializationTimeout;
	}

	void VoipDescriptor::InitializationTimeout(double value) {
		m_initializationTimeout = value;
	}

	double VoipDescriptor::ReceiveTimeout() {
		return m_receiveTimeout;
	}

	void VoipDescriptor::ReceiveTimeout(double value) {
		m_receiveTimeout = value;
	}

	VoipDataSaving VoipDescriptor::DataSaving() {
		return m_dataSaving;
	}

	void VoipDescriptor::DataSaving(VoipDataSaving value) {
		m_dataSaving = value;
	}

	winrt::Windows::Foundation::Collections::IVector<uint8_t> VoipDescriptor::PersistentState() {
		return m_persistentState;
	}

	void VoipDescriptor::PersistentState(winrt::Windows::Foundation::Collections::IVector<uint8_t> value) {
		m_persistentState = value;
	}

	winrt::Windows::Foundation::Collections::IVector<VoipServer> VoipDescriptor::Servers() {
		return m_servers;
	}

	void VoipDescriptor::Servers(winrt::Windows::Foundation::Collections::IVector<VoipServer> value) {
		m_servers = value;
	}

	winrt::Windows::Foundation::Collections::IVector<uint8_t> VoipDescriptor::EncryptionKey() {
		return m_encryptionKey;
	}

	void VoipDescriptor::EncryptionKey(winrt::Windows::Foundation::Collections::IVector<uint8_t> value) {
		m_encryptionKey = value;
	}

	bool VoipDescriptor::IsOutgoing() {
		return m_isOutgoing;
	}

	void VoipDescriptor::IsOutgoing(bool value) {
		m_isOutgoing = value;
	}

	hstring VoipDescriptor::AudioInputId() {
		return m_audioInputId;
	}

	void VoipDescriptor::AudioInputId(hstring value) {
		m_audioInputId = value;
	}

	hstring VoipDescriptor::AudioOutputId() {
		return m_audioOutputId;
	}

	void VoipDescriptor::AudioOutputId(hstring value) {
		m_audioOutputId = value;
	}

	VoipVideoCapture VoipDescriptor::VideoCapture() {
		return m_videoCapture;
	}

	void VoipDescriptor::VideoCapture(VoipVideoCapture value) {
		m_videoCapture = value;
	}

} // namespace winrt::Unigram::Native::Calls::implementation
