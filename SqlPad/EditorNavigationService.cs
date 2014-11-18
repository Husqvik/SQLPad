using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad
{
	public static class EditorNavigationService
	{
		private static readonly List<DocumentCursorPosition> DocumentCursorPositions = new List<DocumentCursorPosition>();
		private static string _lastDocumentIdentifier;
		private static int _lastCursorPosition = -1;

		private static int _currentIndex;

		public static bool IsEnabled { get; set; }

		public static void Initialize(WorkDocument initialDocument = null)
		{
			DocumentCursorPositions.Clear();
			_lastDocumentIdentifier = null;
			_lastCursorPosition = -1;

			_currentIndex = 0;

			IsEnabled = true;

			if (initialDocument != null)
			{
				RegisterDocumentCursorPosition(initialDocument, initialDocument.CursorPosition);
			}
		}

		public static void RegisterDocumentCursorPosition(WorkDocument workDocument, int cursorPosition)
		{
			if (!IsEnabled)
			{
				return;
			}

			var isCursorAtAdjacentPosition = Math.Abs(cursorPosition - _lastCursorPosition) <= 1 && workDocument.Identifier == _lastDocumentIdentifier;

			_lastDocumentIdentifier = workDocument.Identifier;
			_lastCursorPosition = cursorPosition;

			if (isCursorAtAdjacentPosition)
			{
				return;
			}

			if (DocumentCursorPositions.Count > _currentIndex + 1)
			{
				var nextDocument = DocumentCursorPositions[_currentIndex + 1];
				if (workDocument.Identifier == nextDocument.Document.Identifier && nextDocument.CursorPosition == cursorPosition)
				{
					_currentIndex++;
					return;
				}

				DocumentCursorPositions.RemoveRange(_currentIndex + 1, DocumentCursorPositions.Count - _currentIndex - 1);
			}

			_currentIndex = DocumentCursorPositions.Count;

			DocumentCursorPositions.Add(
				new DocumentCursorPosition
				{
					CursorPosition = cursorPosition,
					Document = workDocument
				});
		}

		public static DocumentCursorPosition GetNextEdit()
		{
			return _currentIndex >= DocumentCursorPositions.Count - 1 ? null : DocumentCursorPositions[++_currentIndex];
		}

		public static DocumentCursorPosition GetPreviousEdit()
		{
			var effectiveIndex = _currentIndex <= 0 ? 0 : --_currentIndex;
			return DocumentCursorPositions[effectiveIndex];
		}
	}

	[DebuggerDisplay("DocumentCursorPosition (DocumentIdentifier={Document.Identifier}, CursorPosition={CursorPosition})")]
	public class DocumentCursorPosition
	{
		public WorkDocument Document { get; set; }

		public int CursorPosition { get; set; }
	}
}
