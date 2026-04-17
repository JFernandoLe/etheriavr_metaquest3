package com.etheriavr.midi;

import android.os.Handler;
import android.os.Looper;
import android.os.Bundle;
import android.content.Context;
import android.media.midi.MidiManager;
import android.media.midi.MidiDevice;
import android.media.midi.MidiOutputPort;
import android.media.midi.MidiDeviceInfo;
import android.media.midi.MidiDeviceStatus;
import android.media.midi.MidiReceiver;
import android.util.Log;
import java.io.IOException;

/**
 * MIDI Input Bridge - Buffer-based native service for Quest 3
 * Handles MIDI input callbacks with circular event buffer for multi-note support
 */
@SuppressWarnings("all")
public class MidiInputBridge {
    private static final String TAG = "MidiInputBridge";
    private static MidiInputBridge instance;
    private static final int BUFFER_SIZE = 32;  // Ring buffer for up to 32 events
    
    private Context context;
    private MidiManager midiManager;
    private MidiDevice midiDevice;
    private MidiOutputPort outputPort;
    private Handler mainHandler;
    private MidiManager.DeviceCallback deviceCallback;
    private boolean deviceCallbackRegistered = false;
    private boolean isOpeningDevice = false;
    private int currentDeviceId = -1;
    
    // Ring buffer for MIDI events (3 bytes each)
    private static byte[] eventBuffer = new byte[BUFFER_SIZE * 3];
    private static int writeIndex = 0;
    private static int readIndex = 0;
    private static int eventCount = 0;
    private static final Object bufferLock = new Object();
    
    // Status
    public static boolean isConnected = false;
    private static String currentDeviceName = "NO REGISTRADO";
    
    private MidiInputBridge(Context context) {
        this.context = context.getApplicationContext();
        this.mainHandler = new Handler(Looper.getMainLooper());
    }
    
    /**
     * Get or create singleton instance
     */
    public static MidiInputBridge getInstance(Context context) {
        if (instance == null) {
            synchronized (MidiInputBridge.class) {
                if (instance == null) {
                    instance = new MidiInputBridge(context);
                }
            }
        }
        return instance;
    }
    
    /**
     * Initialize MIDI system
     */
    public void init() {
        Log.d(TAG, "Initializing MIDI");
        try {
            midiManager = (MidiManager) context.getSystemService(Context.MIDI_SERVICE);
            if (midiManager != null) {
                Log.d(TAG, "✅ MIDI Manager initialized");
                registerDeviceCallback();
                if (!isConnected && !isOpeningDevice) {
                    scanDevices();
                }
            } else {
                Log.w(TAG, "⚠️ MIDI Manager not available");
            }
        } catch (Exception e) {
            Log.e(TAG, "Error initializing: " + e.getMessage());
        }
    }
    
    /**
     * Scan for MIDI devices and open first one found
     */
    private void scanDevices() {
        if (midiManager == null) {
            setDisconnectedState();
            return;
        }

        if (isConnected || isOpeningDevice) {
            return;
        }

        try {
            MidiDeviceInfo[] devices = midiManager.getDevices();
            Log.d(TAG, "Found " + devices.length + " MIDI device(s)");
            
            if (devices.length == 0) {
                Log.w(TAG, "No MIDI devices found");
                setDisconnectedState();
                return;
            }
            
            // Try to open first device with input ports
            for (MidiDeviceInfo deviceInfo : devices) {
                int inputPorts = deviceInfo.getInputPortCount();
                String deviceName = buildDeviceName(deviceInfo);
                Log.d(TAG, "Device: " + deviceName + " | " + inputPorts + " input port(s)");
                
                if (inputPorts > 0) {
                    openDevice(deviceInfo);
                    return;
                }
            }

            setDisconnectedState();
        } catch (Exception e) {
            Log.e(TAG, "Error scanning: " + e.getMessage());
            setDisconnectedState();
        }
    }
    
