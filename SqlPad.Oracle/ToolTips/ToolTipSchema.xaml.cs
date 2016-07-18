using System;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipSchema
	{
		public ToolTipSchema(OracleSchemaModel dataModel)
		{
			InitializeComponent();

			DataContext = dataModel;
			var objectType = String.Equals(dataModel.Schema.Name, OracleObjectIdentifier.SchemaPublic)
				? "Schema"
				: "User/schema";

			IsExtractDdlVisible = !dataModel.Schema.Name.In(OracleObjectIdentifier.SchemaPublic, OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.SchemaSystem);

			LabelTitle.Text = $"{dataModel.Schema.Name.ToSimpleIdentifier()} ({objectType})";
		}

		protected override Task<string> ExtractDdlAsync(CancellationToken cancellationToken)
		{
			var dataModel = (OracleSchemaModel)DataContext;
			return ScriptExtractor.ExtractNonSchemaObjectScriptAsync(dataModel.Schema.Name.Trim('"'), "USER", cancellationToken);
		}
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

		public TablespaceDetailModel DefaultTablespaceModel { get; } = new TablespaceDetailModel();

		public TablespaceDetailModel TemporaryTablespaceModel { get; } = new TablespaceDetailModel();

		public ProfileDetailModel ProfileModel { get; } = new ProfileDetailModel();

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
					DefaultTablespaceModel.Name = value;
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
					TemporaryTablespaceModel.Name = value;
				}
			}
		}

		public string Profile
		{
			get { return _profile; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _profile, value))
				{
					ProfileModel.Name = value;
				}
			}
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
