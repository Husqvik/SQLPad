using System.Diagnostics;
using NUnit.Framework;

namespace SqlPad.Oracle.Test
{
	[SetUpFixture]
	public class GlobalTestFixture
	{
		[OneTimeSetUp]
		public void SetUp()
		{
			Trace.Listeners.Add(new ConsoleTraceListener());
		}
	}
}