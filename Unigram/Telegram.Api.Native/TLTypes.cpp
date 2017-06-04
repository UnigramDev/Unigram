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


ActivatableClassWithFactory(TLError, TLErrorFactory);

RegisterTLObjectConstructor(TLError);
RegisterTLObjectConstructor(TLDcOption);
RegisterTLObjectConstructor(TLDisabledFeature);
RegisterTLObjectConstructor(TLConfig);
RegisterTLObjectConstructor(TLRpcError);
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
RegisterTLObjectConstructor(TLMsgResendStateReq);
RegisterTLObjectConstructor(TLMsgDetailedInfo);
RegisterTLObjectConstructor(TLMsgNewDetailedInfo);
RegisterTLObjectConstructor(TLMsgsAllInfo);
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


TLError::TLError() :
	m_code(0)
{
}

TLError::~TLError()
{
}

HRESULT TLError::RuntimeClassInitialize(INT32 code, HSTRING text)
{
	m_code = code;
	return m_text.Set(text);
}

HRESULT TLError::RuntimeClassInitialize(HRESULT result)
{
	m_code = result;

	WCHAR* text;
	UINT32 length;
	if ((length = FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS, nullptr,
		result, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), reinterpret_cast<LPWSTR>(&text), 0, nullptr)) == 0)
	{
		m_text.Set(L"Unknown error");
	}
	else
	{
		m_text.Set(text, length);
		LocalFree(text);
	}

	return S_OK;
}

HRESULT TLError::get_Code(UINT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_code;
	return S_OK;
}

HRESULT TLError::get_Text(HSTRING* value)
{
	return m_text.CopyTo(value);
}

HRESULT TLError::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_code));

	return reader->ReadString(m_text.GetAddressOf());
}

HRESULT TLError::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(m_code));

	return writer->WriteString(m_text.Get());
}


TLDcOption::TLDcOption() :
	m_flags(0),
	m_id(0),
	m_port(0)
{
}

TLDcOption::~TLDcOption()
{
}

HRESULT TLDcOption::get_Flags(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_flags;
	return S_OK;
}

HRESULT TLDcOption::get_Id(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_id;
	return S_OK;
}

HRESULT TLDcOption::get_IpAddress(HSTRING* value)
{
	return m_ipAddress.CopyTo(value);
}

HRESULT TLDcOption::get_Port(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_port;
	return S_OK;
}

HRESULT TLDcOption::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_flags));
	ReturnIfFailed(result, reader->ReadInt32(&m_id));
	ReturnIfFailed(result, reader->ReadString(m_ipAddress.GetAddressOf()));

	return reader->ReadInt32(&m_port);
}

HRESULT TLDcOption::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(m_flags));
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
	m_flags(0),
	m_date(0),
	m_expires(0),
	m_testMode(false),
	m_thisDc(0),
	m_chatSizeMax(0),
	m_megagroupSizeMax(0),
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
	m_tmpSessions(0),
	m_pinnedDalogsCountMax(0),
	m_callReceiveTimeoutMs(0),
	m_callRingTimeoutMs(0),
	m_callConnectTimeoutMs(0),
	m_callPacketTimeoutMs(0)
{
}

TLConfig::~TLConfig()
{
}

HRESULT TLConfig::get_Flags(INT32* value)
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

