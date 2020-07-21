﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Composable.Testing
{
    [TestFixture, TestFixtureSource(typeof(PluggableComponentsTestFixtureSource))]
    public class DuplicateByPluggableComponentTest
    {
        public DuplicateByPluggableComponentTest(string _) {}
    }

    public class PluggableComponentsTestFixtureSource : IEnumerable<string>
    {
        static readonly List<string> Dimensions = CreateDimensions().ToList();
        public IEnumerator<string> GetEnumerator() => Dimensions.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        const string NCrunchDuplicateByDimensions = "TestUsingPluggableComponentCombinations";
        public static string[] CreateDimensions()
        {
            try
            {
                return File.ReadAllLines(NCrunchDuplicateByDimensions)
                           .Select(@this => @this.Trim())
                           .Where(line => !string.IsNullOrEmpty(line))
                           .Where(line => !line.StartsWith("#", StringComparison.InvariantCulture))
                           .ToArray();
            }
            catch(Exception e)
            {
                return  new[]{ e.ToString() };
            }
        }
    }
}
