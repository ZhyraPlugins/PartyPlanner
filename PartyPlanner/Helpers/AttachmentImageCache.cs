using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PartyPlanner.Helpers;

/// <summary>
/// Downloads and caches attachment images from partake.gg CDN as GPU textures.
/// </summary>
public sealed class AttachmentImageCache : IDisposable
{
    private enum LoadState { Pending, Loaded, Failed }

    private sealed class Entry
    {
        public LoadState State = LoadState.Pending;
        public IDalamudTextureWrap? Texture;
    }

    private readonly Dictionary<string, Entry> _cache = new();
    private readonly object _lock = new();
    private readonly HttpClient _http;
    private readonly ITextureProvider _textureProvider;
    private readonly CancellationTokenSource _cts = new();

    public AttachmentImageCache(ITextureProvider textureProvider)
    {
        _textureProvider = textureProvider;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Dalamud-PartyPlanner");
    }

    /// <summary>
    /// Returns the loaded texture for the given URL, or null if still loading or failed.
    /// Kicks off a download if this URL hasn't been seen before.
    /// </summary>
    public IDalamudTextureWrap? TryGet(string url)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(url, out var entry))
                return entry.State == LoadState.Loaded ? entry.Texture : null;

            var newEntry = new Entry();
            _cache[url] = newEntry;
            _ = LoadAsync(url, newEntry);
            return null;
        }
    }

    private async Task LoadAsync(string url, Entry entry)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(url, _cts.Token).ConfigureAwait(false);
            var tex = await _textureProvider.CreateFromImageAsync(bytes, url, _cts.Token).ConfigureAwait(false);
            lock (_lock)
            {
                entry.Texture = tex;
                entry.State = LoadState.Loaded;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Plugin.Logger.Warning(ex, "Failed to load attachment image: {0}", url);
            lock (_lock) { entry.State = LoadState.Failed; }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var entry in _cache.Values)
                entry.Texture?.Dispose();
            _cache.Clear();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        Clear();
        _http.Dispose();
    }
}
