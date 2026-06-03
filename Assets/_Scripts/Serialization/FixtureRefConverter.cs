using System;
using Newtonsoft.Json;

/// <summary>
/// Serializes Fixture references as Id (int).
/// On read, resolves via FixturesManager.Instance.GetFixture(id).
/// Apply with [JsonConverter(typeof(FixtureRefConverter))] on Fixture *reference* fields
/// (not on owned-inline fixture lists like Competition.Fixtures).
/// </summary>
public class FixtureRefConverter : JsonConverter<Fixture>
{
    public override void WriteJson(JsonWriter writer, Fixture value, JsonSerializer serializer)
    {
        writer.WriteValue(value != null ? value.Id : -1);
    }

    public override Fixture ReadJson(JsonReader reader, Type objectType, Fixture existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        int id = Convert.ToInt32(reader.Value);
        if (id < 0) return null;

        return FixturesManager.Instance.GetFixture(id);
    }
}
