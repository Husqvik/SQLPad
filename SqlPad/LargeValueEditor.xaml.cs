using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using MoonPdfLib.MuPdf;
using SqlPad.FindReplace;

namespace SqlPad
{
	public partial class LargeValueEditor
	{
		private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
		private readonly FindReplaceManager _findReplaceManager;
		private ILargeBinaryValue _largeBinaryValue;

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
			_largeBinaryValue = largeValue as ILargeBinaryValue;
			var searchEditor = HexEditor;
			if (largeTextValue != null)
			{
				TabText.Visibility = Visibility.Visible;
				TabControl.SelectedItem = TabText;
				
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
			else if (_largeBinaryValue != null)
			{
				TabRaw.Visibility = Visibility.Visible;
				TabControl.SelectedItem = TabRaw;

				try
				{
					HexEditor.Text = await BinaryDataHelper.FormatBinaryDataAsync(_largeBinaryValue.Value, _cancellationTokenSource.Token);
					LoadingNotification.Visibility = Visibility.Collapsed;
				}
				catch (TaskCanceledException)
				{
					Close();
				}
			}

			_findReplaceManager.CurrentEditor = new TextEditorAdapter(searchEditor);
		}

		private bool TryOpenPdf()
		{
			if (!HasPdfHeader())
				return false;

			String password = null;

			do
			{
				try
				{
					PdfViewer.Open(new MemorySource(_largeBinaryValue.Value), password);
					TabPdf.Visibility = Visibility.Visible;
					TabControl.SelectedItem = TabPdf;
					return true;
				}
				catch (MissingOrInvalidPdfPasswordException)
				{
					if (password != null)
					{
						Messages.ShowError("Invalid password");
					}

					var passwordDialog = new PasswordDialog("PDF password: ");
					if (passwordDialog.ShowDialog() != true)
					{
						return true;
					}

					password = passwordDialog.Password;
				}
				catch
				{
					break;
				}
			}
			while (true);

			return false;
		}

		private bool HasPdfHeader()
		{
			return _largeBinaryValue.Value.Length >= 5 && _largeBinaryValue.Value[0] == '%' && _largeBinaryValue.Value[1] == 'P' && _largeBinaryValue.Value[2] == 'D' && _largeBinaryValue.Value[3] == 'F' && _largeBinaryValue.Value[4] == '-';
		}

		private void CloseWindowHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Close();
		}

		private void CancelLoadingHandler(object sender, RoutedEventArgs e)
		{
			_cancellationTokenSource.Cancel();
		}

		private void LargeValueEditorLoadedHandler(object sender, RoutedEventArgs e)
		{
			if (_largeBinaryValue == null || _largeBinaryValue.Value.Length == 0)
				return;

			var isPdf = TryOpenPdf();
			if (!isPdf)
			{
				TryOpenImage();
			}
		}

		private void TryOpenImage()
		{
			var bitmapImage = new BitmapImage { CacheOption = BitmapCacheOption.OnLoad, CreateOptions = BitmapCreateOptions.PreservePixelFormat };
			bitmapImage.BeginInit();

			try
			{
				var stream = new MemoryStream(_largeBinaryValue.Value);
				bitmapImage.StreamSource = stream;
				bitmapImage.EndInit();
				bitmapImage.Freeze();

				ImageViewer.Source = bitmapImage;
				TabImage.Visibility = Visibility.Visible;
				TabControl.SelectedItem = TabImage;
			}
			catch {}
		}

		private void NextPdfPageHandler(object sender, ExecutedRoutedEventArgs e)
		{
			ChangePdfPage(() => PdfViewer.GotoNextPage());
		}

		private void PreviousPdfPageHandler(object sender, ExecutedRoutedEventArgs e)
		{
			ChangePdfPage(() => PdfViewer.GotoPreviousPage());
		}

		private void ChangePdfPage(Action action)
		{
			if (_largeBinaryValue == null)
				return;

			action();
		}

		private void RefreshPdfHandler(object sender, ExecutedRoutedEventArgs e)
		{
			ChangePdfPage(() => PdfViewer.ZoomToHeight());
		}
	}
}
