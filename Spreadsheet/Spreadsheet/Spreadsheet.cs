using Microsoft.VisualBasic;
using SpreadsheetUtilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;
using System.Transactions;
using System.Net.NetworkInformation;
using System.Data;
using static System.Net.Mime.MediaTypeNames;
using System.Reflection.Metadata.Ecma335;

namespace SS
{
    /// <summary>
    /// An AbstractSpreadsheet object represents the state of a simple spreadsheet.  A 
    /// spreadsheet consists of an infinite number of named cells.
    /// 
    /// A string is a cell name if and only if it consists of one or more letters,
    /// followed by one or more digits AND it satisfies the predicate IsValid.
    /// For example, "A15", "a15", "XY032", and "BC7" are cell names so long as they
    /// satisfy IsValid.  On the other hand, "Z", "X_", and "hello" are not cell names,
    /// regardless of IsValid.
    /// 
    /// Any valid incoming cell name, whether passed as a parameter or embedded in a formula,
    /// must be normalized with the Normalize method before it is used by or saved in 
    /// this spreadsheet.  For example, if Normalize is s => s.ToUpper(), then
    /// the Formula "x3+a5" should be converted to "X3+A5" before use.
    /// 
    /// A spreadsheet contains a cell corresponding to every possible cell name.  
    /// In addition to a name, each cell has a contents and a value.  The distinction is
    /// important.
    /// 
    /// The contents of a cell can be (1) a string, (2) a double, or (3) a Formula.  If the
    /// contents is an empty string, we say that the cell is empty.  (By analogy, the contents
    /// of a cell in Excel is what is displayed on the editing line when the cell is selected.)
    /// 
    /// In a new spreadsheet, the contents of every cell is the empty string.
    ///  
    /// The value of a cell can be (1) a string, (2) a double, or (3) a FormulaError.  
    /// (By analogy, the value of an Excel cell is what is displayed in that cell's position
    /// in the grid.)
    /// 
    /// If a cell's contents is a string, its value is that string.
    /// 
    /// If a cell's contents is a double, its value is that double.
    /// 
    /// If a cell's contents is a Formula, its value is either a double or a FormulaError,
    /// as reported by the Evaluate method of the Formula class.  The value of a Formula,
    /// of course, can depend on the values of variables.  The value of a variable is the 
    /// value of the spreadsheet cell it names (if that cell's value is a double) or 
    /// is undefined (otherwise).
    /// 
    /// Spreadsheets are never allowed to contain a combination of Formulas that establish
    /// a circular dependency.  A circular dependency exists when a cell depends on itself.
    /// For example, suppose that A1 contains B1*2, B1 contains C1*2, and C1 contains A1*2.
    /// A1 depends on B1, which depends on C1, which depends on A1.  That's a circular
    /// dependency.
    /// 
    /// Last updated by: Shem Snow
    /// On date: 9/30/2022
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Spreadsheet : AbstractSpreadsheet
    {
        // Global Fields
        [JsonProperty(PropertyName = "cells")]
        private Dictionary<string, Cell> nonEmptyCells;
        private DependencyGraph SSDependencies;
        private bool CHANGED;


        /// <summary>
        /// Zero parameter constructor.
        /// </summary>
        public Spreadsheet() : this(s => true, s => s, "default")
        {
        }

        /// <summary>
        /// Non-default constructor sets the abstract parent's "IsValid", "Normalize", and "Version" properties.
        /// It also creates a new empty Spreadsheet with no dependencies or non-empty cells.
        /// </summary>
        /// <param name="isValid"></param>
        /// <param name="normalize"></param>
        /// <param name="version"></param>
        public Spreadsheet(Func<string, bool> isValid, Func<string, string> normalize, string version)
            : base(isValid, normalize, version)
        {
            nonEmptyCells = new();
            SSDependencies = new();
        }

        /// <summary>
        /// 4 parameter constructor initializes the "Version" and two functors "IsValid" and "Normalize".
        /// It also receives a "filePath" that is used to read a pre-existing spreadsheet at the 
        /// specified file path and use it to create a new spreadsheet using the (potentially) new:
        /// validator, normalizer, and version.
        /// </summary>
        /// <param name="isValid"></param>
        /// <param name="normalize"></param>
        /// <param name="version"></param>
        /// <param name="filePath"></param>
        public Spreadsheet(string filePath, Func<string, bool> isValid, Func<string, string> normalize, string version)
            : this(isValid, normalize, version)
        {
            
            string oldVersion = ""; // If the version doesn't change, an exception will be thrown later.
            try
            {
                // Read the file
                string fileData = File.ReadAllText(filePath);

                // Serialize the file then add each one of its non-empty cells to this new spreadsheet.
                Spreadsheet? SS = JsonConvert.DeserializeObject<Spreadsheet>(fileData);

                // Save the old version so you can check if it changed (later).
                if (SS is not null && SS.Version is not null)
                    oldVersion = SS.Version;


                if (SS is not null)
                    foreach (string name in SS.nonEmptyCells.Keys)
                    {
                        // Throw if the name is invalid
                        if (!isValid(name))
                            throw new SpreadsheetReadWriteException("An invalid cell name was found in the file.");

                        // You have to set the contents from stringForm (because serializing spreadsheet didn't do it).
                        if (SS.nonEmptyCells.TryGetValue(name, out Cell? theCell))
                        {
                            ReplaceCellContents(name, theCell.StringForm);
                        } 
                    }
            } catch(Exception)
            {
                throw new SpreadsheetReadWriteException("The file can't be read, found, or it was empty.");
            }

            // Throw if the version in the saved spreadsheet is different from the one specified in the parameter.
            if (!oldVersion.Equals(version))
                throw new SpreadsheetReadWriteException("The Version was not properly updated.");
        }

        /// <summary>
        /// Enumerates the names of all the non-empty cells in the spreadsheet.
        /// </summary>
        /// <returns></returns>
        public override IEnumerable<string> GetNamesOfAllNonemptyCells()
        {
            return nonEmptyCells.Keys;
        }

        /// <summary>
        /// If name is invalid, throws an InvalidNameException.
        /// 
        /// Otherwise, returns the contents (as opposed to the value) of the named cell.  The return
        /// value should be either a string, a double, or a Formula.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="InvalidNameException"></exception>
        public override object GetCellContents(string name)
        {
            // Throw if the name is invalid
            ValidateName(ref name);

            // Try to get the cell from "nonEmptyStrings". Return an empty string if it's not possible.
            if (!nonEmptyCells.TryGetValue(name, out Cell? theCell))
                return "";

            // else get and return the cell's contents.
            return theCell.Content;
        }

        /// <summary>
        /// The contents of the named cell becomes number.  The method returns a
        /// list consisting of name plus the names of all other cells whose value depends, 
        /// directly or indirectly, on the named cell. The order of the list should be any
        /// order such that if cells are re-evaluated in that order, their dependencies 
        /// are satisfied by the time they are evaluated.
        /// 
        /// For example, if name is A1, B1 contains A1*2, and C1 contains B1+A1, the
        /// list {A1, B1, C1} is returned.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="number"></param>
        /// <returns>IList<string></returns>
        /// <exception cref="InvalidNameException"></exception>
        protected override IList<string> SetCellContents(string name, double number)
        {
            // Since all "SetCellContents" methods behave the same, there is a general helper method for them.
            return ReplaceCellContents(name, number);
        }

        /// <summary>
        /// The contents of the named cell becomes "text".  The method returns a
        /// list consisting of name plus the names of all other cells whose value depends, 
        /// directly or indirectly, on the named cell.
        /// 
        /// For example, if name is A1, B1 contains A1*2, and C1 contains B1+A1, the
        /// list {A1, B1, C1} is returned.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="text"></param>
        /// <returns>IList<string></returns>
        /// <exception cref="InvalidNameException"></exception>
        protected override IList<string> SetCellContents(string name, string text)
        {
            // If the text is an empty string,
            if (text == "")
            {
                List<string> empty = new();
                // Make sure it's not in "nonEmptyCells" and remove its dependencies.
                if (nonEmptyCells.ContainsKey(name))
                {
                    nonEmptyCells.Remove(name);
                    SSDependencies.ReplaceDependents(name, empty);
                }
                // then return an empty list<string>.
                return empty;
            }
            
            return ReplaceCellContents(name, text);
        }

        /// <summary>
        /// If changing the contents of the named cell to be the formula would cause a 
        /// circular dependency, throws a CircularException, and no change is made to the spreadsheet.
        /// 
        /// Otherwise, the contents of the named cell becomes "formula".  The method returns a
        /// list consisting of name plus the names of all other cells whose value depends,
        /// directly or indirectly, on the named cell.
        /// 
        /// For example, if name is A1, B1 contains A1*2, and C1 contains B1+A1, the
        /// list {A1, B1, C1} is returned.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="formula"></param>
        /// <returns>IList<string></returns>
        /// <exception cref="InvalidNameException"></exception>
        protected override IList<string> SetCellContents(string name, Formula formula)
        {
            HashSet<string> PreExistingDeps = (HashSet<string>)SSDependencies.GetDependees(name);
            HashSet<string> formulaDeps = (HashSet<string>)formula.GetVariables();

            // Replace the current dependees and throw if it creates a circular dependencee.
            SSDependencies.ReplaceDependees(name, formulaDeps);
            try
            {
                List<string> cellsToRecalc = GetCellsToRecalculate(name).ToList(); // Will throw on circular dependancy
                ReplaceCellContents(name, formula); // Replace the cell contents if there was no circular exception.

                return cellsToRecalc.Union(formulaDeps.ToList()).ToList();
            }
            catch (CircularException circle)
            {
                // If there was a circular dependency, undo the change then throw.
                SSDependencies.ReplaceDependees(name, PreExistingDeps);
                throw circle;
            }
        }

        /// <summary>
        /// Returns an enumeration, without duplicates, of the names of all cells whose
        /// values depend directly on the value of the named cell.  In other words, returns
        /// an enumeration, without duplicates, of the names of all cells that contain
        /// formulas containing name.
        /// 
        /// Note that there is no need to validate the name because this method is protected.
        /// Therefore, it can only be accessed by other methods which already happen to check 
        /// the name's validity.
        /// 
        /// For example, suppose that
        /// A1 contains 3
        /// B1 contains the formula A1 * A1
        /// C1 contains the formula B1 + A1
        /// D1 contains the formula B1 - C1
        /// The direct dependents of A1 are B1 and C1
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        protected override IEnumerable<string> GetDirectDependents(string name)
        {
            return new HashSet<string>(SSDependencies.GetDependents(name));
        }

        // _______________________  Private Helper Methods  ______________________________________

        /// <summary>
        /// Converts the received name into its normalized form and validates it.
        /// Will throw an InvalidNameException if it is not.
        /// </summary>
        /// <param name="name"></param>
        /// <exception cref="InvalidNameException"></exception>
        private void ValidateName(ref string name)
        {
            // Normalize
            name = Normalize(name);

            // Validate
            if(!(Regex.IsMatch(name, "^[a-zA-Z_]([a-zA-Z_]|\\d)*$") & IsValid(name)))
                throw new InvalidNameException();
        }

        /// <summary>
        /// Helper method for the three SetCellContents method.
        /// Changes the contents of the named cell to be the received "content".
        /// 
        /// Preconditions:
        ///         "name" must be valid. Otherwise, don't call this method.
        ///         "content" is valid. i.e., it is not an empty string "" or anything that would not be
        ///         placed into a valid cell.
        /// Postcondition: This method only replaces a cell's content, adds new dependecies, and recalculates the cell's 
        ///         value if it changed.
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="content"></param>
        /// <returns>IList<string></returns>
        private IList<string> ReplaceCellContents(string name, object content)
        {
            name = Normalize(name);
            Cell? theCell;
            // If there's an existing cell, replace its content.
            if (nonEmptyCells.TryGetValue(name, out theCell))
            {
                // If the contents are a formula, make sure to add an "=" to the front of them.
                if (content.GetType().Equals(typeof(Formula)))
                    theCell.Content = "=" + content.ToString();
                else
                    theCell.Content = content;
            }

            else // the cell doesn't exist: create it and set its contents to "name".
            {
                theCell = new(content, LookUp, Normalize, IsValid);
                nonEmptyCells.Add(name, theCell);
            }

            ReplaceDependees(name, content);
            CHANGED = true; // Any call to Set/Replace is considered a change.

            // Get a list of cells to recalculate, recalculate each one, then return the list.
           List<string> cellsToReCalc = GetCellsToRecalculate(name).ToList();
            foreach (string cell in cellsToReCalc)
                if (nonEmptyCells.TryGetValue(cell, out theCell))
                    theCell.Recalculate();

            // Return the list of Cells to recalculate.
            return cellsToReCalc;
        }

        /// <summary>
        /// This helper method will be called every time a cell's content is replaced.
        /// </summary>
        /// <param name="name"></param>
        private void ReplaceDependees(string name, Object content)
        {
            if (content as Formula is not null)
                SSDependencies.ReplaceDependees(name, ((Formula)content).GetVariables());
            else if (content as string is not null)
                SSDependencies.ReplaceDependees(name, new List<string>() { (string)content });
            else // content is a double
                SSDependencies.ReplaceDependees(name, new List<string>());
        }


        // _______________________  Methods Added for PS5  ______________________________________

        /// <summary>
        /// True if this spreadsheet has been modified since it was created OR SAVED                  
        /// (whichever happened most recently); false otherwise.
        /// </summary>
        public override bool Changed { get => CHANGED; protected set => CHANGED = value; }

        /// <summary>
        /// Writes the contents of this spreadsheet to the named file using a JSON format.
        /// The JSON object should have the following fields:
        /// "Version" - the version of the spreadsheet software (a string)
        /// "cells" - an object containing 0 or more cell objects
        ///           Each cell object has a field named after the cell itself 
        ///           The value of that field is another object representing the cell's contents
        ///               The contents object has a single field called "stringForm",
        ///               representing the string form of the cell's contents
        ///               - If the contents is a string, the value of stringForm is that string
        ///               - If the contents is a double d, the value of stringForm is d.ToString()
        ///               - If the contents is a Formula f, the value of stringForm is "=" + f.ToString()
        /// 
        /// For example, if this spreadsheet has a version of "default" 
        /// and contains a cell "A1" with contents being the double 5.0 
        /// and a cell "B3" with contents being the Formula("A1+2"), 
        /// a JSON string produced by this method would be:
        /// 
        /// {
        ///   "cells": {
        ///     "A1": {
        ///       "stringForm": "5"
        ///     },
        ///     "B3": {
        ///       "stringForm": "=A1+2"
        ///     }
        ///   },
        ///   "Version": "default"
        /// }
        /// 
        /// If there are any problems opening, writing, or closing the file, the method should throw a
        /// SpreadsheetReadWriteException with an explanatory message.
        /// </summary>
        public override void Save(string filepath)
        {
            try
            {
                // serialize
                string JsonSerial = JsonConvert.SerializeObject(this);

                // Write the serialization to the file (replacing any files with the same name).
                File.WriteAllText(filepath, JsonSerial);

                // CHANGED is now false.
                CHANGED = false;
            } catch(Exception) // Throw an exception if the file could not be opened, written, or closed.
            {
                throw new SpreadsheetReadWriteException("There was a problem opening, writting to, or closing the file.");
            }
        }

        /// <summary>
        /// If name is invalid, throws an InvalidNameException.
        /// 
        /// Otherwise, returns the value (as opposed to the contents) of the named cell.  The return
        /// value should be either a string, a double, or a SpreadsheetUtilities.FormulaError.
        /// </summary>
        public override object GetCellValue(string name)
        {
            // Throw if the name is invalid.
            ValidateName(ref name);

            // Return the cell's value if it exists.
            if (nonEmptyCells.TryGetValue(name, out Cell? theCell))
                return theCell.Value;
               
            else // return an empty string.
                return "";
        }

        /// <summary>
        /// If the Cell's name is invalid, throws an InvalidNameException.
        /// 
        /// Otherwise, if content parses as a double, the contents of the named
        /// cell becomes that double.
        /// 
        /// Otherwise, if content begins with the character '=', an attempt is made
        /// to parse the remainder of content into a Formula f using the Formula
        /// constructor.  There are then three possibilities:
        /// 
        ///   (1) If the remainder of content cannot be parsed into a Formula, a 
        ///       SpreadsheetUtilities.FormulaFormatException is thrown.
        ///       
        ///   (2) Otherwise, if changing the contents of the named cell to be f
        ///       would cause a circular dependency, a CircularException is thrown,
        ///       and no change is made to the spreadsheet.
        ///       
        ///   (3) Otherwise, the contents of the named cell becomes f.
        /// 
        /// Otherwise, the contents of the named cell becomes content.
        /// 
        /// If an exception is not thrown, the method returns a list consisting of
        /// name plus the names of all other cells whose value depends, directly
        /// or indirectly, on the named cell. The order of the list should be any
        /// order such that if cells are re-evaluated in that order, their dependencies 
        /// are satisfied by the time they are evaluated.
        /// 
        /// For example, if name is A1, B1 contains A1*2, and C1 contains B1+A1, the
        /// list {A1, B1, C1} is returned.
        /// </summary>
        public override IList<string> SetContentsOfCell(string name, string content)
        {
            // throw if the Cell's name is invalid.
            ValidateName(ref name);

            // Determine the content type then set it within the cell.
            if (double.TryParse(content, out double dump)) // content is a double
                return SetCellContents(name, dump);
            else if (content.Any() && content.ToString()[..1] == "=") // content is a Formula
            {
                return SetCellContents(name, new Formula(content.ToString()[1..(content.Length)], Normalize, IsValid));
            }
            else // content is a string
                return SetCellContents(name, content.ToString());
        }

        /// <summary>
        /// Searches for a non-empty cell specified by the "name" parameter and calculates its value as a double.
        /// If this can't be one then a FormulaFormatException is thrown.
        /// Special condition:
        ///     This method cannot convert formulas into doubles. Only variable names but this is okay because the 
        ///     "GetCellsToRecalculate" method calculates them in the proper order.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="FormulaFormatException"></exception>
        private double LookUp(string name)
        {
            // Save the specified cell
            Cell? theCell;
            if (nonEmptyCells.TryGetValue(name, out theCell))
                // return its value as a double.
                if (double.TryParse(theCell.Value.ToString(), out double value))
                    return value;
                else  // or a variable
                    return LookUp(theCell.StringForm);
            // Throw if any of the previous steps could not be completed.
            throw new FormulaFormatException("There is either no such cell or its value can not be evaluated.");
        }
    }

