/*
 * basis_mp4.c — fragmented-MP4 demuxer for live streams (init segment then a
 * sequence of moof+mdat fragments) -> H.264/H.265 + AAC.
 *
 * Parses moov (track config: codec, timescale, avcC/hvcC -> Annex B extradata,
 * esds -> AAC ASC) and each moof (tfhd/tfdt/trun: per-sample sizes, durations,
 * composition offsets, base decode time), then slices the following mdat into
 * samples in trun order.
 *
 * Common-case assumptions (sufficient for VRCDN-style fMP4, flagged for iteration):
 *   - one video track + one audio track
 *   - one trun per traf, mdat samples contiguous in trun order
 *   - 4-byte NAL length (from avcC); version 0/1 boxes
 */

#include "basis_mp4.h"
#include "basis_bitstream.h"
#include "basis_http_provider.h"

#include <stdlib.h>
#include <string.h>

#define MP4_MAX_FRAGS 4   /* per-moof trun runs (audio + video, with headroom) */

typedef struct {
    int track_id;
    int is_video;
    basis_codec_t codec;
    int timescale;
    uint8_t extradata[2048];
    int extradata_len;
    int nal_len_size;
    uint8_t asc[16];
    int asc_len;
    int sr, ch, obj;
    int64_t next_dts; /* running, in track timescale */
    int announced;
} mp4_track_t;

/* One trun run within a moof: its track, where its samples sit (data-offset,
 * relative to the moof start), and the per-sample table. A moof carries one of
 * these per track (audio + video), all slicing the single following mdat. */
typedef struct {
    int       track_id;
    int       data_offset;
    int64_t   base_dts;
    int       default_dur;
    uint32_t* sizes;
    uint32_t* durs;
    int32_t*  ctos;
    int       count;
    int       cap;
} mp4_frag_t;

typedef enum segment_reference_type {
    INVALID = 0,
    FRAGMENT = 1,
	SEGMENT_INDEX = 2,
} segment_reference_type_t;

typedef struct {
    segment_reference_type_t type;
    uint64_t start_pts_us;
    uint64_t end_pts_us;
    uint64_t offset;
    uint64_t size;

} mp4_segment_index_reference_t;

typedef struct mp4_segment_index {
    int length;
    int actual_length;
    mp4_segment_index_reference_t* references;
} mp4_segment_index_t;

int extend_segment_index(mp4_segment_index_t* index, int len) {
    if (index->actual_length >= len) {
        return TRUE;
    }

    mp4_segment_index_reference_t* allocated;

    if (index->references == NULL) {
        allocated = malloc(sizeof(mp4_segment_index_reference_t) * len);
        if (allocated != NULL) {
            memset(allocated, 0, sizeof(mp4_segment_index_reference_t) * len);
        }
    }
    else
    {
        allocated = realloc(index->references, sizeof(mp4_segment_index_reference_t) * len);

        if (allocated != NULL) {
            memset(allocated + index->actual_length, 0, sizeof(mp4_segment_index_reference_t) * (len - index->actual_length));
        }
    }

    if (allocated == NULL) {
        return FALSE;
    }

    index->references = allocated;
    index->actual_length = len;

    return TRUE;
}

int insert_segment_index(mp4_segment_index_t* index, mp4_segment_index_reference_t* reference) {
    if (!extend_segment_index(index, index->length + 1)) {
        return FALSE;
    }

    index->references[index->length++] = *reference;

    return TRUE;
}

mp4_segment_index_reference_t* search_segment_index(mp4_segment_index_t* index, uint64_t pts_us) {
    mp4_segment_index_reference_t *segment_index_ref = NULL;

    for (int i = 0; i < index->length; i++) {
        mp4_segment_index_reference_t* ref = &index->references[i];

        if (ref->start_pts_us <= pts_us && pts_us <= ref->end_pts_us) {
            if (ref->type == FRAGMENT) {
                return segment_index_ref;
            }
            else if (ref->type == SEGMENT_INDEX)
            {
                segment_index_ref = ref;
            }
        }
    }

    return segment_index_ref;
}

