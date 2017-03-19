using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Win32;
using MoonPdfLib.MuPdf;
using Newtonsoft.Json.Linq;

namespace SqlPad
{
	public partial class LargeValueEditor
	{
		private static readonly string SyntaxHighlightingNameJavaScript;
		private static readonly IHighlightingDefinition XmlHighlightingDefinition = HighlightingManager.Instance.GetDefinition("XML");

		private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
		private readonly ILargeValue _largeValue;
		private readonly ILargeBinaryValue _largeBinaryValue;
		private bool _isXml;
		private bool _isJson;

		static LargeValueEditor()
		{
			using (var stream = typeof (LargeValueEditor).Assembly.GetManifestResourceStream("SqlPad.JavaScriptSyntaxHighlight.xshd"))
			{
				using (XmlReader reader = new XmlTextReader(stream))
				{
					var jsonHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
					SyntaxHighlightingNameJavaScript = jsonHighlighting.Name;
					HighlightingManager.Instance.RegisterHighlighting(SyntaxHighlightingNameJavaScript, new[] {"*.js", "*.json"}, jsonHighlighting);
				}
			}
		}

		public LargeValueEditor(string columnName, ILargeValue largeValue)
		{
			InitializeComponent();

			SetMaximumHeight();

			HexEditor.TextArea.TextView.ElementGenerators.Clear();

			Title = columnName;

			_largeValue = largeValue;
			_largeBinaryValue = _largeValue as ILargeBinaryValue;
		}

		private async Task SetEditorValue()
		{
			var largeTextValue = _largeValue as ILargeTextValue;
			var collectionValue = _largeValue as ICollectionValue;
			var complexType = _largeValue as IComplexType;

			try
			{
				if (largeTextValue != null)
				{
					TabText.Visibility = Visibility.Visible;
					TabText.IsSelected = true;

					_isXml = true;
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
							_isXml = false;
						}
					}

					_isJson = true;

					try
					{
						JToken.Parse(largeTextValue.Value);
					}
					catch (Exception)
					{
						_isJson = false;
					}

					TextEditor.Text = largeTextValue.Value;

					if (_isXml)
					{
						TextEditor.SyntaxHighlighting = XmlHighlightingDefinition;
					}
					else if (_isJson)
					{
						TextEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition(SyntaxHighlightingNameJavaScript);
					}

					TabRaw.Visibility = Visibility.Visible;
					await DisplayRawData(Encoding.UTF8.GetBytes(largeTextValue.Value));
				}
				else if (_largeBinaryValue != null)
				{
					TabRaw.Visibility = Visibility.Visible;
					TabRaw.IsSelected = true;

					await DisplayRawData(_largeBinaryValue.Value);
					ButtonSaveRawAs.Visibility = Visibility.Visible;
				}
				else if (collectionValue != null)
				{
					TabCollection.Visibility = Visibility.Visible;
					TabCollection.IsSelected = true;
					var columnTemplate = DataGridHelper.CreateDataGridTemplateColumn(collectionValue.ColumnHeader);
					CollectionViewer.Columns.Add(columnTemplate);
					CollectionViewer.ItemsSource = collectionValue.Records.Cast<object>().Select(r => new [] { r }).ToArray();
				}
				else if (complexType != null)
				{
					TabComplexType.Visibility = Visibility.Visible;
					TabComplexType.IsSelected = true;
					ComplexTypeViewer.ComplexType = complexType;
				}
			}
			catch (OperationCanceledException)
			{
				TraceLog.WriteLine("User has canceled large data editor load operation. ");
				Close();
			}
			catch (Exception e)
			{
				Messages.ShowError(e.Message, owner: this);
				Close();
			}
		}

		private async Task DisplayRawData(byte[] data)
		{
			HexEditor.Text = await BinaryDataHelper.FormatBinaryDataAsync(data, _cancellationTokenSource.Token);
			LoadingNotification.Visibility = Visibility.Collapsed;
		}

		private bool TryOpenPdf()
		{
			if (!HasPdfHeader())
			{
				return false;
			}

			String password = null;

			do
			{
				try
				{
					PdfViewer.Open(new MemorySource(_largeBinaryValue.Value), password);
					TabPdf.Visibility = Visibility.Visible;
					TabPdf.IsSelected = true;
					return true;
				}
				catch (MissingOrInvalidPdfPasswordException)
				{
					if (password != null)
					{
						Messages.ShowError("Invalid password");
					}

					var securePassword = PasswordDialog.AskForPassword("PDF password: ", this);
					if (securePassword == null)
					{
						return false;
					}

					password = securePassword.ToString();
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

		private async void LargeValueEditorLoadedHandler(object sender, RoutedEventArgs e)
		{
			await SetEditorValue();

			if (_largeBinaryValue == null || _largeBinaryValue.Value.Length == 0)
			{
				return;
			}

			if (TryOpenPdf())
			{
				return;
			}

			if (TryOpenImage())
			{
				return;
			}

			TryOpenMedia();
		}

		private void TryOpenMedia()
		{
			
		}

		private bool TryOpenImage()
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
				TabImage.IsSelected = true;
				return true;
			}
			catch
			{
				return false;
			}
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
			{
				return;
			}

			action();
		}

		private void RefreshPdfHandler(object sender, ExecutedRoutedEventArgs e)
		{
			ChangePdfPage(() => PdfViewer.ZoomToHeight());
		}

		private void WindowStateChanged(object sender, EventArgs e)
		{
			SetMaximumHeight();
		}

		private void SetMaximumHeight()
		{
			MaxHeight = WindowState == WindowState.Maximized ? Double.PositiveInfinity : SystemParameters.WorkArea.Height;
		}

		private void SaveTextAsClickHandler(object sender, RoutedEventArgs args)
		{
			var filterSettings = "Text files (*.txt)|*.txt|All files (*.*)|*";
			if (_isXml)
			{
				filterSettings = $"XML files (*.xml)|*.xml|{filterSettings}";
			}

			var dialog = new SaveFileDialog { Filter = filterSettings, OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			App.SafeActionWithUserError(
				() => File.WriteAllText(dialog.FileName, String.IsNullOrEmpty(TextEditor.SelectedText) ? TextEditor.Text : TextEditor.SelectedText));
		}

		private void SaveRawAsClickHandler(object sender, RoutedEventArgs args)
		{
			var dialog = new SaveFileDialog { Filter = "All files (*.*)|*", OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			App.SafeActionWithUserError(
				() => File.WriteAllBytes(dialog.FileName, _largeBinaryValue.Value));
		}

		private void CollectionViewerMouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
		{
			DataGridHelper.ShowLargeValueEditor(CollectionViewer);
		}
	}
}
