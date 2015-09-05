using System;
using System.Collections.Generic;

namespace SqlPad
{
	public class BindVariableModel : ModelBase
	{
		public BindVariableModel(BindVariableConfiguration bindVariable)
		{
			BindVariable = bindVariable;
		}

		public BindVariableConfiguration BindVariable { get; }

		public bool IsFilePath
		{
			get { return BindVariable.IsFilePath; }
			set { BindVariable.IsFilePath = value; }
		}

		public string Name => BindVariable.Name;

		public ICollection<BindVariableType> DataTypes => BindVariable.DataTypes.Values;

		public object Value
		{
			get { return BindVariable.Value; }
			set
			{
				if (BindVariable.Value == value)
				{
					return;
				}

				BindVariable.Value = value;
				RaisePropertyChanged(nameof(Value));
			}
		}

		public Type InputType => BindVariable.DataTypes[BindVariable.DataType].InputType;

		public BindVariableType DataType
		{
			get { return BindVariable.DataTypes[BindVariable.DataType]; }
			set
			{
				if (BindVariable.DataType == value.Name)
				{
					return;
				}

				var previousInputType = BindVariable.DataTypes[value.Name].InputType;

				BindVariable.DataType = value.Name;

				if (previousInputType != InputType)
				{
					Value = null;
					RaisePropertyChanged(nameof(InputType));
				}

				RaisePropertyChanged(nameof(DataType));
			}
		}
	}
}
