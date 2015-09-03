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
using SqlPad.Bookmarks;
using SqlPad.Commands;

namespace SqlPad
{
	public partial class SqlEditor
	{
		private readonly SqlDocumentColorizingTransformer _colorizingTransformer = new SqlDocumentColorizingTransformer();
		private readonly Popup _dynamicPopup;
		private readonly IconMargin _iconMargin;

		private SqlDocumentRepository _sqlDocumentRepository;
		private IHelpProvider _createHelpProvider;
		private ICodeCompletionProvider _codeCompletionProvider;
		private INavigationService _navigationService;

		public SqlEditor()
		{
			InitializeComponent();

			_iconMargin = new IconMargin(Editor);
			Editor.TextArea.LeftMargins.Add(_iconMargin);

			Editor.TextArea.TextView.LineTransformers.Add(_colorizingTransformer);

			_dynamicPopup = new Popup { PlacementTarget = Editor.TextArea };
		}

		public void Initialize(IInfrastructureFactory infrastructureFactory, IDatabaseModel databaseModel)
		{
			_createHelpProvider = infrastructureFactory.CreateHelpProvider();
			_codeCompletionProvider = infrastructureFactory.CreateCodeCompletionProvider();
			_navigationService = infrastructureFactory.CreateNavigationService();
			_colorizingTransformer.SetParser(infrastructureFactory.CreateParser());
			_iconMargin.DocumentRepository = _sqlDocumentRepository = new SqlDocumentRepository(infrastructureFactory.CreateParser(), infrastructureFactory.CreateStatementValidator(), databaseModel);
		}

		public async Task LoadAsync(string sqlText, CancellationToken cancellationToken)
		{
			Editor.Text = sqlText;
			await _sqlDocumentRepository.UpdateStatementsAsync(sqlText, cancellationToken);
			_colorizingTransformer.SetDocumentRepository(_sqlDocumentRepository);
			Editor.TextArea.TextView.Redraw();
		}

		private void ShowFunctionOverloads(object sender, ExecutedRoutedEventArgs args)
		{
			var functionOverloads = _codeCompletionProvider.ResolveFunctionOverloads(_sqlDocumentRepository, Editor.CaretOffset);
			if (functionOverloads.Count == 0)
			{
				return;
			}

			_dynamicPopup.Child = new FunctionOverloadList { FunctionOverloads = functionOverloads, FontFamily = new FontFamily("Segoe UI") }.AsPopupChild();
			//_isToolTipOpenByShortCut = true;

			var rectangle = Editor.TextArea.Caret.CalculateCaretRectangle();
			_dynamicPopup.Placement = PlacementMode.Relative;
			_dynamicPopup.HorizontalOffset = rectangle.Left - Editor.TextArea.TextView.HorizontalOffset;
			_dynamicPopup.VerticalOffset = rectangle.Top - Editor.TextArea.TextView.VerticalOffset + Editor.TextArea.TextView.DefaultLineHeight;
			_dynamicPopup.IsOpen = true;
		}

		private void FindUsages(object sender, ExecutedRoutedEventArgs args)
		{
			var executionContext = ActionExecutionContext.Create(Editor, _sqlDocumentRepository);
			_navigationService.FindUsages(executionContext);
			AddHighlightSegments(executionContext.SegmentsToReplace);
		}

		private void AddHighlightSegments(ICollection<TextSegment> segments)
		{
			_colorizingTransformer.AddHighlightSegments(segments);
			Editor.TextArea.TextView.Redraw();
		}

		private void NavigateToPreviousHighlightedUsage(object sender, ExecutedRoutedEventArgs args)
		{
			var nextSegments = _colorizingTransformer.HighlightSegments
						.Where(s => s.IndextStart < Editor.CaretOffset)
						.OrderByDescending(s => s.IndextStart);

			NavigateToUsage(nextSegments);
		}

		private void NavigateToNextHighlightedUsage(object sender, ExecutedRoutedEventArgs args)
		{
			var nextSegments = _colorizingTransformer.HighlightSegments
						.Where(s => s.IndextStart > Editor.CaretOffset)
						.OrderBy(s => s.IndextStart);

			NavigateToUsage(nextSegments);
		}

		private void NavigateToUsage(IEnumerable<TextSegment> nextSegments)
		{
			if (!_colorizingTransformer.HighlightSegments.Any())
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
			var executionContext = ActionExecutionContext.Create(Editor, _sqlDocumentRepository);
			var queryBlockRootIndex = _navigationService.NavigateToQueryBlockRoot(executionContext);
			Editor.NavigateToOffset(queryBlockRootIndex);
		}

		private void NavigateToDefinition(object sender, ExecutedRoutedEventArgs args)
		{
			var executionContext = ActionExecutionContext.Create(Editor, _sqlDocumentRepository);
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
			var executionContext = ActionExecutionContext.Create(Editor, _sqlDocumentRepository);
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

		public SqlEditor CodeViewer { get; }

		public int? ActiveLine
		{
			get { return _backgroundRenderer.ActiveLine; }
			set
			{
				_backgroundRenderer.ActiveLine = value;
				CodeViewer.Editor.TextArea.TextView.Redraw();
			}
		}

		public DebuggerTabItem(string header)
		{
			Header = header;
			Content = CodeViewer = new SqlEditor();
			CodeViewer.Editor.TextArea.TextView.BackgroundRenderers.Add(_backgroundRenderer);
		}
	}
}
