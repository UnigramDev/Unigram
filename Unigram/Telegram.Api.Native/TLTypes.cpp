#include "pch.h"
#include "TLTypes.h"
#include "Datacenter.h"
#include "Connection.h"
#include "DatacenterServer.h"
#include "DatacenterCryptography.h"
#include "ConnectionManager.h"
#include "TLBinaryReader.h"
#include "TLBinaryWriter.h"
#include "GZip.h"
#include "Collections.h"
#include "Reference.h"
#include "Helpers\COMHelper.h"

using namespace Telegram::Api::Native;
using namespace Telegram::Api::Native::TL;
using Windows::Foundation::Collections::VectorView;


RegisterTLObjectConstructor(TLDCOption);
RegisterTLObjectConstructor(TLDisabledFeature);
RegisterTLObjectConstructor(TLConfig);
RegisterTLObjectConstructor(TLConfigSimple);
RegisterTLObjectConstructor(TLIpPort);
RegisterTLObjectConstructor(TLCDNPublicKey);
RegisterTLObjectConstructor(TLCDNConfig);
RegisterTLObjectConstructor(TLRPCError);
//RegisterTLObjectConstructor(TLBoolTrue);
//RegisterTLObjectConstructor(TLBoolFalse);
RegisterTLObjectConstructor(TLRpcReqError);
RegisterTLObjectConstructor(TLRpcResult);
RegisterTLObjectConstructor(TLRpcAnswerDropped);
RegisterTLObjectConstructor(TLRpcAnswerDroppedRunning);
RegisterTLObjectConstructor(TLRpcAnswerUnknown);
RegisterTLObjectConstructor(TLMsgsAck);
RegisterTLObjectConstructor(TLMessage);
RegisterTLObjectConstructor(TLMsgContainer);
RegisterTLObjectConstructor(TLMsgCopy);
RegisterTLObjectConstructor(TLMsgsStateReq);
RegisterTLObjectConstructor(TLMsgResendReq);
RegisterTLObjectConstructor(TLMsgDetailedInfo);
RegisterTLObjectConstructor(TLMsgNewDetailedInfo);
RegisterTLObjectConstructor(TLMsgsAllInfo);
RegisterTLObjectConstructor(TLMsgsStateInfo);
RegisterTLObjectConstructor(TLGZipPacked);
RegisterTLObjectConstructor(TLAuthExportedAuthorization);
RegisterTLObjectConstructor(TLNewSessionCreated);
RegisterTLObjectConstructor(TLBadMessage);
RegisterTLObjectConstructor(TLBadServerSalt);
RegisterTLObjectConstructor(TLPong);
RegisterTLObjectConstructor(TLDHGenOk);
RegisterTLObjectConstructor(TLDHGenFail);
RegisterTLObjectConstructor(TLDHGenRetry);
RegisterTLObjectConstructor(TLServerDHParamsFail);
RegisterTLObjectConstructor(TLServerDHParamsOk);
RegisterTLObjectConstructor(TLResPQ);
RegisterTLObjectConstructor(TLFutureSalts);
RegisterTLObjectConstructor(TLFutureSalt);

ActivatableStaticOnlyFactory(TLConfigStatics);


TLDCOption::TLDCOption() :
	m_flags(TLDCOptionFlag::None),
	m_id(0),
	m_port(0)
{
}

TLDCOption::~TLDCOption()
{
}

HRESULT TLDCOption::get_Flags(TLDCOptionFlag* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_flags;
	return S_OK;
}

HRESULT TLDCOption::get_Id(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_id;
	return S_OK;
}

HRESULT TLDCOption::get_IpAddress(HSTRING* value)
{
	return m_ipAddress.CopyTo(value);
}

HRESULT TLDCOption::get_Port(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_port;
	return S_OK;
}

HRESULT TLDCOption::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(reinterpret_cast<INT32*>(&m_flags)));
	ReturnIfFailed(result, reader->ReadInt32(&m_id));
	ReturnIfFailed(result, reader->ReadString(m_ipAddress.GetAddressOf()));

	return reader->ReadInt32(&m_port);
}

HRESULT TLDCOption::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(static_cast<INT32>(m_flags)));
	ReturnIfFailed(result, writer->WriteInt32(m_id));
	ReturnIfFailed(result, writer->WriteString(m_ipAddress.Get()));

	return writer->WriteInt32(m_port);
}


HRESULT TLDisabledFeature::get_Feature(HSTRING* value)
{
	return m_feature.CopyTo(value);
}

HRESULT TLDisabledFeature::get_Description(HSTRING* value)
{
	return m_description.CopyTo(value);
}

HRESULT TLDisabledFeature::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadString(m_feature.GetAddressOf()));

	return reader->ReadString(m_description.GetAddressOf());
}

HRESULT TLDisabledFeature::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteString(m_feature.Get()));

	return writer->WriteString(m_description.Get());
}


