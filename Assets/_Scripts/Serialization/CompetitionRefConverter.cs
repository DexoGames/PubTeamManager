using System;
using Newtonsoft.Json;

/// <summary>
/// Serializes Competition references as Id (int).
/// On read, resolves via FixturesManager.Instance.GetCompetition(id).
/// Apply with [JsonConverter(typeof(CompetitionRefConverter))] on Competition fields.
/// </summary>
public class CompetitionRefConverter : JsonConverter<Competition>
{
    public override void WriteJson(JsonWriter writer, Competition value, JsonSerializer serializer)
    {
        writer.WriteValue(value != null ? value.Id : -1);
    }

    public override Competition ReadJson(JsonReader reader, Type objectType, Competition existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        int id = Convert.ToInt32(reader.Value);
        if (id < 0) return null;

        return FixturesManager.Instance.GetCompetition(id);
    }
}
