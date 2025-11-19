using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using ResizeMe.Models;

namespace ResizeMe.Services
{
    /// <summary>
    /// Manages persistent preset sizes stored as JSON in LocalFolder.
    /// </summary>
    public class PresetManager
    {
        private const string FileName = "presets.json";
        private readonly object _syncRoot = new();
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private readonly List<PresetSize> _presets = new();
        private bool _loaded;

        /// <summary>
        /// Indicates whether presets have been loaded from disk.
        /// </summary>
        public bool IsLoaded => _loaded;

        /// <summary>Returns a snapshot of current presets.</summary>
        public IReadOnlyList<PresetSize> Presets
        {
            get { lock (_syncRoot) return _presets.ToList(); }
        }

        /// <summary>Loads presets from disk (idempotent).</summary>
        public async Task LoadAsync(bool forceReload = false)
        {
            if (_loaded && !forceReload) return;
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                StorageFile? file = null;
                try { file = await folder.GetFileAsync(FileName); } catch { }
                if (file == null)
                {
                    SeedDefaults();
                    await SaveAsync();
                    _loaded = true;
                    return;
                }
                string json = await FileIO.ReadTextAsync(file);
                var data = JsonSerializer.Deserialize<List<PresetSize>>(json) ?? new List<PresetSize>();
                lock (_syncRoot)
                {
                    _presets.Clear();
                    _presets.AddRange(data.Where(p => p.IsValid));
                }
                if (!_presets.Any()) SeedDefaults();
                _loaded = true;
            }
            catch
            {
                lock (_syncRoot)
                {
                    _presets.Clear();
                    SeedDefaults();
                }
                _loaded = true;
            }
        }

        /// <summary>Persists presets to disk.</summary>
        public async Task SaveAsync()
        {
            List<PresetSize> snapshot;
            lock (_syncRoot) snapshot = _presets.ToList();
            var folder = ApplicationData.Current.LocalFolder;
            var file = await folder.CreateFileAsync(FileName, CreationCollisionOption.OpenIfExists);
            string json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            await FileIO.WriteTextAsync(file, json);
        }

        /// <summary>Adds a preset if valid and unique.</summary>
        public async Task<bool> AddPresetAsync(PresetSize preset)
        {
            if (preset == null || !preset.IsValid) return false;
            lock (_syncRoot)
            {
                if (_presets.Any(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase))) return false;
                _presets.Add(preset);
            }
            await SaveAsync();
            return true;
        }

        /// <summary>Removes a preset by name.</summary>
        public async Task<bool> RemovePresetAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            bool removed = false;
            lock (_syncRoot)
            {
                var item = _presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (item != null) removed = _presets.Remove(item);
            }
            if (removed) await SaveAsync();
            return removed;
        }

        /// <summary>Resets to default preset list.</summary>
        public async Task ResetToDefaultsAsync()
        {
            lock (_syncRoot)
            {
                _presets.Clear();
                SeedDefaults();
            }
            await SaveAsync();
        }

        private void SeedDefaults()
        {
            _presets.AddRange(new[]
            {
                new PresetSize { Name = "HD", Width = 1280, Height = 720 },
                new PresetSize { Name = "Full HD", Width = 1920, Height = 1080 },
                new PresetSize { Name = "Laptop", Width = 1366, Height = 768 },
                new PresetSize { Name = "Classic", Width = 1024, Height = 768 }
            });
        }
    }
}