TLConfig::TLConfig() :
	m_flags(TLConfigFlag::None),
	m_date(0),
	m_expires(0),
	m_testMode(false),
	m_thisDc(0),
	m_chatSizeMax(0),
	m_megaGroupSizeMax(0),
	m_forwardedCountMax(0),
	m_onlineUpdatePeriodMs(0),
	m_offlineBlurTimeoutMs(0),
	m_offlineIdleTimeoutMs(0),
	m_onlineCloudTimeoutMs(0),
	m_notifyCloudDelayMs(0),
	m_notifyDefaultDelayMs(0),
	m_chatBigSize(0),
	m_pushChatPeriodMs(0),
	m_pushChatLimit(0),
	m_savedGifsLimit(0),
	m_editTimeLimit(0),
	m_ratingEDecay(0),
	m_stickersRecentLimit(0),
	m_stickersFavedLimit(0),
	m_channelsReadMediaPeriod(0),
	m_tmpSessions(0),
	m_pinnedDialogsCountMax(0),
	m_callReceiveTimeoutMs(0),
	m_callRingTimeoutMs(0),
	m_callConnectTimeoutMs(0),
	m_callPacketTimeoutMs(0),
	m_langPackVersion(0)
{
}

TLConfig::~TLConfig()
{
}

HRESULT TLConfig::RuntimeClassInitialize(bool testMode)
{
	m_testMode = testMode;
	m_callConnectTimeoutMs = 30000;
	m_callPacketTimeoutMs = 10000;
	m_callReceiveTimeoutMs = 20000;
	m_callRingTimeoutMs = 90000;
	m_channelsReadMediaPeriod = 604800;
	m_chatBigSize = 10;
	m_chatSizeMax = 200;
	m_editTimeLimit = 172800;
	m_flags = TLConfigFlag::PhoneCallsEnabled | TLConfigFlag::LangPackVersion;
	m_forwardedCountMax = 100;
	m_megaGroupSizeMax = 30000;
	m_notifyCloudDelayMs = 30000;
	m_notifyDefaultDelayMs = 1500;
	m_offlineBlurTimeoutMs = 5000;
	m_offlineIdleTimeoutMs = 30000;
	m_onlineCloudTimeoutMs = 300000;
	m_onlineUpdatePeriodMs = 120000;
	m_pinnedDialogsCountMax = 5;
	m_pushChatLimit = 2;
	m_pushChatPeriodMs = 60000;
	m_ratingEDecay = 2419200;
	m_savedGifsLimit = 200;
	m_stickersFavedLimit = 5;
	m_stickersRecentLimit = 30;
	m_thisDc = 4;

	return m_meUrlPrefix.Set(L"https://t.me/");
}

HRESULT TLConfig::get_Flags(TLConfigFlag* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_flags;
	return S_OK;
}

HRESULT TLConfig::get_Date(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_date;
	return S_OK;
}

HRESULT TLConfig::get_Expires(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_expires;
	return S_OK;
}

HRESULT TLConfig::get_TestMode(boolean* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_testMode;
	return S_OK;
}

HRESULT TLConfig::get_ThisDc(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_thisDc;
	return S_OK;
}

HRESULT TLConfig::get_DCOptions(__FIVectorView_1_Telegram__CApi__CNative__CTL__CTLDCOption** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto vectorView = Make<VectorView<ABI::Telegram::Api::Native::TL::TLDCOption*>>();

	std::transform(m_dcOptions.begin(), m_dcOptions.end(), std::back_inserter(vectorView->GetItems()), [](auto& ptr)
	{
		return static_cast<ITLDCOption*>(ptr.Get());
	});

	*value = vectorView.Detach();
	return S_OK;
}

HRESULT TLConfig::get_ChatSizeMax(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_chatSizeMax;
	return S_OK;
}

HRESULT TLConfig::get_MegaGroupSizeMax(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_megaGroupSizeMax;
	return S_OK;
}

HRESULT TLConfig::get_ForwardedCountMax(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_forwardedCountMax;
	return S_OK;
}

HRESULT TLConfig::get_OnlineUpdatePeriodMs(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_onlineUpdatePeriodMs;
	return S_OK;
}

HRESULT TLConfig::get_OfflineBlurTimeoutMs(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_offlineBlurTimeoutMs;
	return S_OK;
}

HRESULT TLConfig::get_OfflineIdleTimeoutMs(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_offlineIdleTimeoutMs;
	return S_OK;
}

HRESULT TLConfig::get_OnlineCloudTimeoutMs(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_onlineCloudTimeoutMs;
	return S_OK;
}

HRESULT TLConfig::get_NotifyCloudDelayMs(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_notifyCloudDelayMs;
	return S_OK;
}

HRESULT TLConfig::get_NotifyDefaultDelayMs(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_notifyDefaultDelayMs;
	return S_OK;
}

HRESULT TLConfig::get_ChatBigSize(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_chatBigSize;
	return S_OK;
}

