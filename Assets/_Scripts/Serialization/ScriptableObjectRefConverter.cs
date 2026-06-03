using System;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Serializes ScriptableObject references (Formation, TacticTemplate, EventType)
/// as their asset name string. Resolves via Resources.Load on read.
/// 
/// Usage: [JsonConverter(typeof(ScriptableObjectRefConverter), "Formations/Usable")]
/// The constructor argument is the Resources folder path.
/// </summary>
public class ScriptableObjectRefConverter : JsonConverter
{
    private readonly string resourcePath;
    private readonly Type soType;

    public ScriptableObjectRefConverter(Type type, string resourcePath)
    {
        this.soType = type;
        this.resourcePath = resourcePath;
    }

    public override bool CanConvert(Type objectType)
    {
        return typeof(ScriptableObject).IsAssignableFrom(objectType);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var so = value as ScriptableObject;
        writer.WriteValue(so != null ? so.name : "");
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        string name = reader.Value?.ToString();
        if (string.IsNullOrEmpty(name)) return null;

        // Load all SOs from the resource path and find by name
        var allAssets = Resources.LoadAll(resourcePath, objectType);
        foreach (var asset in allAssets)
        {
            if (asset.name == name) return asset;
        }

        Debug.LogWarning($"[Save] Could not find {objectType.Name} '{name}' in Resources/{resourcePath}");
        return null;
    }
}
