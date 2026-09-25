use std::sync::{Arc, Mutex, atomic::AtomicBool};

use desktopcast_engine::stream;

fn main() {
    stream(6754994, 3856, "http://stream.space.superneko.net/index/api/whip?app=desktopcast&stream=catherine", 60, Arc::new(Mutex::new(false))).unwrap();
}
