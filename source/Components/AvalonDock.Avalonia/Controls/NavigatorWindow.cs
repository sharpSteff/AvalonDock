using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvalonDock.Layout;

namespace AvalonDock.Controls
{
	/// <summary>
	/// The Ctrl+Tab window that lists the open documents and tool windows, most recently used first.
	/// Releasing Ctrl activates the selected entry.
	/// </summary>
	public class NavigatorWindow : Window
	{
		private readonly DockingManager _manager;
		private readonly ListBox _anchorableListBox;
		private readonly ListBox _documentListBox;
		private bool _isSelectingDocument;
		private bool _closing;

		/// <summary>Initializes a new instance of the <see cref="NavigatorWindow"/> class.</summary>
		/// <param name="manager">The manager whose contents are listed.</param>
		internal NavigatorWindow(DockingManager manager)
		{
			_manager = manager;
			ShowInTaskbar = false;
			CanResize = false;
			WindowDecorations = WindowDecorations.BorderOnly;
			SizeToContent = SizeToContent.WidthAndHeight;
			Title = Properties.Resources.Active_Files;

			Anchorables = _manager.Layout.Descendents()
				.OfType<LayoutAnchorable>()
				.Where(a => a.IsVisible)
				.OrderByDescending(d => d.LastActivationTimeStamp.GetValueOrDefault())
				.Select(d => _manager.GetLayoutItemFromModel(d) as LayoutAnchorableItem)
				.Where(i => i != null)
				.ToArray();
			Documents = _manager.Layout.Descendents()
				.OfType<LayoutDocument>()
				.OrderByDescending(d => d.LastActivationTimeStamp.GetValueOrDefault())
				.Select(d => _manager.GetLayoutItemFromModel(d) as LayoutDocumentItem)
				.Where(i => i != null)
				.ToArray();

			var itemTemplate = new FuncDataTemplate<LayoutItem>((item, _) => new TextBlock
			{
				Text = item?.LayoutElement?.Title,
				Margin = new Thickness(4, 2),
				TextTrimming = TextTrimming.CharacterEllipsis,
			});

			_anchorableListBox = new ListBox { ItemsSource = Anchorables, ItemTemplate = itemTemplate, MaxHeight = 400, MinWidth = 160, Name = "PART_AnchorableListBox" };
			_documentListBox = new ListBox { ItemsSource = Documents, ItemTemplate = itemTemplate, MaxHeight = 400, MinWidth = 220, Name = "PART_DocumentListBox" };
			_anchorableListBox.PointerReleased += (s, e) => CloseAndActivateSelected();
			_documentListBox.PointerReleased += (s, e) => CloseAndActivateSelected();

			var grid = new Grid { Margin = new Thickness(8), ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,*") };
			var anchorablesLabel = new TextBlock { Text = Properties.Resources.Active_ToolWindows, FontWeight = FontWeight.Bold, Margin = new Thickness(4) };
			var documentsLabel = new TextBlock { Text = Properties.Resources.Active_Files, FontWeight = FontWeight.Bold, Margin = new Thickness(4) };
			Grid.SetColumn(documentsLabel, 1);
			Grid.SetRow(_anchorableListBox, 1);
			Grid.SetRow(_documentListBox, 1);
			Grid.SetColumn(_documentListBox, 1);
			grid.Children.Add(anchorablesLabel);
			grid.Children.Add(documentsLabel);
			grid.Children.Add(_anchorableListBox);
			grid.Children.Add(_documentListBox);
			Content = grid;

			// if there are multiple documents, select the next document; with one, select it; with none, select the first anchorable.
			if (Documents.Length > 1) SelectDocument(Documents[1]);
			else if (Documents.Length == 1) SelectDocument(Documents[0]);
			else if (Anchorables.Length > 0) SelectAnchorable(Anchorables[0]);

			Deactivated += (s, e) => { if (!_closing) Close(); };
		}

		/// <summary>Gets the listed documents, most recently used first.</summary>
		public LayoutDocumentItem[] Documents { get; }

		/// <summary>Gets the listed anchorables, most recently used first.</summary>
		public LayoutAnchorableItem[] Anchorables { get; }

		/// <summary>Gets the selected document, if a document is selected.</summary>
		public LayoutDocumentItem SelectedDocument => _isSelectingDocument ? _documentListBox.SelectedItem as LayoutDocumentItem : null;

		/// <summary>Gets the selected anchorable, if an anchorable is selected.</summary>
		public LayoutAnchorableItem SelectedAnchorable => _isSelectingDocument ? null : _anchorableListBox.SelectedItem as LayoutAnchorableItem;

		/// <inheritdoc/>
		protected override Type StyleKeyOverride => typeof(Window);

		/// <summary>Selects the next entry of the current list.</summary>
		internal void SelectNext() => Move(+1);

		/// <inheritdoc/>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Tab:
				case Key.Down:
					Move(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : +1);
					e.Handled = true;
					break;
				case Key.Up:
					Move(-1);
					e.Handled = true;
					break;
				case Key.Left:
				case Key.Right:
					if (_isSelectingDocument && Anchorables.Length > 0)
						SelectAnchorable(Anchorables.ElementAtOrDefault(_documentListBox.SelectedIndex) ?? Anchorables.Last());
					else if (!_isSelectingDocument && Documents.Length > 0)
						SelectDocument(Documents.ElementAtOrDefault(_anchorableListBox.SelectedIndex) ?? Documents.Last());
					e.Handled = true;
					break;
				case Key.Escape:
					_closing = true;
					Close();
					e.Handled = true;
					break;
			}

			if (!e.Handled) base.OnKeyDown(e);
		}

		/// <inheritdoc/>
		protected override void OnKeyUp(KeyEventArgs e)
		{
			if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
			{
				CloseAndActivateSelected();
				e.Handled = true;
			}

			base.OnKeyUp(e);
		}

		private void Move(int offset)
		{
			if (_isSelectingDocument)
			{
				if (Documents.Length == 0) return;
				var index = (_documentListBox.SelectedIndex + offset + Documents.Length) % Documents.Length;
				SelectDocument(Documents[index]);
			}
			else
			{
				if (Anchorables.Length == 0) return;
				var index = (_anchorableListBox.SelectedIndex + offset + Anchorables.Length) % Anchorables.Length;
				SelectAnchorable(Anchorables[index]);
			}
		}

		private void SelectDocument(LayoutDocumentItem document)
		{
			_isSelectingDocument = true;
			_anchorableListBox.SelectedItem = null;
			_documentListBox.SelectedItem = document;
		}

		private void SelectAnchorable(LayoutAnchorableItem anchorable)
		{
			_isSelectingDocument = false;
			_documentListBox.SelectedItem = null;
			_anchorableListBox.SelectedItem = anchorable;
		}

		private void CloseAndActivateSelected()
		{
			if (_closing) return;
			_closing = true;
			LayoutItem selected = (LayoutItem)SelectedDocument ?? SelectedAnchorable;
			Close();
			if (selected?.ActivateCommand?.CanExecute(null) == true) selected.ActivateCommand.Execute(null);
		}
	}
}
