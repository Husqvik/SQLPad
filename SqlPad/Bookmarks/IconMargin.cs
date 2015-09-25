using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Utils;

namespace SqlPad.Bookmarks
{
	public class IconMargin : AbstractMargin
	{
		private readonly DebuggerTabItem _debuggerView;
		private readonly RoutedCommand _enableBreakPointCommand = new RoutedCommand();
		private readonly RoutedCommand _disableBreakPointCommand = new RoutedCommand();
		private readonly ContextMenu _contextMenu = new ContextMenu();
		private readonly MenuItem _enableBreakpointMenuItem;
		private readonly MenuItem _disableBreakpointMenuItem;

		private readonly HashSet<BreakpointMarker> _markers = new HashSet<BreakpointMarker>();
		private readonly List<BreakpointMarker> _visibleMarkers = new List<BreakpointMarker>();

		private ICollection<BreakpointData> DocumentBreakpoints => _debuggerView.OutputViewer.DocumentPage.WorkDocument.Breakpoints;

		public SqlDocumentRepository DocumentRepository { get; set; }

		public IEnumerable<Breakpoint> Breakpoints => _markers.Select(m => m.Breakpoint);

		protected override int VisualChildrenCount => _visibleMarkers.Count;

		public IconMargin(DebuggerTabItem debuggerView)
		{
			_debuggerView = debuggerView;

			_contextMenu.CommandBindings.Add(new CommandBinding(_enableBreakPointCommand, EnableBreakpointExecutedHandler));
			_contextMenu.CommandBindings.Add(new CommandBinding(_disableBreakPointCommand, DisableBreakpointExecutedHandler));
			_contextMenu.Items.Add(_enableBreakpointMenuItem = new MenuItem { Header = "_Enable", Command = _enableBreakPointCommand });
			_contextMenu.Items.Add(_disableBreakpointMenuItem = new MenuItem { Header = "_Disable", Command = _disableBreakPointCommand });
		}

		private async void EnableBreakpointExecutedHandler(object sender, ExecutedRoutedEventArgs args)
		{
			await EnableOrDisableBreakpoint((BreakpointMarker)args.Parameter, BreakpointState.Enabled);
		}

		private async void DisableBreakpointExecutedHandler(object sender, ExecutedRoutedEventArgs args)
		{
			await EnableOrDisableBreakpoint((BreakpointMarker)args.Parameter, BreakpointState.Disabled);
		}

		private async Task EnableOrDisableBreakpoint(BreakpointMarker breakpointMarker, BreakpointState state)
		{
			var breakpoint = breakpointMarker.Breakpoint;
			var actionResult = await SafeRaiseBreakpointChanged(breakpoint, state);
			if (!actionResult.IsSuccessful)
			{
				return;
			}

			breakpoint.IsEnabled = state == BreakpointState.Enabled;
			var breakpointData = GetBreakpointData(breakpoint);
			DocumentBreakpoints.Single(bp => Equals(bp, breakpointData)).IsEnabled = breakpoint.IsEnabled;

			breakpointMarker.InvalidateVisual();
		}

		public async void RemoveBreakpoint(BreakpointMarker marker)
		{
			var actionResult = await SafeRaiseBreakpointChanged(marker.Breakpoint, BreakpointState.Removed);
			if (!actionResult.IsSuccessful)
			{
				return;
			}

			_markers.Remove(marker);
			_visibleMarkers.Remove(marker);

			RemoveVisualChild(marker);
			InvalidateMeasure();

			DocumentBreakpoints.Remove(GetBreakpointData(marker.Breakpoint));
		}

		public void AddExistingBreakpoint(object breakpointIdentifier, int line, bool enable)
		{
			var sqlTextEditor = _debuggerView.CodeViewer.Editor;
			var lineStartOffset = sqlTextEditor.Document.GetLineByNumber(line).Offset;
			var anchor = sqlTextEditor.Document.CreateAnchor(lineStartOffset);
			var breakpoint = new Breakpoint(anchor) { Identifier = breakpointIdentifier, IsEnabled = enable };
			AddBreakpointMarker(breakpoint);
		}

		private void AddBreakpointMarker(Breakpoint breakpoint)
		{
			_markers.Add(new BreakpointMarker(breakpoint));
			InvalidateMeasure();
		}

