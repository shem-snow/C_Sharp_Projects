Written by Shem Snow for CS 3500 during Fall 2022
Last Edited on Oct 21 2022

Set "SpreadsheetGUI" as the startup project.


Organization
------------

I added two extra helper classes "EmailSender.cs" and "SMSSender.cs" which were used in the project.

Global fields and the constructor are at the top above three different line breaks I added so that you could
easily understand my GUI's structure.

The first line break separates the method that handles clicks on the spreadsheet grid (individual cells)
The second line break separates the methods for handling menue items.
The third line break separates the helper methods I added.


Design decisions
----------------

- I synchronized a Spreadsheet and a SpreadsheetGrid object.
From the beginning I created both a Spreadsheet object and a SpreadsheetGrid object which I kept synchronized all
throughout the running time of the program. i.e. Every time the Grid was edited, the spreadsheet was also edited.

This required me to relate the coordinated (rows and columns) on the grid to the variable name used on both the
grid and in the spreadsheet. I did this by casting between integer coordinates and characters in the helper 
methods "GetCellName" and "GetCellCoordinates".

- I took the user's input then just checked every possible outcome.
My solution to editing cell contents was to display a prompt window the user could edit and save their response.
I simply checked every possible form of the response (formula, variable, double, and unexpected/invalid) then 
calculated its value.

At the end, I used the Spreadsheet object (this is why I synchronized SS and SSGrid) to keep track of all the 
dependencies and update the spreadsheet.

- I defined validators and a normalizer.
This required me to define validators and a normalizer. My biggest difficulty was in distinguishing which validator 
to be used in different parts of the program. Both the Spreadsheet and Formula constructors require a token 
validator and single variables require a variable validator. 



Additional Features
--------------------

- Collapsable flyout menu item.
I added flyout subitems to the fly items so that similar features could be 'nested'.
They exist within a parent tab and expand when you hover the mouse over them.

-Exporting via email and SMS
I added two Export methods that are triggered by clicking on their tab within the "file" menu.

Both of them disregarded whether or not the spreadsheet is saved and just immediately serialize the currently 
running spreadsheet (while temporarily disabling the user from editing it)
then they send the serialized JSON string of the spreadsheet to the user-provided email or phone number (via text).


Instructions
------------
Select cells by clicking on them. Doing so generates a prompted window that allows you to change the contents.

Click on then nover your mouse over the File and Help menus to expand them.
Some of their options (the ones with an arrow on them) can be hovered over to expand even more items.
Each option will call a method related to it. Simply click on it and watch the magic happen.

I added two extra helper classes "EmailSender.cs" and "SMSSender.cs" which were used in the project. 
Don't forget to account for them and their complexity when you grade.

IMPORTANT NOTE ABOUT TWILIO:
----------------------------
Twilio requires you to set your computers "Environment variables" and it will crash if they are not set.

Set them by:    -Searching on your computer's start menu "Environment variables" and opening it.
                -Clicking on "Environment variables"
                -Clicking on "new" to add them.
                    You need to add two new variables: my twilio SID and AuthToken.
                    The names can be whatever you want them to be but the values should be:
                        My twilio SID: "AC5020b9d84949e7644dbcea9fe5fa361d"
                        My twilio AuthorizationToken: "790986d997d2e1e8c9da4224f76d9f7e"
                    They are saved as global fields in my "SMSSender.cs" class and
                    You may need to restart visual studio to make sure they're saved.

IMPORTANT NOTE ABOUT SMTP:
----------------------------
The website I used (MailTrap) will only automatically forward the emails it receives if the recipient email
has allowed permission. A request for permission must be manually sent (emailed) through my MailTrap account.

All attempts to send email will be saved in my account regardless of whether or not the recipient allows permission.
I can manually check and forward each one.XX
