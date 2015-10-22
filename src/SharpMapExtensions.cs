using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using GeoAPI.Geometries;
using SharpMap.Layers;

#if !DotSpatialProjections
using GeoAPI.CoordinateSystems.Transformations;
#else
using DotSpatial.Projections;
#endif

namespace SharpMap.Layers
{
#if SharpMap_1_1
    internal static class SharpMapExtensions
    {
        public static Envelope ToSource(this Layer self, Envelope envelope)
        {
#if !DotSpatialProjections
            if (self.ReverseCoordinateTransformation != null)
            {
                return GeometryTransform.TransformBox(envelope, 
                    self.ReverseCoordinateTransformation.MathTransform);
            }
#endif
            if (self.CoordinateTransformation != null)
            {
#if !DotSpatialProjections
                var mt = self.CoordinateTransformation.MathTransform;
                mt.Invert();
                var res = GeometryTransform.TransformBox(envelope, mt);
                mt.Invert();
                return res;
#else
                return GeometryTransform.TransformBox(envelope, CoordinateTransformation.Target, CoordinateTransformation.Source);
#endif
            }

            // no transformation
            return envelope;
        }

        public static IGeometry ToSource(this Layer self, IGeometry geometry, IGeometryFactory sourceFactory)
        {
            if (geometry.SRID == sourceFactory.SRID)
                return geometry;

#if !DotSpatialProjections
            if (self.ReverseCoordinateTransformation != null)
            {
                return GeometryTransform.TransformGeometry(geometry,
                    self.ReverseCoordinateTransformation.MathTransform, sourceFactory);
            }
#endif
            if (self.CoordinateTransformation != null)
            {
#if !DotSpatialProjections
                var mt = self.CoordinateTransformation.MathTransform;
                mt.Invert();
                var res = GeometryTransform.TransformGeometry(geometry, mt, sourceFactory);
                mt.Invert();
                return res;
#else
                return GeometryTransform.TransformGeometry(geometry, 
                    self.CoordinateTransformation.Target, 
                    self.CoordinateTransformation.Source, SourceFactory);
#endif
            }

            return geometry;
        }

        public static Envelope ToTarget(this Layer self, Envelope envelope)
        {
            if (self.CoordinateTransformation == null)
                return envelope;

#if !DotSpatialProjections
            return GeometryTransform.TransformBox(envelope, self.CoordinateTransformation.MathTransform);
#else
            return GeometryTransform.TransformBox(envelope, 
                self.CoordinateTransformation.Source, 
                self.coordinateTransformation.Target);
#endif
        }

        public static IGeometry ToTarget(this Layer self, IGeometry geometry, IGeometryFactory targetFactory)
        {
            if (geometry.SRID == self.TargetSRID)
                return geometry;

            if (self.CoordinateTransformation != null)
            {
#if !DotSpatialProjections
                return GeometryTransform.TransformGeometry(geometry, self.CoordinateTransformation.MathTransform, targetFactory);
#else
                return GeometryTransform.TransformGeometry(geometry, 
                    self.CoordinateTransformation.Source, 
                    self.CoordinateTransformation.Target, TargetFactory);
#endif
            }

            return geometry;
        }

        public static VisibilityUnits VisibilityUnits(this ILayer self)
        {
            return Layers.VisibilityUnits.ZoomLevel;
        }


    }

    internal enum VisibilityUnits
    {
        ZoomLevel,
        MapScale
    }
#endif
}
