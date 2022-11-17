using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SpreadsheetUtilities
{
    /// <summary>
    /// Represents formulas written in standard infix notation using standard precedence
    /// rules.  The allowed symbols are non-negative numbers written using double-precision 
    /// floating-point syntax (without unary preceeding '-' or '+'); 
    /// NormalVariables that consist of a letter or underscore followed by 
    /// zero or more letters, underscores, or digits; parentheses; and the four operator 
    /// symbols +, -, *, and /.
    /// 
    /// your code should verify that the only tokens are (, ), +, -, *, /, NormalVariables, and 
    /// decimal real numbers (including scientific notation).
    /// 
    /// Spaces are significant only insofar that they delimit tokens.  For example, "xy" is
    /// a single variable, "x y" consists of two NormalVariables "x" and y; "x23" is a single variable; 
    /// and "x 23" consists of a variable "x" and a number "23".
    /// 
    /// Associated with every builder are two delegates:  a normalizer and a validator.  The
    /// normalizer is used to convert NormalVariables into a canonical (valid/accepted) form, and the validator is used
    /// to add extra restrictions on the validity of a variable (beyond the standard requirement 
    /// that it consist of a letter or underscore followed by zero or more letters, underscores,
    /// or digits.)  Their use is described in detail in the constructor and method comments.
    /// 
    /// Last updated by Shem Snow
    /// On date: 09/16/2022
    /// 
    /// </summary>
    public class Formula
    {
        // Private Global Fields
        private readonly List<string> NormalFormulaAsEnumerable; // List<string> because I need to maintain order.
        private readonly string NormalFormula; // Gives ToString() O(1) complexity.
        private readonly HashSet<string> NormalVariables; // Simplifies GetVariables().

        /// <summary>
        /// Creates a SpreadsheetUtilities from a string that consists of an infix expression written as
        /// described in the class comment.  If the expression is syntactically invalid,
        /// throws a FormulaFormatException with an explanatory Message.
        /// 
        /// The associated normalizer is the identity function, and the associated validator
        /// maps every string to true.  
        /// </summary>
        public Formula(String formula) :
            this(formula, s => s, s => true)
        {
        }

        /// <summary>
        /// Creates a SpreadsheetUtilities from a string that consists of an infix expression written as
        /// described in the class comment.  If the expression is syntactically incorrect,
        /// throws a FormulaFormatException with an explanatory Message.
        /// 
        /// The associated normalizer and validator are the second and third parameters,
        /// respectively.  
        /// 
        /// If the builder contains a variable v such that normalize(v) is not a legal variable, 
        /// throws a FormulaFormatException with an explanatory message. 
        /// 
        /// If the builder contains a variable v such that isValid(normalize(v)) is false,
        /// throws a FormulaFormatException with an explanatory message.
        /// 
        /// Suppose that N is a method that converts all the letters in a string to upper case, and
        /// that V is a method that returns true only if a string consists of one letter followed
        /// by one digit.  Then:
        /// 
        /// new SpreadsheetUtilities("x2+y3", N, V) should succeed
        /// new SpreadsheetUtilities("x+y3", N, V) should throw an exception, since V(N("x")) is false
        /// new SpreadsheetUtilities("2x+y3", N, V) should throw an exception, since "2x+y3" is syntactically incorrect.
        /// </summary>
        public Formula(String formula, Func<string, string> normalize, Func<string, bool> isValid)
        {
            // Get an Enumerable list of all the tokens.
            IEnumerable<string> RawTokens = GetTokens(formula);

            // Throw an exception if "RawTokens" is empty or null and therefore can't be enumerated.
            if ((!RawTokens.Any()))
                throw new FormulaFormatException("The input formula had no valid characters.");

            // Validate then normalize each token, appending each one to an enumerable list.
            NormalFormulaAsEnumerable = new List<string>();
            NormalVariables = new HashSet<string>();
            foreach (string token in RawTokens)
            {
                string normalizedToken;
                if (isValid(token))
                {
                    normalizedToken = normalize(token);
                    // Note that a buggy normalize method will invalidate NormalVariables.
                    // Therefor we need a second validity check after normalization.
                    if (isValid(normalizedToken))
                        NormalFormulaAsEnumerable.Add(normalizedToken);
                    else
                        throw new FormulaFormatException("A valid string was 'invalidated' by your normalize method.");
                }
                else
                {
                    throw new FormulaFormatException($"The invalid token \"{token}\" was found in the input formula.");
                }
                   

                if (IsVar(normalizedToken))
                    NormalVariables.Add(normalizedToken);
            }

            // Now check for errors 
            CheckForSyntaxErrors(NormalFormulaAsEnumerable);

            // Save a string of the normalized formula so ToString() can return a variable
            // instead of building a new string every time it's called.
            NormalFormula = BuildStringFromEnumerable(NormalFormulaAsEnumerable);
        }

        /// <summary>
        /// Evaluates this SpreadsheetUtilities, using the lookup delegate to determine the values of
        /// NormalVariables.
        /// 
        /// When a variable symbol v needs to be determined, it should be looked up
        /// via lookup(normalize(v)). (Here, normalize is the normalizer that was passed to 
        /// the constructor.)
        /// Evaluate doesn't have visability of normalize. Does this mean I need another global field?
        /// No, because Evaluate will only ever be called on normalized NormalVariables.
        /// 
        /// For example, if L("x") is 2, L("X") is 4, and N is a method that converts all the letters 
        /// in a string to upper case:
        /// 
        /// new SpreadsheetUtilities("x+7", N, s => true).Evaluate(L) is 11
        /// new SpreadsheetUtilities("x+7").Evaluate(L) is 9
        /// 
        /// Given a variable symbol as its parameter, lookup returns the variable's value 
        /// (if it has one) or throws an ArgumentException (otherwise).
        /// 
        /// If no undefined NormalVariables or divisions by zero are encountered when evaluating 
        /// this SpreadsheetUtilities, the value is returned.  Otherwise, a FormulaError is returned.  
        /// The Reason property of the FormulaError should have a meaningful explanation.
        ///
        /// This method should never throw an exception.
        /// </summary>
        public object Evaluate(Func<string, double> lookup)
        {
            Stack<double> valStack = new();
            Stack<string> opStack = new();

            // Push each token into the stack
            foreach (string token in NormalFormulaAsEnumerable)
            {
                string resultOfPush = PushToken(token, valStack, opStack, lookup);

                if (resultOfPush != "No Errors.")
                    return new FormulaError(resultOfPush);
            }

            // Now, if the opStack is empty then return the only element in the valStack
            // Otherwise, do one more operation before returning the result.
            if (opStack.Count != 0)
                DoOperation(valStack, opStack, valStack.Pop());
            
            return valStack.Pop();
        }



        /// <summary>
        /// Enumerates the normalized versions of all of the NormalVariables that occur in this 
        /// builder. NO NORMALIZATION MAY APPEAR MORE THAN ONCE IN THE ENUMERATION, even 
        /// if it appears more than once in this SpreadsheetUtilities. So use a HashSet<string>.
        /// 
        /// For example, if N is a method that converts all the letters in a string to upper case:
        /// 
        /// new SpreadsheetUtilities("x+y*z", N, s => true).GetVariables() should enumerate "X", "Y", and "Z"
        /// new SpreadsheetUtilities("x+X*z", N, s => true).GetVariables() should enumerate "X" and "Z".
        /// new SpreadsheetUtilities("x+X*z").GetVariables() should enumerate "x", "X", and "z".
        /// </summary>
        public IEnumerable<String> GetVariables()
        {
            // Return a Hashset<string> containing a copy of all the elements in "NormalVariables".
            return new HashSet<string>(NormalVariables);
        }

        /// <summary>
        /// Returns a string containing no spaces which, if passed to the SpreadsheetUtilities
        /// constructor, will produce a SpreadsheetUtilities f such that this.Equals(f).  All of the
        /// NormalVariables in the string should be normalized.
        /// 
        /// For example, if N is a method that converts all the letters in a string to upper case:
        /// 
        /// new SpreadsheetUtilities("x + y", N, s => true).ToString() should return "X+Y"
        /// new SpreadsheetUtilities("x + Y").ToString() should return "x+Y"
        /// </summary>
        public override string ToString()
        {
            return NormalFormula;
        }

        /// <summary>
        /// If obj is null or obj is not a SpreadsheetUtilities, returns false.  Otherwise, reports
        /// whether or not this SpreadsheetUtilities and obj are equal.
        /// 
        /// Two Formulae are considered equal if they consist of the same tokens in the
        /// same order.  To determine token equality, all tokens are compared as strings 
        /// except for numeric tokens and variable tokens.
        /// Numeric tokens are considered equal if they are equal after being "normalized" 
        /// by C#'s standard conversion from string to double, then back to string. This 
        /// eliminates any inconsistencies due to limited floating point precision.
        /// Variable tokens are considered equal if their normalized forms are equal, as 
        /// defined by the provided normalizer.
        /// 
        /// For example, if N is a method that converts all the letters in a string to upper case:
        ///  
        /// new SpreadsheetUtilities("x1+y2", N, s => true).Equals(new SpreadsheetUtilities("X1  +  Y2")) is true
        /// new SpreadsheetUtilities("x1+y2").Equals(new SpreadsheetUtilities("X1+Y2")) is false
        /// new SpreadsheetUtilities("x1+y2").Equals(new SpreadsheetUtilities("y2+x1")) is false
        /// new SpreadsheetUtilities("2.0 + x7").Equals(new SpreadsheetUtilities("2.000 + x7")) is true
        /// </summary>
        public override bool Equals(object? obj)
        {
            // If they are not both formulas then there's no way they can be equal.
            if (obj is null || obj.GetType() != typeof(Formula))
                return false;

            // I need to compare their two normal formulas but those are privately modified.
            // I can access an 'equivalent' of the "NormalFormulaAsEnumerable" field via ToString().
            return this.ToString().Equals(obj.ToString());
        }

        /// <summary>
        /// Reports whether f1 == f2, using the notion of equality from the Equals method.
        /// Note that f1 and f2 cannot be null, because their types are non-nullable
        /// </summary>
        public static bool operator ==(Formula f1, Formula f2)
        {
            // If both Formulas are null than they are equal.
            // They are also equal if neither are null and f1.Equals(f2) returns true. 
            if ((f1 is null && f2 is null) || ((f1 is not null && f2 is not null) && f1.Equals(f2)))
                return true;

            // Otherwise they are unequal.
            return false;
        }

        /// <summary>
        /// Reports whether f1 != f2, using the notion of equality from the Equals method.
        /// Note that f1 and f2 cannot be null, because their types are non-nullable
        /// </summary>
        public static bool operator !=(Formula f1, Formula f2)
        {
            return !(f1 == f2);
        }

        /// <summary>
        /// Returns a hash code for this SpreadsheetUtilities.  If f1.Equals(f2), then it must be the
        /// case that f1.GetHashCode() == f2.GetHashCode().  Ideally, the probability that two 
        /// randomly-generated unequal Formulae have the same hash code should be extremely small.
        /// </summary>
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        /// <summary>
        /// Given an expression, enumerates the tokens that compose it.  Tokens are left parenthesis;
        /// right parenthesis; one of the four operator symbols; a string consisting of a letter or _
        /// followed by zero or more letters, digits, or underscores; a double literal; and anything that
        /// doesn't match one of those patterns.  There are no empty tokens, and no token contains white space.
        /// </summary>
        private static IEnumerable<string> GetTokens(String formula)
        {
            // Patterns for individual tokens
            String lpPattern = @"\(";
            String rpPattern = @"\)";
            String opPattern = @"[\+\-*/]";
            String varPattern = @"[a-zA-Z_](?: [a-zA-Z_]|\d)*";
            String doublePattern = @"(?: \d+\.\d* | \d*\.\d+ | \d+ ) (?: [eE][\+-]?\d+)?";
            String spacePattern = @"\s+";

            // Overall pattern
            String pattern = String.Format("({0}) | ({1}) | ({2}) | ({3}) | ({4}) | ({5})",
                    lpPattern, rpPattern, opPattern, varPattern, doublePattern, spacePattern);

            // Enumerate matching tokens that don't consist solely of white space.
            foreach (String s in Regex.Split(formula, pattern, RegexOptions.IgnorePatternWhitespace))
            {
                if (!Regex.IsMatch(s, @"^\s*$", RegexOptions.Singleline))
                {
                    // "yield return s;" returns the current expression (s) AND saves the current location.
                    // The next time this method is called, it will start at the current location
                    // instead of at the beginning.
                    yield return s; // s is each token.
                }
            }
        }

        // ___________________________________ Helper method for ToString and GetVariables ___________________________________

        /// <summary>
        /// Builds and returns a string from an IEnermerable<string>
        /// 
        /// This enables multiple methods(such as ToString()) to simply return a private field 
        /// instead of building a new one from scratch every time it's called.
        /// 
        /// </summary>
        /// <param name="strSet">an IEnumerable<string></param>
        /// <returns>the corresponding string.</returns>
        private string BuildStringFromEnumerable(IEnumerable<string> strSet)
        {
            // Create a new string builder, build it up, and then return its ToString().
            StringBuilder builder = new();
            double dump;
            foreach (string str in strSet)
            {
                // Take care of double precision. 2.00000000 should be equal to 2
                if(double.TryParse(str, out dump))
                    builder.Append(dump);
                else
                    builder.Append(str);
            }
            return builder.ToString();
        }

        // ___________________________________ Helper methods for the Constructors ___________________________________

        /// <summary>
        /// This method performs the final validity checks for a constructed SpreadsheetUtilities class.
        /// 
        /// If it is called, then that means all tokens within the Enumerable collection are recognized.
        /// This method checks that there are no syntactical errors and the current SpreadsheetUtilities is a valid one.
        /// 
        /// Note that the logic for these error checks is the same as the regular expressions in the 
        /// GetTokens() method.
        /// 
        /// </summary>
        /// <param name="NormalTokens">the normal formula as an Enumerable collection of tokens.</param>
        /// <exception cref="FormulaFormatException"></exception>
        private void CheckForSyntaxErrors(List<string> NormalTokens)
        {
            // I need to count the number of each type of parenthesis.
            int numOpenParenth = 0;
            int numClosedParenth = 0;

            // I need to compare tokens next to each other in an iterative for-loop.
            string current = NormalTokens[0];
            string next;

            // Regular Expressions I need in order to check my SpreadsheetUtilities's syntax.
            String lpPattern = @"\(";
            String rpPattern = @"\)";
            String opPattern = @"[\+\-*/]";
            String varPattern = @"[a-zA-Z_](?: [a-zA-Z_]|\d)*";
            double doubleDump; // A place to store the result of parsing doubles.

            // Throw if the first token is not one of: "(", a double, or a variable
            if (!(Regex.IsMatch(current, lpPattern) || double.TryParse(current, out doubleDump) || Regex.IsMatch(current, varPattern)))
                throw new FormulaFormatException("The first character of the input formula " +
                    "was not one of: \"(\", a double, or a variable.");

            // Throw if the last token is not one of: ")", a double, or a variable.
            string last = NormalTokens[NormalTokens.Count - 1];
            if (!(Regex.IsMatch(last, rpPattern) || double.TryParse(last, out doubleDump) || Regex.IsMatch(last, varPattern)))
                throw new FormulaFormatException("The last character of the input formula " +
                    "was not one of: \")\", a double, or a variable.");

            // Iterate through the loop and throw as soon as any syntax errors are found.
            for (int i = 1; i < NormalTokens.Count; i++)
            {
                next = NormalTokens[i];

                // Throw if any token after a "(" are not one of: "(", a double, or a variable.
                if (Regex.IsMatch(current, lpPattern))
                {
                    numOpenParenth++;

                    if (!(Regex.IsMatch(next, lpPattern) || double.TryParse(next, out doubleDump) || Regex.IsMatch(next, varPattern)))
                        throw new FormulaFormatException("Your input formula contained a " +
                            "\"(\" that was not followed by a: \"(\", double, or a variable.");
                }

                else if (Regex.IsMatch(current, rpPattern))
                {
                    numClosedParenth++;
                    if (!(Regex.IsMatch(next, opPattern) || Regex.IsMatch(next, rpPattern)))
                        throw new FormulaFormatException("Your input formula contained a " +
                            "\")\", that was not followed by either another \")\" or an operator.");
                }

                // Throw if any token after a double or variable are not an operator or a ).
                else if (double.TryParse(current, out doubleDump) || Regex.IsMatch(current, varPattern))
                {
                    if (!(Regex.IsMatch(next, opPattern) || Regex.IsMatch(next, rpPattern)))
                        throw new FormulaFormatException("Your input formula contained a " +
                            "double or variable that was not followed by a \")\" or an operator.");
                }

                // All tokens after an operator must be doubles, variables or (
                else if (Regex.IsMatch(current, opPattern))
                {
                    if (!(double.TryParse(next, out doubleDump) || Regex.IsMatch(next, lpPattern) || Regex.IsMatch(next, varPattern)))
                        throw new FormulaFormatException("Your input formula contained either " +
                            "double operators or a \")\" after an operator.");
                }

                // Throw if at any point, there are more closed parenthesis than open.
                if (numClosedParenth > numOpenParenth)
                    throw new FormulaFormatException("The input formula has more \")\" than \"(\".");

                // Advance the loop
                current = NormalTokens[i];
            }

            // My loop did not consider the very last element. If it is a ")" then I should count it.
            if (Regex.IsMatch(current, rpPattern))
                numClosedParenth++;

            // Throw if there's a mis-matching number of Parenthesis.
            if (numOpenParenth != numClosedParenth)
                throw new FormulaFormatException("The input formula has a mis-matching number of parenthesis.");
        }


        /// <summary>
        /// Checks to see if a given token is a varible.
        /// 
        /// The logic (regular expression) for doing so is found in the GetTokens() method
        /// and is saved in a variable called "varPattern".
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private bool IsVar(string token)
        {
            return Regex.IsMatch(token, @"[a-zA-Z_](?: [a-zA-Z_]|\d)*");
        }

        // ___________________________________ Helper methods and struct for Evaluate ___________________________________

        /// <summary>
        /// This method pushes a given token onto one of the two provided stacks (valStack and opStack)
        /// and may further manipulate both of the stacks depending on what the value of the given token was
        /// If further manipulation is to be done then another helper method will be called.
        /// </summary>
        /// <param name="token">The new value to be pushed</param>
        /// <param name="valStack">Stack that holds numbers</param>
        /// <param name="opStack">Stack the holds operators</param>
        /// <param name="VariableEvaluator">converts variables into double that can be pushed to the valStack</param>
        /// <returns> A string that identifies the result of the push. </returns>
        private string PushToken(string token, Stack<double> valStack, Stack<string> opStack, Func<string, double> VariableEvaluator)
        {
            double dump;
            switch (token)
            {
                case "+":
                case "-":
                    return PushArithmetic(valStack, opStack, token);

                case "*":
                case "/":
                case "(":
                    opStack.Push(token);
                    return "No Errors.";

                case ")":
                    return PushClosedPar(valStack, opStack, token);
                default:
                    break;
            }

            // There are only other 3 possibilities from here: double, variable and invalid token.
            if (double.TryParse(token, out dump))
                return PushDouble(valStack, opStack, dump);
            
            // Make sure all variable tokens evaluate to a double.
            if (IsVar(token))
                try
                {
                    if (VariableEvaluator(token) == 111.111) // sentinal value
                        throw new Exception("");
                    return PushDouble(valStack, opStack, VariableEvaluator(token));
                }
                catch (Exception)
                {
                    return "There was an error is the Evaluate method.";
                }
            if (VariableEvaluator(token) == 111.111) // sentinal value
                return $"Could not lookup the variable {token}";
            return PushDouble(valStack, opStack, VariableEvaluator(token));
        }


        /// <summary>
        /// Tries to push a double onto the valStack and returns a boolean indicating whether or not 
        /// it was successful.
        /// There are no operations to perform if the opStack is empty or if its top element is 
        /// not one of the accepted operations.
        /// </summary>
        /// <param name="valStack"></param>
        /// <param name="opStack"></param>
        /// <param name="number"></param>
        /// <returns> A string that identifies the result of the push. </returns>
        private string PushDouble(Stack<double> valStack, Stack<string> opStack, double number)
        {
            // Push the new number into the valStack if the top operators are * or /.
            if (opStack.Count == 0 || !(opStack.Peek() == "*" || opStack.Peek() == "/"))
            {
                valStack.Push(number);
                return "No Errors.";
            }
            
            // Otherwise, do the required operation.
            return DoOperation(valStack, opStack, number);
        }


        /// <summary>
        /// This method handles closed parenthesis by performing all operations it can until an open
        /// parenthesis is found in the opStack. This is guarenteed to happen.
        /// 
        /// Precondition:
        ///       THE opStack IS NOT EMPTY IF THIS METHOD WAS CALLED.
        ///       It hold a recognized operation that's NOT A ")"
        /// </summary>
        /// <param name="valStack"></param>
        /// <param name="opStack"></param>
        /// <param name="token"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <returns> A string that identifies the result of the push. </returns>
        private string PushClosedPar(Stack<double> valStack, Stack<string> opStack, string token)
        {
            // The top operator after one more operation is a ")"
            DoOperation(valStack, opStack, valStack.Pop());
            opStack.Pop();

            // Do an additional "*" or "/" operation if they're in the opStack.
            string? op;
            opStack.TryPeek(out op);
            if (op == "*" || op == "/")
                return DoOperation(valStack, opStack, valStack.Pop());
            return "No Errors.";
        } 


        /// <summary>
        /// This method is called when iterating through the array of substrings. It uses the provided
        /// algorithm to add the top two elements in the stack.
        /// 
        /// Note: If there are multiple operations in sequence, this algorithm will perform ALL 
        /// of those  as well. This satisfies the PEMDAS requirement.
        /// </summary>
        /// <param name="valStack"></param>
        /// <param name="opStack"></param>
        /// <param name="token">it's value is always "+" or "-" otherwise this method would not have been called.</param>
        /// <returns> A string that identifies the result of the push. </returns>
        private string PushArithmetic(Stack<double> valStack, Stack<string> opStack, string token)
        {
            // If the opStack is empty, push the new operation into it.
            if (opStack.Count == 0)
            {
                opStack.Push(token);
                return "No Errors.";
            }

            // else check to see if multiple arithmetic operations should be performed.
            else if(Regex.IsMatch(opStack.Peek(), @"[\+\-*/]"))
            {
                // do the two arithmetic operation for the existing operator.
                DoOperation(valStack, opStack, valStack.Pop());
                opStack.Push(token); // Then push the new operation into the opStack.
            }

            else
                opStack.Push(token);

            return "No Errors."; // The push was successful.
        }


        /// <summary>
        /// Performs one operation using the top elements of the valStack and opStack as well 
        /// as the received parameter "number".
        /// 
        /// </summary>
        /// <param name="valStack"></param>
        /// <param name="opStack"></param>
        /// <param name="target"></param>
        /// <returns> A string that identifies the result of the operation.</returns>
        private string DoOperation(Stack<double> valStack, Stack<string> opStack, double target)
        {
            if (opStack.Count == 0)
                return "No Errors.";

            // Perform the operation
            double source = valStack.Pop();
            string operation = opStack.Pop();
            switch (operation)
            {
                case "+":
                    valStack.Push(source + target);
                    break;
                case "-":
                    valStack.Push(source - target);
                    break;
                case "*":
                    valStack.Push(source * target);
                    break;
                case "/":
                    if (target == 0)
                        return "Can't divide by zero.";
                    valStack.Push(source / target);
                    break;
                default:
                    break;
            }
            return "No Errors.";
        }
    }


    // _________________________________ Provided Classes and Structs _________________________________

    /// <summary>
    /// Used to report syntactic errors in the argument to the SpreadsheetUtilities constructor.
    /// </summary>
    public class FormulaFormatException : Exception
    {
        /// <summary>
        /// Constructs a FormulaFormatException containing the explanatory message.
        /// </summary>
        public FormulaFormatException(String message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Used as a possible return value of the SpreadsheetUtilities.Evaluate method.
    /// </summary>
    public struct FormulaError
    {
        /// <summary>
        /// Constructs a FormulaError containing the explanatory reason.
        /// </summary>
        /// <param name="reason"></param>
        public FormulaError(String reason)
            : this()
        {
            Reason = reason;
        }

        /// <summary>
        ///  The reason why this FormulaError was created.
        /// </summary>
        public string Reason { get; private set; }
    }

}