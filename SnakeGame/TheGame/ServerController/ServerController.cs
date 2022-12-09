using NetworkUtil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace SnakeGame
{
    /// <summary>
    /// A controller for handling updates to a Snake Game Server
    /// 
    /// Updates the model according to the game mechanics, keeps track 
    /// of clients, and broadcasts the world to them.
    /// </summary>
    public class ServerController
    {
        #region Class Fields
        // Model Params
        public World theWorld { get; private set; } = new();
        private int powerupId = 0;
        private int powerupSpawnTimeRemaining;

        // Game Settings
        private int framesPerShot;               // Legacy code
        public int MSPerFrame { get; private set; }     // How many miliseconds for each frame
        private int snakeRespawnRate;       // How many frames before snakes respawn
        private int maxPowerupSpawn;    // How long to wait before spawning the first powerup
        private int foodGrowth;     // How many frames snakes grow for each food they eat
        private int snakeSpawnLen;  // How big snakes are when they are initially spawned
        private string mode = "Free For All";   // This controller supports two modes ("Team Death Match" & "Free For All")

        // Directions
        private static Vector2D UP = new Vector2D(0, -1);
        private static Vector2D DOWN = new Vector2D(0, 1);
        private static Vector2D LEFT = new Vector2D(-1, 0);
        private static Vector2D RIGHT = new Vector2D(1, 0);

        // Client Params
        public Dictionary<long, SocketState> Clients { get; private set; } = new();
        #endregion

        #region Controller Initialization
        /// <summary>
        /// Constructs a Server Controller with game settings/mechanics read from the
        /// "settings.xml" file.
        /// </summary>
        public ServerController()
        {
            // Get the current settings
            XmlDocument settings = new();
            StringBuilder settingsFilePath = new(Environment.CurrentDirectory);
            settingsFilePath.Append("/../../../../ServerController/settings.xml");
            settings.Load(settingsFilePath.ToString());
            XmlNode? fpshot = settings.SelectSingleNode("//GameSettings/FramesPerShot");
            if (fpshot != null)
            {
                framesPerShot = int.Parse(fpshot.InnerText);
            }
            XmlNode? mspframe = settings.SelectSingleNode("//GameSettings/MSPerFrame");
            if (mspframe != null)
            {
                MSPerFrame = int.Parse(mspframe.InnerText);
            }
            XmlNode? respawnRate = settings.SelectSingleNode("//GameSettings/RespawnRate");
            if (respawnRate != null)
            {
                snakeRespawnRate = int.Parse(respawnRate.InnerText);
            }
            XmlNode? univereSize = settings.SelectSingleNode("//GameSettings/UniverseSize");
            if (univereSize != null)
            {
                theWorld.worldSize = int.Parse(univereSize.InnerText);
            }
            XmlNode? maxPowerupDelay = settings.SelectSingleNode("//GameSettings/MaxPowerupDelay");
            if (maxPowerupDelay != null)
            {
                maxPowerupSpawn = int.Parse(maxPowerupDelay.InnerText);
            }
            XmlNode? snakeGrowth = settings.SelectSingleNode("//GameSettings/SnakeGrowth");
            if (snakeGrowth != null)
            {
                foodGrowth = int.Parse(snakeGrowth.InnerText);
            }
            XmlNode? snakeStartingLen = settings.SelectSingleNode("//GameSettings/SnakeStartingLength");
            if (snakeStartingLen != null)
            {
                snakeSpawnLen = int.Parse(snakeStartingLen.InnerText);
            }
            XmlNode? gameMode = settings.SelectSingleNode("//GameSettings/GameMode");
            if (gameMode != null)
            {
                mode = gameMode.InnerText;
            }

            // Read the the list of walls
            XmlNode? wallsNode = settings.SelectSingleNode("//GameSettings/Walls");
            if (wallsNode != null)
            {   // Read each individual wall
                XmlNodeList walls = wallsNode.ChildNodes;
                foreach (XmlNode w in walls)
                {
                    XmlNode? id = w.SelectSingleNode("ID");
                    XmlNode? p1_x = w.SelectSingleNode("p1/x");
                    XmlNode? p1_y = w.SelectSingleNode("p1/y");
                    XmlNode? p2_x = w.SelectSingleNode("p2/x");
                    XmlNode? p2_y = w.SelectSingleNode("p2/y");
                    if (id != null && p1_x != null && p1_y != null && p2_x != null &&
                        p2_y != null)
                    {
                        Wall wall = new();
                        wall.id = int.Parse(id.InnerText);
                        wall.p1.X = double.Parse(p1_x.InnerText);
                        wall.p1.Y = double.Parse(p1_y.InnerText);
                        wall.p2.X = double.Parse(p2_x.InnerText);
                        wall.p2.Y = double.Parse(p2_y.InnerText);
                        theWorld.walls.Add(wall.id, wall);
                    }
                }
            }
        }

        #endregion

        #region Processing New Clients
        /// <summary>
        /// Listens to a newly connected client until they send their name then transfers control
        /// to the "ClientNameReceived" method.
        /// </summary>
        /// <param name="state">the client</param>
        public void ClientConnection(SocketState state)
        {
            Console.WriteLine("Accepted new client.");
            state.OnNetworkAction = ClientNameReceived;
            Networking.GetData(state);
        }

        /// <summary>
        /// Processes a client who has sent their name by adding them to the list 
        /// of clients then sending them their player id, world size, and the walls.
        /// 
        /// 1. The client sends two strings representing integer numbers each terminated by a 
        ///     '\n'. The first number is the player's unique ID. The second is the 
        ///     size of the world, representing both width and height.
        /// 2. This method sends the client all of the walls as JSON objects, each separated by 
        ///     a '\n'.
        /// 3. Then continually sends the current state of the rest of the game on every 
        ///     frame. Each object ends with a '\n' characer and there is no guarantee 
        ///     that all objects will be sent in order or be included in a single network 
        ///     send/receive operation.
        /// </summary>
        /// <param name="state">the client being processed</param>
        private void ClientNameReceived(SocketState state)
        {
            // Read player name from client
            int clientId = (int)state.ID;
            string raw = state.GetData();
            string[] data = Regex.Split(raw, "\n");
            string playerName = data[0];
            state.RemoveData(0, playerName.Length);

            // Find an empty location in the world to place the snake
            List<Vector2D> spawnLoc = SpawnSnake();
            // Calculate the snake's spawn Direction
            Vector2D spawnDir = CalculateSegmentDirection(spawnLoc[0], spawnLoc[1]);
            lock (theWorld)
            { // Add this client to the world as a newly spawned snake
                Snake player = new Snake(clientId, spawnLoc, spawnDir, playerName);
                theWorld.snakes.Add(player.id, player);
            }

            // Send client ID and world size
            Networking.Send(state.TheSocket, clientId + "\n" + theWorld.worldSize + "\n");
            // Send client the walls
            StringBuilder wallsJSON = new();
            lock (theWorld)
            {
                foreach (Wall w in theWorld.walls.Values)
                {
                    wallsJSON.Append(JsonConvert.SerializeObject(w) + "\n");
                }
            }
            Networking.Send(state.TheSocket, wallsJSON.ToString());

            // Start sending client info on each frame
            lock (Clients)
            {
                Clients.Add(state.ID, state);
            }
            Console.WriteLine("Player(" + state.ID + ") \"" + playerName + "\" connected");
            // Allow the client to send move commands
            state.OnNetworkAction = ReceiveMoveCommand;
            Networking.GetData(state);
        }

        /// <summary>
        /// Finds a randomly-chosen empty spot in the world that can fit a snake and returns a 
        /// list of 2DVectors spanning that space. 
        /// </summary>
        /// <returns>The body of a newly spawned snake</returns>
        private List<Vector2D> SpawnSnake()
        {
            // Prepare a list of Snake segments to return
            List<Vector2D> body;

            // Choose a random axis-aligned orientation for snake
            Random rng = new();
            Vector2D spawnDir = UP * 1; // assume vertical
            if (rng.Next(2) == 0) // 50% chance to swap to horizontal
            {
                spawnDir.Rotate(90);
            }
            if (rng.Next(2) == 0) // 50% chance to reverse direction
            {
                spawnDir *= -1;
            }

            // Find a valid spawn location for the snake's starting segment
            do
            {
                // Randomly place the head
                int xCor = rng.Next(-1 * theWorld.worldSize / 2, theWorld.worldSize / 2);
                int yCor = rng.Next(-1 * theWorld.worldSize / 2, theWorld.worldSize / 2);
                Vector2D head = new(xCor, yCor);
                // Calculate where the tail should be based on spawn direction and snake length.
                Vector2D tail = new Vector2D(head.X + (snakeSpawnLen * spawnDir.X),
                    head.Y + (snakeSpawnLen * spawnDir.Y));
                body = new List<Vector2D>();
                body.Add(tail);
                body.Add(head);
                // Check if placing the snake here will result in a collision. If so, repeat the loop.
            } while (InvalidSpawn(body));

            // Then return the newly spawned snake's body as a list with two vectors: {head, tail}.
            return body;
        }

        #endregion

        #region Collision Checking
        /// <summary>
        /// Checks whether or not a given powerup location is an Invalid spawn location
        /// </summary>
        /// <param name="powerup"></param>
        /// <returns>Invalid spawn location?</returns>
        private bool InvalidSpawn(Vector2D powerup)
        {
            // Check for a collision between each snake-segment and every single collidable world object
            foreach (Wall w in theWorld.walls.Values)
            {   // walls
                List<Vector2D> wall = new();
                wall.Add(w.p1);
                wall.Add(w.p2);
                if (AreColliding(powerup, 16, wall, 50))
                    return true;
            }
            foreach (Snake s in theWorld.snakes.Values)
            {   // snakes
                if (AreColliding(powerup, 10, s.body, 10))
                    return true;
            }
            foreach (PowerUp p in theWorld.powerups.Values)
            {   // other powerups
                if (AreColliding(powerup, p))
                    return true;
            }

            // There were no collisions
            return false;
        }

        /// <summary>
        /// Checks whether or not a given snake body is an Invalid spawn location
        /// </summary>
        /// <param name="snake"></param>
        /// <returns>Invalid spawn location?</returns>
        private bool InvalidSpawn(List<Vector2D> snake)
        {
            // Check for a collision between each snake-segment and every single collidable world object
            foreach (Wall w in theWorld.walls.Values)
            {   // walls
                List<Vector2D> wall = new();
                wall.Add(w.p1);
                wall.Add(w.p2);
                if (AreColliding(snake, wall, 50))
                    return true;
            }
            foreach (Snake s in theWorld.snakes.Values)
            {   // other snakes
                if (AreColliding(snake, s.body, 10))
                    return true;
            }
            foreach (PowerUp p in theWorld.powerups.Values)
            {   // powerups
                if (AreColliding(snake, p))
                    return true;
            }

            // There were no collisions
            return false;
        }

        /// <summary>
        /// Checks if two rectangles defined by the argued topleft and botright corners 
        /// overlap. 
        /// </summary>
        /// <param name="rect1TL">rectangle one top left corner</param>
        /// <param name="rect1BR">rectangle one bottom right corner</param>
        /// <param name="rect2TL">rectangle two top left corner</param>
        /// <param name="rect2BR">rectangle two bottom right corner</param>
        /// <returns>a boolean indicating whether or not the rectangles intersect</returns>
        private bool IsIntersectingRectangles(Vector2D rect1TL, Vector2D rect1BR,
                              Vector2D rect2TL, Vector2D rect2BR)
        {
            // if rectangle has area 0, no overlap
            if (rect1TL.X == rect1BR.X || rect1TL.Y == rect1BR.Y || rect2BR.X == rect2TL.X || rect2TL.Y == rect2BR.Y)
            {
                return false;
            }

            // If one rectangle is on left side of other
            if (rect1TL.X > rect2BR.X || rect2TL.X > rect1BR.X)
            {
                return false;
            }

            // If one rectangle is above other
            if (rect1BR.Y < rect2TL.Y || rect2BR.Y < rect1TL.Y)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Checks whether or not two snakes are colliding with each other
        /// 
        /// Snakes cannot collide with dead snakes
        /// </summary>
        /// <param name="snake1">Snake being checked for collision</param>
        /// <param name="snake2">Snake being collided with</param>
        /// <returns>Whether or not the two snakes collided</returns>
        private bool AreColliding(Snake snake1, Snake snake2)
        {
            return snake2.alive && AreColliding(snake1.body.Last(), 10, snake2.body, 10);
        }

        /// <summary>
        /// Checks whether or not the body of a newly spawned snake is colliding with 
        /// an object
        /// </summary>
        /// <param name="body">newly spawned snake's body</param>
        /// <param name="obj">obj being collided with</param>
        /// <param name="width">width of the object being collided with</param>
        /// <returns>Whether or not the body and object have collided</returns>
        private bool AreColliding(List<Vector2D> body, List<Vector2D> obj, int width)
        {
            // Check the collision barrier one segment at a time
            Vector2D bodyTopLeft, bodyBottomRight, objTopLeft, objBottomRight;
            CalculateCollisionBarrier(body[0], body[1], 10, out bodyTopLeft, out bodyBottomRight);
            for (int i = 0; i < obj.Count - 1; i++)
            {
                Vector2D p1 = obj[i], p2 = obj[i + 1];
                CalculateCollisionBarrier(p1, p2, width, out objTopLeft, out objBottomRight);
                // Check for collision
                if (IsIntersectingRectangles(bodyTopLeft, bodyBottomRight, objTopLeft, objBottomRight))
                {
                    return true;
                }
            }

            // No collisions found
            return false;
        }

        /// <summary>
        /// Checks whether or not a body of a newly spawned snake is colliding with a 
        /// powerup
        /// 
        /// Powerup collision barriers are 10x10 squares
        /// </summary>
        /// <param name="body">newly spawned snake body</param>
        /// <param name="p">powerup</param>
        /// <returns>If the snake and powerup are colliding</returns>
        private bool AreColliding(List<Vector2D> body, PowerUp p)
        {
            // Calculate Collision Barrier
            Vector2D pTopLeft = new Vector2D(p.loc.X - 10, p.loc.Y - 10);
            Vector2D pBottomRight = new Vector2D(p.loc.X + 10, p.loc.Y + 10);
            Vector2D segTopLeft, segBottomRight;
            CalculateCollisionBarrier(body[0], body[1], 10, out segTopLeft, out segBottomRight);
            // Check for collision
            return IsIntersectingRectangles(segTopLeft, segBottomRight, pTopLeft, pBottomRight);
        }

        /// <summary>
        /// Checks whether or not a given snake is colliding with itself
        /// </summary>
        /// <param name="snake">snake</param>
        /// <returns>If the snake collided with itself</returns>
        private bool AreColliding(Snake snake)
        {
            // Verify this snake is large enough to self-collide
            if (snake.body.Count() <= 2)
                return false;

            // Find what direction is opposite to the snake's current direction
            Vector2D oppositeDir = snake.direction * -1;

            // Starting from the head, travel through the snake until we find an opposite turn
            int index = snake.body.Count() - 1;
            Vector2D segmentDir = snake.direction;
            while (index > 0 && !(segmentDir.X == oppositeDir.X && segmentDir.Y == oppositeDir.Y))
            {
                segmentDir = CalculateSegmentDirection(snake.body[index - 1], snake.body[index--]);
            }
            // Check if there were any opposite turns
            if (index < 0)
                return false;

            // Check for collision with the snake body from the tail up to the opposite turn
            List<Vector2D> collidableBody = snake.body.GetRange(0, index + 1);
            return AreColliding(snake.body.Last(), 10, collidableBody, 10);
        }

        /// <summary>
        /// Checks whether or not a given vector is colliding with an object
        /// </summary>
        /// <param name="v">vector</param>
        /// <param name="vw">vector width</param>
        /// <param name="obj">object being collided with</param>
        /// <param name="objWidth">width of the object being collided with</param>
        /// <returns>Whether or not the vector and object have collided</returns>
        private bool AreColliding(Vector2D v, int vw, List<Vector2D> obj, int objWidth)
        {
            // Find rectangle representing the vector being checked for collision
            Vector2D vTopLeft = new(), vBottomRight = new();
            vTopLeft.X = v.X - (vw / 2);
            vTopLeft.Y = v.Y - (vw / 2);
            vBottomRight.X = v.X + (vw / 2);
            vBottomRight.Y = v.Y + (vw / 2);

            // Check each segment of the obj for collision with the vector
            for (int i = 0; i < obj.Count - 1; i++)
            {
                // Calculate the collision barrier of this segment
                Vector2D objTopLeft, objBottomRight;
                CalculateCollisionBarrier(obj[i], obj[i + 1], objWidth, out objTopLeft, out objBottomRight);
                // Check for collision
                if (IsIntersectingRectangles(vTopLeft, vBottomRight, objTopLeft, objBottomRight))
                {
                    return true;
                }
            }

            // No collisions found
            return false;
        }

        /// <summary>
        /// Checks whether or not a given head is colliding with a powerup
        /// 
        /// Powerups are 16x16 pixels but there collision barrier is 10x10
        /// </summary>
        /// <param name="head">Vector2D representing a head</param>
        /// <param name="p">powerup being collided with</param>
        /// <returns>Whether or not the head and powerup have collided</returns>
        private bool AreColliding(Vector2D head, PowerUp p)
        {
            // Calculate Collision Barrier
            Vector2D topLeft = new Vector2D(p.loc.X - 10, p.loc.Y - 10);
            Vector2D bottomRight = new Vector2D(p.loc.X + 10, p.loc.Y + 10);
            // Check for collision
            return (head.X > topLeft.X && head.X < bottomRight.X) &&
                    (head.Y > topLeft.Y && head.Y < bottomRight.Y);
        }

        /// <summary>
        /// Calculates the top left and bottom right corners of a collision barrier
        /// </summary>
        /// <param name="p1">start of segment</param>
        /// <param name="p2">end of segment</param>
        /// <param name="width">width of segment</param>
        /// <param name="topLeft"></param>
        /// <param name="bottomRight"></param>
        private void CalculateCollisionBarrier(Vector2D p1, Vector2D p2, int width,
            out Vector2D topLeft, out Vector2D bottomRight)
        {
            // Calcualte direction
            Vector2D segmentDir;
            bool isVertical = p1.X == p2.X;
            if (isVertical)
            {   // Vertical segment
                if (p1.Y < p2.Y)
                {
                    segmentDir = DOWN * 1;
                }
                else
                {
                    segmentDir = UP * 1;
                }
            }
            else
            {   // Horizontal segment
                if (p1.X < p2.X)
                {
                    segmentDir = RIGHT * 1;
                }
                else
                {
                    segmentDir = LEFT * 1;
                }
            }
            // Calculate corners of this collision barrier
            if (isVertical)
            {
                if (segmentDir.X == DOWN.X && segmentDir.Y == DOWN.Y)
                {   // Going Down
                    topLeft = new Vector2D(p1.X - (width / 2), p1.Y - (width / 2));
                    bottomRight = new Vector2D(p2.X + (width / 2), p2.Y + (width / 2));
                }
                else
                {   // Going Up
                    topLeft = new Vector2D(p2.X - (width / 2), p2.Y - (width / 2));
                    bottomRight = new Vector2D(p1.X + (width / 2), p1.Y + (width / 2));
                }
            }
            else
            {
                if (segmentDir.X == RIGHT.X && segmentDir.Y == RIGHT.Y)
                {   // Going Right
                    topLeft = new Vector2D(p1.X - (width / 2), p1.Y - (width / 2));
                    bottomRight = new Vector2D(p2.X + (width / 2), p2.Y + (width / 2));
                }
                else
                {   // Going Left
                    topLeft = new Vector2D(p2.X - (width / 2), p2.Y - (width / 2));
                    bottomRight = new Vector2D(p1.X + (width / 2), p1.Y + (width / 2));
                }
            }
        }
        #endregion

        #region Receiving From Clients
        /// <summary>
        /// Gets data from each client
        /// </summary>
        public void GetClientData()
        {
            lock (Clients)
            {
                foreach (SocketState c in Clients.Values)
                {
                    Networking.GetData(c);
                }
            }
        }

        /// <summary>
        /// Receive a movement command from the client and update the client's 
        /// snake's direction.
        /// </summary>
        /// <param name="state">the client</param>
        private void ReceiveMoveCommand(SocketState state)
        {
            // Get data from the client
            string raw = "";
            lock (state)
            {
                raw = state.GetData();
                state.RemoveData(0, raw.Length);
            }
            string[] data = Regex.Split(raw, "\n");

            // Check if they sent a move command
            bool foundCmd = false;
            int count = 0;
            ControlCommand? cmd = new();
            while (!foundCmd && count < data.Length)
            {
                // Skip non-JSON strings
                if (!(data[count].StartsWith("{") && data[count].EndsWith("}")))
                {
                    count++;
                    continue;
                }

                // Parse the string into a JSON object
                JObject obj = JObject.Parse(data[count++]);
                JToken? token = obj["moving"];
                if (token != null)
                {
                    cmd = JsonConvert.DeserializeObject<ControlCommand>(data[0]);
                    foundCmd = true;
                }
            }

            // Process the client's move command
            lock (theWorld)
            {
                // See if the snake disconnected
                if (theWorld.snakes.ContainsKey((int)state.ID))
                {   // Queue the move command into the snake for next frame
                    Snake clientSnake = theWorld.snakes[(int)state.ID];
                    if (cmd != null)
                    {
                        clientSnake.MoveRequest = cmd.moving;
                    }
                }
            }
        }

        /// <summary>
        /// A JSON compatible structure for representing control commands from 
        /// a client.
        /// </summary>
        [JsonObject(MemberSerialization.OptIn)]
        internal class ControlCommand
        {
            [JsonProperty]
            public string moving = "none";

            /// <summary>
            /// Constructs a control command with a moving value of none
            /// </summary>
            [JsonConstructor]
            public ControlCommand()
            {
            }
        }

        #endregion

        #region Updating The Model

        /// <summary>
        /// Updates the state of each object in the world (movement, position, booleans) using the 
        /// Server Controller.
        /// </summary>
        public void UpdateWorld()
        {
            // Update the current state of the world
            lock (theWorld)
            {
                // Update powerups
                foreach (PowerUp p in theWorld.powerups.Values)
                {
                    if (p.died)
                        theWorld.powerups.Remove(p.id);
                }
                SpawnPowerup();

                // Update the snakes
                List<int> disconnectedSnakes = new();
                foreach (Snake s in theWorld.snakes.Values)
                {
                    // See if the snake is still connected to the server
                    if (s.dc)
                    {
                        disconnectedSnakes.Add(s.id);
                        break;
                    }

                    // Handle dead snakes
                    s.died = false;
                    if (!s.alive)
                    {   // Lower the Snake's respawn timer
                        s.alive = ++s.FramesSpentDead >= snakeRespawnRate;
                        if (s.alive)
                        {
                            // Replace the snake into the world
                            s.body = SpawnSnake();
                            s.direction = CalculateSegmentDirection(s.body[0], s.body[1]);
                            // Reset the snake's respawn timer
                            Console.WriteLine("Snake " + s.name + " respawned.");
                            s.FramesSpentDead = 0;
                        }
                    }
                    // Move alive snakes
                    else
                    {
                        // Process Move Commands From Clients
                        UpdateSnakeDirection(s);

                        // Move the head
                        Vector2D head = s.body.Last() + (s.direction * 3);
                        s.body.Last().X = head.X;
                        s.body.Last().Y = head.Y;

                        // See if the head collided with anything special
                        if (!DiedThisFrame(s))
                        {   // See if the head grabbed any powerups
                            foreach (PowerUp p in theWorld.powerups.Values)
                            {
                                if (AreColliding(s.body.Last(), p))
                                {   // The snake collided with a powerup
                                    s.score++;
                                    s.FoodInBelly += foodGrowth;
                                    p.died = true;
                                    break;
                                }
                            }
                            // See if the head reached the edge of the world
                            HandleWrapAround(s);

                            // Progress the snake's tail
                            if (s.FoodInBelly > 0)
                            {   // The snake grows one frame worth of movement
                                s.FoodInBelly -= 1;
                            }
                            else
                            {   // Move the rest of the body starting from the tail
                                MoveTailOfSnake(s);
                            }
                        }
                        else
                        {   // The snake died and shouldn't be moved
                            s.died = true;
                            s.alive = false;
                            s.score = 0;
                            break;
                        }
                    }
                }

                // Remove disconnected snakes from the world
                foreach (int snake in disconnectedSnakes)
                {
                    theWorld.snakes.Remove(snake);
                }
            }
        }

        /// <summary>
        /// Checks to see if the argued snake's head has crossed any world borders.
        /// If it has then a new snake joint will be created at the opposite side of the world. 
        /// </summary>
        /// <param name="snake"></param>
        private void HandleWrapAround(Snake snake)
        {
            // Create a new vector to represent a new joint in case the snake crosses a world border.
            Vector2D newhead = new(snake.body.Last());
            Vector2D borderEdge;

            // See if the head is outside the world and add the new joint to the snake's body if it is.
            if (snake.body.Last().Y < -(theWorld.worldSize / 2) || snake.body.Last().Y > theWorld.worldSize / 2)
            { // top and bottom borders
                newhead.Y = (newhead.Y < 0) ? (theWorld.worldSize / 2) : -(theWorld.worldSize / 2);
                borderEdge = new(newhead);
                snake.body.Add(borderEdge);
                snake.body.Add(newhead);
            }
            else if (snake.body.Last().X < -(theWorld.worldSize / 2) || snake.body.Last().X > (theWorld.worldSize / 2))
            { // left and right borders
                newhead.X = (newhead.X < 0) ? (theWorld.worldSize / 2) : -(theWorld.worldSize / 2);
                borderEdge = new(newhead);
                snake.body.Add(borderEdge);
                snake.body.Add(newhead);
            }
        }

        /// <summary>
        /// Moves the tail of a snake forward
        /// </summary>
        /// <param name="s"></param>
        private void MoveTailOfSnake(Snake s)
        {
            int movement = 3;

            // Clean up tail-end joints
            Vector2D tail = s.body[1] - s.body[0];
            while (tail.Length() < movement)
            {
                movement -= (int)tail.Length();
                s.body.RemoveAt(0);
                tail = s.body[1] - s.body[0];
            }
            if (tail.Length() >= theWorld.worldSize)
            {   // The tail wrapped around the world
                s.body.RemoveAt(0);
            }

            // Find what direction the tail is moving in
            Vector2D tailDir = CalculateSegmentDirection(s.body[0], s.body[1]);
            Vector2D newTail = (tailDir * movement) + s.body[0];
            s.body[0] = newTail;
        }

        /// <summary>
        /// Calculates which direction a given snake-segment is moving in.
        /// </summary>
        /// <param name="p1">Joint hte segment is starting from</param>
        /// <param name="p2">Joint the segment is moving towards</param>
        /// <returns>A unit vector representing the direction a segment is moving in</returns>
        private Vector2D CalculateSegmentDirection(Vector2D p1, Vector2D p2)
        {
            bool isVertical = p1.X == p2.X;
            if (isVertical)
            {
                if (p1.Y < p2.Y)
                {
                    return DOWN * 1;
                }
                else
                {
                    return UP * 1;
                }
            }
            else
            {
                if (p1.X < p2.X)
                {
                    return RIGHT * 1;
                }
                else
                {
                    return LEFT * 1;
                }
            }
        }

        /// <summary>
        /// Returns a boolean that indicates if a snake is currently colliding with either another 
        /// snake or a wall and should therefor be dead.
        /// </summary>
        /// <param name="snake">Snake being checked for death</param>
        /// <returns>Whether or not the snake died</returns>
        private bool DiedThisFrame(Snake snake)
        {
            // See if the snake died by hitting a snake
            foreach (Snake snakeBeingHit in theWorld.snakes.Values)
            {
                if (snakeBeingHit.id == snake.id)
                {   // Check for self collisions
                    if (AreColliding(snake))
                        return true;
                }
                else
                {   // Check for collisions with enemy snakes
                    if (mode == "Team Death Match")
                    {   // Even Snakes can pass through Even Snakes and Odds through Odds
                        bool evenSnakeClient = snake.id % 2 == 0;
                        if (evenSnakeClient)
                        {   // This snake can pass through other evens
                            if (!(snakeBeingHit.id % 2 == 0))
                            {   // Enemy snake
                                if (AreColliding(snake, snakeBeingHit))
                                    return true;
                            }
                        }
                        else
                        {   // This snake can pass through other odds
                            if (snakeBeingHit.id % 2 == 0)
                            {   // Enemy Snake
                                if (AreColliding(snake, snakeBeingHit))
                                    return true;
                            }
                        }
                    } else
                    {   // Snakes die if they collide with any other snakes
                        if (AreColliding(snake, snakeBeingHit))
                            return true;
                    }
                }
            }

            // See if the snake died by hitting a wall
            foreach (Wall w in theWorld.walls.Values)
            {
                List<Vector2D> wallSeg = new();
                if ((w.p1.Y == w.p2.Y && w.p2.X > w.p1.X)
                    || (w.p1.X == w.p2.X && w.p2.Y > w.p1.Y))
                {   // Wall is drawn going right or down
                    wallSeg.Add(w.p2);
                    wallSeg.Add(w.p1);
                }
                else
                {   // Wall is drawn going left or up
                    wallSeg.Add(w.p1);
                    wallSeg.Add(w.p2);
                }
                if (AreColliding(snake.body.Last(), 10, wallSeg, 50))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Randomly spawns a powerup into the world or lowers the respawn timer 
        /// if it has not reached zero yet.
        /// </summary>
        private void SpawnPowerup()
        {
            if (theWorld.powerups.Count() < theWorld.MaxPowerups)
            {
                if (powerupSpawnTimeRemaining == 0)
                {
                    // Find a valid spawn location for the snake's starting segment
                    Random rng = new();
                    Vector2D loc;   // tmp value
                    do
                    {
                        // Randomly place the powerup
                        int xCor = rng.Next(-1 * theWorld.worldSize / 2, theWorld.worldSize / 2);
                        int yCor = rng.Next(-1 * theWorld.worldSize / 2, theWorld.worldSize / 2);
                        loc = new(xCor, yCor);
                    } while (InvalidSpawn(loc));
                    // Reset the spawn timer for the next powerup
                    powerupSpawnTimeRemaining = rng.Next(0, maxPowerupSpawn);
                    // Spawn the powerup into the world
                    PowerUp p = new PowerUp(powerupId++, loc);
                    theWorld.powerups.Add(p.id, p);
                }
                else
                {   // Lower the spawn timer by one frame
                    powerupSpawnTimeRemaining--;
                }
            }
        }

        /// <summary>
        /// Processes a move command. If it is updated, adds a new joint to the 
        /// snake to allow turning and updates the opposite direction for self- 
        /// collision checking.
        /// 
        /// Snakes cannot turn 180 degrees in one command.
        /// 
        /// Snakes cannot turn 180 degrees until they have traveled their width (10 units).
        /// </summary>
        /// <param name="snake">Snake</param>
        private void UpdateSnakeDirection(Snake snake)
        {
            // See where the client is trying to turn
            Vector2D moveRequest = new();
            if (snake.MoveRequest == "up")
            {
                moveRequest = UP * 1;
            }
            else if (snake.MoveRequest == "right")
            {
                moveRequest = RIGHT * 1;
            }
            else if (snake.MoveRequest == "down")
            {
                moveRequest = DOWN * 1;
            }
            else if (snake.MoveRequest == "left")
            {
                moveRequest = LEFT * 1;
            }
            else
            {
                return;
            }

            // See if the client requested a valid movement
            if (moveRequest.Equals(snake.direction))
            {   // The snake is already moving in this direction
                return;
            }
            if (snake.body.Count() >= 3)
            {
                // Calculate the size of the neck
                Vector2D neckP1 = snake.body[snake.body.Count() - 2];
                Vector2D neckP2 = snake.body[snake.body.Count() - 1];
                double neckLen = (neckP2 - neckP1).Length();
                // See if the head is turning 180 degrees from the segment before the neck
                Vector2D shoulderP1 = snake.body[snake.body.Count() - 3];
                Vector2D shoulderP2 = snake.body[snake.body.Count() - 2];
                Vector2D oppositeDir = moveRequest * -1;
                Vector2D shoulderDir = CalculateSegmentDirection(shoulderP1, shoulderP2);
                if ((shoulderDir.X == oppositeDir.X && shoulderDir.Y == oppositeDir.Y)
                    && neckLen <= 10)
                {   // The snake cannot turn 180 degrees yet
                    return;
                }
            }
            if (snake.direction.ToAngle() == (moveRequest * -1).ToAngle())
            {   // Snakes cannot turn 180 degrees in one command
                return;
            }
            // Movement request accepted
            snake.direction = moveRequest;

            // Place a new joint where the head is
            Vector2D newHead = snake.body.Last() * 1;
            snake.body.Add(newHead);
        }

        #endregion

        #region Broadcasting To Clients
        /// <summary>
        /// Broadcasts the current state of the world to each client
        /// </summary>
        public void BroadcastWorld()
        {
            // Serialize each object in the world
            StringBuilder jsonSerialization = new();
            lock (theWorld)
            {
                foreach (Snake s in theWorld.snakes.Values)
                {   // Snakes
                    jsonSerialization.Append(JsonConvert.SerializeObject(s) + "\n");
                    s.join = false;
                }
                foreach (PowerUp p in theWorld.powerups.Values)
                {   // Powerups
                    jsonSerialization.Append(JsonConvert.SerializeObject(p) + "\n");
                }
            }
            // Send each client the new state of the world
            lock (Clients)
            {
                List<long> disconnectedClients = new();
                // Send all clients the current state of the world
                foreach (SocketState c in Clients.Values)
                {
                    if (!c.TheSocket.Connected)
                    {   // This client disconnected
                        disconnectedClients.Add(c.ID);
                    }
                    else
                    {
                        Networking.Send(c.TheSocket, jsonSerialization.ToString());
                    }
                }
                // Remove disconnected clients
                foreach (long id in disconnectedClients)
                {
                    lock (theWorld)
                    {
                        theWorld.snakes[(int)id].dc = true;
                    }
                    Console.WriteLine("Client " + id + " disconnected.");
                    Clients.Remove(id);
                }
            }
        }

        #endregion
    }
}