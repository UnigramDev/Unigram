#include "pch.h"
#include "VoipGroupManager.h"
#if __has_include("VoipGroupManager.g.cpp")
#include "VoipGroupManager.g.cpp"
#endif

#include <winrt/Telegram.Td.Api.h>

namespace winrt::Unigram::Native::Calls::implementation
{
	VoipGroupManager::VoipGroupManager(VoipGroupDescriptor descriptor) {
		auto logPath = Windows::Storage::ApplicationData::Current().LocalFolder().Path();
		logPath = logPath + hstring(L"\\tgcalls_group.txt");

		tgcalls::GroupConfig config = tgcalls::GroupConfig{
			logPath.data()
		};

		tgcalls::GroupInstanceDescriptor impl = tgcalls::GroupInstanceDescriptor{
			config,
			[this](bool state) {
				m_networkStateUpdated(*this, state);
			},
			[this](tgcalls::GroupLevelsUpdate const& levels) {
				auto args = winrt::single_threaded_map<int32_t, IKeyValuePair<float, bool>>(/*std::move(levels)*/);

				for (const tgcalls::GroupLevelUpdate& x : levels.updates) {
					args.Insert(x.ssrc, winrt::make<winrt::impl::key_value_pair<IKeyValuePair<float, bool>>>(x.value.level, x.value.voice));
				}

				m_audioLevelsUpdated(*this, args.GetView());
			},
			string_to_unmanaged(descriptor.AudioInputId()),
			string_to_unmanaged(descriptor.AudioOutputId()),
			false
		};

		m_impl = std::make_unique<tgcalls::GroupInstanceImpl>(std::move(impl));
	}

	void VoipGroupManager::Close() {
		m_impl->stop();
		m_impl.reset();
	}

	void VoipGroupManager::EmitJoinPayload(EmitJsonPayloadDelegate completion) {
		m_impl->emitJoinPayload([completion](auto const& payload) {
			auto fingerprints = winrt::single_threaded_vector<GroupCallPayloadFingerprint>();

			for (const tgcalls::GroupJoinPayloadFingerprint& x : payload.fingerprints) {
				fingerprints.Append(GroupCallPayloadFingerprint(
					string_from_unmanaged(x.hash),
					string_from_unmanaged(x.setup),
					string_from_unmanaged(x.fingerprint)
					));
			}

			GroupCallPayload result = GroupCallPayload(
				string_from_unmanaged(payload.ufrag),
				string_from_unmanaged(payload.pwd),
				fingerprints
			);

			completion(payload.ssrc, result);
			});
	}

	void VoipGroupManager::SetJoinResponsePayload(GroupCallJoinResponse payload) {
		auto fingerprints = std::vector<tgcalls::GroupJoinPayloadFingerprint>();
		auto candidates = std::vector<tgcalls::GroupJoinResponseCandidate>();

		for (const GroupCallPayloadFingerprint& x : payload.Payload().Fingerprints()) {
			fingerprints.push_back(tgcalls::GroupJoinPayloadFingerprint{
				string_to_unmanaged(x.Hash()),
				string_to_unmanaged(x.Setup()),
				string_to_unmanaged(x.Fingerprint()),
				});
		}

		for (const GroupCallJoinResponseCandidate& x : payload.Candidates()) {
			candidates.push_back(tgcalls::GroupJoinResponseCandidate{
				string_to_unmanaged(x.Port()),
				string_to_unmanaged(x.Protocol()),
				string_to_unmanaged(x.Network()),
				string_to_unmanaged(x.Generation()),
				string_to_unmanaged(x.Id()),
				string_to_unmanaged(x.Component()),
				string_to_unmanaged(x.Foundation()),
				string_to_unmanaged(x.Priority()),
				string_to_unmanaged(x.Ip()),
				string_to_unmanaged(x.Type()),

				string_to_unmanaged(x.TcpType()),
				string_to_unmanaged(x.RelAddr()),
				string_to_unmanaged(x.RelPort()),
				});
		}

		tgcalls::GroupJoinResponsePayload impl = tgcalls::GroupJoinResponsePayload{
			string_to_unmanaged(payload.Payload().Ufrag()),
			string_to_unmanaged(payload.Payload().Pwd()),
			fingerprints,
			candidates
		};

		m_impl->setJoinResponsePayload(std::move(impl));
	}

	void VoipGroupManager::RemoveSsrcs(IVector<int32_t> ssrcs) {
		auto impl = std::vector<uint32_t>();

		for (const uint32_t& x : ssrcs) {
			impl.push_back(x);
		}

		m_impl->removeSsrcs(impl);
	}



	bool VoipGroupManager::IsMuted() {
		return m_isMuted;
	}

	void VoipGroupManager::IsMuted(bool value) {
		m_impl->setIsMuted(m_isMuted = value);
	}

	void VoipGroupManager::SetAudioOutputDevice(hstring id) {
		m_impl->setAudioOutputDevice(string_to_unmanaged(id));
	}
	void VoipGroupManager::SetAudioInputDevice(hstring id) {
		m_impl->setAudioInputDevice(string_to_unmanaged(id));
	}



	winrt::event_token VoipGroupManager::NetworkStateUpdated(Windows::Foundation::TypedEventHandler<
		winrt::Unigram::Native::Calls::VoipGroupManager,
		bool> const& value)
	{
		return m_networkStateUpdated.add(value);
	}

	void VoipGroupManager::NetworkStateUpdated(winrt::event_token const& token)
	{
		m_networkStateUpdated.remove(token);
	}



	winrt::event_token VoipGroupManager::AudioLevelsUpdated(Windows::Foundation::TypedEventHandler<
		winrt::Unigram::Native::Calls::VoipGroupManager,
		IMapView<int32_t, IKeyValuePair<float, bool>>> const& value)
	{
		return m_audioLevelsUpdated.add(value);
	}

	void VoipGroupManager::AudioLevelsUpdated(winrt::event_token const& token)
	{
		m_audioLevelsUpdated.remove(token);
	}
}
