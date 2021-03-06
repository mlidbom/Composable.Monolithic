using System;
using System.Collections.Generic;
using System.Linq;
using Composable.Contracts;

namespace Composable.SystemCE.LinqCE
{
    /// <summary/>
    public static partial class EnumerableCE
    {
        /// <summary>
        /// Creates an enumerable consisting of the passed parameter values is order.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="values"></param>
        /// <returns></returns>
        public static IEnumerable<T> Create<T>(params T[] values)
        {
            Contract.ArgumentNotNull(values, nameof(values));
            return values;
        }

        /// <summary>
        /// Adds <paramref name="instances"/> to the end of <paramref name="source"/>
        /// </summary>
        public static IEnumerable<T> Append<T>(this IEnumerable<T> source, params T[] instances)
        {
            Contract.ArgumentNotNull(source, nameof(source), instances, nameof(instances));
            return source.Concat(instances);
        }

        /// <summary>
        /// <para>The inversion of Enumerable.Any() .</para>
        /// <para>Returns true if <paramref name="me"/> contains no elements.</para>
        /// </summary>
        /// <returns>true if <paramref name="me"/> contains no objects. Otherwise false.</returns>
        public static bool None<T>(this IEnumerable<T> me)
        {
            Contract.ArgumentNotNull(me, nameof(me));

            return !me.Any();
        }

        //Add these so that we don't waste effort enumerating these types to check if any entries exist.
        public static bool None<T>(this List<T> me) => me.Count == 0;
        public static bool None<T>(this IList<T> me) => me.Count == 0;
        public static bool None<T>(this IReadOnlyList<T> me) => me.Count == 0;
        public static bool None<T>(this T[] me) => me.Length == 0;

        /// <summary>
        /// <para>The inversion of Enumerable.Any() .</para>
        /// <para>Returns true if <paramref name="me"/> contains no elements.</para>
        /// </summary>
        /// <returns>true if <paramref name="me"/> contains no objects. Otherwise false.</returns>
        public static bool None<T>(this IEnumerable<T> me, Func<T,bool> condition)
        {
            Contract.ArgumentNotNull(me, nameof(me), condition, nameof(condition));

            return !me.Any(condition);
        }

        /// <summary>
        /// Chops an IEnumerable up into <paramref name="size"/> sized chunks.
        /// </summary>
        public static IEnumerable<IEnumerable<T>> ChopIntoSizesOf<T>(this IEnumerable<T> me, int size)
        {
            Contract.ArgumentNotNull(me, nameof(me));

            // ReSharper disable once GenericEnumeratorNotDisposed ReSharper is plain wrong again.
            using var enumerator = me.GetEnumerator();
            var yielded = size;
            while(yielded == size)
            {
                yielded = 0;
                var next = new T[size];
                while(yielded < size && enumerator.MoveNext())
                {
                    next[yielded++] = enumerator.Current;
                }

                if(yielded == 0)
                {
                    yield break;
                }

                yield return yielded == size ? next : next.Take(yielded);
            }
        }


        /// <summary>
        /// Acting on an <see cref="IEnumerable{T}"/> <paramref name="me"/> where T is an <see cref="IEnumerable{TChild}"/>
        /// returns an <see cref="IEnumerable{TChild}"/> aggregating all the TChild instances
        /// 
        /// Using SelectMany(x=>x) is ugly and unintuitive.
        /// This method provides an intuitively named alternative.
        /// </summary>
        /// <typeparam name="T">A type implementing <see cref="IEnumerable{TChild}"/></typeparam>
        /// <typeparam name="TChild">The type contained in the nested enumerables.</typeparam>
        /// <param name="me">the collection to act upon</param>
        /// <returns>All the objects in all the nested collections </returns>
        public static IEnumerable<TChild> Flatten<T, TChild>(this IEnumerable<T> me) where T : IEnumerable<TChild>
        {
            Contract.ArgumentNotNull(me, nameof(me));

            return me.SelectMany(obj => obj);
        }
    }
}