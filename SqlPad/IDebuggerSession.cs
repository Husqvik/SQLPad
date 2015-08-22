using System.Threading;
using System.Threading.Tasks;

namespace SqlPad
{
	public interface IDebuggerSession
	{
		int? ActiveLine { get; }

		Task Start(CancellationToken cancellationToken);

		Task Continue(CancellationToken cancellationToken);

		Task StepNextLine(CancellationToken cancellationToken);

		Task StepInto(CancellationToken cancellationToken);

		Task StepOut(CancellationToken cancellationToken);

		Task Detach(CancellationToken cancellationToken);
	}
}
