using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace SqlPad.Commands
{
	public static class GenericCommands
	{
		public static RoutedCommand CloseDocument = new RoutedCommand("CloseDocument", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F4, ModifierKeys.Control), new KeyGesture(Key.W, ModifierKeys.Control) });
		public static RoutedCommand CloseAllDocumentsButThis = new RoutedCommand();
		public static RoutedCommand OpenContainingFolder = new RoutedCommand();
		public static RoutedCommand ExplainPlan = new RoutedCommand("ExplainPlan", typeof(Grid), new InputGestureCollection { new KeyGesture(Key.E, ModifierKeys.Control) });
		public static RoutedCommand ExportToCsv = new RoutedCommand();
		public static RoutedCommand GoToNextEdit = new RoutedCommand("GoToNextEdit", typeof(Window), new InputGestureCollection { new KeyGesture(Key.OemMinus, ModifierKeys.Control | ModifierKeys.Shift) });
		public static RoutedCommand GoToPreviousEdit = new RoutedCommand("GoToPreviousEdit", typeof(Window), new InputGestureCollection { new KeyGesture(Key.OemMinus, ModifierKeys.Control) });
		public static RoutedCommand ShowCodeCompletionOption = new RoutedCommand("ShowCodeCompletionOptions", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Space, ModifierKeys.Control) });
		public static RoutedCommand ShowFunctionOverload = new RoutedCommand("ShowFunctionOverloads", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Space, ModifierKeys.Control | ModifierKeys.Shift) });
		public static RoutedCommand DuplicateText = new RoutedCommand("DuplicateText", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.D, ModifierKeys.Control) });
		public static RoutedCommand BlockComment = new RoutedCommand("BlockComment", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Oem2, ModifierKeys.Control | ModifierKeys.Shift) });
		public static RoutedCommand LineComment = new RoutedCommand("LineComment", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Oem2, ModifierKeys.Control | ModifierKeys.Alt) });
		public static RoutedCommand ListContextAction = new RoutedCommand("ListContextActions", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Enter, ModifierKeys.Alt) });
		public static RoutedCommand MultiNodeEdit = new RoutedCommand("EditMultipleNodes", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F6, ModifierKeys.Shift) });
		public static RoutedCommand NavigateToPreviousUsage = new RoutedCommand("NavigateToPreviousHighlightedUsage", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.PageUp, ModifierKeys.Control | ModifierKeys.Alt) });
		public static RoutedCommand NavigateToNextUsage = new RoutedCommand("NavigateToNextHighlightedUsage", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.PageDown, ModifierKeys.Control | ModifierKeys.Alt) });
		public static RoutedCommand NavigateToQueryBlockRoot = new RoutedCommand("NavigateToQueryBlockRoot", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Home, ModifierKeys.Control | ModifierKeys.Alt) });
		public static RoutedCommand NavigateToDefinitionRoot = new RoutedCommand("NavigateToDefinition", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F12) });
		public static RoutedCommand ExecuteDatabaseCommand = new RoutedCommand("ExecuteDatabaseCommand", typeof(Grid), new InputGestureCollection { new KeyGesture(Key.F9), new KeyGesture(Key.Enter, ModifierKeys.Control) });
		public static RoutedCommand ExecuteDatabaseCommandWithActualExecutionPlan = new RoutedCommand("ExecuteDatabaseCommandWithActualExecutionPlan", typeof(Grid), new InputGestureCollection { new KeyGesture(Key.F9, ModifierKeys.Shift), new KeyGesture(Key.Enter, ModifierKeys.Control | ModifierKeys.Shift) });
		public static RoutedCommand Save = new RoutedCommand("Save", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Control) });
		public static RoutedCommand SaveAs = new RoutedCommand("SaveAs", typeof(TextEditor));
		public static RoutedCommand SaveAll = new RoutedCommand("SaveAll", typeof(Window), new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift) });
		public static RoutedCommand FormatStatement = new RoutedCommand("FormatStatement", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F, ModifierKeys.Control | ModifierKeys.Alt) });
		public static RoutedCommand FormatStatementAsSingleLine = new RoutedCommand("FormatStatementAsSingleLine", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F, ModifierKeys.Shift | ModifierKeys.Alt) });
		public static RoutedCommand FindUsages = new RoutedCommand("FindUsages", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F11, ModifierKeys.Alt | ModifierKeys.Shift) });
		public static RoutedCommand FetchAllRows = new RoutedCommand("FetchAllRows", typeof(DataGrid));
		public static RoutedCommand CancelUserAction = new RoutedCommand("CancelUserAction", typeof(Grid), new InputGestureCollection { new KeyGesture(Key.Escape) });
		public static RoutedCommand RefreshDatabaseModel = new RoutedCommand("RefreshDatabaseModel", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F5) });
		public static RoutedCommand CreateNewPage = new RoutedCommand("CreateNewPage", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.T, ModifierKeys.Control) });
	}
}
