#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"
#include "TLObject.h"
#include "NativeBuffer.h"
#include "DatacenterServer.h"

#define MakeTLTypeTraits(objectTypeName, constructor, isLayerNeeded) MakeTLObjectTraits(objectTypeName, constructor, isLayerNeeded, "Telegram.Api.Native.TL")

using namespace Microsoft::WRL;
using ABI::Telegram::Api::Native::TL::ITLError;
using ABI::Telegram::Api::Native::TL::ITLDcOption;
using ABI::Telegram::Api::Native::TL::ITLDisabledFeature;
using ABI::Telegram::Api::Native::TL::ITLConfig;
using ABI::Telegram::Api::Native::TL::ITLErrorFactory;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			namespace TL
			{

				class TLError;
				class TLDcOption;
				class TLDisabledFeature;
				class TLConfig;
				class TLRpcError;
				class TLRpcReqError;
				class TLRpcResult;
				class TLRpcAnswerDropped;
				class TLRpcAnswerDroppedRunning;
				class TLRpcAnswerUnknown;
				class TLMsgsAck;
				class TLMessage;
				class TLMsgContainer;
				class TLMsgCopy;
				class TLMsgsStateReq;
				class TLMsgResendStateReq;
				class TLMsgDetailedInfo;
				class TLMsgNewDetailedInfo;
				class TLMsgsAllInfo;
				class TLGZipPacked;
				class TLAuthExportedAuthorization;
				class TLNewSessionCreated;
				class TLDestroySessionOk;
				class TLDestroySessionNone;
				class TLBadMessage;
				class TLBadServerSalt;
				class TLPong;
				class TLDHGenOk;
				class TLDHGenFail;
				class TLDHGenRetry;
				class TLServerDHParamsFail;
				class TLServerDHParamsOk;
				class TLResPQ;
				class TLFutureSalts;
				class TLFutureSalt;


				namespace TLObjectTraits
				{

					MakeTLTypeTraits(TLError, 0xc4b9f9bb, false);
					MakeTLTypeTraits(TLDcOption, 0x5d8c6cc, false);
					MakeTLTypeTraits(TLDisabledFeature, 0xae636f24, false);
					MakeTLTypeTraits(TLConfig, 0xcb601684, false);
					MakeTLTypeTraits(TLRpcError, 0x2144ca19, false);
					MakeTLTypeTraits(TLRpcReqError, 0x7ae432f5, false);
					MakeTLTypeTraits(TLRpcResult, 0xf35c6d01, false);
					MakeTLTypeTraits(TLRpcAnswerDropped, 0xa43ad8b7, false);
					MakeTLTypeTraits(TLRpcAnswerDroppedRunning, 0xcd78e586, false);
					MakeTLTypeTraits(TLRpcAnswerUnknown, 0x5e2ad36e, false);
					MakeTLTypeTraits(TLMsgsAck, 0x62d6b459, false);
					MakeTLTypeTraits(TLMessage, 0x5bb8e511, false);
					MakeTLTypeTraits(TLMsgContainer, 0x73f1f8dc, false);
					MakeTLTypeTraits(TLMsgCopy, 0xe06046b2, false);
					MakeTLTypeTraits(TLMsgsStateReq, 0xda69fb52, false);
					MakeTLTypeTraits(TLMsgResendStateReq, 0x7d861a08, false);
					MakeTLTypeTraits(TLMsgDetailedInfo, 0x276d3ec6, false);
					MakeTLTypeTraits(TLMsgNewDetailedInfo, 0x809db6df, false);
					MakeTLTypeTraits(TLMsgsAllInfo, 0x8cc0d131, false);
					MakeTLTypeTraits(TLGZipPacked, 0x3072cfa1, false);
					MakeTLTypeTraits(TLAuthExportedAuthorization, 0xdf969c2d, false);
					MakeTLTypeTraits(TLNewSessionCreated, 0x9ec20908, false);
					MakeTLTypeTraits(TLDestroySessionOk, 0xe22045fc, false);
					MakeTLTypeTraits(TLDestroySessionNone, 0x62d350c9, false);
					MakeTLTypeTraits(TLBadMessage, 0xa7eff811, false);
					MakeTLTypeTraits(TLBadServerSalt, 0xedab447b, false);
					MakeTLTypeTraits(TLPong, 0x347773c5, false);
					MakeTLTypeTraits(TLDHGenOk, 0x3bcbf734, false);
					MakeTLTypeTraits(TLDHGenFail, 0xa69dae02, false);
					MakeTLTypeTraits(TLDHGenRetry, 0x46dc1fb9, false);
					MakeTLTypeTraits(TLServerDHParamsFail, 0x79cb045d, false);
					MakeTLTypeTraits(TLServerDHParamsOk, 0xd0e8075c, false);
					MakeTLTypeTraits(TLResPQ, 0x05162463, false);
					MakeTLTypeTraits(TLFutureSalts, 0xae500895, false);
					MakeTLTypeTraits(TLFutureSalt, 0x0949d9dc, false);

				}


				class TLError WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLError, TLObjectT<TLObjectTraits::TLErrorTraits>>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLError, BaseTrust);

				public:
					TLError();
					~TLError();

					//COM exported methods
					IFACEMETHODIMP get_Code(_Out_ UINT32* value);
					IFACEMETHODIMP get_Text(_Out_ HSTRING* value);

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(INT32 code, _In_ HSTRING text);
					STDMETHODIMP RuntimeClassInitialize(HRESULT result);

					template<size_t sizeDest>
					STDMETHODIMP RuntimeClassInitialize(INT32 code, _In_ const WCHAR(&text)[sizeDest])
					{
						m_code = code;
						return m_text.Set(text);
					}

					inline INT32 GetCode() const
					{
						return m_code;
					}

					inline HString const& GetText() const
					{
						return m_text;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					INT32 m_code;
					HString m_text;
				};

				class TLDcOption WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLDcOption, TLObjectT<TLObjectTraits::TLDcOptionTraits>>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLDcOption, BaseTrust);

				public:
					TLDcOption();
					~TLDcOption();

					//COM exported methods
					STDMETHODIMP get_Flags(_Out_ INT32* value);
					STDMETHODIMP get_Id(_Out_ INT32* value);
					STDMETHODIMP get_IpAddress(_Out_ HSTRING* value);
					STDMETHODIMP get_Port(_Out_ INT32* value);

					//Internal methods
					inline INT32 GetFlags() const
					{
						return m_flags;
					}

					inline INT32 GetId() const
					{
						return m_id;
					}

					inline HString const& GetIpAddress() const
					{
						return m_ipAddress;
					}

					inline INT32 GetPort() const
					{
						return m_port;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					INT32 m_flags;
					INT32 m_id;
					HString m_ipAddress;
					INT32 m_port;
				};

				class TLDisabledFeature WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLDisabledFeature, TLObjectT<TLObjectTraits::TLDisabledFeatureTraits>>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLDisabledFeature, BaseTrust);

				public:
					//COM exported methods
					STDMETHODIMP get_Feature(_Out_ HSTRING* value);
					STDMETHODIMP get_Description(_Out_ HSTRING* value);

					//Internal methods
					inline HString const& GetFeature() const
					{
						return m_feature;
					}

					inline HString const& GetDescription() const
					{
						return m_description;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					HString m_feature;
					HString m_description;
				};

				class TLConfig WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLConfig, TLObjectT<TLObjectTraits::TLConfigTraits>>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLConfig, BaseTrust);

				public:
					TLConfig();
					~TLConfig();

					//COM exported methods
					IFACEMETHODIMP get_Flags(_Out_ INT32* value);
					IFACEMETHODIMP get_Date(_Out_ INT32* value);
					IFACEMETHODIMP get_Expires(_Out_ INT32* value);
					IFACEMETHODIMP get_TestMode(_Out_ boolean* value);
					IFACEMETHODIMP get_ThisDc(_Out_ INT32* value);
					IFACEMETHODIMP get_DcOptions(_Out_ __FIVectorView_1_Telegram__CApi__CNative__CTL__CTLDcOption** value);
					IFACEMETHODIMP get_ChatSizeMax(_Out_ INT32* value);
					IFACEMETHODIMP get_MegagroupSizeMax(_Out_ INT32* value);
					IFACEMETHODIMP get_ForwardedCountMax(_Out_ INT32* value);
					IFACEMETHODIMP get_OnlineUpdatePeriodMs(_Out_ INT32* value);
					IFACEMETHODIMP get_OfflineBlurTimeoutMs(_Out_ INT32* value);
					IFACEMETHODIMP get_OfflineIdleTimeoutMs(_Out_ INT32* value);
					IFACEMETHODIMP get_OnlineCloudTimeoutMs(_Out_ INT32* value);
					IFACEMETHODIMP get_NotifyCloudDelayMs(_Out_ INT32* value);
					IFACEMETHODIMP get_NotifyDefaultDelayMs(_Out_ INT32* value);
					IFACEMETHODIMP get_ChatBigSize(_Out_ INT32* value);
					IFACEMETHODIMP get_PushChatPeriodMs(_Out_ INT32* value);
					IFACEMETHODIMP get_PushChatLimit(_Out_ INT32* value);
					IFACEMETHODIMP get_SavedGifsLimit(_Out_ INT32* value);
					IFACEMETHODIMP get_EditTimeLimit(_Out_ INT32* value);
					IFACEMETHODIMP get_RatingEDecay(_Out_ INT32* value);
					IFACEMETHODIMP get_StickersRecentLimit(_Out_ INT32* value);
					IFACEMETHODIMP get_TmpSessions(_Out_ __FIReference_1_int** value);
					IFACEMETHODIMP get_PinnedDalogsCountMax(_Out_ INT32* value);
					IFACEMETHODIMP get_CallReceiveTimeoutMs(_Out_ INT32* value);
					IFACEMETHODIMP get_CallRingTimeoutMs(_Out_ INT32* value);
					IFACEMETHODIMP get_CallConnectTimeoutMs(_Out_ INT32* value);
					IFACEMETHODIMP get_CallPacketTimeoutMs(_Out_ INT32* value);
					IFACEMETHODIMP get_MeUrlPrefix(_Out_ HSTRING* value);
					IFACEMETHODIMP get_DisabledFeatures(_Out_ __FIVectorView_1_Telegram__CApi__CNative__CTL__CTLDisabledFeature** value);

					inline INT32 GetFlags() const
					{
						return m_flags;
					}

					inline INT32 GetDate() const
					{
						return m_date;
					}

					inline INT32 GetExpires() const
					{
						return m_expires;
					}

					inline boolean GetTestMode() const
					{
						return m_testMode;
					}

					inline INT32 GetThisDc() const
					{
						return m_thisDc;
					}

					inline std::vector<ComPtr<TLDcOption>> const& GetDcOptions() const
					{
						return m_dcOptions;
					}

					inline INT32 GetChatSizeMax() const
					{
						return m_chatSizeMax;
					}

					inline INT32 GetMegagroupSizeMax() const
					{
						return m_megagroupSizeMax;
					}

					inline INT32 GetForwardedCountMax() const
					{
						return m_forwardedCountMax;
					}

					inline INT32 GetOnlineUpdatePeriodMs() const
					{
						return m_onlineUpdatePeriodMs;
					}

					inline INT32 GetOfflineBlurTimeoutMs() const
					{
						return m_offlineBlurTimeoutMs;
					}

					inline INT32 GetOfflineIdleTimeoutMs() const
					{
						return m_offlineIdleTimeoutMs;
					}

					inline INT32 GetOnlineCloudTimeoutMs() const
					{
						return m_onlineCloudTimeoutMs;
					}

					inline INT32 GetNotifyCloudDelayMs() const
					{
						return m_notifyCloudDelayMs;
					}

					inline INT32 GetNotifyDefaultDelayMs() const
					{
						return m_notifyDefaultDelayMs;
					}

					inline INT32 GetChatBigSize() const
					{
						return m_chatBigSize;
					}

					inline INT32 GetPushChatPeriodMs() const
					{
						return m_pushChatPeriodMs;
					}

					inline INT32 GetPushChatLimit() const
					{
						return m_pushChatLimit;
					}

					inline INT32 GetSavedGifsLimit() const
					{
						return m_savedGifsLimit;
					}

					inline INT32 GetEditTimeLimit() const
					{
						return m_editTimeLimit;
					}

					inline INT32 GetRatingEDecay() const
					{
						return m_ratingEDecay;
					}

					inline INT32 GetStickersRecentLimit() const
					{
						return m_stickersRecentLimit;
					}

					inline INT32 GetTmpSessions() const
					{
						return m_tmpSessions;
					}

					inline INT32 GetPinnedDalogsCountMax() const
					{
						return m_pinnedDalogsCountMax;
					}

					inline INT32 GetCallReceiveTimeoutMs() const
					{
						return m_callReceiveTimeoutMs;
					}

					inline INT32 GetCallRingTimeoutMs() const
					{
						return m_callRingTimeoutMs;
					}

					inline INT32 GetCallConnectTimeoutMs() const
					{
						return m_callConnectTimeoutMs;
					}

					inline INT32 GetCallPacketTimeoutMs() const
					{
						return m_callPacketTimeoutMs;
					}

					inline HString const& GetMeUrlPrefix() const
					{
						return m_meUrlPrefix;
					}

					inline std::vector<ComPtr<TLDisabledFeature>> const& GetDisabledFeatures() const
					{
						return m_disabledFeatures;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					INT32 m_flags;
					INT32 m_date;
					INT32 m_expires;
					boolean m_testMode;
					INT32 m_thisDc;
					std::vector<ComPtr<TLDcOption>> m_dcOptions;
					INT32 m_chatSizeMax;
					INT32 m_megagroupSizeMax;
					INT32 m_forwardedCountMax;
					INT32 m_onlineUpdatePeriodMs;
					INT32 m_offlineBlurTimeoutMs;
					INT32 m_offlineIdleTimeoutMs;
					INT32 m_onlineCloudTimeoutMs;
					INT32 m_notifyCloudDelayMs;
					INT32 m_notifyDefaultDelayMs;
					INT32 m_chatBigSize;
					INT32 m_pushChatPeriodMs;
					INT32 m_pushChatLimit;
					INT32 m_savedGifsLimit;
					INT32 m_editTimeLimit;
					INT32 m_ratingEDecay;
					INT32 m_stickersRecentLimit;
					INT32 m_tmpSessions;
					INT32 m_pinnedDalogsCountMax;
					INT32 m_callReceiveTimeoutMs;
					INT32 m_callRingTimeoutMs;
					INT32 m_callConnectTimeoutMs;
					INT32 m_callPacketTimeoutMs;
					HString m_meUrlPrefix;
					std::vector<ComPtr<TLDisabledFeature>> m_disabledFeatures;
				};

				template<typename TLObjectTraits>
				class TLRpcErrorT abstract : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits>, CloakedIid<IMessageResponseHandler>>
				{
					InspectableClass(TLObjectTraits::RuntimeClassName, BaseTrust);

				public:
					TLRpcErrorT() :
						m_code(0)
					{
					}

					~TLRpcErrorT()
					{
					}

					//Internal methods
					inline INT32 GetCode() const
					{
						return m_code;
					}

					inline HString const& GetText() const
					{
						return m_text;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					INT32 m_code;
					HString m_text;
				};

				class TLRpcError WrlSealed : public TLRpcErrorT<TLObjectTraits::TLRpcErrorTraits>
				{
				public:
					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);
				};

				class TLRpcReqError WrlSealed : public TLRpcErrorT<TLObjectTraits::TLRpcReqErrorTraits>
				{
				public:
					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					inline INT64 GetQueryId() const
					{
						return m_queryId;
					}

				protected:
					HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader);

				private:
					INT64 m_queryId;
				};

				class TLRpcResult WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLRpcResultTraits>, TLObjectWithQuery>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLRpcResult();
					~TLRpcResult();

					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					inline INT64 GetRequestMessageId() const
					{
						return m_requestMessageId;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					INT64 m_requestMessageId;
				};

				template<typename TLObjectTraits>
				class TLRpcDropAnswerT abstract : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits>>
				{
					InspectableClass(TLObjectTraits::RuntimeClassName, BaseTrust);
				};

				class TLRpcAnswerDropped WrlSealed : public TLRpcDropAnswerT<TLObjectTraits::TLRpcAnswerDroppedTraits>
				{
				public:
					TLRpcAnswerDropped();
					~TLRpcAnswerDropped();

					//Internal methods
					inline MessageContext const* GetMessageContext() const
					{
						return &m_messageContext;
					}

					inline INT32 GetBytes() const
					{
						return m_bytes;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					MessageContext m_messageContext;
					INT32 m_bytes;
				};

				class TLRpcAnswerDroppedRunning WrlSealed : public TLRpcDropAnswerT<TLObjectTraits::TLRpcAnswerDroppedRunningTraits>
				{
				};

				class TLRpcAnswerUnknown WrlSealed : public TLRpcDropAnswerT<TLObjectTraits::TLRpcAnswerUnknownTraits>
				{
				};

				class TLMsgsAck WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLMsgsAckTraits>, CloakedIid<IMessageResponseHandler>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					inline std::vector<INT64>& GetMessagesIds()
					{
						return m_messagesIds;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					std::vector<INT64> m_messagesIds;
				};

				class TLMessage WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLMessageTraits>, TLObjectWithQuery>
				{
					friend class TLMsgContainer;
					friend class TLMsgCopy;

					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLMessage();
					~TLMessage();

					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ MessageContext const* messageContext, _In_ ITLObject* object);
					STDMETHODIMP RuntimeClassInitialize(INT64 messageId, UINT32 sequenceNumber, _In_ ITLObject* query);

					inline MessageContext const* GetMessageContext() const
					{
						return &m_messageContext;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					MessageContext m_messageContext;
				};

				class TLMsgContainer WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLMsgContainerTraits>, CloakedIid<IMessageResponseHandler>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					inline std::vector<ComPtr<TLMessage>>& GetMessages()
					{
						return m_messages;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					std::vector<ComPtr<TLMessage>> m_messages;
				};

				class TLMsgCopy : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLMsgCopyTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ TLMessage* message);

					inline ComPtr<TLMessage>& GetMessage()
					{
						return m_message;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					ComPtr<TLMessage> m_message;
				};

				class TLMsgsStateReq WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLMsgsStateReqTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					inline std::vector<INT64>& GetMessagesIds()
					{
						return m_messagesIds;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					std::vector<INT64> m_messagesIds;
				};

				class TLMsgResendStateReq WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLMsgResendStateReqTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					inline std::vector<INT64>& GetMessagesIds()
					{
						return m_messagesIds;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					std::vector<INT64> m_messagesIds;
				};

				template<typename TLObjectTraits>
				class TLMsgDetailedInfoT abstract : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits>>
				{
					InspectableClass(TLObjectTraits::RuntimeClassName, BaseTrust);

				public:
					TLMsgDetailedInfoT() :
						m_answerMessageId(0),
						m_bytes(0),
						m_status(0)
					{
					}

					~TLMsgDetailedInfoT()
					{
					}

					//Internal methods
					inline INT64 GetAnswerMessageId() const
					{
						return m_answerMessageId;
					}

					inline INT32 GetBytes() const
					{
						return m_bytes;
					}

					inline INT32 GeStatus() const
					{
						return m_status;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					INT64 m_answerMessageId;
					INT32 m_bytes;
					INT32 m_status;
				};

				class TLMsgDetailedInfo WrlSealed : public TLMsgDetailedInfoT<TLObjectTraits::TLMsgDetailedInfoTraits>
				{
				public:
					TLMsgDetailedInfo();
					~TLMsgDetailedInfo();

					//Internal methods
					inline INT64 GetMessageId() const
					{
						return m_messageId;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					INT64 m_messageId;
				};

				class TLMsgNewDetailedInfo WrlSealed : public TLMsgDetailedInfoT<TLObjectTraits::TLMsgNewDetailedInfoTraits>
				{
				};

				class TLMsgsAllInfo WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLMsgsAllInfoTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					inline std::vector<INT64> const& GetMessages() const
					{
						return m_messagesIds;
					}

					inline HString const& GetInfo() const
					{
						return m_info;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					std::vector<INT64> m_messagesIds;
					HString m_info;
				};

				class TLGZipPacked WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLGZipPackedTraits>, CloakedIid<ITLObjectWithQuery>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//COM exported methods
					IFACEMETHODIMP get_Query(_Out_ ITLObject** value);
					//IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ ITLObject* object);
					STDMETHODIMP RuntimeClassInitialize(_In_ NativeBuffer* rawData);

					inline NativeBuffer* GetPackedData() const
					{
						return m_packedData.Get();
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					ComPtr<NativeBuffer> m_packedData;
				};

				class TLAuthExportedAuthorization WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLAuthExportedAuthorizationTraits>, CloakedIid<IMessageResponseHandler>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLAuthExportedAuthorization();
					~TLAuthExportedAuthorization();

					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					inline INT32 GetDatacenterId() const
					{
						return m_datacenterId;
					}

					inline NativeBuffer* GetBytes() const
					{
						return m_bytes.Get();
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					INT32 m_datacenterId;
					ComPtr<NativeBuffer> m_bytes;
				};

				class TLNewSessionCreated WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLNewSessionCreatedTraits>, CloakedIid<IMessageResponseHandler>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLNewSessionCreated();
					~TLNewSessionCreated();

					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					inline INT64 GetFirstMesssageId() const
					{
						return m_firstMesssageId;
					}

					inline INT64 GetUniqueId() const
					{
						return m_uniqueId;
					}

					inline INT64 GetServerSalt() const
					{
						return m_serverSalt;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					INT64 m_firstMesssageId;
					INT64 m_uniqueId;
					INT64 m_serverSalt;
				};

				template<typename TLObjectTraits>
				class TLDestroySessionT abstract : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits>>
				{
					InspectableClass(TLObjectTraits::RuntimeClassName, BaseTrust);
				};

				class TLDestroySessionOk WrlSealed : public TLDestroySessionT<TLObjectTraits::TLDestroySessionOkTraits>
				{
				};

				class TLDestroySessionNone WrlSealed : public TLDestroySessionT<TLObjectTraits::TLDestroySessionNoneTraits>
				{
				};

				template<typename TLObjectTraits>
				class TLBadMsgNotificationT abstract : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits>, CloakedIid<IMessageResponseHandler>>
				{
					InspectableClass(TLObjectTraits::RuntimeClassName, BaseTrust);

				public:
					TLBadMsgNotificationT() :
						m_badMessageContext({})
					{
					}

					~TLBadMsgNotificationT()
					{
					}

					//Internal methods
					inline MessageContext const* GetBadMessageContext() const
					{
						return &m_badMessageContext;
					}

					inline INT32 GetErrorCode() const
					{
						return m_errorCode;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					MessageContext m_badMessageContext;
					INT32 m_errorCode;
				};

				class TLBadMessage WrlSealed : public TLBadMsgNotificationT<TLObjectTraits::TLBadMessageTraits>
				{
				public:
					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);
				};

				class TLBadServerSalt WrlSealed : public TLBadMsgNotificationT<TLObjectTraits::TLBadServerSaltTraits>
				{
				public:
					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					inline INT64 GetNewServerSalt() const
					{
						return m_newServerSalt;
					}

				protected:
					HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader);

				private:
					INT64 m_newServerSalt;
				};

				class TLPong WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLPongTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLPong();
					~TLPong();

					//Internal methods
					inline INT64 GetMessageId()
					{
						return m_messageId;
					}

					inline INT64 GetPingId()
					{
						return m_pingId;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					INT64 m_messageId;
					INT64 m_pingId;
				};

				template<typename TLObjectTraits>
				class TLDHGenT abstract : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits>, CloakedIid<IMessageResponseHandler>>
				{
					InspectableClass(TLObjectTraits::RuntimeClassName, BaseTrust);

				public:
					TLDHGenT()
					{
						ZeroMemory(&m_nonce, sizeof(TLInt128));
						ZeroMemory(&m_serverNonce, sizeof(TLInt128));
						ZeroMemory(&m_newNonceHash, sizeof(TLInt128));
					}

					~TLDHGenT()
					{
					}

					//Internal methods
					inline TLInt128 const& GetNonce() const
					{
						return m_nonce;
					}

					inline TLInt128 const& GetServerNonce() const
					{
						return m_serverNonce;
					}

					inline TLInt128 const& GetNewNonceHash() const
					{
						return m_newNonceHash;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					BYTE m_nonce[16];
					BYTE m_serverNonce[16];
					BYTE m_newNonceHash[16];
				};

				class TLDHGenOk WrlSealed : public TLDHGenT<TLObjectTraits::TLDHGenOkTraits>
				{
				public:
					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);
				};

				class TLDHGenFail WrlSealed : public TLDHGenT<TLObjectTraits::TLDHGenFailTraits>
				{
				public:
					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);
				};

				class TLDHGenRetry WrlSealed : public TLDHGenT<TLObjectTraits::TLDHGenRetryTraits>
				{
				public:
					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);
				};

				template<typename TLObjectTraits>
				class TLServerDHParamsT abstract : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits>, CloakedIid<IMessageResponseHandler>>
				{
					InspectableClass(TLObjectTraits::RuntimeClassName, BaseTrust);

				public:
					TLServerDHParamsT()
					{
						ZeroMemory(&m_nonce, sizeof(TLInt128));
						ZeroMemory(&m_serverNonce, sizeof(TLInt128));
					}

					~TLServerDHParamsT()
					{
					}

					//Internal methods
					inline TLInt128 const& GetNonce() const
					{
						return m_nonce;
					}

					inline TLInt128 const& GetServerNonce() const
					{
						return m_serverNonce;
					}

				protected:
					HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader);

				private:
					BYTE m_nonce[16];
					BYTE m_serverNonce[16];
				};

				class TLServerDHParamsFail WrlSealed : public TLServerDHParamsT<TLObjectTraits::TLServerDHParamsFailTraits>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					inline TLInt128 const& GetNewNonceHash() const
					{
						return m_newNonceHash;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					BYTE m_newNonceHash[16];
				};

				class TLServerDHParamsOk WrlSealed : public TLServerDHParamsT<TLObjectTraits::TLServerDHParamsOkTraits>
				{
				public:
					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					inline NativeBuffer* GetEncryptedData() const
					{
						return m_encryptedData.Get();
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					ComPtr<NativeBuffer> m_encryptedData;
				};

				class TLResPQ WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLResPQTraits>, CloakedIid<IMessageResponseHandler>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLResPQ();
					~TLResPQ();

					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					inline TLInt128 const& GetNonce() const
					{
						return m_nonce;
					}

					inline TLInt128 const& GetServerNonce() const
					{
						return m_serverNonce;
					}

					inline TLInt64 const& GetPQ() const
					{
						return m_pq;
					}

					inline std::vector<INT64> const& GetServerPublicKeyFingerprints() const
					{
						return m_serverPublicKeyFingerprints;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* writer) override;

				private:
					BYTE m_nonce[16];
					BYTE m_serverNonce[16];
					BYTE m_pq[8];
					std::vector<INT64> m_serverPublicKeyFingerprints;
				};

				class TLFutureSalts WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLFutureSaltsTraits>, CloakedIid<IMessageResponseHandler>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLFutureSalts();
					~TLFutureSalts();

					//COM exported methods
					IFACEMETHODIMP HandleResponse(_In_ MessageContext const* messageContext, _In_::Telegram::Api::Native::ConnectionManager* connectionManager, _In_::Telegram::Api::Native::Connection* connection);

					//Internal methods
					inline INT64 GetRequestMessageId() const
					{
						return m_requestMessageId;
					}

					inline INT32 GetNow() const
					{
						return m_now;
					}

					inline std::vector<ServerSalt> const& GetSalts() const
					{
						return m_salts;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					INT64 m_requestMessageId;
					INT32 m_now;
					std::vector<ServerSalt> m_salts;
				};

				class TLFutureSalt WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLFutureSaltTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLFutureSalt();
					~TLFutureSalt();

					//Internal methods
					inline ServerSalt const& GetSalt() const
					{
						return m_salt;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					ServerSalt m_salt;
				};


				class TLErrorFactory WrlSealed : public AgileActivationFactory<ITLErrorFactory>
				{
					InspectableClassStatic(RuntimeClass_Telegram_Api_Native_TL_TLError, BaseTrust);

				public:
					//COM exported methods
					IFACEMETHODIMP CreateTLError(UINT32 code, _In_  HSTRING text, _Out_ ITLError** instance);
				};

			}
		}
	}
}