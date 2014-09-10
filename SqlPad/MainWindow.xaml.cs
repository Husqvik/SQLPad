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
using SqlPad.Commands;
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

			_timerWorkingDocumentSave.Elapsed += (sender, args) => Dispatcher.Invoke(SaveWorkingDocuments);
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

			CommandBindings.Add(new CommandBinding(GenericCommands.SaveAllCommand, SaveAllCommandExecutedHandler));

			WorkingDocumentCollection.RestoreApplicationWindowProperties(this);

			if (WorkingDocumentCollection.WorkingDocuments.Count > 0)
			{
				foreach (var workingDocument in WorkingDocumentCollection.WorkingDocuments.OrderBy(d => d.TabIndex))
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

			EditorNavigationService.Initialize(ActiveDocument.WorkingDocument);
		}

		private void SaveAllCommandExecutedHandler(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			foreach (var document in AllDocuments)
			{
				if (!document.Save())
				{
					return;
				}
			}
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
			var tabItem = e.AddedItems.Count == 0 ? null : e.AddedItems[0] as TabItem;
			var document = tabItem == null ? null : tabItem.Content as DocumentPage;

			if (document != null)
			{
				document.SaveWorkingDocument();
				_findReplaceManager.CurrentEditor = document.EditorAdapter;
				WorkingDocumentCollection.ActiveDocumentIndex = DocumentTabControl.SelectedIndex;
				EditorNavigationService.RegisterDocumentCursorPosition(document.WorkingDocument, document.Editor.CaretOffset);
			}

			if (!e.AddedItems.Contains(NewTabItem))
			{
				return;
			}

			CreateNewDocumentPage();
		}

		public DocumentPage CreateNewDocumentPage(WorkingDocument workingDocument = null)
		{
			var newDocumentPage = new DocumentPage(workingDocument);
			
			_editorAdapters.Add(newDocumentPage.EditorAdapter);

			AddDocumentTabItemContextMenuCommandBindings(newDocumentPage);

			DocumentTabControl.Items.Insert(DocumentTabControl.Items.Count - 1, newDocumentPage.TabItem);
			DocumentTabControl.SelectedItem = newDocumentPage.TabItem;

			if (workingDocument == null)
			{
				newDocumentPage.WorkingDocument.TabIndex = DocumentTabControl.TabIndex;
			}

			_findReplaceManager.CurrentEditor = newDocumentPage.EditorAdapter;

			return newDocumentPage;
		}

		private void AddDocumentTabItemContextMenuCommandBindings(DocumentPage documentPage)
		{
			documentPage.TabItemContextMenu.CommandBindings.Add(new CommandBinding(GenericCommands.CloseDocumentCommand, CloseTabExecutedHandler, (sender, args) => args.CanExecute = true));
			documentPage.TabItemContextMenu.CommandBindings.Add(new CommandBinding(GenericCommands.CloseAllDocumentsButThisCommand, CloseAllButThisTabExecutedHandler, (sender, args) => args.CanExecute = DocumentTabControl.Items.Count > 2));
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

			SaveWorkingDocuments();
			
			DocumentTabControl.Items.Clear();
		}

		private void SaveWorkingDocuments()
		{
			foreach (var document in AllDocuments)
			{
				document.SaveWorkingDocument();
			}

			SqlPadConfiguration.StoreConfiguration();
			WorkingDocumentCollection.SetApplicationWindowProperties(this);
			WorkingDocumentCollection.Save();
		}

		private void WindowClosedHandler(object sender, EventArgs e)
		{
			_timerWorkingDocumentSave.Dispose();
			ConfigurationProvider.Dispose();
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

		private DocumentPage OpenExistingFile(string fileName)
		{
			DocumentPage documentPage;
			WorkingDocument workingDocument;
			if (WorkingDocumentCollection.TryGetWorkingDocumentFor(fileName, out workingDocument))
			{
				documentPage = AllDocuments.First(d => d.WorkingDocument == workingDocument);
				DocumentTabControl.SelectedItem = documentPage.TabItem;
			}
			else
			{
				workingDocument = new WorkingDocument { DocumentFileName = fileName };

				WorkingDocumentCollection.AddDocument(workingDocument);
				documentPage = CreateNewDocumentPage(workingDocument);
			}

			return documentPage;
		}

		private void DocumentTabControlDropHandler(object sender, DragEventArgs e)
		{
			var draggedObject = e.Source as ContentControl;
			if (draggedObject == null)
			{
				return;
			}

			var tabItemTarget = draggedObject.Parent as TabItem;
			var tabItemDragged = (TabItem)e.Data.GetData(typeof(TabItem));
			if (tabItemTarget == null || tabItemDragged == null || NewTabItem.Equals(tabItemTarget) || Equals(tabItemTarget, tabItemDragged))
			{
				return;
			}

			var draggedDocumentPage = tabItemDragged.Content as DocumentPage;
			if (draggedDocumentPage == null)
			{
				return;
			}

			var indexFrom = DocumentTabControl.Items.IndexOf(tabItemDragged);
			var workingDocumentFrom = draggedDocumentPage.WorkingDocument;
			var indexTo = DocumentTabControl.Items.IndexOf(tabItemTarget);
			var workingDocumentTo = ((DocumentPage)tabItemTarget.Content).WorkingDocument;

			DocumentTabControl.SelectedIndex = 0;

			DocumentTabControl.Items.Remove(tabItemDragged);
			DocumentTabControl.Items.Insert(indexTo, tabItemDragged);
			workingDocumentFrom.TabIndex = indexTo;

			DocumentTabControl.Items.Remove(tabItemTarget);
			DocumentTabControl.Items.Insert(indexFrom, tabItemTarget);
			workingDocumentTo.TabIndex = indexFrom;

			DocumentTabControl.SelectedIndex = indexTo;
		}

		private void DocumentTabControlPreviewMouseMoveHandler(object sender, MouseEventArgs e)
		{
			var tabItem = e.Source as TabItem;
			if (tabItem == null || Equals(tabItem, NewTabItem))
				return;

			if (Mouse.PrimaryDevice.LeftButton == MouseButtonState.Pressed)
			{
				DragDrop.DoDragDrop(tabItem, tabItem, DragDropEffects.All);
			}	
		}

		private void GoToNextEditCommandExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			GoToEditCommand(EditorNavigationService.GetNextEdit());
		}

		private void GoToPreviousEditCommandExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			GoToEditCommand(EditorNavigationService.GetPreviousEdit());
		}

		private void GoToEditCommand(DocumentCursorPosition documentCursorPosition)
		{
			if (documentCursorPosition == null)
			{
				return;
			}

			var documentPage = AllDocuments.SingleOrDefault(d => d.WorkingDocument.Identifier == documentCursorPosition.Document.Identifier);
			if (documentPage == null)
			{
				if (documentCursorPosition.Document.File == null || !File.Exists(documentCursorPosition.Document.DocumentFileName))
				{
					return;
				}

				documentPage =  OpenExistingFile(documentCursorPosition.Document.DocumentFileName);
			}

			EditorNavigationService.IsEnabled = false;
			documentPage.Editor.CaretOffset = documentCursorPosition.CursorPosition;
			DocumentTabControl.SelectedItem = documentPage.TabItem;
			EditorNavigationService.IsEnabled = true;
		}
	}
}
