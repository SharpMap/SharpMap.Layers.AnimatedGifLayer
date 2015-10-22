// Copyright 2015 - Felix Obermaier (www.ivv-aachen.de)
//
// This file is part of SharpMap.Layers.AnimatedGif.
// SharpMap.Layers.AnimatedGif is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// SharpMap.Layers.AnimatedGif is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with SharpMap; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA 


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using GeoAPI.Geometries;
using SharpMap.Data;
using SharpMap.Data.Providers;
using SharpMap.Forms;
using SharpMap.Styles;

namespace SharpMap.Layers
{
	/// <summary>
	/// Description of AnimatedGifLayer.
	/// </summary>
	public class AnimatedGifLayer : Layer, ICanQueryLayer
	{
	    private class TransparentPicBox : PictureBox
	    {
	        protected override void WndProc(ref Message m)
	        {
	            // ReSharper disable InconsistentNaming
                const int WM_NCHITTEST = 0x0084;
                const int HTTRANSPARENT = (-1);
                // ReSharper enable InconsistentNaming

                if (m.Msg == WM_NCHITTEST)
                {
                    m.Result = (IntPtr)HTTRANSPARENT;
                }
                else
                {
                    base.WndProc(ref m);
                }
            }
	    }
        
        private readonly object _renderLock = new object();
        private MapBox _mapBox;
        private Image _animatedGif;
		private readonly IProvider _provider;
	    private readonly Dictionary<uint, PictureBox> _pictureBoxes; 


        /// <summary>
        /// Creates an instance of this class
        /// </summary>
        /// <param name="layername">The layer name</param>
        /// <param name="provider">The provider</param>
        public AnimatedGifLayer(string layername, IProvider provider)
		{
            var rs = Assembly.GetExecutingAssembly().GetManifestResourceStream("SharpMap.Layers.GreenDot.gif");

			if (rs != null)
			{	_animatedGif = Image.FromStream(rs);
				//rs.Dispose();
			}
			
			LayerName = layername;
			_provider = provider;
            SourceFactory = new NetTopologySuite.Geometries.GeometryFactory(
                NetTopologySuite.Geometries.GeometryFactory.Default.PrecisionModel,_provider.SRID,
                NetTopologySuite.Geometries.GeometryFactory.Default.CoordinateSequenceFactory                );

            _pictureBoxes = new Dictionary<uint, PictureBox>();
		}
		
		/// <summary>
		/// Event raised when the <see cref="AnimatedGif"/> has changed
		/// </summary>
		public event EventHandler AnimatedGifChanged;

        /// <summary>
        /// Event invoker for the <see cref="AnimatedGifChanged"/> event.
        /// </summary>
        /// <param name="e">The event's arguments</param>
        protected virtual void OnAnimatedGifChanged(EventArgs e)
        {
            lock (_renderLock)
            {
                var gif = _animatedGif;
                _mapBox.Invoke(new MethodInvoker(
                    delegate
                    {
                        foreach (var ctrl in _mapBox.Controls)
                        {
                            if (!(ctrl is PictureBox)) continue;
                            var pb = (PictureBox)ctrl;
                            if (!(pb.Name.StartsWith("smpic"))) continue;
                            var oldImage = pb.Image;
                            pb.Image = gif != null ? (Image)gif.Clone() : null;
                            if (oldImage != null) oldImage.Dispose();
                            pb.Visible = pb.Image != null;
                        }
                    }
                    ));
            }
            var handler = AnimatedGifChanged;
            if (handler != null) handler(this, e);
        }


        /// <summary>
        /// Event raised when the <see cref="MapBox"/> is about to change
        /// </summary>
        public event EventHandler<CancelEventArgs> MapControlChanging;

        /// <summary>
        /// Event invoker for the <see cref="MapControlChanging"/> event.
        /// </summary>
        /// <param name="cea">The event's arguments</param>
        protected virtual void OnMapControlChanging(CancelEventArgs cea)
        {
            var handler = MapControlChanging;
            if (handler != null) handler(this, cea);

            if (!cea.Cancel)
            {
                if (_mapBox != null)
                {
                    _mapBox.MapZooming -= HandleMapZooming;
                    _mapBox.MapCenterChanged -= HandleMapCenterChanged;
                }

                foreach (var value in _pictureBoxes.Values)
                {
                    var value1 = value;
                    new MethodInvoker(() => _mapBox.Controls.Remove(value1)).Invoke();
                }
            }
        }

