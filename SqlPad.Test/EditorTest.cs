using System;
using ICSharpCode.AvalonEdit;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;

namespace SqlPad.Test
{
	[TestFixture] 
	public class EditorTest
	{
		private TextEditor _editor;

		[SetUp]
		public void SetUp()
		{
			_editor = new TextEditor();
		}

		[Test(Description = @""), STAThread]
		public void SetLineDuplicationAtLineBeginning()
		{
			_editor.Text = "SELECT * FROM SELECTION;";
			GenericCommandHandler.DuplicateText(_editor.TextArea, null);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION;\nSELECT * FROM SELECTION;");
			_editor.CaretOffset.ShouldBe(25);
		}

		[Test(Description = @""), STAThread]
		public void SetLineDuplicationAtLineEnd()
		{
			_editor.Text = "SELECT * FROM SELECTION;";
			_editor.CaretOffset = 24;
			GenericCommandHandler.DuplicateText(_editor.TextArea, null);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION;\nSELECT * FROM SELECTION;");
			_editor.CaretOffset.ShouldBe(49);
		}

		[Test(Description = @""), STAThread]
		public void SetSelectionDuplicate()
		{
			_editor.Text = "SELECT * FROM SELECTION;";
			_editor.CaretOffset = 13;
			_editor.SelectionLength = 10;
			_editor.SelectedText.ShouldBe(" SELECTION");
			GenericCommandHandler.DuplicateText(_editor.TextArea, null);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION SELECTION;");
			_editor.CaretOffset.ShouldBe(33);
		}
	}
}
