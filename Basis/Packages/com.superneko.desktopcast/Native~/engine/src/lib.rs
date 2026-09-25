use anyhow::anyhow;
use gstreamer::{
    self as gst, Buffer, Caps, Element, ElementFactory, FlowError, FlowSuccess, Pipeline,
    element_error,
    glib::{self, object::Cast},
    prelude::{ElementExt, ElementExtManual, GstBinExtManual, GstObjectExt as _},
};
use gstreamer_app::{AppSink, AppSinkCallbacks, AppSrc};
use std::{
    result::Result,
    str::FromStr,
    sync::{Arc, Mutex},
    time::Duration,
};

pub fn stream(
    window_handle: u64,
    process_id: u32,
    url: &str,
    frame_rate: u64,
    all_cancel: Arc<Mutex<bool>>,
) -> Result<(), anyhow::Error> {
    gst::init()?;

    let pipeline = gst::Pipeline::default();

    // VIDEO

    let vwindowcap = add_window_capture_block(&pipeline, window_handle, frame_rate, all_cancel.clone())?;

    // AUDIO

    let aprocesscap = add_process_audio_capture_block(&pipeline, process_id)?;

    // MUX + STREAM

    if url.starts_with("rtmp") {
        // Video side
        let venc = ElementFactory::make("mfh264enc")
            .property_from_str("bitrate", "2000")
            .property_from_str("gop-size", "60")
            .build()?;
        let vparse = ElementFactory::make("h264parse").build()?;

        pipeline.add_many([&venc, &vparse])?;

        vwindowcap.link(&venc)?;
        venc.link(&vparse)?;

        // Audio side
        let aenc = ElementFactory::make("mfaacenc")
            .property("bitrate", 128000u32)
            .build()?;
        let aqueue = ElementFactory::make("queue").build()?;

        pipeline.add_many([&aenc, &aqueue])?;

        aprocesscap.link(&aenc)?;
        aenc.link(&aqueue)?;

        let mux = ElementFactory::make("flvmux").build()?;
        let rtmp = ElementFactory::make("rtmp2sink") // TODO: change to rtmp2sink
            .property("location", url)
            .build()?;

        pipeline.add_many([&mux, &rtmp])?;

        vparse.link(&mux)?;
        aqueue.link(&mux)?;
        mux.link(&rtmp)?;
    } else if url.starts_with("whip") || url.starts_with("http") {
        // Video side
        let venc = ElementFactory::make("mfh264enc")
            .property_from_str("bitrate", "2000")
            .property_from_str("gop-size", "60")
            .build()?;
        let vpay = ElementFactory::make("rtph264pay").build()?;

        pipeline.add_many([&venc, &vpay])?;

        vwindowcap.link(&venc)?;
        venc.link(&vpay)?;

        // Audio side
        let aenc = ElementFactory::make("opusenc").build()?;
        let apay = ElementFactory::make("rtpopuspay2").build()?;

        pipeline.add_many([&aenc, &apay])?;

        aprocesscap.link(&aenc)?;
        aenc.link(&apay)?;

        // Mux

        let whip = ElementFactory::make("whipsink")
            .property("whip-endpoint", url)
            .build()?;

        pipeline.add_many([&whip])?;

        vpay.link_pads_filtered(
            None,
            &whip,
            Some("sink_0"),
            &Caps::from_str(
                "application/x-rtp,media=video,encoding-name=H264,payload=97,clock-rate=90000",
            )?,
        )?;
        apay.link_pads_filtered(None, &whip, Some("sink_1"), &Caps::from_str("application/x-rtp,media=audio,encoding-name=OPUS,payload=96,clock-rate=48000,encoding-params=(string)2")?)?;
    }

    // GRAPH EXECUTION

    pipeline.set_state(gst::State::Playing)?;

    let bus = pipeline
        .bus()
        .expect("Pipeline without bus. Shouldn't happen!");

    loop {
        for msg in bus.iter_timed(gst::ClockTime::from_mseconds(100)) {
            use gst::MessageView;

            match msg.view() {
                MessageView::Eos(..) => {
                    *all_cancel.lock().unwrap() = true;
                    break;
                }
                MessageView::Error(err) => {
                    pipeline.set_state(gst::State::Null)?;

                    return Err(anyhow!(
                        "Gstreamer error. {err} on {}",
                        msg.src()
                            .map(|s| s.path_string())
                            .unwrap_or_else(|| glib::GString::from("UNKNOWN"))
                    ));
                }
                MessageView::StateChanged(s) => {
                    println!(
                        "State changed from {:?}: {:?} -> {:?} ({:?})",
                        s.src().map(|s| s.path_string()),
                        s.old(),
                        s.current(),
                        s.pending()
                    );
                }
                _ => (),
            }

            if *all_cancel.lock().unwrap() {
                break;
            }
        }

        if *all_cancel.lock().unwrap() {
            break;
        }
    }

    pipeline.set_state(gst::State::Null)?;

    Ok(())
}

