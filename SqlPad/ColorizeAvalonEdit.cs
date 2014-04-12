using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace SqlPad
{
	public class ColorizeAvalonEdit : DocumentColorizingTransformer
	{
		private ICollection<StatementBase> _parsedStatements = new List<StatementBase>();
		private readonly IStatementValidator _validator = ConfigurationProvider.InfrastructureFactory.CreateStatementValidator();
		private readonly IDatabaseModel _databaseModel = ConfigurationProvider.InfrastructureFactory.CreateDatabaseModel(null);
		private static readonly SolidColorBrush ErrorBrush = new SolidColorBrush(Colors.Red);
		private static readonly SolidColorBrush NormalTextBrush = new SolidColorBrush(Colors.Black);

		public void SetStatementCollection(ICollection<StatementBase> statements)
		{
			_parsedStatements = statements;
		}

		protected override void ColorizeLine(DocumentLine line)
		{
			var statementsAtLine = _parsedStatements.Where(s => s.SourcePosition.IndexStart <= line.EndOffset && s.SourcePosition.IndexEnd >= line.Offset);

			foreach (var statement in statementsAtLine)
			{
				var backgroundColor = new SolidColorBrush(statement.ProcessingStatus == ProcessingStatus.Success ? Colors.LightGreen : Colors.PaleVioletRed);

				var colorStartOffset = Math.Max(line.Offset, statement.SourcePosition.IndexStart);
				var colorEndOffset = Math.Min(line.EndOffset, statement.SourcePosition.IndexEnd + 1);

				var validationModel = _validator.ResolveReferences(null, statement, _databaseModel);
				var nodeRecognizeData = validationModel.TableNodeValidity.Concat(validationModel.ColumnNodeValidity.Select(kvp => new KeyValuePair<StatementDescriptionNode, bool>(kvp.Key, kvp.Value.IsRecognized)));

				foreach (var nodeValidity in nodeRecognizeData)
				{
					ProcessNodeAtLine(line, nodeValidity.Key.SourcePosition,
						element =>
						{
							element.TextRunProperties.SetForegroundBrush(nodeValidity.Value ? NormalTextBrush : ErrorBrush);
						});
				}

				var semanticErrors = validationModel.ColumnNodeValidity.Select(nv => new { ColumnNode = nv.Key, HasSemanticError = nv.Value.SemanticError != ColumnSemanticError.None });
				foreach (var semanticError in semanticErrors.Where(e => e.HasSemanticError))
				{
					ProcessNodeAtLine(line, semanticError.ColumnNode.SourcePosition,
						element => element.TextRunProperties.SetTextDecorations(Resources.WaveErrorUnderline));
					/*ProcessNodeAtLine(line, semanticError.ColumnNode.SourcePosition,
						element => element.TextRunProperties.SetTextDecorations(Resources.BoxedText));*/
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