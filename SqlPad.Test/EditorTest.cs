using System;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;

namespace SqlPad.Test
{
	[TestFixture] 
	public class EditorTest
	{
		private SqlTextEditor _editor;

		[SetUp]
		public void SetUp()
		{
			_editor = new SqlTextEditor();
		}

		[Test, STAThread]
		public void SetLineDuplicationAtLineBeginning()
		{
			_editor.Text = "SELECT * FROM SELECTION;";
			GenericCommandHandler.DuplicateText(_editor.TextArea, null);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION;\nSELECT * FROM SELECTION;");
			_editor.CaretOffset.ShouldBe(25);
		}

		[Test, STAThread]
		public void SetLineDuplicationAtLineEnd()
		{
			_editor.Text = "SELECT * FROM SELECTION;";
			_editor.CaretOffset = 24;
			GenericCommandHandler.DuplicateText(_editor.TextArea, null);

			_editor.Text.ShouldBe("SELECT * FROM SELECTION;\nSELECT * FROM SELECTION;");
			_editor.CaretOffset.ShouldBe(49);
		}

		[Test, STAThread]
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

		[Test, STAThread]
		public void TestBlockComments()
		{
			_editor.Text = "SELECT * FROM SELECTION;\nSELECT * FROM RESPONDENTBUCKET";
			_editor.CaretOffset = 13;
			_editor.SelectionLength = 35;

			GenericCommandHandler.HandleBlockComments(_editor.TextArea, null);

			_editor.Text.ShouldBe("SELECT * FROM/* SELECTION;\nSELECT * FROM RESPONDEN*/TBUCKET");
			_editor.CaretOffset.ShouldBe(50);
			_editor.SelectionLength.ShouldBe(0);
		}

		[Test, STAThread]
		public void TestLineComments()
		{
			_editor.Text = "SELECT * FROM SELECTION;\nSELECT * FROM RESPONDENTBUCKET";
			_editor.CaretOffset = 13;
			_editor.SelectionLength = 35;

			GenericCommandHandler.HandleLineComments(_editor.TextArea, null);

			_editor.Text.ShouldBe("--SELECT * FROM SELECTION;\n--SELECT * FROM RESPONDENTBUCKET");
			_editor.CaretOffset.ShouldBe(52);
			_editor.SelectionLength.ShouldBe(0);
		}
	}
}
