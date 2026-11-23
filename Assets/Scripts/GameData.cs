using UnityEngine;

/// <summary>All car families in the game. Every family is drawn procedurally,
/// so no 3D models or image files are needed (great for weak tablets).</summary>
public enum CarKind
{
    Sedan,
    Beetle,
    Sports,
    Race,
    Taxi,
    Police,
    Ambulance,
    Bus,
    FireTruck,
    Tractor,
    DumpTruck,
    Mixer,
    GarbageTruck,
    IceCream,
    Limo,
    MonsterTruck,
    Van
}

public sealed class LevelDef
{
    public readonly int Index;
    public readonly string Name;
    public readonly CarKind Kind;
    public readonly Color Body;
    public readonly Color Accent;
    public readonly float Size;
    public readonly float Dirt;
    public readonly int Theme;

    public LevelDef(int index, string name, CarKind kind, Color body, Color accent, float size, float dirt, int theme)
    {
        Index = index;
        Name = name;
        Kind = kind;
        Body = body;
        Accent = accent;
        Size = size;
        Dirt = dirt;
        Theme = theme;
    }
}

public static class GameData
{
    public const int LevelCount = 30;

    private static readonly CarKind[] Kinds =
    {
        CarKind.Sedan, CarKind.Beetle, CarKind.Sports, CarKind.Race, CarKind.Taxi,
        CarKind.Police, CarKind.Ambulance, CarKind.Bus, CarKind.FireTruck, CarKind.Tractor,
        CarKind.DumpTruck, CarKind.Mixer, CarKind.GarbageTruck, CarKind.IceCream, CarKind.Limo,
        CarKind.MonsterTruck, CarKind.Van
    };

    private static readonly string[] Names =
    {
        "Blue Buddy", "Bouncy Bug", "Zoom Zoom", "Number One", "Yellow Cab",
        "Police Patrol", "Help Machine", "Big Bus", "Fire Hero", "Farm Friend",
        "Dump Truck", "Mixing Day", "Trash Truck", "Ice Cream!", "Long Limo",
        "Monster Wheels", "Little Van", "Red Rocket", "Green Bug", "Super Sports",
        "City Cab", "Night Patrol", "Ambulance", "School Bus", "Fire Truck",
        "Tractor Fun", "Mud Hauler", "Cement Mixer", "Clean-up Crew", "Party Bus"
    };

    private static readonly Color[] Bodies =
    {
        new Color32( 72, 156, 255, 255), new Color32(255, 196,  64, 255),
        new Color32(255,  92,  92, 255), new Color32( 96, 214, 130, 255),
        new Color32(255, 214,  64, 255), new Color32( 74, 118, 220, 255),
        new Color32(250, 250, 250, 255), new Color32(255, 176,  48, 255),
        new Color32(232,  66,  62, 255), new Color32( 92, 178,  86, 255),
        new Color32(255, 150,  56, 255), new Color32( 96, 170, 210, 255),
        new Color32(118, 196, 110, 255), new Color32(255, 236, 196, 255),
        new Color32( 70,  78,  96, 255), new Color32(206,  78, 200, 255),
        new Color32(128, 200, 235, 255)
    };

    private static readonly Color[] Accents =
    {
        new Color32( 40,  92, 170, 255), new Color32(255, 126,  70, 255),
        new Color32(255, 232, 120, 255), new Color32( 46, 130,  90, 255),
        new Color32( 48,  48,  52, 255), new Color32(250, 250, 250, 255),
        new Color32(236,  78,  78, 255), new Color32( 62, 110, 190, 255),
        new Color32(255, 238, 210, 255), new Color32(255, 214,  80, 255),
        new Color32(120,  92,  64, 255), new Color32(240, 240, 240, 255),
        new Color32( 70, 120,  80, 255), new Color32(255, 140, 170, 255),
        new Color32(220, 190,  90, 255), new Color32(255, 220,  80, 255),
        new Color32( 70, 130, 180, 255)
    };

    // 0.74 = little car, 1.0 = big truck / bus.
    private static readonly float[] Sizes = { 0.74f, 0.82f, 0.90f, 1.00f };
    private static readonly int[] Themes = { 0, 1, 2, 3, 4 };

    public static readonly LevelDef[] Levels = BuildLevels();

    private static LevelDef[] BuildLevels()
    {
        var levels = new LevelDef[LevelCount];
        for (int i = 0; i < LevelCount; i++)
        {
            CarKind kind = Kinds[i % Kinds.Length];
            Color body = Bodies[i % Bodies.Length];
            Color accent = Accents[i % Accents.Length];

            // Make later levels a little bit dirtier, but never frustrating.
            float dirt = 0.72f + ((i * 7) % 5) * 0.055f;
            float size = Sizes[(i / 2) % Sizes.Length];
            if (kind == CarKind.Bus || kind == CarKind.FireTruck || kind == CarKind.Limo ||
                kind == CarKind.GarbageTruck || kind == CarKind.DumpTruck || kind == CarKind.Mixer)
                size = 1f;

            levels[i] = new LevelDef(i, Names[i], kind, body, accent, size, dirt, Themes[(i / 6) % Themes.Length]);
        }
        return levels;
    }

    public static string StarsKey(int level) { return "lwc_stars_" + level; }
    public static string SoundKey { get { return "lwc_sound"; } }
}
