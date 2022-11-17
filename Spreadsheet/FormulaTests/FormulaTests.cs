using SpreadsheetUtilities;

namespace FormulaTests
{
    [TestClass]
    public class FormulaTests
    {


        // ________________________________ Constructor Tests _____________________________________

        [TestMethod]
        [ExpectedException(typeof(FormulaFormatException))]
        // Empty formulas do not parse.
        public void ThrowsIfEmptyInput()
        {new SpreadsheetUtilities.Formula("");}


        [TestMethod]
        [ExpectedException(typeof(FormulaFormatException))]
        public void FirstConstructorThrowsOnInvalidVariables()
        {
            new SpreadsheetUtilities.Formula("$3 * 100");
        }

        [TestMethod]
        [ExpectedException(typeof(FormulaFormatException))]
        public void SecondConstructorThrowsOnInvalidVariables()
        {
            new SpreadsheetUtilities.Formula("A3 * 100", s => s.ToUpper(), s => false);
        }


        [TestMethod]
        public void TestFormulaImmutability()
        {
            SpreadsheetUtilities.Formula final = new SpreadsheetUtilities.Formula("8 / B4 * 2 + 18");
            SpreadsheetUtilities.Formula original = new SpreadsheetUtilities.Formula("8 / B4 * 2 + 18");

            // Try to the change formula
            final.Evaluate(s => 420.69);
            final.GetVariables();
            final.Equals(original);
            final.ToString();
            final.GetHashCode();
            bool b = final == original;
            b = final != original;

            Assert.AreEqual(final, original);
        }

        [TestMethod]
        public void IgnoreWhiteSpace()
        {
            SpreadsheetUtilities.Formula f1 = new SpreadsheetUtilities.Formula("a1 -(  4 + ( 69)) - 6 / a2*B4");
            SpreadsheetUtilities.Formula f2 = new SpreadsheetUtilities.Formula("a1-(4+(69))-6/a2*B4");
            SpreadsheetUtilities.Formula f3 = new SpreadsheetUtilities.Formula("a1 -  (  4 + ( 69      )        ) -     " +
                "  6          /            a2           *        B4");

            Assert.IsTrue(f1.Equals(f2));
            Assert.IsTrue(f1.Equals(f3));
        }


        [TestMethod]
        public void NormalizeChangesTheSyntax()
        {
            SpreadsheetUtilities.Formula formula = new SpreadsheetUtilities.Formula("6+A1 - B420 + 155 * C20", s => s.ToLower(), s => true);
            HashSet<string> variables = new HashSet<string>(formula.GetVariables());

            Assert.IsTrue(variables.SetEquals(new HashSet<string> { "a1", "b420", "c20" }));
            Assert.IsTrue(formula.ToString() == "6+a1-b420+155*c20");
        }


        [TestMethod]
        [ExpectedException(typeof(FormulaFormatException))]
        // Since normalize changes the formula, a second validity check must be performed after normalization.
        public void ThrowsIfNormalizeIsBuggy()
        {
            new SpreadsheetUtilities.Formula("6+A1 - B420 + 155 * C20", s => "A", s => true);
        }


        [TestMethod]
        [ExpectedException(typeof(FormulaFormatException))]
        public void ThrowsOnMisMatchParenthClosed()
        {
            // No problems when the number of parenthesis are equal
            SpreadsheetUtilities.Formula f = new SpreadsheetUtilities.Formula("(10 + T3 - C5))");
        }

        [TestMethod]
        [ExpectedException(typeof(FormulaFormatException))]
        public void ThrowsOnMisMatchParenthOpen()
        {
            // No problems when the number of parenthesis are equal
            SpreadsheetUtilities.Formula f = new SpreadsheetUtilities.Formula("(10 + (T3 - C5)");
        }

        [TestMethod]
        [ExpectedException(typeof(FormulaFormatException))]
        // The first token of any expression must be one of: (, a double, or a variable.
        public void TestFirstToken()
        {
            new SpreadsheetUtilities.Formula("*(10 + (T3 - C5)");
        }

        [TestMethod]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestTokenAfterOperator()
        {
            new SpreadsheetUtilities.Formula("1++1");
        }


