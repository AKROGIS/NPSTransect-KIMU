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
        //FIXME - Bearing will be 180 degrees off if traveling from finish to start

        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("Transects");

        static Dictionary<Guid, Transect> transects;

        public Guid Guid { get; private set; }
        public Geometry Shape { get; private set; }
        public string Name { get; private set; }
        public double Bearing { get; private set; }

        private Transect()
        {}

        static public IEnumerable<Transect> AllTransects
        {
            get
            {
                if (transects == null)
                    LoadReadOnlyTransectsFromDB();
                return transects.Values;
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
            if (transects == null)
                LoadReadOnlyTransectsFromDB();
            if (transects.ContainsKey(guid))
                return transects[guid];
            else
                return null;
        }

        static public Transect FromName(string name)
        {
            if (transects == null)
                LoadReadOnlyTransectsFromDB();
            return transects.Values.FirstOrDefault(transect => transect.Name == name);
        }

        private static void LoadReadOnlyTransectsFromDB()
        {
            transects = new Dictionary<Guid, Transect>();
            using (FeatureDataReader data = FeatureLayer.GetDataReader(new QueryFilter(), EditState.Current))
            {
                while (data.Read())
                {
                    Transect transect = new Transect();
                    //If we can't load a transect for some reason (i.e bad geometry, missing values, etc), then skip it
                    try
                    {
                        transect.LoadAttributes(data);
                        transects[transect.Guid] = transect;
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
            Bearing = CalculateBearing(Shape as Polyline);
        }

        private double CalculateBearing(Polyline line)
        {
            if (line == null)
                throw new ArgumentNullException("line");
            //transect should have only one part; regardless, ignore additional parts.
            CoordinateCollection points = line.Parts[0];
            //assume transect is a simple (2 point) line; regardless, ignore additional vertices.
            Coordinate firstPoint = points.First();
            Coordinate lastPoint = points.Last();
            return Math.Atan2(lastPoint.Y - firstPoint.Y, lastPoint.X - firstPoint.Y);
        }

    }

    public static class TransectListExtension
    {
        public static Transect GetNearest(this IEnumerable<Transect> transects, Coordinate coordinate)
        {
            Debug.Assert(coordinate != null, "Fail, null coordinate in IEnumerable<Transect>.GetNearest()");

            Geometry myLocation = new Point(coordinate);
            Transect Closest = transects.OrderBy(transect => transect.Shape.Distance(myLocation))
                                        .FirstOrDefault();
            return Closest;
        }
    }
}