    /// <summary>
    /// This class represents an individual cell of a spreadsheet.
    /// 
    /// Every cell contains:
    ///     - content which can be a: string, double, or Formula.
    ///     - value which can be a: string, double, or FormulaError.
    /// 
    /// If the content is a string or a double then the value is the same thing.
    /// If the content is a Formula then the value is either a double or Formula Exception.
    /// Precondition: the normalizer should not change doubles because the constructor normalizes both strings and doubles.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal class Cell
    {
        //Global Fields
        private object content;
        private object value;
        private Func<string, double> LookUp;
        private Func<string, string> Normalize;
        private Func<string, bool> IsValid;
        [JsonProperty]
        private string? stringForm;


        /// <summary>
        /// A default constructor IS REQUIRED in order for cells to be serialized and de-serialized.
        /// </summary>
        internal Cell() : this("", s => 0, s => s, s => true)
        {

        }

        /// <summary>
        /// Constructor initializes the content and value as specified by the documentation.
        /// 
        /// PRECONDITION: The "contents" parameter must be one of: [string, double, Formula]
        ///     otherwise the cell will be instantiated with an invalid type.
        /// 
        /// If the contents is null then it sets the content and value to an empty string.
        /// </summary>
        /// <param name="contents"></param>
        internal Cell(object contents, Func<string, double> someLookUp, Func<string, string> Normalizer, Func<string, bool> Validator)
        {
            LookUp = someLookUp;
            Normalize = Normalizer;
            IsValid = Validator;

            // If the contents is a string or double, content == value.
            if (contents.GetType().Equals(typeof(string)) || contents.GetType().Equals(typeof(double)))
            {
                content = contents;
                value = contents;
                stringForm = contents.ToString() ;
            }
            else // it's a formula, value will be the result of calculating that formula.
            {
                content = contents;
                value = ((Formula)content).Evaluate(LookUp);

                stringForm = "=" + content.ToString();
            }
        }

        /// <summary>
        /// Property for accessing this Cell's string form.
        /// </summary>
        internal string StringForm
        {
            get { return (stringForm is null)? "":stringForm; }
        }

        /// <summary>
        /// Property for accessing and mutating this Cell's content.
        /// </summary>
        internal object Content
        {
            get { return content; }
            set {
                content = value;
                stringForm = value.ToString();
            }
        }
        /// <summary>
        /// Property for setting and getting the value
        /// </summary>
        internal object Value
        {
            get {
                // doubles
                if (double.TryParse(value.ToString(), out double dump))
                    return dump;

                // formulas
                string? theContent = content.ToString();
                //Formula f;
                if (theContent is not null && theContent.Substring(0,1) == "=")
                {
                    return new Formula(theContent.Substring(1, theContent.Length - 1)).Evaluate(LookUp);
                }

                // strings
                return value;
            }
        }

        /// <summary>
        /// Uses the Lookup function to recalculate and reset this cell's value.
        /// </summary>
        internal void Recalculate()
        {
            // if it's a formula, evaluate it.
            if (content.GetType().Equals(typeof(Formula)))
                value = ((Formula)content).Evaluate(LookUp);
                
            else // Content is a string or double
                value = content;
        }
    }
}
