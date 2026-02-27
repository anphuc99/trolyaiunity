using System;
using UnityEngine;
#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

namespace Features.JournalOverlay.Infrastructure
{
	/// <summary>
	/// Win32 window manager for controlling the Unity application window.
	/// Provides always-on-top, borderless, and resize/position functionality.
	/// Only operational on Windows Standalone; all methods are no-ops on other platforms.
	/// </summary>
	public static class NativeWindowManager
	{
#if UNITY_STANDALONE_WIN
		// --- Win32 constants ---
		private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
		private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
		private const uint SWP_NOMOVE = 0x0002;
		private const uint SWP_NOSIZE = 0x0001;
		private const uint SWP_SHOWWINDOW = 0x0040;
		private const uint SWP_FRAMECHANGED = 0x0020;
		private const int GWL_STYLE = -16;
		private const int WS_CAPTION = 0x00C00000;
		private const int WS_THICKFRAME = 0x00040000;
		private const int WS_MINIMIZEBOX = 0x00020000;
		private const int WS_MAXIMIZEBOX = 0x00010000;
		private const int WS_SYSMENU = 0x00080000;

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr GetActiveWindow();

		[DllImport("user32.dll", SetLastError = true)]
		private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		// --- Saved window state ---
		private static IntPtr _cachedHwnd = IntPtr.Zero;
		private static int _savedStyle;
		private static RECT _savedRect;
		private static bool _hasSavedState;
		private static bool _isTopmost;

		/// <summary>
		/// Retrieves the Unity main window handle. Cached after first call.
		/// </summary>
		private static IntPtr GetUnityWindowHandle()
		{
			if (_cachedHwnd == IntPtr.Zero)
			{
				_cachedHwnd = GetActiveWindow();
			}
			return _cachedHwnd;
		}
#endif

		/// <summary>
		/// Sets or clears always-on-top for the Unity window.
		/// </summary>
		/// <param name="enable">True to make window always-on-top.</param>
		public static void SetAlwaysOnTop(bool enable)
		{
#if UNITY_STANDALONE_WIN
			var hwnd = GetUnityWindowHandle();
			if (hwnd == IntPtr.Zero) return;

			var insertAfter = enable ? HWND_TOPMOST : HWND_NOTOPMOST;
			SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
			_isTopmost = enable;
#endif
		}

		/// <summary>
		/// Removes or restores the window border (title bar and thick frame).
		/// </summary>
		/// <param name="enable">True to make borderless.</param>
		public static void SetBorderless(bool enable)
		{
#if UNITY_STANDALONE_WIN
			var hwnd = GetUnityWindowHandle();
			if (hwnd == IntPtr.Zero) return;

			var style = GetWindowLong(hwnd, GWL_STYLE);
			if (enable)
			{
				style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
			}
			else if (_hasSavedState)
			{
				style = _savedStyle;
			}

			SetWindowLong(hwnd, GWL_STYLE, style);
			SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
				SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
#endif
		}

		/// <summary>
		/// Moves and resizes the Unity window.
		/// </summary>
		/// <param name="x">Screen X in pixels.</param>
		/// <param name="y">Screen Y in pixels.</param>
		/// <param name="width">Window width in pixels.</param>
		/// <param name="height">Window height in pixels.</param>
		public static void ResizeAndPosition(int x, int y, int width, int height)
		{
#if UNITY_STANDALONE_WIN
			var hwnd = GetUnityWindowHandle();
			if (hwnd == IntPtr.Zero) return;

			var insertAfter = _isTopmost ? HWND_TOPMOST : HWND_NOTOPMOST;
			SetWindowPos(hwnd, insertAfter, x, y, width, height, SWP_SHOWWINDOW);
#endif
		}

		/// <summary>
		/// Saves the current window position, size, and style so it can be restored later.
		/// </summary>
		public static void SaveWindowState()
		{
#if UNITY_STANDALONE_WIN
			var hwnd = GetUnityWindowHandle();
			if (hwnd == IntPtr.Zero) return;

			_savedStyle = GetWindowLong(hwnd, GWL_STYLE);
			GetWindowRect(hwnd, out _savedRect);
			_hasSavedState = true;
#endif
		}

		/// <summary>
		/// Restores the window to its previously saved position, size, and style.
		/// </summary>
		public static void RestoreWindowState()
		{
#if UNITY_STANDALONE_WIN
			if (!_hasSavedState) return;

			var hwnd = GetUnityWindowHandle();
			if (hwnd == IntPtr.Zero) return;

			SetWindowLong(hwnd, GWL_STYLE, _savedStyle);
			SetWindowPos(hwnd, HWND_NOTOPMOST,
				_savedRect.Left, _savedRect.Top,
				_savedRect.Right - _savedRect.Left,
				_savedRect.Bottom - _savedRect.Top,
				SWP_SHOWWINDOW | SWP_FRAMECHANGED);

			_isTopmost = false;
			_hasSavedState = false;
#endif
		}

		/// <summary>
		/// Whether the window currently has saved state that can be restored.
		/// </summary>
		public static bool HasSavedState
		{
			get
			{
#if UNITY_STANDALONE_WIN
				return _hasSavedState;
#else
				return false;
#endif
			}
		}

		/// <summary>
		/// Whether the window is currently always-on-top.
		/// </summary>
		public static bool IsTopmost
		{
			get
			{
#if UNITY_STANDALONE_WIN
				return _isTopmost;
#else
				return false;
#endif
			}
		}
	}
}
