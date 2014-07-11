using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections;

namespace SqlPad.FindReplace
{
	public class FindReplaceManager : DependencyObject
	{
		private FindReplaceDialog _dialog;

		private FindReplaceDialog dialog
		{
			get
			{
				if (_dialog == null)
				{
					_dialog = new FindReplaceDialog(this);
					_dialog.Closed += delegate { _dialog = null; };
					_dialog.tabMain.SelectionChanged += TabMainOnSelectionChanged;

					if (OwnerWindow != null)
						_dialog.Owner = OwnerWindow;
				}

				return _dialog;
			}
		}

		private void TabMainOnSelectionChanged(object sender, SelectionChangedEventArgs selectionChangedEventArgs)
		{
			dialog.Title = dialog.tabMain.SelectedIndex == 0 ? "Find" : "Find And Replace";
		}

		public FindReplaceManager()
		{
			ReplacementText = String.Empty;

			SearchIn = SearchScope.CurrentDocument;
			ShowSearchIn = true;
		}

		#region Exposed CommandBindings
		public CommandBinding FindBinding
		{
			get { return new CommandBinding(ApplicationCommands.Find, (s, e) => ShowAsFind()); }
		}

		public CommandBinding FindNextBinding
		{
			get { return new CommandBinding(NavigationCommands.Search, (s, e) => FindNext(e.Parameter != null)); }
		}

		public CommandBinding ReplaceBinding
		{
			get { return new CommandBinding(ApplicationCommands.Replace, (s, e) => { if (AllowReplace) ShowAsReplace(); }); }
		}
		#endregion

		#region Public Properties
		/// <summary>
		/// The list of editors in which the search should take place.
		/// The elements must either implement the IEditor interface, or 
		/// InterfaceConverter should bne set.
		/// </summary>       
		public IEnumerable Editors
		{
			get { return (IEnumerable)GetValue(EditorsProperty); }
			set { SetValue(EditorsProperty, value); }
		}

		public static readonly DependencyProperty EditorsProperty =
			DependencyProperty.Register("Editors", typeof(IEnumerable), typeof(FindReplaceManager), new PropertyMetadata(null));

		/// <summary>
		/// The editor in which the current search operation takes place.
		/// </summary>
		public object CurrentEditor
		{
			get { return GetValue(CurrentEditorProperty); }
			set { SetValue(CurrentEditorProperty, value); }
		}

		public static readonly DependencyProperty CurrentEditorProperty =
			DependencyProperty.Register("CurrentEditor", typeof(object), typeof(FindReplaceManager), new PropertyMetadata(0));


		/// <summary>
		/// Objects in the Editors list that do not implement the IEditor interface are converted to IEditor using this converter.
		/// </summary>
		public IValueConverter InterfaceConverter
		{
			get { return (IValueConverter)GetValue(InterfaceConverterProperty); }
			set { SetValue(InterfaceConverterProperty, value); }
		}

		public static readonly DependencyProperty InterfaceConverterProperty =
			DependencyProperty.Register("InterfaceConverter", typeof(IValueConverter), typeof(FindReplaceManager), new PropertyMetadata(null));

		public static readonly DependencyProperty TextToFindProperty =
			DependencyProperty.Register("TextToFind", typeof(string), typeof(FindReplaceManager), new UIPropertyMetadata(String.Empty));

		public string TextToFind
		{
			get { return (string)GetValue(TextToFindProperty); }
			set { SetValue(TextToFindProperty, value); }
		}

		// public string ReplacementText { get; set; }
		public string ReplacementText
		{
			get { return (string)GetValue(ReplacementTextProperty); }
			set { SetValue(ReplacementTextProperty, value); }
		}

		// Using a DependencyProperty as the backing store for ReplacementText.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty ReplacementTextProperty =
			DependencyProperty.Register("ReplacementText", typeof(string), typeof(FindReplaceManager), new UIPropertyMetadata(String.Empty));

		public bool UseWildcards
		{
			get { return (bool)GetValue(UseWildcardsProperty); }
			set { SetValue(UseWildcardsProperty, value); }
		}

		public static readonly DependencyProperty UseWildcardsProperty =
			DependencyProperty.Register("UseWildcards", typeof(bool), typeof(FindReplaceManager), new UIPropertyMetadata(false));

		public bool SearchUp
		{
			get { return (bool)GetValue(SearchUpProperty); }
			set { SetValue(SearchUpProperty, value); }
		}

		public static readonly DependencyProperty SearchUpProperty =
			DependencyProperty.Register("SearchUp", typeof(bool), typeof(FindReplaceManager), new UIPropertyMetadata(false));

		public bool CaseSensitive
		{
			get { return (bool)GetValue(CaseSensitiveProperty); }
			set { SetValue(CaseSensitiveProperty, value); }
		}

		public static readonly DependencyProperty CaseSensitiveProperty =
			DependencyProperty.Register("CaseSensitive", typeof(bool), typeof(FindReplaceManager), new UIPropertyMetadata(false));

		public bool UseRegEx
		{
			get { return (bool)GetValue(UseRegExProperty); }
			set { SetValue(UseRegExProperty, value); }
		}