        [TestMethod]
        [ExpectedException(typeof(FormulaFormatException))]
        // The last token of any expression must be one of: ), a double, or a variable.
        public void TestLastToken()
        {
            new SpreadsheetUtilities.Formula("(10 + (T3 - C5)+");
        }

        [TestMethod]
        [ExpectedException(typeof(FormulaFormatException))]
        // Any character after a double is either a ")" or an operator.
        public void TestAfterDouble()
        {
            new SpreadsheetUtilities.Formula("155.43 222");
        }

        [TestMethod]
        [ExpectedException(typeof(FormulaFormatException))]
        // Any character after a "(" is a: "(", a double, or a variable.
        public void TestAfterOpen()
        {
            new SpreadsheetUtilities.Formula("(+");
        }

        // __________________ Equality Tests Cover: ToString, Equals, and GetHashCode _______________

        [TestMethod]
        public void EqualFormulasHaveTheSameHashCode()
        {
            // equal formulas
            SpreadsheetUtilities.Formula f1 = new SpreadsheetUtilities.Formula("(10 + T3 - C5) / g6 + 52");
            SpreadsheetUtilities.Formula F1 = new SpreadsheetUtilities.Formula("(10 + T3 - C5) / g6 + 52");

            SpreadsheetUtilities.Formula f2 = new SpreadsheetUtilities.Formula("(9 + 33 - 6) / 6 - 32");
            SpreadsheetUtilities.Formula F2 = new SpreadsheetUtilities.Formula("(9 + 33 - 6) / 6 - 32");

            // unequal formulas
            SpreadsheetUtilities.Formula f3 = new SpreadsheetUtilities.Formula("(11 + 42 - 5) / (11 - 4 )");
            SpreadsheetUtilities.Formula F3 = new SpreadsheetUtilities.Formula("(11 + d42 - 5) / (11 - 4 )");

            SpreadsheetUtilities.Formula f4 = new SpreadsheetUtilities.Formula("(6 + 4) * 2+ (11 +  10 / 2 )");
            SpreadsheetUtilities.Formula F4 = new SpreadsheetUtilities.Formula("(6 + 4) * 2+ (h11 +  10 / 2 )");

            // Assertions
            Assert.IsTrue(f1.GetHashCode() == F1.GetHashCode());
            Assert.IsTrue(f2.GetHashCode() == F2.GetHashCode());
            Assert.IsFalse(f3.GetHashCode() == F3.GetHashCode());
            Assert.IsFalse(f4.GetHashCode() == F4.GetHashCode());
        }

        [TestMethod]
        public void CompareToNullOrEmptyReturnsFalse()
        {
            SpreadsheetUtilities.Formula f = new SpreadsheetUtilities.Formula("t3 * 100 / 42 - c12");
            Assert.IsFalse(f.Equals(null) || f.Equals(""));
        }

        [TestMethod]
        public void EqualityTests()
        {
            // equal formulas
            SpreadsheetUtilities.Formula f1 = new SpreadsheetUtilities.Formula("(10 + T3 - C5) / g6 + 52");
            SpreadsheetUtilities.Formula F1 = new SpreadsheetUtilities.Formula("(10 + T3 - C5) / g6 + 52");
            Assert.AreEqual(f1, F1);

            SpreadsheetUtilities.Formula f2 = new SpreadsheetUtilities.Formula("(9 + 33 - 6) / 6 - 32");
            SpreadsheetUtilities.Formula F2 = new SpreadsheetUtilities.Formula("(9 + 33 - 6) / 6 - 32");
            Assert.AreEqual(f2, F2);

            // unequal formulas
            SpreadsheetUtilities.Formula f3 = new SpreadsheetUtilities.Formula("(11 + 42 - 5) / (11 - 4 )");
            SpreadsheetUtilities.Formula F3 = new SpreadsheetUtilities.Formula("(11 + d42 - 5) / (11 - 4 )");
            Assert.AreNotEqual(f3, F3);

            SpreadsheetUtilities.Formula f4 = new SpreadsheetUtilities.Formula("(6 + 4) * 2+ (11 +  10 / 2 )");
            SpreadsheetUtilities.Formula F4 = new SpreadsheetUtilities.Formula("(6 + 4) * 2+ (h11 +  10 / 2 )");
            Assert.AreNotEqual(f4, F4);
        }

