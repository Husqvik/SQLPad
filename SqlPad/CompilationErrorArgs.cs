using System;

namespace SqlPad
{
	public class CompilationErrorArgs : EventArgs
	{
		public CompilationError CompilationError { get; private set; }

		public CompilationErrorArgs(CompilationError compilationError)
		{
			if (compilationError == null)
			{
				throw new ArgumentNullException(nameof(compilationError));
			}
			
			CompilationError = compilationError;
		}
	}
}