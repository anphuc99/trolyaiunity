using System;
using UnityEngine;

namespace Share.Utils
{
	/// <summary>
	/// Shared transform traversal helpers.
	/// </summary>
	public static class TransformUtils
	{
		/// <summary>
		/// Recursively searches for a child transform with the given name.
		/// </summary>
		/// <param name="root">Root transform to search from.</param>
		/// <param name="targetName">Exact name to match.</param>
		/// <returns>First matching child, or null.</returns>
		public static Transform FindChildByName(Transform root, string targetName)
		{
			if (root == null || string.IsNullOrWhiteSpace(targetName))
			{
				return null;
			}

			for (var i = 0; i < root.childCount; i++)
			{
				var child = root.GetChild(i);
				if (child == null)
				{
					continue;
				}

				if (string.Equals(child.name, targetName, StringComparison.Ordinal))
				{
					return child;
				}

				var nested = FindChildByName(child, targetName);
				if (nested != null)
				{
					return nested;
				}
			}

			return null;
		}
	}
}
