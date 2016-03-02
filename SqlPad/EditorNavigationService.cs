using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace SqlPad
{
	public static class EditorNavigationService
	{
		private const int MaximumClipboardEntries = 32;

		private static readonly List<DocumentCursorPosition> DocumentCursorPositions = new List<DocumentCursorPosition>();
		private static readonly List<string> ClipboardHistoryEntries = new List<string>(MaximumClipboardEntries);
		private static Guid? _lastDocumentIdentifier;
		private static int _lastCursorPosition = -1;

		private static int _currentIndex;

		public static bool IsEnabled { get; set; }

		public static IReadOnlyList<string> ClipboardHistory { get; } = ClipboardHistoryEntries.AsReadOnly();

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

		public static void RegisterClipboardEntry()
		{
			if (!IsEnabled || !Clipboard.ContainsText())
			{
				return;
			}

			string text;
			if (!ClipboardManager.TryGetClipboardText(out text))
			{
				return;
			}

			ClipboardHistoryEntries.Remove(text);

			if (ClipboardHistoryEntries.Count == MaximumClipboardEntries)
			{
				ClipboardHistoryEntries.RemoveAt(MaximumClipboardEntries - 1);
			}

			ClipboardHistoryEntries.Insert(0, text);
		}

		public static void RegisterDocumentCursorPosition(WorkDocument workDocument, int cursorPosition)
		{
			if (!IsEnabled)
			{
				return;
			}

			var isCursorAtAdjacentPosition = Math.Abs(cursorPosition - _lastCursorPosition) <= 1 && workDocument.DocumentId == _lastDocumentIdentifier;

			_lastDocumentIdentifier = workDocument.DocumentId;
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

		public static DocumentCursorPosition GetPreviousDocumentEdit()
		{
			var currentDocument = DocumentCursorPositions[_currentIndex];
			for (var i = DocumentCursorPositions.Count - 2; i >= 0; i--)
			{
				var precedingDocument = DocumentCursorPositions[i];
				if (precedingDocument.Document != currentDocument.Document)
				{
					return precedingDocument;
				}
			}

			return null;
		}
	}

	[DebuggerDisplay("DocumentCursorPosition (DocumentIdentifier={Document.Identifier}, CursorPosition={CursorPosition})")]
	public class DocumentCursorPosition
	{
		public WorkDocument Document { get; set; }

		public int CursorPosition { get; set; }
	}
}
