use std::sync::RwLock;

use log::{Level, LevelFilter, Metadata, Record};

/// Receives one UTF-8 log message. The message pointer is only valid for the
/// duration of the callback.
pub type UnityDlpLogCallback = unsafe extern "C" fn(level: i32, message: *const u8, len: i32);

static CALLBACK: RwLock<Option<UnityDlpLogCallback>> = RwLock::new(None);

struct SimpleLogger;

impl log::Log for SimpleLogger {
    fn enabled(&self, metadata: &Metadata) -> bool {
        metadata.level() <= Level::Debug
    }

    fn log(&self, record: &Record) {
        if self.enabled(record.metadata()) {
            let message = record.args().to_string();

            // Keep the read lock for the duration of the call. Unregistering
            // takes the write lock, so a managed domain cannot discard its
            // delegate while a native callback is still in flight.
            if let Ok(callback) = CALLBACK.read() {
                if let Some(callback) = *callback {
                    let len = i32::try_from(message.len()).unwrap_or(i32::MAX);
                    // SAFETY: the pointer remains valid until callback returns.
                    unsafe { callback(level_number(record.level()), message.as_ptr(), len) };
                    return;
                }
            }

            eprintln!("[unity_dlp][{}] {message}", record.level());
        }
    }

    fn flush(&self) {}
}

static LOGGER: SimpleLogger = SimpleLogger;

pub fn init() {
    let _ = log::set_logger(&LOGGER).map(|()| log::set_max_level(LevelFilter::Debug));
}

pub fn set_callback(callback: Option<UnityDlpLogCallback>) {
    if let Ok(mut current) = CALLBACK.write() {
        *current = callback;
    }
}

fn level_number(level: Level) -> i32 {
    match level {
        Level::Error => 1,
        Level::Warn => 2,
        Level::Info => 3,
        Level::Debug => 4,
        Level::Trace => 5,
    }
}
