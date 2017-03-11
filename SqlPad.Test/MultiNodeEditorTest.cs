using System;
using System.Threading;
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

			var connectionConfiguration = ConfigurationProvider.GetConnectionConfiguration(ConfigurationProvider.ConnectionStrings[0].Name);
			_infrastructureFactory = connectionConfiguration.InfrastructureFactory;
		}

		private SqlDocumentRepository ConfigureDocumentRepository()
		{
			var documentRepository = new SqlDocumentRepository(_infrastructureFactory.CreateParser(), _infrastructureFactory.CreateStatementValidator(), _infrastructureFactory.CreateDatabaseModel(ConfigurationProvider.ConnectionStrings[0], "Test database model"));
			documentRepository.UpdateStatements(_editor.Text);
			return documentRepository;
		}

		[Test, Apartment(ApartmentState.STA)]
		public void AddTextToObjectIdentifier()
		{
			_editor.Text = "SELECT DUAL.DUMMY C1, DUAL.DUMMY C2 FROM DUAL DUAL";
			_editor.CaretOffset = 9;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out MultiNodeEditor multiNodeEditor).ShouldBe(true);

			multiNodeEditor.Replace("__").ShouldBe(true);

			_editor.Text.ShouldBe("SELECT DUAL.DUMMY C1, DU__AL.DUMMY C2 FROM DUAL DU__AL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void CancelEditing()
		{
			const string originalText = "SELECT DUAL.DUMMY C1, DUAL.DUMMY C2 FROM DUAL DUAL";
			_editor.Text = originalText;

			const int originalCaretOffset = 9;
			_editor.CaretOffset = 9;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out MultiNodeEditor multiNodeEditor).ShouldBe(true);

			_editor.Document.Insert(_editor.CaretOffset, "__");

			multiNodeEditor.Replace("__").ShouldBe(true);
			multiNodeEditor.Cancel();

			_editor.Text.ShouldBe(originalText);
			_editor.CaretOffset.ShouldBe(originalCaretOffset);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void ReplaceTextWhenRenamingObjectIdentifier()
		{
			_editor.Text = "SELECT DU__AL.DUMMY C1, DU__AL.DUMMY C2 FROM DUAL DU__AL";
			_editor.CaretOffset = 9;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out MultiNodeEditor multiNodeEditor).ShouldBe(true);

			_editor.SelectionLength = 2;
			_editor.CaretOffset = 9;
			multiNodeEditor.Replace("xx").ShouldBe(true);

			_editor.Text.ShouldBe("SELECT DU__AL.DUMMY C1, DUxxAL.DUMMY C2 FROM DUAL DUxxAL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void RemoveCharacterToObjectIdentifierUsingDelete()
		{
			_editor.Text = "SELECT DU_AL.DUMMY C1, DU_AL.DUMMY C2 FROM DUAL DU_AL";
			_editor.CaretOffset = 9;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out MultiNodeEditor multiNodeEditor).ShouldBe(true);

			multiNodeEditor.RemoveCharacter(false).ShouldBe(true);

			_editor.Text.ShouldBe("SELECT DU_AL.DUMMY C1, DUAL.DUMMY C2 FROM DUAL DUAL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void RemoveCharacterToObjectIdentifierUsingBackspace()
		{
			_editor.Text = "SELECT DU_AL.DUMMY C1, DU_AL.DUMMY C2 FROM DUAL DU_AL";
			_editor.CaretOffset = 10;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out MultiNodeEditor multiNodeEditor).ShouldBe(true);

			multiNodeEditor.RemoveCharacter(true).ShouldBe(true);

			_editor.Text.ShouldBe("SELECT DU_AL.DUMMY C1, DUAL.DUMMY C2 FROM DUAL DUAL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void RemoveSelectionToObjectIdentifierUsingDelete()
		{
			_editor.Text = "SELECT DU__AL.DUMMY C1, DU__AL.DUMMY C2 FROM DUAL DU__AL";
			_editor.CaretOffset = 9;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out MultiNodeEditor multiNodeEditor).ShouldBe(true);

			_editor.SelectionLength = 2;
			_editor.CaretOffset = 9;
			multiNodeEditor.RemoveCharacter(false).ShouldBe(true);

			_editor.Text.ShouldBe("SELECT DU__AL.DUMMY C1, DUAL.DUMMY C2 FROM DUAL DUAL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void RemoveSelectionToObjectIdentifierUsingBackspace()
		{
			_editor.Text = "SELECT DU__AL.DUMMY C1, DU__AL.DUMMY C2 FROM DUAL DU__AL";
			_editor.CaretOffset = 9;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out MultiNodeEditor multiNodeEditor).ShouldBe(true);

			_editor.SelectionLength = 2;
			multiNodeEditor.RemoveCharacter(true).ShouldBe(true);

			_editor.Text.ShouldBe("SELECT DU__AL.DUMMY C1, DUAL.DUMMY C2 FROM DUAL DUAL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void ReplaceTextWhenRenamingColumnIdentifierWithSameColumnWithSameAliases()
		{
			_editor.Text = "SELECT ALIAS1 ALIAS1 FROM (SELECT ALIAS1 ALIAS1 FROM (SELECT DUMMY ALIAS1 FROM DUAL))";
			_editor.CaretOffset = 8;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out MultiNodeEditor multiNodeEditor).ShouldBe(true);

			multiNodeEditor.Replace("__").ShouldBe(true);

			_editor.Text.ShouldBe("SELECT ALIAS1 ALIAS1 FROM (SELECT A__LIAS1 A__LIAS1 FROM (SELECT DUMMY A__LIAS1 FROM DUAL))");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void ReplaceTextWhenRenamingColumnIdentifierWithSameColumnWithMultipleAliases()
		{
			_editor.Text = "SELECT ALIAS2 ALIAS3 FROM (SELECT ALIAS1 ALIAS2 FROM (SELECT DUMMY ALIAS1 FROM DUAL))";
			_editor.CaretOffset = 8;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out MultiNodeEditor multiNodeEditor).ShouldBe(true);

			multiNodeEditor.Replace("__").ShouldBe(true);

			_editor.Text.ShouldBe("SELECT ALIAS2 ALIAS3 FROM (SELECT ALIAS1 A__LIAS2 FROM (SELECT DUMMY ALIAS1 FROM DUAL))");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void ReplaceTextWhenRenamingChildColumnIdentifierWithSameColumnWithMultipleAliases()
		{
			_editor.Text = "SELECT ALIAS2 ALIAS3 FROM (SELECT ALIAS1 ALIAS2 FROM (SELECT DUMMY ALIAS1 FROM DUAL))";
			_editor.CaretOffset = 35;

			var executionContext = ActionExecutionContext.Create(_editor, ConfigureDocumentRepository());

			MultiNodeEditor.TryCreateMultiNodeEditor(_editor, executionContext, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), out MultiNodeEditor multiNodeEditor).ShouldBe(true);

			multiNodeEditor.Replace("__").ShouldBe(true);

			_editor.Text.ShouldBe("SELECT ALIAS2 ALIAS3 FROM (SELECT ALIAS1 ALIAS2 FROM (SELECT DUMMY A__LIAS1 FROM DUAL))");
		}
	}
}
