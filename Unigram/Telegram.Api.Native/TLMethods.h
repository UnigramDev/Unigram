#pragma once
#include <wrl.h>
#include "Telegram.Api.Native.h"
#include "TLObject.h"
#include "NativeBuffer.h"
#include "DatacenterServer.h"

#define MakeTLMethodTraits(objectTypeName, constructor) MakeTLObjectTraits(objectTypeName, constructor, "Telegram.Api.Native.TL.Methods")

using namespace Microsoft::WRL;

namespace Telegram
{
	namespace Api
	{
		namespace Native
		{

			class UserConfiguration;


			namespace TL
			{
				namespace Methods
				{

					class TLRpcDropAnswer;
					class TLAuthExportAuthorization;
					class TLAuthImportAuthorization;
					class TLDestroySession;
					class TLPing;
					class TLPingDelayDisconnect;
					class TLSetClientDHParams;
					class TLReqDHParams;
					class TLReqPQ;
					class TLGetFutureSalts;
					class TLInvokeAfterMsg;
					class TLInvokeWithLayer;
					class TLInitConnection;
					class TLHelpGetConfig;
					class TLHelpGetCDNConfig;
					class TLHelpNearestDC;


					namespace TLObjectTraits
					{

						MakeTLMethodTraits(TLRpcDropAnswer, 0x58e4a740);
						MakeTLMethodTraits(TLAuthExportAuthorization, 0xe5bfffcd);
						MakeTLMethodTraits(TLAuthImportAuthorization, 0xe3ef9613);
						MakeTLMethodTraits(TLDestroySession, 0xe7512126);
						MakeTLMethodTraits(TLPing, 0x7abe77ec);
						MakeTLMethodTraits(TLPingDelayDisconnect, 0xf3427b8c);
						MakeTLMethodTraits(TLSetClientDHParams, 0xf5045f1f);
						MakeTLMethodTraits(TLReqDHParams, 0xd712e4be);
						MakeTLMethodTraits(TLReqPQ, 0x60469778);
						MakeTLMethodTraits(TLGetFutureSalts, 0xb921bd04); 
						MakeTLMethodTraits(TLInvokeAfterMsg, 0xcb9f372d);
						MakeTLMethodTraits(TLInvokeWithLayer, 0xda9b0d0d);
						MakeTLMethodTraits(TLInitConnection, 0xc7481da6);
						MakeTLMethodTraits(TLHelpGetConfig, 0xc4f9186b);
						MakeTLMethodTraits(TLHelpGetCDNConfig, 0x52029342);
						MakeTLMethodTraits(TLHelpNearestDC, 0x1FB33026);

					}


					class TLRpcDropAnswer WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLRpcDropAnswerTraits>>
					{
						InspectableClass(Traits::RuntimeClassName, BaseTrust);

					public:
						TLRpcDropAnswer(INT64 requestMessageId);
						~TLRpcDropAnswer();

						//Internal methods
						inline INT64 GetRequestMessageId()
						{
							return m_requestMessageId;
						}

					protected:
						virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

					private:
						INT64 m_requestMessageId;
					};

					class TLAuthExportAuthorization WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLAuthExportAuthorizationTraits>>
					{
						InspectableClass(Traits::RuntimeClassName, BaseTrust);

					public:
						TLAuthExportAuthorization(INT32 datacenterId);
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

					class TLAuthImportAuthorization WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLAuthImportAuthorizationTraits>>
					{
						InspectableClass(Traits::RuntimeClassName, BaseTrust);

					public:
						//Internal methods
						STDMETHODIMP RuntimeClassInitialize(INT32 id, _In_ NativeBuffer* bytes);

						inline INT32 GetId() const
						{
							return m_id;
						}

						inline NativeBuffer* GetBytes() const
						{
							return m_bytes.Get();
						}

					protected:
						virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

					private:
						INT32 m_id;
						ComPtr<NativeBuffer> m_bytes;
					};

					class TLDestroySession WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLDestroySessionTraits>>
					{
						InspectableClass(Traits::RuntimeClassName, BaseTrust);

					public:
						TLDestroySession(INT64 sessionId);
						~TLDestroySession();

						//Internal methods
						inline INT64 GetSessionId() const
						{
							return m_sessionId;
						}

					protected:
						virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

					private:
						INT64 m_sessionId;
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

					class TLPingDelayDisconnect WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLPingDelayDisconnectTraits>>
					{
						InspectableClass(Traits::RuntimeClassName, BaseTrust);

					public:
						TLPingDelayDisconnect(INT64 pingId, INT32 disconnectDelay);
						~TLPingDelayDisconnect();

						//Internal methods
						inline INT64 GetPingId()
						{
							return m_pingId;
						}

						inline INT32 GetDisconnectDelay()
						{
							return m_disconnectDelay;
						}

					protected:
						virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

					private:
						INT64 m_pingId;
						INT32 m_disconnectDelay;
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

					class TLGetFutureSalts WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLGetFutureSaltsTraits>>
					{
						InspectableClass(Traits::RuntimeClassName, BaseTrust);

					public:
						TLGetFutureSalts(UINT32 count);
						~TLGetFutureSalts();

						//Internal methods
						inline UINT32 GetCount()
						{
							return m_count;
						}

					protected:
						virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

					private:
						UINT32 m_count;
					};

					class TLInvokeAfterMsg WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLInvokeAfterMsgTraits>, TLObjectWithQuery>
					{
						InspectableClass(Traits::RuntimeClassName, BaseTrust);

					public:
						//Internal methods
						STDMETHODIMP RuntimeClassInitialize(INT64 messageId, _In_ ITLObject* query);

						inline INT64 GetMessageId() const
						{
							return m_messageId;
						}

					protected:
						virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

					private:
						INT64 m_messageId;
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
						STDMETHODIMP RuntimeClassInitialize(_In_ UserConfiguration* userConfiguration, _In_ ITLObject* query);

					protected:
						virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

					private:
						ComPtr<UserConfiguration> m_userConfiguration;
					};

					class TLHelpGetConfig WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLHelpGetConfigTraits>>
					{
						InspectableClass(Traits::RuntimeClassName, BaseTrust);
					};

					class TLHelpGetCDNConfig WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLHelpGetCDNConfigTraits>>
					{
						InspectableClass(Traits::RuntimeClassName, BaseTrust);
					};

					class TLHelpNearestDC WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLHelpNearestDCTraits>>
					{
						InspectableClass(Traits::RuntimeClassName, BaseTrust);
					};

				}
			}
		}
	}
}