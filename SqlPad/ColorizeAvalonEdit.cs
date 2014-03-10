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
		private ICollection<IStatement> _parsedStatements = new List<IStatement>();
		private readonly IStatementValidator _validator = ConfigurationProvider.InfrastructureFactory.CreateStatementValidator();
		private readonly IDatabaseModel _databaseModel = ConfigurationProvider.InfrastructureFactory.CreateDatabaseModel(null);
		private static readonly SolidColorBrush ErrorBrush = new SolidColorBrush(Colors.Red);
		private static readonly SolidColorBrush NormalTextBrush = new SolidColorBrush(Colors.Black);

		public void SetStatementCollection(ICollection<IStatement> statements)
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

				foreach (var nodeValidity in _validator.ResolveReferences(null, statement, _databaseModel).NodeValidity)
				{
					if (line.Offset > nodeValidity.Key.SourcePosition.IndexEnd + 1 ||
					    line.EndOffset < nodeValidity.Key.SourcePosition.IndexStart)
						continue;

					var errorColorStartOffset = Math.Max(line.Offset, nodeValidity.Key.SourcePosition.IndexStart);
					var errorColorEndOffset = Math.Min(line.EndOffset, nodeValidity.Key.SourcePosition.IndexEnd + 1);

					ChangeLinePart(errorColorStartOffset, errorColorEndOffset,
						element =>
						{
							element.TextRunProperties.SetForegroundBrush(nodeValidity.Value ? NormalTextBrush : ErrorBrush);
						});
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
	}
}