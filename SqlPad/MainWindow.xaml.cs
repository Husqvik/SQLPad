using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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

		public MainWindow()
		{
			InitializeComponent();

			if (!EnsureValidConfiguration())
				return;

			_findReplaceManager = (FindReplaceManager)Resources["FindReplaceManager"];
			_findReplaceManager.OwnerWindow = this;
			_findReplaceManager.Editors = _editorAdapters;
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

		internal DocumentPage CurrentPage
		{
			get { return ((TabItem)DocumentTabControl.SelectedItem).Content as DocumentPage; }
		}

		internal IEnumerable<DocumentPage> AllPages
		{
			get { return DocumentTabControl.Items.Cast<TabItem>().Take(DocumentTabControl.Items.Count - 1).Select(t => (DocumentPage)t.Content); }
		}

		private void WindowLoadedHandler(object sender, RoutedEventArgs e)
		{
			SqlPad.Resources.Initialize(Resources);

			var filesToRecover = App.GetRecoverableDocuments().Select(f => new FileInfo(f)).Where(f => f.Exists).ToList();
			if (filesToRecover.Count == 0)
			{
				filesToRecover.Add(null);
			}

			foreach (var fileInfo in filesToRecover)
			{
				CreateNewDocumentPage(fileInfo, fileInfo != null);
			}

			App.PurgeRecoveryFiles();

			DocumentTabControl.SelectionChanged += TabControlSelectionChangedHandler;
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

				CreateNewDocumentPage(fileInfo);
			}
		}

		private void TabControlSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
		{
			var document = DocumentTabControl.SelectedContent as DocumentPage;
			if (document != null)
			{
				_findReplaceManager.CurrentEditor = document.EditorAdapter;
			}

			if (!e.AddedItems.Contains(NewTabItem))
				return;

			CreateNewDocumentPage();
		}

		private void CreateNewDocumentPage(FileInfo file = null, bool recoveryMode = false)
		{
			var newDocumentPage = new DocumentPage(file, recoveryMode);
			
			_editorAdapters.Add(newDocumentPage.EditorAdapter);

			AddDocumentTabItemContextMenuCommandBindinga(newDocumentPage);

			DocumentTabControl.Items.Insert(DocumentTabControl.Items.Count - 1, newDocumentPage.TabItem);
			DocumentTabControl.SelectedItem = newDocumentPage.TabItem;

			_findReplaceManager.CurrentEditor = newDocumentPage.EditorAdapter;
		}

		private void AddDocumentTabItemContextMenuCommandBindinga(DocumentPage documentPage)
		{
			documentPage.TabItemContextMenu.CommandBindings.Add(new CommandBinding(DocumentPageCommands.CloseDocumentCommand, CloseTabExecutedHandler, (sender, args) => args.CanExecute = true));
			documentPage.TabItemContextMenu.CommandBindings.Add(new CommandBinding(DocumentPageCommands.CloseAllDocumentsButThisCommand, CloseAllButThisTabExecutedHandler, (sender, args) => args.CanExecute = DocumentTabControl.Items.Count > 2));
		}

		private void CloseAllButThisTabExecutedHandler(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var allDocuments = DocumentTabControl.Items.Cast<TabItem>().Select(ti => ti.Content).OfType<DocumentPage>();
			var documentsToClose = allDocuments.Where(p => !p.Equals(CurrentPage)).ToArray();
			foreach (var page in documentsToClose)
			{
				ClosePage(page);
			}
		}

		private void CloseTabExecutedHandler(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var document = (DocumentPage)((TabItem)DocumentTabControl.SelectedItem).Content;
			ClosePage(document);
		}

		private bool ClosePage(DocumentPage document)
		{
			DocumentTabControl.SelectedItem = document.TabItem;

			if (document.IsDirty && !ConfirmPageSave(document))
				return false;

			SelectNewTabItem();
			DocumentTabControl.Items.Remove(document.TabItem);
			return true;
		}

		private bool ConfirmPageSave(DocumentPage document)
		{
			var message = document.File == null
				? "Do you want to save the document?"
				: String.Format("Do you want to save changes in '{0}'?", document.File.FullName);
			
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
			var pages = DocumentTabControl.Items
				.Cast<TabItem>()
				.Select(t => t.Content)
				.OfType<DocumentPage>()
				.ToArray();
			
			foreach (var page in pages)
			{
				if (!ClosePage(page))
				{
					e.Cancel = true;
					return;
				}
				
				page.Dispose();
			}

			DocumentTabControl.Items.Clear();
		}

		private void WindowClosedHandler(object sender, EventArgs e)
		{
			
		}

		private void OpenFileHandler(object sender, ExecutedRoutedEventArgs e)
		{
			var dialog = new OpenFileDialog { Filter = DocumentPage.FileMaskDefault, CheckFileExists = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			var file = new FileInfo(dialog.FileName);
			CreateNewDocumentPage(file);
		}
	}
}
