using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SpreadsheetUtilities
{

    /// <summary>
    /// (s1,t1) is an ordered pair of strings
    /// t1 depends on s1; s1 must be evaluated before t1
    /// 
    /// A DependencyGraph can be modeled as a set of ordered pairs of strings.  Two ordered pairs
    /// (s1,t1) and (s2,t2) are considered equal if and only if s1 equals s2 and t1 equals t2.
    /// Recall that sets never contain duplicates.  If an attempt is made to add an element to a 
    /// set, and the element is already in the set, the set remains unchanged.
    /// 
    /// Given a DependencyGraph DG:
    /// 
    ///    (1) If s is a string, the set of all strings t such that (s,t) is in DG is called dependents(s).
    ///        (The set of things that depend on s)    
    ///        
    ///    (2) If s is a string, the set of all strings t such that (t,s) is in DG is called dependees(s).
    ///        (The set of things that s depends on) 
    //
    // For example, suppose DG = {("a", "b"), ("a", "c"), ("b", "d"), ("d", "d")}
    //     dependents("a") = {"b", "c"}
    //     dependents("b") = {"d"}
    //     dependents("c") = {}
    //     dependents("d") = {"d"}
    //     dependees("a") = {}
    //     dependees("b") = {"a"}
    //     dependees("c") = {"a"}
    //     dependees("d") = {"b", "d"}
    /// </summary>
    public class DependencyGraph
    {
        // Global Variables
        private Dictionary<string, HashSet<string>> dependents;
        private Dictionary<string, HashSet<string>> dependees;
        private int numberOfDependencies;


        /// <summary>
        /// Constructor: Creates an empty DependencyGraph.
        /// </summary>
        public DependencyGraph()
        {
            // Initialize each global variable
            dependents = new();
            dependees = new();
            numberOfDependencies = 0;
        }


        /// <summary>
        /// The number of ordered pairs in the DependencyGraph.
        /// </summary>
        public int Size
        {
            get { return numberOfDependencies; }
        }


        /// <summary>
        /// The numberOfDependencies of dependees(s).
        /// This property is an example of an indexer.  If dg is a DependencyGraph, you would
        /// invoke it like this:
        /// dg["a"]
        /// It should return the numberOfDependencies of dependees("a")
        /// </summary>
        public int this[string aDependentNode]
        {
            get
            {
                MakeSureDictionariesHaveCells(aDependentNode);
                return dependents[aDependentNode].Count;
            }
        }


        /// <summary>
        /// Reports whether dependents(s) is non-empty.
        /// </summary>
        public bool HasDependents(string aDependeeNode)
        {
            MakeSureDictionariesHaveCells(aDependeeNode);
            return dependees[aDependeeNode].Count > 0;
        }


        /// <summary>
        /// Reports whether dependees(s) is non-empty.
        /// </summary>
        public bool HasDependees(string aDependentNode)
        {
            MakeSureDictionariesHaveCells(aDependentNode);
            return dependents[aDependentNode].Count > 0;
        }


        /// <summary>
        /// Enumerates dependents(s). Enumerate means to list one by one.
        /// </summary>
        public IEnumerable<string> GetDependents(string aDependeeNode)
        {
            MakeSureDictionariesHaveCells(aDependeeNode);
            if (HasDependents(aDependeeNode))
                return dependees[aDependeeNode];
            // else
            return new HashSet<string>();
        }

        /// <summary>
        /// Enumerates dependees(s). Enumerate means to list one by one.
        /// </summary>
        public IEnumerable<string> GetDependees(string aDependentNode)
        {
            MakeSureDictionariesHaveCells(aDependentNode);
            if (HasDependees(aDependentNode))
                return dependents[aDependentNode];
            // else
            return new HashSet<string>();
        }


        /// <summary>
        /// <para>Adds the ordered pair (s,t), if it doesn't exist</para>
        /// 
        /// <para>This should be thought of as:</para>   
        /// 
        ///   t depends on s
        ///
        /// </summary>
        /// <param name="s"> s must be evaluated first. T depends on S</param>
        /// <param name="t"> t cannot be evaluated until s is</param>        /// 
        public void AddDependency(string origin, string destination)
        {
            MakeSureDictionariesHaveCells(origin, destination);

            // just return if the dependency already exists.
            if (dependees[origin].TryGetValue(destination, out _))
                return;

            // Add the new dependency
            dependees[origin].Add(destination);
            dependents[destination].Add(origin);

            // Adjust the numberOfDependencies if it changed.
            numberOfDependencies++;
        }

        /*
         * This method will be called when adding or removing any dependencies.
         * It makes sure the specified dependents (destinations) and dependees (origins) are 
         * contained in the dictionary. If not then they will be placed into it.
         * 
         */
        private void MakeSureDictionariesHaveCells(string origin, string destination)
        {
            // Make sure the dependees' HashSet has both nodes
            if (!dependees.ContainsKey(origin))
                dependees.Add(origin, new HashSet<string>());
            if (!dependees.ContainsKey(destination))
                dependees.Add(destination, new HashSet<string>());

            // Make sure the dependents' HashSet has both nodes
            if (!dependents.ContainsKey(destination))
                dependents.Add(destination, new HashSet<string>());
            if (!dependents.ContainsKey(origin))
                dependents.Add(origin, new HashSet<string>());
        }
        /*
         * This method is overloaded so that if any method tries to access a single node from 
         * a dictionary that doesn't currently contain that node, the node will be added.
         */
        private void MakeSureDictionariesHaveCells(string someNode)
        {
            if (!dependees.ContainsKey(someNode))
                dependees.Add(someNode, new HashSet<string>());

            if (!dependents.ContainsKey(someNode))
                dependents.Add(someNode, new HashSet<string>());
        }


        /// <summary>
        /// Removes the ordered pair (s,t), if it exists
        /// </summary>
        /// <param name="s"></param>
        /// <param name="t"></param>
        public void RemoveDependency(string origin, string destination)
        {
            MakeSureDictionariesHaveCells(origin, destination);

            // Just return if the dependency doesn't exists.
            if (!dependents[destination].TryGetValue(origin, out _))
                return;

            // Add the new dependency
            dependents[destination].Remove(origin);
            dependees[origin].Remove(destination);
            

            // Adjust the numberOfDependencies if it changed.
            numberOfDependencies--;
        }


        /// <summary>
        /// Removes all existing ordered pairs of the form (s,r).  Then, for each
        /// t in newDependents, adds the ordered pair (s,t).
        /// </summary>
        public void ReplaceDependents(string origin, IEnumerable<string> newDependents)
        {
            MakeSureDictionariesHaveCells(origin);

            IEnumerator<string> enumerator = dependees[origin].GetEnumerator();
            while (enumerator.MoveNext())
                RemoveDependency(origin, enumerator.Current);

            // Add the new dependencies.
            enumerator = newDependents.GetEnumerator();
            while (enumerator.MoveNext())
                AddDependency(origin, enumerator.Current);
        }


        /// <summary>
        /// Removes all existing ordered pairs of the form (r,s).  Then, for each 
        /// t in newDependees, adds the ordered pair (t,s).
        /// </summary>
        public void ReplaceDependees(string destination, IEnumerable<string> newDependees)
        {
            MakeSureDictionariesHaveCells(destination);

            // Get rid of the existing dependencies
            IEnumerator<string> enumerator = dependents[destination].GetEnumerator();
            while(enumerator.MoveNext())
                RemoveDependency(enumerator.Current, destination);

            // Add the new dependencies.
            enumerator = newDependees.GetEnumerator();
            while(enumerator.MoveNext())
                AddDependency(enumerator.Current, destination);
        }
    }
}