typedef struct {
    basis_media_sink_t* sink;
    basis_read_fn read;
    void* ctx;
    mp4_track_t tracks[2];
    int ntracks;

    /* runs from the last moof (one per traf/trun), consumed against its mdat */
    mp4_frag_t frags[MP4_MAX_FRAGS];
    int nfrags;

    mp4_segment_index_t segment_index;
    int read_bytes;

    int seek_allowed;
    int seekable;
    volatile uint64_t* seek_request_us;
    uint64_t last_seek_request;

    int video_base_pts_submitted;
    int audio_base_pts_submitted;
} mp4_t;

static uint16_t rd16(const uint8_t* p) { return (uint16_t)(((uint16_t)p[0] << 8) | p[1]); }
static uint32_t rd32(const uint8_t* p) { return ((uint32_t)p[0]<<24)|((uint32_t)p[1]<<16)|((uint32_t)p[2]<<8)|p[3]; }
static uint64_t rd64(const uint8_t* p) { return ((uint64_t)rd32(p)<<32)|rd32(p+4); }

static int read_exact(mp4_t* m, uint8_t* buf, int n) {
    int got = 0;
    while (got < n) {
        if (!m->sink->is_running(m->sink->user)) return got;
        int r = m->read(m->ctx, buf + got, n - got);
        if (r <= 0) return got;
        got += r;
    }
    m->read_bytes += got;
    return got;
}

/* reads a top-level box: header + body into *out (caller frees). type is FOURCC. */
static int read_box(mp4_t* m, uint32_t* type, uint8_t** out, int64_t* out_len) {
    uint8_t hdr[8];
    if (read_exact(m, hdr, 8) != 8) return -1;
    int64_t size = rd32(hdr);
    *type = rd32(hdr + 4);
    int header = 8;
    if (size == 1) {
        uint8_t ext[8];
        if (read_exact(m, ext, 8) != 8) return -1;
        size = (int64_t)rd64(ext);
        header = 16;
    }
    if (size < header || size > 256LL * 1024 * 1024) return -1;
    int64_t body = size - header;
    uint8_t* buf = (uint8_t*)malloc((size_t)body ? (size_t)body : 1);
    if (!buf) return -1;
    if (read_exact(m, buf, (int)body) != (int)body) { free(buf); return -1; }
    *out = buf; *out_len = body;
    return 0;
}

static mp4_track_t* track_by_id(mp4_t* m, int id) {
    for (int i = 0; i < m->ntracks; ++i) if (m->tracks[i].track_id == id) return &m->tracks[i];
    return NULL;
}

/* ---- moov parsing ------------------------------------------------------- */

