# Authors: Ronald Foster & Shem Snow
# Last Edited: 12/08/2022


Description
------------------------
The solution runs a multiplayer Snake game that can be played by multiple people either online or on one machine.
This README document outlines the organization, design decisions, and challenges we faced during development.


Instructions for playing the game
---------------------------------
The server must be started before any of the clients otherwise the game will not run. A client must be run for each player
in the game. Start the server by running the "Server.exe" application within the "Server" project.
Start the client by running the "MainPage.xaml.cs" application within the "SnakeClient" project. Clients must then connect 
to the server by entering the server's name and their own "PlayerName" then clicking on the "Connect" button.
If running on one machine, "localhost" is the server's name.

Once the connection is made, each player controls their snake's movement via the WASD keys.


# Organization
------------------
We used the Model, View, Controller design pattern to separate each of those three concerns into their own projects.

-The model consists of a World class that represents the world at any given time by containing collections of 
all the objects that exist in the game (snakes, powerups, walls). Each one of those objects is defined by their own class
which describes their current state in the game (location, alive, etc..). In other words, the model is just all the objects 
that can exist in the world plus a world class which saves and maintains the current state of the game.

-There are a total of three controllers:
    1. "NetworkController" abstracts all of the networking/communication concerns of our program.
        It uses sockets to manage the communication between a server and its clients.
    2. "GameController" manages the server-client communications from the client's end by using 
        multiple 'listeners'/methods that 'activate' every time the user performs an action that 
        involves the server. There will be a Game Controller for each player connected to the game.
    3. "ServerController" manages the commmunications between the server and the model by processing 
        world updates and all the movement commands recieved from each player so that the "Server" 
        class can act as a landing point that does not touch the Model.

- The view (SnakeClient) handles the interface between the user and the game. It consists of a MAUI 
application and two important classes: "MainPage" which handled the actual view/panel/window that the game is played on 
and "WorldPanel" which simply drew pictures onto the MainPage.


# Design Decisions for PS8 (the Client)
---------------------------------------
We wrote the program in multiple stages calling using the format: "version.stage.concern".
Each day we met, our goal was to focus mostly on the concerns of one stage.
The completed program is version 1.0.0 and any additional features will be added after completion.


	Vers	Description					Skeleton Written	Completed
	0.1.0: Connecting to a server				---				11/18
	0.2.0: Controller									11/26
	0.3.0: The World [model]				11/17				11/21
	0.3.1: Walls						11/17				11/25
	0.3.2: Snakes						11/17				11/19
	0.3.3: Power-ups					11/17				11/19
	0.4.0: Update On Each Frame				11/18				11/26			
	0.4.2: De-serialize					11/18				11/26
	0.4.4: Draw Powerups and Snakes				11/17				11/19
	1.0.0: Additional Features				11/26				11/27

# Design Notes for PS8 (the Client)
-------------------------------------
	# 0.1
		- The MainPage has a reference to the GameController as well as a button and text-entries for initiating
		the connnection. To initiate a connection, it simply uses its controller reference to call connect.

		- The GameController just takes the server name and playername then calls the Networking 
		Controller's connect method so it doesn't have to include any socket logic.

	# 0.2
		- The Game Controller has a reference to the Networking controller (which handles Socket connections) as
		well as several methods for requesting some action in the server.

		- In order to decide which method should be called and when in order to avoid concurrency problems.
		Our solution was for the Network Controller just to have a single delegate called "OnNetworkAction" 
		and each action would reset that delegate to call another method so that it was impossible for them to 
		happen out of order.

		- The order in which the delegates would be called is: Connect, OnConnect, GetPlayerIDAndWorldSize, GetWalls, 
		and OnFrame.

		- The GameController also kept track of the direction the snake was moving and would send it off to the 
		NetWorkController every time the OnFrame method was called (the server constantly calls itt indirectly 
		through the UpdateWorld method in the model).

	# 0.3

		- The Model is essentially just a World object containing a bunch of JSON-compatible objects and a single 
		method "UpdateWorld" which receives a string of the changes to be made.

		- The challenges we had to overcome was parsing the string of new data (which we did with a regular expression)
		and locking the world object every time an update was made.

		- The trickiest problem we overcame was removing old no-longer-useful data from a socket each time the 
		world was updated. Doing so greatly increased the game's performance.

	#0.4
		- Communications between the server and client are terminated with a "\n"
		- Snakes are drawn one segment at a time moving head-to-tail.
		- World 'wrap-around' of the snake is handled by checking to see if a current segment is at the world 
		border and if it is, then the next segment will not be drawn.

		- Walls are drawn by 'placing' a 'drawer' onto the panel at a given position and orientation then moving 
		forward to draw each wall segment.

		- The greatest challenges here were shifting the wall sprite by 25 units 
		and getting its orientation right so the wall's position in the game was accurate. We did these by comparing 
		x and y coordinates of the start and end points of each wall sprite. Whether or not the value of start was 
		less than the value of end would determine which direction to draw the sprites in.



