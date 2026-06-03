using System;
using Newtonsoft.Json;

/// <summary>
/// Serializes Person/Player/Manager references as PersonID (int).
/// On read, resolves via PersonManager.Instance.GetPerson(id).
/// Apply with [JsonConverter(typeof(PersonRefConverter))] on Person fields.
/// </summary>
public class PersonRefConverter : JsonConverter<Person>
{
    public override void WriteJson(JsonWriter writer, Person value, JsonSerializer serializer)
    {
        writer.WriteValue(value != null ? value.PersonID : -1);
    }

    public override Person ReadJson(JsonReader reader, Type objectType, Person existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        int id = Convert.ToInt32(reader.Value);
        if (id < 0) return null;

        return PersonManager.Instance.GetPerson(id);
    }
}
