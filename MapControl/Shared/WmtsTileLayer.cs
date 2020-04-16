﻿// XAML Map Control - https://github.com/ClemensFischer/XAML-Map-Control
// © 2020 Clemens Fischer
// Licensed under the Microsoft Public License (Ms-PL)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if WINDOWS_UWP
using Windows.Foundation;
using Windows.UI.Xaml;
#else
using System.Windows;
#endif

namespace MapControl
{
    public class WmtsTileLayer : MapTileLayerBase
    {
        public static readonly DependencyProperty CapabilitiesUriProperty = DependencyProperty.Register(
            nameof(CapabilitiesUri), typeof(Uri), typeof(WmtsTileLayer),
            new PropertyMetadata(null, (o, e) => ((WmtsTileLayer)o).TileMatrixSets.Clear()));

        public static readonly DependencyProperty LayerIdentifierProperty = DependencyProperty.Register(
            nameof(LayerIdentifier), typeof(string), typeof(WmtsTileLayer), new PropertyMetadata(null));

        public WmtsTileLayer()
            : this(new TileImageLoader())
        {
        }

        public WmtsTileLayer(ITileImageLoader tileImageLoader)
            : base(tileImageLoader)
        {
            IsHitTestVisible = false;

            Loaded += OnLoaded;
        }

        public Uri CapabilitiesUri
        {
            get { return (Uri)GetValue(CapabilitiesUriProperty); }
            set { SetValue(CapabilitiesUriProperty, value); }
        }

        public string LayerIdentifier
        {
            get { return (string)GetValue(LayerIdentifierProperty); }
            set { SetValue(LayerIdentifierProperty, value); }
        }

        public IEnumerable<WmtsTileMatrixLayer> ChildLayers
        {
            get { return Children.Cast<WmtsTileMatrixLayer>(); }
        }

        public Dictionary<string, WmtsTileMatrixSet> TileMatrixSets { get; } = new Dictionary<string, WmtsTileMatrixSet>();

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var layer in ChildLayers)
            {
                layer.Measure(availableSize);
            }

            return new Size();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (var layer in ChildLayers)
            {
                layer.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            }

            return finalSize;
        }

        protected override void TileSourcePropertyChanged()
        {
            UpdateTileLayer();
        }

        protected override void UpdateTileLayer()
        {
            UpdateTimer.Stop();

            if (ParentMap == null ||
                !TileMatrixSets.TryGetValue(ParentMap.MapProjection.CrsId, out WmtsTileMatrixSet tileMatrixSet))
            {
                Children.Clear();
                UpdateTiles(null);
            }
            else if (UpdateChildLayers(tileMatrixSet))
            {
                SetRenderTransform();
                UpdateTiles(tileMatrixSet);
            }
        }

        protected override void SetRenderTransform()
        {
            foreach (var layer in ChildLayers)
            {
                layer.SetRenderTransform(ParentMap.ViewTransform);
            }
        }

        private bool UpdateChildLayers(WmtsTileMatrixSet tileMatrixSet)
        {
            var layersChanged = false;
            var maxScale = 1.001 * ParentMap.ViewTransform.Scale; // avoid rounding issues

            // show all TileMatrix layers with Scale <= maxScale, at least the first layer
            //
            var currentMatrixes = tileMatrixSet.TileMatrixes
                .Where((matrix, i) => i == 0 || matrix.Scale <= maxScale)
                .ToList();

            if (this != ParentMap.MapLayer) // do not load background tiles
            {
                currentMatrixes = currentMatrixes.Skip(currentMatrixes.Count - 1).ToList(); // last element only
            }
            else if (currentMatrixes.Count > MaxBackgroundLevels + 1)
            {
                currentMatrixes = currentMatrixes.Skip(currentMatrixes.Count - MaxBackgroundLevels - 1).ToList();
            }

            var currentLayers = ChildLayers.Where(layer => currentMatrixes.Contains(layer.TileMatrix)).ToList();

            Children.Clear();

            foreach (var tileMatrix in currentMatrixes)
            {
                var layer = currentLayers.FirstOrDefault(l => l.TileMatrix == tileMatrix);

                if (layer == null)
                {
                    layer = new WmtsTileMatrixLayer(tileMatrix, tileMatrixSet.TileMatrixes.IndexOf(tileMatrix));
                    layersChanged = true;
                }

                if (layer.SetBounds(ParentMap.ViewTransform, ParentMap.RenderSize))
                {
                    layersChanged = true;
                }

                Children.Add(layer);
            }

            return layersChanged;
        }

        private void UpdateTiles(WmtsTileMatrixSet tileMatrixSet)
        {
            var tiles = new List<Tile>();

            foreach (var layer in ChildLayers)
            {
                layer.UpdateTiles();
                tiles.AddRange(layer.Tiles);
            }

            var tileSource = TileSource as WmtsTileSource;
            var sourceName = SourceName;

            if (tileSource != null && tileMatrixSet != null)
            {
                tileSource.TileMatrixSet = tileMatrixSet;

                if (sourceName != null)
                {
                    sourceName += "/" + tileMatrixSet.Identifier;
                }
            }

            TileImageLoader.LoadTilesAsync(tiles, tileSource, sourceName);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (TileMatrixSets.Count == 0 && CapabilitiesUri != null)
            {
                try
                {
                    var capabilities = await WmtsCapabilities.ReadCapabilities(CapabilitiesUri, LayerIdentifier);

                    TileMatrixSets.Clear();
                    capabilities.TileMatrixSets.ForEach(s => TileMatrixSets.Add(s.SupportedCrs, s));

                    LayerIdentifier = capabilities.LayerIdentifier;
                    TileSource = capabilities.TileSource;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("WmtsTileLayer: {0}: {1}", CapabilitiesUri, ex.Message);
                }
            }
        }
    }
}
