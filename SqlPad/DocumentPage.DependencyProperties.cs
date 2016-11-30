﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Windows;
using System.Windows.Media;

namespace SqlPad
{
	public partial class DocumentPage
	{
		private static readonly string DefaultDocumentHeaderBackgroundColorCode = Colors.White.ToString();
		private static readonly string DefaultDocumentHeaderTextColorCode = Colors.Black.ToString();

		#region dependency properties registration
		public static readonly DependencyProperty ConnectionStatusProperty = DependencyProperty.Register(nameof(ConnectionStatus), typeof(ConnectionStatus), typeof(DocumentPage), new FrameworkPropertyMetadata(ConnectionStatus.Connecting));
		public static readonly DependencyProperty EnableDebugProperty = DependencyProperty.Register(nameof(EnableDebug), typeof(bool), typeof(DocumentPage), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsProductionConnectionProperty = DependencyProperty.Register(nameof(IsProductionConnection), typeof(bool), typeof(DocumentPage), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsModifiedProperty = DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(DocumentPage), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register(nameof(IsRunning), typeof(bool), typeof(DocumentPage), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty ConnectionErrorMessageProperty = DependencyProperty.Register(nameof(ConnectionErrorMessage), typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty TimedNotificationMessageProperty = DependencyProperty.Register(nameof(TimedNotificationMessage), typeof(NotificationMessage), typeof(DocumentPage), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty DocumentHeaderBackgroundColorCodeProperty = DependencyProperty.Register(nameof(DocumentHeaderBackgroundColorCode), typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(DocumentHeaderBackgroundColorCodePropertyChangedCallbackHandler) { DefaultValue = DefaultDocumentHeaderBackgroundColorCode });
		public static readonly DependencyProperty DocumentHeaderTextColorCodeProperty = DependencyProperty.Register(nameof(DocumentHeaderTextColorCode), typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(DocumentHeaderTextColorCodePropertyChangedCallbackHandler) { DefaultValue = DefaultDocumentHeaderTextColorCode });
		public static readonly DependencyProperty DocumentHeaderProperty = DependencyProperty.Register(nameof(DocumentHeader), typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(DocumentHeaderPropertyChangedCallbackHandler));
		public static readonly DependencyProperty DocumentHeaderToolTipProperty = DependencyProperty.Register(nameof(DocumentHeaderToolTip), typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty DateTimeFormatProperty = DependencyProperty.Register(nameof(DateTimeFormat), typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty SchemaLabelProperty = DependencyProperty.Register(nameof(SchemaLabel), typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty CurrentSchemaProperty = DependencyProperty.Register(nameof(CurrentSchema), typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(CurrentSchemaPropertyChangedCallbackHandler));
		public static readonly DependencyProperty CurrentConnectionProperty = DependencyProperty.Register(nameof(CurrentConnection), typeof(ConnectionStringSettings), typeof(DocumentPage), new FrameworkPropertyMetadata(CurrentConnectionPropertyChangedCallbackHandler));
		public static readonly DependencyProperty BindVariablesProperty = DependencyProperty.Register(nameof(BindVariables), typeof(IReadOnlyList<BindVariableModel>), typeof(DocumentPage), new FrameworkPropertyMetadata(new BindVariableModel[0]));
		public static readonly DependencyProperty DatabaseModelRefreshStatusProperty = DependencyProperty.Register(nameof(DatabaseModelRefreshStatus), typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata());
		#endregion

		#region dependency property accessors
		[Bindable(true)]
		public ConnectionStatus ConnectionStatus
		{
			get { return (ConnectionStatus)GetValue(ConnectionStatusProperty); }
			set { SetValue(ConnectionStatusProperty, value); }
		}

		[Bindable(true)]
		public bool EnableDebug
		{
			get { return (bool)GetValue(EnableDebugProperty); }
			set { SetValue(EnableDebugProperty, value); }
		}

		[Bindable(true)]
		public bool IsProductionConnection
		{
			get { return (bool)GetValue(IsProductionConnectionProperty); }
			private set { SetValue(IsProductionConnectionProperty, value); }
		}

		[Bindable(true)]
		public bool IsModified
		{
			get { return (bool)GetValue(IsModifiedProperty); }
			private set { SetValue(IsModifiedProperty, value); }
		}

		[Bindable(true)]
		public bool IsRunning
		{
			get { return (bool)GetValue(IsRunningProperty); }
			private set { SetValue(IsRunningProperty, value); }
		}

		[Bindable(true)]
		public string ConnectionErrorMessage
		{
			get { return (string)GetValue(ConnectionErrorMessageProperty); }
			private set { SetValue(ConnectionErrorMessageProperty, value); }
		}

		[Bindable(true)]
		public NotificationMessage TimedNotificationMessage
		{
			get { return (NotificationMessage)GetValue(TimedNotificationMessageProperty); }
			private set { SetValue(TimedNotificationMessageProperty, value); }
		}

		[Bindable(true)]
		public string DocumentHeaderBackgroundColorCode
		{
			get { return (string)GetValue(DocumentHeaderBackgroundColorCodeProperty); }
			private set { SetValue(DocumentHeaderBackgroundColorCodeProperty, value); }
		}

		private static void DocumentHeaderBackgroundColorCodePropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var newColorCode = (string)args.NewValue;
			var documentPage = (DocumentPage)dependencyObject;
			documentPage.WorkDocument.HeaderBackgroundColorCode = newColorCode;
			documentPage.SetDocumentModifiedIfSqlx();
		}

		[Bindable(true)]
		public string DocumentHeaderTextColorCode
		{
			get { return (string)GetValue(DocumentHeaderTextColorCodeProperty); }
			private set { SetValue(DocumentHeaderTextColorCodeProperty, value); }
		}

		private static void DocumentHeaderTextColorCodePropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var newColorCode = (string)args.NewValue;
			var documentPage = (DocumentPage)dependencyObject;
			documentPage.WorkDocument.HeaderTextColorCode = newColorCode;
			documentPage.SetDocumentModifiedIfSqlx();
		}

		[Bindable(true)]
		public string DocumentHeaderToolTip
		{
			get { return (string)GetValue(DocumentHeaderToolTipProperty); }
			private set { SetValue(DocumentHeaderToolTipProperty, value); }
		}

		[Bindable(true)]
		public string DateTimeFormat
		{
			get { return (string)GetValue(DateTimeFormatProperty); }
			private set { SetValue(DateTimeFormatProperty, value); }
		}

		[Bindable(true)]
		public string DocumentHeader
		{
			get { return (string)GetValue(DocumentHeaderProperty); }
			private set { SetValue(DocumentHeaderProperty, value); }
		}

		private static void DocumentHeaderPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var newHeader = (string)args.NewValue;
			var documentPage = (DocumentPage)dependencyObject;
			documentPage.WorkDocument.DocumentTitle = newHeader;
			documentPage.SetDocumentModifiedIfSqlx();
		}

		private void SetDocumentModifiedIfSqlx()
		{
			if (_isInitializing || !WorkDocument.IsSqlx || WorkDocument.IsModified)
			{
				return;
			}

			WorkDocument.IsModified = IsModified = true;
		}

		[Bindable(true)]
		public string SchemaLabel
		{
			get { return (string)GetValue(SchemaLabelProperty); }
			private set { SetValue(SchemaLabelProperty, value); }
		}

		[Bindable(true)]
		public string CurrentSchema
		{
			get { return (string)GetValue(CurrentSchemaProperty); }
			set { SetValue(CurrentSchemaProperty, value); }
		}

		private static void CurrentSchemaPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var newSchema = (string)args.NewValue;
			if (String.IsNullOrEmpty(newSchema))
			{
				return;
			}

			var documentPage = (DocumentPage)dependencyObject;
			documentPage.DatabaseModel.CurrentSchema = newSchema;

			if (!documentPage._isParsing)
			{
				documentPage.ReParse();
			}
		}

		[Bindable(true)]
		public ConnectionStringSettings CurrentConnection
		{
			get { return (ConnectionStringSettings)GetValue(CurrentConnectionProperty); }
			set { SetValue(CurrentConnectionProperty, value); }
		}

		private static void CurrentConnectionPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var newConnectionString = (ConnectionStringSettings)args.NewValue;
			var documentPage = (DocumentPage)dependencyObject;
			documentPage.ConnectionStatus = ConnectionStatus.Connecting;

			if (String.IsNullOrEmpty(documentPage.WorkDocument.DocumentTitle))
			{
				documentPage.DocumentHeader = documentPage.WorkDocument.File?.Name ?? InitialDocumentHeader;
			}
			else
			{
				documentPage.DocumentHeader = documentPage.WorkDocument.DocumentTitle;
			}

			documentPage.InitializeInfrastructureComponents(newConnectionString);
		}

		[Bindable(true)]
		public IReadOnlyList<BindVariableModel> BindVariables
		{
			get { return (IReadOnlyList<BindVariableModel>)GetValue(BindVariablesProperty); }
			private set { SetValue(BindVariablesProperty, value); }
		}

		[Bindable(true)]
		public string DatabaseModelRefreshStatus
		{
			get { return (string)GetValue(DatabaseModelRefreshStatusProperty); }
			private set { SetValue(DatabaseModelRefreshStatusProperty, value); }
		}
		#endregion
	}

	public class NotificationMessage
	{
		public string Text { get; set; }
		public Severity Severity { get; set; }
	}

	public enum Severity
	{
		Information,
		Warning
	}
}