# Design Decisions for PS9 (the Server)
---------------------------------------

	- We abstracted all collision-determination logic into a helper method called "AreColliding" which we overloaded 
	5 different times to handle every possible type of collision:
		two snakes, a snake and a list<Vector2D>, a snake and itself, a snake head or body with a 2DVector.

	- Determining collisions between world objects was done by calculating their top-left and bottom-right borders 
	and imagining them as rectangles. If one rectangle's bottom right coordinate was to the left and above the other
	rectangle's top left coordinate then it was not possible for them to overlap. After checking every possible way 
	that two rectangles COULD NOT intersect then we concluded whether or not they did intersect.

	- Snake movemovent was done by representing the snake as a list of 2D vectors which could be imagined as segments
	that each contain two joints and so only the head and tail needed to be moved.
	When the ending joints had equal value then the last segment could be deleted.

	- Snake size was increased by giving each snake a "foodInBelly" field which would hold off on moving the tail
	of the snake forward until there was no more food in the snake's belly


# Progress Notes for PS9 (the Server)
------------------------------------

	- 11/30
		# Project Structure and API completed
		# We learned about reading xml files and started implementing the Server class by updating the model.

	- 12/6
		# Snakes respawn when they die
		# Refactored code and separated concerns (there were long blocks of logic that could be split into helper methods)
		# Handled socketstate errors so the server did not crash upon clients disconnecting unexpectedly.
		# Verifed that disconnected clients are being removed properly
		# Print into the console when the server was ready for clients
		# Received move commands from the client
		# Fixed Snake parameter serialization to be in the right order.
		# Implemented logic for checking snake segment collisions: self-collisions, wall collisions, and collisions with other snakes
	
	- 12/7
		# Checked the diameters of each object (snakes, powerups, and walls) for collision barrier
		# Snakes now die when they collide with other snakes and themselves
		# Reset snake respawn timers when they die
		# Verifed that disconnected clients were being removed properly
		# Wraped snakes around the world when it reached an edge
		# Finished serializing Snake parameters in the proper order
		# Debuged and Fixed snake segment collisions: self-collisions, wall collisions, and collisions with other snakes

	- 12/8
		# Remove unused using statements in the ENTIRE project, PS7, PS8, PS9
		# snake parameters are being serialized in the wrong order
		# Re-read all assignment instructions to make sure we're up to par
		# Refactored ALL ps9 code
		# Updated comments for the ENTIRE project, PS7, PS8, PS9
		# Stopped allowing snakes to turn so fast that they kill themselves
		# Implemented two Game modes (Team death match and Free for all).
		# Checked that the settings.xml file will be properly downloaded from the github


Additional Feature (Game Modes)
--------------------------------
	— There are two game modes for Snake. Select the one to play by editing the "GameMode" field within the "settings.xml" file.

		# "Team Death Match": In this game mode, snakes with an even client id can pass through each other 
		without dying. And snakes with an odd client id can pass through each other without dying.

		# "Free For All": In this game mode, no snake can pass through another snake without dying.