using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SqlPad
{
	[TemplatePart(Name = "PART_TabHeader", Type = typeof(TextBox))]
	public class EditableTabHeaderControl : ContentControl
	{
		private static readonly DependencyProperty EditModeEnabledProperty = DependencyProperty.Register("EditModeEnabled", typeof(bool), typeof(EditableTabHeaderControl));
		public static readonly DependencyProperty IsModifiedProperty = DependencyProperty.Register("IsModified", typeof(bool), typeof(EditableTabHeaderControl));
		public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register("IsRunning", typeof(bool), typeof(EditableTabHeaderControl));
		
		private string _oldHeader;
		private TextBox _textBox;
		private DispatcherTimer _timer;

		public bool EditModeEnabled
		{
			get
			{
				return (bool)GetValue(EditModeEnabledProperty);
			}
			set
			{
				if (String.IsNullOrEmpty(_textBox.Text))
				{
					_textBox.Text = _oldHeader;
				}

				_oldHeader = _textBox.Text;
				SetValue(EditModeEnabledProperty, value);
			}
		}

		public bool IsModified
		{
			get { return (bool)GetValue(IsModifiedProperty); }
			set { SetValue(IsModifiedProperty, value); }
		}

		public bool IsRunning
		{
			get { return (bool)GetValue(IsRunningProperty); }
			set { SetValue(IsRunningProperty, value); }
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			
			_textBox = Template.FindName("PART_TabHeader", this) as TextBox;
			if (_textBox == null)
			{
				return;
			}
			
			_timer = new DispatcherTimer();
			_timer.Tick += TimerTick;
			_timer.Interval = TimeSpan.FromMilliseconds(1);
			_textBox.KeyDown += TextBoxKeyDown;
			LostFocus += TextBoxLostFocus;
			MouseDoubleClick += EditableTabHeaderControlMouseDoubleClick;
		}

		public void SetEditMode(bool value)
		{
			EditModeEnabled = value;
			_timer.Start();
		}

		private void TimerTick(object sender, EventArgs e)
		{
			_timer.Stop();
			MoveTextBoxInFocus();
		}

		private void MoveTextBoxInFocus()
		{
			if (_textBox.CheckAccess())
			{
				if (!String.IsNullOrEmpty(_textBox.Text) && !IsFocused && !_textBox.IsFocused)
				{
					_textBox.Focus();
				}
			}
			else
			{
				_textBox.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(MoveTextBoxInFocus));
			}
		}

		private void TextBoxKeyDown(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Escape:
					_textBox.Text = _oldHeader;
					EditModeEnabled = false;
					break;
				case Key.Enter:
					EditModeEnabled = false;
					break;
			}
		}

		private void TextBoxLostFocus(object sender, RoutedEventArgs e)
		{
			EditModeEnabled = false;
		}

		private void EditableTabHeaderControlMouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				SetEditMode(true);
			}
		}
	}
}
