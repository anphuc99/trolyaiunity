using System;

namespace Features.GamePlay.SubFeatures.Knowledges.Infrastructure.Attributes
{
	/// <summary>
	/// Feature-level event attribute that forwards to core infrastructure.
	/// </summary>
	public sealed class OnEventAttribute : Core.Infrastructure.Attributes.OnEventAttribute
	{
		public OnEventAttribute(string key) : base(key)
		{
		}
	}
}
