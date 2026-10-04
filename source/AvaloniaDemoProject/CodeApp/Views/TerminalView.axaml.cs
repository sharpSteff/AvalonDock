using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ToggleTestApp.ViewModels;

namespace ToggleTestApp.Views;

public partial class TerminalView : UserControl
{
	private TerminalViewModel? _viewModel;

	public TerminalView()
	{
		InitializeComponent();

		// Tunnel, like WPF's OnPreviewKeyDown, so the text box does not see the Enter key first.
		AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
	}

	protected override void OnDataContextChanged(EventArgs e)
	{
		base.OnDataContextChanged(e);

		if (_viewModel != null)
			_viewModel.PropertyChanged -= OnTerminalPropertyChanged;

		_viewModel = DataContext as TerminalViewModel;

		if (_viewModel != null)
			_viewModel.PropertyChanged += OnTerminalPropertyChanged;
	}

	private void OnTerminalPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(TerminalViewModel.Output))
		{
			// After the layout pass that measures the new text.
			Dispatcher.UIThread.Post(OutputScroller.ScrollToEnd, DispatcherPriority.Background);
		}
	}

	private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter && DataContext is TerminalViewModel vm)
		{
			vm.SendCommandCommand.Execute(null);
			e.Handled = true;
		}
	}
}