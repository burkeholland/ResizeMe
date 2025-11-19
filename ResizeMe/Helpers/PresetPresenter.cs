using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ResizeMe.Models;
using ResizeMe.Services;
using System.Linq;
using System;

namespace ResizeMe.Helpers
{
    internal class PresetPresenter
    {
        private readonly PresetManager _presetManager;
        public PresetPresenter(PresetManager presetManager)
        {
            _presetManager = presetManager;
        }

        public void RenderPresets(Panel panel, ResourceDictionary? resources, RoutedEventHandler clickHandler)
        {
            if (panel == null) return;
            panel.Children.Clear();
            foreach (var preset in _presetManager.Presets)
            {
                var btn = PresetButtonBuilder.Build(preset, resources, clickHandler);
                panel.Children.Add(btn);
            }
            if (!_presetManager.Presets.Any())
            {
                // caller should handle resetting index
            }
            else
            {
                FocusPreset(panel, 0);
            }
        }

        public int FocusPreset(Panel panel, int index)
        {
            if (panel == null) return -1;
            var buttons = panel.Children.OfType<Button>().ToList();
            if (!buttons.Any()) return -1;
            if (index < 0) index = 0;
            if (index >= buttons.Count) index = buttons.Count - 1;
            var target = buttons[index];
            target.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            return index;
        }

        public void UpdateActivePreset(Panel panel, string sizeTag)
        {
            if (panel == null) return;
            var resources = Application.Current?.Resources;
            if (resources == null) return;
            if (!resources.TryGetValue("ActivePresetButtonStyle", out var activeObj) || !(activeObj is Style activeStyle)) return;
            if (!resources.TryGetValue("PresetButtonBaseStyle", out var baseObj) || !(baseObj is Style baseStyle)) return;
            foreach (var child in panel.Children.OfType<Button>())
            {
                if (child.Tag is string tag && tag == sizeTag) child.Style = activeStyle; else child.Style = baseStyle;
            }
        }
    }
}
