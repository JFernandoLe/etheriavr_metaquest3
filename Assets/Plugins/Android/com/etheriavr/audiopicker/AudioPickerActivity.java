package com.etheriavr.audiopicker;

import android.app.Activity;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.provider.OpenableColumns;
import android.util.Log;
import android.webkit.MimeTypeMap;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

public class AudioPickerActivity extends Activity
{
    private static final int REQUEST_PICK = 9010;
    private static final String TAG = "EtheriaAudioPicker";
    private static final String RESULT_SEPARATOR = "\t";

    public static final String EXTRA_CALLBACK_OBJECT = "callbackObject";
    public static final String EXTRA_CALLBACK_METHOD = "callbackMethod";

    private String callbackObject;
    private String callbackMethod;

    @Override
    protected void onCreate(android.os.Bundle savedInstanceState)
    {
        super.onCreate(savedInstanceState);

        callbackObject = getIntent().getStringExtra(EXTRA_CALLBACK_OBJECT);
        callbackMethod = getIntent().getStringExtra(EXTRA_CALLBACK_METHOD);

        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("audio/*");
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        intent.addFlags(Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
        startActivityForResult(intent, REQUEST_PICK);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data)
    {
        super.onActivityResult(requestCode, resultCode, data);

        if (requestCode != REQUEST_PICK)
        {
            finish();
            return;
        }

        if (resultCode != Activity.RESULT_OK || data == null || data.getData() == null)
        {
            sendResult("");
            finish();
            return;
        }

        try
        {
            Uri uri = data.getData();
            int takeFlags = data.getFlags() & (Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
            try
            {
                getContentResolver().takePersistableUriPermission(uri, takeFlags);
            }
            catch (Exception ignored)
            {
                Log.w(TAG, "No persistable permission for uri: " + uri);
            }

            String displayName = queryDisplayName(uri);
            String cachedPath = copyUriToCache(uri, displayName);
            if (cachedPath == null || cachedPath.isEmpty())
            {
                sendResult("");
            }
            else
            {
                String title = displayName != null ? stripExtension(displayName) : stripExtension(new File(cachedPath).getName());
                sendResult(cachedPath + RESULT_SEPARATOR + title);
            }
        }
        catch (Exception ex)
        {
            Log.e(TAG, "Error copiando audio desde URI", ex);
            sendResult("");
        }

        finish();
    }

    private String copyUriToCache(Uri uri, String displayName) throws Exception
    {
        InputStream inputStream = getContentResolver().openInputStream(uri);
        if (inputStream == null)
            throw new IllegalStateException("No se pudo abrir InputStream para " + uri);

        File cacheDir = new File(getCacheDir(), "picked_audio");
        if (!cacheDir.exists() && !cacheDir.mkdirs())
            throw new IllegalStateException("No se pudo crear cacheDir");

        String extension = resolveExtension(uri);
        String baseName = sanitizeBaseName(displayName != null ? stripExtension(displayName) : "selected_song");
        File outFile = new File(cacheDir, baseName + extension);

        int suffix = 1;
        while (outFile.exists())
        {
            outFile = new File(cacheDir, baseName + "_" + suffix + extension);
            suffix++;
        }

        FileOutputStream outputStream = new FileOutputStream(outFile, false);

        byte[] buffer = new byte[8192];
        int read;
        while ((read = inputStream.read(buffer)) != -1)
            outputStream.write(buffer, 0, read);

        inputStream.close();
        outputStream.close();

        Log.i(TAG, "Audio copiado a: " + outFile.getAbsolutePath());
        return outFile.getAbsolutePath();
    }

    private String queryDisplayName(Uri uri)
    {
        Cursor cursor = null;
        try
        {
            cursor = getContentResolver().query(uri, new String[]{OpenableColumns.DISPLAY_NAME}, null, null, null);
            if (cursor != null && cursor.moveToFirst())
            {
                int index = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME);
                if (index >= 0)
                    return cursor.getString(index);
            }
        }
        catch (Exception ex)
        {
            Log.w(TAG, "No se pudo leer DISPLAY_NAME", ex);
        }
        finally
        {
            if (cursor != null)
                cursor.close();
        }

        return null;
    }

    private static String stripExtension(String name)
    {
        if (name == null)
            return "";

        int dot = name.lastIndexOf('.');
        if (dot > 0)
            return name.substring(0, dot);
        return name;
    }

    private static String sanitizeBaseName(String name)
    {
        if (name == null || name.trim().isEmpty())
            return "selected_song";

        String cleaned = name.trim().replaceAll("[\\\\/:*?\"<>|]", "_");
        if (cleaned.isEmpty())
            return "selected_song";
        return cleaned;
    }

    private String resolveExtension(Uri uri)
    {
        String mime = getContentResolver().getType(uri);
        if (mime != null)
        {
            String ext = MimeTypeMap.getSingleton().getExtensionFromMimeType(mime);
            if (ext != null && !ext.isEmpty())
                return "." + ext;
        }

        return ".mp3";
    }

    private void sendResult(String payload)
    {
        if (callbackObject == null || callbackMethod == null)
            return;

        UnityPlayer.UnitySendMessage(callbackObject, callbackMethod, payload != null ? payload : "");
    }
}
