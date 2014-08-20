using System.Collections.Generic;

namespace SqlPad
{
	public class BindVariableModel : ModelBase
	{
		public BindVariableModel(BindVariableConfiguration bindVariable)
		{
			BindVariable = bindVariable;
		}

		public BindVariableConfiguration BindVariable { get; private set; }

		public string Name { get { return BindVariable.Name; } }
		
		public ICollection<string> DataTypes { get { return BindVariable.DataTypes.Keys; } }

		public object Value
		{
			get { return BindVariable.Value; }
			set
			{
				if (BindVariable.Value == value)
					return;

				BindVariable.Value = value;
				RaisePropertyChanged("Value");
			}
		}

		public string InputType
		{
			get { return BindVariable.DataTypes[BindVariable.DataType].Name; }
		}

		public string DataType
		{
			get { return BindVariable.DataType; }
			set
			{
				if (BindVariable.DataType == value)
					return;

				var previousInputType = BindVariable.DataTypes[BindVariable.DataType].Name;
				BindVariable.DataType = value;

				if (previousInputType != InputType)
				{
					Value = null;
					RaisePropertyChanged("InputType");
				}

				RaisePropertyChanged("DataType");
			}
		}
	}
}