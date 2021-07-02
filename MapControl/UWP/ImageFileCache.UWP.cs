﻿// XAML Map Control - https://github.com/ClemensFischer/XAML-Map-Control
// © 2021 Clemens Fischer
// Licensed under the Microsoft Public License (Ms-PL)

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MapControl.Caching
{
    public partial class ImageFileCache : IImageCache
    {
        public async Task<ImageCacheItem> GetAsync(string key)
        {
            ImageCacheItem cacheItem = null;
            var path = GetPath(key);

            try
            {
                if (path != null && File.Exists(path))
                {
                    var buffer = await File.ReadAllBytesAsync(path);
                    var expiration = ReadExpiration(ref buffer);

                    cacheItem = new ImageCacheItem
                    {
                        Buffer = buffer,
                        Expiration = expiration
                    };

                    //Debug.WriteLine("ImageFileCache: Read {0}, Expires {1}", path, expiration.ToLocalTime());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ImageFileCache: Failed reading {0}: {1}", path, ex.Message);
            }

            return cacheItem;
        }

        public async Task SetAsync(string key, ImageCacheItem cacheItem)
        {
            var path = GetPath(key);

            if (cacheItem.Buffer != null && cacheItem.Buffer.Length > 0 && path != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                    using (var stream = File.Create(path))
                    {
                        await stream.WriteAsync(cacheItem.Buffer, 0, cacheItem.Buffer.Length);
                        await WriteExpirationAsync(stream, cacheItem.Expiration);
                    }

                    //Debug.WriteLine("ImageFileCache: Wrote {0}, Expires {1}", path, expiration.ToLocalTime());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ImageFileCache: Failed writing {0}: {1}", path, ex.Message);
                }
            }
        }
    }
}
