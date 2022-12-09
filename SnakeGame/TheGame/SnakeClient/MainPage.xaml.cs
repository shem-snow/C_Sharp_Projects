using Microsoft.UI.Xaml.Controls;

namespace SnakeGame;

using NetworkUtil;
using Windows.Gaming.Input;
using TextChangedEventArgs = Microsoft.Maui.Controls.TextChangedEventArgs;

/// <summary>
/// The Main Page is the interface between the user of this program (a human) and the program that controls it.
/// It handles things such as button clicks and keyboard inputs.
/// </summary>
public partial class MainPage : ContentPage
{
    GameController controller;
    World theWorld;

    // Default Constructor
    public MainPage()
    {
        theWorld = new World();
        controller = new GameController(InvalidateGraphicsView, NetworkErrorHandler,
            theWorld);

        InitializeComponent();
        worldPanel.theWorld = theWorld;
        graphicsView.Invalidate(); 
    }

    private void InvalidateGraphicsView()
    {
        graphicsView.Invalidate();
    }

    #region GUI focussing and Error Handling

    /// <summary>
    /// This just makes so when you click on text entries, the program focuses on them so you can type in them.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    void OnTapped(object sender, EventArgs args)
    {
        keyboardHack.Focus();
    }

    /// <summary>
    /// This just makes so when you click on text entries, the program focuses on them andyou can start typing in them.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ContentPage_Focused(object sender, FocusEventArgs e)
    {
        if (!connectButton.IsEnabled)
            keyboardHack.Focus();
    }

    /// <summary>
    /// Dispatch an error message to the main thread to be displayed
    /// 
    /// The error message is based on the state of our connection.
    /// </summary>
    /// <param name="state"></param>
    private void NetworkErrorHandler(SocketState state)
    {
        // Show the error
        Dispatcher.Dispatch(() => DisplayAlert("Error", state.ErrorMessage, "OK"));

        // Then re-enable the controlls so the user can reconnect to a new server with a new name.
        Dispatcher.Dispatch
            (
                () =>
                    {
                        connectButton.IsEnabled = true;
                        connectButton.BackgroundColor = Color.Parse("#512bd4");
                        connectButton.TextColor = Colors.White;
                        serverText.IsEnabled = true;
                        nameText.IsEnabled = true;
                    }
            );
    }

    #endregion

    #region Methods for Handling Keyboard Input

    /// <summary>
    /// This method is called every time the user types something into the text box that controls the snake.
    /// It reads the first letter then clears the entry's text.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    void OnTextChanged(object sender, TextChangedEventArgs args)
    {
        Entry entry = (Entry)sender;
        String text = entry.Text.ToLower();
        if (text == "w")
        {
            controller.MoveCommand("up");
        }
        else if (text == "a")
        {
            controller.MoveCommand("left");
        }
        else if (text == "s")
        {
            controller.MoveCommand("down");
        }
        else if (text == "d")
        {
            controller.MoveCommand("right");
        }
       
        // Reset the text
        entry.Text = "";
    }

    #endregion

    #region Methods for Handling User Clicks
    /// <summary>
    /// Event handler for the connect button
    /// We will put the connection attempt logic here in the view, instead of the controller,
    /// because it is closely tied with disabling/enabling buttons, and showing dialogs.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void ConnectClick(object sender, EventArgs args)
    {
        if (serverText.Text == "")
        {
            DisplayAlert("Error", "Please enter a server address", "OK");
            return;
        }
        if (nameText.Text == "")
        {
            DisplayAlert("Error", "Please enter a name", "OK");
            return;
        }
        if (nameText.Text.Length > 16)
        {
            DisplayAlert("Error", "Name must be less than 16 characters", "OK");
            return;
        }

        // Attempt to connect to the server.
        controller.Connect(serverText.Text, nameText.Text + "\n");

        // Disable the connect button and text entries so the user can't connect again or change the server or their name.
        connectButton.IsEnabled = false;
        connectButton.TextColor = Colors.Black;
        connectButton.BackgroundColor = Colors.Grey;
        serverText.IsEnabled = false;
        nameText.IsEnabled = false;

        keyboardHack.Focus();
    }

    /// <summary>
    /// Displays the controlls when a player clicks on "help".
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ControlsButton_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("Controls",
                     "W:\t\t Move up\n" +
                     "A:\t\t Move left\n" +
                     "S:\t\t Move down\n" +
                     "D:\t\t Move right\n",
                     "OK");
    }

    /// <summary>
    /// Displays the creation information about this MAUI program when the "about" button is clicked.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AboutButton_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("About",
      "SnakeGame solution\nArtwork by Jolie Uk and Alex Smith\nGame design by Daniel Kopta and Travis Martin\n" +
      "Implementation by Ronald Foster and Shem Snow\n" +
        "CS 3500 Fall 2022, University of Utah", "OK");
    }

    #endregion


}