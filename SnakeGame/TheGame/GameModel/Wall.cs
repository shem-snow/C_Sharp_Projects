using Newtonsoft.Json;
using SnakeGame;
using System;
using System.Text.Json.Serialization;


/// <summary>
/// Walls will always be axis-aligned.
/// 
/// One sector of a wall has width fifty.
/// 
/// Walls can overlap and intersect each other.
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public class Wall
{
    [JsonProperty(PropertyName = "wall")]
    public int id { get; set; }           // Wall's unique ID
    [JsonProperty(PropertyName = "p1")]
    public Vector2D p1 { get; private set; } = new();       // endpoint
    [JsonProperty(PropertyName = "p2")]
    public Vector2D p2 { get; private set; } = new();       // endpoint

}