static void parse_stsd(mp4_track_t* t, const uint8_t* p, int len) {
    /* stsd: version/flags(4) entry_count(4) then sample entries */
    if (len < 8) return;
    int n = (int)rd32(p + 4);
    int off = 8;
    for (int e = 0; e < n && off + 8 <= len; ++e) {
        int esize = (int)rd32(p + off);
        uint32_t etype = rd32(p + off + 4);
        const uint8_t* ent = p + off;
        if (esize < 8 || off + esize > len) break;

        if (etype == 0x61766331 /*avc1*/ || etype == 0x68766331 /*hvc1*/ || etype == 0x68657631 /*hev1*/) {
            t->is_video = 1;
            t->codec = (etype == 0x61766331) ? BASIS_CODEC_H264 : BASIS_CODEC_H265;
            /* visual sample entry header is 78 bytes, then child boxes (avcC/hvcC) */
            int co = 8 + 78;
            while (co + 8 <= esize) {
                int csz = (int)rd32(ent + co);
                uint32_t ct = rd32(ent + co + 4);
                if (csz < 8 || co + csz > esize) break;
                if (ct == 0x61766343 /*avcC*/ || ct == 0x68766343 /*hvcC*/) {
                    int nls = 4;
                    int got = basis_avcc_extradata_to_annexb(ent + co + 8, csz - 8,
                                t->codec == BASIS_CODEC_H265, t->extradata, sizeof(t->extradata), &nls);
                    if (got > 0) { t->extradata_len = got; t->nal_len_size = nls; }
                }
                co += csz;
            }
        } else if (etype == 0x6d703461 /*mp4a*/) {
            t->is_video = 0;
            t->codec = BASIS_CODEC_AAC;
            /* audio sample entry header 28 bytes, then esds */
            t->ch = (ent[8 + 16] << 8) | ent[8 + 17];
            t->sr = (int)(rd32(ent + 8 + 24) >> 16);
            int co = 8 + 28;
            while (co + 8 <= esize) {
                int csz = (int)rd32(ent + co);
                uint32_t ct = rd32(ent + co + 4);
                if (csz < 8 || co + csz > esize) break;
                if (ct == 0x65736473 /*esds*/) {
                    /* find DecoderSpecificInfo (tag 0x05) inside esds */
                    const uint8_t* ep = ent + co + 12; int el = csz - 12;
                    for (int i = 0; i + 2 < el; ++i) {
                        if (ep[i] == 0x05) {
                            int j = i + 1, dlen = 0, b;
                            do { b = ep[j++]; dlen = (dlen << 7) | (b & 0x7F); } while ((b & 0x80) && j < el);
                            if (j + dlen <= el && dlen <= (int)sizeof(t->asc)) {
                                memcpy(t->asc, ep + j, dlen); t->asc_len = dlen;
                                int aot = (t->asc[0] >> 3) & 0x1F;
                                int sri = ((t->asc[0] & 7) << 1) | (t->asc[1] >> 7);
                                t->obj = aot;
                                if (!t->sr) t->sr = basis_aac_sample_rate_from_index(sri);
                                if (!t->ch) t->ch = basis_aac_channels_from_config((t->asc[1] >> 3) & 0xF);
                            }
                            break;
                        }
                    }
                }
                co += csz;
            }
        }
        off += esize;
    }
}

static void parse_box_tree(mp4_t* m, mp4_track_t* t, const uint8_t* p, int len);

static void parse_trak(mp4_t* m, const uint8_t* p, int len) {
    if (m->ntracks >= 2) return;
    mp4_track_t* t = &m->tracks[m->ntracks];
    memset(t, 0, sizeof(*t));
    t->nal_len_size = 4;
    t->timescale = 90000;
    parse_box_tree(m, t, p, len);
    if (t->codec != BASIS_CODEC_NONE) m->ntracks++;
}

static void parse_box_tree(mp4_t* m, mp4_track_t* t, const uint8_t* p, int len) {
    int off = 0;
    while (off + 8 <= len) {
        int sz = (int)rd32(p + off);
        uint32_t ty = rd32(p + off + 4);
        if (sz < 8 || off + sz > len) break;
        const uint8_t* body = p + off + 8;
        int blen = sz - 8;
        switch (ty) {
            case 0x7472616b: parse_trak(m, body, blen); break;          /* trak */
            case 0x6d646961: /* mdia */
            case 0x6d696e66: /* minf */
            case 0x7374626c: parse_box_tree(m, t, body, blen); break;    /* stbl */
            case 0x6d646864: /* mdhd: version(1) flags(3) ... timescale */
                if (t) { int ver = body[0]; t->track_id = t->track_id; int tsoff = ver == 1 ? 4 + 8 + 8 : 4 + 4 + 4; if (tsoff + 4 <= blen) t->timescale = (int)rd32(body + tsoff); }
                break;
            case 0x746b6864: /* tkhd: track id */
                if (t) { int ver = body[0]; int idoff = ver == 1 ? 4 + 8 + 8 : 4 + 4 + 4; if (idoff + 4 <= blen) t->track_id = (int)rd32(body + idoff); }
                break;
            case 0x73747364: if (t) parse_stsd(t, body, blen); break;    /* stsd */
            default: break;
        }
        off += sz;
    }
}

