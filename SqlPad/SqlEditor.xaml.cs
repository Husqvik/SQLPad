using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;
using SqlPad.Bookmarks;
using SqlPad.Commands;

namespace SqlPad
{
	public partial class SqlEditor
	{
		private readonly SqlDocumentColorizingTransformer _colorizingTransformer = new SqlDocumentColorizingTransformer();
		private readonly SqlEditorBackgroundRenderer _backgroundRenderer;
		private readonly Popup _dynamicPopup;

		private IHelpProvider _createHelpProvider;
		private ICodeCompletionProvider _codeCompletionProvider;
		private INavigationService _navigationService;

		public SqlDocumentRepository DocumentRepository { get; private set; }

		public bool ColorizeBackground
		{
			get { return _colorizingTransformer.ColorizeBackground; }
			set { _colorizingTransformer.ColorizeBackground = value; }
		}

		public SqlEditor()
		{
			InitializeComponent();

			_backgroundRenderer = new SqlEditorBackgroundRenderer(Editor);

			Editor.TextArea.TextView.LineTransformers.Add(_colorizingTransformer);
			Editor.TextArea.TextView.BackgroundRenderers.Add(_backgroundRenderer);

			_dynamicPopup = new Popup { PlacementTarget = Editor.TextArea };
		}

		public void Initialize(IInfrastructureFactory infrastructureFactory, IDatabaseModel databaseModel)
		{
			_createHelpProvider = infrastructureFactory.CreateHelpProvider();
			_codeCompletionProvider = infrastructureFactory.CreateCodeCompletionProvider();
			_navigationService = infrastructureFactory.CreateNavigationService();
			_colorizingTransformer.SetParser(infrastructureFactory.CreateParser());
			DocumentRepository = new SqlDocumentRepository(infrastructureFactory.CreateParser(), infrastructureFactory.CreateStatementValidator(), databaseModel);
		}

		public async Task LoadAsync(string sqlText, CancellationToken cancellationToken)
		{
			Editor.Text = sqlText;
			await DocumentRepository.UpdateStatementsAsync(sqlText, cancellationToken);
			_colorizingTransformer.SetDocumentRepository(DocumentRepository);
			Editor.TextArea.TextView.Redraw();
		}

		private void ShowFunctionOverloads(object sender, ExecutedRoutedEventArgs args)
		{
			var functionOverloads = _codeCompletionProvider.ResolveProgramOverloads(DocumentRepository, Editor.CaretOffset);
			if (functionOverloads.Count == 0)
			{
				return;
			}

			_dynamicPopup.Child = new ProgramOverloadList { FunctionOverloads = functionOverloads, FontFamily = new FontFamily("Segoe UI") }.AsPopupChild();
			//_isToolTipOpenByShortCut = true;

			var rectangle = Editor.TextArea.Caret.CalculateCaretRectangle();
			_dynamicPopup.Placement = PlacementMode.Relative;
			_dynamicPopup.HorizontalOffset = rectangle.Left - Editor.TextArea.TextView.HorizontalOffset;
			_dynamicPopup.VerticalOffset = rectangle.Top - Editor.TextArea.TextView.VerticalOffset + Editor.TextArea.TextView.DefaultLineHeight;
			_dynamicPopup.IsOpen = true;
		}

		private void FindUsages(object sender, ExecutedRoutedEventArgs args)
		{
			var executionContext = ActionExecutionContext.Create(Editor, DocumentRepository);
			_navigationService.FindUsages(executionContext);
			AddHighlightSegments(executionContext.SegmentsToReplace);
		}

		private void AddHighlightSegments(ICollection<TextSegment> segments)
		{
			_backgroundRenderer.AddHighlightSegments(segments);
			Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
		}

		private void NavigateToPreviousHighlightedUsage(object sender, ExecutedRoutedEventArgs args)
		{
			var nextSegments = _backgroundRenderer.HighlightSegments
				.Where(s => s.IndextStart < Editor.CaretOffset)
				.OrderByDescending(s => s.IndextStart);

			NavigateToUsage(nextSegments);
		}

