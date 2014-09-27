using System.Collections.Generic;
using System.Linq;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace SqlPad
{
	public class SqlFoldingStrategy : AbstractFoldingStrategy
	{
		private readonly FoldingManager _foldingManager;
		private readonly TextEditor _editor;
		private StatementCollection _statements;

		public SqlFoldingStrategy(FoldingManager foldingManager, TextEditor editor)
		{
			_foldingManager = foldingManager;
			_editor = editor;
		}

		public override IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
		{
			firstErrorOffset = -1;
			return _statements.SelectMany(s => s.Sections)
				.Where(IsMultilineOrNestedSection)
				.Select(s => new NewFolding(s.FoldingStart, s.FoldingEnd) { Name = s.Placeholder });
		}

		public void UpdateFoldings(StatementCollection statements)
		{
			_statements = statements;
			UpdateFoldings(_foldingManager, _editor.Document);
		}

		public void Store(WorkingDocument workingDocument)
		{
			workingDocument.UpdateFoldingStates(_foldingManager.AllFoldings.Select(f => f.IsFolded));
		}

		public void Restore(WorkingDocument workingDocument)
		{
			var foldingEnumerator = _foldingManager.AllFoldings.GetEnumerator();
			foreach (var isFolded in workingDocument.FoldingStates)
			{
				if (!foldingEnumerator.MoveNext())
				{
					break;
				}
				
				foldingEnumerator.Current.IsFolded = isFolded;
			}
		}

		private bool IsMultilineOrNestedSection(FoldingSection section)
		{
			return section.IsNestedSection || _editor.GetLineNumberByOffset(section.FoldingStart) != _editor.GetLineNumberByOffset(section.FoldingEnd);
		}
	}
}
