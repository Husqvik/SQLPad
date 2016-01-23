using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SqlPad
{
	public class ClipboardManager
	{
		private static readonly ClipboardManager Instance = new ClipboardManager();
		private static readonly IntPtr WndProcSuccess = IntPtr.Zero;

		public static event EventHandler ClipboardChanged;

		private ClipboardManager()
		{
		}

		public static void RegisterWindow(Window window)
		{
			var source = PresentationSource.FromVisual(window) as HwndSource;
			if (source == null)
			{
				throw new ArgumentException("Window must be initialized first. ", nameof(window));
			}

			source.AddHook(Instance.WndProc);

			var windowHandle = new WindowInteropHelper(window).Handle;

			NativeMethods.AddClipboardFormatListener(windowHandle);
		}

		private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == NativeMethods.ClipboardUpdate)
			{
				ClipboardChanged?.Invoke(this, EventArgs.Empty);
				handled = true;
			}

			return WndProcSuccess;
		}

		private static class NativeMethods
		{
			public const int ClipboardUpdate = 0x031D;

			[DllImport("user32.dll", SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool AddClipboardFormatListener(IntPtr hwnd);
		}
	}
}