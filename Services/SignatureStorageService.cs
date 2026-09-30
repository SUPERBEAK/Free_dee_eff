using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace Freedeeeff.Services
{
    public class SavedSignature
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "My Signature";
        public DateTime Created { get; set; } = DateTime.Now;
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();

        private BitmapSource? _cachedBitmap;
        public BitmapSource? PreviewImage
        {
            get
            {
                if (_cachedBitmap == null && ImageBytes.Length > 0)
                {
                    try
                    {
                        using var ms = new MemoryStream(ImageBytes);
                        var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        _cachedBitmap = decoder.Frames[0];
                        _cachedBitmap.Freeze();
                    }
                    catch { }
                }
                return _cachedBitmap;
            }
        }
    }

    public class SignatureStorageService
    {
        private readonly string _storageDir;
        private readonly string _indexFile;

        public SignatureStorageService()
        {
            _storageDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Freedeeeff",
                "Signatures");

            _indexFile = Path.Combine(_storageDir, "signatures.json");
            Directory.CreateDirectory(_storageDir);
        }

        public List<SavedSignature> GetSavedSignatures()
        {
            if (!File.Exists(_indexFile)) return new List<SavedSignature>();

            try
            {
                string json = File.ReadAllText(_indexFile);
                var items = JsonSerializer.Deserialize<List<SavedSignature>>(json);
                return items ?? new List<SavedSignature>();
            }
            catch
            {
                return new List<SavedSignature>();
            }
        }

        public void SaveSignature(string name, byte[] pngBytes)
        {
            var signatures = GetSavedSignatures();
            var item = new SavedSignature
            {
                Name = string.IsNullOrWhiteSpace(name) ? $"Signature {signatures.Count + 1}" : name,
                ImageBytes = pngBytes,
                Created = DateTime.Now
            };

            signatures.Add(item);
            SaveList(signatures);
        }

        public void DeleteSignature(Guid id)
        {
            var signatures = GetSavedSignatures();
            signatures.RemoveAll(s => s.Id == id);
            SaveList(signatures);
        }

        private void SaveList(List<SavedSignature> signatures)
        {
            try
            {
                string json = JsonSerializer.Serialize(signatures, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_indexFile, json);
            }
            catch { }
        }
    }
}
