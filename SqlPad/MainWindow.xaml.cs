using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SqlPad.FindReplace;

namespace SqlPad
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		private readonly FindReplaceManager _findReplaceManager;
		private readonly List<TextEditorAdapter> _editorAdapters = new List<TextEditorAdapter>();

		private readonly Timer _timerWorkingDocumentSave = new Timer(60000);

		public MainWindow()
		{
			InitializeComponent();

			if (!EnsureValidConfiguration())
				return;

			_findReplaceManager = (FindReplaceManager)Resources["FindReplaceManager"];
			_findReplaceManager.OwnerWindow = this;
			_findReplaceManager.Editors = _editorAdapters;

			_timerWorkingDocumentSave.Elapsed += (sender, args) => Dispatcher.Invoke(SaveActiveWorkingDocument);
		}

		private void SaveActiveWorkingDocument()
		{
			if (ActiveDocument == null)
				return;

			ActiveDocument.SaveWorkingDocument();
		}

		private static bool EnsureValidConfiguration()
		{
			try
			{
				if (ConfigurationProvider.ConnectionStrings.Count == 0)
				{
					ShowStartingErrorMessage();
					return false;
				}
			}
			catch (Exception)
			{
				ShowStartingErrorMessage();
				return false;
			}
			
			return true;
		}

		private static void ShowStartingErrorMessage()
		{
			Messages.ShowError("At least one connection string and infrastructure factory must be defined", "Configuration Error");
		}

		internal DocumentPage ActiveDocument
		{
			get { return ((TabItem)DocumentTabControl.SelectedItem).Content as DocumentPage; }
		}

		internal IEnumerable<DocumentPage> AllDocuments
		{
			get
			{
				return DocumentTabControl.Items
					.Cast<TabItem>()
					.Select(t => t.Content)
					.OfType<DocumentPage>();
			}
		}

		private void WindowLoadedHandler(object sender, RoutedEventArgs e)
		{
			SqlPad.Resources.Initialize(Resources);

			if (WorkingDocumentCollection.WorkingDocuments.Count > 0)
			{
				foreach (var workingDocument in WorkingDocumentCollection.WorkingDocuments)
				{
					CreateNewDocumentPage(workingDocument);
				}
			}
			else
			{
				CreateNewDocumentPage();
			}

			DocumentTabControl.SelectionChanged += TabControlSelectionChangedHandler;

			DocumentTabControl.SelectedIndex = WorkingDocumentCollection.ActiveDocumentIndex;
		}

		private void DropObjectHandler(object sender, DragEventArgs e)
		{
			if (!e.Data.GetDataPresent(DataFormats.FileDrop))
				return;

			var files = (string[])e.Data.GetData(DataFormats.FileDrop);
			foreach (var file in files)
			{
				var fileInfo = new FileInfo(file);
				if (!fileInfo.Exists)
					continue;

				OpenExistingFile(fileInfo.FullName);
			}
		}

		private void TabControlSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
		{
			var document = DocumentTabControl.SelectedContent as DocumentPage;
			if (document != null)
			{
				_findReplaceManager.CurrentEditor = document.EditorAdapter;
			}

			WorkingDocumentCollection.ActiveDocumentIndex = DocumentTabControl.SelectedIndex;

			if (!e.AddedItems.Contains(NewTabItem))
				return;

			CreateNewDocumentPage();
		}

		private void CreateNewDocumentPage(WorkingDocument workingDocument = null)
		{
			var newDocumentPage = new DocumentPage(workingDocument);
			
			_editorAdapters.Add(newDocumentPage.EditorAdapter);

			AddDocumentTabItemContextMenuCommandBindings(newDocumentPage);

			DocumentTabControl.Items.Insert(DocumentTabControl.Items.Count - 1, newDocumentPage.TabItem);
			DocumentTabControl.SelectedItem = newDocumentPage.TabItem;

			_findReplaceManager.CurrentEditor = newDocumentPage.EditorAdapter;
		}

		private void AddDocumentTabItemContextMenuCommandBindings(DocumentPage documentPage)
		{
			documentPage.TabItemContextMenu.CommandBindings.Add(new CommandBinding(DocumentPageCommands.CloseDocumentCommand, CloseTabExecutedHandler, (sender, args) => args.CanExecute = true));
			documentPage.TabItemContextMenu.CommandBindings.Add(new CommandBinding(DocumentPageCommands.CloseAllDocumentsButThisCommand, CloseAllButThisTabExecutedHandler, (sender, args) => args.CanExecute = DocumentTabControl.Items.Count > 2));
		}

		private void CloseAllButThisTabExecutedHandler(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var currentDocument = (DocumentPage)executedRoutedEventArgs.Parameter;
			var allDocuments = DocumentTabControl.Items.Cast<TabItem>().Select(ti => ti.Content).OfType<DocumentPage>();
			var documentsToClose = allDocuments.Where(p => !p.Equals(currentDocument)).ToArray();
			foreach (var page in documentsToClose)
			{
				CloseDocument(page);
			}
		}

		private void CloseTabExecutedHandler(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var currentDocument = (DocumentPage)executedRoutedEventArgs.Parameter;
			CloseDocument(currentDocument);
		}

		private void CloseDocument(DocumentPage document)
		{
			SqlPadConfiguration.StoreConfiguration();
			DocumentTabControl.SelectedItem = document.TabItem;

			if (document.IsDirty && !ConfirmDocumentSave(document))
				return;

			SelectNewTabItem();
			DocumentTabControl.Items.Remove(document.TabItem);

			WorkingDocumentCollection.CloseDocument(document.WorkingDocument);

			document.Dispose();
		}

		private bool ConfirmDocumentSave(DocumentPage document)
		{
			var message = document.WorkingDocument.File == null
				? "Do you want to save the document?"
				: String.Format("Do you want to save changes in '{0}'?", document.WorkingDocument.File.FullName);
			
			var dialogResult = MessageBox.Show(message, "Confirmation", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);
			switch (dialogResult)
			{
				case MessageBoxResult.Yes:
					return document.Save();
				case MessageBoxResult.No:
					return true;
				case MessageBoxResult.Cancel:
					return false;
				default:
					throw new NotSupportedException(String.Format("'{0}' result is not supported. ", dialogResult));
			}
		}

		private void SelectNewTabItem()
		{
			if (DocumentTabControl.SelectedIndex > 0)
			{
				DocumentTabControl.SelectedIndex--;
			}
			else if (DocumentTabControl.Items.Count > 2)
			{
				DocumentTabControl.SelectedIndex++;
			}
			else
			{
				CreateNewDocumentPage();
			}
		}

		private void WindowClosingHandler(object sender, CancelEventArgs e)
		{
			_timerWorkingDocumentSave.Stop();

			foreach (var document in AllDocuments)
			{
				document.SaveWorkingDocument();
			}

			WorkingDocumentCollection.Save();
			
			DocumentTabControl.Items.Clear();
		}

		private void WindowClosedHandler(object sender, EventArgs e)
		{
			_timerWorkingDocumentSave.Dispose();
		}

		private void OpenFileHandler(object sender, ExecutedRoutedEventArgs e)
		{
			var dialog = new OpenFileDialog { Filter = DocumentPage.FileMaskDefault, CheckFileExists = true, Multiselect = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			foreach (var fileName in dialog.FileNames)
			{
				OpenExistingFile(fileName);
			}
		}

		private void OpenExistingFile(string fileName)
		{
			WorkingDocument workingDocument;
			if (WorkingDocumentCollection.TryGetWorkingDocumentFor(fileName, out workingDocument))
			{
				var documentForFile = AllDocuments.First(d => d.WorkingDocument == workingDocument);
				DocumentTabControl.SelectedItem = documentForFile.TabItem;
			}
			else
			{
				workingDocument = new WorkingDocument { DocumentFileName = fileName };

				WorkingDocumentCollection.AddDocument(workingDocument);
				CreateNewDocumentPage(workingDocument);
			}
		}
	}
}
