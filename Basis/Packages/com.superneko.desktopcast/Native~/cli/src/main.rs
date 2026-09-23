use desktopcast_engine::to_rtmp;

fn main() {
    to_rtmp(6754994, 76200, "rtmp://localhost/live/catherine", 60).unwrap();
}
