using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ProfileDetails
	{
		public static readonly DependencyProperty IsDetailVisibleProperty = DependencyProperty.Register(nameof(IsDetailVisible), typeof (bool), typeof (ProfileDetails), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(nameof(Profile), typeof (ProfileDetailModel), typeof (ProfileDetails), new FrameworkPropertyMetadata());

		[Bindable(true)]
		public bool IsDetailVisible
		{
			get { return (bool)GetValue(IsDetailVisibleProperty); }
			private set { SetValue(IsDetailVisibleProperty, value); }
		}

		[Bindable(true)]
		public ProfileDetailModel Profile
		{
			get { return (ProfileDetailModel)GetValue(ProfileProperty); }
			set { SetValue(ProfileProperty, value); }
		}

		public ProfileDetails()
		{
			InitializeComponent();
		}

		protected override async Task<string> ExtractDdlAsync(CancellationToken cancellationToken)
		{
			return await ScriptExtractor.ExtractNonSchemaObjectScriptAsync(Profile.Name, "PROFILE", cancellationToken);
		}

		private void ProfileHyperlinkClickHandler(object sender, RoutedEventArgs e)
		{
			IsDetailVisible = true;
		}
	}

	public class ProfileDetailModel : ModelBase
	{
		private string _name;
		private int? _sessionsPerUser;
		private int? _compositeLimit;
		private TimeSpan? _cpuPerSession;
		private TimeSpan? _cpuPerCall;
		private int? _logicalReadsPerSession;
		private int? _logicalReadsPerCall;
		private int? _idleTime;
		private int? _connectTime;
		private int? _privateSystemGlobalArea;
		private int? _failedLoginAttempts;
		private int? _passwordLifeTime;
		private int? _passwordReuseTime;
		private int? _passwordReuseMax;
		private string _passwordVerifyFunction;
		private int? _passwordLockTime;
		private int? _passwordGraceTime;

		public string Name
		{
			get { return _name; }
			set { UpdateValueAndRaisePropertyChanged(ref _name, value); }
		}

		public int? SessionsPerUser
		{
			get { return _sessionsPerUser; }
			set { UpdateValueAndRaisePropertyChanged(ref _sessionsPerUser, value); }
		}

		public int? CompositeLimit
		{
			get { return _compositeLimit; }
			set { UpdateValueAndRaisePropertyChanged(ref _compositeLimit, value); }
		}

		public TimeSpan? CpuPerSession
		{
			get { return _cpuPerSession; }
			set { UpdateValueAndRaisePropertyChanged(ref _cpuPerSession, value); }
		}

		public TimeSpan? CpuPerCall
		{
			get { return _cpuPerCall; }
			set { UpdateValueAndRaisePropertyChanged(ref _cpuPerCall, value); }
		}

		public int? LogicalReadsPerSession
		{
			get { return _logicalReadsPerSession; }
			set { UpdateValueAndRaisePropertyChanged(ref _logicalReadsPerSession, value); }
		}

		public int? LogicalReadsPerCall
		{
			get { return _logicalReadsPerCall; }
			set { UpdateValueAndRaisePropertyChanged(ref _logicalReadsPerCall, value); }
		}

		public int? IdleTime
		{
			get { return _idleTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _idleTime, value); }
		}

		public int? ConnectTime
		{
			get { return _connectTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _connectTime, value); }
		}

		public int? PrivateSystemGlobalArea
		{
			get { return _privateSystemGlobalArea; }
			set { UpdateValueAndRaisePropertyChanged(ref _privateSystemGlobalArea, value); }
		}

		public int? FailedLoginAttempts
		{
			get { return _failedLoginAttempts; }
			set { UpdateValueAndRaisePropertyChanged(ref _failedLoginAttempts, value); }
		}

		public int? PasswordLifeTime
		{
			get { return _passwordLifeTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _passwordLifeTime, value); }
		}

		public int? PasswordReuseTime
		{
			get { return _passwordReuseTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _passwordReuseTime, value); }
		}

		public int? PasswordReuseMax
		{
			get { return _passwordReuseMax; }
			set { UpdateValueAndRaisePropertyChanged(ref _passwordReuseMax, value); }
		}

		public string PasswordVerifyFunction
		{
			get { return _passwordVerifyFunction; }
			set { UpdateValueAndRaisePropertyChanged(ref _passwordVerifyFunction, value); }
		}

		public int? PasswordLockTime
		{
			get { return _passwordLockTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _passwordLockTime, value); }
		}

		public int? PasswordGraceTime
		{
			get { return _passwordGraceTime; }
			set { UpdateValueAndRaisePropertyChanged(ref _passwordGraceTime, value); }
		}
	}

	public class ProfileLimitConverter : PrettyPrintIntegerConverter
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
			{
				return "Unlimited";
			}

			var timespan = value as TimeSpan?;
			return timespan == null
				? AddUnitIfSpecified((string)base.Convert(value, targetType, parameter, culture), (string)parameter)
				: timespan.Value.ToPrettyString();
		}

		private static string AddUnitIfSpecified(string value, string unit)
		{
			return unit == null ? value : $"{value} {unit}";
		}
	}
}
