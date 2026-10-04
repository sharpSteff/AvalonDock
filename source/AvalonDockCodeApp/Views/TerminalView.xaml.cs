#if AVALONIA
using System;
#endif
using System.ComponentModel;
#if AVALONIA
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
#else
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
#endif
using ToggleTestApp.ViewModels;

namespace ToggleTestApp.Views;

public partial class TerminalView : UserControl
{
#if AVALONIA
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
#else
	public TerminalView()
	{
		InitializeComponent();
		DataContextChanged += OnDataContextChanged;
	}

	private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (e.OldValue is TerminalViewModel oldVm)
			oldVm.PropertyChanged -= OnTerminalPropertyChanged;

		if (e.NewValue is TerminalViewModel newVm)
			newVm.PropertyChanged += OnTerminalPropertyChanged;
	}
#endif

	private void OnTerminalPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(TerminalViewModel.Output))
		{
#if AVALONIA
			// After the layout pass that measures the new text.
			Dispatcher.UIThread.Post(OutputScroller.ScrollToEnd, DispatcherPriority.Background);
#else
			OutputScroller.ScrollToEnd();
#endif
		}
	}

#if AVALONIA
	private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
#else
	protected override void OnPreviewKeyDown(KeyEventArgs e)
#endif
	{
		if (e.Key == Key.Enter && DataContext is TerminalViewModel vm)
		{
			vm.SendCommandCommand.Execute(null);
			e.Handled = true;
		}
#if !AVALONIA

		base.OnPreviewKeyDown(e);
#endif
	}
}