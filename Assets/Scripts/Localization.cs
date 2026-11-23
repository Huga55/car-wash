using System.Collections.Generic;
using UnityEngine;

/// <summary>Language selection. Russian is the shipped language; the dictionary
/// structure makes adding another language a data change, not a UI rewrite.</summary>
public enum GameLanguage
{
    Russian
}

/// <summary>Every visible string lives here so the game can never leak English UI.</summary>
public static class Loc
{
    public const string Play = "play";
    public const string Cars = "cars";
    public const string Exit = "exit";
    public const string SoundOn = "sound_on";
    public const string SoundOff = "sound_off";
    public const string HomeHint = "home_hint";
    public const string Collection = "collection";
    public const string PickCar = "pick_car";
    public const string Back = "back";
    public const string Choose = "choose";
    public const string Locked = "locked";
    public const string Progress = "progress";
    public const string Level = "level";
    public const string Unlocked = "unlocked";
    public const string AllDone = "all_done";
    public const string Pause = "pause";
    public const string Resume = "resume";
    public const string Restart = "restart";
    public const string ToMenu = "to_menu";
    public const string ExitTitle = "exit_title";
    public const string Stay = "stay";
    public const string Quit = "quit";
    public const string LevelComplete = "level_complete";
    public const string NextCar = "next_car";
    public const string Wet = "wet";
    public const string Foam = "foam";
    public const string Rinse = "rinse";
    public const string Scrub = "scrub";
    public const string Dry = "dry";
    public const string Tool = "tool";
    public const string Great = "great";
    public const string Sparkle = "sparkle";
    public const string Complete = "complete";
    public const string CarLabel = "car_label";
    public const string Washer = "washer";
    public const string Skip = "skip";
    public const string UpdateCheck = "update_check";
    public const string UpdateChecking = "update_checking";
    public const string UpdateLatest = "update_latest";
    public const string UpdateAvailable = "update_available";
    public const string UpdateError = "update_error";
    public const string UpdateTitle = "update_title";
    public const string UpdateVersion = "update_version";
    public const string UpdateNoNeed = "update_noneed";
    public const string UpdateInstall = "update_install";
    public const string UpdateLater = "update_later";
    public const string UpdateDownloading = "update_downloading";
    public const string UpdateBrowserHint = "update_browser_hint";
    public const string UpdateCheckError = "update_check_error";
    public const string UpdateInstallError = "update_install_error";
    public const string UpdatePlatformError = "update_platform_error";
    public const string UpdateOpenPage = "update_open_page";

