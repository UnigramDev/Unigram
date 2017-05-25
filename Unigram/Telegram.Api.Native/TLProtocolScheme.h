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
				class TLMsgsAck;
				class TLMsgContainer;
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
				class TLInvokeWithLayer;
				class TLInitConnection;

				typedef BYTE TLInt32[4];
				typedef BYTE TLInt64[8];
				typedef BYTE TLInt128[16];
				typedef BYTE TLInt256[32];


				namespace TLObjectTraits
				{

					MakeTLObjectTraits(TLError, 0xc4b9f9bb, false);
					MakeTLObjectTraits(TLMsgsAck, 0x62d6b459, false);
					MakeTLObjectTraits(TLMsgContainer, 0x73f1f8dc, false);
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

				class TLMsgContainer WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLMsgContainerTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					inline std::vector<ComPtr<ITLObject>>& GetMessages()
					{
						return m_messages;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override;

				private:
					std::vector<ComPtr<ITLObject>> m_messages;
				};

				class TLPing WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLPingTraits>>
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				public:
					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(INT64 pingId);

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
					inline INT64 GetMsgId()
					{
						return m_msgId;
					}

					inline INT64 GetPingId()
					{
						return m_pingId;
					}

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override;

				private:
					INT64 m_msgId;
					INT64 m_pingId;
				};

				class TLDHGen abstract
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

					inline TLInt128 const& GetNewNonceHash() const
					{
						return m_newNonceHash;
					}

				protected:
					HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader);

				private:
					BYTE m_nonce[16];
					BYTE m_serverNonce[16];
					BYTE m_newNonceHash[16];
				};

				class TLDHGenOk WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLDHGenOkTraits>>, public TLDHGen
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override
					{
						return TLDHGen::ReadBody(reader);
					}
				};

				class TLDHGenFail WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLDHGenFailTraits>>, public TLDHGen
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override
					{
						return TLDHGen::ReadBody(reader);
					}
				};

				class TLDHGenRetry WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObjectT<TLObjectTraits::TLDHGenRetryTraits>>, public TLDHGen
				{
					InspectableClass(Traits::RuntimeClassName, BaseTrust);

				protected:
					virtual HRESULT ReadBody(_In_ ITLBinaryReaderEx* reader) override
					{
						return TLDHGen::ReadBody(reader);
					}
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
					//Internal methods
					STDMETHODIMP RuntimeClassInitialize(UINT32 pingId);

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


				class TLFake WrlSealed : public RuntimeClass<RuntimeClassFlags<WinRtClassicComMix>, TLObject>
				{
					InspectableClass(L"Telegram.Api.Native.TL.TLFake", BaseTrust);

				public:
					//Internal methods
					IFACEMETHODIMP get_Constructor(_Out_ UINT32* value)
					{
						if (value == nullptr)
						{
							return E_POINTER;
						}

						*value = 0xDA9B0D0D;
						return S_OK;
					}

					IFACEMETHODIMP get_IsLayerNeeded(_Out_ boolean* value)
					{
						if (value == nullptr)
						{
							return E_POINTER;
						}

						*value = false;
						return S_OK;
					}

					inline INT64 GetPingId()
					{
						return m_pingId;
					}

				protected:
					virtual HRESULT WriteBody(_In_ ITLBinaryWriterEx* writer) override
					{
						writer->WriteInt32(66);
						writer->WriteUInt32(0xC4F9186B);
						return S_OK;
					}

				private:
					INT64 m_pingId;
				};


				class TLErrorFactory WrlSealed : public AgileActivationFactory<ITLErrorFactory>
				{
					InspectableClassStatic(RuntimeClass_Telegram_Api_Native_TL_TLError, BaseTrust);

				public:
					IFACEMETHODIMP CreateTLError(UINT32 code, _In_  HSTRING text, _Out_ ITLError** instance);
				};

			}
		}
	}
}