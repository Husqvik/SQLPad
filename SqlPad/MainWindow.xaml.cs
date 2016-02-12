using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using System.Windows.Threading;
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
		private const string TitleTemplate = "SQL Pad {0} ALPHA ({1})";

		private readonly List<TextEditorAdapter> _editorAdapters = new List<TextEditorAdapter>();

		private readonly WindowDatabaseMonitor _windowDatabaseMonitor;
		private readonly FindReplaceManager _findReplaceManager;
		private readonly ContextMenu _recentDocumentsMenu;
		private readonly DispatcherTimer _timerWorkingDocumentSave;

		public MainWindow()
		{
			if (!EnsureValidConfiguration())
			{
				Application.Current.Shutdown(Int32.MinValue);
				return;
			}

			InitializeComponent();

			Title = String.Format(TitleTemplate, App.Version, App.VersionTimestamp);

			_recentDocumentsMenu = (ContextMenu)Resources["RecentFileMenu"];
			_recentDocumentsMenu.PlacementTarget = this;
			_recentDocumentsMenu.Closed += RecentDocumentsMenuClosedHandler;

			_findReplaceManager = (FindReplaceManager)Application.Current.Resources["FindReplaceManager"];
			_findReplaceManager.OwnerWindow = this;
			_findReplaceManager.Editors = _editorAdapters;

			_timerWorkingDocumentSave = new DispatcherTimer(TimeSpan.FromMinutes(3), DispatcherPriority.Normal, delegate { SaveWorkingDocuments(); }, Dispatcher);

			Loaded += WindowLoadedHandler;
			Closing += WindowClosingHandler;
			Closed += WindowClosedHandler;

			_windowDatabaseMonitor = new WindowDatabaseMonitor();
		}

		private void RecentDocumentsMenuClosedHandler(object sender, RoutedEventArgs routedEventArgs)
		{
			_recentDocumentsMenu.ItemsSource = null;
			_recentDocumentsMenu.CommandBindings.Clear();
			_recentDocumentsMenu.InputBindings.Clear();
		}

		private static bool EnsureValidConfiguration()
		{
			try
			{
				if (ConfigurationProvider.ConnectionStrings.Count == 0)
				{
					Messages.ShowError("At least one connection string and infrastructure factory must be defined", "Configuration Error");
					return false;
				}
			}
			catch (Exception e)
			{
				Messages.ShowError(e.ToString());
				return false;
			}
			
			return true;
		}

		internal DocumentPage ActiveDocument => ((TabItem)DocumentTabControl.SelectedItem).Content as DocumentPage;

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
			_windowDatabaseMonitor.Owner = this;

			CommandBindings.Add(new CommandBinding(GenericCommands.SaveAll, SaveAllCommandExecutedHandler));

			WorkDocumentCollection.RestoreWindowProperties(this);
			_windowDatabaseMonitor.RestoreAppearance();

			if (WorkDocumentCollection.WorkingDocuments.Count > 0)
			{
				foreach (var workingDocument in WorkDocumentCollection.WorkingDocuments.OrderBy(d => d.TabIndex))
				{
					AddNewDocumentPage(workingDocument);
				}
			}
			else
			{
				AddNewDocumentPage();
			}

			DocumentTabControl.SelectionChanged += TabControlSelectionChangedHandler;

			DocumentTabControl.SelectedIndex = WorkDocumentCollection.ActiveDocumentIndex;

			ClipboardManager.RegisterWindow(this);
			ClipboardManager.ClipboardChanged += ClipboardChangedHandler;

			EditorNavigationService.Initialize(ActiveDocument.WorkDocument);
		}

		private static void ClipboardChangedHandler(object sender, EventArgs eventArgs)
		{
			EditorNavigationService.RegisterClipboardEntry();
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
			{
				return;
			}

			var files = (string[])e.Data.GetData(DataFormats.FileDrop);
			foreach (var file in files)
			{
				var fileInfo = new FileInfo(file);
				if (!fileInfo.Exists)
				{
					continue;
				}

				OpenExistingFile(fileInfo.FullName);
			}
		}

		private void TabControlSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
		{
			var tabItem = e.AddedItems.Count == 0 ? null : e.AddedItems[0] as TabItem;
			var document = tabItem?.Content as DocumentPage;

			if (document != null)
			{
				document.SaveWorkingDocument();
				_findReplaceManager.CurrentEditor = document.EditorAdapter;
				WorkDocumentCollection.ActiveDocumentIndex = DocumentTabControl.SelectedIndex;
				EditorNavigationService.RegisterDocumentCursorPosition(document.WorkDocument, document.Editor.CaretOffset);
			}

			if (!e.AddedItems.Contains(NewTabItem))
			{
				return;
			}

			AddNewDocumentPage();
		}

		public DocumentPage AddNewDocumentPage(WorkDocument workDocument = null)
		{
			var newDocumentPage = new DocumentPage(workDocument);
			
			_editorAdapters.Add(newDocumentPage.EditorAdapter);

			AddDocumentTabItemContextMenuCommandBindings(newDocumentPage);

			DocumentTabControl.Items.Insert(DocumentTabControl.Items.Count - 1, newDocumentPage.TabItem);
			DocumentTabControl.SelectedItem = newDocumentPage.TabItem;

			_findReplaceManager.CurrentEditor = newDocumentPage.EditorAdapter;

			return newDocumentPage;
		}

		private void AddDocumentTabItemContextMenuCommandBindings(DocumentPage documentPage)
		{
			var closeDocumentCommandBinding = new CommandBinding(GenericCommands.CloseDocument, CloseTabExecutedHandler, (sender, args) => args.CanExecute = true);
			documentPage.TabItemContextMenu.CommandBindings.Add(closeDocumentCommandBinding);
			documentPage.CommandBindings.Add(closeDocumentCommandBinding);
			documentPage.TabItemContextMenu.CommandBindings.Add(new CommandBinding(GenericCommands.CloseAllDocumentsButThis, CloseAllButThisTabExecutedHandler, (sender, args) => args.CanExecute = DocumentTabControl.Items.Count > 2));
		}

		private void CloseAllButThisTabExecutedHandler(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var currentDocument = (DocumentPage)executedRoutedEventArgs.Parameter;
			var allDocuments = DocumentTabControl.Items.Cast<TabItem>().Select(ti => ti.Content).OfType<DocumentPage>();
			var documentsToClose = allDocuments.Where(p => !p.Equals(currentDocument)).ToArray();
			foreach (var page in documentsToClose)
			{
				var isCancelled = !CloseDocument(page);
				if (isCancelled)
				{
					break;
				}
			}
		}

		private void CloseTabExecutedHandler(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var currentDocument = (executedRoutedEventArgs.Parameter ?? sender) as DocumentPage;
			if (currentDocument == null)
			{
				return;
			}

			var activeDocument = ActiveDocument;
			var previousDocument = EditorNavigationService.GetPreviousDocumentEdit();
			EditorNavigationService.IsEnabled = false;
			if (CloseDocument(currentDocument))
			{
				var isDocumentOpen = previousDocument != null && AllDocuments.Any(p => p.WorkDocument == previousDocument.Document);
				if (Equals(activeDocument, currentDocument) && isDocumentOpen)
				{
					GoToEditCommand(previousDocument);
				}
				else
				{
					DocumentTabControl.SelectedItem = activeDocument.TabItem;
				}
			}

			EditorNavigationService.IsEnabled = true;
		}

		internal bool CloseDocument(DocumentPage document)
		{
			DocumentTabControl.SelectedItem = document.TabItem;

			if (document.IsDirty && !ConfirmDocumentSave(document))
			{
				return false;
			}

			document.SaveWorkingDocument();

			SelectNewTabItem();
			DocumentTabControl.RemoveTabItemWithoutBindingError(document.TabItem);

			WorkDocumentCollection.CloseDocument(document.WorkDocument);

			document.Dispose();
			return true;
		}

		private bool ConfirmDocumentSave(DocumentPage document)
		{
			var message = document.WorkDocument.File == null
				? "Do you want to save the document?"
				: $"Do you want to save changes in '{document.WorkDocument.File.FullName}'?";
			
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
					throw new NotSupportedException($"'{dialogResult}' result is not supported. ");
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
				AddNewDocumentPage();
			}
		}

		private void WindowClosingHandler(object sender, CancelEventArgs e)
		{
			var documentWithActiveTransaction = AllDocuments.FirstOrDefault(d => d.OutputViewers.Any(ov => ov.HasActiveTransaction));
			if (documentWithActiveTransaction != null)
			{
				var dialogResult = MessageBox.Show("Some documents have active transaction. Do you want to continue and roll back the changes? ", "Confirmation", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
				if (dialogResult == MessageBoxResult.Cancel)
				{
					e.Cancel = true;
					documentWithActiveTransaction.TabItem.IsSelected = true;
					return;
				}
			}

			_timerWorkingDocumentSave.Stop();

			SaveWorkingDocuments();
		}

		internal void SaveWorkingDocuments()
		{
			foreach (var document in AllDocuments)
			{
				document.SaveWorkingDocument();
				document.WorkDocument.TabIndex = DocumentTabControl.Items.IndexOf(document.TabItem);
			}

			WorkDocumentCollection.StoreWindowProperties(this);
			WorkDocumentCollection.Save();

			Trace.WriteLine($"{DateTime.Now} - Working document collection saved. ");
		}

		private void WindowClosedHandler(object sender, EventArgs e)
		{
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
			if (!File.Exists(fileName))
			{
				return null;
			}

			var document = WorkDocument.IsSqlxFile(fileName)
				? WorkDocumentCollection.LoadDocumentFromFile(fileName)
				: new WorkDocument {DocumentFileName = fileName};
			
			return OpenExistingWorkDocument(document);
		}

		private DocumentPage OpenExistingWorkDocument(WorkDocument document)
		{
			DocumentPage documentPage;
			WorkDocument workDocument;
			if (WorkDocumentCollection.TryGetWorkingDocumentFor(document.DocumentFileName, out workDocument))
			{
				documentPage = AllDocuments.Single(d => d.WorkDocument == workDocument);
				DocumentTabControl.SelectedItem = documentPage.TabItem;
			}
			else
			{
				WorkDocumentCollection.AddDocument(document);
				documentPage = AddNewDocumentPage(document);
			}

			return documentPage;
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

			var documentPage = AllDocuments.SingleOrDefault(d => d.WorkDocument.Identifier == documentCursorPosition.Document.Identifier);
			if (documentPage == null)
			{
				if (documentCursorPosition.Document.File == null || !File.Exists(documentCursorPosition.Document.DocumentFileName))
				{
					return;
				}

				documentPage = OpenExistingFile(documentCursorPosition.Document.DocumentFileName);
			}

			EditorNavigationService.IsEnabled = false;
			documentPage.Editor.CaretOffset = Math.Min(documentCursorPosition.CursorPosition, documentPage.Editor.Document.TextLength - 1);
			DocumentTabControl.SelectedItem = documentPage.TabItem;
			documentPage.Editor.ScrollToCaret();
			EditorNavigationService.IsEnabled = true;
		}

		public void NotifyTaskStatus()
		{
			var isAnyDocumentBusy = AllDocuments.Any(d => d.IsBusy);
			TaskbarItemInfo.ProgressState = isAnyDocumentBusy
				? TaskbarItemProgressState.Indeterminate
				: TaskbarItemProgressState.None;
		}

		private void ShowRecentDocumentsHandler(object sender, ExecutedRoutedEventArgs e)
		{
			if (WorkDocumentCollection.RecentDocuments.Count == 0)
			{
				return;
			}

			_recentDocumentsMenu.HorizontalOffset = (Width - 240) / 2;
			RecentDocumentsMenuClosedHandler(null, null);

			var items = new List<RecentFileItem>();
			for (var i = 0; i < WorkDocumentCollection.RecentDocuments.Count; i++)
			{
				var item = new RecentFileItem(WorkDocumentCollection.RecentDocuments[i], i);
				items.Add(item);
				_recentDocumentsMenu.CommandBindings.Add(new CommandBinding(item.Command, OpenRecentFileHandler));

				if (i < 10)
				{
					var shortcut = (Key)Enum.Parse(typeof(Key), String.Format(CultureInfo.InvariantCulture, "NumPad{0}", i));
					_recentDocumentsMenu.InputBindings.Add(new KeyBinding(item.Command, shortcut, ModifierKeys.None) { CommandParameter = item.WorkDocument });
				}
			}

			_recentDocumentsMenu.ItemsSource = items;

			_recentDocumentsMenu.IsOpen = true;
		}

		private void OpenRecentFileHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var workDocument = (WorkDocument)args.Parameter;
			if (workDocument.File.Exists)
			{
				WorkDocumentCollection.AddRecentDocument(workDocument);
				OpenExistingWorkDocument(workDocument);
			}
			else
			{
				var result = MessageBox.Show(this, $"File '{workDocument.DocumentFileName}' does not exist anymore. Do you want to remove the link? ", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
				if (result == MessageBoxResult.Yes)
				{
					WorkDocumentCollection.RemoveRecentDocument(workDocument);
				}
			}
		}

		private void AddNewPageHandler(object sender, ExecutedRoutedEventArgs e)
		{
			AddNewDocumentPage();
		}

		private void ShowDatabaseMonitorExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			if (_windowDatabaseMonitor.IsVisible)
			{
				if (_windowDatabaseMonitor.WindowState == WindowState.Minimized)
				{
					SystemCommands.RestoreWindow(_windowDatabaseMonitor);
				}

				_windowDatabaseMonitor.Focus();
			}
			else
			{
				_windowDatabaseMonitor.Show();
			}

			_windowDatabaseMonitor.CurrentConnection = ActiveDocument.CurrentConnection;
		}

		private void ShowClipboardHistoryHandler(object sender, ExecutedRoutedEventArgs args)
		{
			new WindowClipboardHistory { Owner = this }
				.ShowDialog();
		}
	}

	internal class RecentFileItem
	{
		public RecentFileItem(WorkDocument workDocument, int index)
		{
			Index = index;
			WorkDocument = workDocument;
			DocumentFileName = workDocument.DocumentFileName.Replace("_", "__");
			var command = new RoutedCommand($"OpenRecentFile{index}", typeof(ContextMenu));

			Command = command;
		}

		public int Index { get; private set; }

		public WorkDocument WorkDocument { get; }
		
		public string DocumentFileName { get; private set; }

		public ICommand Command { get; }
	}
}
