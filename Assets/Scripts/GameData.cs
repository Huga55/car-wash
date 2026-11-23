using UnityEngine;

/// <summary>Every car family is drawn procedurally, so no 3D models or image
/// files are needed. New shapes only require a new enum value and a painter.</summary>
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
    SchoolBus,
    FireTruck,
    Tractor,
    Excavator,
    Bulldozer,
    DumpTruck,
    Mixer,
    GarbageTruck,
    IceCream,
    Limo,
    MonsterTruck,
    Van
}

public enum DirtKind
{
    Mud,
    Dust,
    Sand,
    Puddle,
    Leaves,
    Snow
}

public enum AnimalKind
{
    Cat,
    Dog,
    Bear,
    Hare,
    Fox,
    Panda,
    Raccoon,
    Frog,
    Lion,
    Elephant
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
    public readonly DirtKind DirtType;
    public readonly AnimalKind Driver;
    public readonly bool UseScrub;

    public LevelDef(int index, string name, CarKind kind, Color body, Color accent, float size,
        float dirt, int theme, DirtKind dirtType, AnimalKind driver, bool useScrub)
    {
        Index = index;
        Name = name;
        Kind = kind;
        Body = body;
        Accent = accent;
        Size = size;
        Dirt = dirt;
        Theme = theme;
        DirtType = dirtType;
        Driver = driver;
        UseScrub = useScrub;
    }
}

public static class GameData
{
    public const int LevelCount = 30;

    // First 20 levels cover every transport type requested in the design brief.
    public static readonly CarKind[] Kinds =
    {
        CarKind.Sedan, CarKind.Sports, CarKind.Taxi, CarKind.Police, CarKind.FireTruck,
        CarKind.Ambulance, CarKind.DumpTruck, CarKind.Excavator, CarKind.Bulldozer, CarKind.Tractor,
        CarKind.Bus, CarKind.SchoolBus, CarKind.Van, CarKind.Limo, CarKind.GarbageTruck,
        CarKind.Mixer, CarKind.Beetle, CarKind.MonsterTruck, CarKind.IceCream, CarKind.Race,
        CarKind.Sedan, CarKind.Taxi, CarKind.Police, CarKind.Ambulance, CarKind.Bus,
        CarKind.FireTruck, CarKind.Tractor, CarKind.DumpTruck, CarKind.Mixer, CarKind.Limo
    };

    private static readonly string[] Names =
    {
        "Маленькая машинка", "Спортивная машина", "Такси", "Полицейская машина", "Пожарная машина",
        "Скорая помощь", "Самосвал", "Экскаватор", "Бульдозер", "Трактор",
        "Автобус", "Школьный автобус", "Микроавтобус", "Лимузин", "Мусоровоз",
        "Бетономешалка", "Жук-машинка", "Монстр-трак", "Фургон мороженого", "Гоночная машина",
        "Голубой друг", "Жёлтое такси", "Ночной патруль", "Скорая на помощь", "Большой автобус",
        "Огненный грузовик", "Фермерский трактор", "Гружёный самосвал", "Миксер на стройке", "Праздничный лимузин"
    };

    private static readonly Color[] Bodies =
    {
        new Color32( 72, 156, 255, 255), new Color32(255,  92,  92, 255),
        new Color32(255, 214,  64, 255), new Color32( 74, 118, 220, 255),
        new Color32(232,  66,  62, 255), new Color32(250, 250, 250, 255),
        new Color32(255, 150,  56, 255), new Color32(255, 190,  48, 255),
        new Color32(240, 172,  52, 255), new Color32( 92, 178,  86, 255),
        new Color32(255, 176,  48, 255), new Color32(255, 204,  56, 255),
        new Color32(128, 200, 235, 255), new Color32( 70,  78,  96, 255),
        new Color32(118, 196, 110, 255), new Color32( 96, 170, 210, 255),
        new Color32(255, 196,  64, 255), new Color32(206,  78, 200, 255),
        new Color32(255, 236, 196, 255), new Color32( 96, 214, 130, 255)
    };

