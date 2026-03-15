using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CardDisableControl.Scripts.Runtime;

internal sealed class CardDisableControlBanSettings
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("bannedCards")]
    public List<string> BannedCards { get; init; } = new();
}

