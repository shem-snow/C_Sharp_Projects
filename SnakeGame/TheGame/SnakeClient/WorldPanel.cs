using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using IImage = Microsoft.Maui.Graphics.IImage;
#if MACCATALYST
using Microsoft.Maui.Graphics.Platform;
#else
using Microsoft.Maui.Graphics.Win2D;
#endif
using Color = Microsoft.Maui.Graphics.Color;
using System.Reflection;
using Microsoft.Maui;
using System.Net;
using Font = Microsoft.Maui.Graphics.Font;
using SizeF = Microsoft.Maui.Graphics.SizeF;
using System;
using Microsoft.Maui.Graphics;
using System.Xml.Serialization;
using System.Security.Cryptography;
using Microsoft.UI.Xaml.Controls;

namespace SnakeGame;
/// <summary>
/// The World Panel is responsible for drawing onto the panel (the window) which the game is played on.
/// It does not interract with the user in any way. It simply draws images when prompted by other parts of the program.
/// </summary>
public class WorldPanel : IDrawable
{
    #region Images
    private IImage wallImg;
    private IImage backgroundImg;
    private IImage powerupsImg;
    #endregion
    private bool initializedForDrawing = false;
    private delegate void ObjectDrawer(object o, ICanvas canvas);
    public World theWorld { private get; set; }

    #region OS Compatibility
#if MACCATALYST
    private IImage loadImage(string name)
    {
        Assembly assembly = GetType().GetTypeInfo().Assembly;
        string path = "SnakeGame.Resources.Images";
        return PlatformImage.FromStream(assembly.GetManifestResourceStream($"{path}.{name}"));
    }
#else
    private IImage loadImage(string name)
    {
        Assembly assembly = GetType().GetTypeInfo().Assembly;
        string path = "SnakeGame.Resources.Images";
        var service = new W2DImageLoadingService();
        return service.FromStream(assembly.GetManifestResourceStream($"{path}.{name}"));
    }
#endif

    #endregion

    #region Initialization

    /// <summary>
    /// Constructs a world panel with the argued player to be centered
    /// </summary>
    /// <param name="playerId">Snake of the Player</param>
    public WorldPanel()
    {
    }

    /// <summary>
    /// Loads all images required for Draw method 
    /// and initializes the world for drawing.
    /// </summary>
    private void InitializeDrawing()
    {
        // Set images of all objects
        wallImg = loadImage("WallSprite.png");
        backgroundImg = loadImage("Background.png");
        powerupsImg = loadImage("Food.png");

        // Drawing has been initialized
        initializedForDrawing = true;
    }
    #endregion

    #region Drawing

    /// <summary>
    /// This runs whenever the drawing panel is invalidated and draws the game
    /// 
    /// The drawing panel should be invalidated on each frame.
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="dirtyRect"></param>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // Images must be loaded before they can be drawn
        if (!initializedForDrawing)
        {
            InitializeDrawing();
            return;
        }

        // undo any leftover transformations from last frame
        canvas.ResetState();

        // Calculate the location of the head of the player's snake
        float playerX = 0, playerY = 0;
        lock (theWorld)
        {
            if (theWorld.snakes.TryGetValue(theWorld.playerID, out Snake player))
            {
                playerX = (float)player.body.Last().GetX();
                playerY = (float)player.body.Last().GetY();
            }
        }
        // center the view on the player
        canvas.Translate(-playerX + (900 / 2), -playerY + (900 / 2));

