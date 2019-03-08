#pragma once
#include <inttypes.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include "IAvEffect.h"
#include "AvEffectDefinition.h"
#include <sstream>

extern "C"
{
#include "libavutil/channel_layout.h"
#include "libavutil/md5.h"
#include "libavutil/mem.h"
#include "libavutil/opt.h"
#include "libavutil/samplefmt.h"
#include "libavfilter/buffersink.h"
#include "libavfilter/buffersrc.h"
#include "libavfilter/avfilter.h"
#include "libswresample/swresample.h"
}

using namespace Windows::Foundation::Collections;
using namespace Windows::Media::Playback;
using namespace Windows::Foundation;
using namespace Platform;
using namespace Windows::Storage;



namespace Unigram
{
	namespace Native
	{
		namespace Streaming
		{
			ref class AudioFilter : public IAvEffect
			{
				const AVFilter  *AVSource;
				const AVFilter  *AVSink;
				const AVFilter  *aResampler;

				AVFilterContext *aResampler_ctx;
				AVFilterGraph	*graph;
				AVFilterContext *avSource_ctx, *avSink_ctx;

				AVCodecContext *inputCodecCtx;

				std::vector<const AVFilter*> AVFilters;
				std::vector<AVFilterContext*> AVFilterContexts;
				char channel_layout_name[256];
				long long inChannelLayout;
				int nb_channels;

				HRESULT init_filter_graph(IVectorView<AvEffectDefinition^>^ effects)
				{
					//init graph
					int error = 0;

					error = AllocGraph();
					if (error < 0)
						return E_FAIL;

					//alloc src and sink

					error = AlocSourceAndSync();
					if (error < 0)
						return E_FAIL;


					//dynamic graph
					AVFilters.push_back(AVSource);
					AVFilterContexts.push_back(avSource_ctx);

					for (unsigned int i = 0; i < effects->Size; i++)
					{
						auto effectDefinition = effects->GetAt(i);

						auto effectName = PlatformStringToChar(effectDefinition->FilterName);
						auto configString = PlatformStringToChar(effectDefinition->Configuration);
						auto c_effectName = effectName->c_str();
						auto c_configString = configString->c_str();

						AVFilterContext* ctx;
						const AVFilter* filter;

						filter = avfilter_get_by_name(c_effectName);
						ctx = avfilter_graph_alloc_filter(graph, filter, c_configString);
						if (!filter)
						{
							delete configString;
							delete effectName;
							return AVERROR_FILTER_NOT_FOUND;

						}
						if (avfilter_init_str(ctx, c_configString) < 0)
						{
							delete configString;
							delete effectName;
							return E_FAIL;
						}
						AVFilters.push_back(filter);
						AVFilterContexts.push_back(ctx);
						delete configString;
						delete effectName;

					}


					AVFilters.push_back(AVSink);
					AVFilterContexts.push_back(avSink_ctx);


					error = LinkGraph();
					return error;
				}

				std::string* PlatformStringToChar(String^ value)
				{
					std::wstring strW(value->Begin());
					std::string* strA = new std::string(strW.begin(), strW.end());

					return strA;
				}



				HRESULT AllocGraph()
				{
					if (graph)
						avfilter_graph_free(&this->graph);

					graph = avfilter_graph_alloc();

					if (graph)
						return S_OK;
					else return E_FAIL;
				}


				HRESULT AllocSource()
				{
					AVDictionary *options_dict = NULL;

					int err;

					/* Create the abuffer filter;
					* it will be used for feeding the data into the graph. */
					AVSource = avfilter_get_by_name("abuffer");
					if (!AVSource) {
						fprintf(stderr, "Could not find the abuffer filter.\n");
						return AVERROR_FILTER_NOT_FOUND;
					}

					avSource_ctx = avfilter_graph_alloc_filter(graph, AVSource, "avSource_ctx");
					if (!avSource_ctx) {
						fprintf(stderr, "Could not allocate the abuffer instance.\n");
						return AVERROR(ENOMEM);
					}
					/* Set the filter options through the AVOptions API. */
					av_opt_set(avSource_ctx, "channel_layout", channel_layout_name, AV_OPT_SEARCH_CHILDREN);
					av_opt_set(avSource_ctx, "sample_fmt", av_get_sample_fmt_name(inputCodecCtx->sample_fmt), AV_OPT_SEARCH_CHILDREN);



					AVRational relational;
					relational.den = 1;
					relational.den = inputCodecCtx->sample_rate;

					av_opt_set_q(avSource_ctx, "time_base", relational, AV_OPT_SEARCH_CHILDREN);
					av_opt_set_int(avSource_ctx, "sample_rate", inputCodecCtx->sample_rate, AV_OPT_SEARCH_CHILDREN);
					/* Now initialize the filter; we pass NULL options, since we have already
					* set all the options above. */
					err = avfilter_init_str(avSource_ctx, NULL);
					return err;
				}

				HRESULT AllocSink()
				{
					AVSink = avfilter_get_by_name("abuffersink");
					if (!AVSink) {
						fprintf(stderr, "Could not find the abuffersink filter.\n");
						return AVERROR_FILTER_NOT_FOUND;
					}

					avSink_ctx = avfilter_graph_alloc_filter(graph, AVSink, "sink");
					if (!avSink_ctx) {
						fprintf(stderr, "Could not allocate the abuffersink instance.\n");
						return AVERROR(ENOMEM);
					}

					/* This filter takes no options. */
					return avfilter_init_str(avSink_ctx, NULL);

				}

				/*exampler for creating an aresample filter. Not actually used*/
				HRESULT AllocResampler()
				{
					aResampler = avfilter_get_by_name("aresample");
					if (!aResampler) {
						fprintf(stderr, "Could not find the aresample filter.\n");
						return AVERROR_FILTER_NOT_FOUND;
					}

					aResampler_ctx = avfilter_graph_alloc_filter(graph, aResampler, "aResampler_ctx");
					if (!aResampler_ctx) {
						fprintf(stderr, "Could not allocate the aresample instance.\n");
						return AVERROR(ENOMEM);
					}

					std::stringstream resamplerConfigString;

					resamplerConfigString << "osf=" << av_get_sample_fmt_name(inputCodecCtx->sample_fmt) << ":";
					resamplerConfigString << "ocl=" << channel_layout_name << ":";
					resamplerConfigString << "osr=" << inputCodecCtx->sample_rate;


					auto configStringC = resamplerConfigString.str();
					auto configString = configStringC.c_str();
					auto err = avfilter_init_str(aResampler_ctx, configString);
					if (err < 0) {
						fprintf(stderr, "Could not initialize the aresample instance.\n");
						return err;
					}
					return 0;

				}

				///There are 2 mandatory filters: the source, the sink.
				HRESULT AlocSourceAndSync()
				{
					//AVFilterContext *abuffer_ctx;
					auto hr = AllocSource();
					if (SUCCEEDED(hr))
					{
						hr = AllocSink();

					}

					return hr;
				}

				HRESULT LinkGraph()
				{
					int err = 0;

					//link all except last item
					for (unsigned int i = 0; i < AVFilterContexts.size() - 1; i++)
					{
						if (err >= 0)
							err = avfilter_link(AVFilterContexts[i], 0, AVFilterContexts[i + 1], 0);
					}

					/* Configure the graph. */
					err = avfilter_graph_config(graph, NULL);
					if (err < 0) {
						return err;
					}
					return S_OK;
				}

			public:
				virtual ~AudioFilter()
				{
					avfilter_graph_free(&this->graph);

					AVFilters.clear();
					AVFilterContexts.clear();
				}


			internal:

				AudioFilter(AVCodecContext *m_inputCodecCtx, long long p_inChannelLayout, int p_nb_channels)
				{
					inChannelLayout = p_inChannelLayout;
					nb_channels = p_nb_channels;
					this->inputCodecCtx = m_inputCodecCtx;
				}

				HRESULT AllocResources(IVectorView<AvEffectDefinition^>^ effects)
				{
					av_get_channel_layout_string(channel_layout_name, sizeof(channel_layout_name), nb_channels, inChannelLayout);
					return init_filter_graph(effects);
				}






				HRESULT AddFrame(AVFrame *avFrame) override
				{
					return av_buffersrc_add_frame(avSource_ctx, avFrame);
				}

				HRESULT GetFrame(AVFrame *avFrame) override
				{
					auto hr = av_buffersink_get_frame(avSink_ctx, avFrame);

					return hr;
				}
			};
		}
	}
}