		public async Task AddBreakpoint(int offset, bool enable = true)
		{
			var sqlTextEditor = _debuggerView.CodeViewer.Editor;
			var lineStartOffset = sqlTextEditor.Document.GetLineByOffset(offset).Offset;
			var anchor = sqlTextEditor.Document.CreateAnchor(lineStartOffset);
			var breakpoint = new Breakpoint(anchor) { IsEnabled = enable };

			var actionResult = await SafeRaiseBreakpointChanged(breakpoint, BreakpointState.Added);
			if (!actionResult.IsSuccessful)
			{
				return;
			}

			AddBreakpointMarker(breakpoint);

			DocumentBreakpoints.Add(GetBreakpointData(breakpoint));
		}

		private BreakpointData GetBreakpointData(Breakpoint breakpoint)
		{
			return new BreakpointData(_debuggerView.ProgramItem.ProgramIdentifier, breakpoint.Anchor.Line, breakpoint.IsEnabled);
		}

		private async Task<BreakpointActionResult> SafeRaiseBreakpointChanged(Breakpoint breakpoint, BreakpointState breakpointState)
		{
			var result = new BreakpointActionResult();

			try
			{
				var changingEventArgs = new BreakpointChangingEventArgs(breakpoint, breakpointState);
				_debuggerView.RaiseBreakpointChanging(changingEventArgs);
				result = await changingEventArgs.SetBreakpointTask;

				if (result.IsSuccessful)
				{
					if (breakpointState == BreakpointState.Added)
					{
						breakpoint.Identifier = result.BreakpointIdentifier;
					}

					_debuggerView.RaiseBreakpointChanged(new BreakpointChangedEventArgs(breakpoint, breakpointState, GetBreakpointData(breakpoint)));
				}
			}
			catch (Exception exception)
			{
				Messages.ShowError(exception.Message);
			}

			return result;
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			return new Size(14, 0);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			var pixelSize = PixelSnapHelpers.GetPixelSize(this);
			var textView = TextView;

			_visibleMarkers.Clear();
			
			foreach (var marker in _markers)
			{
				RemoveVisualChild(marker);

				var visualLine = textView.GetVisualLine(marker.Breakpoint.Anchor.Line);
				if (visualLine == null)
				{
					continue;
				}

				_visibleMarkers.Add(marker);
				AddVisualChild(marker);

				var topLeft = new Point(0, visualLine.VisualTop - textView.VerticalOffset);
				marker.Arrange(new Rect(PixelSnapHelpers.Round(topLeft, pixelSize), marker.DesiredSize));
			}
			
			return base.ArrangeOverride(finalSize);
		}

		protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
		{
			if (oldTextView != null)
			{
				oldTextView.VisualLinesChanged -= TextViewVisualLinesChangedHandler;
			}
			
			base.OnTextViewChanged(oldTextView, newTextView);
			
			if (newTextView != null)
			{
				newTextView.VisualLinesChanged += TextViewVisualLinesChangedHandler;
			}
			
			InvalidateVisual();
		}

		private void TextViewVisualLinesChangedHandler(object sender, EventArgs eventArgs)
		{
			InvalidateVisual();
		}

		protected async override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			e.Handled = true;

			var visualPosition = e.GetPosition(_debuggerView.CodeViewer.Editor);
			var sqlTextEditor = _debuggerView.CodeViewer.Editor;
			var position = sqlTextEditor.GetPositionFromPoint(visualPosition);
			if (position == null)
			{
				return;
			}

			var offset = sqlTextEditor.Document.GetOffset(position.Value.Line, position.Value.Column);
			await AddBreakpoint(offset);
		}

		protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
		{
			return new PointHitTestResult(this, hitTestParameters.HitPoint);
		}

		protected override Visual GetVisualChild(int index)
		{
			return _visibleMarkers[index];
		}

		protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
		{
			ContextMenu = null;
		}

