namespace SqlPad
{
	public abstract class StatementNode
	{
		private bool _isSourcePositionBuilt;
		private SourcePosition _sourcePosition;

		protected StatementNode(StatementBase statement, IToken token)
		{
			Statement = statement;
			Token = token;
		}

		public StatementBase Statement { get; private set; }

		public IToken Token { get; private set; }

		public SourcePosition SourcePosition
		{
			get
			{
				if (_isSourcePositionBuilt)
					return _sourcePosition;

				_sourcePosition = BuildSourcePosition();
				_isSourcePositionBuilt = true;
				return _sourcePosition;
			}
		}

		protected abstract SourcePosition BuildSourcePosition();
	}
}
