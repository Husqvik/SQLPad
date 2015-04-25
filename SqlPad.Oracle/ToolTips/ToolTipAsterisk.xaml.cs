using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipAsterisk : IToolTip
	{
		public ToolTipAsterisk()
		{
			InitializeComponent();
		}

		public IEnumerable<OracleColumnModel> Columns
		{
			get
			{
				var sourceModels = (IEnumerable<OracleColumnViewModel>)ItemsControl.ItemsSource;
				return sourceModels.Where(m => !m.IsSeparator);
			}
			set
			{
				var columnViewModels = new List<OracleColumnViewModel>();

				OracleColumnModel previousModel = null;
				foreach (var model in value)
				{
					if (previousModel != null && previousModel.RowSourceName != model.RowSourceName)
					{
						columnViewModels.Add(OracleColumnViewModel.CreateSeparator());
					}

					columnViewModels.Add(OracleColumnViewModel.FromDataModel(model));
					
					previousModel = model;
				}

				ItemsControl.ItemsSource = columnViewModels;
			}
		}

		public UserControl Control
		{
			get { return this; }
		}
	}

	public class OracleColumnModel
	{
		public int ColumnIndex { get; set; }

		public string Name { get; set; }
		
		public string FullTypeName { get; set; }

		public string RowSourceName { get; set; }

		public string Label { get { return String.IsNullOrEmpty(RowSourceName) ? Name : String.Format("{0}.{1}", RowSourceName, Name); } }
	}

	internal class OracleColumnViewModel : OracleColumnModel
	{
		public bool IsSeparator { get; private set; }

		public static OracleColumnViewModel CreateSeparator()
		{
			return new OracleColumnViewModel { IsSeparator = true };
		}

		public static OracleColumnViewModel FromDataModel(OracleColumnModel model)
		{
			return
				new OracleColumnViewModel
				{
					ColumnIndex = model.ColumnIndex,
					FullTypeName = model.FullTypeName,
					Name = model.Name.ToSimpleIdentifier(),
					RowSourceName = model.RowSourceName
				};
		}
	}
}