		public static readonly DependencyProperty UseRegExProperty =
			DependencyProperty.Register("UseRegEx", typeof(bool), typeof(FindReplaceManager), new UIPropertyMetadata(false));

		public bool WholeWord
		{
			get { return (bool)GetValue(WholeWordProperty); }
			set { SetValue(WholeWordProperty, value); }
		}

		public static readonly DependencyProperty WholeWordProperty =
			DependencyProperty.Register("WholeWord", typeof(bool), typeof(FindReplaceManager), new UIPropertyMetadata(false));

		public bool AcceptsReturn
		{
			get { return (bool)GetValue(AcceptsReturnProperty); }
			set { SetValue(AcceptsReturnProperty, value); }
		}

		public static readonly DependencyProperty AcceptsReturnProperty =
			DependencyProperty.Register("AcceptsReturn", typeof(bool), typeof(FindReplaceManager), new UIPropertyMetadata(false));

		public enum SearchScope
		{
			CurrentDocument,
			AllDocuments
		}

		public SearchScope SearchIn
		{
			get { return (SearchScope)GetValue(SearchInProperty); }
			set { SetValue(SearchInProperty, value); }
		}

		public static readonly DependencyProperty SearchInProperty =
			DependencyProperty.Register("SearchIn", typeof(SearchScope), typeof(FindReplaceManager), new UIPropertyMetadata(SearchScope.CurrentDocument));

		public double WindowLeft
		{
			get { return (double)GetValue(WindowLeftProperty); }
			set { SetValue(WindowLeftProperty, value); }
		}

		public static readonly DependencyProperty WindowLeftProperty =
			DependencyProperty.Register("WindowLeft", typeof(double), typeof(FindReplaceManager), new UIPropertyMetadata(100.0));

		public double WindowTop
		{
			get { return (double)GetValue(WindowTopProperty); }
			set { SetValue(WindowTopProperty, value); }
		}

		public static readonly DependencyProperty WindowTopProperty =
			DependencyProperty.Register("WindowTop", typeof(double), typeof(FindReplaceManager), new UIPropertyMetadata(100.0));

		/// <summary>
		/// Determines whether to display the Search in combo box
		/// </summary>
		public bool ShowSearchIn
		{
			get { return (bool)GetValue(ShowSearchInProperty); }
			set { SetValue(ShowSearchInProperty, value); }
		}

		public static readonly DependencyProperty ShowSearchInProperty =
			DependencyProperty.Register("ShowSearchIn", typeof(bool), typeof(FindReplaceManager), new UIPropertyMetadata(true));

		/// <summary>
		/// Determines whether the "Replace"-page in the dialog in shown or not.
		/// </summary>
		public bool AllowReplace
		{
			get { return (bool)GetValue(AllowReplaceProperty); }
			set { SetValue(AllowReplaceProperty, value); }
		}

		// Using a DependencyProperty as the backing store for AllowReplace.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty AllowReplaceProperty =
			DependencyProperty.Register("AllowReplace", typeof(bool), typeof(FindReplaceManager), new UIPropertyMetadata(true));

		/// <summary>
		/// The Window that serves as the parent of the Find/Replace dialog
		/// </summary>
		public Window OwnerWindow
		{
			get { return (Window)GetValue(OwnerWindowProperty); }
			set { SetValue(OwnerWindowProperty, value); }
		}

		public static readonly DependencyProperty OwnerWindowProperty =
			DependencyProperty.Register("OwnerWindow", typeof(Window), typeof(FindReplaceManager), new UIPropertyMetadata(null));
		#endregion

		private IEditor GetCurrentEditor()
		{
			if (CurrentEditor == null)
				return null;
			if (CurrentEditor is IEditor)
				return CurrentEditor as IEditor;
			if (InterfaceConverter == null)
				return null;

			return InterfaceConverter.Convert(CurrentEditor, typeof(IEditor), null, CultureInfo.CurrentCulture) as IEditor;
		}

		private IEditor GetNextEditor(bool previous = false)
		{
			if (!ShowSearchIn || SearchIn == SearchScope.CurrentDocument || Editors == null)
				return GetCurrentEditor();

			var editors = new List<object>(Editors.Cast<object>());
			var i = editors.IndexOf(CurrentEditor);
			if (i >= 0)
			{
				i = (i + (previous ? editors.Count - 1 : +1)) % editors.Count;
				CurrentEditor = editors[i];
			}

			return GetCurrentEditor();
		}

		/// <summary>
		/// Constructs a regular expression according to the currently selected search parameters.
		/// </summary>
		/// <param name="forceLeftToRight"></param>
		/// <returns>The regular expression.</returns>
		public Regex GetRegularExpression(bool forceLeftToRight = false)
		{
			Regex regularExpression;
			var options = RegexOptions.None;
			if (SearchUp && !forceLeftToRight)
			{
				options = options | RegexOptions.RightToLeft;
			}
			if (!CaseSensitive)
			{
				options = options | RegexOptions.IgnoreCase;
			}

			if (UseRegEx)
			{
				regularExpression = new Regex(TextToFind, options);
			}
			else
			{
				var pattern = Regex.Escape(TextToFind);
				if (UseWildcards)
				{
					pattern = pattern.Replace("\\*", ".*").Replace("\\?", ".");
				}
				
				if (WholeWord)
				{
					pattern = "\\b" + pattern + "\\b";
				}
				
				regularExpression = new Regex(pattern, options);
			}

			return regularExpression;
		}