HRESULT TLConfig::get_DcOptions(__FIVectorView_1_Telegram__CApi__CNative__CTL__CTLDcOption** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	auto vectorView = Make<VectorView<ABI::Telegram::Api::Native::TL::TLDcOption*>>();

	std::transform(m_dcOptions.begin(), m_dcOptions.end(), std::back_inserter(vectorView->GetItems()), [](auto& ptr)
	{
		return static_cast<ITLDcOption*>(ptr.Get());
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

HRESULT TLConfig::get_MegagroupSizeMax(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_megagroupSizeMax;
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

HRESULT TLConfig::get_TmpSessions(__FIReference_1_int** value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	if (m_flags & 1)
	{
		*value = Make<Windows::Foundation::Reference<INT32>>(m_tmpSessions).Detach();
	}
	else
	{
		*value = nullptr;
	}

	return S_OK;
}

HRESULT TLConfig::get_PinnedDalogsCountMax(INT32* value)
{
	if (value == nullptr)
	{
		return E_POINTER;
	}

	*value = m_pinnedDalogsCountMax;
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
	ReturnIfFailed(result, reader->ReadInt32(&m_flags));
	ReturnIfFailed(result, reader->ReadInt32(&m_date));
	ReturnIfFailed(result, reader->ReadInt32(&m_expires));
	ReturnIfFailed(result, reader->ReadBool(&m_testMode));
	ReturnIfFailed(result, reader->ReadInt32(&m_thisDc));
	ReturnIfFailed(result, ReadTLObjectVector<TLDcOption>(reader, m_dcOptions));
	ReturnIfFailed(result, reader->ReadInt32(&m_chatSizeMax));
	ReturnIfFailed(result, reader->ReadInt32(&m_megagroupSizeMax));
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

	if (m_flags & 1)
	{
		ReturnIfFailed(result, reader->ReadInt32(&m_tmpSessions));
	}

	ReturnIfFailed(result, reader->ReadInt32(&m_pinnedDalogsCountMax));
	ReturnIfFailed(result, reader->ReadInt32(&m_callReceiveTimeoutMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_callRingTimeoutMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_callConnectTimeoutMs));
	ReturnIfFailed(result, reader->ReadInt32(&m_callPacketTimeoutMs));
	ReturnIfFailed(result, reader->ReadString(m_meUrlPrefix.GetAddressOf()));

	return ReadTLObjectVector<TLDisabledFeature>(reader, m_disabledFeatures);
}

HRESULT TLConfig::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt32(m_flags));
	ReturnIfFailed(result, writer->WriteInt32(m_date));
	ReturnIfFailed(result, writer->WriteInt32(m_expires));
	ReturnIfFailed(result, writer->WriteBool(m_testMode));
	ReturnIfFailed(result, writer->WriteInt32(m_thisDc));
	ReturnIfFailed(result, WriteTLObjectVector<TLDcOption>(writer, m_dcOptions));
	ReturnIfFailed(result, writer->WriteInt32(m_chatSizeMax));
	ReturnIfFailed(result, writer->WriteInt32(m_megagroupSizeMax));
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

	if (m_flags & 1)
	{
		ReturnIfFailed(result, writer->WriteInt32(m_tmpSessions));
	}

	ReturnIfFailed(result, writer->WriteInt32(m_pinnedDalogsCountMax));
	ReturnIfFailed(result, writer->WriteInt32(m_callReceiveTimeoutMs));
	ReturnIfFailed(result, writer->WriteInt32(m_callRingTimeoutMs));
	ReturnIfFailed(result, writer->WriteInt32(m_callConnectTimeoutMs));
	ReturnIfFailed(result, writer->WriteInt32(m_callPacketTimeoutMs));
	ReturnIfFailed(result, writer->WriteString(m_meUrlPrefix.Get()));

	return WriteTLObjectVector<TLDisabledFeature>(writer, m_disabledFeatures);
}


template<typename TLObjectTraits>
HRESULT TLRpcErrorT<TLObjectTraits>::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_code));

	return reader->ReadString(m_text.GetAddressOf());
}


HRESULT TLRpcError::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLRpcError response handling");

	return S_OK;
}


HRESULT TLRpcReqError::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLRpcReqError response handling");

	return S_OK;
}

HRESULT TLRpcReqError::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, TLRpcErrorT::ReadBody(reader));

	return reader->ReadInt64(&m_queryId);
}


TLRpcResult::TLRpcResult() :
	m_requestMessageId(0)
{
}

TLRpcResult::~TLRpcResult()
{
}

HRESULT TLRpcResult::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLRpcResult response handling");

	return S_OK;
}

HRESULT TLRpcResult::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_requestMessageId));

	return TLObjectWithQuery::ReadQuery(reader);
}


TLRpcAnswerDropped::TLRpcAnswerDropped() :
	m_bytes(0)
{
	ZeroMemory(&m_messageContext, sizeof(MessageContext));
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


HRESULT TLMsgsAck::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
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


TLMessage::TLMessage()
{
	ZeroMemory(&m_messageContext, sizeof(MessageContext));
}

TLMessage::~TLMessage()
{
}

HRESULT TLMessage::RuntimeClassInitialize(INT64 messageId, UINT32 sequenceNumber, ITLObject* object)
{
	m_messageContext.Id = messageId;
	m_messageContext.SequenceNumber = sequenceNumber;
	return TLObjectWithQuery::RuntimeClassInitialize(object);
}

HRESULT TLMessage::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	return TLObjectWithQuery::HandleResponse(&m_messageContext, connectionManager, connection);
}

HRESULT TLMessage::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_messageContext.Id));
	ReturnIfFailed(result, reader->ReadUInt32(&m_messageContext.SequenceNumber));

	UINT32 bodyLength;
	ReturnIfFailed(result, reader->ReadUInt32(&bodyLength));

	UINT32 constructor;
	return reader->ReadObjectAndConstructor(bodyLength, &constructor, GetQuery().ReleaseAndGetAddressOf());
}

HRESULT TLMessage::WriteBody(ITLBinaryWriterEx* writer)
{
	HRESULT result;
	ReturnIfFailed(result, writer->WriteInt64(m_messageContext.Id));
	ReturnIfFailed(result, writer->WriteUInt32(m_messageContext.SequenceNumber));

	UINT32 bodyLength;
	ReturnIfFailed(result, TLObjectSizeCalculator::GetSize(GetQuery().Get(), &bodyLength));
	ReturnIfFailed(result, writer->WriteUInt32(bodyLength));

	return TLObjectWithQuery::WriteQuery(writer);
}


HRESULT TLMsgContainer::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	HRESULT result;
	for (size_t i = 0; i < m_messages.size(); i++)
	{
		ReturnIfFailed(result, m_messages[i]->HandleResponse(messageContext, connectionManager, connection));
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


HRESULT TLMsgResendStateReq::ReadBody(ITLBinaryReaderEx* reader)
{
	return ReadTLVector<INT64>(reader, m_messagesIds);
}

HRESULT TLMsgResendStateReq::WriteBody(ITLBinaryWriterEx* writer)
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

HRESULT TLMsgDetailedInfo::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_messageId));

	return TLMsgDetailedInfoT::ReadBody(reader);
}


