﻿using Composable.System;
using Composable.Testing;
using NUnit.Framework;

namespace Composable.Tests.StrictlyManagedResource
{
    public class PerformanceTests
    {
        class StrictResource : IStrictlyManagedResource
        {
            public void Dispose() { }
        }

        [Test] public void Allocated_and_disposes_40_instances_in_10_millisecond_when_actually_collecting_stack_traces() =>
            TimeAsserter.Execute(() => new StrictlyManagedResource<StrictResource>(forceStackTraceCollection: true).Dispose(),
                                 iterations: 40,
                                 maxTotal: TimeSpanExtensions.Milliseconds(10),
                                 maxTries: 10,
                                 timeFormat: "s\\.ffffff");

        [Test] public void Allocates_and_disposes_7000_instances_in_10_millisecond_when_not_collecting_stack_traces() =>
            TimeAsserter.Execute(() => new StrictlyManagedResource<StrictResource>(forceStackTraceCollection: false).Dispose(),
                                 iterations: 7000,
                                 maxTotal: TimeSpanExtensions.Milliseconds(10),
                                 maxTries: 10,
                                 timeFormat: "s\\.ffffff");
    }
}
