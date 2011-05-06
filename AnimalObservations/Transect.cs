using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ESRI.ArcGIS.Mobile;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    public class Transect
    {
        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("Transects");
        private static readonly Dictionary<string, Transect> Transects = new Dictionary<string, Transect>();

        public Geometry Shape { get; private set; }
        public string Name { get; private set; }

        #region Class Constructors

        static Transect()
        {
            LoadAllTransectsFromDataSource();
        }

        private static void LoadAllTransectsFromDataSource()
        {
            using (FeatureDataReader data = FeatureLayer.GetDataReader(new QueryFilter(), EditState.Original))
            {
                while (data.Read())
                {
                    var transect = new Transect();
                    //If we can't load a transect for some reason (i.e bad geometry, missing values, duplicate name), then skip it
                    try
                    {
                        transect.LoadAttributes(data);
                        Transects[transect.Name] = transect;
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("Read a bad transect. Error Message: {0}" + ex.Message);
                    }
                }
            }
        }

        private void LoadAttributes(FeatureDataReader data)
        {
            Shape = data.GetGeometry();
            Name = data.GetString(data.GetOrdinal("TransectID"));
        }

        #endregion

        #region Instance Constructors

        private Transect()
        {}

        static public Transect FromName(string name)
        {
            return Transects.ContainsKey(name) ? Transects[name] : null;
        }

        #endregion

        #region Lists of Transects

        static public IEnumerable<Transect> GetWithin(Envelope extents)
        {
            if (extents == null)
                return Enumerable.Empty<Transect>();
            var results = from transect in AllTransects
                          where transect.Shape.Intersects(extents)
                          orderby (transect.Name)
                          select transect;
            return results;
        }

        static public IEnumerable<Transect> AllTransects
        {
            get { return Transects.Values; }
        }

        #endregion

        #region bearing calculations

        //Called when creating a birdgroup to correct the boat's heading to the transect heading
        public Azimuth NormalizedAzimuth(GpsPoint gpsData)
        {
            if (gpsData == null)
                throw new ArgumentNullException("gpsData");

            CoordinateCollection vertices = GetTwoClosestVertices(Shape, gpsData.Location);
            Azimuth heading = GetAzimuthFromVertices(vertices);
            //heading will be off by 180 degrees off if traveling from finish to start
            heading = OrientateHeading(heading, gpsData.Bearing);
            return heading;
        }

        //Vertices must be adjacent in shape, i.e. the end points of the closest segment 
        private static CoordinateCollection GetTwoClosestVertices(Geometry shape, Coordinate searchPoint)
        {
            if (shape == null)
                throw new ArgumentNullException("shape");
            if (searchPoint == null)
                throw new ArgumentNullException("searchPoint");
            if (searchPoint.IsEmpty)
                throw new ArgumentException("searchPoint is not valid");
            if (!shape.IsValid || shape.IsEmpty || (shape.Dimension != GeometryDimension.Line && shape.Dimension != GeometryDimension.Area))
                throw new ArgumentException("shape is not valid");
            int partIndex = -1;
            int priorVertexIndex = -1;
            Double unusedDistance = -1;
            var foundPoint = new Coordinate();
            if (shape.GetNearestCoordinate(searchPoint, foundPoint, ref partIndex, ref priorVertexIndex, ref unusedDistance))
            {
                //priorVertexIndex + 1 will always be valid when shape is a valid, non-empty line or area
                Coordinate pt1 = shape.Parts[partIndex][priorVertexIndex];
                Coordinate pt2 = shape.Parts[partIndex][priorVertexIndex + 1];
                if (pt1.SquareDistance(searchPoint) < pt2.SquareDistance(searchPoint))
                    return new CoordinateCollection {pt1, pt2};
                return new CoordinateCollection {pt2, pt1};
            }
            throw new InvalidOperationException("shape.GetNearestCoordinate(searchPoint) failed");
        }

        //returns the Azimuth of line from first vertex to last vertex
        private static Azimuth GetAzimuthFromVertices(CoordinateCollection points)
        {
            if (points == null)
                throw new ArgumentNullException("points");
            if (points.LastIndex != 1 || points.IsARing)
                throw new ArgumentException("vertices must contain two, and only two, non equal coordinates");

            Coordinate firstPoint = points.First();
            Coordinate lastPoint = points.Last();
            double angle = Math.Atan2(lastPoint.Y - firstPoint.Y, lastPoint.X - firstPoint.X);
            return Azimuth.FromTrigAngleAsRadians(angle);
        }

        private static Azimuth OrientateHeading(Azimuth transectAzimuth, Azimuth boatAzimuth)
        {
            //Azimuth subtraction yields a new azimuth, not the difference, so we need to use the azimuth's value to
            double difference = transectAzimuth.Value - boatAzimuth.Value;
            if (-90 < difference && difference < 90)
                return transectAzimuth;
            return transectAzimuth < 180 ? transectAzimuth + 180 : transectAzimuth - 180;
        }

        #endregion
    }



    public static class TransectListExtension
    {
        public static Transect GetNearest(this IEnumerable<Transect> transects, Coordinate searchPoint)
        {
            if (searchPoint == null || searchPoint.IsEmpty)
            {
                Trace.TraceWarning("searchPoint is null or empty in IEnumerable<Transect>.GetNearest()");
                return transects.FirstOrDefault();
            }

            Geometry myLocation = new Point(searchPoint);
            Transect closest = transects.OrderBy(transect => transect.Shape.Distance(myLocation))
                                        .FirstOrDefault();
            return closest;
        }
    }
}
