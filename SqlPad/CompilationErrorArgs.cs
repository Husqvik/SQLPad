using System;

namespace SqlPad
{
	public class CompilationErrorArgs : EventArgs
	{
		public CompilationError CompilationError { get; private set; }

		public CompilationErrorArgs(CompilationError compilationError)
		{
			CompilationError = compilationError ?? throw new ArgumentNullException(nameof(compilationError));
		}
	}
}