    private static readonly Dictionary<GameLanguage, Dictionary<string, string>> Languages =
        new Dictionary<GameLanguage, Dictionary<string, string>>
        {
            {
                GameLanguage.Russian, new Dictionary<string, string>
                {
                    { Play, "ИГРАТЬ" },
                    { Cars, "МАШИНЫ" },
                    { Exit, "ВЫХОД" },
                    { SoundOn, "ЗВУК: ВКЛ." },
                    { SoundOff, "ЗВУК: ВЫКЛ." },
                    { HomeHint, "Веди пальцем или мышью по машинке" },
                    { Collection, "МОЯ КОЛЛЕКЦИЯ" },
                    { PickCar, "Выбери машинку" },
                    { Back, "НАЗАД" },
                    { Choose, "ВЫБРАТЬ" },
                    { Locked, "ЗАБЛОКИРОВАНО" },
                    { Progress, "ПРОЙДЕНО" },
                    { Level, "УРОВЕНЬ" },
                    { Unlocked, "НОВАЯ МАШИНКА РАЗБЛОКИРОВАНА!" },
                    { AllDone, "ВСЕ МАШИНКИ ЧИСТЫЕ!" },
                    { Pause, "ПАУЗА" },
                    { Resume, "ПРОДОЛЖИТЬ" },
                    { Restart, "ЕЩЁ РАЗ" },
                    { ToMenu, "В МЕНЮ" },
                    { ExitTitle, "Ты точно хочешь уйти?" },
                    { Stay, "ОСТАТЬСЯ" },
                    { Quit, "ВЫЙТИ" },
                    { LevelComplete, "УРОВЕНЬ ПРОЙДЕН!" },
                    { NextCar, "СЛЕДУЮЩАЯ МАШИНА" },
                    { Wet, "Давай намочим машинку!" },
                    { Foam, "Теперь добавим немного пены!" },
                    { Rinse, "Отлично! Теперь смоем пену!" },
                    { Scrub, "Потрём губкой упрямые места!" },
                    { Dry, "Последний штрих — сделаем машинку блестящей!" },
                    { Tool, "ИНСТРУМЕНТ" },
                    { Great, "Ура! Получается!" },
                    { Sparkle, "Так сияет!" },
                    { Complete, "ЧИСТАЯ МАШИНКА" },
                    { CarLabel, "МАШИНКА" },
                    { Washer, "ЮНЫЙ МОЙЩИК" },
                    { Skip, "ПРОПУСТИТЬ →" },
                    { UpdateCheck, "ПРОВЕРИТЬ ОБНОВЛЕНИЯ" },
                    { UpdateChecking, "ПРОВЕРКА..." },
                    { UpdateLatest, "ВЕРСИЯ АКТУАЛЬНА" },
                    { UpdateAvailable, "ОБНОВЛЕНИЕ ДОСТУПНО" },
                    { UpdateError, "ПОВТОРИТЬ ПРОВЕРКУ" },
                    { UpdateTitle, "ОБНОВЛЕНИЕ ИГРЫ" },
                    { UpdateVersion, "Доступна версия {0}" },
                    { UpdateNoNeed, "У тебя уже последняя версия игры." },
                    { UpdateInstall, "ОБНОВИТЬ" },
                    { UpdateLater, "ПОЗЖЕ" },
                    { UpdateDownloading, "Загрузка обновления..." },
                    { UpdateBrowserHint, "Откроется браузер. Нажми «Установить», чтобы обновить игру." },
                    { UpdateCheckError, "Не удалось проверить обновление. Проверь интернет." },
                    { UpdateInstallError, "Не получилось обновиться. Открой страницу выпуска в браузере." },
                    { UpdatePlatformError, "Обновление для этой платформы пока недоступно." },
                    { UpdateOpenPage, "ОТКРЫТЬ СТРАНИЦУ" }
                }
            }
        };

    private static GameLanguage _language = GameLanguage.Russian;

    public static GameLanguage Language
    {
        get { return _language; }
        set
        {
            if (Languages.ContainsKey(value)) _language = value;
        }
    }

    public static string T(string key)
    {
        Dictionary<string, string> words;
        if (Languages.TryGetValue(_language, out words))
        {
            string value;
            if (words.TryGetValue(key, out value)) return value;
        }
        return words == null ? key : key;
    }

    public static string StageInstruction(WashStage stage)
    {
        switch (stage)
        {
            case WashStage.Wet: return T(Wet);
            case WashStage.Foam: return T(Foam);
            case WashStage.Rinse: return T(Rinse);
            case WashStage.Scrub: return T(Scrub);
            case WashStage.Dry: return T(Dry);
            default: return T(Sparkle);
        }
    }

    /// <summary>Compact labels for the vertical stage list in the play HUD.</summary>
    public static string StageShort(WashStage stage)
    {
        switch (stage)
        {
            case WashStage.Wet: return "ВОДА";
            case WashStage.Foam: return "ПЕНА";
            case WashStage.Scrub: return "ГУБКА";
            case WashStage.Rinse: return "СМЫВ";
            case WashStage.Dry: return "СУШКА";
            default: return T(Tool);
        }
    }

    public static string StageTool(WashStage stage)
    {
        switch (stage)
        {
            case WashStage.Wet: return "ШЛАНГ";
            case WashStage.Foam: return "ПЕНА";
            case WashStage.Rinse: return "ШЛАНГ";
            case WashStage.Scrub: return "ГУБКА";
            case WashStage.Dry: return "ФЕН";
            default: return T(Tool);
        }
    }

    public static string DriverThanks(int level)
    {
        string[] lines =
        {
            "Ура! Какая чистая машинка!",
            "Спасибо! Ты настоящий мастер!",
            "Вау! Теперь я сияю!",
            "Спасибо за блеск!",
            "Какая чудесная мойка!",
            "Я так доволен!",
            "Моя машинка как новая!",
            "Ты отлично помыл!",
            "Спасибо, друг!",
            "Едем кататься!"
        };
        return lines[Mathf.Abs(level) % lines.Length];
    }
}
