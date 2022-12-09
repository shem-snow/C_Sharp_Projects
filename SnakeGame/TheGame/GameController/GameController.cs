using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using NetworkUtil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Controller for a SnakeClient game
/// </summary>
public class GameController
{
    #region View Controls
    private Action UpdateArrived;       // Notifies the view that an update came from the server
    private Action<SocketState> ErrorOccurred;             // Did this client succesfully connect?
    #endregion
    #region Client Params
    private string playerName;
    private World theWorld;
    #endregion
    #region Control Commands
    private bool clientPressedCommand = false;
    private string moving;              // What direction the player is moving in
    #endregion

    /// <summary>
    /// Creates a game controller with the argued method for drawing the world
    /// </summary>
    /// <param name="updateArrived"></param>
    public GameController(Action updateArrived, Action<SocketState> errorOccurred, World w)
    {
        UpdateArrived = updateArrived;
        ErrorOccurred = errorOccurred;
        moving = "none";
        playerName = "";    // temporary value
        theWorld = w;
    }


    /// <summary>
    /// Connects to the argued server's host name on port 11000
    /// 
    /// Returns whether or not the connection was succesful
    /// </summary>
    /// <param name="hostName"></param>
    public void Connect(string hostName, string playerName)
    {
        this.playerName = playerName;
        Networking.ConnectToServer(OnConnection, hostName, 11000);
    }

    /// <summary>
    /// Queues a request to the server to update the player's control command
    /// </summary>
    /// <param name="dir"></param>
    public void MoveCommand(string dir)
    {
        moving = dir;
        clientPressedCommand = true;
    }

    /// <summary>
    /// Handles connections to the server.
    /// 
    /// 1. Documents and draws the walls 
    /// 2. Starts event-looping for control commands
    /// 3. Begins updating the client each frame
    /// </summary>
    /// <param name="state"></param>
    private void OnConnection(SocketState state)
    {
        if (state.ErrorOccurred)
        {
            ErrorOccurred.Invoke(state);
            return;
        }

        // Send the player name to the server
        if (Networking.Send(state.TheSocket, playerName))
        {
            // Receive the playerID and worldSize
            state.OnNetworkAction = GetPlayerIDAndWorldSize;
            Networking.GetData(state);
        }
    }

    private void GetPlayerIDAndWorldSize(SocketState state)
    {
        // Document the player ID and world size
        string raw = state.GetData();
        state.RemoveData(0, raw.Length - 1);
        string[] data = Regex.Split(raw, "\n");
        theWorld.playerID = int.Parse(data[0]);
        theWorld.worldSize = int.Parse(data[1]);

        // Allow the server to start updating the walls
        if (data.Length > 3)
        {   // The server is already sending the walls
            DocumentWalls(data);
            // Notify the View that the walls have arrived from the server
            UpdateArrived.Invoke();

            // Allow the server to start populating the world
            state.OnNetworkAction = OnFrame;
            Networking.GetData(state);
        }
        else
        {   // This is the first connection and hasn't been sent walls
            state.OnNetworkAction = GetWalls;
            Networking.GetData(state);
        }
    }

    private void DocumentWalls(string[] data)
    {
        lock (theWorld)
        {
            foreach (string str in data)
            {
                // Skip non-json strings
                if (!str.StartsWith('{') && !str.EndsWith('}'))
                {
                    continue;
                }

                // Parse the wall as a Json object
                JObject obj = JObject.Parse(str);
                JToken? token = obj["wall"];
                if (token != null)
                {   // Document the wall in the world
                    Wall w = JsonConvert.DeserializeObject<Wall>(str)!;
                    theWorld.walls.Add(w.id, w);
                }
            }
        }
    }

    private void GetWalls(SocketState state)
    {
        // Receive walls from the server
        string raw = state.GetData();
        state.RemoveData(0, raw.Length - 1);
        string[] data = Regex.Split(raw, "\n");
        DocumentWalls(data);
        // Notify the View that the walls have arrived from the server
        UpdateArrived.Invoke();

        // Allow the server to start populating the world
        state.OnNetworkAction = OnFrame;
        Networking.GetData(state);
    }

    /// <summary>
    /// Method to be called by the server on each frame
    /// </summary>
    /// <param name="state"></param>
    private void OnFrame(SocketState state)
    {
        // Only one command may be received each frame
        if (clientPressedCommand)
        {
            string controlCommand = "{\"moving\":\"" + moving + "\"}\n";
            Networking.Send(state.TheSocket, controlCommand);
            clientPressedCommand = false;
        }

        // Update the values in the world
        Networking.GetData(state);
        string raw = state.GetData();
        state.RemoveData(0, raw.Length - 2);
        theWorld.UpdateWorld(raw);
        // Notify the View that an update has arrived from the server
        UpdateArrived.Invoke();
    }

}
