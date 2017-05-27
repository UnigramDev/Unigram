#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"
#include "TLObject.h"
#include "NativeBuffer.h"
#include "DatacenterServer.h"

using namespace Microsoft::WRL;
using ABI::Telegram::Api::Native::TL::ITLError;
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
				class TLRpcResult;
				class TLMsgsAck;
				class TLMessage;
				class TLMsgContainer;
				class TLGZipPacked;
				class TLAuthExportAuthorization;
				class TLAuthExportedAuthorization;
				class TLAuthImportAuthorization;
				class TLNewSessionCreated;
				class TLBadMessage;
				class TLBadServerSalt;
				class TLPing;
				class TLPong;
				class TLDHGenOk;
				class TLDHGenFail;
				class TLDHGenRetry;
				class TLSetClientDHParams;
				class TLReqDHParams;
				class TLServerDHParamsFail;
				class TLServerDHParamsOk;
				class TLReqPQ;
				class TLResPQ;
				class TLGetFutureSalts;
				class TLFutureSalts;
				class TLFutureSalt;
				class TLHelpGetConfig;
				class TLInvokeWithLayer;
				class TLInitConnection;

				typedef BYTE TLInt32[4];
				typedef BYTE TLInt64[8];
				typedef BYTE TLInt128[16];
				typedef BYTE TLInt256[32];


				namespace TLObjectTraits
				{

					MakeTLObjectTraits(TLError, 0xc4b9f9bb, false);
					MakeTLObjectTraits(TLRpcResult, 0xf35c6d01, false);
					MakeTLObjectTraits(TLMsgsAck, 0x62d6b459, false);
					MakeTLObjectTraits(TLMessage, 0x5bb8e511, false);
					MakeTLObjectTraits(TLMsgContainer, 0x73f1f8dc, false);
					MakeTLObjectTraits(TLGZipPacked, 0x3072cfa1, false);
					MakeTLObjectTraits(TLAuthExportAuthorization, 0xe5bfffcd, true);
					MakeTLObjectTraits(TLAuthExportedAuthorization, 0xdf969c2d, false);
					MakeTLObjectTraits(TLAuthImportAuthorization, 0xe3ef9613, true);
					MakeTLObjectTraits(TLNewSessionCreated, 0x9ec20908, false);
					MakeTLObjectTraits(TLBadMessage, 0xa7eff811, false);
					MakeTLObjectTraits(TLBadServerSalt, 0xedab447b, false);
					MakeTLObjectTraits(TLPing, 0x7abe77ec, false);
					MakeTLObjectTraits(TLPong, 0x347773c5, false);
					MakeTLObjectTraits(TLDHGenOk, 0x3bcbf734, false);
					MakeTLObjectTraits(TLDHGenFail, 0xa69dae02, false);
					MakeTLObjectTraits(TLDHGenRetry, 0x46dc1fb9, false);
					MakeTLObjectTraits(TLSetClientDHParams, 0xf5045f1f, false);
					MakeTLObjectTraits(TLReqDHParams, 0xd712e4be, false);
					MakeTLObjectTraits(TLServerDHParamsFail, 0x79cb045d, false);
					MakeTLObjectTraits(TLServerDHParamsOk, 0xd0e8075c, false);
					MakeTLObjectTraits(TLReqPQ, 0x60469778, false);
					MakeTLObjectTraits(TLResPQ, 0x05162463, false);
					MakeTLObjectTraits(TLGetFutureSalts, 0xb921bd04, false);
					MakeTLObjectTraits(TLFutureSalts, 0xae500895, false);
					MakeTLObjectTraits(TLFutureSalt, 0x0949d9dc, false);
					MakeTLObjectTraits(TLHelpGetConfig, 0xc4f9186b, false);
					MakeTLObjectTraits(TLInvokeWithLayer, 0xda9b0d0d, false);
					MakeTLObjectTraits(TLInitConnection, 0x69796de9, false);

				}


				class TLError WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, ITLError, TLObjectT<TLObjectTraits::TLErrorTraits>>
				{
					InspectableClass(RuntimeClass_Telegram_Api_Native_TL_TLError, BaseTrust);

				public:
					TLError();
					~TLError();

					//COM exported methods
					STDMETHODIMP get_Code(_Out_ UINT32* value);
					STDMETHODIMP get_Text(_Out_ HSTRING* value);

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(INT32 code, _In_ HSTRING text);

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

					inline HSTRING GetText() const
					{
						return m_text.Get();
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					INT32 m_code;
					HString m_text;
				};

				class TLRpcResult WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLRpcResultTraits>, TLObjectWithQuery>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLRpcResult();
					~TLRpcResult();

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

				class TLMsgsAck WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLMsgsAckTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					inline std::vector<INT64>& GetMsgIds()
					{
						return m_msgIds;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					std::vector<INT64> m_msgIds;
				};

				class TLMessage WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLMessageTraits>, TLObjectWithQuery>
				{
					friend class TLMsgContainer;

					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLMessage();
					~TLMessage();

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(INT64 messageId, UINT32 sequenceNumber, _In_ ITLObject* query);

					inline INT64 GetMessageId() const
					{
						return m_messageId;
					}

					inline UINT32 GetSequenceNumber() const
					{
						return m_sequenceNumber;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					INT64 m_messageId;
					UINT32 m_sequenceNumber;
				};

				class TLMsgContainer WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLMsgContainerTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
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

				class TLGZipPacked WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLGZipPackedTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
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

				class TLAuthExportAuthorization WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLAuthExportAuthorizationTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLAuthExportAuthorization(UINT32 datacenterId);
					~TLAuthExportAuthorization();

					//Internal methods
					inline INT32 GetDatacenterId() const
					{
						return m_datacenterId;
					}

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					INT32 m_datacenterId;
				};

				class TLAuthExportedAuthorization WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLAuthExportedAuthorizationTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLAuthExportedAuthorization();
					~TLAuthExportedAuthorization();

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

				class TLAuthImportAuthorization WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLAuthImportAuthorizationTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(INT32 datacenterId, _In_ NativeBuffer* bytes);

					inline INT32 GetDatacenterId() const
					{
						return m_datacenterId;
					}

					inline NativeBuffer* GetBytes() const
					{
						return m_bytes.Get();
					}

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					INT32 m_datacenterId;
					ComPtr<NativeBuffer> m_bytes;
				};

				class TLNewSessionCreated WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLNewSessionCreatedTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLNewSessionCreated();
					~TLNewSessionCreated();

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
				class TLBadMsgNotificationT  abstract : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits>>
				{
					InspectableClass(TLObjectTraits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					inline INT64 GetBadMessageId() const
					{
						return m_badMessageId;
					}

					inline UINT32 GetBadMessageSequenceNumber() const
					{
						return m_badMessageId;
					}

					inline INT32 GetErrorCode() const
					{
						return m_errorCode;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					INT64 m_badMessageId;
					UINT32 m_badMessageSequenceNumber;
					INT32 m_errorCode;

				};

				class TLBadMessage WrlSealed : public TLBadMsgNotificationT<TLObjectTraits::TLBadMessageTraits>
				{
				};

				class TLBadServerSalt WrlSealed : public TLBadMsgNotificationT<TLObjectTraits::TLBadServerSaltTraits>
				{
				public:
					inline INT64 GetNewServerSalt() const
					{
						return m_newServerSalt;
					}

				protected:
					HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader);

				private:
					INT64 m_newServerSalt;
				};

				class TLPing WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLPingTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLPing(INT64 pingId);
					~TLPing();

					//Internal methods
					inline INT64 GetPingId()
					{
						return m_pingId;
					}

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					INT64 m_pingId;
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
				class TLDHGenT abstract : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits>>
				{
					InspectableClass(TLObjectTraits::RuntimeClassName, BaseTrust);

				public:
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
				};

				class TLDHGenFail WrlSealed : public TLDHGenT<TLObjectTraits::TLDHGenFailTraits>
				{
				};

				class TLDHGenRetry WrlSealed : public TLDHGenT<TLObjectTraits::TLDHGenRetryTraits>
				{
				};

				class TLSetClientDHParams WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLSetClientDHParamsTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ TLInt128 nonce, _In_ TLInt128 serverNonce, UINT32 encryptedDataLength);

					inline TLInt128 const& GetNonce() const
					{
						return m_nonce;
					}

					inline TLInt128 const& GetServerNonce() const
					{
						return m_serverNonce;
					}

					inline NativeBuffer* GetEncryptedData() const
					{
						return m_encryptedData.Get();
					}

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					BYTE m_nonce[16];
					BYTE m_serverNonce[16];
					ComPtr<NativeBuffer> m_encryptedData;
				};

				class TLReqDHParams WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLReqDHParamsTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					struct TLReqDHParamsInnerData
					{
						BYTE const* PQ;
					};

					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ TLInt128 nonce, _In_ TLInt128 serverNonce, _In_ TLInt256 newNonce, UINT32 p, UINT32 q, INT64 publicKeyFingerprint, UINT32 encryptedDataLength);

					inline TLInt128 const& GetNonce() const
					{
						return m_nonce;
					}

					inline TLInt128 const& GetServerNonce() const
					{
						return m_serverNonce;
					}

					inline TLInt256 const& GetNewNonce() const
					{
						return m_newNonce;
					}

					inline TLInt32 const& GetP() const
					{
						return m_p;
					}

					inline TLInt32 const& GetQ() const
					{
						return m_q;
					}

					inline NativeBuffer* GetEncryptedData()
					{
						return m_encryptedData.Get();
					}

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					BYTE m_nonce[16];
					BYTE m_serverNonce[16];
					BYTE m_newNonce[32];
					BYTE m_p[4];
					BYTE m_q[4];
					INT64 m_publicKeyFingerprint;
					ComPtr<NativeBuffer> m_encryptedData;
				};

				class TLServerDHParams abstract
				{
				public:
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

				class TLServerDHParamsFail WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLServerDHParamsFailTraits>>, public TLServerDHParams
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
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

				class TLServerDHParamsOk WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLServerDHParamsOkTraits>>, public TLServerDHParams
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
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

				class TLReqPQ WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLReqPQTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ TLInt128 nonce);

					inline TLInt128 const& GetNonce() const
					{
						return m_nonce;
					}

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					BYTE m_nonce[16];
				};

				class TLResPQ WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLResPQTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLResPQ();
					~TLResPQ();

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

				class TLGetFutureSalts WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLGetFutureSaltsTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLGetFutureSalts(UINT32 count);
					~TLGetFutureSalts();

					//Internal methods
					//STDMETHODIMP RuntimeClassInitialize(UINT32 count);

					inline UINT32 GetCount()
					{
						return m_count;
					}

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					UINT32 m_count;
				};

				class TLFutureSalts  WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLFutureSaltsTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					TLFutureSalts();
					~TLFutureSalts();

					//Internal methods
					inline INT64 GetReqMessageId() const
					{
						return m_reqMessageId;
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
					INT64 m_reqMessageId;
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

				class TLInvokeWithLayer WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLInvokeWithLayerTraits>, TLObjectWithQuery>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ ITLObject* query);

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;
				};

				class TLInitConnection WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLInitConnectionTraits>, TLObjectWithQuery>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(_In_ IUserConfiguration* userConfiguration, _In_ ITLObject* query);

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					ComPtr<IUserConfiguration> m_userConfiguration;
				};

				class TLHelpGetConfig WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLHelpGetConfigTraits>, TLObjectWithQuery>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override
					{
						return S_OK;
					}
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