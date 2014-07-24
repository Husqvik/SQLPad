using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Windows;

namespace SqlPad
{
	public class PageModel : ModelBase
	{
		private readonly DocumentPage _documentPage;
		private readonly ObservableCollection<object[]> _resultRowItems = new ObservableCollection<object[]>();
		private readonly ObservableCollection<string> _schemas = new ObservableCollection<string>();
		private string _documentHeader;
		private int _currentLine;
		private int _currentColumn;
		private int? _selectionLength;
		private Visibility _selectionTextVisibility = Visibility.Collapsed;
		private ConnectionStringSettings _currentConnection;
		private string _currentSchema;

		public PageModel(DocumentPage documentPage)
		{
			_documentPage = documentPage;
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

		public ObservableCollection<string> Schemas { get { return _schemas; } }

		public string CurrentSchema
		{
			get { return _currentSchema; }
			set
			{
				if (value == null)
					return;

				if (!UpdateValueAndRaisePropertyChanged(ref _currentSchema, value))
					return;

				_documentPage.DatabaseModel.CurrentSchema = value;
				_documentPage.ReParse();
			}
		}

		public ConnectionStringSettings CurrentConnection
		{
			get { return _currentConnection; }
			set
			{
				try
				{
					_documentPage.InitializeInfrastructureComponents(value);
				}
				catch(Exception e)
				{
					Messages.ShowError(e.Message);

					if (_currentConnection == null)
						return;

					try
					{
						_documentPage.InitializeInfrastructureComponents(_currentConnection);
					}
					catch { }

					return;
				}

				_currentConnection = value;

				SetSchemas();

				CurrentSchema = _documentPage.DatabaseModel.CurrentSchema;
			}
		}

		private void SetSchemas()
		{
			var schemas = _documentPage.DatabaseModel.Schemas.OrderBy(s => s);
			_schemas.Clear();
			_schemas.AddRange(schemas);
			_currentSchema = null;
		}
	}
}
