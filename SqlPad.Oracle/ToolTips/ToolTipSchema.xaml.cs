using System;
using System.Windows;
using System.Windows.Controls;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipSchema : IToolTip
	{
		public event EventHandler Pin;

		public ToolTipSchema(OracleSchemaModel dataModel)
		{
			InitializeComponent();

			DataContext = dataModel;
			var objectType = String.Equals(dataModel.Schema.Name, OracleDatabaseModelBase.SchemaPublic)
				? "Schema"
				: "User/schema";

			LabelTitle.Text = $"{dataModel.Schema.Name.ToSimpleIdentifier()} ({objectType})";
		}

		public Control Control => this;

	    public FrameworkElement InnerContent => this;
	}

	public class OracleSchemaModel : ModelBase
	{
		private bool? _editionsEnabled;
		private string _accountStatus;
		private string _defaultTablespace;
		private string _temporaryTablespace;
		private string _profile;
		private string _authenticationType;
		private DateTime? _lockDate;
		private DateTime? _expiryDate;
		private DateTime? _lastLogin;

		public TablespaceDetailModel DefaultTablespaceDataModel { get; } = new TablespaceDetailModel();

		public TablespaceDetailModel TemporaryTablespaceDataModel { get; } = new TablespaceDetailModel();

		public OracleSchema Schema { get; set; }

		public string AccountStatus
		{
			get { return _accountStatus; }
			set { UpdateValueAndRaisePropertyChanged(ref _accountStatus, value); }
		}

		public string DefaultTablespace
		{
			get { return _defaultTablespace; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _defaultTablespace, value))
				{
					DefaultTablespaceDataModel.Name = value;
				}
			}
		}

		public string TemporaryTablespace
		{
			get { return _temporaryTablespace; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _temporaryTablespace, value))
				{
					TemporaryTablespaceDataModel.Name = value;
				}
			}
		}

		public string Profile
		{
			get { return _profile; }
			set { UpdateValueAndRaisePropertyChanged(ref _profile, value); }
		}

		public string AuthenticationType
		{
			get { return _authenticationType; }
			set { UpdateValueAndRaisePropertyChanged(ref _authenticationType, value); }
		}

		public bool? EditionsEnabled
		{
			get { return _editionsEnabled; }
			set { UpdateValueAndRaisePropertyChanged(ref _editionsEnabled, value); }
		}

		public DateTime? LockDate
		{
			get { return _lockDate; }
			set { UpdateValueAndRaisePropertyChanged(ref _lockDate, value); }
		}

		public DateTime? ExpiryDate
		{
			get { return _expiryDate; }
			set { UpdateValueAndRaisePropertyChanged(ref _expiryDate, value); }
		}

		public DateTime? LastLogin
		{
			get { return _lastLogin; }
			set { UpdateValueAndRaisePropertyChanged(ref _lastLogin, value); }
		}
	}
}