	    /// <summary>
        /// Event raised when the <see cref="MapBox"/> has changed
        /// </summary>
        public event EventHandler MapControlChanged;

        /// <summary>
        /// Event invoker for the <see cref="OnMapControlChanged"/> event.
        /// </summary>
        /// <param name="e">The event's arguments</param>
        protected virtual void OnMapControlChanged(EventArgs e)
        {
            foreach (var value in _pictureBoxes.Values)
            {
                var value1 = value;
                new MethodInvoker(() => _mapBox.Controls.Add(value1)).Invoke();
            }

            _mapBox.MapZooming += HandleMapZooming;
            _mapBox.MapCenterChanged += HandleMapCenterChanged;

            var handler = MapControlChanged;
            if (handler != null) handler(this, e);
        }

	    private void HandleMapZooming(double zoom)
	    {
	        DisplayPictureBoxes(false);
	    }
        private void HandleMapCenterChanged(Coordinate center)
        {
            DisplayPictureBoxes(false);
        }


	    /// <summary>
		/// Gets or sets a value indicating the animated gif
		/// </summary>
		public Image AnimatedGif 
		{ 
			get { return _animatedGif; }
			set { 
				if (value == null) throw new ArgumentNullException("value");
				if (!ImageAnimator.CanAnimate(value))
					throw new ArgumentException("Not an image with multiple frames", "value");
				_animatedGif = value;
				OnAnimatedGifChanged(EventArgs.Empty);
			}
		}
		
        /// <summary>
        /// Gets or sets a value indicating the <see cref="SharpMap.Forms.MapBox"/> control this layer works with
        /// </summary>
	    public MapBox MapControl
	    {
	        get
	        {
	            return _mapBox;
	        }
	        set
	        {
	            if (value == _mapBox)
	                return;
	            var cea = new CancelEventArgs();
                OnMapControlChanging(cea);
                if (cea.Cancel) return;
	            _mapBox = value;
	            OnMapControlChanged(EventArgs.Empty);
	        }
	    }

	    /// <summary>
        /// Gets a value indicating the data source
        /// </summary>
		public IProvider DataSource { get { return _provider; } }

	    /// <summary>
	    /// Method to render the layer
	    /// </summary>
	    /// <param name="g">The graphics object</param>
	    /// <param name="map">The map</param>
		public override void Render(Graphics g, Map map)
	    {
	        if (!Enabled)
	        {
	            DisplayPictureBoxes(false);
                return;
	        }
            
            var env = this.ToSource(map.Envelope);
            _provider.Open();
            var points = _provider.GetObjectIDsInView(env);
	        var validBoxes = new HashSet<uint>();
            Monitor.Enter(_renderLock);
            if (points != null && points.Count > 0)
	        {
                _mapBox.Invoke(new MethodInvoker(() => _mapBox.SuspendLayout()));
	            var gif = (Bitmap)_animatedGif.Clone();
	            var gifSize = gif.Size;
	            gif.SelectActiveFrame(FrameDimension.Page, 0);
                foreach (var oid in points)
	            {
	                if (!_pictureBoxes.ContainsKey(oid))
	                {
	                    //var pic = new PictureBox {Image = _animatedGif, AutoSize = true, Name = string.Format("smpic{0}", oid)};
	                    //new MethodInvoker(() =>_mapBox.Controls.Add(pic)).Invoke();
	                    var oid1 = oid;
	                    var ctname = string.Format("smpic{0}", oid1);
	                    _mapBox.Invoke(
	                        new MethodInvoker(delegate
	                        {
                                var pic = new TransparentPicBox
	                            {
	                                Image = (Image) _animatedGif.Clone(),
	                                AutoSize = true,
	                                Name = ctname
                                    , Size = _animatedGif.Size
                                    , BackColor = Color.Transparent
                                    
	                            };

                                //((Bitmap)pic).MakeTransparent(Color.Black);
	                            _mapBox.Controls.Add(pic);
	                        }));
	                    //Thread.Sleep(5);
	                    _pictureBoxes.Add(oid, (PictureBox) _mapBox.Controls[ctname]);
	                }

	                var point = this.ToTarget(_provider.GetGeometryByID(oid), map.Factory).Coordinate;
	                var np = Point.Subtract(Point.Truncate(map.WorldToImage(point)), gifSize);

                    // Draw on the map
                    g.DrawImageUnscaled(gif, new Rectangle(np, gifSize));

                    var picbox = _pictureBoxes[oid];
	                _mapBox.Invoke(new MethodInvoker(delegate
	                {
	                    picbox.Location = np;
	                    picbox.Visible = true;
	                }
	                    ));
	                validBoxes.Add(oid);

	            }
	        }

	        // Close the connection
            _provider.Close();

            // Disable pictureboxes that are not in viewport
	        foreach (var picbox in _pictureBoxes)
	        {
	            if (!validBoxes.Contains(picbox.Key))
	            {
	                var p = picbox.Value;
                    _mapBox.Invoke(new MethodInvoker(() =>  p.Visible = false));
	            }
	        }

            _mapBox.Invoke(new MethodInvoker(() => _mapBox.ResumeLayout()));
            Monitor.Exit(_renderLock);
			base.Render(g, map);

		}

