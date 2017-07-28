using System;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Test
{
	[TestFixture]
	public class EditorNavigationServiceTest
	{
		[SetUp]
		public void SetUp()
		{
			EditorNavigationService.Initialize();
		}

		[Test, STAThread]
		public void TestInitializedEditorNavigationService()
		{
			EditorNavigationService.IsEnabled.ShouldBeTrue();
			EditorNavigationService.GetNextEdit().ShouldBeNull();
			Should.Throw<ArgumentOutOfRangeException>(() => EditorNavigationService.GetPreviousEdit());
		}

		[Test, STAThread]
		public void TestRegisterDocumentCursorPosition()
		{
			var workingDocument = new WorkDocument();
			const int originalPosition = 10;
			const int position2 = 99;
			const int lastPosition = 43;
			EditorNavigationService.RegisterDocumentCursorPosition(workingDocument, originalPosition);
			EditorNavigationService.RegisterDocumentCursorPosition(workingDocument, position2);
			EditorNavigationService.RegisterDocumentCursorPosition(workingDocument, lastPosition);
			EditorNavigationService.GetNextEdit().ShouldBeNull();
			var previousEdit = EditorNavigationService.GetPreviousEdit();
			previousEdit.ShouldNotBeNull();
			previousEdit.Document.ShouldBe(workingDocument);
			previousEdit.CursorPosition.ShouldBe(position2);
			previousEdit = EditorNavigationService.GetPreviousEdit();
			previousEdit.ShouldNotBeNull();
			previousEdit.Document.ShouldBe(workingDocument);
			previousEdit.CursorPosition.ShouldBe(originalPosition);
			previousEdit = EditorNavigationService.GetPreviousEdit();
			previousEdit.ShouldNotBeNull();
			previousEdit.Document.ShouldBe(workingDocument);
			previousEdit.CursorPosition.ShouldBe(originalPosition);

			var nextEdit = EditorNavigationService.GetNextEdit();
			nextEdit.ShouldNotBeNull();
			nextEdit.Document.ShouldBe(workingDocument);
			nextEdit.CursorPosition.ShouldBe(position2);
			nextEdit = EditorNavigationService.GetNextEdit();
			nextEdit.ShouldNotBeNull();
			nextEdit.Document.ShouldBe(workingDocument);
			nextEdit.CursorPosition.ShouldBe(lastPosition);
			EditorNavigationService.GetNextEdit().ShouldBeNull();
		}
	}
}
