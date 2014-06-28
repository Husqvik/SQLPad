using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace SqlPad.Test
{
	[TestFixture]
	public class DocumentPageTest
	{
		[Test(Description = @""), STAThread]
		public void TestDocumentPageInitialization()
		{
			var page = new DocumentPage(ConfigurationProvider.InfrastructureFactory, null) { IsParsingSynchronous = true };
			page.Editor.Load(new MemoryStream(Encoding.ASCII.GetBytes("SELECT * FROM DUAL")));
		}
	}
}