static void announce_tracks(mp4_t* m) {
    for (int i = 0; i < m->ntracks; ++i) {
        mp4_track_t* t = &m->tracks[i];
        if (t->announced) continue;
        if (t->is_video) {
            int w = 0, h = 0;
            if (t->codec == BASIS_CODEC_H264 && t->extradata_len) {
                int pos=0,no,nl;
                while ((pos=basis_annexb_next(t->extradata,t->extradata_len,pos,&no,&nl))>=0)
                    if (nl>0 && basis_h264_nal_type(t->extradata[no])==7){ basis_h264_sps_dimensions(t->extradata+no,nl,&w,&h); break; }
            }
            m->sink->on_video_format(m->sink->user, t->codec, t->extradata, t->extradata_len, w, h);
        } else {
            m->sink->on_audio_format(m->sink->user, BASIS_CODEC_AAC, t->sr ? t->sr : 48000, t->ch ? t->ch : 2,
                                     t->asc_len ? t->asc : NULL, t->asc_len);
        }
        t->announced = 1;
    }
}

/* ---- moof parsing ------------------------------------------------------- */

static int frag_reserve(mp4_frag_t* f, int n) {
    if (n <= f->cap) return 1;
    int nc = f->cap ? f->cap * 2 : 256;
    while (nc < n) nc *= 2;
    uint32_t* s = (uint32_t*)realloc(f->sizes, (size_t)nc * sizeof(uint32_t));
    if (s) f->sizes = s;
    uint32_t* d = (uint32_t*)realloc(f->durs, (size_t)nc * sizeof(uint32_t));
    if (d) f->durs = d;
    int32_t*  c = (int32_t*)realloc(f->ctos, (size_t)nc * sizeof(int32_t));
    if (c) f->ctos = c;
    if (!s || !d || !c) return 0;
    f->cap = nc;
    return 1;
}

/* Parse one traf into a fragment run per trun. tfhd/tfdt give the track and base
 * decode time; each trun gives a data-offset (relative to the moof) and samples. */
static void parse_traf(mp4_t* m, const uint8_t* p, int len) {
    int off = 0;
    int track_id = 0, default_dur = 0, default_size = 0;
    int64_t base_dts = 0;
    while (off + 8 <= len) {
        int sz = (int)rd32(p + off);
        uint32_t ty = rd32(p + off + 4);
        if (sz < 8 || off + sz > len) break;
        const uint8_t* b = p + off + 8;
        if (ty == 0x74666864) { /* tfhd */
            uint32_t flags = rd32(b) & 0xFFFFFF;
            int q = 4;
            track_id = (int)rd32(b + q); q += 4;
            if (flags & 0x000001) q += 8;  /* base-data-offset */
            if (flags & 0x000002) q += 4;  /* sample-description-index */
            if (flags & 0x000008) { default_dur = (int)rd32(b + q); q += 4; }
            if (flags & 0x000010) { default_size = (int)rd32(b + q); q += 4; }
        } else if (ty == 0x74666474) { /* tfdt */
            int ver = b[0];
            base_dts = ver == 1 ? (int64_t)rd64(b + 4) : (int64_t)rd32(b + 4);
        } else if (ty == 0x7472756e && m->nfrags < MP4_MAX_FRAGS) { /* trun */
            mp4_frag_t* f = &m->frags[m->nfrags];
            uint32_t flags = rd32(b) & 0xFFFFFF;
            int count = (int)rd32(b + 4);
            int q = 8;
            int data_offset = 0;
            if (flags & 0x000001) { data_offset = (int)rd32(b + q); q += 4; } /* data-offset */
            if (flags & 0x000004) q += 4; /* first-sample-flags */
            if (!frag_reserve(f, count)) { off += sz; continue; }
            for (int i = 0; i < count; ++i) {
                uint32_t dur = (uint32_t)default_dur, size = (uint32_t)default_size;
                int32_t cto = 0;
                if (flags & 0x000100) { dur = rd32(b + q); q += 4; }
                if (flags & 0x000200) { size = rd32(b + q); q += 4; }
                if (flags & 0x000400) { q += 4; }            /* sample flags */
                if (flags & 0x000800) { cto = (int32_t)rd32(b + q); q += 4; }
                f->sizes[i] = size; f->durs[i] = dur; f->ctos[i] = cto;
            }
            f->track_id = track_id;
            f->base_dts = base_dts;
            f->data_offset = data_offset;
            f->default_dur = default_dur;
            f->count = count;
            m->nfrags++;
        }
        off += sz;
    }
}

