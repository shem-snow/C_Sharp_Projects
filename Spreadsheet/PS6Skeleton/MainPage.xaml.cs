using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Compatibility;
using Newtonsoft.Json;
using SpreadsheetUtilities;
using SS;
using System.Net.Mail;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SpreadsheetGUI;

/// <summary>
/// This SpreadsheetGUI class is the intermediate object that handles all the interractions between the user and 
/// the spreadsheet. It requires a reference to a spreadsheet which is saved in the global field "ss".
/// </summary>
public partial class MainPage : ContentPage
{

    // Private Global Fields
    private Spreadsheet ss;
    private string runningFilePath;
    private EmailSender emailer;
    private SMSSender texter;

    /// Constructor
	public MainPage()
    {
        // Initialize the global fields
        ss = new(TokenValidate, s => s.ToUpper(), "ps6");
        emailer = new();

        // Set the default file path upon startup.
        runningFilePath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\PS6.sprd";

        InitializeComponent();

        // This an example of registering a method so that it is notified when
        // an event happens.  The SelectionChanged event is declared with a
        // delegate that specifies that all methods that register with it must
        // take a SpreadsheetGrid as its parameter and return nothing.  So we
        // register the SelectionListener method below.
        spreadsheetGrid.SelectionChanged += SelectionListener;
        // Start by selecting cell A1 by default.
        spreadsheetGrid.SetSelection(0, 0);
    }


    // _______________________________________________ Method for Handling Grid Clicks ___________________________________

    /// <summary>
    /// This is the functionality that is called when a cell is selected (my spreadsheet selects them when they are clicked).
    /// This method gets the coordinates and contents of the selected cell then allows the user to replace the contents
    /// which will cause a new value to be displayed.
    /// 
    /// </summary>
    /// <param name="grid"></param>
    private async void SelectionListener(SpreadsheetGrid grid)
    {
        // Get the currently selected cell's coordinates, name, contents, and value.
        spreadsheetGrid.GetSelection(out int col, out int row);
        string cellName = GetCellName(col, row);
        object oldContent = ss.GetCellContents(cellName);
        spreadsheetGrid.GetValue(col, row, out string cellValue);

        // Prompt the user to overwrite the contents. Immediately returning if there is no response.
        string response = await DisplayPromptAsync($"Cell {cellName}", "Replace the Contents", initialValue: oldContent.ToString().ToUpper());
        if (response is null)
            return; // It's null if you click cancel.


        // Their response (the new cell contents) should be one of three types (string(single variables and empty inputs), double, Formula).
        // Display an error if it was anything different. Meanwhile, also make sure the value is displayed on the SSGrid.
        string cellContent = response.ToString().ToUpper();
        // Empty input
        if (response == "")
        {
            spreadsheetGrid.SetValue(col, row, ""); // It's "" if you click ok on empty input.
            ss.SetContentsOfCell(cellName, cellContent);
        }
        // doubles
        else if (double.TryParse(cellContent, out double parsedDub))
        {
            spreadsheetGrid.SetValue(col, row, parsedDub.ToString()); // evaluate a string or double.
            ss.SetContentsOfCell(cellName, cellContent);
        }

        // Formulas
        else if (cellContent[0].Equals('='))
        {
            try // Evaluating.
            {
                // Create the formula
                Formula formula = new Formula(cellContent.ToString().Substring(1), s => s.ToUpper(), TokenValidate);

                // Check for circular dependencies.
                List<string> variables = formula.GetVariables().ToList();
                foreach (string variable in variables)
                {
                    if (variable == cellName)
                        throw new FormulaFormatException($"The input \"{response}\" would cause {cellName} to depend on itself.");
                }

                // Update the ss's contents and SSGrid's values.
                ss.SetContentsOfCell(cellName, cellContent);
                spreadsheetGrid.SetValue(col, row, formula.Evaluate(LookUp).ToString());
            }
            catch (FormulaFormatException formEx) // FormulaError.
            {

                await DisplayAlert("Invalid Cell Input", $"The input \"{response}\" is not a valid cell content because {formEx}.", "OK");
                return;
            }
        }
        // Invalid variables.
        else if (!VariableValidate(cellContent))
        {
            await DisplayAlert("Invalid Cell Input", $"The input \"{response}\" is not a valid cell content. Are you missing an \"=\"?", "OK");
            return;
        }
        // Circular dependencies
        else if (cellName == cellContent)
        {
            await DisplayAlert("Invalid Cell Input", $"The input \"{response}\" would cause {cellName} to depend on itself.", "OK");
            return;
        }
        // Valid variables
        else
        {
            // If a variable is undefined, display a default FormulaError.
            double varValue = LookUp(cellContent.ToString());
            if (varValue == 111.111) // sentinal value which indicates that the cell's content could not be parsed.
            {
                spreadsheetGrid.SetValue(col, row, new FormulaError("Undefined Variable").ToString());
                ss.SetContentsOfCell(cellName, cellContent);
            }
            else // Display its defined value.
            {
                spreadsheetGrid.SetValue(col, row, varValue.ToString());
                ss.SetContentsOfCell(cellName, cellContent);
            }
            return;
        }

        try // Updating the value of the selected cell and all its dependencies.
        {
            // Doing so returns a list of all its dependent cells. Save that list.
            List<string> cellsToRecalc;
            try
            {
                cellsToRecalc = ss.SetContentsOfCell(cellName, cellContent).ToList();
            }
            catch (FormulaFormatException)
            {
                cellsToRecalc = new Formula(cellContent.ToString().Substring(1), s => s.ToUpper(), TokenValidate).GetVariables().ToList();
            }

            // Reverse the list of cells to recalculate
            cellsToRecalc.Reverse();

            foreach (string cell in cellsToRecalc)
            {
                // Ignore it if it was never edited.
                if (ss.GetCellContents(cell).Equals(""))
                    continue;

                // Determine its coordinates
                GetCellCoordinates(cell, out int cellCol, out int cellRow);

                // Get the cell's value from the SS then update it on the SSGrid.
                string value = ss.GetCellValue(cell).ToString();
                if (value == "" || value == "SpreadsheetUtilities.FormulaError")
                    spreadsheetGrid.SetValue(cellCol, cellRow, new FormulaError("Undefined Variable").ToString());

                else
                    spreadsheetGrid.SetValue(cellCol, cellRow, new Formula(value.ToString(), s => s.ToUpper(), TokenValidate).Evaluate(LookUp).ToString());
            }
        }
        catch (CircularException circEx)
        {
            // Alert
            await DisplayAlert("Invalid Cell Input", $"The input \"{response}\" causes a circular exception because {circEx}.", "OK");
            // Reset the cell's content to what it was before user input.
            spreadsheetGrid.SetValue(col, row, oldContent.ToString());
        }
    }

