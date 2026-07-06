/* Fragmented-MP4 demuxer (ftyp/moov/moof/mdat) feeding H.264/H.265 + AAC into a
 * basis_media_sink. Pulls bytes through the supplied read callback. Targets the
 * live fMP4 profile (init segment + moof/mdat fragments). */
#ifndef BASIS_MP4_H
#define BASIS_MP4_H

#include "../basis_media_internal.h"
#include "basis_http_provider.h"

#ifdef __cplusplus
extern "C" {
#endif

int basis_mp4_run(basis_media_sink_t* sink, basis_http_provider_t* read, void* ctx, const char* url, volatile uint64_t* seek_request_us, int allow_seek);

#ifdef __cplusplus
}
#endif
#endif