        [MethodImpl(MethodImplOptions.Synchronized)]
	    private void DisplayPictureBoxes(bool display)
	    {
            _mapBox.Invoke(new MethodInvoker(delegate
            {
                var pba = new PictureBox[_pictureBoxes.Count];
                _pictureBoxes.Values.CopyTo(pba, 0);
                for (var i = 0; i < pba.Length; i++)
                    pba[i].Visible = display;
            }));
        }

	    /// <summary>
	    /// Method called when <see cref="T:SharpMap.Styles.Style"/> has changed, to invoke <see cref="E:SharpMap.Layers.Layer.StyleChanged"/>
	    /// </summary>
	    /// <param name="eventArgs">The arguments associated with the event</param>
	    protected override void OnStyleChanged(EventArgs eventArgs)
	    {
	        foreach (var pb in _pictureBoxes.Values)
	        {
	            var visible = Enabled;
	            if (visible)
	            {
#if SharpMap_1_1
                    var compare = this.VisibilityUnits() == VisibilityUnits.ZoomLevel ? _mapBox.Map.Zoom : _mapBox.Map.MapScale;
#else
                    var compare = this.VisibilityUnits == VisibilityUnits.ZoomLevel ? _mapBox.Map.Zoom : _mapBox.Map.MapScale;
#endif
	                visible = MaxVisible >= compare && MinVisible < compare;
	            }

                if (pb.Visible != visible)
	            {
	                var p = pb;
	                _mapBox.Invoke(new MethodInvoker(() => p.Visible = visible));
	            }

	        }
            base.OnStyleChanged(eventArgs);
	    }

	    /// <summary>
	    /// Returns the extent of the layer
	    /// </summary>
	    /// <returns>Bounding box corresponding to the extent of the features in the layer</returns>
	    public override Envelope Envelope
	    {
	        get { return this.ToTarget(_provider.GetExtents()); }
	    }

	    /// <summary>
	    /// Releases managed resources
	    /// </summary>
	    protected override void ReleaseManagedResources()
	    {
	        _mapBox.Invoke(new MethodInvoker(delegate
	        {
	            foreach (var value in _pictureBoxes.Values.ToArray())
                {
                    _mapBox.Controls.Remove(value);
                    value.Dispose();
                }
            }
            ));
            base.ReleaseManagedResources();
	    }

	    public void ExecuteIntersectionQuery(Envelope box, FeatureDataSet ds)
	    {
	        if (!IsQueryEnabled) return;

            var tmp = this.ToSource(box);
	        var numTables = ds.Tables.Count;
            DataSource.ExecuteIntersectionQuery(tmp, ds);

            if (numTables < ds.Tables.Count)
	        {
	            foreach (FeatureDataRow fdr in ds.Tables[numTables-1].Rows)
	                fdr.Geometry = this.ToTarget(fdr.Geometry, MapControl.Map.Factory);
	        }
	    }

	    public void ExecuteIntersectionQuery(IGeometry geometry, FeatureDataSet ds)
	    {
            if (!IsQueryEnabled) return;

            var geom = this.ToSource(geometry, SourceFactory);
            var numTables = ds.Tables.Count;
            DataSource.ExecuteIntersectionQuery(geom, ds);

            if (numTables < ds.Tables.Count)
            {
                foreach (FeatureDataRow fdr in ds.Tables[numTables - 1].Rows)
                    fdr.Geometry = this.ToTarget(fdr.Geometry, MapControl.Map.Factory);
            }
        }

	    public bool IsQueryEnabled { get; set; }

#if SharpMap_1_1
        private IGeometryFactory SourceFactory { get; set; }
        //private IGeometryFactory TargetFactory { get; set; }
#endif

	}
}
