using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;

namespace SqlPad.DragDrop
{
	public class DraggedAdorner : Adorner
	{
		private readonly ContentPresenter contentPresenter;
		private double left;
		private double top;
		private readonly AdornerLayer adornerLayer;

		public DraggedAdorner(object dragDropData, DataTemplate dragDropTemplate, UIElement adornedElement, AdornerLayer adornerLayer)
			: base(adornedElement)
		{
			this.adornerLayer = adornerLayer;

			this.contentPresenter =
				new ContentPresenter
				{
					Content = dragDropData,
					ContentTemplate = dragDropTemplate,
					Opacity = 0.7
				};

			this.adornerLayer.Add(this);
		}

		public void SetPosition(double left, double top)
		{
			this.left = left - 1;
			this.top = top + 13;
			
			if (this.adornerLayer != null)
			{
				this.adornerLayer.Update(this.AdornedElement);
			}
		}

		protected override Size MeasureOverride(Size constraint)
		{
			this.contentPresenter.Measure(constraint);
			return this.contentPresenter.DesiredSize;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			this.contentPresenter.Arrange(new Rect(finalSize));
			return finalSize;
		}

		protected override Visual GetVisualChild(int index)
		{
			return this.contentPresenter;
		}

		protected override int VisualChildrenCount
		{
			get { return 1; }
		}

		public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
		{
			var result = new GeneralTransformGroup();
			result.Children.Add(base.GetDesiredTransform(transform));
			result.Children.Add(new TranslateTransform(this.left, this.top));

			return result;
		}

		public void Detach()
		{
			this.adornerLayer.Remove(this);
		}
	}
}
