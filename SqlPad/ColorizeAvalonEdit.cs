using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace SqlPad
{
	public class ColorizeAvalonEdit : DocumentColorizingTransformer
	{
		private readonly object _lockObject = new object();

		private StatementCollection _statements;
		private readonly Stack<ICollection<TextSegment>> _highlightSegments = new Stack<ICollection<TextSegment>>();
		private readonly List<StatementDescriptionNode> _highlightParenthesis = new List<StatementDescriptionNode>();
		private readonly IStatementValidator _validator = ConfigurationProvider.InfrastructureFactory.CreateStatementValidator();
		private readonly ISqlParser _parser = ConfigurationProvider.InfrastructureFactory.CreateSqlParser();
		private readonly IDatabaseModel _databaseModel = ConfigurationProvider.InfrastructureFactory.CreateDatabaseModel(ConfigurationProvider.ConnectionStrings["Default"]);
		private static readonly SolidColorBrush ErrorBrush = new SolidColorBrush(Colors.Red);
		private static readonly SolidColorBrush HighlightUsageBrush = new SolidColorBrush(Colors.Turquoise);
		private static readonly SolidColorBrush HighlightDefinitionBrush = new SolidColorBrush(Colors.SandyBrown);
		private static readonly SolidColorBrush KeywordBrush = new SolidColorBrush(Colors.Blue);
		private static readonly SolidColorBrush LiteralBrush = new SolidColorBrush(Colors.SaddleBrown/*Color.FromRgb(214, 157, 133)*/);
		private static readonly SolidColorBrush AliasBrush = new SolidColorBrush(Colors.Green);
		private static readonly SolidColorBrush FunctionBrush = new SolidColorBrush(Colors.Magenta);
		private static readonly Color ValidStatementBackground = Color.FromArgb(32, Colors.LightGreen.R, Colors.LightGreen.G, Colors.LightGreen.B);
		private static readonly Color InvalidStatementBackground = Color.FromArgb(32, Colors.PaleVioletRed.R, Colors.PaleVioletRed.G, Colors.PaleVioletRed.B);

		private IDictionary<StatementBase, IValidationModel> _validationModels;
		public IList<StatementDescriptionNode> HighlightParenthesis { get { return _highlightParenthesis.AsReadOnly(); } }

		private readonly Dictionary<DocumentLine, ICollection<StatementDescriptionNode>> _lineTerminals = new Dictionary<DocumentLine, ICollection<StatementDescriptionNode>>();

		public void SetStatementCollection(StatementCollection statements)
		{
			if (statements == null)
				return;

			var validationModels = statements.Select(s => _validator.BuildValidationModel(null, s, _databaseModel))
					.ToDictionary(vm => vm.Statement, vm => vm);

			lock (_lockObject)
			{
				_statements = statements;

				_validationModels = validationModels;

				_lineTerminals.Clear();
			}
		}

		public void SetHighlightParenthesis(ICollection<StatementDescriptionNode> parenthesisNodes)
		{
			_highlightParenthesis.Clear();
			_highlightParenthesis.AddRange(parenthesisNodes);
		}

		public void SetHighlightSegments(ICollection<TextSegment> highlightSegments)
		{
			lock (_lockObject)
			{
				if (highlightSegments != null)
				{
					if (highlightSegments.Count == 0 ||
					    _highlightSegments.SelectMany(c => c).Contains(highlightSegments.First()))
						return;

					_highlightSegments.Push(highlightSegments);
				}
				else if (_highlightSegments.Count > 0)
				{
					_highlightSegments.Pop();
				}
			}
		}

		protected override void Colorize(ITextRunConstructionContext context)
		{
			lock (_lockObject)
			{
				if (_statements == null)
					return;

				if (_lineTerminals.Count == 0)
				{
					var terminalEnumerator = _statements.SelectMany(s => s.AllTerminals).GetEnumerator();
					if (terminalEnumerator.MoveNext())
					{
						foreach (var line in context.Document.Lines)
						{
							var singleLineTerminals = new List<StatementDescriptionNode>();
							_lineTerminals.Add(line, singleLineTerminals);

							do
							{
								if (line.EndOffset < terminalEnumerator.Current.SourcePosition.IndexStart)
									break;

								singleLineTerminals.Add(terminalEnumerator.Current);
							}
							while (terminalEnumerator.MoveNext());
						}
					}
				}

				base.Colorize(context);
			}
		}

		protected override void ColorizeLine(DocumentLine line)
		{
			if (_statements == null)
				return;

			if (_lineTerminals.ContainsKey(line))
			{
				foreach (var terminal in _lineTerminals[line])
				{
					SolidColorBrush brush = null;
					if (_parser.IsKeyword(terminal.Token.Value))
						brush = KeywordBrush;
					else if (_parser.IsLiteral(terminal.Id))
						brush = LiteralBrush;
					else if (_parser.IsAlias(terminal.Id))
						brush = AliasBrush;

					if (brush == null)
						continue;

					ProcessNodeAtLine(line, terminal.SourcePosition,
						element => element.TextRunProperties.SetForegroundBrush(brush));
				}
			}

			foreach (var parenthesisNode in _highlightParenthesis)
			{
				ProcessNodeAtLine(line, parenthesisNode.SourcePosition, element => element.TextRunProperties.SetTextDecorations(TextDecorations.Underline));
			}

			var statementsAtLine = _statements.Where(s => s.SourcePosition.IndexStart <= line.EndOffset && s.SourcePosition.IndexEnd >= line.Offset);

			foreach (var statement in statementsAtLine)
			{
				var backgroundColor = new SolidColorBrush(statement.ProcessingStatus == ProcessingStatus.Success ? ValidStatementBackground : InvalidStatementBackground);

				var colorStartOffset = Math.Max(line.Offset, statement.SourcePosition.IndexStart);
				var colorEndOffset = Math.Min(line.EndOffset, statement.SourcePosition.IndexEnd + 1);

				var validationModel = _validationModels[statement];
				var nodeRecognizeData = validationModel.ObjectNodeValidity
					.Select(kvp => new KeyValuePair<StatementDescriptionNode, bool>(kvp.Key, kvp.Value.IsRecognized))
					.Concat(validationModel.FunctionNodeValidity.Select(kvp => new KeyValuePair<StatementDescriptionNode, bool>(kvp.Key, kvp.Value.IsRecognized)))
					.Concat(validationModel.ColumnNodeValidity.Select(kvp => new KeyValuePair<StatementDescriptionNode, bool>(kvp.Key, kvp.Value.IsRecognized)));

				foreach (var nodeValidity in nodeRecognizeData.Where(nv => !nv.Value))
				{
					ProcessNodeAtLine(line, nodeValidity.Key.SourcePosition,
						element => element.TextRunProperties.SetForegroundBrush(ErrorBrush));
				}

				foreach (var terminal in validationModel.FunctionNodeValidity.Where(kvp => kvp.Value.IsRecognized && kvp.Key.Type == NodeType.Terminal).Select(kvp => kvp.Key))
				{
					ProcessNodeAtLine(line, terminal.SourcePosition,
						element => element.TextRunProperties.SetForegroundBrush(FunctionBrush));
				}

				foreach (var invalidGrammarNode in statement.InvalidGrammarNodes)
				{
					ProcessNodeAtLine(line, invalidGrammarNode.SourcePosition,
						element => element.TextRunProperties.SetTextDecorations(Resources.WaveErrorUnderline));
				}

				foreach (var nodeSemanticError in validationModel.GetNodesWithSemanticErrors())
				{
					ProcessNodeAtLine(line, nodeSemanticError.Key.SourcePosition,
						element => element.TextRunProperties.SetTextDecorations(Resources.WaveErrorUnderline));
					//ProcessNodeAtLine(line, semanticError.Node.SourcePosition,
					//	element => element.TextRunProperties.SetTextDecorations(Resources.BoxedText));

					/*ProcessNodeAtLine(line, semanticError.Node.SourcePosition,
						element =>
						{
							element.BackgroundBrush = Resources.OutlineBoxBrush;
							var x = 1;
						});*/
				}

				ChangeLinePart(
					colorStartOffset,
					colorEndOffset,
					element =>
					{
						element.BackgroundBrush = backgroundColor;

						/*
						// This lambda gets called once for every VisualLineElement
						// between the specified offsets.
						var tf = element.TextRunProperties.Typeface;
						// Replace the typeface with a modified version of
						// the same typeface
						element.TextRunProperties.SetTypeface(new Typeface(
							tf.FontFamily,
							FontStyles.Italic,
							FontWeights.Bold,
							tf.Stretch
						));*/
					});

				foreach (var highlightSegment in _highlightSegments.SelectMany(s => s))
				{
					ProcessNodeAtLine(line,
						new SourcePosition { IndexStart = highlightSegment.IndextStart, IndexEnd = highlightSegment.IndextStart + highlightSegment.Length - 1 },
						element => element.BackgroundBrush = highlightSegment.DisplayOptions == DisplayOptions.Usage ? HighlightUsageBrush : HighlightDefinitionBrush);
				}
			}
		}

		private void ProcessNodeAtLine(ISegment line, SourcePosition nodePosition, Action<VisualLineElement> action)
		{
			if (line.Offset > nodePosition.IndexEnd + 1 ||
			    line.EndOffset < nodePosition.IndexStart)
				return;

			var errorColorStartOffset = Math.Max(line.Offset, nodePosition.IndexStart);
			var errorColorEndOffset = Math.Min(line.EndOffset, nodePosition.IndexEnd + 1);

			ChangeLinePart(errorColorStartOffset, errorColorEndOffset, action);
		}
	}
}