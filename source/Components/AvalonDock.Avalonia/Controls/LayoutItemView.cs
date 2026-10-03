using System;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// The control that presents the content of a <see cref="LayoutContent"/> (<see cref="LayoutItem.View"/>).
	/// </summary>
	/// <remarks>
	/// The content picks its template from <see cref="DockingManager.LayoutItemTemplate"/>, then
	/// <see cref="DockingManager.LayoutItemTemplateSelector"/>, and otherwise from the data templates in scope
	/// (the usual Avalonia lookup). The view is moved, not recreated, when its content moves in the layout:
	/// see <see cref="LayoutItemViewHost"/>.
	/// </remarks>
	public class LayoutItemView : ContentControl
	{
		private LayoutContent _layoutContent;

		/// <summary>Initializes a new instance of the <see cref="LayoutItemView"/> class.</summary>
		/// <param name="layoutItem">The layout item this view belongs to.</param>
		internal LayoutItemView(LayoutItem layoutItem)
		{
			LayoutItem = layoutItem ?? throw new ArgumentNullException(nameof(layoutItem));
			HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
			VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
			Focusable = false;
			Attach(layoutItem.LayoutElement);
		}

		/// <summary>Gets the layout item this view belongs to.</summary>
		public LayoutItem LayoutItem { get; }

		/// <inheritdoc/>
		protected override Type StyleKeyOverride => typeof(ContentControl);

		/// <summary>Gets the host currently presenting this view, if any.</summary>
		internal LayoutItemViewHost Host => Parent as LayoutItemViewHost;

		/// <summary>
		/// Gets the top level the view was last shown in. Its layout manager may still have the view queued
		/// after the view has been detached, so the view only moves to another top level between layout passes.
		/// </summary>
		internal TopLevel LastTopLevel => _lastTopLevel != null && _lastTopLevel.TryGetTarget(out var topLevel) ? topLevel : null;

		private WeakReference<TopLevel> _lastTopLevel;

		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);
			var topLevel = TopLevel.GetTopLevel(this);
			_lastTopLevel = topLevel == null ? null : new WeakReference<TopLevel>(topLevel);
		}

		/// <summary>Disconnects the view from its content and its host.</summary>
		internal void Release()
		{
			Host?.ReleaseView();
			Attach(null);
		}

		/// <summary>Re-evaluates the content template, e.g. after the template properties of the manager changed.</summary>
		internal void UpdateTemplate()
		{
			var manager = _layoutContent?.Root?.Manager;
			if (manager == null)
			{
				ClearValue(ContentTemplateProperty);
				return;
			}

			// Like WPF, content that is a control is shown as it is, and a template that does not match the
			// content leaves it to the usual data template lookup.
			var content = Content;
			var template = content is Control ? null : manager.LayoutItemTemplate ?? manager.LayoutItemTemplateSelector?.SelectTemplate(content, this);
			if (template != null && template.Match(content)) ContentTemplate = template;
			else ClearValue(ContentTemplateProperty);
		}

		private void Attach(LayoutContent content)
		{
			if (_layoutContent != null) _layoutContent.PropertyChanged -= OnLayoutContentPropertyChanged;
			_layoutContent = content;
			if (_layoutContent != null) _layoutContent.PropertyChanged += OnLayoutContentPropertyChanged;
			Content = _layoutContent?.Content;
			UpdateTemplate();
		}

		private void OnLayoutContentPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(LayoutContent.Content))
			{
				Content = _layoutContent?.Content;
				UpdateTemplate();
			}
			else if (e.PropertyName == nameof(LayoutElement.Parent))
			{
				UpdateTemplate();
			}
		}
	}

	/// <summary>
	/// Hosts the <see cref="LayoutItem.View"/> of a <see cref="LayoutItem"/> inside a control template.
	/// </summary>
	/// <remarks>
	/// An Avalonia control can only have one parent, and a view can outlive the template that showed it
	/// when its content moves to another pane or window. A host therefore claims the view while it is in
	/// the visual tree, taking it away from whichever host had it before, and lets go of it as soon as it
	/// leaves the visual tree. The WPF library solves the same problem by cutting the old parent link
	/// explicitly (<c>DockingManager.DisconnectFromVisualParent</c>).
	/// </remarks>
	public class LayoutItemViewHost : Decorator
	{
		/// <summary><see cref="LayoutItem"/> property.</summary>
		public static readonly StyledProperty<LayoutItem> LayoutItemProperty =
			AvaloniaProperty.Register<LayoutItemViewHost, LayoutItem>(nameof(LayoutItem));

		/// <summary>Gets or sets the layout item whose view this host presents.</summary>
		private bool _claimPosted;

		public LayoutItem LayoutItem
		{
			get => GetValue(LayoutItemProperty);
			set => SetValue(LayoutItemProperty, value);
		}

		/// <summary>Removes the view from this host.</summary>
		internal void ReleaseView() => Child = null;

		/// <inheritdoc/>
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == LayoutItemProperty)
			{
				ReleaseView();
				if (this.IsAttachedToVisualTree()) ClaimView();
			}
		}

		/// <inheritdoc/>
		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);
			ClaimView();
		}

		/// <inheritdoc/>
		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnDetachedFromVisualTree(e);
			ReleaseView();
		}

		private void ClaimView()
		{
			var layoutItem = LayoutItem;
			if (layoutItem?.LayoutElement == null) return;
			var view = layoutItem.View;
			if (ReferenceEquals(Child, view)) return;

			// Moving the view out of another window while a layout pass may be running there leaves it in that
			// window's layout queue. Take it over once the current passes are done instead.
			var lastTopLevel = (view as LayoutItemView)?.LastTopLevel ?? TopLevel.GetTopLevel(view);
			if (lastTopLevel != null && lastTopLevel != TopLevel.GetTopLevel(this))
			{
				if (_claimPosted) return;
				_claimPosted = true;
				Dispatcher.UIThread.Post(
					() =>
					{
						_claimPosted = false;
						if (this.IsAttachedToVisualTree() && ReferenceEquals(LayoutItem, layoutItem)) TakeView(view);
					},
					DispatcherPriority.Loaded);
				return;
			}

			TakeView(view);
		}

		private void TakeView(Control view)
		{
			if (ReferenceEquals(Child, view)) return;
			if (view is LayoutItemView itemView) itemView.Host?.ReleaseView();

			// Detached windows and custom hosts may hold the view in a plain content control.
			if (view.Parent is ContentControl contentControl && ReferenceEquals(contentControl.Content, view))
				contentControl.Content = null;
			else if (view.Parent is Decorator decorator && ReferenceEquals(decorator.Child, view))
				decorator.Child = null;
			else if (view.Parent is Panel panel)
				panel.Children.Remove(view);

			if (view.Parent != null) return;
			Child = view;
		}
	}
}