HRESULT TLConfig::get_PushChatPeriodMs(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_pushChatPeriodMs;
	return S_OK;
}

HRESULT TLConfig::get_PushChatLimit(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_pushChatLimit;
	return S_OK;
}

HRESULT TLConfig::get_SavedGifsLimit(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_savedGifsLimit;
	return S_OK;
}

HRESULT TLConfig::get_EditTimeLimit(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_editTimeLimit;
	return S_OK;
}

HRESULT TLConfig::get_RatingEDecay(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_ratingEDecay;
	return S_OK;
}

HRESULT TLConfig::get_StickersRecentLimit(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_stickersRecentLimit;
	return S_OK;
}

HRESULT TLConfig::get_StickersFavedLimit(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_stickersFavedLimit;
	return S_OK;
}

HRESULT TLConfig::get_ChannelsReadMediaPeriod(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_channelsReadMediaPeriod;
	return S_OK;
}

HRESULT TLConfig::get_TmpSessions(__FIReference_1_int** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if ((m_flags & TLConfigFlag::TmpSessions) == TLConfigFlag::TmpSessions)
	{
		*value = Make<Windows::Foundation::Reference<INT32>>(m_tmpSessions).Detach();
	}
	else
	{
		*value = nullptr;
	}

	return S_OK;
}

HRESULT TLConfig::get_PinnedDialogsCountMax(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_pinnedDialogsCountMax;
	return S_OK;
}

HRESULT TLConfig::get_CallReceiveTimeoutMs(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_callReceiveTimeoutMs;
	return S_OK;
}

HRESULT TLConfig::get_CallRingTimeoutMs(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_callRingTimeoutMs;
	return S_OK;
}

HRESULT TLConfig::get_CallConnectTimeoutMs(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_callConnectTimeoutMs;
	return S_OK;
}

HRESULT TLConfig::get_CallPacketTimeoutMs(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_callPacketTimeoutMs;
	return S_OK;
}

HRESULT TLConfig::get_MeUrlPrefix(HSTRING* value)
{
	return m_meUrlPrefix.CopyTo(value);
}

HRESULT TLConfig::get_SuggestedLangCode(HSTRING* value)
{
	return m_suggestedLangCode.CopyTo(value);
}

HRESULT TLConfig::get_LangPackVersion(__FIReference_1_int** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if ((m_flags & TLConfigFlag::SuggestedLangCode) == TLConfigFlag::SuggestedLangCode)
	{
		*value = Make<Windows::Foundation::Reference<INT32>>(m_langPackVersion).Detach();
	}
	else
	{
		*value = nullptr;
	}

	return S_OK;
}

HRESULT TLConfig::get_DisabledFeatures(__FIVectorView_1_Telegram__CApi__CNative__CTL__CTLDisabledFeature** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto vectorView = Make<VectorView<ABI::Telegram::Api::Native::TL::TLDisabledFeature*>>();

	std::transform(m_disabledFeatures.begin(), m_disabledFeatures.end(), std::back_inserter(vectorView->GetItems()), [](auto& ptr)
	{
		return static_cast<ITLDisabledFeature*>(ptr.Get());
	});

	*value = vectorView.Detach();
	return S_OK;
}

HRESULT TLConfig::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(reinterpret_cast<INT32*>(&m_flags)));
	ReturnIfFailed(result, reader->ReadInt32(&m_date));
	ReturnIfFailed(result, reader->ReadInt32(&m_expires));
	ReturnIfFailed(result, reader->ReadBoolean(&m_testMode));
	ReturnIfFailed(result, reader->ReadInt32(&m_thisDc));
	ReturnIfFailed(result, ReadTLObjectVector<TLDCOption>(reader, m_dcOptions));
	ReturnIfFailed(result, reader->ReadInt32(&m_chatSizeMax));
	ReturnIfFailed(result, reader->ReadInt32(&m_megaGroupSizeMax));
	ReturnIfFailed(result, reader->ReadInt32(&m_forwardedCountMax));
	ReturnIfFailed(result, reader->ReadInt32(&m_onlineUpdatePeriodMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_offlineBlurTimeoutMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_offlineIdleTimeoutMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_onlineCloudTimeoutMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_notifyCloudDelayMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_notifyDefaultDelayMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_chatBigSize));
	ReturnIfFailed(result, reader->ReadInt32(&m_pushChatPeriodMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_pushChatLimit));
	ReturnIfFailed(result, reader->ReadInt32(&m_savedGifsLimit));
	ReturnIfFailed(result, reader->ReadInt32(&m_editTimeLimit));
	ReturnIfFailed(result, reader->ReadInt32(&m_ratingEDecay));
	ReturnIfFailed(result, reader->ReadInt32(&m_stickersRecentLimit));
	ReturnIfFailed(result, reader->ReadInt32(&m_stickersFavedLimit));
	ReturnIfFailed(result, reader->ReadInt32(&m_channelsReadMediaPeriod));

	if ((m_flags & TLConfigFlag::TmpSessions) == TLConfigFlag::TmpSessions)
	{
		ReturnIfFailed(result, reader->ReadInt32(&m_tmpSessions));
	}

	ReturnIfFailed(result, reader->ReadInt32(&m_pinnedDialogsCountMax));
	ReturnIfFailed(result, reader->ReadInt32(&m_callReceiveTimeoutMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_callRingTimeoutMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_callConnectTimeoutMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_callPacketTimeoutMs));
	ReturnIfFailed(result, reader->ReadString(m_meUrlPrefix.GetAddressOf()));

	if ((m_flags & TLConfigFlag::SuggestedLangCode) == TLConfigFlag::SuggestedLangCode)
	{
		ReturnIfFailed(result, reader->ReadString(m_suggestedLangCode.GetAddressOf()));
		ReturnIfFailed(result, reader->ReadInt32(&m_langPackVersion));
	}

	return ReadTLObjectVector<TLDisabledFeature>(reader, m_disabledFeatures);
}

