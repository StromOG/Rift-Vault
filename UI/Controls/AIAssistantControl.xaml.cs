using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RiftVault.ViewModels;

namespace RiftVault.UI.Controls
{
    public partial class AIAssistantControl : UserControl
    {
        public event RoutedEventHandler? CloseRequested;

        public AIAssistantControl()
        {
            InitializeComponent();
            DataContextChanged += AIAssistantControl_DataContextChanged;
        }

        private void AIAssistantControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is AIAssistantViewModel oldVm)
            {
                oldVm.Messages.CollectionChanged -= Messages_CollectionChanged;
            }

            if (e.NewValue is AIAssistantViewModel newVm)
            {
                newVm.Messages.CollectionChanged += Messages_CollectionChanged;
            }
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    ChatScrollViewer.ScrollToEnd();
                });
            }
        }

        private void PromptTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                if (DataContext is AIAssistantViewModel vm && vm.SendMessageCommand.CanExecute(null))
                {
                    vm.SendMessageCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, e);
        }

        public void FocusInput()
        {
            PromptTextBox.Focus();
        }
    }
}
