using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Win32;
using MoonPdfLib.MuPdf;
using SqlPad.FindReplace;

namespace SqlPad
{
	public partial class LargeValueEditor
	{
		private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
		private readonly FindReplaceManager _findReplaceManager;
		private readonly ILargeValue _largeValue;
		private readonly ILargeBinaryValue _largeBinaryValue;
		private bool _isXml;

		public LargeValueEditor(string columnName, ILargeValue largeValue)
		{
			InitializeComponent();

			SetMaximumHeight();

			HexEditor.TextArea.TextView.ElementGenerators.Clear();

			_findReplaceManager = (FindReplaceManager)Resources["FindReplaceManager"];
			_findReplaceManager.OwnerWindow = this;

			Title = columnName;

			_largeValue = largeValue;
			_largeBinaryValue = _largeValue as ILargeBinaryValue;
		}

		private async Task SetEditorValue()
		{
			var largeTextValue = _largeValue as ILargeTextValue;
			var collectionValue = _largeValue as ICollectionValue;
			var complexType = _largeValue as IComplexType;
			var searchEditor = HexEditor;

			try
			{
				if (largeTextValue != null)
				{
					TabText.Visibility = Visibility.Visible;
					TabControl.SelectedItem = TabText;

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

					TextEditor.Text = largeTextValue.Value;

					if (_isXml)
					{
						TextEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
					}

					searchEditor = TextEditor;
				}
				else if (_largeBinaryValue != null)
				{
					TabRaw.Visibility = Visibility.Visible;
					TabControl.SelectedItem = TabRaw;

					HexEditor.Text = await BinaryDataHelper.FormatBinaryDataAsync(_largeBinaryValue.Value, _cancellationTokenSource.Token);
					LoadingNotification.Visibility = Visibility.Collapsed;
					ButtonSaveRawAs.Visibility = Visibility.Visible;
				}
				else if (collectionValue != null)
				{
					TabCollection.Visibility = Visibility.Visible;
					TabControl.SelectedItem = TabCollection;
					var columnTemplate = DocumentPage.CreateDataGridTextColumnTemplate(collectionValue.ColumnHeader);
					CollectionViewer.Columns.Add(columnTemplate);
					CollectionViewer.ItemsSource = collectionValue.Records.Cast<object>().Select(r => new [] { r }).ToArray();
				}
				else if (complexType != null)
				{
					TabComplexType.Visibility = Visibility.Visible;
					TabControl.SelectedItem = TabComplexType;
					ComplexTypeViewer.ComplexType = complexType;
				}

				_findReplaceManager.CurrentEditor = new TextEditorAdapter(searchEditor);
			}
			catch (OperationCanceledException)
			{
				Trace.WriteLine("User has cancelled large data editor load operation. ");
				Close();
			}
			catch (Exception e)
			{
				Messages.ShowError(this, e.Message);
				Close();
			}
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

					var passwordDialog = new PasswordDialog("PDF password: ") { Owner = this };
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

		private async void LargeValueEditorLoadedHandler(object sender, RoutedEventArgs e)
		{
			await SetEditorValue();

			if (_largeBinaryValue == null || _largeBinaryValue.Value.Length == 0)
				return;

			if (TryOpenPdf())
				return;

			if (TryOpenImage())
				return;

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
				TabControl.SelectedItem = TabImage;
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
				return;

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
			var filterSettings = "Text Files (*.txt)|*.txt|All Files (*.*)|*";
			if (_isXml)
			{
				filterSettings = String.Format("XML Files (*.xml)|*.xml|{0}", filterSettings);
			}

			var dialog = new SaveFileDialog { Filter = filterSettings, OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			DocumentPage.SafeActionWithUserError(
				() => File.WriteAllText(dialog.FileName, String.IsNullOrEmpty(TextEditor.SelectedText) ? TextEditor.Text : TextEditor.SelectedText));
		}

		private void SaveRawAsClickHandler(object sender, RoutedEventArgs args)
		{
			var dialog = new SaveFileDialog { Filter = "All Files (*.*)|*", OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			DocumentPage.SafeActionWithUserError(
				() => File.WriteAllBytes(dialog.FileName, _largeBinaryValue.Value));
		}

		private void CollectionViewerMouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
		{
			DocumentPage.ShowLargeValueEditor(CollectionViewer);
		}

		private void WindowSizeChangedHandler(object sender, SizeChangedEventArgs e)
		{
			if (MinWidth < Width)
			{
				MinWidth = Width;
			}
		}
	}
}
