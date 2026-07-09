using System.Windows.Controls;
using System.Windows.Data;

namespace AvalonDock.Controls
{
	/// <summary>
	/// Represents the context Menu Ex.
	/// </summary>
	public class ContextMenuEx : ContextMenu
	{
		/// <summary>
		/// Initializes static members of the <see cref="ContextMenuEx"/> class.
		/// </summary>
		static ContextMenuEx()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ContextMenuEx"/> class.
		/// </summary>
		public ContextMenuEx()
		{
		}

		/// <inheritdoc/>
		protected override System.Windows.DependencyObject GetContainerForItemOverride()
		{
			return new MenuItemEx();
		}

		/// <inheritdoc/>
		protected override void OnOpened(System.Windows.RoutedEventArgs e)
		{
			// The ItemsSource binding uses a TemplatedParent RelativeSource that lives outside the
			// context menu's own popup tree, so it may not have been (re)evaluated yet when the menu
			// opens. Refresh it manually - but only when a binding expression actually exists.
			// Cross-platform WPF hosts (e.g. LibreWPF) can open a context menu that has no such
			// binding, in which case GetBindingExpression returns null and the previous unconditional
			// call threw a NullReferenceException, taking down every context menu.
			BindingOperations.GetBindingExpression(this, ItemsSourceProperty)?.UpdateTarget();

			base.OnOpened(e);
		}
	}
}