HRESULT TLConfig::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(static_cast<INT32>(m_flags)));
	ReturnIfFailed(result, writer->WriteInt32(m_date));
	ReturnIfFailed(result, writer->WriteInt32(m_expires));
	ReturnIfFailed(result, writer->WriteBoolean(m_testMode));
	ReturnIfFailed(result, writer->WriteInt32(m_thisDc));
	ReturnIfFailed(result, WriteTLObjectVector<TLDCOption>(writer, m_dcOptions));
	ReturnIfFailed(result, writer->WriteInt32(m_chatSizeMax));
	ReturnIfFailed(result, writer->WriteInt32(m_megaGroupSizeMax));
	ReturnIfFailed(result, writer->WriteInt32(m_forwardedCountMax));
	ReturnIfFailed(result, writer->WriteInt32(m_onlineUpdatePeriodMs));
	ReturnIfFailed(result, writer->WriteInt32(m_offlineBlurTimeoutMs));
	ReturnIfFailed(result, writer->WriteInt32(m_offlineIdleTimeoutMs));
	ReturnIfFailed(result, writer->WriteInt32(m_onlineCloudTimeoutMs));
	ReturnIfFailed(result, writer->WriteInt32(m_notifyCloudDelayMs));
	ReturnIfFailed(result, writer->WriteInt32(m_notifyDefaultDelayMs));
	ReturnIfFailed(result, writer->WriteInt32(m_chatBigSize));
	ReturnIfFailed(result, writer->WriteInt32(m_pushChatPeriodMs));
	ReturnIfFailed(result, writer->WriteInt32(m_pushChatLimit));
	ReturnIfFailed(result, writer->WriteInt32(m_savedGifsLimit));
	ReturnIfFailed(result, writer->WriteInt32(m_editTimeLimit));
	ReturnIfFailed(result, writer->WriteInt32(m_ratingEDecay));
	ReturnIfFailed(result, writer->WriteInt32(m_stickersRecentLimit));
	ReturnIfFailed(result, writer->WriteInt32(m_stickersFavedLimit));
	ReturnIfFailed(result, writer->WriteInt32(m_channelsReadMediaPeriod));

	if ((m_flags & TLConfigFlag::TmpSessions) == TLConfigFlag::TmpSessions)
	{
		ReturnIfFailed(result, writer->WriteInt32(m_tmpSessions));
	}

	ReturnIfFailed(result, writer->WriteInt32(m_pinnedDialogsCountMax));
	ReturnIfFailed(result, writer->WriteInt32(m_callReceiveTimeoutMs));
	ReturnIfFailed(result, writer->WriteInt32(m_callRingTimeoutMs));
	ReturnIfFailed(result, writer->WriteInt32(m_callConnectTimeoutMs));
	ReturnIfFailed(result, writer->WriteInt32(m_callPacketTimeoutMs));
	ReturnIfFailed(result, writer->WriteString(m_meUrlPrefix.Get()));

	if ((m_flags & TLConfigFlag::SuggestedLangCode) == TLConfigFlag::SuggestedLangCode)
	{
		ReturnIfFailed(result, writer->WriteString(m_suggestedLangCode.Get()));
		ReturnIfFailed(result, writer->WriteInt32(m_langPackVersion));
	}

	return WriteTLObjectVector<TLDisabledFeature>(writer, m_disabledFeatures);
}


TLConfigSimple::TLConfigSimple() :
	m_date(0),
	m_expires(0),
	m_dcId(0)
{
}

TLConfigSimple::~TLConfigSimple()
{
}

HRESULT TLConfigSimple::get_Date(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_date;
	return S_OK;
}

HRESULT TLConfigSimple::get_Expires(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_expires;
	return S_OK;
}

HRESULT TLConfigSimple::get_DCId(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_dcId;
	return S_OK;
}

