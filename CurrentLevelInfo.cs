using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RDLevelEditor;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

// This mod is easy enough that i can stuff everything in this file, even if i don't want to
namespace CurrentLevelInfo;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("Rhythm Doctor.exe")]
// i don't know what
#pragma warning disable BepInEx002 // Classes with BepInPlugin attribute must inherit from BaseUnityPlugin
public class CurrentLevelInfo : BaseUnityPlugin
#pragma warning restore BepInEx002 // Classes with BepInPlugin attribute must inherit from BaseUnityPlugin
{
    internal static ManualLogSource logger;

    public static new ConfigEntry<bool> enabled;
    public static ConfigEntry<string> filePath;
    public static ConfigEntry<string> customLevelFileText;
    public static ConfigEntry<string> storyLevelFileText;
    public static ConfigEntry<string> notInLevelFileText;

    private void Awake()
    {
        logger = Logger;

        enabled = Config.Bind("General", "Enabled", true, "Whether to create a text file detailing the current level's information.");

        filePath = Config.Bind("General", "FilePath", "details.txt", "Where to place the text file.\n" + 
        "If the path isn't a path containing the absolute location (e.g. this contains the absolute location: 'C:/details.txt'),\n" + 
        "it will create the text file in the same place as the save files are located.");

        customLevelFileText = Config.Bind("General", "CustomLevelFileContents", "{song} by {artist}, level by {author}", "What the text file should contain when in a custom level.\n" + 
        "{song}, {artist}, {author} and {difficulty} will be replaced with the respective details from the level.");

        storyLevelFileText = Config.Bind("General", "StoryLevelFileContents", "{song}", "What the text file should contain when in a story level.\n" + 
        "{song} will be replaced with the respective detail from the level.");

        notInLevelFileText = Config.Bind("General", "NotInLevelFileContents", "Not in a level", "What the text file should contain when not in a level.");

        logger.LogMessage($"Current Level Information plugin loaded! Will {(enabled.Value ? "" : "not ")}create a file containing the current level's information!");

        Harmony instance = new("patcher");
        instance.PatchAll(typeof(DetailsPatch));
        instance.PatchAll(typeof(GameDetailsPatch));
    }

    // could prob code better but like it'll work
    public static void CreateTextFile(bool inCustomLevel, bool inStoryLevel, 
        string song = "", string artist = "", 
        string author = "", LevelDifficulty difficulty = LevelDifficulty.Medium)
    {
        string contents = notInLevelFileText.Value;
        if (inStoryLevel)
            contents = storyLevelFileText.Value.Replace("{song}", song);
        else if (inCustomLevel)
        {
            contents = customLevelFileText.Value;
            contents = contents.Replace("{song}", song);
            contents = contents.Replace("{artist}", artist);
            contents = contents.Replace("{author}", author);
            contents = contents.Replace("{difficulty}", RDString.Get($"enum.LevelDifficulty." + difficulty.ToString()));
        }
        contents = contents.Replace("\\n", "\n");
        contents = new Regex(@"<(\/)?color(=("")?([#A-Za-z0-9 ])*("")?)?>", RegexOptions.Multiline).Replace(contents, "");

        // Assume absolute
        if (filePath.Value.Contains(Path.DirectorySeparatorChar) || filePath.Value.Contains(Path.AltDirectorySeparatorChar))
            File.WriteAllText(filePath.Value, contents);
        else
            File.WriteAllText(Persistence.GetSaveFileFolderPath() + Path.DirectorySeparatorChar + filePath.Value, contents);
    }

    [HarmonyPatch(typeof(scnBase), "Start")]
    public class DetailsPatch
    {
        public static void Postfix(scnBase __instance)
        {
            if (__instance is not scnGame || __instance.editor != null)
                CreateTextFile(false, false);
        }
    }

    [HarmonyPatch(typeof(scnGame), nameof(scnGame.EnsureSwitchPlayersState))]
    public class GameDetailsPatch
    {
        public static void Postfix(scnGame __instance)
        {
            if (scnGame.levelToLoadSource == LevelSource.CutscenesPath)
            {
                CreateTextFile(false, false);
                return;
            }
            if (scnGame.levelToLoadSource == LevelSource.InternalPath)
            {
                CreateTextFile(false, true, RDString.Get("levelSelect." + scnGame.internalIdentifier));
                return;
            }
            // it must be external path!
            Level_Custom custom = __instance.currentLevel as Level_Custom;
            RDLevelSettings settings = custom.data.settings;

            static string notDefined(string thing)
                => (thing == null || thing.Length <= 0) ? "???" : thing;

            CreateTextFile(true, false, 
                notDefined(settings.song), notDefined(settings.artist), 
                notDefined(settings.author), settings.difficulty);
        }
    }
}