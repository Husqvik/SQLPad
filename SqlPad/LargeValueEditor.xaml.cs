using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using SqlPad.FindReplace;

namespace SqlPad
{
	public partial class LargeValueEditor
	{
		private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
		private readonly FindReplaceManager _findReplaceManager;

		public LargeValueEditor(string columnName, ILargeValue largeValue)
		{
			InitializeComponent();

			HexEditor.TextArea.TextView.ElementGenerators.Clear();

			_findReplaceManager = (FindReplaceManager)Resources["FindReplaceManager"];
			_findReplaceManager.OwnerWindow = this;

			Title = columnName;

			SetEditorValue(largeValue);
		}

		private async void SetEditorValue(ILargeValue largeValue)
		{
			var largeTextValue = largeValue as ILargeTextValue;
			var largeBinaryValue = largeValue as ILargeBinaryValue;
			var searchEditor = HexEditor;
			if (largeTextValue != null)
			{
				TabRaw.IsEnabled = false;
				var isXml = true;
				using (var reader = new XmlTextReader(new StringReader(largeTextValue.Value)))
				{
					try
					{
						while (reader.Read())
						{
						}
					}
					catch
					{
						isXml = false;
					}
				}

				if (isXml)
				{
					TextEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
				}

				TextEditor.Text = largeTextValue.Value;

				searchEditor = TextEditor;
			}
			else if (largeBinaryValue != null)
			{
				TabControl.SelectedItem = TabRaw;
				TabText.IsEnabled = false;

				try
				{
					HexEditor.Text = await BinaryDataHelper.FormatBinaryDataAsync(largeBinaryValue.Value, _cancellationTokenSource.Token);
					LoadingNotification.Visibility = Visibility.Collapsed;
				}
				catch (TaskCanceledException)
				{
					Close();
				}
			}

			_findReplaceManager.CurrentEditor = new TextEditorAdapter(searchEditor);
		}

		private void CloseWindowHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Close();
		}

		private void CancelLoadingHandler(object sender, RoutedEventArgs e)
		{
			_cancellationTokenSource.Cancel();
		}
	}
}