    private static readonly Color[] Accents =
    {
        new Color32( 40,  92, 170, 255), new Color32(255, 232, 120, 255),
        new Color32( 48,  48,  52, 255), new Color32(250, 250, 250, 255),
        new Color32(255, 238, 210, 255), new Color32(236,  78,  78, 255),
        new Color32(120,  92,  64, 255), new Color32( 76,  76,  84, 255),
        new Color32(110,  92,  60, 255), new Color32(255, 214,  80, 255),
        new Color32( 62, 110, 190, 255), new Color32( 52, 100, 170, 255),
        new Color32( 70, 130, 180, 255), new Color32(220, 190,  90, 255),
        new Color32( 70, 120,  80, 255), new Color32(240, 240, 240, 255),
        new Color32(255, 126,  70, 255), new Color32(255, 220,  80, 255),
        new Color32(255, 140, 170, 255), new Color32( 46, 130,  90, 255)
    };

    private static readonly float[] Sizes = { 0.74f, 0.82f, 0.90f, 1.00f };
    private static readonly int[] Themes = { 0, 1, 2, 3, 4 };
    private static readonly DirtKind[] DirtTypes =
    {
        DirtKind.Mud, DirtKind.Puddle, DirtKind.Dust,
        DirtKind.Sand, DirtKind.Leaves, DirtKind.Snow
    };
    private static readonly AnimalKind[] Drivers =
    {
        AnimalKind.Cat, AnimalKind.Dog, AnimalKind.Bear, AnimalKind.Hare, AnimalKind.Fox,
        AnimalKind.Panda, AnimalKind.Raccoon, AnimalKind.Frog, AnimalKind.Lion, AnimalKind.Elephant
    };

    public static readonly LevelDef[] Levels = BuildLevels();

    private static LevelDef[] BuildLevels()
    {
        var levels = new LevelDef[LevelCount];
        for (int i = 0; i < LevelCount; i++)
        {
            CarKind kind = Kinds[i];
            float size = Sizes[(i / 2) % Sizes.Length];
            if (kind == CarKind.Bus || kind == CarKind.SchoolBus || kind == CarKind.FireTruck ||
                kind == CarKind.Limo || kind == CarKind.GarbageTruck || kind == CarKind.DumpTruck ||
                kind == CarKind.Mixer || kind == CarKind.Excavator || kind == CarKind.Bulldozer)
                size = 1f;

            float dirt = 0.70f + ((i * 7) % 5) * 0.05f;
            levels[i] = new LevelDef(
                i, Names[i], kind, Bodies[i % Bodies.Length], Accents[i % Accents.Length],
                size, dirt, Themes[(i / 6) % Themes.Length], DirtTypes[(i / 3) % DirtTypes.Length],
                Drivers[i % Drivers.Length], i >= 3);
        }
        return levels;
    }

    /// <summary>Stages follow the design brief: wet, foam, scrub, rinse, dry.
    /// The sponge is introduced after the first three easy cars so the first
    /// level stays as simple as possible.</summary>
    public static WashStage[] StagesFor(int level)
    {
        bool scrub = level >= 0 && level < LevelCount && Levels[level].UseScrub;
        return scrub
            ? new[] { WashStage.Wet, WashStage.Foam, WashStage.Scrub, WashStage.Rinse, WashStage.Dry }
            : new[] { WashStage.Wet, WashStage.Foam, WashStage.Rinse, WashStage.Dry };
    }

    public static string StarsKey(int level) { return "lwc_stars_" + level; }
    public static string CompletedKey(int level) { return "lwc_done_" + level; }
    public static string UnlockKey { get { return "lwc_unlocked"; } }
    public static string SoundKey { get { return "lwc_sound"; } }
    public static string MuteKey { get { return "lwc_mute"; } }
    public static string MusicKey { get { return "lwc_music"; } }
    public static string SfxKey { get { return "lwc_sfx"; } }
    public static string MasterVolumeKey { get { return "lwc_volume_master"; } }
    public static string MusicVolumeKey { get { return "lwc_volume_music"; } }
    public static string SfxVolumeKey { get { return "lwc_volume_sfx"; } }
}