HRESULT TLConfigSimple::get_IpPortList(__FIVectorView_1_Telegram__CApi__CNative__CTL__CTLIpPort** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto vectorView = Make<VectorView<ABI::Telegram::Api::Native::TL::TLIpPort*>>();

	std::transform(m_ipPortList.begin(), m_ipPortList.end(), std::back_inserter(vectorView->GetItems()), [](auto& ptr)
	{
		return static_cast<ITLIpPort*>(ptr.Get());
	});

	*value = vectorView.Detach();
	return S_OK;
}

HRESULT TLConfigSimple::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_date));
	ReturnIfFailed(result, reader->ReadInt32(&m_expires));
	ReturnIfFailed(result, reader->ReadInt32(&m_dcId));

	UINT32 constructor;
	ReturnIfFailed(result, reader->ReadUInt32(&constructor));

	if (constructor != TLVECTOR_CONSTRUCTOR)
	{
		return E_FAIL;
	}

	UINT32 count;
	ReturnIfFailed(result, reader->ReadUInt32(&count));

	m_ipPortList.resize(count);

	for (UINT32 i = 0; i < count; i++)
	{
		m_ipPortList[i] = Make<TLIpPort>();

		ReturnIfFailed(result, m_ipPortList[i]->ReadBody(reader));
	}

	return S_OK;
}

HRESULT TLConfigSimple::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(m_date));
	ReturnIfFailed(result, writer->WriteInt32(m_expires));
	ReturnIfFailed(result, writer->WriteInt32(m_dcId));

	ReturnIfFailed(result, writer->WriteUInt32(TLVECTOR_CONSTRUCTOR));
	ReturnIfFailed(result, writer->WriteUInt32(static_cast<UINT32>(m_ipPortList.size())));

	for (size_t i = 0; i < m_ipPortList.size(); i++)
	{
		ReturnIfFailed(result, m_ipPortList[i]->WriteBody(writer));
	}

	return S_OK;
}


TLIpPort::TLIpPort() :
	m_ipv4(0),
	m_port(0)
{
}

TLIpPort::~TLIpPort()
{
}

HRESULT TLIpPort::get_Ipv4(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_ipv4;
	return S_OK;
}

HRESULT TLIpPort::get_Port(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_port;
	return S_OK;
}

HRESULT TLIpPort::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_ipv4));

	return reader->ReadInt32(&m_port);
}

HRESULT TLIpPort::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(m_ipv4));

	return writer->WriteInt32(m_port);
}


TLCDNPublicKey::TLCDNPublicKey() :
	m_datacenterId(0)
{
}

TLCDNPublicKey::~TLCDNPublicKey()
{
}

HRESULT TLCDNPublicKey::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_datacenterId));

	return reader->ReadString(m_publicKey.GetAddressOf());
}


HRESULT TLCDNConfig::ReadBody(ITLBinaryReaderEx* reader)
{
	return ReadTLObjectVector<TLCDNPublicKey>(reader, m_publicKeys);
}


template<typename TLObjectTraits>
HRESULT TLRPCErrorT<TLObjectTraits>::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_errorCode));

	return reader->ReadString(m_errorMessage.GetAddressOf());
}

template<typename TLObjectTraits>
HRESULT TLRPCErrorT<TLObjectTraits>::get_ErrorCode(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_errorCode;
	return S_OK;
}

template<typename TLObjectTraits>
HRESULT TLRPCErrorT<TLObjectTraits>::get_ErrorMessage(HSTRING* value)
{
	return m_errorMessage.CopyTo(value);
}


HRESULT TLRpcReqError::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, TLRPCErrorT::ReadBody(reader));

	return reader->ReadInt64(&m_queryId);
}


TLRpcResult::TLRpcResult() :
	m_requestMessageId(0)
{
}

TLRpcResult::~TLRpcResult()
{
}

HRESULT TLRpcResult::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	HRESULT result;
	ComPtr<ITLObject> query = GetQuery();
	ComPtr<ITLObjectWithQuery> objectWithQuery;
	if (SUCCEEDED(query.As(&objectWithQuery)))
	{
		ReturnIfFailed(result, objectWithQuery->get_Query(&query));
	}

	return TLObject::CompleteRequest(m_requestMessageId, messageContext, query.Get(), connection);

	/*ReturnIfFailed(result, TLObject::CompleteRequest(m_requestMessageId, messageContext, query.Get(), connectionManager, connection));

	ComPtr<IMessageResponseHandler> responseHandler;
	if (SUCCEEDED(query.As(&responseHandler)))
	{
		return responseHandler->HandleResponse(messageContext, connectionManager, connection);
	}
	else
	{
		return S_OK;
	}*/
}

HRESULT TLRpcResult::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_requestMessageId));

	return TLObjectWithQuery::ReadQuery(reader);
}


TLRpcAnswerDropped::TLRpcAnswerDropped() :
	m_bytes(0),
	m_messageContext({})
{
}

