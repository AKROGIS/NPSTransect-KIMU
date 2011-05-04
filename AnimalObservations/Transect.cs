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

        static Dictionary<Guid, Transect> _transects;

        public Guid Guid { get; private set; }
        public Geometry Shape { get; private set; }
        public string Name { get; private set; }
        //public double Bearing { get; private set; }

        private Transect()
        {}

        static public IEnumerable<Transect> AllTransects
        {
            get
            {
                if (_transects == null)
                    LoadAllTransectsFromDataSource();
                return _transects.Values;
            }
        }

        static public IEnumerable<Transect> GetWithin(Envelope extents)
        {
            Debug.Assert(extents != null, "Fail, null extents in Transect.GetWithin()");
            var results = from transect in AllTransects
                          where transect.Shape.Intersects(extents)
                          select transect;
            return results;
        }

        static public Transect FromGuid(Guid guid)
        {
            if (_transects == null)
                LoadAllTransectsFromDataSource();
            return _transects.ContainsKey(guid) ? _transects[guid] : null;
        }

        static public Transect FromName(string name)
        {
            if (_transects == null)
                LoadAllTransectsFromDataSource();
            return _transects.Values.FirstOrDefault(transect => transect.Name == name);
        }

        private static void LoadAllTransectsFromDataSource()
        {
            _transects = new Dictionary<Guid, Transect>();
            using (FeatureDataReader data = FeatureLayer.GetDataReader(new QueryFilter(), EditState.Current))
            {
                while (data.Read())
                {
                    var transect = new Transect();
                    //If we can't load a transect for some reason (i.e bad geometry, missing values, etc), then skip it
                    try
                    {
                        transect.LoadAttributes(data);
                        _transects[transect.Guid] = transect;
                    }
                    catch (Exception ex)
                    {
                        Debug.Print("Read a bad transect. " + ex.Message);
                    }
                }
            }
        }

        private void LoadAttributes(FeatureDataReader data)
        {
            Guid = new Guid(data.GetGlobalId().ToByteArray());
            Shape = data.GetGeometry();
            Name = data.GetString(data.GetOrdinal("Name"));
            //Bearing = CalculateBearing(Shape as Polyline);
        }

        //TODO - consider a property returning null (multi-segment) or bearing.  bearing must be determined once at the start of the tracklog

        //Called when creating a birdgroup to correct the boat's heading to the transect heading
        public double NormalizeHeading(GpsPoint gpsData)
        {
            Polyline segment = GetClosestSegment(Shape, gpsData.Location);
            double heading = GetHeadingFromSegment(segment);
            //heading will be off by 180 degrees off if traveling from finish to start
            heading = OrientateHeading(heading, gpsData.Bearing);
            return heading;
        }

        private static Polyline GetClosestSegment(Geometry shape, Coordinate coordinate)
        {
            throw new NotImplementedException();
            int partIndex, vertexIndex;
            Polyline line;
            Double distance;
            Coordinate vertex;
            shape.GetNearestVertex(coordinate, vertex, ref partIndex, ref vertexIndex, ref distance);
            shape.CurrentPartIndex = partIndex;
            shape.CurrentCoordinateIndex = vertexIndex;
            return line;
        }

        private static double GetHeadingFromSegment(Polyline line)
        {
            if (line == null)
                throw new ArgumentNullException("line");
            //line should have only one part; regardless, ignore additional parts.
            CoordinateCollection points = line.Parts[0];
            //line should only have 2 point; regardless, ignore additional vertices.
            Coordinate firstPoint = points.First();
            Coordinate lastPoint = points.Last();
            return Math.Atan2(lastPoint.Y - firstPoint.Y, lastPoint.X - firstPoint.Y);
        }

        private static double OrientateHeading(double transectHeading, double boatHeading)
        {
            double diff = transectHeading - boatHeading;
            if (-90 < diff && diff < 90)
                return transectHeading;
            return transectHeading < 180 ? transectHeading + 180 : transectHeading - 180;
        }

    }

    public static class TransectListExtension
    {
        public static Transect GetNearest(this IEnumerable<Transect> transects, Coordinate coordinate)
        {
            Debug.Assert(coordinate != null, "Fail, null coordinate in IEnumerable<Transect>.GetNearest()");

            Geometry myLocation = new Point(coordinate);
            Transect closest = transects.OrderBy(transect => transect.Shape.Distance(myLocation))
                                        .FirstOrDefault();
            return closest;
        }
    }
}