static void parse_moof(mp4_t* m, const uint8_t* p, int len) {
    m->nfrags = 0;
    int off = 0;
    while (off + 8 <= len) {
        int sz = (int)rd32(p + off);
        uint32_t ty = rd32(p + off + 4);
        if (sz < 8 || off + sz > len) break;
        if (ty == 0x74726166) parse_traf(m, p + off + 8, sz - 8); /* traf */
        off += sz;
    }
}

static void parse_sidx(mp4_t* m, const uint8_t* p, int len) {
    if (!m || !p || len < 24) return;

    int off;

    /* p starts at the FullBox payload (version/flags), not the box header. */
    if (p[0] > 1) return;
    /* sidx.reference_id = rd32(p + 4); */
    uint64_t timescale = rd32(p + 8);
    off = 12;

    uint64_t epts;
    uint64_t foff;
    if (p[0] == 0) {
        epts = rd32(p + off);
        foff = rd32(p + off + 4);
        off += 8;
    } else {
        if (len < 32) return;
        epts = rd64(p + off);
        foff = rd64(p + off + 8);
        off += 16;
    }

    /* reserved(16), reference_count(16), then 12 bytes per reference. */
    if (off > len - 4) return;

	int reference_count = rd16(p + off + 2);
    off += 4;

    if ((size_t)reference_count > (size_t)(len - off) / 12) return;

    uint64_t moff = (uint64_t)m->read_bytes;
    uint64_t mpts = epts;

    for (uint16_t i = 0; i < reference_count; i++, off += 12) {
        mp4_segment_index_reference_t ref;
        uint32_t reference = rd32(p + off);
        int reference_type = (reference & 0x80000000U) >> 31;
        int referenced_size = reference & 0x7FFFFFFFU;
        uint64_t duration = rd32(p + off + 4);
        uint32_t sap_metadata = rd32(p + off + 8);
        short starts_with_sap = (sap_metadata >> 31) & 1;
        short sap_type = (sap_metadata >> 28) & 0b111;
        uint64_t sap_delta_time = sap_metadata & 0x0FFFFFFFU;

        if (reference_type == 0 && starts_with_sap) {
            /* fragment */
            ref.type = FRAGMENT;
            ref.start_pts_us = (mpts) * 1000000 / timescale;
            ref.end_pts_us = (mpts + duration) * 1000000 / timescale;
            ref.offset = moff;
            ref.size = referenced_size;

            if (!insert_segment_index(&m->segment_index, &ref)) {
                break;
            }
        }
        else
        {
            /* segment index */
            /* TODO */
        }

        moff += referenced_size;
        mpts += duration;
    }

    /* Commit only after the complete box has passed all bounds checks. */
    m->seekable = 1;
}

static void consume_frag(mp4_t* m, const mp4_frag_t* f, const uint8_t* data, int len, int base_off) {
    mp4_track_t* t = track_by_id(m, f->track_id);
    if (!t) return;
    int ts = t->timescale > 0 ? t->timescale : 90000;
    int pos = f->data_offset - base_off;
    if (pos < 0) return;
    int64_t dts = f->base_dts;

    for (int i = 0; i < f->count; ++i) {
        int ssize = (int)f->sizes[i];
        if (ssize <= 0 || pos + ssize > len) break;
        int64_t pts_us = (dts + f->ctos[i]) * 1000000 / ts;

        if (t->is_video) {
            uint8_t* out = (uint8_t*)malloc((size_t)ssize + 64);
            if (out) {
                int n = basis_avcc_to_annexb(data + pos, ssize, t->nal_len_size ? t->nal_len_size : 4, out, ssize + 64);
                if (n > 0) {
                    int key = t->codec == BASIS_CODEC_H265 ? basis_h265_is_keyframe(out, n) : basis_h264_is_keyframe(out, n);
                    m->sink->on_video_au(m->sink->user, out, n, pts_us, key, !m->video_base_pts_submitted);
                    m->video_base_pts_submitted = 1;
                }
                free(out);
            }
        } else {
            m->sink->on_audio_frame(m->sink->user, data + pos, ssize, pts_us, !m->audio_base_pts_submitted);
            m->audio_base_pts_submitted = 1;
        }

        if (m->last_seek_request != *m->seek_request_us) {
            return;
        }

        pos += ssize;
        dts += f->durs[i] ? f->durs[i] : f->default_dur;
    }
}

