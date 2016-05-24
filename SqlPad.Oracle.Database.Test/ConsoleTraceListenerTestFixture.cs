using System.Diagnostics;
using NUnit.Framework;

namespace SqlPad.Oracle.Database.Test
{
	[SetUpFixture]
	public class ConsoleTraceListenerTestFixture
	{
		[OneTimeSetUp]
		public void SetUp()
		{
			Trace.Listeners.Add(new ConsoleTraceListener());
		}
	}
}