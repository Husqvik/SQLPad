using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class AddAliasCommand : OracleCommandBase
	{
		public const string Title = "Add Alias";
		private OracleQueryBlock _currentQueryBlock;
		private OracleDataObjectReference _currentObjectReference;
		private OracleColumnReference _currentColumnReference;

		private AddAliasCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override Func<StatementGrammarNode, bool> CurrentNodeFilterFunction
		{
			get { return n => n.Id.In(Terminals.ObjectIdentifier, Terminals.Identifier); }
		}

		protected override bool CanExecute()
		{
			if (CurrentNode == null)
				return false;

			_currentQueryBlock = SemanticModel.GetQueryBlock(CurrentNode);
			
			if (CurrentNode.Id == Terminals.ObjectIdentifier)
			{
				var objectReferences = _currentQueryBlock.ObjectReferences.Where(t => t.ObjectNode == CurrentNode).ToArray();
				return objectReferences.Length == 1 && (_currentObjectReference = objectReferences[0]).AliasNode == null;
			}

			if (CurrentNode.Id == Terminals.Identifier)
			{
				return GetCurrentColumnReference() != null;
			}

			return false;
		}

		private OracleColumnReference GetCurrentColumnReference()
		{
			return _currentColumnReference = _currentQueryBlock.Columns
				.Where(c => c.IsDirectReference)
				.SelectMany(c => c.ColumnReferences)
				.FirstOrDefault(c => c.ColumnNode == CurrentNode && c.SelectListColumn.AliasNode == CurrentNode && c.ColumnNodeColumnReferences.Count == 1);
		}

		private CommandSettingsModel ConfigureSettings()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			var settingsModel = ExecutionContext.SettingsProvider.Settings;
			settingsModel.ValidationRule = new OracleIdentifierValidationRule();

			switch (CurrentNode.Id)
			{
				case Terminals.ObjectIdentifier:
					settingsModel.Title = "Add Object Alias";
					settingsModel.Description = String.Format("Enter an alias for the object '{0}'", CurrentNode.Token.Value);
					break;
				case Terminals.Identifier:
					settingsModel.Title = "Add Column Alias";
					settingsModel.Description = String.Format("Enter an alias for the column '{0}'", CurrentNode.Token.Value);
					break;
			}

			settingsModel.Heading = settingsModel.Title;

			return settingsModel;
		}

		protected override void Execute()
		{
			var settingsModel = ConfigureSettings();

			if (!ExecutionContext.SettingsProvider.GetSettings())
				return;

			switch (CurrentNode.Id)
			{
				case Terminals.ObjectIdentifier:
					AddObjectAlias(settingsModel.Value);
					break;
				case Terminals.Identifier:
					AddColumnAlias(settingsModel.Value);
					break;
			}
		}

		private void AddColumnAlias(string alias)
		{
			ExecutionContext.SegmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = _currentColumnReference.ColumnNode.SourcePosition.IndexEnd + 1,
					Text = " " + alias
				});

			var parentObjectReferences = GetParentObjectReferences(_currentColumnReference.Owner);
			foreach (var objectReference in parentObjectReferences)
			{
				AddColumnAliasToQueryBlock(_currentColumnReference.NormalizedName, alias, objectReference);
			}
		}

		private IEnumerable<OracleDataObjectReference> GetParentObjectReferences(OracleQueryBlock referredQueryBlock)
		{
			return SemanticModel.QueryBlocks
				.SelectMany(qb => qb.ObjectReferences)
				.Where(o => o.QueryBlocks.Count == 1 && o.QueryBlocks.First() == referredQueryBlock);
		}

		private void AddColumnAliasToQueryBlock(string columnName, string alias, OracleDataObjectReference objectReference)
		{
			var parentObjectReferences = new HashSet<OracleDataObjectReference>();
			var columnReferences = objectReference.Owner.AllColumnReferences.Where(c => c.ValidObjectReference == objectReference && c.NormalizedName == columnName);
			foreach (var columnReference in columnReferences)
			{
				ExecutionContext.SegmentsToReplace.Add(
					new TextSegment
					{
						IndextStart = columnReference.ColumnNode.SourcePosition.IndexStart,
						Length = columnReference.ColumnNode.SourcePosition.Length,
						Text = alias
					});

				if (columnReference.SelectListColumn != null &&
				    columnReference.SelectListColumn.IsDirectReference && columnReference.SelectListColumn.AliasNode == columnReference.ColumnNode)
				{
					parentObjectReferences.AddRange(GetParentObjectReferences(objectReference.Owner));
				}
			}

			foreach (var parentReference in parentObjectReferences)
			{
				AddColumnAliasToQueryBlock(columnName, alias, parentReference);
			}
		}

		private void AddObjectAlias(string alias)
		{
			var prefixedColumnReferences = _currentQueryBlock.AllColumnReferences
				.Where(c => (c.OwnerNode != null || c.ObjectNode != null) && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == _currentObjectReference);

			var asteriskColumnReferences = _currentQueryBlock.Columns.Where(c => c.IsAsterisk).SelectMany(c => c.ColumnReferences)
				.Where(c => c.ObjectNodeObjectReferences.Count == 1 && c.ObjectNodeObjectReferences.Single() == _currentObjectReference);

			foreach (var columnReference in prefixedColumnReferences.Concat(asteriskColumnReferences))
			{
				var firstPrefixNode = columnReference.OwnerNode ?? columnReference.ObjectNode;

				ExecutionContext.SegmentsToReplace.Add(
					new TextSegment
					{
						IndextStart = firstPrefixNode == null ? columnReference.ColumnNode.SourcePosition.IndexStart - 1 : firstPrefixNode.SourcePosition.IndexStart,
						Length = columnReference.ColumnNode.SourcePosition.IndexStart - firstPrefixNode.SourcePosition.IndexStart,
						Text = alias + "."
					});
			}

			ExecutionContext.SegmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = CurrentNode.SourcePosition.IndexEnd + 1,
					Length = 0,
					Text = " " + alias
				});
		}
	}
}
