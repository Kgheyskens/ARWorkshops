using System;
using System.Collections.Generic;
using UnityEngine;

// Bewaart voortgang en instellingen via PlayerPrefs.
public static class SaveSystem
{
    private const string KeyParts = "parts";
    private const string KeyOwned = "ownedRadios";
    private const string KeySelected = "selectedRadio";
    private const string KeyAudio = "audioOn";
    private const string KeyVibration = "vibrationOn";
    private const string KeyCompleted = "storiesCompleted";
    private const string KeyLastStory = "lastStory";
    private const string KeyCompletedStories = "completedStories";

    public static int Parts
    {
        get => PlayerPrefs.GetInt(KeyParts, 0);
        set { PlayerPrefs.SetInt(KeyParts, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public static int StoriesCompleted
    {
        get => PlayerPrefs.GetInt(KeyCompleted, 0);
        set { PlayerPrefs.SetInt(KeyCompleted, value); PlayerPrefs.Save(); }
    }

    public static bool AudioOn
    {
        get => PlayerPrefs.GetInt(KeyAudio, 1) == 1;
        set { PlayerPrefs.SetInt(KeyAudio, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static bool VibrationOn
    {
        get => PlayerPrefs.GetInt(KeyVibration, 1) == 1;
        set { PlayerPrefs.SetInt(KeyVibration, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static string SelectedRadioId
    {
        get => PlayerPrefs.GetString(KeySelected, RadioCatalog.DefaultId);
        set { PlayerPrefs.SetString(KeySelected, value); PlayerPrefs.Save(); }
    }

    public static string LastStoryId
    {
        get => PlayerPrefs.GetString(KeyLastStory, "");
        set { PlayerPrefs.SetString(KeyLastStory, value); PlayerPrefs.Save(); }
    }

    public static bool IsStoryCompleted(string id) =>
        new HashSet<string>(PlayerPrefs.GetString(KeyCompletedStories, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)).Contains(id);

    public static void MarkStoryCompleted(string id)
    {
        var set = new HashSet<string>(PlayerPrefs.GetString(KeyCompletedStories, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        set.Add(id);
        PlayerPrefs.SetString(KeyCompletedStories, string.Join(",", set));
        PlayerPrefs.Save();
    }

    private static HashSet<string> Owned()
    {
        string raw = PlayerPrefs.GetString(KeyOwned, "");
        return new HashSet<string>(raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool OwnsRadio(string id) => id == RadioCatalog.DefaultId || Owned().Contains(id);

    public static void AddOwnedRadio(string id)
    {
        var owned = Owned();
        owned.Add(id);
        PlayerPrefs.SetString(KeyOwned, string.Join(",", owned));
        PlayerPrefs.Save();
    }

    // Alles wissen (om te testen).
    public static void ResetAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}

// Trillen als dat aanstaat.
public static class Haptics
{
    public static void Vibrate()
    {
        if (!SaveSystem.VibrationOn) return;
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }
}
