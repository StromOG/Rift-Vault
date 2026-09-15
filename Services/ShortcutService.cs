using System;
using System.Collections.Generic;
using System.Windows.Input;
using RiftVault.Models;

namespace RiftVault.Services
{
    public interface IShortcutService
    {
        void RegisterShortcut(string actionName, Key key, ModifierKeys modifiers, Action action);
        void HandleKeyDown(KeyEventArgs e);
    }

    public class ShortcutService : IShortcutService
    {
        private readonly ISettingsService _settingsService;
        private readonly Dictionary<string, Action> _actions = new();
        private readonly Dictionary<KeyGesture, string> _bindings = new();

        public ShortcutService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            LoadBindings();
        }

        private void LoadBindings()
        {
            // In a full implementation, this parses _settingsService.Current.Hotkeys
            // For now, we register them dynamically via RegisterShortcut
        }

        public void RegisterShortcut(string actionName, Key key, ModifierKeys modifiers, Action action)
        {
            _actions[actionName] = action;
            var gesture = new KeyGesture(key, modifiers);
            _bindings[gesture] = actionName;
        }

        public void HandleKeyDown(KeyEventArgs e)
        {
            foreach (var kvp in _bindings)
            {
                if (kvp.Key.Matches(null, e))
                {
                    if (_actions.TryGetValue(kvp.Value, out var action))
                    {
                        action.Invoke();
                        e.Handled = true;
                        return;
                    }
                }
            }
        }
    }
}