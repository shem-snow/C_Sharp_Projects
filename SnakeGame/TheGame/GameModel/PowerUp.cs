using Newtonsoft.Json;
using SnakeGame;
using System;

[JsonObject(MemberSerialization.OptIn)]
/// <summary>
/// Consumable item that does something to snakes when 
/// they collide with it.
/// 
/// Powerups are 16x16 pixels
/// </summary>
public class PowerUp
{
    [JsonProperty(PropertyName = "power")]
    public int id { get; private set; }      // unique ID
    [JsonProperty(PropertyName = "loc")]
    public Vector2D loc { get; private set; } = new();  // location in the world
    [JsonProperty(PropertyName = "died")]
    public bool died;   // Did the power-up die on this frame?

    /// <summary>
    /// Default constructor for JSON serialization
    /// </summary>
    [JsonConstructor]
    public PowerUp()
    {
    }

    /// <summary>
    /// Constructor for a newly spawned powerup being added server-side
    /// </summary>
    /// <param name="id"></param>
    /// <param name="loc"></param>
    public PowerUp(int id, Vector2D loc)
    {
        this.id = id;
        this.loc = loc;
        this.died = false;
    }

    /// <summary>
    /// String representation of a powerup in JSON format
    /// </summary>
    /// <returns>This powerup's params in JSON format</returns>
    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }
}
