﻿// XAML Map Control - https://github.com/ClemensFischer/XAML-Map-Control
// © 2021 Clemens Fischer
// Licensed under the Microsoft Public License (Ms-PL)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MapControl
{
    namespace Caching
    {
        public class ImageCacheItem
        {
            public byte[] Buffer { get; set; }
            public DateTime Expiration { get; set; }
        }
    }

#if NETFRAMEWORK
    static class ConcurrentQueueEx
    {
        public static void Clear<T>(this ConcurrentQueue<T> tileQueue)
        {
            while (tileQueue.TryDequeue(out _)) ;
        }
    }
#endif

    /// <summary>
    /// Loads and optionally caches map tile images for a MapTileLayer.
    /// </summary>
    public partial class TileImageLoader : ITileImageLoader
    {
        /// <summary>
        /// Maximum number of parallel tile loading tasks. The default value is 4.
        /// </summary>
        public static int MaxLoadTasks { get; set; } = 4;

        /// <summary>
        /// Default expiration time for cached tile images. Used when no expiration time
        /// was transmitted on download. The default value is one day.
        /// </summary>
        public static TimeSpan DefaultCacheExpiration { get; set; } = TimeSpan.FromDays(1);

        /// <summary>
        /// Maximum expiration time for cached tile images. A transmitted expiration time
        /// that exceeds this value is ignored. The default value is ten days.
        /// </summary>
        public static TimeSpan MaxCacheExpiration { get; set; } = TimeSpan.FromDays(10);

        private readonly ConcurrentQueue<Tile> tileQueue = new ConcurrentQueue<Tile>();
        private TileSource tileSource;
        private string cacheName;
        private int taskCount;

        /// <summary>
        /// Loads all pending tiles from the tiles collection.
        /// If source.UriFormat starts with "http" and cache is a non-empty string,
        /// tile images will be cached in the TileImageLoader's Cache (if that is not null).
        /// </summary>
        public void LoadTiles(IEnumerable<Tile> tiles, TileSource source, string cache)
        {
            tileQueue.Clear();

            tiles = tiles.Where(tile => tile.Pending);

            tileSource = source;
            cacheName = null;

            if (tiles.Any() && tileSource != null)
            {
                if (!string.IsNullOrEmpty(cache) &&
                    Cache != null &&
                    tileSource.UriFormat != null &&
                    tileSource.UriFormat.StartsWith("http"))
                {
                    cacheName = cache;
                }

                foreach (var tile in tiles)
                {
                    tileQueue.Enqueue(tile);
                }

                while (taskCount < Math.Min(tileQueue.Count, MaxLoadTasks))
                {
                    Interlocked.Increment(ref taskCount);

                    Task.Run(LoadTilesFromQueueAsync);
                }
            }
        }

        private async Task LoadTilesFromQueueAsync()
        {
            // tileSource or cacheName may change after dequeuing a tile
            var source = tileSource;
            var cache = cacheName;

            while (tileQueue.TryDequeue(out Tile tile))
            {
                tile.Pending = false;

                try
                {
                    await LoadTileAsync(tile, source, cache).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("TileImageLoader: {0}/{1}/{2}: {3}", tile.ZoomLevel, tile.XIndex, tile.Y, ex.Message);
                }
            }

            Interlocked.Decrement(ref taskCount);
        }

        private static Task LoadTileAsync(Tile tile, TileSource tileSource, string cacheName)
        {
            if (cacheName == null)
            {
                return LoadTileAsync(tile, tileSource);
            }

            var uri = tileSource.GetUri(tile.XIndex, tile.Y, tile.ZoomLevel);

            if (uri == null)
            {
                return Task.CompletedTask;
            }

            var extension = Path.GetExtension(uri.LocalPath);

            if (string.IsNullOrEmpty(extension) || extension == ".jpeg")
            {
                extension = ".jpg";
            }

            var cacheKey = string.Format("{0}/{1}/{2}/{3}{4}", cacheName, tile.ZoomLevel, tile.XIndex, tile.Y, extension);

            return LoadCachedTileAsync(tile, uri, cacheKey);
        }

        private static DateTime GetExpiration(TimeSpan? maxAge)
        {
            if (!maxAge.HasValue)
            {
                maxAge = DefaultCacheExpiration;
            }
            else if (maxAge.Value > MaxCacheExpiration)
            {
                maxAge = MaxCacheExpiration;
            }

            return DateTime.UtcNow.Add(maxAge.Value);
        }
    }
}
