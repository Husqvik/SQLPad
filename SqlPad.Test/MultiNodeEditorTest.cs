using System;
using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;

namespace SqlPad.Test
{
	[TestFixture]
	public class MultiNodeEditorTest
	{
		private SqlTextEditor _editor;
		private IInfrastructureFactory _infrastructureFactory;

		[SetUp]
		public void SetUp()
		{
			_editor = new SqlTextEditor();

			var connectionConfiguration = ConfigurationProvider.GetConnectionCofiguration(ConfigurationProvider.ConnectionStrings[0].Name);
			_infrastructureFactory = connectionConfiguration.InfrastructureFactory;
		}

		private SqlDocumentRepository ConfigureDocumentRepository()
		{
			var documentRepository = new SqlDocumentRepository(_infrastructureFactory.CreateParser(), _infrastructureFactory.CreateStatementValidator(), _infrastructureFactory.CreateDatabaseModel(ConfigurationProvider.ConnectionStrings[0], "Test database model"));
			documentRepository.UpdateStatements(_editor.Text);
			return documentRepository;
		}

		[Test(Description = @""), STAThread]
		public void AddTextToObjectIdentifier()
		{
			_editor.Text = "SELECT DUAL.DUMMY C1, DUAL.DUMMY C2 FROM DUAL DUAL";
			_editor.CaretOffset = 9;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor multiNodeEditor;
			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out multiNodeEditor).ShouldBe(true);

			multiNodeEditor.Replace("__").ShouldBe(true);

			_editor.Text.ShouldBe("SELECT DUAL.DUMMY C1, DU__AL.DUMMY C2 FROM DUAL DU__AL");
		}

		[Test(Description = @""), STAThread]
		public void ReplaceTextWhenRenamingObjectIdentifier()
		{
			_editor.Text = "SELECT DU__AL.DUMMY C1, DU__AL.DUMMY C2 FROM DUAL DU__AL";
			_editor.CaretOffset = 9;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor multiNodeEditor;
			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out multiNodeEditor).ShouldBe(true);

			_editor.SelectionLength = 2;
			_editor.CaretOffset = 9;
			multiNodeEditor.Replace("xx").ShouldBe(true);

			_editor.Text.ShouldBe("SELECT DU__AL.DUMMY C1, DUxxAL.DUMMY C2 FROM DUAL DUxxAL");
		}

		[Test(Description = @""), STAThread]
		public void RemoveCharacterToObjectIdentifierUsingDelete()
		{
			_editor.Text = "SELECT DU_AL.DUMMY C1, DU_AL.DUMMY C2 FROM DUAL DU_AL";
			_editor.CaretOffset = 9;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor multiNodeEditor;
			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out multiNodeEditor).ShouldBe(true);

			multiNodeEditor.RemoveCharacter(false).ShouldBe(true);

			_editor.Text.ShouldBe("SELECT DU_AL.DUMMY C1, DUAL.DUMMY C2 FROM DUAL DUAL");
		}

		[Test(Description = @""), STAThread]
		public void RemoveCharacterToObjectIdentifierUsingBackspace()
		{
			_editor.Text = "SELECT DU_AL.DUMMY C1, DU_AL.DUMMY C2 FROM DUAL DU_AL";
			_editor.CaretOffset = 10;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor multiNodeEditor;
			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out multiNodeEditor).ShouldBe(true);

			multiNodeEditor.RemoveCharacter(true).ShouldBe(true);

			_editor.Text.ShouldBe("SELECT DU_AL.DUMMY C1, DUAL.DUMMY C2 FROM DUAL DUAL");
		}

		[Test(Description = @""), STAThread]
		public void RemoveSelectionToObjectIdentifierUsingDelete()
		{
			_editor.Text = "SELECT DU__AL.DUMMY C1, DU__AL.DUMMY C2 FROM DUAL DU__AL";
			_editor.CaretOffset = 9;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor multiNodeEditor;
			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out multiNodeEditor).ShouldBe(true);

			_editor.SelectionLength = 2;
			_editor.CaretOffset = 9;
			multiNodeEditor.RemoveCharacter(false).ShouldBe(true);

			_editor.Text.ShouldBe("SELECT DU__AL.DUMMY C1, DUAL.DUMMY C2 FROM DUAL DUAL");
		}

		[Test(Description = @""), STAThread]
		public void RemoveSelectionToObjectIdentifierUsingBackspace()
		{
			_editor.Text = "SELECT DU__AL.DUMMY C1, DU__AL.DUMMY C2 FROM DUAL DU__AL";
			_editor.CaretOffset = 9;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor multiNodeEditor;
			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out multiNodeEditor).ShouldBe(true);

			_editor.SelectionLength = 2;
			multiNodeEditor.RemoveCharacter(true).ShouldBe(true);

			_editor.Text.ShouldBe("SELECT DU__AL.DUMMY C1, DUAL.DUMMY C2 FROM DUAL DUAL");
		}
	}
}