		public void ConfigureContextActions(BreakpointMarker breakpointMarker)
		{
			ContextMenu = _contextMenu;
			_enableBreakpointMenuItem.Visibility = breakpointMarker.Breakpoint.IsEnabled ? Visibility.Collapsed : Visibility.Visible;
			_disableBreakpointMenuItem.Visibility = breakpointMarker.Breakpoint.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
			_enableBreakpointMenuItem.CommandParameter = _disableBreakpointMenuItem.CommandParameter = breakpointMarker;
		}
	}

	public sealed class BreakpointMarker : UIElement
	{
		private const double BreakpointRadius = 7;
		
		private static readonly Size BreakpointSize = new Size(2 * BreakpointRadius, 2 * BreakpointRadius);
		private static readonly Pen EdgePenEnabled = new Pen(Brushes.White, 1.0) { StartLineCap = PenLineCap.Square, EndLineCap = PenLineCap.Square };
		private static readonly Pen EdgePenDisabled = new Pen(Brushes.Red, 1.0) { StartLineCap = PenLineCap.Square, EndLineCap = PenLineCap.Square };

		public Breakpoint Breakpoint { get; }

		static BreakpointMarker()
		{
			EdgePenEnabled.Freeze();
			EdgePenDisabled.Freeze();
		}

		public BreakpointMarker(Breakpoint breakpoint)
		{
			Breakpoint = breakpoint;
		}

		protected override Size MeasureCore(Size availableSize)
		{
			return BreakpointSize;
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			Brush brush;
			Pen pen;
			if (Breakpoint.IsEnabled)
			{
				pen = EdgePenEnabled;
				brush = Brushes.Red;
			}
			else
			{
				pen = EdgePenDisabled;
				brush = Brushes.White;
			}

			drawingContext.DrawEllipse(brush, pen, new Point(BreakpointSize.Width / 2, BreakpointSize.Height / 2), BreakpointRadius, BreakpointRadius);
		}

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			e.Handled = true;

			var bookmarkMargin = (IconMargin)VisualParent;
			bookmarkMargin.RemoveBreakpoint(this);
		}

		protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
		{
			e.Handled = true;

			var bookmarkMargin = (IconMargin)VisualParent;
			bookmarkMargin.ConfigureContextActions(this);
		}
	}

	public class Breakpoint
	{
		public TextAnchor Anchor { get; }

		public bool IsEnabled { get; set; }

		public object Identifier { get; set; }

		public Breakpoint(TextAnchor anchor)
		{
			Anchor = anchor;
			IsEnabled = true;
		}
	}

	public class BreakpointChangingEventArgs : BreakpointChangedEventArgsBase
	{
		public Task<BreakpointActionResult> SetBreakpointTask { get; set; }

		public BreakpointChangingEventArgs(Breakpoint breakpoint, BreakpointState state)
			: base(breakpoint, state)
		{
		}
	}

	public class BreakpointChangedEventArgs : BreakpointChangedEventArgsBase
	{
		public BreakpointData BreakpointData { get; }

		public BreakpointChangedEventArgs(Breakpoint breakpoint, BreakpointState state, BreakpointData breakpointData)
			: base(breakpoint, state)
		{
			BreakpointData = breakpointData;
		}
	}

	public abstract class BreakpointChangedEventArgsBase : EventArgs
	{
		public Breakpoint Breakpoint { get; }

		public BreakpointState State { get; }

		protected BreakpointChangedEventArgsBase(Breakpoint breakpoint, BreakpointState state)
		{
			Breakpoint = breakpoint;
			State = state;
		}
	}

	public enum BreakpointState
	{
		Added,
		Enabled,
		Disabled,
		Removed
	}

	public class ExecutedCodeBackgroundRenderer : IBackgroundRenderer
	{
		private static readonly Brush BackgroundBrushActive = Brushes.Yellow;
		private static readonly Brush BackgroundBrushInactive = Brushes.LightGreen;
		private static readonly Pen EdgePen = new Pen(BackgroundBrushActive, 1);

		private int? _activeLine;
		private int[] _inactiveLines = new int[0];

		public KnownLayer Layer { get; } = KnownLayer.Background;

		static ExecutedCodeBackgroundRenderer()
		{
			EdgePen.Freeze();
		}

		public void Draw(TextView textView, DrawingContext drawingContext)
		{
			DrawLine(textView, drawingContext, _activeLine, BackgroundBrushActive);

			foreach (var lineNumber in _inactiveLines)
			{
				DrawLine(textView, drawingContext, lineNumber, BackgroundBrushInactive);
			}
		}

		public void HighlightStackTraceLines(int? lineNumber, IEnumerable<int> lineNumbers)
		{
			_activeLine = lineNumber;
			_inactiveLines = lineNumbers.ToArray();
		}

		private static void DrawLine(TextView textView, DrawingContext drawingContext, int? line, Brush brush)
		{
			if (line == null)
			{
				return;
			}

			textView.EnsureVisualLines();
			var startOffset = textView.Document.GetLineByNumber(line.Value).Offset;
			var textSegment = new ICSharpCode.AvalonEdit.Document.TextSegment {StartOffset = startOffset};
			foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, textSegment))
			{
				drawingContext.DrawRectangle(brush, EdgePen, new Rect(rect.Location, new Size(textView.ActualWidth, rect.Height)));
			}
		}
	}
}
