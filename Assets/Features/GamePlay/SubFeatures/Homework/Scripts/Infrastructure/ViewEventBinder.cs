using System;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Homework.Infrastructure
{
	/// <summary>
	/// Feature-level view event binder that forwards to core infrastructure.
	/// </summary>
	public sealed class ViewEventBinder
	{
		private readonly Core.Infrastructure.Views.ViewEventBinder _inner;

		public ViewEventBinder(MonoBehaviour view)
		{
			_inner = new Core.Infrastructure.Views.ViewEventBinder(view);
		}

		public void Bind()
		{
			_inner.Bind();
		}

		public void Unbind()
		{
			_inner.Unbind();
		}
	}
}
