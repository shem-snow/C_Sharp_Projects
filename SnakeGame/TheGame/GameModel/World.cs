using NetworkUtil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SnakeGame;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

/// <summary>
/// Represents all of the objects in the world and what direction the player is moving
/// </summary>
public class World
{
    public Dictionary<int, Snake> snakes { get; private set; }       // All snakes to be drawn each frame
    public Dictionary<int, Wall> walls;                              // All walls to be drawn on initialization
    public Dictionary<int, PowerUp> powerups { get; private set; }   // All powerups to be drawn each frame
    public int worldSize;                   // Size of each side of the world; the world is square
    public int playerID;                    // This client's snakes player ID
    public int MaxPowerups { get; private set; } = 20;   // Max amount of powerups aloud in the world

    // Construct an empty world
    public World()
    {
        // Initialize params
        snakes = new();
        powerups = new();
        walls = new();
    }

    /// <summary>
    /// Updates the state of the world by adding and removing snakes or powerups
    /// 
    /// Should only be called by the game controller
    /// </summary>
    /// <param name="data"></param>
    public void UpdateWorld(string raw)
    {
        List<Snake> tmpSnakes = new();
        List<PowerUp> tmpPowerups = new();
        string[] data = Regex.Split(raw, "\n");

        // Parse the data
        foreach (string str in data)
        {
            // Skip non-JSON strings
            if (!(str.StartsWith("{") && str.EndsWith("}")))
            {
                continue;
            }

            // Parse the string into a JSON object
            JObject obj = JObject.Parse(str);
            JToken? token;
            // Check if this object is a snake
            token = obj["snake"];
            if (token != null)
            {
                // Deserialize the snake
                Snake s = JsonConvert.DeserializeObject<Snake>(str)!;
                // Document the snake in a temporary list
                tmpSnakes.Add(s);
                continue;
            }
            // Check if this object is a powerup
            token = obj["power"];
            if (token != null)
            {
                // Deserialize the powerup
                PowerUp p = JsonConvert.DeserializeObject<PowerUp>(str)!;
                // Document the powerup in a temporary list
                tmpPowerups.Add(p);
            }
        }

        // Update the positions of objects in the world
        lock (this)
        {
            // Document the snakes in the world
            foreach (Snake s in tmpSnakes)
            {
                if (snakes.ContainsKey(s.id))
                {
                    if (s.dc)
                    {   // Removes disconnected snakes
                        snakes.Remove(s.id);
                    }
                    else
                    {
                        snakes[s.id] = s;
                    }
                }
                else
                {
                    snakes.Add(s.id, s);
                }
            }
            // Document the powerups in the world
            foreach (PowerUp p in tmpPowerups)
            {
                if (!powerups.ContainsKey(p.id))
                {
                    powerups.Add(p.id, p);
                }
                else
                {
                    if (p.died)
                    {   // Remove collected powerups
                        powerups.Remove(p.id);
                    }
                }
                continue;
            }
        }
    }
}