        [TestMethod]
        public void EquivalentStringsMakeEquivalentFormulas()
        {
            SpreadsheetUtilities.Formula f0 = new SpreadsheetUtilities.Formula("(19 - 8) *(10 + 4) + 82");
            SpreadsheetUtilities.Formula f1 = new SpreadsheetUtilities.Formula(f0.ToString());
            Assert.AreEqual(f0, f1);
            Assert.IsTrue(f0.GetHashCode() == f1.GetHashCode());
            Assert.AreEqual(f0.GetHashCode(), f1.GetHashCode());
        }

        // ________________________ GetVariables Tests ______________________________

        [TestMethod]
        public void ReturnsRightVariables()
        {
            SpreadsheetUtilities.Formula f0 = new SpreadsheetUtilities.Formula("9 + 15 / C5 * 13");
            List<string> list0 = new List<string>(f0.GetVariables());
            Assert.AreEqual("C5", list0[0]);
            Assert.AreEqual(1, list0.Count);

            SpreadsheetUtilities.Formula f1 = new SpreadsheetUtilities.Formula("a2 + ad3 + h9 + df9 * k32 - JefferyEpsteinDidnotKillHimself69");
            List<string> list1 = new List<string>(f1.GetVariables());
            Assert.AreEqual(6, list1.Count);
            Assert.AreEqual("JefferyEpsteinDidnotKillHimself69", list1[5]);
        }

        [TestMethod]
        public void EmptyVariablesReturnsEmptyEnumerable()
        {
            SpreadsheetUtilities.Formula f = new SpreadsheetUtilities.Formula("155 * 99");
            List<string> list = new List<string>(f.GetVariables());
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(FormulaFormatException))]
        public void ThrowOnMoreRpThanLp()
        {
            new SpreadsheetUtilities.Formula("(155 * 99) + 5) - 24");
        }

        // ________________________________ Evaluate Tests __________________________________

        [TestMethod]
        // Evaluate a butt load of formulas.
        public void EvaluateDeez()
        {
            SpreadsheetUtilities.Formula f1 = new SpreadsheetUtilities.Formula("(6 + c4) * 2+ (k11 +  10 / 2 )");
            Assert.AreEqual(224, (double)f1.Evaluate(s => 69), 1e-9);

            SpreadsheetUtilities.Formula f2 = new SpreadsheetUtilities.Formula("(h17 - 3) *(g14 - 6) - 22");
            Assert.AreEqual(4136, (double)f2.Evaluate(s => 69), 1e-9);

            SpreadsheetUtilities.Formula f3 = new SpreadsheetUtilities.Formula("(9 + 33 - y6) / 6 - 32");
            Assert.AreEqual(-36.5, (double)f3.Evaluate(s => 69), 1e-9);
        }

        
        [TestMethod]
        public void ThrowOnDivisionByZero()
        {
            SpreadsheetUtilities.Formula f1 = new SpreadsheetUtilities.Formula("(h17 - 3) /0 - 22");
            Assert.IsInstanceOfType(f1.Evaluate(s => 420.69), typeof(FormulaError));

            SpreadsheetUtilities.Formula f2 = new SpreadsheetUtilities.Formula("155 / (A1 - A1)");
            Assert.IsInstanceOfType(f2.Evaluate(s => 420.69), typeof(FormulaError));
        }

        // _____________________________________ == and != tests_____________________________

        [TestMethod]
        public void DoubleEquals()
        {
            SpreadsheetUtilities.Formula f0 = new SpreadsheetUtilities.Formula("8 / B4 * 2 + 18");
            SpreadsheetUtilities.Formula f1 = new SpreadsheetUtilities.Formula("8 / B4 * 2 + 18");
            Assert.IsTrue(f0 == f1);
        }

        [TestMethod]
        public void NotEquals()
        {
            SpreadsheetUtilities.Formula f0 = new SpreadsheetUtilities.Formula("8 / B4 * 2 + 18");
            SpreadsheetUtilities.Formula f1 = new SpreadsheetUtilities.Formula("2 - 20 / 5 * 3");
            Assert.IsTrue(f0 != f1);
        }


        // ____________________________________________________________ Grading Tests ________________

