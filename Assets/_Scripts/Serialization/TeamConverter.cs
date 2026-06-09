using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Custom converter for Team (ScriptableObject).
/// Writes all serializable Team fields. On read, creates a new ScriptableObject instance.
/// Players and Manager are serialized inline (owned by Team).
///
/// Registers each team into TeamManager as it's read, so TeamRefConverter can resolve
/// team references mid-deserialization (Teams deserialize before Competitions).
/// </summary>
public class TeamConverter : JsonConverter<Team>
{
    public override void WriteJson(JsonWriter writer, Team value, JsonSerializer serializer)
    {
        if (value == null) { writer.WriteNull(); return; }

        writer.WriteStartObject();

        writer.WritePropertyName("TeamId");
        writer.WriteValue(value.TeamId);

        writer.WritePropertyName("Name");
        writer.WriteValue(value.Name);

        writer.WritePropertyName("YearFounded");
        writer.WriteValue(value.YearFounded);

        writer.WritePropertyName("TeamColor");
        serializer.Serialize(writer, new float[] { value.TeamColor.r, value.TeamColor.g, value.TeamColor.b, value.TeamColor.a });

        writer.WritePropertyName("AwayColor");
        serializer.Serialize(writer, new float[] { value.AwayColor.r, value.AwayColor.g, value.AwayColor.b, value.AwayColor.a });

        writer.WritePropertyName("Players");
        serializer.Serialize(writer, value.Players);

        writer.WritePropertyName("Manager");
        serializer.Serialize(writer, value.Manager);

        writer.WritePropertyName("Stats");
        serializer.Serialize(writer, value.Stats);

        writer.WriteEndObject();
    }

    public override Team ReadJson(JsonReader reader, Type objectType, Team existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;

        JObject obj = JObject.Load(reader);

        Team team = ScriptableObject.CreateInstance<Team>();

        team.Name = obj["Name"]?.ToString() ?? "";
        team.SetTeamId(obj["TeamId"]?.Value<int>() ?? 0);
        team.YearFounded = obj["YearFounded"]?.Value<int>() ?? 0;

        // Register early so TeamRefConverter can resolve during fixture deserialization
        TeamManager.Instance.RegisterTeamDuringLoad(team);

        // Color
        var colorArr = obj["TeamColor"]?.ToObject<float[]>();
        if (colorArr != null && colorArr.Length >= 4)
            team.TeamColor = new Color(colorArr[0], colorArr[1], colorArr[2], colorArr[3]);

        var awayArr = obj["AwayColor"]?.ToObject<float[]>();
        if (awayArr != null && awayArr.Length >= 4)
            team.AwayColor = new Color(awayArr[0], awayArr[1], awayArr[2], awayArr[3]);

        // Players — deserialize inline, then assign Team reference
        var playersToken = obj["Players"];
        if (playersToken != null)
        {
            team.Players = playersToken.ToObject<List<Player>>(serializer) ?? new List<Player>();
            foreach (var player in team.Players)
            {
                player.Team = team;
                PersonManager.Instance.RegisterExisting(player);
            }
        }

        // Manager
        var managerToken = obj["Manager"];
        if (managerToken != null && managerToken.Type != JTokenType.Null)
        {
            Manager manager = managerToken.ToObject<Manager>(serializer);
            if (manager != null)
            {
                manager.Team = team;
                // RegisterExisting (not RegisterPerson) so the manager keeps its saved PersonID —
                // RegisterPerson would allocate a fresh ID on every load, breaking manager refs
                // and inflating the person ID counter.
                PersonManager.Instance.RegisterExisting(manager);

                // Rebuild Instructions from Template
                if (manager.ManStats.Template != null)
                    manager.ManStats.Instructions = manager.ManStats.Template.Instructions;

                // Stats
                ClubStats stats = obj["Stats"]?.ToObject<ClubStats>(serializer) ?? new ClubStats();

                team.RestoreTeamState(manager, stats);
            }
        }

        return team;
    }
}
