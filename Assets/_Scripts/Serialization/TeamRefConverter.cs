using System;
using Newtonsoft.Json;

/// <summary>
/// Serializes Team references as TeamId (int).
/// On read, resolves via TeamManager.Instance.GetTeam(id). Teams are registered into
/// TeamManager during deserialization (by TeamConverter), so references resolve correctly
/// because Teams deserialize before Competitions/fixtures.
/// Apply with [JsonConverter(typeof(TeamRefConverter))] on Team fields.
/// </summary>
public class TeamRefConverter : JsonConverter<Team>
{
    public override void WriteJson(JsonWriter writer, Team value, JsonSerializer serializer)
    {
        writer.WriteValue(value != null ? value.TeamId : -1);
    }

    public override Team ReadJson(JsonReader reader, Type objectType, Team existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        int id = Convert.ToInt32(reader.Value);
        if (id < 0) return null;

        return TeamManager.Instance?.GetTeam(id);
    }
}