    // _______________________________________________ Methods for Menu Items __________________________________

    /// <summary>
    /// When the "Send Email" menu bar item is clicked, this method will prompt the user for a recipient email
    /// if it does not exist then will serialize the current SS and email it to the specified recipient.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void SendEmail(Object sender, EventArgs e)
    {
        // If a receiving email is not set, prompt the user for one.
        // Remove this conditional to force the user to always input an email.
        //if (emailer.Recipient.Equals("u1058151@umail.utah.edu"))
        //{
            string response = await DisplayPromptAsync("A recipient is not selected.", "Enter the (full) Email Address you want to send the serialized spreadsheet to");
            if (response is null)
                return; // It's null if you click cancel.

            // Set the recipient.
            emailer.Recipient = response;
        //}

        // Save and Serialize the current SS then email it.
        string JsonSerial = JsonConvert.SerializeObject(ss);
        string success = emailer.SendEmail(JsonSerial) ? "successfully" : "not";
        await DisplayAlert("Result of email", $"The email was {success} sent.", "OK");
    }

    /// <summary>
    /// When the "Send SMS" menu bar item is clicked, this method will prompt the user for a recipient phone number
    /// if it does not exist then will serialize the current SS and text it to the specified recipient.
    /// 
    /// I'm using a singleton to enforce that only one texter is ever made. This is because twilio will crash the program
    /// if the computer's Environmental variables are not set.
    /// </summary>
    private async void SendSMS(Object sender, EventArgs e)
    {
        string response = await DisplayPromptAsync("Select a recipient.", "Enter the phone number you want to send the serialized spreadsheet to.");
        if (response is null)
            return; // It's null if you click cancel.


        try // Sending the text message.
        {
            // Save and Serialize the current SS then email it.
            string JsonSerial = JsonConvert.SerializeObject(ss);

            // send the text message.
            if (texter is null)
                throw new Exception("texter was null.");
            string result = texter.SendSMS(JsonSerial, response) ? "" : "not";
            await DisplayAlert("Result of SMS", $"The text was {result} sent. Try again with a valid phone number.", "OK");
        }
        catch (Exception)
        {
            await DisplayAlert("Environment Variables are not set.", "Twilio requires you to set your computers \"Environment variables\"\n" +
                "\nIf this error is displayed then your local machine does not have them set.\n" +
                "Set them by:\n-Searching on your computer's start menu \"Environment variables\" and opening it.\n" +
                "-Clicking on \"Environment variables\"\n-Clicking on \"new\" to add them.\n" +
                "-You need to add two new variables: my twilio SID and AuthToken.\n\nThe names can be whatever you want them to be but the" +
                " values should be:\n\nMy twilio SID: \"AC5020b9d84949e7644dbcea9fe5fa361d\"\nMy twilio AuthorizationToken: \"790986d997d2e1e8c9da4224f76d9f7e\"" +
                "\n\nThey are saved as global fields in my \"SMSSender.cs\" class.\nYou may need to restart visual studio to make sure they're saved.", "OK");
        }

        
    }