TLRpcAnswerDropped::~TLRpcAnswerDropped()
{
}

HRESULT TLRpcAnswerDropped::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_messageContext.Id));
	ReturnIfFailed(result, reader->ReadUInt32(&m_messageContext.SequenceNumber));

	return reader->ReadInt32(&m_bytes);
}


HRESULT TLMsgsAck::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLMsgsAck response handling");

	return S_OK;
}

HRESULT TLMsgsAck::ReadBody(ITLBinaryReaderEx* reader)
{
	return ReadTLVector(reader, m_messagesIds);
}

HRESULT TLMsgsAck::WriteBody(ITLBinaryWriterEx* writer)
{
	return WriteTLVector(writer, m_messagesIds);
}


TLMessage::TLMessage() :
	m_messageContext({}),
	m_bodyLength(0)
{
}

TLMessage::~TLMessage()
{
}

HRESULT TLMessage::RuntimeClassInitialize(MessageContext const* messageContext, ITLObject* object)
{
	if (messageContext == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(object, &m_bodyLength));

	CopyMemory(&m_messageContext, messageContext, sizeof(MessageContext));

	return TLObjectWithQuery::RuntimeClassInitialize(object);
}

HRESULT TLMessage::RuntimeClassInitialize(INT64 messageId, UINT32 sequenceNumber, ITLObject* object)
{
	HRESULT result;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(object, &m_bodyLength));

	m_messageContext.Id = messageId;
	m_messageContext.SequenceNumber = sequenceNumber;

	return TLObjectWithQuery::RuntimeClassInitialize(object);
}

HRESULT TLMessage::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	return connection->HandleMessageResponse(&m_messageContext, GetQuery().Get());
}

HRESULT TLMessage::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_messageContext.Id));
	ReturnIfFailed(result, reader->ReadUInt32(&m_messageContext.SequenceNumber));
	ReturnIfFailed(result, reader->ReadUInt32(&m_bodyLength));

	UINT32 constructor;
	return reader->ReadObjectAndConstructor(m_bodyLength, &constructor, GetQuery().ReleaseAndGetAddressOf());
}

HRESULT TLMessage::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt64(m_messageContext.Id));
	ReturnIfFailed(result, writer->WriteUInt32(m_messageContext.SequenceNumber));
	ReturnIfFailed(result, writer->WriteUInt32(m_bodyLength));

	return writer->WriteObject(GetQuery().Get());
}


HRESULT TLMsgContainer::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	HRESULT result;
	for (size_t i = 0; i < m_messages.size(); i++)
	{
		ReturnIfFailed(result, m_messages[i]->HandleResponse(messageContext, connection));
	}

	return S_OK;
}

HRESULT TLMsgContainer::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	UINT32 count;
	ReturnIfFailed(result, reader->ReadUInt32(&count));

	m_messages.resize(count);

	for (UINT32 i = 0; i < count; i++)
	{
		m_messages[i] = Make<TLMessage>();
		ReturnIfFailed(result, m_messages[i]->ReadBody(reader));
	}

	return S_OK;
}

HRESULT TLMsgContainer::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteUInt32(static_cast<UINT32>(m_messages.size())));

	for (size_t i = 0; i < m_messages.size(); i++)
	{
		ReturnIfFailed(result, m_messages[i]->WriteBody(writer));
	}

	return S_OK;
}


HRESULT TLMsgCopy::RuntimeClassInitialize(TLMessage* message)
{
	if (message == nullptr)
	{
		return E_INVALIDARG;
	}

	m_message = message;
	return S_OK;
}

HRESULT TLMsgCopy::ReadBody(ITLBinaryReaderEx* reader)
{
	m_message = Make<TLMessage>();

	return m_message->ReadBody(reader);
}

HRESULT TLMsgCopy::WriteBody(ITLBinaryWriterEx* writer)
{
	return m_message->WriteBody(writer);
}


HRESULT TLMsgsStateReq::ReadBody(ITLBinaryReaderEx* reader)
{
	return ReadTLVector<INT64>(reader, m_messagesIds);
}

HRESULT TLMsgsStateReq::WriteBody(ITLBinaryWriterEx* writer)
{
	return WriteTLVector<INT64>(writer, m_messagesIds);
}


HRESULT TLMsgResendReq::ReadBody(ITLBinaryReaderEx* reader)
{
	return ReadTLVector<INT64>(reader, m_messagesIds);
}

HRESULT TLMsgResendReq::WriteBody(ITLBinaryWriterEx* writer)
{
	return WriteTLVector<INT64>(writer, m_messagesIds);
}


template<typename TLObjectTraits>
HRESULT TLMsgDetailedInfoT<TLObjectTraits>::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_answerMessageId));
	ReturnIfFailed(result, reader->ReadInt32(&m_bytes));

	return reader->ReadInt32(&m_status);
}


TLMsgDetailedInfo::TLMsgDetailedInfo() :
	m_messageId(0)
{
}

