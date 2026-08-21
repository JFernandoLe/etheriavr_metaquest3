package com.etheriavr.audiopicker;

import android.media.MediaCodec;
import android.media.MediaExtractor;
import android.media.MediaFormat;
import android.util.Log;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;

/**
 * Decodifica MP3/M4A/AAC a WAV usando MediaCodec nativo de Android (Quest).
 * Unity no decodifica MP3 locales de forma fiable via UnityWebRequestMultimedia.
 */
public class AudioDecodeBridge
{
    private static final String TAG = "AudioDecodeBridge";

    public static String convertToWavIfNeeded(String inputPath)
    {
        if (inputPath == null || inputPath.isEmpty())
            return null;

        File inputFile = new File(inputPath);
        if (!inputFile.exists())
        {
            Log.e(TAG, "Input missing: " + inputPath);
            return null;
        }

        String lower = inputPath.toLowerCase();
        if (lower.endsWith(".wav"))
            return inputPath;

        String outputPath = inputPath.replaceAll("\\.[^.]+$", "") + ".wav";
        File outputFile = new File(outputPath);

        try
        {
            if (decodeToWav(inputPath, outputPath))
            {
                Log.i(TAG, "WAV generado: " + outputPath + " (" + outputFile.length() + " bytes)");
                return outputPath;
            }
        }
        catch (Exception ex)
        {
            Log.e(TAG, "Error decodificando " + inputPath, ex);
        }

        return null;
    }

    private static boolean decodeToWav(String inputPath, String outputPath) throws IOException
    {
        MediaExtractor extractor = new MediaExtractor();
        extractor.setDataSource(inputPath);

        int trackIndex = -1;
        MediaFormat inputFormat = null;
        for (int i = 0; i < extractor.getTrackCount(); i++)
        {
            MediaFormat format = extractor.getTrackFormat(i);
            String mime = format.getString(MediaFormat.KEY_MIME);
            if (mime != null && mime.startsWith("audio/"))
            {
                trackIndex = i;
                inputFormat = format;
                break;
            }
        }

        if (trackIndex < 0 || inputFormat == null)
        {
            extractor.release();
            Log.e(TAG, "No audio track in " + inputPath);
            return false;
        }

        extractor.selectTrack(trackIndex);
        String mime = inputFormat.getString(MediaFormat.KEY_MIME);
        MediaCodec codec = MediaCodec.createDecoderByType(mime);
        codec.configure(inputFormat, null, null, 0);
        codec.start();

        ByteArrayOutputStream pcmStream = new ByteArrayOutputStream();
        MediaCodec.BufferInfo bufferInfo = new MediaCodec.BufferInfo();
        MediaFormat outputFormat = codec.getOutputFormat();
        boolean inputDone = false;

        while (true)
        {
            if (!inputDone)
            {
                int inputBufferIndex = codec.dequeueInputBuffer(10000);
                if (inputBufferIndex >= 0)
                {
                    ByteBuffer inputBuffer = codec.getInputBuffer(inputBufferIndex);
                    if (inputBuffer == null)
                        break;

                    int sampleSize = extractor.readSampleData(inputBuffer, 0);
                    if (sampleSize < 0)
                    {
                        codec.queueInputBuffer(inputBufferIndex, 0, 0, 0, MediaCodec.BUFFER_FLAG_END_OF_STREAM);
                        inputDone = true;
                    }
                    else
                    {
                        codec.queueInputBuffer(inputBufferIndex, 0, sampleSize, extractor.getSampleTime(), 0);
                        extractor.advance();
                    }
                }
            }

            int outputBufferIndex = codec.dequeueOutputBuffer(bufferInfo, 10000);
            if (outputBufferIndex == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED)
            {
                outputFormat = codec.getOutputFormat();
            }
            else if (outputBufferIndex >= 0)
            {
                ByteBuffer outputBuffer = codec.getOutputBuffer(outputBufferIndex);
                if (outputBuffer != null && bufferInfo.size > 0)
                {
                    byte[] chunk = new byte[bufferInfo.size];
                    outputBuffer.position(bufferInfo.offset);
                    outputBuffer.limit(bufferInfo.offset + bufferInfo.size);
                    outputBuffer.get(chunk);
                    pcmStream.write(chunk);
                }

                codec.releaseOutputBuffer(outputBufferIndex, false);

                if ((bufferInfo.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0)
                    break;
            }
        }

        codec.stop();
        codec.release();
        extractor.release();

        byte[] pcmData = pcmStream.toByteArray();
        if (pcmData.length == 0)
        {
            Log.e(TAG, "PCM vacío para " + inputPath);
            return false;
        }

        if (outputFormat == null)
        {
            Log.e(TAG, "Formato de salida nulo");
            return false;
        }

        int sampleRate = outputFormat.getInteger(MediaFormat.KEY_SAMPLE_RATE);
        int channelCount = outputFormat.getInteger(MediaFormat.KEY_CHANNEL_COUNT);
        writeWavFile(outputPath, pcmData, sampleRate, channelCount, 16);
        return new File(outputPath).length() > 44;
    }

    private static void writeWavFile(String path, byte[] pcmData, int sampleRate, int channels, int bitsPerSample)
            throws IOException
    {
        int byteRate = sampleRate * channels * bitsPerSample / 8;
        int blockAlign = channels * bitsPerSample / 8;
        int dataSize = pcmData.length;

        ByteBuffer header = ByteBuffer.allocate(44);
        header.order(ByteOrder.LITTLE_ENDIAN);
        header.put("RIFF".getBytes());
        header.putInt(36 + dataSize);
        header.put("WAVE".getBytes());
        header.put("fmt ".getBytes());
        header.putInt(16);
        header.putShort((short) 1); // PCM
        header.putShort((short) channels);
        header.putInt(sampleRate);
        header.putInt(byteRate);
        header.putShort((short) blockAlign);
        header.putShort((short) bitsPerSample);
        header.put("data".getBytes());
        header.putInt(dataSize);

        FileOutputStream outputStream = new FileOutputStream(path);
        outputStream.write(header.array());
        outputStream.write(pcmData);
        outputStream.close();
    }
}
