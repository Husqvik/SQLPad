using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace SqlPad
{
	public class SqlDocumentColorizingTransformer : DocumentColorizingTransformer
	{
		private readonly object _lockObject = new object();

		private static readonly SolidColorBrush ErrorBrush = Brushes.Red;
		private static readonly SolidColorBrush KeywordBrush = Brushes.Blue;
		private static readonly SolidColorBrush LiteralBrush = Brushes.SaddleBrown;
		private static readonly SolidColorBrush AliasBrush = Brushes.MidnightBlue;
		private static readonly SolidColorBrush CommentBrush = Brushes.Green;
		private static readonly SolidColorBrush ProgramBrush = Brushes.Magenta;
		private static readonly SolidColorBrush CorrespondingTerminalBrush = Brushes.Yellow;
		private static readonly SolidColorBrush ValidStatementBackgroundBrush = new SolidColorBrush(Color.FromArgb(24, Colors.LightGreen.R, Colors.LightGreen.G, Colors.LightGreen.B));
		private static readonly SolidColorBrush ValidActiveStatementBackgroundBrush = new SolidColorBrush(Color.FromArgb(64, Colors.LightGreen.R, Colors.LightGreen.G, Colors.LightGreen.B));
		private static readonly SolidColorBrush InvalidStatementBackgroundBrush = new SolidColorBrush(Color.FromArgb(32, Colors.PaleVioletRed.R, Colors.PaleVioletRed.G, Colors.PaleVioletRed.B));
		private static readonly SolidColorBrush InvalidActiveStatementBackgroundBrush = new SolidColorBrush(Color.FromArgb(64, Colors.PaleVioletRed.R, Colors.PaleVioletRed.G, Colors.PaleVioletRed.B));
		private static readonly SolidColorBrush RedundantBrush = new SolidColorBrush(Color.FromArgb(168, Colors.Black.R, Colors.Black.G, Colors.Black.B));

		private readonly List<SourcePosition> _correspondingSegments = new List<SourcePosition>();
		private readonly HashSet<StatementGrammarNode> _redundantTerminals = new HashSet<StatementGrammarNode>();
		private readonly Dictionary<DocumentLine, ICollection<StatementGrammarNode>> _lineTerminals = new Dictionary<DocumentLine, ICollection<StatementGrammarNode>>();
		private readonly Dictionary<DocumentLine, ICollection<StatementGrammarNode>> _lineNodesWithSemanticErrorsOrInvalidGrammar = new Dictionary<DocumentLine, ICollection<StatementGrammarNode>>();
		private readonly Dictionary<DocumentLine, ICollection<StatementGrammarNode>> _lineNodesWithSuggestion = new Dictionary<DocumentLine, ICollection<StatementGrammarNode>>();
		private readonly Dictionary<DocumentLine, ICollection<StatementCommentNode>> _lineComments = new Dictionary<DocumentLine, ICollection<StatementCommentNode>>();
		private readonly HashSet<StatementGrammarNode> _recognizedProgramTerminals = new HashSet<StatementGrammarNode>();
		private readonly HashSet<StatementGrammarNode> _unrecognizedTerminals = new HashSet<StatementGrammarNode>();

		private ISqlParser _parser;
		private StatementCollection _statements;
		private IDictionary<StatementBase, IValidationModel> _validationModels;

		public StatementBase ActiveStatement { get; private set; }

		public IReadOnlyCollection<SourcePosition> CorrespondingSegments => _correspondingSegments.AsReadOnly();

		public bool ColorizeBackground { get; set; } = true;

		public void SetParser(ISqlParser parser)
		{
			_parser = parser;
		}

		private void EnsureParserSet()
		{
			if (_parser == null)
			{
				throw new InvalidOperationException("Parser hasn't been set. ");
			}
		}

		public bool SetActiveStatement(StatementBase activeStatement)
		{
			var isSameStatement = ActiveStatement == activeStatement;
			ActiveStatement = activeStatement;
			return !isSameStatement;
		}

		public void SetDocumentRepository(SqlDocumentRepository documentRepository)
		{
			if (documentRepository == null)
			{
				return;
			}

			EnsureParserSet();

			lock (_lockObject)
			{
				_statements = documentRepository.Statements;

				_validationModels = documentRepository.ValidationModels;

				ClearNodeIndexes();
			}
		}

		private void ClearNodeIndexes()
		{
			_lineTerminals.Clear();
			_redundantTerminals.Clear();
			_recognizedProgramTerminals.Clear();
			_unrecognizedTerminals.Clear();
			_lineNodesWithSemanticErrorsOrInvalidGrammar.Clear();
			_lineNodesWithSuggestion.Clear();
			_lineComments.Clear();
		}

		public void SetCorrespondingTerminals(IEnumerable<SourcePosition> correspondingSegments)
		{
			_correspondingSegments.Clear();
			_correspondingSegments.AddRange(correspondingSegments);
		}

		protected override void Colorize(ITextRunConstructionContext context)
		{
			lock (_lockObject)
			{
				if (_statements == null)
				{
					return;
				}

				BuildLineNodeIndexes(context);

				base.Colorize(context);
			}
		}

		private void BuildLineNodeIndexes(ITextRunConstructionContext context)
		{
			if (_lineTerminals.Count > 0)
				return;

			ClearNodeIndexes();
			
			BuildLineTerminalDictionary(context);

			BuildRedundantHashSet();

			BuildProgramTerminalHashset();

			BuildUnrecognizedTerminalHashset();

			BuildLineNodeWithSemanticErrorOrInvalidGrammarDictionary(context);

			BuildLineNodeWithSuggestionDictionary(context);

			BuildLineCommentDictionary(context);
		}

		private void BuildRedundantHashSet()
		{
			var redundantTerminals = _validationModels.Values.SelectMany(vm => vm.SemanticModel.RedundantSymbolGroups.SelectMany(g => g));
			_redundantTerminals.AddRange(redundantTerminals);
		}

		private void BuildLineCommentDictionary(ITextRunConstructionContext context)
		{
			var commentEnumerator = _statements.Comments.GetEnumerator();
			BuildLineNodeDictionary(commentEnumerator, context, _lineComments);
		}

		private void BuildLineNodeWithSemanticErrorOrInvalidGrammarDictionary(ITextRunConstructionContext context)
		{
			var semanticErrorOrInvalidGrammarNodeEnumerator = _validationModels.Values
				.SelectMany(vm => vm.SemanticErrors)
				.Select(nv => nv.Node)
				.Concat(_statements.SelectMany(s => s.InvalidGrammarNodes))
				.OrderBy(n => n.SourcePosition.IndexStart)
				.GetEnumerator();

			BuildLineNodeDictionary(semanticErrorOrInvalidGrammarNodeEnumerator, context, _lineNodesWithSemanticErrorsOrInvalidGrammar);
		}

		private void BuildLineNodeWithSuggestionDictionary(ITextRunConstructionContext context)
		{
			var suggestionNodeEnumerator = _validationModels.Values
				.SelectMany(vm => vm.Suggestions)
				.Select(nv => nv.Node)
				.OrderBy(n => n.SourcePosition.IndexStart)
				.GetEnumerator();

			BuildLineNodeDictionary(suggestionNodeEnumerator, context, _lineNodesWithSuggestion);
		}

		private void BuildUnrecognizedTerminalHashset()
		{
			var notRecognizedTerminals = _validationModels.Values
				.SelectMany(vm => vm.ObjectNodeValidity.Concat(vm.ProgramNodeValidity).Concat(vm.ColumnNodeValidity).Concat(vm.IdentifierNodeValidity))
				.Where(kvp => !kvp.Value.IsRecognized)
				.Select(kvp => kvp.Key);

			_unrecognizedTerminals.AddRange(notRecognizedTerminals);
		}

		private void BuildProgramTerminalHashset()
		{
			var recognizedProgramTerminalEnumerator = _validationModels.Values
				.SelectMany(vm => vm.ProgramNodeValidity)
				.Where(kvp => kvp.Value.IsRecognized && kvp.Key.Type == NodeType.Terminal)
				.Select(kvp => kvp.Key);

			_recognizedProgramTerminals.AddRange(recognizedProgramTerminalEnumerator);
		}

		private void BuildLineTerminalDictionary(ITextRunConstructionContext context)
		{
			var terminalEnumerator = _statements.SelectMany(s => s.AllTerminals).GetEnumerator();
			BuildLineNodeDictionary(terminalEnumerator, context, _lineTerminals);
		}

		private static void BuildLineNodeDictionary<TNode>(IEnumerator<TNode> nodeEnumerator, ITextRunConstructionContext context, IDictionary<DocumentLine, ICollection<TNode>> dictionary) where TNode : StatementNode
		{
			var nodesAvailable = nodeEnumerator.MoveNext();

			foreach (var line in context.Document.Lines)
			{
				if (!nodesAvailable)
					break;

				var singleLineTerminals = new List<TNode>();
				dictionary.Add(line, singleLineTerminals);

				do
				{
					if (line.EndOffset < nodeEnumerator.Current.SourcePosition.IndexStart)
						break;

					singleLineTerminals.Add(nodeEnumerator.Current);

					if (line.EndOffset < nodeEnumerator.Current.SourcePosition.IndexEnd)
						break;

					nodesAvailable = nodeEnumerator.MoveNext();
				}
				while (nodesAvailable);
			}
		}

		protected override void ColorizeLine(DocumentLine line)
		{
			if (_statements == null)
			{
				return;
			}

			ICollection<StatementGrammarNode> lineTerminals;
			if (_lineTerminals.TryGetValue(line, out lineTerminals))
			{
				foreach (var terminal in lineTerminals)
				{
					SolidColorBrush brush = null;
					if (_unrecognizedTerminals.Contains(terminal))
						brush = ErrorBrush;
					else if (_redundantTerminals.Contains(terminal))
						brush = RedundantBrush;
					else if (terminal.IsReservedWord)
						brush = KeywordBrush;
					else if (_parser.IsLiteral(terminal.Id))
						brush = LiteralBrush;
					else if (_parser.IsAlias(terminal.Id))
						brush = AliasBrush;
					else if (_recognizedProgramTerminals.Contains(terminal))
						brush = ProgramBrush;

					if (brush == null)
					{
						continue;
					}

					ProcessSegmentAtLine(line, terminal.SourcePosition,
						element => element.TextRunProperties.SetForegroundBrush(brush));
				}
			}

			ProcessNodeCollectionAtLine(line, _lineNodesWithSemanticErrorsOrInvalidGrammar,
				element => element.TextRunProperties.SetTextDecorations(Resources.WaveErrorUnderline));

			ProcessNodeCollectionAtLine(line, _lineNodesWithSuggestion,
				element => element.TextRunProperties.SetTextDecorations(Resources.WaveWarningUnderline));

			ProcessNodeCollectionAtLine(line, _lineComments,
				element =>
				{
					element.TextRunProperties.SetForegroundBrush(CommentBrush);
					element.BackgroundBrush = null;
				});

			var statementsAtLine = _statements.Where(s => s.SourcePosition.IndexStart <= line.EndOffset && s.SourcePosition.IndexEnd >= line.Offset);
			var colorizeBackground = ColorizeBackground;
			foreach (var statement in statementsAtLine)
			{
				Brush backgroundBrush;
				if (statement == ActiveStatement)
				{
					backgroundBrush = statement.ParseStatus == ParseStatus.Success ? ValidActiveStatementBackgroundBrush : InvalidActiveStatementBackgroundBrush;
				}
				else
				{
					backgroundBrush = statement.ParseStatus == ParseStatus.Success ? ValidStatementBackgroundBrush : InvalidStatementBackgroundBrush;
				}

				var colorStartOffset = Math.Max(line.Offset, statement.SourcePosition.IndexStart);
				var colorEndOffset = Math.Min(line.EndOffset, statement.SourcePosition.IndexEnd + 1);

				SetCorrespondingTerminalsBrush(line);

				if (colorizeBackground)
				{
					ChangeLinePart(
						colorStartOffset,
						colorEndOffset,
						element =>
						{
							element.BackgroundBrush = backgroundBrush;

							//ProcessSegmentAtLine(line, semanticError.Node.SourcePosition,
							//	element => element.TextRunProperties.SetTextDecorations(Resources.BoxedText));

							/*ProcessSegmentAtLine(line, nodeSemanticError.Key.SourcePosition,
							element =>
							{
								element.BackgroundBrush = Resources.OutlineBoxBrush;
								var x = 1;
							});*/

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
				}
			}

			SetCorrespondingTerminalsBrush(line);
		}

		private void SetCorrespondingTerminalsBrush(ISegment line)
		{
			foreach (var segment in _correspondingSegments)
			{
				ProcessSegmentAtLine(line, segment, element => element.BackgroundBrush = CorrespondingTerminalBrush);
			}
		}

		private void ProcessNodeCollectionAtLine<TNode>(DocumentLine line, IReadOnlyDictionary<DocumentLine, ICollection<TNode>> lineNodeDictionary, Action<VisualLineElement> visualElementAction) where TNode : StatementNode
		{
			ICollection<TNode> nodes;
			if (!lineNodeDictionary.TryGetValue(line, out nodes))
			{
				return;
			}
			
			foreach (var node in nodes)
			{
				ProcessSegmentAtLine(line, node.SourcePosition, visualElementAction);
			}
		}

		private void ProcessSegmentAtLine(ISegment line, SourcePosition segment, Action<VisualLineElement> visualElementAction)
		{
			if (line.Offset > segment.IndexEnd + 1 ||
			    line.EndOffset < segment.IndexStart)
			{
				return;
			}

			var errorColorStartOffset = Math.Max(line.Offset, segment.IndexStart);
			var errorColorEndOffset = Math.Min(line.EndOffset, segment.IndexEnd + 1);

			ChangeLinePart(errorColorStartOffset, errorColorEndOffset, visualElementAction);
		}
	}
}