		public void ReplaceAll()
		{
			var currentEditor = GetCurrentEditor();
			if (currentEditor == null)
				return;

			var initialEditor = CurrentEditor;
			// loop through all editors, until we are back at the starting editor                
			do
			{
				var r = GetRegularExpression(true); // force left to right, otherwise indices are screwed up
				var offset = 0;
				currentEditor.BeginChange();
				foreach (Match m in r.Matches(currentEditor.Text))
				{
					currentEditor.Replace(offset + m.Index, m.Length, ReplacementText);
					offset += ReplacementText.Length - m.Length;
				}

				currentEditor.EndChange();
				currentEditor = GetNextEditor();
			}
			while (CurrentEditor != initialEditor);
		}

		/// <summary>
		/// Shows this instance of FindReplaceDialog, with the Find page active
		/// </summary>
		public void ShowAsFind()
		{
			dialog.tabMain.SelectedIndex = 0;

			SetDefaultSearchedText(dialog.txtFind);

			dialog.Show();
			dialog.Activate();
			dialog.txtFind.Focus();
			dialog.txtFind.SelectAll();
		}

		private void SetDefaultSearchedText(TextBox textBox)
		{
			var currentEditor = GetCurrentEditor();
			if (currentEditor != null)
			{
				textBox.Text = currentEditor.SelectedText;
			}
		}

		public void ShowAsFind(TextEditor target)
		{
			CurrentEditor = target;
			ShowAsFind();
		}

		/// <summary>
		/// Shows this instance of FindReplaceDialog, with the Replace page active
		/// </summary>
		public void ShowAsReplace()
		{
			dialog.tabMain.SelectedIndex = 1;

			SetDefaultSearchedText(dialog.txtFind2);

			dialog.Show();
			dialog.Activate();
			dialog.txtFind2.Focus();
			dialog.txtFind2.SelectAll();
		}

		public void ShowAsReplace(object target)
		{
			CurrentEditor = target;
			ShowAsReplace();
		}

		public void FindNext(object target, bool invertLeftRight = false)
		{
			CurrentEditor = target;
			FindNext(invertLeftRight);
		}

		public void FindNext(bool invertLeftRight = false)
		{
			var currentEditor = GetCurrentEditor();
			if (currentEditor == null)
				return;

			Regex regularExpression;
			if (invertLeftRight)
			{
				SearchUp = !SearchUp;
				regularExpression = GetRegularExpression();
				SearchUp = !SearchUp;
			}
			else
			{
				regularExpression = GetRegularExpression();
			}

			var match = regularExpression.Match(currentEditor.Text, regularExpression.Options.HasFlag(RegexOptions.RightToLeft) ? currentEditor.SelectionStart : currentEditor.SelectionStart + currentEditor.SelectionLength);
			if (match.Success)
			{
				currentEditor.Select(match.Index, match.Length);
			}
			else
			{
				// we have reached the end of the document
				// start again from the beginning/end,
				var oldEditor = CurrentEditor;
				do
				{
					if (ShowSearchIn)
					{
						currentEditor = GetNextEditor(regularExpression.Options.HasFlag(RegexOptions.RightToLeft));
						if (currentEditor == null) return;
					}

					match = regularExpression.Match(currentEditor.Text, regularExpression.Options.HasFlag(RegexOptions.RightToLeft) ? currentEditor.Text.Length : 0);

					if (match.Success)
					{
						currentEditor.Select(match.Index, match.Length);
						break;
					}
					else
					{
						// Failed to find the text
						//MessageBox.Show("No occurence found.", "Search");
					}
				}
				while (CurrentEditor != oldEditor);
			}
		}

		public void FindPrevious()
		{
			FindNext(true);
		}

		public void Replace()
		{
			var currentEditor = GetCurrentEditor();
			if (currentEditor == null) return;

			// if currently selected text matches -> replace; anyways, find the next match
			var regularExpression = GetRegularExpression();
			var substring = currentEditor.Text.Substring(currentEditor.SelectionStart, currentEditor.SelectionLength);
			var match = regularExpression.Match(substring);
			if (match.Success && match.Index == 0 && match.Length == substring.Length)
			{
				currentEditor.Replace(currentEditor.SelectionStart, currentEditor.SelectionLength, ReplacementText);
			}

			FindNext();
		}

		/// <summary>
		/// Closes the Find/Replace dialog, if it is open
		/// </summary>
		public void CloseWindow()
		{
			dialog.Close();
		}
	}

	public class SearchScopeToInt : IValueConverter
	{
		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (int)value;
		}

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (FindReplaceManager.SearchScope)value;
		}

	}

	public class BoolToInt : IValueConverter
	{
		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ((bool)value)
				return 1;
			return 0;
		}

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
