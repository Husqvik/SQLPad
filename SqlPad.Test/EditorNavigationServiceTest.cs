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

		[Test(Description = @""), STAThread]
		public void TestInitializedEditorNavigationService()
		{
			EditorNavigationService.IsEnabled.ShouldBe(true);
			EditorNavigationService.GetNextEdit().ShouldBe(null);
			Assert.Throws<ArgumentOutOfRangeException>(() => EditorNavigationService.GetPreviousEdit());
		}

		[Test(Description = @""), STAThread]
		public void TestRegisterDocumentCursorPosition()
		{
			var workingDocument = new WorkingDocument();
			const int originalPosition = 10;
			const int position2 = 99;
			const int lastPosition = 43;
			EditorNavigationService.RegisterDocumentCursorPosition(workingDocument, originalPosition);
			EditorNavigationService.RegisterDocumentCursorPosition(workingDocument, position2);
			EditorNavigationService.RegisterDocumentCursorPosition(workingDocument, lastPosition);
			EditorNavigationService.GetNextEdit().ShouldBe(null);
			var previousEdit = EditorNavigationService.GetPreviousEdit();
			previousEdit.ShouldNotBe(null);
			previousEdit.Document.ShouldBe(workingDocument);
			previousEdit.CursorPosition.ShouldBe(position2);
			previousEdit = EditorNavigationService.GetPreviousEdit();
			previousEdit.ShouldNotBe(null);
			previousEdit.Document.ShouldBe(workingDocument);
			previousEdit.CursorPosition.ShouldBe(originalPosition);
			previousEdit = EditorNavigationService.GetPreviousEdit();
			previousEdit.ShouldNotBe(null);
			previousEdit.Document.ShouldBe(workingDocument);
			previousEdit.CursorPosition.ShouldBe(originalPosition);

			var nextEdit = EditorNavigationService.GetNextEdit();
			nextEdit.ShouldNotBe(null);
			nextEdit.Document.ShouldBe(workingDocument);
			nextEdit.CursorPosition.ShouldBe(position2);
			nextEdit = EditorNavigationService.GetNextEdit();
			nextEdit.ShouldNotBe(null);
			nextEdit.Document.ShouldBe(workingDocument);
			nextEdit.CursorPosition.ShouldBe(lastPosition);
			EditorNavigationService.GetNextEdit().ShouldBe(null);
		}
	}
}
