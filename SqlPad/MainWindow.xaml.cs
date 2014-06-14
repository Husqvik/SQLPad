using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
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

		private void WindowLoadedHandler(object sender, RoutedEventArgs e)
		{
			SqlPad.Resources.Initialize(Resources);

			CreateNewDocumentPage();

			DocumentTabControl.SelectionChanged += TabControlSelectionChangedHandler;
		}

		private void DropObjectHandler(object sender, DragEventArgs e)
		{
			if (!e.Data.GetDataPresent(DataFormats.FileDrop))
				return;

			var files = (string[])e.Data.GetData(DataFormats.FileDrop);
			CreateNewDocumentPage().Editor.Text = File.ReadAllText(files[0]);
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

		private DocumentPage CreateNewDocumentPage()
		{
			var newDocumentPage = new DocumentPage(_infrastructureFactory);
			newDocumentPage.ComboBoxConnection.ItemsSource = ConfigurationProvider.ConnectionStrings;
			newDocumentPage.ComboBoxConnection.SelectedIndex = 0;
			
			_editorAdapters.Add(newDocumentPage.EditorAdapter);
			
			var newTab = new TabItem { Content = newDocumentPage, Header = "New" };
			DocumentTabControl.Items.Insert(DocumentTabControl.Items.Count - 1, newTab);
			DocumentTabControl.SelectedItem = newTab;

			_findReplaceManager.CurrentEditor = newDocumentPage.EditorAdapter;

			return newDocumentPage;
		}
	}
}