TLMsgDetailedInfo::~TLMsgDetailedInfo()
{
}

HRESULT TLMsgDetailedInfo::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	return connection->OnMsgDetailedInfoResponse(this);
}

HRESULT TLMsgDetailedInfo::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_messageId));

	return TLMsgDetailedInfoT::ReadBody(reader);
}


HRESULT TLMsgNewDetailedInfo::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	return connection->OnMsgNewDetailedInfoResponse(this);
}


HRESULT TLMsgsAllInfo::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, ReadTLVector<INT64>(reader, m_messagesIds));

	return reader->ReadString(m_info.GetAddressOf());
}


HRESULT TLMsgsStateInfo::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	return connection->OnMsgsStateInfoResponse(this);
}

HRESULT TLMsgsStateInfo::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_messageId));

	return reader->ReadString(m_info.GetAddressOf());
}


HRESULT TLGZipPacked::RuntimeClassInitialize(ITLObject* object)
{
	if (object == nullptr)
	{
		return E_INVALIDARG;
	}

	HRESULT result;
	UINT32 objectSize;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(object, &objectSize));

	ComPtr<TLMemoryBinaryWriter> writer;
	ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryWriter>(&writer, objectSize));
	ReturnIfFailed(result, writer->WriteObject(object));

	return GZipCompressBuffer(writer->GetBuffer(), objectSize, &m_packedData);
}

HRESULT TLGZipPacked::RuntimeClassInitialize(NativeBuffer* packedData)
{
	if (packedData == nullptr)
	{
		return E_INVALIDARG;
	}

	m_packedData = packedData;
	return S_OK;
	//return GZipCompressBuffer(rawData->GetBuffer(), rawData->GetCapacity(), &m_packedData);
}

HRESULT TLGZipPacked::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	HRESULT result;
	ComPtr<ITLObject> query;
	ReturnIfFailed(result, get_Query(&query));

	return TLObject::HandleResponse(messageContext, query.Get(), connection);
}

HRESULT TLGZipPacked::get_Query(ITLObject** value)
{
	HRESULT result;
	ComPtr<TLMemoryBinaryReader> reader;
	ReturnIfFailed(result, MakeAndInitialize<TLMemoryBinaryReader>(&reader, m_packedData.Get()));

	UINT32 constructor;
	return reader->ReadObjectAndConstructor(m_packedData->GetCapacity(), &constructor, value);
}

HRESULT TLGZipPacked::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	BYTE const* buffer;
	UINT32 bufferLength;
	ReturnIfFailed(result, reader->ReadBuffer2(&buffer, &bufferLength));

	return GZipDecompressBuffer(buffer, bufferLength, &m_packedData);
}

HRESULT TLGZipPacked::WriteBody(ITLBinaryWriterEx* writer)
{
	return writer->WriteBuffer(m_packedData->GetBuffer(), m_packedData->GetCapacity());
}


TLAuthExportedAuthorization::TLAuthExportedAuthorization() :
	m_id(0)
{
}

TLAuthExportedAuthorization::~TLAuthExportedAuthorization()
{
}

HRESULT TLAuthExportedAuthorization::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_id));

	BYTE const* buffer;
	UINT32 bufferLength;
	ReturnIfFailed(result, reader->ReadBuffer2(&buffer, &bufferLength));
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&m_bytes, bufferLength));

	CopyMemory(m_bytes->GetBuffer(), buffer, bufferLength);
	return S_OK;
}


TLNewSessionCreated::TLNewSessionCreated() :
	m_firstMessageId(0),
	m_uniqueId(0),
	m_serverSalt(0)
{
}

TLNewSessionCreated::~TLNewSessionCreated()
{
}

HRESULT TLNewSessionCreated::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	return connection->OnNewSessionCreatedResponse(this);
}

HRESULT TLNewSessionCreated::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_firstMessageId));
	ReturnIfFailed(result, reader->ReadInt64(&m_uniqueId));

	return reader->ReadInt64(&m_serverSalt);
}


template<typename TLObjectTraits>
HRESULT TLBadMsgNotificationT<TLObjectTraits>::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_badMessageContext.Id));
	ReturnIfFailed(result, reader->ReadUInt32(&m_badMessageContext.SequenceNumber));

	return reader->ReadInt32(&m_errorCode);
}


HRESULT TLBadMessage::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	return connection->GetDatacenter()->OnBadMessageResponse(connection, messageContext->Id, this);
}


HRESULT TLBadServerSalt::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	return connection->GetDatacenter()->OnBadServerSaltResponse(connection, messageContext->Id, this);
}

HRESULT TLBadServerSalt::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, TLBadMsgNotificationT::ReadBody(reader));

	return reader->ReadInt64(&m_newServerSalt);
}


TLPong::TLPong() :
	m_messageId(0),
	m_pingId(0)
{
}

TLPong::~TLPong()
{
}

