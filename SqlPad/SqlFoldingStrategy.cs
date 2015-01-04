using System.Linq;
using ICSharpCode.AvalonEdit.Folding;

namespace SqlPad
{
	public class SqlFoldingStrategy
	{
		private readonly SqlTextEditor _editor;

		public FoldingManager FoldingManager { get; private set; }

		public SqlFoldingStrategy(FoldingManager foldingManager, SqlTextEditor editor)
		{
			FoldingManager = foldingManager;
			_editor = editor;
		}

		public void UpdateFoldings(StatementCollection statements)
		{
			var foldings = statements.FoldingSections
				.Where(IsMultilineOrNestedSection)
				.Select(s => new NewFolding(s.FoldingStart, s.FoldingEnd) { Name = s.Placeholder });
			
			FoldingManager.UpdateFoldings(foldings, -1);
		}

		public void Store(WorkDocument workDocument)
		{
			workDocument.UpdateFoldingStates(FoldingManager.AllFoldings.Select(f => f.IsFolded));
		}

		public void Restore(WorkDocument workDocument)
		{
			var foldingEnumerator = FoldingManager.AllFoldings.GetEnumerator();
			foreach (var isFolded in workDocument.FoldingStates.Where(s => foldingEnumerator.MoveNext()))
			{
				foldingEnumerator.Current.IsFolded = isFolded;
			}
		}

		private bool IsMultilineOrNestedSection(FoldingSection section)
		{
			return section.IsNested || _editor.GetLineNumberByOffset(section.FoldingStart) != _editor.GetLineNumberByOffset(section.FoldingEnd);
		}
	}
}
