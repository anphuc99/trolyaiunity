using System;

namespace Features.StartScene.Infrastructure
{
	/// <summary>
	/// Feature-level cache access to core view event cache.
	/// </summary>
	public static class ViewEventCache
	{
		public static Core.Infrastructure.Views.ViewEventDescriptor[] GetOrCreate(Type viewType)
		{
			return Core.Infrastructure.Views.ViewEventCache.GetOrCreate(viewType);
		}
	}
}
