using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia;

namespace AvalonDock
{
	/// <summary>
	/// Provides helper members for extensions.
	/// </summary>
	internal static class Extensions
	{
		/// <summary>
		/// Executes the contains operation.
		/// </summary>
		/// <param name="collection">The collection.</param>
		/// <param name="item">The item.</param>
		/// <returns>true if the operation succeeds; otherwise, false.</returns>
		public static bool Contains(this IEnumerable collection, object item)
		{
			foreach (var o in collection)
				if (o == item) return true;
			return false;
		}

		/// <summary>
		/// Executes the for Each operation.
		/// </summary>
		/// <typeparam name="T">The t type.</typeparam>
		/// <param name="collection">The collection.</param>
		/// <param name="action">The action.</param>
		public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
		{
			foreach (var v in collection) action(v);
		}

		/// <summary>
		/// Executes the index Of operation.
		/// </summary>
		/// <typeparam name="T">The t type.</typeparam>
		/// <param name="array">The array.</param>
		/// <param name="value">The value.</param>
		/// <returns>The result of the operation.</returns>
		public static int IndexOf<T>(this T[] array, T value)
			where T : class
		{
			for (var i = 0; i < array.Length; i++)
				if (array[i] == value) return i;
			return -1;
		}

		/// <summary>
		/// Gets the get Value Or Default.
		/// </summary>
		/// <typeparam name="V">The value type.</typeparam>
		/// <param name="wr">The wr.</param>
		/// <returns>The requested value.</returns>
		public static V GetValueOrDefault<V>(this WeakReference wr)
		{
			return wr == null || !wr.IsAlive ? default : (V)wr.Target;
		}

		/// <summary>
		/// Executes the sum operation.
		/// </summary>
		/// <typeparam name="T">The t type.</typeparam>
		/// <param name="enumerable">The enumerable.</param>
		/// <param name="func">The func.</param>
		/// <returns>The result of the operation.</returns>
		public static Thickness Sum<T>(this IEnumerable<T> enumerable, Func<T, Thickness> func)
		{
			double top = 0, bottom = 0, left = 0, right = 0;
			foreach (var e in enumerable)
			{
				var t = func(e);
				left = t.Left;
				top += t.Top;
				right = t.Right;
				bottom += t.Bottom;
			}

			return new Thickness(left, top, right, bottom);
		}

		/// <summary>
		/// Executes the add operation.
		/// </summary>
		/// <param name="thickness">The thickness.</param>
		/// <param name="other">The other.</param>
		/// <returns>The result of the operation.</returns>
		public static Thickness Add(this Thickness thickness, Thickness other)
		{
			return new Thickness(
				thickness.Left + other.Left,
				thickness.Top + other.Top,
				thickness.Right + other.Right,
				thickness.Bottom + other.Bottom);
		}
	}
}