		private void NavigateToNextHighlightedUsage(object sender, ExecutedRoutedEventArgs args)
		{
			var nextSegments = _backgroundRenderer.HighlightSegments
				.Where(s => s.IndextStart > Editor.CaretOffset)
				.OrderBy(s => s.IndextStart);

			NavigateToUsage(nextSegments);
		}

		private void NavigateToUsage(IEnumerable<TextSegment> nextSegments)
		{
			if (!_backgroundRenderer.HighlightSegments.Any())
			{
				return;
			}

			var nextSegment = nextSegments.FirstOrDefault();
			if (!nextSegment.Equals(TextSegment.Empty))
			{
				Editor.CaretOffset = nextSegment.IndextStart;
				Editor.ScrollToCaret();
			}
		}

		private void NavigateToQueryBlockRoot(object sender, ExecutedRoutedEventArgs args)
		{
			var executionContext = ActionExecutionContext.Create(Editor, DocumentRepository);
			var queryBlockRootIndex = _navigationService.NavigateToQueryBlockRoot(executionContext);
			Editor.NavigateToOffset(queryBlockRootIndex);
		}

		private void NavigateToDefinition(object sender, ExecutedRoutedEventArgs args)
		{
			var executionContext = ActionExecutionContext.Create(Editor, DocumentRepository);
			var queryBlockRootIndex = _navigationService.NavigateToDefinition(executionContext);
			Editor.NavigateToOffset(queryBlockRootIndex);
		}

		private void EditorZoomInHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Editor.ZoomIn();
		}

		private void EditorZoomOutHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Editor.ZoomOut();
		}

		private void ShowHelpHandler(object sender, ExecutedRoutedEventArgs e)
		{
			var executionContext = ActionExecutionContext.Create(Editor, DocumentRepository);
			_createHelpProvider.ShowHelp(executionContext);
		}
	}

	internal class IsReadOnlyToVisibilityConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (bool)value ? Visibility.Collapsed : Visibility.Visible;
		}
	}

	public class DebuggerTabItem : TabItem
	{
		private readonly ExecutedCodeBackgroundRenderer _backgroundRenderer = new ExecutedCodeBackgroundRenderer();
		private readonly IconMargin _iconMargin;

		public DebugProgramItem ProgramItem { get; }

		public SqlEditor CodeViewer { get; }

		public OutputViewer OutputViewer { get; }

		public event EventHandler<BreakpointChangingEventArgs> BreakpointChanging;

		public event EventHandler<BreakpointChangedEventArgs> BreakpointChanged;

		public DebuggerTabItem(DebugProgramItem programItem, OutputViewer outputViewer)
		{
			ProgramItem = programItem;
			OutputViewer = outputViewer;

			Header = programItem.Header;
			Content = CodeViewer = new SqlEditor { ColorizeBackground = false };
			CodeViewer.Initialize(outputViewer.DocumentPage.InfrastructureFactory, outputViewer.ConnectionAdapter.DatabaseModel);
			CodeViewer.Editor.TextArea.TextView.BackgroundRenderers.Add(_backgroundRenderer);

			_iconMargin = new IconMargin(this) { DocumentRepository = CodeViewer.DocumentRepository };
			CodeViewer.Editor.TextArea.LeftMargins.Add(_iconMargin);
		}

		public void AddBreakpoint(BreakpointData breakpoint, object breakpointIdentifier)
		{
			_iconMargin.AddExistingBreakpoint(breakpointIdentifier, breakpoint.LineNumber, breakpoint.IsEnabled);
		}

		public void HighlightStackTraceLines(int? activeLineNumber, IEnumerable<int> inactiveLines)
		{
			_backgroundRenderer.HighlightStackTraceLines(activeLineNumber, inactiveLines);

			if (activeLineNumber.HasValue)
			{
				CodeViewer.Editor.ScrollToLine(activeLineNumber.Value);
			}

			CodeViewer.Editor.TextArea.TextView.Redraw();
		}

		internal void RaiseBreakpointChanging(BreakpointChangingEventArgs args)
		{
			BreakpointChanging?.Invoke(this, args);
		}

		internal void RaiseBreakpointChanged(BreakpointChangedEventArgs args)
		{
			BreakpointChanged?.Invoke(this, args);
		}
	}
}
