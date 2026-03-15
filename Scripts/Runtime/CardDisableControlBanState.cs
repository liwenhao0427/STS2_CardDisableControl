using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Entities.Players;

namespace CardDisableControl.Scripts.Runtime;

internal static class CardDisableControlBanState
{
    private const int SchemaVersion = 1;

    private static readonly object SyncRoot = new();
    private static readonly HashSet<string> BannedCardKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static bool _initialized;

    public static event Action<string, bool>? BanStateChanged;

    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            LoadInternal();
            _initialized = true;
        }
    }

    public static bool IsBanned(CardModel? card)
    {
        string? key = GetCardKey(card);
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        lock (SyncRoot)
        {
            return BannedCardKeys.Contains(key);
        }
    }

    public static bool Toggle(CardModel card, string reason)
    {
        string? key = GetCardKey(card);
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        bool newState;
        bool changed;
        lock (SyncRoot)
        {
            EnsureInitializedUnsafe();
            newState = !BannedCardKeys.Contains(key);
            changed = SetBannedUnsafe(key, newState);
        }

        if (changed)
        {
            CardDisableControlLogger.Info($"已通过 {reason} {(newState ? "禁用" : "解禁")} 卡牌: {key}");
            BanStateChanged?.Invoke(key, newState);
        }

        return newState;
    }

    public static bool SetBanned(CardModel card, bool banned, string reason)
    {
        string? key = GetCardKey(card);
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        bool changed;
        lock (SyncRoot)
        {
            EnsureInitializedUnsafe();
            changed = SetBannedUnsafe(key, banned);
        }

        if (changed)
        {
            CardDisableControlLogger.Info($"已通过 {reason} {(banned ? "禁用" : "解禁")} 卡牌: {key}");
            BanStateChanged?.Invoke(key, banned);
        }

        return banned;
    }

    public static IEnumerable<CardModel> FilterCardsWithFallback(IEnumerable<CardModel> sourceCards, string context)
    {
        List<CardModel> source = sourceCards.ToList();
        if (source.Count == 0)
        {
            return source;
        }

        List<CardModel> filtered = source.Where((CardModel card) => !IsBanned(card)).ToList();
        if (filtered.Count == 0)
        {
            CardDisableControlLogger.Info($"{context} 过滤后候选卡池为空，已回退到原卡池。");
            return source;
        }

        if (filtered.Count != source.Count)
        {
            CardDisableControlLogger.Info($"{context} 已过滤禁用卡牌: {source.Count - filtered.Count} 张。");
        }

        return filtered;
    }

    public static CardCreationOptions ApplyToCreationOptions(CardCreationOptions options, Player player, string context)
    {
        List<CardModel> source = options.GetPossibleCards(player).ToList();
        if (source.Count == 0)
        {
            return options;
        }

        List<CardModel> filtered = source.Where((CardModel card) => !IsBanned(card)).ToList();
        if (filtered.Count == 0)
        {
            CardDisableControlLogger.Info($"{context} 过滤后候选卡池为空，已回退到原卡池。");
            return options;
        }

        if (filtered.Count == source.Count)
        {
            return options;
        }

        CardRarityOddsType rarityOdds = options.RarityOdds;
        if (rarityOdds != CardRarityOddsType.Uniform)
        {
            CardRarity? singleRarity = TryGetSingleRarity(filtered);
            if (singleRarity.HasValue)
            {
                rarityOdds = CardRarityOddsType.Uniform;
            }
        }

        CardCreationOptions newOptions = new CardCreationOptions(filtered, options.Source, rarityOdds).WithFlags(options.Flags);
        if (options.RngOverride != null)
        {
            newOptions.WithRngOverride(options.RngOverride);
        }

        CardDisableControlLogger.Info($"{context} 已过滤禁用卡牌: {source.Count - filtered.Count} 张。");
        return newOptions;
    }

    public static string? GetCardKey(CardModel? card)
    {
        if (card == null)
        {
            return null;
        }

        return card.CanonicalInstance.Id.ToString();
    }

    private static bool SetBannedUnsafe(string key, bool banned)
    {
        bool changed;
        if (banned)
        {
            changed = BannedCardKeys.Add(key);
        }
        else
        {
            changed = BannedCardKeys.Remove(key);
        }

        if (changed)
        {
            SaveInternal();
        }

        return changed;
    }

    private static void EnsureInitializedUnsafe()
    {
        if (_initialized)
        {
            return;
        }

        LoadInternal();
        _initialized = true;
    }

    private static void LoadInternal()
    {
        string settingsPath = GetSettingsPath();
        try
        {
            string? directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(settingsPath))
            {
                CardDisableControlLogger.Info($"未找到禁用配置文件，使用默认配置: {settingsPath}");
                BannedCardKeys.Clear();
                return;
            }

            string json = File.ReadAllText(settingsPath);
            CardDisableControlBanSettings? settings = JsonSerializer.Deserialize<CardDisableControlBanSettings>(json, JsonOptions);
            BannedCardKeys.Clear();

            if (settings?.BannedCards != null)
            {
                foreach (string key in settings.BannedCards.Where((string item) => !string.IsNullOrWhiteSpace(item)))
                {
                    BannedCardKeys.Add(key.Trim());
                }
            }

            if (settings != null && settings.SchemaVersion != SchemaVersion)
            {
                CardDisableControlLogger.Warn($"检测到旧配置版本 {settings.SchemaVersion}，已按兼容方式读取。");
            }

            CardDisableControlLogger.Info($"禁用配置加载完成，当前禁用 {BannedCardKeys.Count} 张卡。");
        }
        catch (Exception exception)
        {
            CardDisableControlLogger.Error($"读取禁用配置失败: {exception}");
            BannedCardKeys.Clear();
        }
    }

    private static void SaveInternal()
    {
        string settingsPath = GetSettingsPath();
        try
        {
            string? directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            CardDisableControlBanSettings settings = new()
            {
                SchemaVersion = SchemaVersion,
                BannedCards = BannedCardKeys.OrderBy((string key) => key, StringComparer.Ordinal).ToList()
            };

            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(settingsPath, json);
        }
        catch (Exception exception)
        {
            CardDisableControlLogger.Error($"写入禁用配置失败: {exception}");
        }
    }

    private static CardRarity? TryGetSingleRarity(IEnumerable<CardModel> cards)
    {
        CardRarity? rarity = null;
        foreach (CardModel card in cards)
        {
            if (!rarity.HasValue)
            {
                rarity = card.Rarity;
                continue;
            }

            if (rarity.Value != card.Rarity)
            {
                return null;
            }
        }

        return rarity;
    }

    private static string GetSettingsPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            return Path.Combine(appData, "SlayTheSpire2", "mods", "CardDisableControl", "settings.json");
        }

        return Path.Combine("C:\\", "SlayTheSpire2", "mods", "CardDisableControl", "settings.json");
    }
}