        // draw the background
        canvas.DrawImage(backgroundImg, -theWorld.worldSize / 2, -theWorld.worldSize / 2,
            theWorld.worldSize, theWorld.worldSize);
        // draw the objects in the world
        lock (theWorld)
        {
            // draw the walls
            foreach (var wall in theWorld.walls.Values)
            {
                DrawWall(wall, canvas);
            }
            // Draw the snakes
            foreach (var snake in theWorld.snakes.Values)
            {
                if (snake.alive)
                {
                    DrawSnake(snake, canvas);
                }
            }
            // Draw the powerups
            foreach (var powerup in theWorld.powerups.Values)
            {
                DrawObjectWithTransform(canvas, powerup, powerup.loc.GetX(), powerup.loc.GetY(),
                    0, PowerupDrawer);
            }
        }

    }

    /// <summary>
    /// This method performs a translation and rotation to draw an object.
    /// </summary>
    /// <param name="canvas">The canvas object for drawing onto</param>
    /// <param name="o">The object to draw</param>
    /// <param name="worldX">The X component of the object's position in world space</param>
    /// <param name="worldY">The Y component of the object's position in world space</param>
    /// <param name="angle">The orientation of the object, measured in degrees clockwise from "up"</param>
    /// <param name="drawer">The drawer delegate. After the transformation is applied, the delegate is invoked to draw whatever it wants</param>
    private void DrawObjectWithTransform(ICanvas canvas, object o, double worldX, double worldY, double angle, ObjectDrawer drawer)
    {
        // "push" the current transform
        canvas.SaveState();

        canvas.Translate((float)worldX, (float)worldY);
        canvas.Rotate((float)angle);
        drawer(o, canvas);

        // "pop" the transform
        canvas.RestoreState();
    }

    /// <summary>
    /// Draws a snake segment by segment
    /// 
    /// Snakes have a stroke size of 10 pixels
    /// </summary>
    /// <param name="o">The snake to draw</param>
    /// <param name="canvas"></param>
    private void DrawSnake(Snake s, ICanvas canvas)
    {
        canvas.StrokeSize = 10;
        // Calculate the snake's color
        if (s.id % 8 == 0)
        {
            canvas.StrokeColor = Colors.White;
        }
        else if (s.id % 7 == 0)
        {
            canvas.StrokeColor = Colors.Green;
        }
        else if (s.id % 6 == 0)
        {
            canvas.StrokeColor = Colors.Black;
        }
        else if (s.id % 5 == 0)
        {
            canvas.StrokeColor = Colors.Gold;
        }
        else if (s.id % 4 == 0)
        {
            canvas.StrokeColor = Colors.Purple;
        }
        else if (s.id % 3 == 0)
        {
            canvas.StrokeColor = Colors.Pink;
        }
        else if (s.id % 2 == 0)
        {
            canvas.StrokeColor = Colors.Blue;
        }
        else
        {
            canvas.StrokeColor = Colors.Red;
        }

        // Draw the snake one segment at a time starting from the tail
        int i = 0;
        SnakeSegment segment = new();
        segment.Direction = "up";
        segment.Rotation = 0;

        while (i < s.body.Count - 1)
        {
            // Check if this joint is at the map's edge
            if (PassedEdgeOfMap(s.body[i]))
            {   // Continue drawing from the other edge of the map
                i++;
                continue;
            }
            Vector2D p1 = s.body[i], p2 = s.body[++i];

            // Calculate segment orientation
            segment.IsVertical = p1.X == p2.X;
            // Calculate segment length and direction
            if (segment.IsVertical)
            {
                segment.Length = (int)(p2.Y - p1.Y);
                if (segment.Length < 0)
                {
                    // Find the rotation from the last segment of the snake to this segment
                    if (segment.Direction == "right")
                    {   // Rotate the snake's direction 90 degrees counter-clockwise
                        segment.Rotation += 270;
                    }
                    else if (segment.Direction == "down")
                    {   // Rotate the snake's direction 180 degrees
                        segment.Rotation += 180;
                    }
                    else if (segment.Direction == "left")
                    {   // Rotate the snake's direction 90 degrees clockwise
                        segment.Rotation += 90;
                    }

                    // This segment should be drawn going up
                    segment.Direction = "up";
                    segment.Rotation = 0;
                }
                else
                {
                    // Find the rotation from the last segment of the snake to this segment
                    if (segment.Direction == "right")
                    {   // Rotate the snake's direction 90 degrees clockwise
                        segment.Rotation += 90;
                    }
                    else if (segment.Direction == "left")
                    {   // Rotate the snake's direction 90 degrees counter-clockwise
                        segment.Rotation += 270;
                    }
                    else if (segment.Direction == "up")
                    {   // Rotate the snake's direction 180 degrees
                        segment.Rotation += 180;
                    }

                    // This segment should be drawn going down
                    segment.Direction = "down";
                    segment.Length = -segment.Length;
                }
            }
            else
            {
                segment.Length = (int)(p2.X - p1.X);
                if (segment.Length > 0)
                {
                    // Find the rotation from the last segment of the snake to this segment
                    if (segment.Direction == "down")
                    {   // Rotate the snake's direction 90 degrees counter-clockwise
                        segment.Rotation += 270;
                    }
                    else if (segment.Direction == "left")
                    {   // Rotate the snake's direction 180 degrees
                        segment.Rotation += 180;
                    }
                    else if (segment.Direction == "up")
                    {   // Rotate the snake's direction 90 degrees clockwise
                        segment.Rotation += 90;
                    }

                    // This segment should be drawn going right
                    segment.Direction = "right";
                    segment.Length = -segment.Length;
                }
                else
                {
                    // Find the rotation from the last segment of the snake to this segment
                    if (segment.Direction == "right" || segment.Direction == null)
                    {   // Rotate the snake's direction 180 degrees
                        segment.Rotation += 180;
                    }
                    else if (segment.Direction == "down")
                    {   // Rotate the snake's direction 90 degrees clockwise
                        segment.Rotation += 90;
                    }
                    else if (segment.Direction == "up")
                    {   // Rotate the snake's directino 90 degrees counter-clockwise
                        segment.Rotation += 270;
                    }

                    // This segment should be drawn going left
                    segment.Direction = "left";
                }
            }

            // Draw the segment
            DrawObjectWithTransform(canvas, segment.Length, p1.X, p1.Y,
                segment.Rotation, SnakeSegmentDrawer);
        }
        // Draw the snake's name and score
        canvas.FontColor = Colors.White;
        canvas.DrawString(s.name + ": " + s.score, (float)s.body[i].X, (float)s.body[i].Y - 15,
            HorizontalAlignment.Center);
    }


    /// <summary>
    /// Returns true if this joint interesects the edge of the map
    /// </summary>
    /// <param name="joint"></param>
    /// <returns>whether or not this joint is on the edge of the map</returns>
    private bool PassedEdgeOfMap(Vector2D joint)
    {
        //return (joint.X.Equals4DigitPrecision(Math.Abs(theWorld.worldSize / 2))) ||
        //    (joint.Y == Math.Abs(theWorld.worldSize / 2));

        double negBorder, posBorder;
        negBorder = -(theWorld.worldSize / 2);
        posBorder = theWorld.worldSize / 2;
        return (joint.X < negBorder || joint.Y < negBorder) ||
            (joint.X > posBorder || joint.Y > posBorder);
    }

    /// <summary>
    /// Structure that represents a segment of a snake to be drawn
    /// </summary>
    private struct SnakeSegment
    {
        public int Length;
        public bool IsVertical;
        public string Direction;
        public double Rotation;
    }

    /// <summary>
    /// A method that can be used as an ObjectDrawer delegate 
    /// for drawing segments of a snake
    /// 
    /// Relies on DrawObjectWithTransform to set the location and 
    /// rotation of the segment.
    /// 
    /// Draws a straight segment going upwards by default.
    /// </summary>
    /// <param name="o"></param>
    /// <param name="canvas"></param>
    private void SnakeSegmentDrawer(object o, ICanvas canvas)
    {
        int snakeSegmentLength = (int)o;
        canvas.DrawLine(0, 0, 0, snakeSegmentLength);
    }

    /// <summary>
    /// A method that can be used as an ObjectDrawer delegate
    /// </summary>
    /// <param name="o"></param>
    /// <param name="canvas"></param>
    private void DrawWall(Wall w, ICanvas canvas)
    {
        bool isVertical;
        int numOfSprites = 0;
        double rotation, x, y;

        // Calculate the orientation of the wall
        isVertical = w.p1.X == w.p2.X;
        // Calculate the length of the wall
        numOfSprites = 1 + (isVertical ? (int)Math.Abs(w.p1.Y - w.p2.Y) / 50 : (int)Math.Abs(w.p1.X - w.p2.X) / 50);

        // Draw the wall one sprite at a time
        if (isVertical)
        {
            // Determine the direction to draw the wall.
            bool drawDown = w.p1.Y < w.p2.Y;

            // Draw the wall
            for (int i = 0; i < numOfSprites; i++)
            {
                // Calculate the position of the sprite
                x = w.p1.X - 25;
                y = drawDown ? w.p1.Y + (50 * i) + 25 : w.p1.Y - (50 * i);
                rotation = 270;
                // Draw the sprite
                DrawObjectWithTransform(canvas, null, x, y, rotation, WallSpriteDrawer);
            }
        }
        else // It's horizontal
        {
            // Determine the direction to draw the wall
            bool drawRight = w.p1.X < w.p2.X;

            // Draw the wall
            for (int i = 0; i < numOfSprites; i++)
            {
                // Calculate the position of the sprite
                x = (drawRight ? w.p1.X + (50 * i) : w.p1.X - (50 * i)) - 25;
                y = w.p1.Y - 25;
                rotation = 0;
                // Draw the sprite
                DrawObjectWithTransform(canvas, null, x, y, rotation, WallSpriteDrawer);
            }
        }
    }

    /// <summary>
    /// A method that can be used as an ObjectDrawer for 
    /// drawing wall sprites.
    /// 
    /// Wall sprites are 50x50 pixels.
    /// 
    /// Draws from the top-left going down 50 and right 50 
    /// from the center of the canvas.
    /// </summary>
    /// <param name="o"></param>
    /// <param name="canvas"></param>
    private void WallSpriteDrawer(object o, ICanvas canvas)
    {
        canvas.DrawImage(wallImg, 0, 0, 50, 50);
    }

    /// <summary>
    /// A method that can be used as an ObjectDrawer for 
    /// drawing powerups
    /// 
    /// Powerups are 16x16 pixels
    /// </summary>
    /// <param name="o"></param>
    /// <param name="canvas"></param>
    private void PowerupDrawer(object o, ICanvas canvas)
    {
        canvas.DrawImage(powerupsImg, 0, 0, 16, 16);
    }
    #endregion

}
