/* basis_http_provider - Generic bytestream abstraction for HTTP, HTTPS, etc.  */
#ifndef BASIS_HTTP_PROVIDER_H
#define BASIS_HTTP_PROVIDER_H

#include <stdint.h>
#include "../basis_media_internal.h"

#ifdef __cplusplus
extern "C" {
#endif

#if defined(_WIN32)
#include <windows.h>
#include "windows/basis_win_http.h"
    typedef HANDLE       basis_thread_t;
    typedef CRITICAL_SECTION basis_mutex_t;
#else
#include <pthread.h>
#include <unistd.h>
    typedef pthread_t        basis_thread_t;
    typedef pthread_mutex_t  basis_mutex_t;
#endif

#define BASIS_READAHEAD_CAP (16 * 1024 * 1024)

/* Pluggable HTTP(S) byte-source. Matches basis_win_http_open/read/close exactly:
 * open(url) returns a context that streams the response body; read is
 * basis_read_fn-compatible (bytes read, 0 on EOF, <0 on error); close frees it. */
typedef struct basis_http_provider {
    void* (*open)(const char* url);
    void* (*open_range_request)(const char* url, int start);
    int   (*read)(void* ctx, uint8_t* buf, int len);
    void  (*close)(void* ctx);
} basis_http_provider_t;

#ifdef __cplusplus
}
#endif

#endif
