﻿using System;
using System.Collections.Generic;
using System.Linq;
using Composable.Logging;
using NSpec;
using NSpec.Domain;
using NSpec.Domain.Formatters;
using NUnit.Framework;

namespace Composable
{
    /// <summary>
    /// This class acts as as shim between NSpec and NUnit. If you inherit it instead of <see cref="NSpec.nspec"/> you can work as usual with nspec and nunit will execute your tests for you.
    /// </summary>
    [TestFixture]
    // ReSharper disable InconsistentNaming
    public abstract class nspec : NSpec.nspec
        // ReSharper restore InconsistentNaming
    {
        [Test]
        public void ValidateSpec()
        {
            var finder = new SpecFinder(new [] {GetType()});
            var tagsFilter = new Tags();
            var builder = new ContextBuilder(finder, tagsFilter, new DefaultConventions());
            var runner = new ContextRunner(tagsFilter, new MyFormatter(), false);

            ContextCollection result;
            var contextCollection = builder.Contexts().Build();
            lock(SafeConsole.SyncronizationRoot)
            {
                result = runner.Run(contextCollection);
            }

            if (result.Failures().Any())
            {
                throw new SpecificationException("unknown", result.Failures().First().Exception.InnerException);
            }
        }

        class MyFormatter : ConsoleFormatter, IFormatter, ILiveFormatter
        {
            static void WriteNoticeably(string message, params object[] formatwith)
            {
                message = $"#################################    {message}    #################################";
                SafeConsole.WriteLine(message, formatwith);
            }

            void IFormatter.Write(ContextCollection contexts)
            {
                //Not calling base here lets us get rid of its noisy stack trace output so it does not obscure our thrown exceptions stacktrace.
                SafeConsole.WriteLine();
                if(contexts.Failures().Any())
                {
                    WriteNoticeably("SUMMARY");
                    SafeConsole.WriteLine(Summary(contexts));

                    int currentFailure = 0;
                    foreach (var failure in contexts.Failures())
                    {
                        SafeConsole.WriteLine();
                        SafeConsole.Write("#################################  FAILURE {0} #################################", ++currentFailure);

                        var current = failure.Context;
                        var relatedContexts = new List<Context>() { current };
                        while (null != (current = current.Parent))
                        {
                            relatedContexts.Add(current);
                        }

                        var levels = relatedContexts.Select(me => me.Name)
                                                    .Reverse()
                                                    .Skip(1)
                                                    .Concat(new[] { failure.Spec + " - " + failure.Exception.Message });

                        var message = levels
                            .Select((name, level) => "\t".Times(level) + name)
                            .Aggregate(Environment.NewLine + "at: ", (agg, curr) => agg + curr + Environment.NewLine);

                        SafeConsole.WriteLine(message);

                        SafeConsole.WriteLine(WriteFailure(failure));
                    }
                }



                SafeConsole.WriteLine();
                WriteNoticeably("END OF NSPEC RESULTS");
                SafeConsole.WriteLine();
            }

            void ILiveFormatter.Write(Context context)
            {
                //base.Write(context);
            }

            void ILiveFormatter.Write(ExampleBase e, int level)
            {
                //base.Write(e, level);
            }
        }
    }

    class SpecificationException : Exception
    {
        public SpecificationException(string position, Exception exception) : base(position, exception) {}
    }
}