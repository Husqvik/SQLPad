using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace SqlPad
{
	public class PageModel : ModelBase
	{
		private readonly IDatabaseModel _databaseModel;
		private readonly Action _reParseAction;
		private readonly ObservableCollection<object[]> _resultRowItems = new ObservableCollection<object[]>();
		private string _documentHeader;
		private int _currentLine;
		private int _currentColumn;
		private int? _selectionLength;
		private Visibility _selectionTextVisibility = Visibility.Collapsed;

		public PageModel(IDatabaseModel databaseModel, Action reParseAction)
		{
			_reParseAction = reParseAction;
			_databaseModel = databaseModel;
		}

		public string DocumentHeader
		{
			get { return _documentHeader; }
			set { UpdateValueAndRaisePropertyChanged(ref _documentHeader, value); }
		}

		public int CurrentLine
		{
			get { return _currentLine; }
			set { UpdateValueAndRaisePropertyChanged(ref _currentLine, value); }
		}

		public int CurrentColumn
		{
			get { return _currentColumn; }
			set { UpdateValueAndRaisePropertyChanged(ref _currentColumn, value); }
		}

		public int? SelectionLength
		{
			get { return _selectionLength; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _selectionLength, value))
				{
					SelectionTextVisibility = _selectionLength == null ? Visibility.Collapsed : Visibility.Visible;
				}
			}
		}

		public Visibility SelectionTextVisibility
		{
			get { return _selectionTextVisibility; }
			set { UpdateValueAndRaisePropertyChanged(ref _selectionTextVisibility, value); }
		}

		public ObservableCollection<object[]> ResultRowItems { get { return _resultRowItems; } }

		public string CurrentSchema
		{
			get { return _databaseModel.CurrentSchema; }
			set
			{
				_databaseModel.CurrentSchema = value;
				_reParseAction();
			}
		}

		public string CurrentConnection
		{
			get { return null; }
			set
			{
				
			}
		}
	}
}