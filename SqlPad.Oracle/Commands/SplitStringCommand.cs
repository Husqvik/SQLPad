using System;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class SplitStringCommand : OracleCommandBase
	{
		public const string Title = "Split string";

		private const char SingleQuoteCharacter = '\'';

		private int _trimIndex;
		private bool _isQuotedString;
		private int _positionInString;

		private string LiteralValue => CurrentNode.Token.Value;

		private SplitStringCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			var isAtStringLiteral = CurrentNode != null && String.Equals(CurrentNode.Id, Terminals.StringLiteral);
			if (!isAtStringLiteral)
			{
				return false;
			}

			_trimIndex = OracleExtensions.GetTrimIndex(LiteralValue, out _isQuotedString);
			var endOffset = _isQuotedString ? 1 : 0;
			_positionInString = ExecutionContext.CaretOffset - CurrentNode.SourcePosition.IndexStart;

			if (IsAfterOddApostrophe())
			{
				_positionInString++;
			}

			return _positionInString >= _trimIndex && _positionInString < LiteralValue.Length - endOffset;
		}

		private bool IsAfterOddApostrophe()
		{
			if (_isQuotedString || LiteralValue[_positionInString] != SingleQuoteCharacter)
			{
				return false;
			}

			return LiteralValue.Substring(0, _positionInString)
				.Reverse()
				.TakeWhile(c => c == SingleQuoteCharacter)
				.Count() % 2 == 1;
		}

		protected override void Execute()
		{
			var stringInitializer = _isQuotedString ? LiteralValue.Substring(0, _trimIndex) : "'";
			var stringFinalizer = _isQuotedString ? LiteralValue.Substring(LiteralValue.Length - 2, 2) : "'";

			var firstPart = LiteralValue.Substring(0, _positionInString);
			var secondPart = LiteralValue.Substring(_positionInString);

			ExecutionContext.SegmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = CurrentNode.SourcePosition.IndexStart,
					Length = CurrentNode.SourcePosition.Length,
					Text = $"{firstPart}{stringFinalizer} ||  || {stringInitializer}{secondPart}"
				});

			ExecutionContext.CaretOffset = CurrentNode.SourcePosition.IndexStart + firstPart.Length + stringFinalizer.Length + 4;
		}
	}
}
