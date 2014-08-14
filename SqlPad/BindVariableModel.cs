using System.Collections.Generic;

namespace SqlPad
{
	public class BindVariableModel : ModelBase
	{
		private readonly BindVariableConfiguration _bindVariable;

		public BindVariableModel(BindVariableConfiguration bindVariable)
		{
			_bindVariable = bindVariable;
		}

		public string Name { get { return _bindVariable.Name; } }
		
		public ICollection<string> DataTypes { get { return _bindVariable.DataTypes.Keys; } }

		public object Value
		{
			get { return _bindVariable.Value; }
			set
			{
				if (_bindVariable.Value == value)
					return;

				_bindVariable.Value = value;
				RaisePropertyChanged("Value");
			}
		}

		public string InputType
		{
			get { return _bindVariable.DataTypes[_bindVariable.DataType].Name; }
		}

		public string DataType
		{
			get { return _bindVariable.DataType; }
			set
			{
				if (_bindVariable.DataType == value)
					return;

				var previousInputType = _bindVariable.DataTypes[_bindVariable.DataType].Name;
				_bindVariable.DataType = value;

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