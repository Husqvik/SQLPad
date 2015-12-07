using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace SqlPad
{
	public static class WpfExtensions
	{
		public static T FindParentVisual<T>(this Visual child) where T : Visual
		{
			var parent = (Visual)VisualTreeHelper.GetParent(child);
			if (parent == null)
			{
				return null;
			}

			var typedParent = parent as T;
			return typedParent ?? FindParentVisual<T>(parent);
		}

		public static T FindChildVisual<T>(this Visual parent) where T : Visual
		{
			var child = default(T);
			var numVisuals = VisualTreeHelper.GetChildrenCount(parent);
			for (var i = 0; i < numVisuals; i++)
			{
				var v = (Visual)VisualTreeHelper.GetChild(parent, i);
				child = v as T ?? FindChildVisual<T>(v);
				
				if (child != null)
				{
					break;
				}
			}

			return child;
		}

		public static void RemoveTabItemWithoutBindingError(this TabControl tabControl, TabItem item)
		{
			item.Template = null;
			tabControl.Items.Remove(item);
		}

		public static bool IsInViewport(this FrameworkElement container, FrameworkElement element)
		{
			if (!element.IsVisible)
			{
				return false;
			}

			var bounds = element.TransformToAncestor(container).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
			var rect = new Rect(0, 0, container.ActualWidth, container.ActualHeight);
			return rect.Contains(bounds.TopLeft) || rect.Contains(bounds.BottomRight);
		}

		public static void CancelOnEscape(this CancellationTokenSource cancellationTokenSource, Key key)
		{
			if (key != Key.Escape)
			{
				return;
			}

			Trace.WriteLine("Action is about to cancel. ");
			cancellationTokenSource.Cancel();
		}

		public static T AsPopupChild<T>(this T control) where T : Control
		{
			control.Background = (SolidColorBrush)Application.Current.Resources["PopupBackgroundBrush"];
			control.BorderThickness = new Thickness(1);
			control.BorderBrush = (SolidColorBrush)Application.Current.Resources["PopupBorderBrush"];
			return control;
		}

		public static void HighlightTextItems(this DependencyObject target, string regexPattern)
		{
			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(target); i++)
			{
				if (target is ListViewItem || target is ListBoxItem || target is DataGridCell)
				{
					HighlightText(target, regexPattern);
				}

				HighlightTextItems(VisualTreeHelper.GetChild(target, i), regexPattern);
			}
		}

		private static void HighlightText(DependencyObject dependencyObject, string regexPattern)
		{
			if (dependencyObject == null)
			{
				return;
			}

			var textBlock = dependencyObject as TextBlock;
			if (textBlock == null)
			{
				for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
				{
					HighlightText(VisualTreeHelper.GetChild(dependencyObject, i), regexPattern);
				}
			}
			else
			{
				var inlines = textBlock.Inlines;
				var inlineCount = inlines.Count;
				var firstRun = inlines.FirstInline as Run;
				if (firstRun == null)
				{
					return;
				}

				var text = textBlock.Text;

				if (regexPattern.Length == 0)
				{
					if (inlineCount == 1 && String.Equals(firstRun.Text, text))
					{
						return;
					}

					inlines.Clear();
					inlines.Add(text);
					return;
				}

				var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
				var substrings = regex.Split(text);
				var index = 0;
				if (substrings.Length == inlineCount && inlines.All(i => (i as Run)?.Text.Length == substrings[index++].Length))
				{
					return;
				}

				inlines.Clear();
				var contentPresenter = (ContentPresenter)textBlock.TemplatedParent;

				object tag = null;
				foreach (var item in substrings)
				{
					if (regex.Match(item).Success)
					{
						var run = new Run(item) { Background = Brushes.Yellow };
						inlines.Add(run);
						tag = DataGridHelper.TagHighlight;
					}
					else
					{
						inlines.Add(item);
					}
				}

				if (contentPresenter != null)
				{
					contentPresenter.Tag = tag;
				}
			}
		}

		public static void AddValueChanged(this DependencyProperty property, object sourceObject, EventHandler handler)
		{
			var propertyDescriptor = DependencyPropertyDescriptor.FromProperty(property, property.OwnerType);
			propertyDescriptor.AddValueChanged(sourceObject, handler);
		}
	}

	[MarkupExtensionReturnType(typeof(Type)), ContentProperty(nameof(TypeArguments))]
	public class TypeExtension : MarkupExtension
	{
		private Type _closedType;

		public TypeExtension()
		{
			_typeArguments = new List<Type>();
		}

		public TypeExtension(string typeName)
			: this()
		{
			CheckNotNull(typeName, "typeName");

			_typeName = typeName;
		}

		public TypeExtension(string typeName, Type typeArgument1)
			: this(typeName)
		{
			CheckNotNull(typeArgument1, "typeArgument1");

			TypeArgument1 = typeArgument1;
		}

		public TypeExtension(string typeName, Type typeArgument1, Type typeArgument2)
			: this(typeName, typeArgument1)
		{
			CheckNotNull(typeArgument2, "typeArgument2");

			TypeArgument2 = typeArgument2;
		}

		public TypeExtension(string typeName, Type typeArgument1, Type typeArgument2, Type typeArgument3)
			: this(typeName, typeArgument1, typeArgument2)
		{
			CheckNotNull(typeArgument3, "typeArgument3");

			TypeArgument3 = typeArgument3;
		}

		public TypeExtension(string typeName, Type typeArgument1, Type typeArgument2, Type typeArgument3,
			Type typeArgument4)
			: this(typeName, typeArgument1, typeArgument2, typeArgument3)
		{
			CheckNotNull(typeArgument4, "typeArgument4");

			TypeArgument4 = typeArgument4;
		}

		private string _typeName;

		[ConstructorArgument("typeName")]
		public string TypeName
		{
			get { return _typeName; }
			set
			{
				CheckNotNull(value, "value");

				_typeName = value;
				_type = null;
			}
		}

		private Type _type;

		public Type Type
		{
			get { return _type; }
			set
			{
				CheckNotNull(value, "value");

				_type = value;
				_typeName = null;
			}
		}

		private readonly List<Type> _typeArguments;

		public IList<Type> TypeArguments => _typeArguments;

		[ConstructorArgument("typeArgument1")]
		public Type TypeArgument1
		{
			get { return GetTypeArgument(0); }
			set { SetTypeArgument(0, value); }
		}

		[ConstructorArgument("typeArgument2")]
		public Type TypeArgument2
		{
			get { return GetTypeArgument(1); }
			set { SetTypeArgument(1, value); }
		}

		[ConstructorArgument("typeArgument3")]
		public Type TypeArgument3
		{
			get { return GetTypeArgument(2); }
			set { SetTypeArgument(2, value); }
		}

		[ConstructorArgument("typeArgument4")]
		public Type TypeArgument4
		{
			get { return GetTypeArgument(3); }
			set { SetTypeArgument(3, value); }
		}

		private Type GetTypeArgument(int index)
		{
			return index < _typeArguments.Count ? _typeArguments[index] : null;
		}

		private void SetTypeArgument(int index, Type value)
		{
			CheckNotNull(value, "value");

			if (index > _typeArguments.Count)
			{
				throw new ArgumentOutOfRangeException(nameof(value), "ArgumentsWrongOrder");
			}

			if (index == _typeArguments.Count)
			{
				_typeArguments.Add(value);
			}
			else
			{
				_typeArguments[index] = value;
			}
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			if (_closedType != null)
			{
				return _closedType;
			}

			if (_typeName == null && _type == null)
			{
				throw new InvalidOperationException("TypeOrNameMissing");
			}

			var type = _type;
			var typeArguments = _typeArguments.TakeWhile(t => t != null).ToArray();

			if (type == null)
			{
				// resolve using type name
				// ReSharper disable once ConditionIsAlwaysTrueOrFalse
				var typeResolver = serviceProvider != null
					? serviceProvider.GetService(typeof (IXamlTypeResolver)) as IXamlTypeResolver
					: null;

				if (typeResolver == null)
				{
					throw new InvalidOperationException("NoIXamlTypeResolver");
				}

				// check that the number of generic arguments match
				var typeName = TypeName;
				if (typeArguments.Length > 0)
				{
					var genericsMarkerIndex = typeName.LastIndexOf('`');
					if (genericsMarkerIndex < 0)
					{
						typeName = $"{typeName}`{typeArguments.Length}";
					}
					else
					{
						var validArgumentCount = false;
						if (genericsMarkerIndex < typeName.Length)
						{
							int typeArgumentCount;
							if (int.TryParse(typeName.Substring(genericsMarkerIndex + 1), out typeArgumentCount))
							{
								validArgumentCount = true;
							}
						}

						if (!validArgumentCount)
						{
							throw new InvalidOperationException("InvalidTypeNameArgumentCount");
						}
					}
				}

				type = typeResolver.Resolve(typeName);
				if (type == null)
				{
					throw new InvalidOperationException("InvalidTypeName");
				}
			}
			else if (type.IsGenericTypeDefinition && type.GetGenericArguments().Length != typeArguments.Length)
			{
				throw new InvalidOperationException("InvalidTypeArgumentCount");
			}

			// build closed type
			if (typeArguments.Length > 0 && type.IsGenericTypeDefinition)
			{
				_closedType = type.MakeGenericType(typeArguments);
			}
			else
			{
				_closedType = type;
			}

			return _closedType;
		}

		private static void CheckNotNull(object argumentValue, string argumentName)
		{
			if (argumentValue == null)
			{
				throw new ArgumentNullException(argumentName);
			}
		}
	}
}
