using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SqlPad.Test
{
	public static class VisualTestRunner
	{
		private const string DomainParameterTestMethodName = "TestMethodName";
		private const string DomainParameterTestClassName = "TestClassName";

		public static void RunTest(string testClassName, string testMethodName)
		{
			var appDomain = AppDomain.CreateDomain("TestDomain", AppDomain.CurrentDomain.Evidence, AppDomain.CurrentDomain.SetupInformation);
			appDomain.SetData(DomainParameterTestClassName, testClassName);
			appDomain.SetData(DomainParameterTestMethodName, testMethodName);
			appDomain.DoCallBack(RunSqlPad);
			AppDomain.Unload(appDomain);
		}

		public static void Wait(double seconds)
		{
			var frame = new DispatcherFrame();
			Task.Factory.StartNew(
				() =>
				{
					Thread.Sleep(TimeSpan.FromSeconds(seconds));
					frame.Continue = false;
				});

			Dispatcher.PushFrame(frame);
		}

		private static void RunSqlPad()
		{
			var app = new App();
			app.InitializeComponent();

			var tempDirectoryName = TestFixture.SetupTestDirectory();
			ConfigurationProvider.SetUserDataFolder(tempDirectoryName);

			var sqlPadDirectory = new Uri(Path.GetDirectoryName(typeof(Snippets).Assembly.CodeBase)).LocalPath;
			ConfigurationProvider.SetSnippetsFolder(Path.Combine(sqlPadDirectory, Snippets.SnippetDirectoryName));
			ConfigurationProvider.SetCodeGenerationItemFolder(Path.Combine(sqlPadDirectory, Snippets.CodeGenerationItemDirectoryName));
			DocumentPage.IsParsingSynchronous = true;

			var testClassName = (string)AppDomain.CurrentDomain.GetData(DomainParameterTestClassName);
			var testClassType = Type.GetType(testClassName);
			if (testClassType == null)
			{
				throw new ArgumentException($"Test class '{testClassName}' was not found. ");
			}

			var testMethodName = (string)AppDomain.CurrentDomain.GetData(DomainParameterTestMethodName);
			var testMethodInfo = testClassType.GetMethod(testMethodName, BindingFlags.Static | BindingFlags.NonPublic);
			if (testMethodInfo == null)
			{
				throw new ArgumentException($"Test method '{testMethodName}' was not found. ");
			}

			var testContext =
				new VisualTestContext
				{
					Application = app,
					TestTemporaryDirectory = tempDirectoryName,
					TestMethodInfo = testMethodInfo
				};

			app.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action<VisualTestContext>(TestMethodWrapper), testContext);
			app.Run();

			Directory.Delete(tempDirectoryName, true);
		}

		private static void TestMethodWrapper(VisualTestContext context)
		{
			try
			{
				context.TestMethodInfo.Invoke(null, new object[] { context });
			}
			catch (Exception e)
			{
				throw new ApplicationException(e.ToString());
			}
			finally
			{
				context.Application.Shutdown();
			}
		}
	}

	public class VisualTestContext
	{
		public string TestTemporaryDirectory { get; set; }

		public App Application { get; set; }

		public MethodInfo TestMethodInfo { get; set; }
	}
}
