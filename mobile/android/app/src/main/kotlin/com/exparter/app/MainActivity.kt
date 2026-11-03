package com.exparter.app

import android.content.Context
import android.hardware.display.DisplayManager
import android.os.Handler
import android.os.Looper
import android.view.Display
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.EventChannel

class MainActivity : FlutterActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        EventChannel(flutterEngine.dartExecutor.binaryMessenger, "com.exparter.app/screen_capture")
            .setStreamHandler(ScreenCaptureStops(this))
    }
}

/**
 * Tells the call when the system ended a screen share by itself, from its own "stop
 * sharing" control or a locked screen. The WebRTC plugin does not report it, so the call
 * would keep showing that it shares. The system then turns off the virtual display the
 * plugin captures into; displays an app created are visible to its own listeners.
 */
private class ScreenCaptureStops(context: Context) : EventChannel.StreamHandler {
    private val displays = context.getSystemService(DisplayManager::class.java)
    private var listener: DisplayManager.DisplayListener? = null

    override fun onListen(arguments: Any?, events: EventChannel.EventSink) {
        val watcher = object : DisplayManager.DisplayListener {
            override fun onDisplayAdded(displayId: Int) = Unit

            override fun onDisplayRemoved(displayId: Int) = Unit

            override fun onDisplayChanged(displayId: Int) {
                val display = displays.getDisplay(displayId) ?: return
                // The name the plugin gives its display (OrientationAwareScreenCapturer).
                if (display.name == "WebRTC_ScreenCapture" && display.state == Display.STATE_OFF) {
                    events.success(null)
                }
            }
        }
        displays.registerDisplayListener(watcher, Handler(Looper.getMainLooper()))
        listener = watcher
    }

    override fun onCancel(arguments: Any?) {
        listener?.let(displays::unregisterDisplayListener)
        listener = null
    }
}