fn add_window_capture_block(
    pipeline: &Pipeline,
    window_handle: u64,
    frame_rate: u64,
    all_cancel: Arc<Mutex<bool>>
) -> Result<AppSrc, anyhow::Error> {
    let vcap = ElementFactory::make("d3d11screencapturesrc")
        .property("window-handle", window_handle)
        .build()?;

    let vconv = ElementFactory::make("d3d11convert").build()?;

    let livesync2caps = Caps::builder("video/x-raw")
        .features(["memory:D3D11Memory"])
        .field("format", "NV12")
        .field("width", 1280i32)
        .field("height", 720i32)
        .field("framerate", gst::Fraction::new(60, 1))
        .field("pixel-aspect-ratio", gst::Fraction::new(1, 1))
        .build();

    let livesync2buffer_sink = Arc::new(Mutex::<Option<Buffer>>::new(None));
    let livesync2buffer_source = livesync2buffer_sink.clone();

    let livesync2sink = AppSink::builder()
        .property("async", false)
        .property("sync", false)
        .caps(&livesync2caps)
        .callbacks(
            AppSinkCallbacks::builder()
                .new_sample(move |appsink| {
                    let sample = appsink.pull_sample().map_err(|_| FlowError::Eos)?;

                    let buffer = sample.buffer().ok_or_else(|| {
                        element_error!(
                            appsink,
                            gst::ResourceError::Failed,
                            ("Failed to get buffer from appsink")
                        );

                        FlowError::Error
                    })?;

                    let buffer = Some(buffer.to_owned());

                    {
                        // panics when sink panicked in clitical section
                        *(livesync2buffer_sink.lock().unwrap()) = buffer;
                    }

                    Ok(FlowSuccess::Ok)
                })
                .build(),
        )
        .build();

    let livesync2source = AppSrc::builder()
        .is_live(true)
        .format(gst::Format::Time)
        .caps(&livesync2caps)
        .property("do-timestamp", true)
        .build();

    let livesync2source_thread = livesync2source.clone();

    std::thread::spawn(move || {
        loop {
            if *all_cancel.lock().unwrap() {
                break;
            }

            if let Some(mut buffer) = livesync2buffer_source
                .lock()
                .unwrap() // panics when source panicked in clitical section
                .as_ref()
                .map(|b| b.copy())
            {
                // TODO: pts override

                {
                    let buffer_ref = buffer.make_mut();

                    buffer_ref.set_pts(gst::ClockTime::NONE);
                    buffer_ref.set_dts(gst::ClockTime::NONE);
                    buffer_ref.set_duration(gst::ClockTime::NONE);
                }

                if let Err(e) = livesync2source_thread.push_buffer(buffer) {
                    eprintln!("Failed to publish buffer: {e}");
                }
            }

            std::thread::sleep(Duration::from_nanos((1000 * 1000 * 1000) / frame_rate));
        }
    });

    pipeline.add_many([
        &vcap,
        &vconv,
        livesync2sink.upcast_ref(),
        livesync2source.upcast_ref(),
    ])?;

    vcap.link(&vconv)?;
    vconv.link(&livesync2sink)?;

    Ok(livesync2source)
}

fn add_process_audio_capture_block(
    pipeline: &Pipeline,
    process_id: u32
) -> Result<Element, anyhow::Error> {
    let acap = ElementFactory::make("wasapi2src")
        // .property("loopback", true)
        .property_from_str("loopback-mode", "include-process-tree")
        .property("loopback-target-pid", process_id)
        .property("low-latency", true)
        .build()?;
    let acapf = ElementFactory::make("capsfilter")
        .property_from_str(
            "caps",
            "audio/x-raw,format=S16LE,layout=interleaved,rate=48000,channels=2",
        )
        .build()?;
    let aconv = ElementFactory::make("audioconvert").build()?;
    let aresample = ElementFactory::make("audioresample").build()?;

    pipeline.add_many([&acap, &aconv, &aresample, &acapf])?;

    acap.link(&aconv)?;
    aconv.link(&aresample)?;
    aresample.link(&acapf)?;

    Ok(acapf)
}
