using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class AddAliasCommand : OracleCommandBase
	{
		public const string Title = "Add Alias";

		private OracleDataObjectReference _currentObjectReference;
		private OracleColumnReference _currentColumnReference;

		private AddAliasCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override Func<StatementGrammarNode, bool> CurrentNodeFilterFunction
		{
			get { return n => n.Id.In(Terminals.ObjectIdentifier, Terminals.Identifier); }
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			if (CurrentNode == null || CurrentQueryBlock == null)
			{
				return false;
			}
			
			if (CurrentNode.Id == Terminals.ObjectIdentifier)
			{
				var objectReferences = CurrentQueryBlock.ObjectReferences
					.SelectMany(o => o.IncludeInnerReferences)
					.Where(o => o.ObjectNode == CurrentNode)
					.ToArray();
				
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
			return _currentColumnReference = CurrentQueryBlock.Columns
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
					settingsModel.Description = $"Enter an alias for the object '{CurrentNode.Token.Value}'";
					break;
				case Terminals.Identifier:
					settingsModel.Title = "Add Column Alias";
					settingsModel.Description = $"Enter an alias for the column '{CurrentNode.Token.Value}'";
					break;
			}

			settingsModel.Heading = settingsModel.Title;

			return settingsModel;
		}

		protected override void Execute()
		{
			var settingsModel = ConfigureSettings();

			if (!ExecutionContext.SettingsProvider.GetSettings())
			{
				return;
			}

			switch (CurrentNode.Id)
			{
				case Terminals.ObjectIdentifier:
					new AliasCommandHelper(ExecutionContext).AddObjectAlias(_currentObjectReference, settingsModel.Value);
					break;
				case Terminals.Identifier:
					new AliasCommandHelper(ExecutionContext).AddColumnAlias(_currentColumnReference.ColumnNode, CurrentQueryBlock, _currentColumnReference.NormalizedName, settingsModel.Value);
					break;
			}
		}
	}

	internal class AliasCommandHelper
	{
		private readonly ActionExecutionContext _executionContext;

		public AliasCommandHelper(ActionExecutionContext executionContext)
		{
			_executionContext = executionContext;
		}

		public void AddObjectAlias(OracleDataObjectReference dataObjectReference, string alias)
		{
			var prefixedColumnReferences = dataObjectReference.Owner.AllColumnReferences
				.Where(c => (c.OwnerNode != null || c.ObjectNode != null) && c.ObjectNodeObjectReferences.Count == 1 && c.ObjectNodeObjectReferences.First() == dataObjectReference);

			foreach (var columnReference in prefixedColumnReferences)
			{
				var firstPrefixNode = columnReference.OwnerNode ?? columnReference.ObjectNode;

				_executionContext.SegmentsToReplace.Add(
					new TextSegment
					{
						IndextStart = firstPrefixNode.SourcePosition.IndexStart,
						Length = columnReference.ColumnNode.SourcePosition.IndexStart - firstPrefixNode.SourcePosition.IndexStart,
						Text = alias + "."
					});
			}

			var currentName = dataObjectReference.FullyQualifiedObjectName.NormalizedName;
			var isUnquotable = !String.Equals(alias, currentName) && String.Equals(alias.ToQuotedIdentifier(), currentName);
			TextSegment textSegment;
			if (isUnquotable)
			{
				textSegment =
					new TextSegment
					{
						IndextStart = dataObjectReference.ObjectNode.SourcePosition.IndexStart,
						Length = dataObjectReference.ObjectNode.SourcePosition.Length,
						Text = alias
					};
			}
			else
			{
				textSegment =
					new TextSegment
					{
						IndextStart = (dataObjectReference.FlashbackClauseNode ?? dataObjectReference.DatabaseLinkNode ?? dataObjectReference.ObjectNode).SourcePosition.IndexEnd + 1,
						Length = 0,
						Text = $" {alias}"
					};
			}

			_executionContext.SegmentsToReplace.Add(textSegment);
		}

		public void AddColumnAlias(StatementGrammarNode columnNode, OracleQueryBlock queryBlock, string columnNormalizedName, string alias)
		{
			_executionContext.SegmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = columnNode.SourcePosition.IndexEnd + 1,
					Text = " " + alias
				});

			var parentObjectReferences = GetParentObjectReferences(queryBlock);
			foreach (var objectReference in parentObjectReferences)
			{
				AddColumnAliasToQueryBlock(columnNormalizedName, alias, objectReference);
			}
		}

		internal static IEnumerable<OracleDataObjectReference> GetParentObjectReferences(OracleQueryBlock referredQueryBlock)
		{
			return referredQueryBlock.SemanticModel.QueryBlocks
				.SelectMany(GetInlineViewObjectReferences)
				.Where(o => o.QueryBlocks.First() == referredQueryBlock);
		}

		private static IEnumerable<OracleDataObjectReference> GetInlineViewObjectReferences(OracleQueryBlock queryBlock)
		{
			return queryBlock.ObjectReferences
				.SelectMany(o => o.IncludeInnerReferences)
				.Where(o => o.QueryBlocks.Count == 1);
		}

		private void AddColumnAliasToQueryBlock(string columnName, string alias, OracleReference objectReference)
		{
			var parentObjectReferences = new HashSet<OracleDataObjectReference>();
			var columnReferences = objectReference.Owner.AllColumnReferences
				.Where(c =>
					(c.ValidObjectReference == objectReference ||
					 (c.Owner != null && c.ValidObjectReference?.QueryBlocks.FirstOrDefault() == c.Owner) ||
					 (c.ValidObjectReference as OracleDataObjectReference)?.IncludeInnerReferences.Any(r => r == objectReference) == true) &&
					String.Equals(c.NormalizedName, columnName));

			foreach (var columnReference in columnReferences)
			{
				_executionContext.SegmentsToReplace.Add(
					new TextSegment
					{
						IndextStart = columnReference.ColumnNode.SourcePosition.IndexStart,
						Length = columnReference.ColumnNode.SourcePosition.Length,
						Text = alias
					});

				if (columnReference.SelectListColumn != null &&
					columnReference.SelectListColumn.IsDirectReference && columnReference.SelectListColumn.AliasNode == columnReference.ColumnNode)
				{
					parentObjectReferences.UnionWith(GetParentObjectReferences(objectReference.Owner));
				}
			}

			foreach (var parentReference in parentObjectReferences)
			{
				AddColumnAliasToQueryBlock(columnName, alias, parentReference);
			}
		}
	}
}
