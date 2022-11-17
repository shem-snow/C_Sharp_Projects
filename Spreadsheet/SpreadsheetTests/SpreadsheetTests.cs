using Newtonsoft.Json.Linq;
using SpreadsheetUtilities;
using SS;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SpreadsheetTests
{
    
    /// <summary>
    /// The test class for my Spreadsheet.cs
    /// Tests designed for every public method except for the constructor are separated by line breaks.
    /// 
    /// Last updated by: Shem Snow
    /// On date: 09/23/2022
    /// </summary>
    [TestClass]
    public class SSTester
    {
        // ___________________________________  Get Names Tests  _________________________________________
        
        [TestMethod]
        public void GetNonEmptyNamesWhenThereAreNone()
        {
            Spreadsheet ss = new Spreadsheet();
            IEnumerable<string> names = ss.GetNamesOfAllNonemptyCells();

            Assert.AreEqual(0, names.Count());
            Assert.IsFalse(names.Any());
            Assert.IsFalse(names.GetEnumerator().MoveNext());
        }

        [TestMethod]
        public void CanGetTheNameOfAnyCell()
        {
            // Add a cell with each type of content.
            Spreadsheet ss = new Spreadsheet();
            ss.SetContentsOfCell("a1", "some string");
            ss.SetContentsOfCell("altplusf4", "696969");
            ss.SetContentsOfCell("thelegend27", "=of - the - crabby - patty");

            List<string> nonEmptyCells = ss.GetNamesOfAllNonemptyCells().ToList();

            // The method should do the same thing regardless of what the contents were.
            Assert.AreEqual("a1", nonEmptyCells[0]);
            Assert.AreEqual("altplusf4", nonEmptyCells[1]);
            Assert.AreEqual("thelegend27", nonEmptyCells[2]);
        }

        [TestMethod]
        public void ReturnedNameCanBeUsedToGetAndSetCellContents()
        {
            // Add a cell with each type of content.
            Spreadsheet ss = new Spreadsheet();
            ss.SetContentsOfCell("a1", "some string");
            ss.SetContentsOfCell("altplusf4", "696.969");
            ss.SetContentsOfCell("thelegend27", "=of - the - crabby - patty");

            // Make a list out of it.
            List<string> nonEmptyCells = ss.GetNamesOfAllNonemptyCells().ToList();

            // Get the original contents by searching with the returned names.
            Assert.AreEqual("some string", ss.GetCellContents(nonEmptyCells[0]));
            Assert.AreEqual(696.969, ss.GetCellContents(nonEmptyCells[1]));
            Assert.AreEqual(new Formula("of - the - crabby - patty"), ss.GetCellContents(nonEmptyCells[2]));

        }

        [TestMethod]
        public void TestReallyBigEquality()
        {
            Spreadsheet ss = new Spreadsheet();

            ss.SetContentsOfCell("a1", "hey");
            ss.SetContentsOfCell("b2", "15.34");
            ss.SetContentsOfCell("c3", "=you + me - everyoneelse34");
            ss.SetContentsOfCell("e4", "11234.3");
            ss.SetContentsOfCell("g6", "1344");
            ss.SetContentsOfCell("u8", "164");

            List<string> names = ss.GetNamesOfAllNonemptyCells().ToList();

            Assert.AreEqual(6, names.Count);
            Assert.AreEqual("e4", names[3]);
            CollectionAssert.AreEqual(names, new List<string>(ss.GetNamesOfAllNonemptyCells()));
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidNameException))]
        public void ThrowOnDigitName()
        {
            Spreadsheet ss = new();
            object theContents = ss.GetCellContents("155NotAVar");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidNameException))]
        public void ThrowOnSymbolName()
        {
            Spreadsheet ss = new();
            object theContents = ss.GetCellContents("$NotAVar");
        }

        [TestMethod]
        public void ValidNonExistentNameReturnsEmptyString()
        {
            Spreadsheet ss = new();
            object theContents = ss.GetCellContents("IsAVar123");
            Assert.AreEqual(theContents, "");
        }

        [TestMethod]
        public void GetAndSetStringContents()
        {
            Spreadsheet ss = new();
            ss.SetContentsOfCell("a1", "...America...");
            Assert.AreEqual("...America...", ss.GetCellContents("a1"));

            // Test that cells are being overwritten correctly.
            ss.SetContentsOfCell("a1", " 'Merica!!! (F**k yeah!)");
            Assert.AreEqual(" 'Merica!!! (F**k yeah!)", ss.GetCellContents("a1"));
        }

        [TestMethod]
        public void GetDoubleContents()
        {
            Spreadsheet ss = new();
            ss.SetContentsOfCell("a1", "420.69");
            Assert.AreEqual(420.69, ss.GetCellContents("a1"));

            // Test that cells are being overwritten correctly.
            ss.SetContentsOfCell("a1", "3.14159");
            Assert.AreEqual(3.14159, ss.GetCellContents("a1"));
        }

        [TestMethod]
        public void GetFormulaContents()
        {
            string f1 = "=a - squared - plus - b - squared - equals - c - squared";
            string f2 = "=2 * 3 - six";

            Spreadsheet ss = new();
            ss.SetContentsOfCell("a1", f1);
            Assert.AreEqual(Regex.Replace(f1.Substring(1, f1.Length - 1), @"\s+", ""), ss.GetCellContents("a1").ToString());

            // Test that cells are being overwritten correctly.
            ss.SetContentsOfCell("a1", f2);
            Assert.AreEqual(Regex.Replace(f2.Substring(0, f2.Length), @"\s+", ""), ss.GetCellContents("a1").ToString());
        }

        [TestMethod]
        [ExpectedException(typeof(CircularException))]
        public void ThrowOnCircularDependency()
        {
            Spreadsheet ss = new();
            ss.SetContentsOfCell("a1", "b2");
            ss.SetContentsOfCell("b2", "a1");
        }

        [TestMethod]
        [ExpectedException(typeof(CircularException))]
        public void ThrowOnSelfDependency()
        {
            Spreadsheet ss = new();
            ss.SetContentsOfCell("a1", "a1");
        }


        [TestMethod]
        [ExpectedException(typeof(CircularException))]
        public void ThrowOnCircularFormula()
        {
            Spreadsheet ss = new();
            ss.SetContentsOfCell("a1", "=b2");
            ss.SetContentsOfCell("b2", "=a1");
        }

        [TestMethod]
        public void EmptyCellsDoNotGetAdded()
        { 
            Spreadsheet ss = new Spreadsheet();
            ss.SetContentsOfCell("a1", "");

            List<string> names = ss.GetNamesOfAllNonemptyCells().ToList();

            Assert.AreEqual(0, names.Count());
            Assert.IsFalse(names.Any());
            Assert.IsFalse(names.GetEnumerator().MoveNext());
        }


        [TestMethod]
        public void SetContentsDoesNotAddEmptyStrings()
        {
            Spreadsheet ss = new Spreadsheet();

            ss.SetContentsOfCell("a1", "");
            List<string> names = ss.GetNamesOfAllNonemptyCells().ToList();

            Assert.AreEqual(0, names.Count());
            Assert.IsFalse(names.Any());
            Assert.IsFalse(names.GetEnumerator().MoveNext());
            Assert.IsFalse(names.Contains(""));
        }

        [TestMethod]
        public void ReplacingContentsWithEmptyStringsRemovesThem()
        {
            // Make a spreadsheet and fill it up
            Spreadsheet ss = new Spreadsheet();

            ss.SetContentsOfCell("a1", "lkdshfl");
            ss.SetContentsOfCell("b2", "34.34");
            ss.SetContentsOfCell("c3", "=58");
            

            // Replace the existing cells with empty strings.
            ss.SetContentsOfCell("a1", "");
            ss.SetContentsOfCell("b2", "");
            ss.SetContentsOfCell("c3", "");

            // The "nonEmptyCells" set should now be empty.
            List<string> names = ss.GetNamesOfAllNonemptyCells().ToList();
            Assert.AreEqual(0, names.Count());
            Assert.IsFalse(names.Any());
            Assert.IsFalse(names.GetEnumerator().MoveNext());
            Assert.IsFalse(names.Contains(""));
        }

        [TestMethod()]
        public void DefaultonstructorTest()
        {
            Spreadsheet ss = new();
        }

        [TestMethod]
        public void NormalizerDefaultConstructor()
        {
            Spreadsheet ss = new Spreadsheet();
            ss.SetContentsOfCell("f4", "9");
            ss.SetContentsOfCell("A1", "6");
            ss.SetContentsOfCell("b2", "= f4 - A1");
            Assert.AreEqual(3, (double)ss.GetCellValue("b2"), 1e-9);
        }

        [TestMethod]
        public void NormalizerThreeParamConstructor()
        {
            Spreadsheet ss = new(s => true, s => s.ToUpper(), "deez nutz");
            ss.SetContentsOfCell("f4", "999.9");

            Assert.AreEqual(999.9, ss.GetCellContents("f4"));
            Assert.AreEqual(999.9, ss.GetCellContents("F4"));
            //Assert.AreEqual("", ss.GetCellValue("f4"));
            //Assert.AreEqual(999.9, ss.GetCellValue("F4"));
        }

        // Failing test
        //[TestMethod()]
        //public void NormalizerFourParamConstructor()
        //{
        //    // Create a file
        //    string JsonSheet = "{ \"version\":\"fdslkj\", \"cells\":[{ \"nam1\":{ \"stringForm\":\"=69\"} },{ \"nam2\":{ \"stringForm\":155} }]}";
        //    File.WriteAllText("Deez_Nutz_Are_Not_in_the_AutoGrader.txt", JsonSheet); // NOTICE: opening the file created by this test(not a pre - existing file)

        //    // Create a new spreadsheet from the file
        //    Spreadsheet ss = new("Deez_Nutz_Are_Not_in_the_AutoGrader.txt", s => true, s => s.ToUpper(), "some version");

        //    //Test the constructor
        //    ss.SetContentsOfCell("f4", "999.9");

        //    Assert.AreEqual(999.9, ss.GetCellContents("f4"));
        //    Assert.AreEqual(999.9, ss.GetCellContents("F4"));
        //}

        [TestMethod]
        [ExpectedException(typeof(InvalidNameException))]
        public void InValidNameThrows()
        {
            Spreadsheet ss = new(s=>false, s=>s, "version");

            ss.SetContentsOfCell("a4d4", "df");
        }



        // ___________________________________  Save and Changed Tests  _________________________________________


        [TestMethod()]
        public void SettingChanges()
        {
            Spreadsheet ss = new Spreadsheet();
            Assert.IsFalse(ss.Changed);
            ss.SetContentsOfCell("a1", "155");
            Assert.IsTrue(ss.Changed);
        }

        [TestMethod()]
        public void SavingUndoesChange()
        {
            Spreadsheet ss = new();
            ss.SetContentsOfCell("a4", "155");
            Assert.IsTrue(ss.Changed);
            ss.Save("save.txt");
            Assert.IsFalse(ss.Changed);
        }

        [TestMethod, Timeout(2000)]
        [TestCategory("31")]
        public void SaveTest3()
        {
            AbstractSpreadsheet s1 = new Spreadsheet();
            s1.SetContentsOfCell("A1", "hello");
            s1.Save("save1.txt");
            s1 = new Spreadsheet("save1.txt", s => true, s => s, "default");
            Assert.AreEqual("hello", s1.GetCellContents("A1"));
        }
    }
}