/* A moof's trafs share one mdat. Each trun's data-offset is relative to the moof
 * start; the smallest maps to the first byte of this mdat's payload, so subtract
 * it to place each run within the buffer we were handed. */
static void consume_mdat(mp4_t* m, const uint8_t* data, int len) {
    if (m->nfrags <= 0) return;
    int base_off = m->frags[0].data_offset;
    for (int k = 1; k < m->nfrags; ++k)
        if (m->frags[k].data_offset < base_off) base_off = m->frags[k].data_offset;

    for (int k = 0; k < m->nfrags; ++k)
        consume_frag(m, &m->frags[k], data, len, base_off);
}

static int try_get_segment_offset_for_pts(mp4_t* m, uint64_t pts, mp4_segment_index_reference_t** segment_offset) {
    /* O(n) */	
    for (int i = 0; i < m->segment_index.length; i++) {
        mp4_segment_index_reference_t *reference = &m->segment_index.references[i];

        if (reference->type == FRAGMENT && reference->start_pts_us <= pts && pts < reference->end_pts_us) {
            *segment_offset = reference;
            return TRUE;
        }
    }

    return FALSE;
}

int basis_mp4_run(basis_media_sink_t* sink, basis_http_provider_t* http, void* ctx, const char* url, volatile uint64_t* seek_request_us, int allow_seek) {
    mp4_t m; memset(&m, 0, sizeof(m));
    m.sink = sink; m.read = http->read; m.ctx = ctx; m.seek_request_us = seek_request_us; m.seek_allowed = allow_seek;

    while (sink->is_running(sink->user)) {
        uint32_t type; uint8_t* buf; int64_t blen;

        if (m.seek_allowed && m.seekable && m.seek_request_us != NULL && m.last_seek_request != *m.seek_request_us && url != NULL) {
            mp4_segment_index_reference_t* segment;

            if (!try_get_segment_offset_for_pts(&m, *m.seek_request_us, &segment)) {
                /* Failed to get segment */
                m.last_seek_request = *m.seek_request_us;
                continue;
            }

            http->close(m.ctx);
            m.ctx = NULL;
            /* TODO: Range Request */
            m.ctx = http->open_range_request(url, segment->offset);
            m.last_seek_request = *m.seek_request_us;
            m.video_base_pts_submitted = 0;
            m.audio_base_pts_submitted = 0;
            continue;
        }

        if (read_box(&m, &type, &buf, &blen) != 0) break;
        switch (type) {
            case 0x6d6f6f76: /* moov */
                parse_box_tree(&m, NULL, buf, (int)blen);
                announce_tracks(&m);
                break;
            case 0x6d6f6f66: /* moof */
                parse_moof(&m, buf, (int)blen);
                break;
            case 0x6d646174: /* mdat */
                consume_mdat(&m, buf, (int)blen);
                break;
            case 0x73696478: /* sidx */
                parse_sidx(&m, buf, (int)blen);
                break;
            default:
                break; /* ftyp, styp, free, ... ignored */
        }
        free(buf);
    }

    for (int k = 0; k < MP4_MAX_FRAGS; ++k) {
        free(m.frags[k].sizes); free(m.frags[k].durs); free(m.frags[k].ctos);
    }

    http->close(m.ctx);
    m.ctx = NULL;

    if (m.segment_index.references != NULL) {
        free(m.segment_index.references);
    }

    return 0;
}
