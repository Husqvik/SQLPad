using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace SqlPad
{
	public class ColorizeAvalonEdit : DocumentColorizingTransformer
	{
		private StatementCollection _parsedStatements;
		private readonly Stack<ICollection<TextSegment>> _highlightSegments = new Stack<ICollection<TextSegment>>();
		private readonly IStatementValidator _validator = ConfigurationProvider.InfrastructureFactory.CreateStatementValidator();
		private readonly ISqlParser _parser = ConfigurationProvider.InfrastructureFactory.CreateSqlParser();
		private readonly IDatabaseModel _databaseModel = ConfigurationProvider.InfrastructureFactory.CreateDatabaseModel(ConfigurationProvider.ConnectionStrings["Default"]);
		private static readonly SolidColorBrush ErrorBrush = new SolidColorBrush(Colors.Red);
		private static readonly SolidColorBrush NormalTextBrush = new SolidColorBrush(Colors.Black);
		private static readonly SolidColorBrush HighlightBrush = new SolidColorBrush(Colors.Turquoise);
		private static readonly SolidColorBrush KeywordBrush = new SolidColorBrush(Colors.Blue);

		public void SetStatementCollection(StatementCollection statements)
		{
			_parsedStatements = statements;
		}

		public void SetHighlightSegments(ICollection<TextSegment> highlightSegments)
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

		protected override void ColorizeLine(DocumentLine line)
		{
			if (_parsedStatements == null)
				return;

			var statementsAtLine = _parsedStatements.Where(s => s.SourcePosition.IndexStart <= line.EndOffset && s.SourcePosition.IndexEnd >= line.Offset);

			foreach (var statement in statementsAtLine)
			{
				var backgroundColor = new SolidColorBrush(statement.ProcessingStatus == ProcessingStatus.Success ? Colors.LightGreen : Colors.PaleVioletRed);

				var colorStartOffset = Math.Max(line.Offset, statement.SourcePosition.IndexStart);
				var colorEndOffset = Math.Min(line.EndOffset, statement.SourcePosition.IndexEnd + 1);

				var validationModel = _validator.ResolveReferences(null, statement, _databaseModel);
				var nodeRecognizeData = validationModel.ObjectNodeValidity
					.Select(kvp => new KeyValuePair<StatementDescriptionNode, bool>(kvp.Key, kvp.Value.IsRecognized))
					.Concat(validationModel.FunctionNodeValidity.Select(kvp => new KeyValuePair<StatementDescriptionNode, bool>(kvp.Key, kvp.Value.IsRecognized)))
					.Concat(validationModel.ColumnNodeValidity.Select(kvp => new KeyValuePair<StatementDescriptionNode, bool>(kvp.Key, kvp.Value.IsRecognized)));

				foreach (var nodeValidity in nodeRecognizeData)
				{
					ProcessNodeAtLine(line, nodeValidity.Key.SourcePosition,
						element =>
						{
							element.TextRunProperties.SetForegroundBrush(nodeValidity.Value ? NormalTextBrush : ErrorBrush);
						});
				}

				foreach (var keyword in statement.AllTerminals.Where(t => _parser.IsKeyword(t.Token.Value)))
				{
					ProcessNodeAtLine(line, keyword.SourcePosition,
						element => element.TextRunProperties.SetForegroundBrush(KeywordBrush));
				}

				foreach (var invalidGrammarNode in statement.InvalidGrammarNodes)
				{
					ProcessNodeAtLine(line, invalidGrammarNode.SourcePosition,
						element => element.TextRunProperties.SetTextDecorations(Resources.WaveErrorUnderline));
				}

				var semanticErrors = validationModel.ColumnNodeValidity
					.Concat(validationModel.ObjectNodeValidity)
					.Concat(validationModel.FunctionNodeValidity)
					.Select(nv => new { Node = nv.Key, HasSemanticError = nv.Value.SemanticError != SemanticError.None });
				
				foreach (var semanticError in semanticErrors.Where(e => e.HasSemanticError))
				{
					ProcessNodeAtLine(line, semanticError.Node.SourcePosition,
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
						element => element.BackgroundBrush = HighlightBrush);
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