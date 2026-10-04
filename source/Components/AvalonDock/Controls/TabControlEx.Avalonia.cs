using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Base class of the pane controls: a <see cref="TabControl"/> over the children of a layout pane whose
	/// selection is kept in sync with <see cref="ILayoutContentSelector.SelectedContentIndex"/>.
	/// </summary>
	/// <remarks>
	/// The WPF library binds <c>TabItem.IsSelected</c> two way to <see cref="LayoutContent.IsSelected"/> through
	/// an item container style. Avalonia's selection model owns container selection, so the selection is
	/// synchronised here in code instead, with the layout model as the source of truth. The WPF "non
	/// virtualizing" mode, which kept every tab's content alive, is not needed: the content of a tab is the
	/// <see cref="LayoutItem.View"/>, which survives tab switches anyway.
	/// </remarks>
	public abstract class TabControlEx : TabControl
	{
		private readonly ILayoutContentSelector _selector;
		private bool _isSyncingSelection;
		private bool _resyncPosted;

		/// <summary>Initializes a new instance of the <see cref="TabControlEx"/> class.</summary>
		/// <param name="model">The pane model.</param>
		/// <param name="ignoreTabControlKeyBindings">Whether the key handling of <see cref="TabControl"/> is bypassed.</param>
		protected TabControlEx(ILayoutContainer model, bool ignoreTabControlKeyBindings)
		{
			if (model == null) throw new ArgumentNullException(nameof(model));
			_selector = model as ILayoutContentSelector;
			IgnoreTabControlKeyBindings = ignoreTabControlKeyBindings;
			Focusable = false;
		}

		/// <summary>Gets a value indicating whether the TabControl key bindings are ignored.</summary>
		public bool IgnoreTabControlKeyBindings { get; }

		/// <summary>Attaches the control to the children of its model.</summary>
		/// <param name="children">The children of the pane model.</param>
		protected void AttachItems(INotifyCollectionChanged children)
		{
			ItemsSource = (System.Collections.IEnumerable)children;
			children.CollectionChanged += OnModelChildrenChanged;
			if (_selector is INotifyPropertyChanged notifier) notifier.PropertyChanged += OnModelPropertyChanged;
			SyncSelectionFromModel();
		}

		/// <inheritdoc/>
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == SelectedIndexProperty && _selector != null)
			{
				var index = SelectedIndex;
				if (!_isSyncingSelection && index >= 0 && index != _selector.SelectedContentIndex)
				{
					using (BeginSync())
						_selector.SelectedContentIndex = index;
				}

				// Like the WPF control, a selection made through the model activates the content as well.
				OnSelectedContentChanged();
			}
		}

		/// <summary>Called after the user changed the selected tab.</summary>
		protected virtual void OnSelectedContentChanged()
		{
		}

		/// <inheritdoc/>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (!IgnoreTabControlKeyBindings) base.OnKeyDown(e);
		}

		private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ILayoutContentSelector.SelectedContentIndex)) SyncSelectionFromModel();
		}

		private void OnModelChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			// The tab control adjusts its own selection while it processes the change, possibly before the
			// model has picked the content it wants selected. Take the model's choice once both are done.
			PostResync();
		}

		private void PostResync()
		{
			if (_resyncPosted) return;
			_resyncPosted = true;
			Dispatcher.UIThread.Post(
				() =>
				{
					_resyncPosted = false;
					SyncSelectionFromModel();
				},
				DispatcherPriority.Loaded);
		}

		private void SyncSelectionFromModel()
		{
			if (_selector == null || _isSyncingSelection) return;
			// While the model's children change, the model can report a selection the items of this control do
			// not reflect yet; synchronise once the change has been processed.
			if (_selector is ILayoutContainer container && container.ChildrenCount != ItemCount)
			{
				PostResync();
				return;
			}

			var index = _selector.SelectedContentIndex;
			if (index >= ItemCount) return;
			if (SelectedIndex == index) return;
			using (BeginSync())
				SelectedIndex = index;
		}

		private IDisposable BeginSync()
		{
			_isSyncingSelection = true;
			return new SyncScope(this);
		}

		private sealed class SyncScope : IDisposable
		{
			private TabControlEx _owner;

			public SyncScope(TabControlEx owner) => _owner = owner;

			public void Dispose()
			{
				if (_owner == null) return;
				_owner._isSyncingSelection = false;
				_owner = null;
			}
		}
	}
}
