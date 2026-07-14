using System.Collections.Generic;
using Godot;
using FileAccess = Godot.FileAccess;

namespace ChipsChallenge;

/// <summary>
/// Player progress persisted to user://progress.json: last level played
/// (for resume-on-launch), furthest level reached, and best time-left per
/// level number. Purely a record — navigation is never gated by it.
/// Uses Godot's Json (not System.Text.Json) so nothing depends on
/// reflection surviving the Android export.
/// </summary>
public sealed class Progress
{
    private const string SavePath = "user://progress.json";

    public int LastLevelIndex;
    public int FurthestLevelIndex;
    public readonly Dictionary<int, int> BestTimeLeft = new(); // level number -> seconds left

    public static Progress Load()
    {
        var progress = new Progress();
        if (!FileAccess.FileExists(SavePath)) return progress;

        var parsed = Json.ParseString(FileAccess.GetFileAsString(SavePath));
        if (parsed.VariantType != Variant.Type.Dictionary) return progress; // corrupt: start fresh
        var dict = parsed.AsGodotDictionary();

        if (dict.TryGetValue("last", out var last)) progress.LastLevelIndex = last.AsInt32();
        if (dict.TryGetValue("furthest", out var furthest)) progress.FurthestLevelIndex = furthest.AsInt32();
        if (dict.TryGetValue("best", out var best) && best.VariantType == Variant.Type.Dictionary)
        {
            foreach (var kv in best.AsGodotDictionary())
            {
                if (int.TryParse(kv.Key.AsString(), out var number))
                    progress.BestTimeLeft[number] = kv.Value.AsInt32();
            }
        }
        return progress;
    }

    public void Save()
    {
        var best = new Godot.Collections.Dictionary();
        foreach (var (number, seconds) in BestTimeLeft) best[number.ToString()] = seconds;
        var dict = new Godot.Collections.Dictionary
        {
            ["last"] = LastLevelIndex,
            ["furthest"] = FurthestLevelIndex,
            ["best"] = best,
        };
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file?.StoreString(Json.Stringify(dict, "  "));
    }
}