HRESULT TLPong::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	auto& datacenter = connection->GetDatacenter();
	auto& connectionManager = datacenter->GetConnectionManager();

	return connectionManager->OnDatacenterPongReceived(datacenter.Get(), m_pingId);

	//return TLObject::CompleteRequest(m_messageId, messageContext, this, connection);
}

HRESULT TLPong::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_messageId));

	return reader->ReadInt64(&m_pingId);
}


template<typename TLObjectTraits>
HRESULT TLDHGenT<TLObjectTraits>::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(m_nonce), m_nonce));
	ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(m_serverNonce), m_serverNonce));

	return reader->ReadRawBuffer(sizeof(m_newNonceHash), m_newNonceHash);
}


HRESULT TLDHGenOk::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	HRESULT result;
	auto datacenter = connection->GetDatacenter();
	if (FAILED(result = datacenter->OnHandshakeClientDHResponse(connection, this)))
	{
		return datacenter->OnHandshakeError(result);
	}

	return S_OK;
}


HRESULT TLDHGenFail::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	return connection->GetDatacenter()->OnHandshakeError(E_FAIL);
}


HRESULT TLDHGenRetry::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	return connection->GetDatacenter()->OnHandshakeError(E_FAIL);
}


template<typename TLObjectTraits>
HRESULT TLServerDHParamsT<TLObjectTraits>::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(m_nonce), m_nonce));

	return reader->ReadRawBuffer(sizeof(m_serverNonce), m_serverNonce);
}


HRESULT TLServerDHParamsFail::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	return connection->GetDatacenter()->OnHandshakeError(E_FAIL);
}

HRESULT TLServerDHParamsFail::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, TLServerDHParamsT::ReadBody(reader));

	return reader->ReadRawBuffer(sizeof(m_newNonceHash), m_newNonceHash);
}


HRESULT TLServerDHParamsOk::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	HRESULT result;
	auto datacenter = connection->GetDatacenter();
	if (FAILED(result = datacenter->OnHandshakeServerDHResponse(connection, this)))
	{
		return datacenter->OnHandshakeError(result);
	}

	return S_OK;
}

HRESULT TLServerDHParamsOk::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, TLServerDHParamsT::ReadBody(reader));

	BYTE const* buffer;
	UINT32 bufferLength;
	ReturnIfFailed(result, reader->ReadBuffer2(&buffer, &bufferLength));
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&m_encryptedData, bufferLength));

	CopyMemory(m_encryptedData->GetBuffer(), buffer, bufferLength);
	return S_OK;
}


TLResPQ::TLResPQ()
{
	ZeroMemory(m_nonce, sizeof(m_nonce));
	ZeroMemory(m_pq, sizeof(m_pq));
	ZeroMemory(m_serverNonce, sizeof(m_serverNonce));
}

TLResPQ::~TLResPQ()
{
}

HRESULT TLResPQ::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	HRESULT result;
	auto datacenter = connection->GetDatacenter();
	if (FAILED(result = datacenter->OnHandshakePQResponse(connection, this)))
	{
		return datacenter->OnHandshakeError(result);
	}

	return S_OK;
}

HRESULT TLResPQ::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(m_nonce), m_nonce));
	ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(m_serverNonce), m_serverNonce));
	ReturnIfFailed(result, reader->ReadBuffer(m_pq, sizeof(m_pq)));

	return ReadTLVector<INT64>(reader, m_serverPublicKeyFingerprints);
}


TLFutureSalts::TLFutureSalts() :
	m_requestMessageId(0),
	m_now(0)
{
}

TLFutureSalts::~TLFutureSalts()
{
}

HRESULT TLFutureSalts::HandleResponse(MessageContext const* messageContext, Connection* connection)
{
	return TLObject::CompleteRequest(m_requestMessageId, messageContext, this, connection);
}

HRESULT TLFutureSalts::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_requestMessageId));
	ReturnIfFailed(result, reader->ReadInt32(&m_now));

	UINT32 count;
	ReturnIfFailed(result, reader->ReadUInt32(&count));

	m_salts.resize(count);

	for (UINT32 i = 0; i < count; i++)
	{
		ReturnIfFailed(result, reader->ReadInt32(&m_salts[i].ValidSince));
		ReturnIfFailed(result, reader->ReadInt32(&m_salts[i].ValidUntil));
		ReturnIfFailed(result, reader->ReadInt64(&m_salts[i].Salt));
	}

	return S_OK;
}


TLFutureSalt::TLFutureSalt() :
	m_salt({})
{
}

TLFutureSalt::~TLFutureSalt()
{
}

HRESULT TLFutureSalt::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_salt.ValidSince));
	ReturnIfFailed(result, reader->ReadInt32(&m_salt.ValidUntil));

	return reader->ReadInt64(&m_salt.Salt);
}


HRESULT TLConfigStatics::get_Default(ITLConfig** value)
{
	return MakeAndInitialize<TLConfig>(value, false);
}