    /**
     * Open device asynchronously
     */
    private void openDevice(MidiDeviceInfo deviceInfo) {
        if (deviceInfo == null || midiManager == null) {
            setDisconnectedState();
            return;
        }

        if (isOpeningDevice) {
            return;
        }

        isOpeningDevice = true;
        final String deviceName = buildDeviceName(deviceInfo);
        final int deviceId = deviceInfo.getId();
        Log.d(TAG, "Opening device: " + deviceName);
        midiManager.openDevice(deviceInfo, new MidiManager.OnDeviceOpenedListener() {
            @Override
            public void onDeviceOpened(MidiDevice device) {
                handleDeviceOpened(device, deviceName, deviceId);
            }
        }, mainHandler);
    }
    
    /**
     * Handle device opened callback - called on main thread
     */
    private void handleDeviceOpened(MidiDevice device, String deviceName, int deviceId) {
        isOpeningDevice = false;

        if (device == null) {
            Log.w(TAG, "Device open failed");
            setDisconnectedState();
            return;
        }
        
        Log.d(TAG, "Device opened successfully");
        closeCurrentConnection();
        midiDevice = device;
        currentDeviceId = deviceId;
        
        try {
            // Get first output port (for receiving FROM device)
            outputPort = device.openOutputPort(0);
            if (outputPort == null) {
                Log.e(TAG, "Failed to open output port");
                setDisconnectedState();
                return;
            }
            
            // Create custom receiver
            SimpleMidiReceiver receiver = new SimpleMidiReceiver();
            outputPort.connect(receiver);
            
            clearEventBuffer();
            isConnected = true;
            currentDeviceName = deviceName != null && !deviceName.trim().isEmpty()
                ? deviceName
                : "NO REGISTRADO";
            Log.d(TAG, "✅ MIDI Connected and listening");
        } catch (Exception e) {
            Log.e(TAG, "Error opening port: " + e.getMessage());
            setDisconnectedState();
        }
    }

    private void registerDeviceCallback() {
        if (midiManager == null || deviceCallbackRegistered) {
            return;
        }

        deviceCallback = new MidiManager.DeviceCallback() {
            @Override
            public void onDeviceAdded(MidiDeviceInfo device) {
                Log.d(TAG, "Device added: " + buildDeviceName(device));
                if (!isConnected && !isOpeningDevice) {
                    scanDevices();
                }
            }

            @Override
            public void onDeviceRemoved(MidiDeviceInfo device) {
                Log.d(TAG, "Device removed: " + buildDeviceName(device));
                if (device != null && device.getId() == currentDeviceId) {
                    disconnectCurrentDevice();
                    mainHandler.post(new Runnable() {
                        @Override
                        public void run() {
                            scanDevices();
                        }
                    });
                }
            }

            @Override
            public void onDeviceStatusChanged(MidiDeviceStatus status) {
                if (status == null || status.getDeviceInfo() == null) {
                    return;
                }

                MidiDeviceInfo deviceInfo = status.getDeviceInfo();
                if (deviceInfo.getId() == currentDeviceId && !isConnected && !isOpeningDevice) {
                    scanDevices();
                }
            }
        };

        midiManager.registerDeviceCallback(deviceCallback, mainHandler);
        deviceCallbackRegistered = true;
    }

    public void rescanDevices() {
        if (!isConnected) {
            scanDevices();
            return;
        }

        disconnectCurrentDevice();
        scanDevices();
    }

    public void disconnectCurrentDevice() {
        isOpeningDevice = false;
        closeCurrentConnection();
        setDisconnectedState();
    }

    private void closeCurrentConnection() {
        try {
            if (outputPort != null) {
                outputPort.close();
            }
        } catch (Exception e) {
            Log.w(TAG, "Error closing output port: " + e.getMessage());
        }

        try {
            if (midiDevice != null) {
                midiDevice.close();
            }
        } catch (Exception e) {
            Log.w(TAG, "Error closing MIDI device: " + e.getMessage());
        }

        outputPort = null;
        midiDevice = null;
    }

    private void setDisconnectedState() {
        currentDeviceId = -1;
        isConnected = false;
        currentDeviceName = "NO REGISTRADO";
        clearEventBuffer();
    }

