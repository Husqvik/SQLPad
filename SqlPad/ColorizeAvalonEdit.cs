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
		private readonly HashSet<TextSegment> _highlightSegments = new HashSet<TextSegment>();
		private readonly IStatementValidator _validator = ConfigurationProvider.InfrastructureFactory.CreateStatementValidator();
		private readonly IDatabaseModel _databaseModel = ConfigurationProvider.InfrastructureFactory.CreateDatabaseModel(null);
		private static readonly SolidColorBrush ErrorBrush = new SolidColorBrush(Colors.Red);
		private static readonly SolidColorBrush NormalTextBrush = new SolidColorBrush(Colors.Black);
		private static readonly SolidColorBrush HighlightBrush = new SolidColorBrush(Colors.Turquoise);

		public void SetStatementCollection(StatementCollection statements)
		{
			_parsedStatements = statements;
		}

		public void SetHighlightSegments(ICollection<TextSegment> highlightSegments)
		{
			if (highlightSegments != null)
			{
				_highlightSegments.AddRange(highlightSegments);
			}
			else
			{
				_highlightSegments.Clear();
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
				var nodeRecognizeData = validationModel.TableNodeValidity
					.Select(kvp => new KeyValuePair<StatementDescriptionNode, bool>(kvp.Key, kvp.Value.IsRecognized))
					.Concat(validationModel.ColumnNodeValidity.Select(kvp => new KeyValuePair<StatementDescriptionNode, bool>(kvp.Key, kvp.Value.IsRecognized)));

				foreach (var nodeValidity in nodeRecognizeData)
				{
					ProcessNodeAtLine(line, nodeValidity.Key.SourcePosition,
						element =>
						{
							element.TextRunProperties.SetForegroundBrush(nodeValidity.Value ? NormalTextBrush : ErrorBrush);
						});
				}

				foreach (var invalidGrammarNode in statement.InvalidGrammarNodes)
				{
					ProcessNodeAtLine(line, invalidGrammarNode.SourcePosition,
						element => element.TextRunProperties.SetTextDecorations(Resources.WaveErrorUnderline));
				}

				var semanticErrors = validationModel.ColumnNodeValidity
					.Concat(validationModel.TableNodeValidity)
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

				foreach (var highlightSegment in _highlightSegments)
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