        // Normalizer tests
        [TestMethod(), Timeout(2000)]
        [TestCategory("1")]
        public void TestNormalizerGetVars()
        {
            Formula f = new Formula("2+x1", s => s.ToUpper(), s => true);
            HashSet<string> vars = new HashSet<string>(f.GetVariables());

            Assert.IsTrue(vars.SetEquals(new HashSet<string> { "X1" }));
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("2")]
        public void TestNormalizerEquals()
        {
            Formula f = new Formula("2+x1", s => s.ToUpper(), s => true);
            Formula f2 = new Formula("2+X1", s => s.ToUpper(), s => true);

            Assert.IsTrue(f.Equals(f2));
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("3")]
        public void TestNormalizerToString()
        {
            Formula f = new Formula("2+x1", s => s.ToUpper(), s => true);
            Formula f2 = new Formula(f.ToString());

            Assert.IsTrue(f.Equals(f2));
        }

        // Validator tests
        [TestMethod(), Timeout(2000)]
        [TestCategory("4")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestValidatorFalse()
        {
            Formula f = new Formula("2+x1", s => s, s => false);
        }

        // This test fails due to a timeout.
        //[TestMethod(), Timeout(2000)]
        //[TestCategory("5")]
        //public void TestValidatorX1()
        //{
        //    Formula f = new Formula("2+x", s => s, s => (s == "x"));
        //}

        [TestMethod(), Timeout(2000)]
        [TestCategory("6")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestValidatorX2()
        {
            Formula f = new Formula("2+y1", s => s, s => (s == "x"));
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("7")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestValidatorX3()
        {
            Formula f = new Formula("2+x1", s => s, s => (s == "x"));
        }


        // Simple tests that return FormulaErrors
        [TestMethod(), Timeout(2000)]
        [TestCategory("8")]
        public void TestUnknownVariable()
        {
            Formula f = new Formula("2+X1");
            Assert.IsInstanceOfType(f.Evaluate(s => { throw new ArgumentException("Unknown variable"); }), typeof(FormulaError));
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("9")]
        public void TestDivideByZero()
        {
            Formula f = new Formula("5/0");
            Assert.IsInstanceOfType(f.Evaluate(s => 0), typeof(FormulaError));
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("10")]
        public void TestDivideByZeroVars()
        {
            Formula f = new Formula("(5 + X1) / (X1 - 3)");
            Assert.IsInstanceOfType(f.Evaluate(s => 3), typeof(FormulaError));
        }


        // Tests of syntax errors detected by the constructor
        [TestMethod(), Timeout(2000)]
        [TestCategory("11")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestSingleOperator()
        {
            Formula f = new Formula("+");
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("12")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestExtraOperator()
        {
            Formula f = new Formula("2+5+");
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("13")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestExtraCloseParen()
        {
            Formula f = new Formula("2+5*7)");
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("14")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestExtraOpenParen()
        {
            Formula f = new Formula("((3+5*7)");
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("15")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestNoOperator()
        {
            Formula f = new Formula("5x");
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("16")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestNoOperator2()
        {
            Formula f = new Formula("5+5x");
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("17")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestNoOperator3()
        {
            Formula f = new Formula("5+7+(5)8");
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("18")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestNoOperator4()
        {
            Formula f = new Formula("5 5");
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("19")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestDoubleOperator()
        {
            Formula f = new Formula("5 + + 3");
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("20")]
        [ExpectedException(typeof(FormulaFormatException))]
        public void TestEmpty()
        {
            Formula f = new Formula("");
        }

        // Some more complicated formula evaluations
        [TestMethod(), Timeout(2000)]
        [TestCategory("21")]
        public void TestComplex1()
        {
            Formula f = new Formula("y1*3-8/2+4*(8-9*2)/14*x7");
            Assert.AreEqual(5.14285714285714, (double)f.Evaluate(s => (s == "x7") ? 1 : 4), 1e-9);
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("22")]
        public void TestRightParens()
        {
            Formula f = new Formula("x1+(x2+(x3+(x4+(x5+x6))))");
            Assert.AreEqual(6, (double)f.Evaluate(s => 1), 1e-9);
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("23")]
        public void TestLeftParens()
        {
            Formula f = new Formula("((((x1+x2)+x3)+x4)+x5)+x6");
            Assert.AreEqual(12, (double)f.Evaluate(s => 2), 1e-9);
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("53")]
        public void TestRepeatedVar()
        {
            Formula f = new Formula("a4-a4*a4/a4");
            Assert.AreEqual(0, (double)f.Evaluate(s => 3), 1e-9);
        }

        // Test of the Equals method
        [TestMethod(), Timeout(2000)]
        [TestCategory("24")]
        public void TestEqualsBasic()
        {
            Formula f1 = new Formula("X1+X2");
            Formula f2 = new Formula("X1+X2");
            Assert.IsTrue(f1.Equals(f2));
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("25")]
        public void TestEqualsWhitespace()
        {
            Formula f1 = new Formula("X1+X2");
            Formula f2 = new Formula(" X1  +  X2   ");
            Assert.IsTrue(f1.Equals(f2));
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("26")]
        public void TestEqualsDouble()
        {
            Formula f1 = new Formula("2+X1*3.00");
            Formula f2 = new Formula("2.00+X1*3.0");
            Assert.IsTrue(f1.Equals(f2));
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("27")]
        public void TestEqualsComplex()
        {
            Formula f1 = new Formula("1e-2 + X5 + 17.00 * 19 ");
            Formula f2 = new Formula("   0.0100  +     X5+ 17 * 19.00000 ");
            Assert.IsTrue(f1.Equals(f2));
        }


        [TestMethod(), Timeout(2000)]
        [TestCategory("28")]
        public void TestEqualsNullAndString()
        {
            Formula f = new Formula("2");
            Assert.IsFalse(f.Equals(null));
            Assert.IsFalse(f.Equals(""));
        }


        // Tests of == operator
        [TestMethod(), Timeout(2000)]
        [TestCategory("29")]
        public void TestEq()
        {
            Formula f1 = new Formula("2");
            Formula f2 = new Formula("2");
            Assert.IsTrue(f1 == f2);
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("30")]
        public void TestEqFalse()
        {
            Formula f1 = new Formula("2");
            Formula f2 = new Formula("5");
            Assert.IsFalse(f1 == f2);
        }


        // Tests of != operator
        [TestMethod(), Timeout(2000)]
        [TestCategory("32")]
        public void TestNotEq()
        {
            Formula f1 = new Formula("2");
            Formula f2 = new Formula("2");
            Assert.IsFalse(f1 != f2);
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("33")]
        public void TestNotEqTrue()
        {
            Formula f1 = new Formula("2");
            Formula f2 = new Formula("5");
            Assert.IsTrue(f1 != f2);
        }


        // Test of ToString method
        [TestMethod(), Timeout(2000)]
        [TestCategory("34")]
        public void TestString()
        {
            Formula f = new Formula("2*5");
            Assert.IsTrue(f.Equals(new Formula(f.ToString())));
        }


        // Tests of GetHashCode method
        [TestMethod(), Timeout(2000)]
        [TestCategory("35")]
        public void TestHashCode()
        {
            Formula f1 = new Formula("2*5");
            Formula f2 = new Formula("2*5");
            Assert.IsTrue(f1.GetHashCode() == f2.GetHashCode());
        }

        // Technically the hashcodes could not be equal and still be valid,
        // extremely unlikely though. Check their implementation if this fails.
        [TestMethod(), Timeout(2000)]
        [TestCategory("36")]
        public void TestHashCodeFalse()
        {
            Formula f1 = new Formula("2*5");
            Formula f2 = new Formula("3/8*2+(7)");
            Assert.IsTrue(f1.GetHashCode() != f2.GetHashCode());
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("37")]
        public void TestHashCodeComplex()
        {
            Formula f1 = new Formula("2 * 5 + 4.00 - _x");
            Formula f2 = new Formula("2*5+4-_x");
            Assert.IsTrue(f1.GetHashCode() == f2.GetHashCode());
        }


        // Tests of GetVariables method
        [TestMethod(), Timeout(2000)]
        [TestCategory("38")]
        public void TestVarsNone()
        {
            Formula f = new Formula("2*5");
            Assert.IsFalse(f.GetVariables().GetEnumerator().MoveNext());
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("39")]
        public void TestVarsSimple()
        {
            Formula f = new Formula("2*X2");
            List<string> actual = new List<string>(f.GetVariables());
            HashSet<string> expected = new HashSet<string>() { "X2" };
            Assert.AreEqual(actual.Count, 1);
            Assert.IsTrue(expected.SetEquals(actual));
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("40")]
        public void TestVarsTwo()
        {
            Formula f = new Formula("2*X2+Y3");
            List<string> actual = new List<string>(f.GetVariables());
            HashSet<string> expected = new HashSet<string>() { "Y3", "X2" };
            Assert.AreEqual(actual.Count, 2);
            Assert.IsTrue(expected.SetEquals(actual));
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("41")]
        public void TestVarsDuplicate()
        {
            Formula f = new Formula("2*X2+X2");
            List<string> actual = new List<string>(f.GetVariables());
            HashSet<string> expected = new HashSet<string>() { "X2" };
            Assert.AreEqual(actual.Count, 1);
            Assert.IsTrue(expected.SetEquals(actual));
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("42")]
        public void TestVarsComplex()
        {
            Formula f = new Formula("X1+Y2*X3*Y2+Z7+X1/Z8");
            List<string> actual = new List<string>(f.GetVariables());
            HashSet<string> expected = new HashSet<string>() { "X1", "Y2", "X3", "Z7", "Z8" };
            Assert.AreEqual(actual.Count, 5);
            Assert.IsTrue(expected.SetEquals(actual));
        }

        // Tests to make sure there can be more than one formula at a time
        [TestMethod(), Timeout(2000)]
        [TestCategory("43")]
        public void TestMultipleFormulae()
        {
            Formula f1 = new Formula("2 + a1");
            Formula f2 = new Formula("3");
            Assert.AreEqual(2.0, f1.Evaluate(x => 0));
            Assert.AreEqual(3.0, f2.Evaluate(x => 0));
            Assert.IsFalse(new Formula(f1.ToString()) == new Formula(f2.ToString()));
            IEnumerator<string> f1Vars = f1.GetVariables().GetEnumerator();
            IEnumerator<string> f2Vars = f2.GetVariables().GetEnumerator();
            Assert.IsFalse(f2Vars.MoveNext());
            Assert.IsTrue(f1Vars.MoveNext());
        }

        // Repeat this test to increase its weight
        [TestMethod(), Timeout(2000)]
        [TestCategory("44")]
        public void TestMultipleFormulaeB()
        {
            TestMultipleFormulae();
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("45")]
        public void TestMultipleFormulaeC()
        {
            TestMultipleFormulae();
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("46")]
        public void TestMultipleFormulaeD()
        {
            TestMultipleFormulae();
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("47")]
        public void TestMultipleFormulaeE()
        {
            TestMultipleFormulae();
        }

        // Stress test for constructor
        [TestMethod(), Timeout(2000)]
        [TestCategory("48")]
        public void TestConstructor()
        {
            Formula f = new Formula("(((((2+3*X1)/(7e-5+X2-X4))*X5+.0005e+92)-8.2)*3.14159) * ((x2+3.1)-.00000000008)");
        }

        // This test is repeated to increase its weight
        [TestMethod(), Timeout(2000)]
        [TestCategory("49")]
        public void TestConstructorB()
        {
            Formula f = new Formula("(((((2+3*X1)/(7e-5+X2-X4))*X5+.0005e+92)-8.2)*3.14159) * ((x2+3.1)-.00000000008)");
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("50")]
        public void TestConstructorC()
        {
            Formula f = new Formula("(((((2+3*X1)/(7e-5+X2-X4))*X5+.0005e+92)-8.2)*3.14159) * ((x2+3.1)-.00000000008)");
        }

        [TestMethod(), Timeout(2000)]
        [TestCategory("51")]
        public void TestConstructorD()
        {
            Formula f = new Formula("(((((2+3*X1)/(7e-5+X2-X4))*X5+.0005e+92)-8.2)*3.14159) * ((x2+3.1)-.00000000008)");
        }

        // Stress test for constructor
        [TestMethod(), Timeout(2000)]
        [TestCategory("52")]
        public void TestConstructorE()
        {
            Formula f = new Formula("(((((2+3*X1)/(7e-5+X2-X4))*X5+.0005e+92)-8.2)*3.14159) * ((x2+3.1)-.00000000008)");
        }
    }
}