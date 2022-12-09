using Newtonsoft.Json;
using SnakeGame;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net;

/// <summary>
/// The Snake class has all the fields that tie a client to a snake in the game such as id, name, and movement details.
/// ToString is overriden to return the Json serialization of this class.
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public class Snake
{
    #region JSON Properties
    [JsonProperty(PropertyName = "snake")]
    public int id { get; private set; }      // Unique id
    [JsonProperty(PropertyName = "body")]
    public List<Vector2D> body = new();    // represents the entire body; first index tail; last index head
    [JsonProperty(PropertyName = "dir")]
    public Vector2D direction { get; set; } = new();  // Snake's orientation
    [JsonProperty(PropertyName = "name")]
    public string name { get; private set; } = "";  // Player's name
    [JsonProperty(PropertyName = "score")]
    public int score;
    [JsonProperty(PropertyName = "died")]
    public bool died;   // Did the snake die on this frame?
    [JsonProperty(PropertyName = "alive")]
    public bool alive;  // Is this snake alive right now?
    [JsonProperty(PropertyName = "dc")]
    public bool dc;     // Did the snake disconnect on this frame?
    [JsonProperty(PropertyName = "join")]
    public bool join;   // Did the snake join on this frame?
    #endregion
    public int FoodInBelly;       // For when a snake gets a powerup
    public int FramesSpentDead;   // For when a snake is waiting to respawn
    public string MoveRequest = "none";

    /// <summary>
    /// Default constructor for JSON serialization
    /// </summary>
    [JsonConstructor]
    public Snake()
    {
    }

    /// <summary>
    /// Constructor for a new Snake being added server-side
    /// </summary>
    /// <param name="id"></param>
    /// <param name="body">each segment of the snake</param>
    /// <param name="dir">direction the snake is moving in</param>
    /// <param name="name">name of the player controlling this snake</param>
    public Snake(int id, List<Vector2D> body, Vector2D dir, string name)
    {
        this.id = id;
        this.body = body;
        this.direction = dir;
        this.name = name;
        this.alive = true;
        this.join = true;
    }

    /// <summary>
    /// String representation of snake in JSON format
    /// </summary>
    /// <returns>The snake's params in JSON format</returns>
    public override string ToString()
    {
        return JsonConvert.SerializeObject(this);
    }
}