    /// <summary>
    /// When the "new" button in the file tab is selected. The current spreadsheet will be overwritten with an empty one.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void NewClicked(Object sender, EventArgs e)
    {
        // Give the user the chance to save changes.
        UnsavedChangesAlert();
        // Set changed to false.
        ss.Save(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\PS6.sprd");

        // Clear the spreadsheet
        spreadsheetGrid.Clear();
    }

    /// <summary>
    /// Opens any file as text and prints its contents.
    /// Note the use of async and await, concepts we will learn more about
    /// later this semester.
    /// </summary>
    private async void OpenClicked(Object sender, EventArgs e)
    {
        try
        {
            FileResult fileResult = await FilePicker.Default.PickAsync();
            if (fileResult != null)
            {
                // Save the new filepath
                string filepath = fileResult.FullPath;

                // Give the user the chance to save changes into the old filepath.
                UnsavedChangesAlert();
                // Update the current file
                runningFilePath = filepath;

                // Optional debugging methods.
                //System.Diagnostics.Debug.WriteLine("Successfully chose file: " + fileResult.FileName);
                //System.Diagnostics.Debug.WriteLine("First 100 file chars:\n" + fileContents.Substring(0, 100));

                // Read the file
                string fileContents = File.ReadAllText(runningFilePath);

                // Deserialize the fileContents AND SAVE IT VIA ss's non-default constructor.
                ss = new Spreadsheet(filepath, TokenValidate, s => s.ToUpper(), "ps6");

                // Get all the non-empty cells
                List<string> nonEmptyCells = ss.GetNamesOfAllNonemptyCells().ToList();

                // Write each of them onto the Grid.
                foreach (string cell in nonEmptyCells)
                {
                    GetCellCoordinates(cell, out int col, out int row);
                    spreadsheetGrid.SetValue(col, row, ss.GetCellValue(cell).ToString());
                }
                ss.Save(runningFilePath);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No file selected.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error opening file:");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    /// <summary>
    /// Saves the current file into the currentFileName.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SaveClicked(Object sender, EventArgs e)
    {
        ss.Save(runningFilePath);
    }

    /// <summary>
    ///  This method will be called any time the user performs an operation that would result in a lost of unsaved data.
    ///  The user will be given the option to save that data before proceding or to discard the unsaved changes.
    /// </summary>
    private async void UnsavedChangesAlert()
    {
        // If an operation would result in a loss of result-saved data
        if (ss.Changed)
        {
            // Display the warning window and record the user's response
            bool answer = await DisplayAlert("Unsaved Changes", "There are unsaved changes in the current file \"" + runningFilePath + "\". Would you like to save them before proceding? If you don't, the unsaved changes will be lost.", "Save Changes", "Discard Changes");
            // Based on their response, either save the spreadsheet or do nothing to discard the changes.
            if (answer)
                ss.Save(runningFilePath);
        }
    }

    /// <summary>
    /// Info popup in the help menu
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void HelpSelecting(Object sender, EventArgs e)
    {
        await DisplayAlert("Selecting Cells", "You literally just click on whatever cell you want to select. " + "" +
            "Selected cells have a black bold outline.", "OK");
    }


    /// <summary>
    /// Info alert that appears when the user clicks on the changing cell contents option within the help menu.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void HelpChanging(Object sender, EventArgs e)
    {
        await DisplayAlert("Changing Cell Contents", "You have to click on individual cells for a prompted to pop " +
            "up that allows you to type in whatever you want the cell's value to be. Note that pre-existing " +
            "contents will be saved in that prompt. Also, dependencies will be recalculated as you edit the " +
            "spreadsheet.", "OK");
    }

    /// <summary>
    /// When The user clicks on the "sending emails" tab within the info menu, an alert containing information about it
    /// is displayed.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void AboutEmail(Object sender, EventArgs e)
    {
        await DisplayAlert("Sending Emails", "You can send a JSON serializatoin of the current spreadsheet to any " +
            "email you want. I used an email host provider called \"MailTrap\" to send and forward emails to" +
            "(It took a long time to learn how to do that so you better go easy on the grading).\n" +
            "Simply navigate to {file > Export > Export via Email} to see for yourself! " +
            "Whichever email address you input will receive a JSON serialization of the current spreadsheet.\n" +
            "Make sure to type in a valid email address.", "OK");
    }

    /// <summary>
    /// When The user clicks on the "sending SMS" tab within the info menu, an alert containing information about it
    /// is displayed.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void AboutSMS(Object sender, EventArgs e)
    {
        await DisplayAlert("Sending Text Messages", "You can send a JSON serializatoin of the current spreadsheet directly " +
            "to your phone. I used \"twilio\" to send SMS messages (It took a long time to learn how to do that).\n" +
            "Simply navigate to {file > Export > Export via SMS} to see for yourself!" +
            "You will be prompted to enter a phone number. If it's a valid one, then you will soon receive a text " +
            "message at that number of the serialized version of the currently running spreadsheet.", "OK");
    }
    // _______________________________________________ Helper methods for displaying cell values ___________________________________
    /// <summary>
    /// The validator TO BE USED ONLY FOR VARIABLE NAMES. It will break BOTH THE SS AND THE FORMULA CLASSESif you pass it into them.
    /// A string is a valid cell name if it is a capital (A-Z) letter followed by a one or two digit number (0-99).
    /// Note that in this assignment, we treat lower case variables as valid and will write them as capital
    /// letters elsewhere in the code.
    /// </summary>
    /// <param name="varName"></param>
    /// <returns></returns>
    private bool VariableValidate(string varName)
    {
        return Regex.IsMatch(varName, @"^[a-zA-Z]{1,1}[0-9]{1,2}$") && !Regex.IsMatch(varName, @"^[A-Z]{1,1}0{2,2}$");
    }

    /// <summary>
    /// Validator TO BE USED ONLY FOR A FORMULA CONSTRUCTOR. It validates each of the tokens within the received parameter.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private bool TokenValidate(string token)
    {
        // Operators
        if (token.Equals("+") || token.Equals("-") || token.Equals("*") || token.Equals("/") || token.Equals("(") || token.Equals(")"))
            return true;
        // doubles
        if (double.TryParse(token, out double dump))
            return true;
        // variables
        if (Regex.IsMatch(token, @"^[a-zA-Z]{1,1}[0-9]{1,2}$") && !Regex.IsMatch(token, @"^[A-Z]{1,1}0{2,2}$"))
            return true;

        return false;
    }

    /// <summary>
    /// The Lookup to be used in Formula's Evaluate().
    /// It's value is the result of looking into the SS and not the SSGrid
    /// </summary>
    /// <returns></returns>
    private double LookUp(string variable)
    {
        string theValue = ss.GetCellValue(variable).ToString();
        if (double.TryParse(theValue, out double dump))
            return dump;
        else
            return 111.111; // sentinal value
    }

    /// <summary>
    /// This method returns a cell's name bases on its row and column in the SSGrid.
    /// - column is the character which is 65 places higher on the ASCII table.
    /// - row is 1 place higher because SSGrid starts countring from zero.
    /// </summary>
    /// <param name="col"></param>
    /// <param name="row"></param>
    /// <returns></returns>
    private string GetCellName(int col, int row)
    {
        return (char)(col + 65) + (row + 1).ToString();
    }

    /// <summary>
    /// This method sets a cell's coordinates (which are passed by reference) based on its name.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="col"></param>
    /// <param name="row"></param>
    private void GetCellCoordinates(string name, out int col, out int row)
    {
        col = name[0] - 65;
        row = int.Parse(name.Substring(1)) - 1;
    }
}
