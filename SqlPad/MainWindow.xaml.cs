using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SqlPad.FindReplace;

namespace SqlPad
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		private readonly IInfrastructureFactory _infrastructureFactory;

		private readonly FindReplaceManager _findReplaceManager;
		private readonly List<TextEditorAdapter> _editorAdapters = new List<TextEditorAdapter>();

		public MainWindow()
		{
			InitializeComponent();

			_infrastructureFactory = ConfigurationProvider.InfrastructureFactory;

			_findReplaceManager = (FindReplaceManager)Resources["FindReplaceManager"];
			_findReplaceManager.OwnerWindow = this;
			_findReplaceManager.Editors = _editorAdapters;
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
				CreateNewDocumentPage(fileInfo, true);
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
			var page = DocumentTabControl.SelectedContent as DocumentPage;
			if (page != null)
			{
				_findReplaceManager.CurrentEditor = page.EditorAdapter;
			}

			if (!e.AddedItems.Contains(NewTabItem))
				return;

			CreateNewDocumentPage();
		}

		private void CreateNewDocumentPage(FileInfo file = null, bool makeDirty = false)
		{
			var newDocumentPage = new DocumentPage(_infrastructureFactory, file, makeDirty);
			newDocumentPage.ComboBoxConnection.IsEnabled = ConfigurationProvider.ConnectionStrings.Count > 1;
			newDocumentPage.ComboBoxConnection.ItemsSource = ConfigurationProvider.ConnectionStrings;
			newDocumentPage.ComboBoxConnection.SelectedIndex = 0;
			
			_editorAdapters.Add(newDocumentPage.EditorAdapter);

			var header = new ContentControl { ContextMenu = CreateTabItemHeaderContextMenu(newDocumentPage) };
			var newTab = new TabItem
			{
				Content = newDocumentPage,
				Header = header
			};

			var binding = new Binding("DocumentHeader") { Source = newDocumentPage.DataContext, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
			header.SetBinding(ContentProperty, binding);
			
			DocumentTabControl.Items.Insert(DocumentTabControl.Items.Count - 1, newTab);
			DocumentTabControl.SelectedItem = newTab;

			_findReplaceManager.CurrentEditor = newDocumentPage.EditorAdapter;
		}

		private ContextMenu CreateTabItemHeaderContextMenu(DocumentPage documentPage)
		{
			var contextMenu = new ContextMenu();
			var menuItemClose = new MenuItem
			{
				Header = "Close",
				Command = new RoutedCommand(),
				CommandParameter = documentPage
			};
			
			contextMenu.Items.Add(menuItemClose);

			contextMenu.CommandBindings.Add(new CommandBinding(menuItemClose.Command, CloseTabExecutedHandler, (sender, args) => args.CanExecute = true));

			return contextMenu;
		}

		private void CloseTabExecutedHandler(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var page = (DocumentPage) executedRoutedEventArgs.Parameter;

			if (page.IsDirty && !ConfirmPageSave(page))
				return;
			
			SelectNewTabItem();
			DocumentTabControl.Items.Remove(page.Parent);
		}

		private bool ConfirmPageSave(DocumentPage page)
		{
			var message = page.File == null
				? "Do you want to save the document?"
				: String.Format("Do you want to save changes in '{0}'?", page.File.FullName);
			
			var dialogResult = MessageBox.Show(message, "Confirmation", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Yes);
			switch (dialogResult)
			{
				case MessageBoxResult.Yes:
					return page.Save();
				case MessageBoxResult.No:
					return true;
				case MessageBoxResult.Cancel:
					return false;
			}

			throw new NotSupportedException(string.Format("'{0}' result is not supported. ", dialogResult));
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
			
		}

		private void WindowClosedHandler(object sender, EventArgs e)
		{
			
		}
	}
}
