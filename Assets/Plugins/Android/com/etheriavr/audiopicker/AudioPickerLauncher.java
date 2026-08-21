package com.etheriavr.audiopicker;

import android.app.Activity;
import android.content.Intent;
import com.unity3d.player.UnityPlayer;

public class AudioPickerLauncher
{
    public static void launch(String callbackObject, String callbackMethod)
    {
        Activity activity = UnityPlayer.currentActivity;
        Intent intent = new Intent(activity, AudioPickerActivity.class);
        intent.putExtra(AudioPickerActivity.EXTRA_CALLBACK_OBJECT, callbackObject);
        intent.putExtra(AudioPickerActivity.EXTRA_CALLBACK_METHOD, callbackMethod);
        activity.startActivity(intent);
    }
}