    private static void clearEventBuffer() {
        synchronized (bufferLock) {
            writeIndex = 0;
            readIndex = 0;
            eventCount = 0;
        }
    }

    private String buildDeviceName(MidiDeviceInfo deviceInfo) {
        if (deviceInfo == null) {
            return "NO REGISTRADO";
        }

        try {
            Bundle properties = deviceInfo.getProperties();
            String name = properties != null ? properties.getString(MidiDeviceInfo.PROPERTY_NAME) : null;
            String manufacturer = properties != null ? properties.getString(MidiDeviceInfo.PROPERTY_MANUFACTURER) : null;
            String product = properties != null ? properties.getString(MidiDeviceInfo.PROPERTY_PRODUCT) : null;

            if (name != null && !name.trim().isEmpty()) {
                return name.trim();
            }

            StringBuilder builder = new StringBuilder();
            if (manufacturer != null && !manufacturer.trim().isEmpty()) {
                builder.append(manufacturer.trim());
            }

            if (product != null && !product.trim().isEmpty()) {
                if (builder.length() > 0) {
                    builder.append(" ");
                }
                builder.append(product.trim());
            }

            if (builder.length() > 0) {
                return builder.toString();
            }
        } catch (Exception e) {
            Log.w(TAG, "Could not resolve MIDI device name: " + e.getMessage());
        }

        return "NO REGISTRADO";
    }
    
    /**
     * Simple MIDI receiver callback - stores data in ring buffer
     */
    private class SimpleMidiReceiver extends MidiReceiver {
        @Override
        public void onSend(byte[] msg, int offset, int count, long timestamp) throws IOException {
            if (count < 3) return;
            
            // Enqueue to ring buffer
            synchronized (bufferLock) {
                // Store 3 bytes at writeIndex
                int bufIdx = (writeIndex % BUFFER_SIZE) * 3;
                eventBuffer[bufIdx] = msg[offset];
                eventBuffer[bufIdx + 1] = msg[offset + 1];
                eventBuffer[bufIdx + 2] = msg[offset + 2];
                
                writeIndex++;
                eventCount = (writeIndex - readIndex);
                
                // Prevent overflow
                if (eventCount > BUFFER_SIZE) {
                    readIndex = writeIndex - BUFFER_SIZE;
                    eventCount = BUFFER_SIZE;
                }
            }
            
            Log.d(TAG, String.format("MIDI RX: status=0x%02X data1=%d data2=%d", 
                msg[offset] & 0xFF, msg[offset + 1] & 0xFF, msg[offset + 2] & 0xFF));
        }
        
        @Override
        public void onFlush() throws IOException {
            // No-op
        }
    }
    
    /**
     * Dequeue one MIDI event from buffer (called by C#)
     * Returns 3-byte array [status, data1, data2] or null if no events
     */
    public static byte[] dequeueEvent() {
        synchronized (bufferLock) {
            if (eventCount <= 0) return null;  // No events
            
            // Read from readIndex
            int bufIdx = (readIndex % BUFFER_SIZE) * 3;
            byte[] event = new byte[3];
            event[0] = eventBuffer[bufIdx];
            event[1] = eventBuffer[bufIdx + 1];
            event[2] = eventBuffer[bufIdx + 2];
            
            readIndex++;
            eventCount--;
            
            return event;
        }
    }
    
    /**
     * Get number of events in buffer
     */
    public static int getEventCount() {
        synchronized (bufferLock) {
            return eventCount;
        }
    }

    public static String getConnectedDeviceName() {
        return currentDeviceName != null && !currentDeviceName.trim().isEmpty()
            ? currentDeviceName
            : "NO REGISTRADO";
    }
    
    /**
     * Close and cleanup
     */
    public void close() {
        try {
            if (midiManager != null && deviceCallbackRegistered && deviceCallback != null) {
                midiManager.unregisterDeviceCallback(deviceCallback);
            }

            deviceCallbackRegistered = false;
            deviceCallback = null;
            disconnectCurrentDevice();
            Log.d(TAG, "Closed");
        } catch (Exception e) {
            Log.e(TAG, "Error closing: " + e.getMessage());
        }
    }
}