HRESULT TLMsgsAllInfo::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, ReadTLVector<INT64>(reader, m_messagesIds));

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

	ComPtr<TLBinaryWriter> writer;
	ReturnIfFailed(result, MakeAndInitialize<TLBinaryWriter>(&writer, objectSize));
	ReturnIfFailed(result, writer->WriteObject(object));

	return GZipCompressBuffer(writer->GetBuffer(), objectSize, &m_packedData);
}

HRESULT TLGZipPacked::RuntimeClassInitialize(NativeBuffer* rawData)
{
	if (rawData == nullptr)
	{
		return E_INVALIDARG;
	}

	return GZipCompressBuffer(rawData->GetBuffer(), rawData->GetCapacity(), &m_packedData);
}

HRESULT TLGZipPacked::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	HRESULT result;
	ComPtr<TLBinaryReader> reader;
	ReturnIfFailed(result, MakeAndInitialize<TLBinaryReader>(&reader, m_packedData.Get()));

	UINT32 constructor;
	ComPtr<ITLObject> query;
	ReturnIfFailed(result, reader->ReadObjectAndConstructor(m_packedData->GetCapacity(), &constructor, &query));

	return TLObject::HandleResponse(messageContext, query.Get(), connectionManager, connection);
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
	m_datacenterId(0)
{
}

TLAuthExportedAuthorization::~TLAuthExportedAuthorization()
{
}

HRESULT TLAuthExportedAuthorization::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	return S_OK;
}

HRESULT TLAuthExportedAuthorization::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt32(&m_datacenterId));

	BYTE const* buffer;
	UINT32 bufferLength;
	ReturnIfFailed(result, reader->ReadBuffer2(&buffer, &bufferLength));
	ReturnIfFailed(result, MakeAndInitialize<NativeBuffer>(&m_bytes, bufferLength));

	CopyMemory(m_bytes->GetBuffer(), buffer, bufferLength);
	return S_OK;
}


TLNewSessionCreated::TLNewSessionCreated() :
	m_firstMesssageId(0),
	m_uniqueId(0),
	m_serverSalt(0)
{
}

TLNewSessionCreated::~TLNewSessionCreated()
{
}

HRESULT TLNewSessionCreated::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	return connection->HandleNewSessionCreatedResponse(connectionManager, this);
}

HRESULT TLNewSessionCreated::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadInt64(&m_firstMesssageId));
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


HRESULT TLBadMessage::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLBadMessage response handling");

	return S_OK;
}


HRESULT TLBadServerSalt::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLBadServerSalt response handling");

	return S_OK;
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


HRESULT TLDHGenOk::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	HRESULT result;
	auto datacenter = connection->GetDatacenter();
	if (FAILED(result = datacenter->HandleHandshakeClientDHResponse(connectionManager, connection, this)))
	{
		return datacenter->HandleHandshakeError(result);
	}

	return S_OK;
}


HRESULT TLDHGenFail::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	return connection->GetDatacenter()->HandleHandshakeError(E_FAIL);
}


HRESULT TLDHGenRetry::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	return connection->GetDatacenter()->HandleHandshakeError(E_FAIL);
}


template<typename TLObjectTraits>
HRESULT TLServerDHParamsT<TLObjectTraits>::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, reader->ReadRawBuffer(sizeof(m_nonce), m_nonce));

	return reader->ReadRawBuffer(sizeof(m_serverNonce), m_serverNonce);
}


HRESULT TLServerDHParamsFail::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	return connection->GetDatacenter()->HandleHandshakeError(E_FAIL);
}

HRESULT TLServerDHParamsFail::ReadBody(ITLBinaryReaderEx* reader)
{
	HRESULT result;
	ReturnIfFailed(result, TLServerDHParamsT::ReadBody(reader));

	return reader->ReadRawBuffer(sizeof(m_newNonceHash), m_newNonceHash);
}


HRESULT TLServerDHParamsOk::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	HRESULT result;
	auto datacenter = connection->GetDatacenter();
	if (FAILED(result = datacenter->HandleHandshakeServerDHResponse(connection, this)))
	{
		return datacenter->HandleHandshakeError(result);
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

HRESULT TLResPQ::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	HRESULT result;
	auto datacenter = connection->GetDatacenter();
	if (FAILED(result = datacenter->HandleHandshakePQResponse(connection, this)))
	{
		return datacenter->HandleHandshakeError(result);
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

HRESULT TLFutureSalts::HandleResponse(MessageContext const* messageContext, ConnectionManager* connectionManager, Connection* connection)
{
	I_WANT_TO_DIE_IS_THE_NEW_TODO("Implement TLFutureSalts response handling");

	return S_OK;
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


HRESULT TLErrorFactory::CreateTLError(UINT32 code, HSTRING text, ITLError** instance)
{
	return MakeAndInitialize<TLError>(instance, code, text);
}