use std::{
    fmt::Display, sync::{
        Arc, Mutex,
    },
};

use desktopcast_engine::stream;

uniffi::setup_scaffolding!();

#[derive(Debug, uniffi::Error)]
pub enum DesktopCastError {
    Generic(String),
}

impl Display for DesktopCastError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            DesktopCastError::Generic(e) => f.write_fmt(format_args!("Generic Error: {e}")),
        }
    }
}

#[derive(Debug, uniffi::Object)]
pub struct Cancellable {
    bool: Arc<Mutex<bool>>,
}

/*impl Display for Cancellable {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_fmt(format_args!("cancelled: {}", self.bool.load(Relaxed)));
        Ok(())
    }
}*/

#[uniffi::export]
impl Cancellable {
    #[uniffi::method]
    pub fn cancel(&self) {
        *self.bool.lock().unwrap() = true;
    }
}

#[uniffi::export]
pub fn start_rtmp(
    window_handle: u64,
    process_id: u32,
    url: String,
    frame_rate: u64,
) -> Result<Cancellable, DesktopCastError> {
    let cancel_send = Arc::new(Mutex::new(false));
    let cancel_recv = cancel_send.clone();

    std::thread::spawn(move || {
        stream(window_handle, process_id, &url, frame_rate, cancel_recv)
            .map_err(|e| DesktopCastError::Generic(e.to_string()));
    });

    Ok(Cancellable { bool: cancel_send })
}
