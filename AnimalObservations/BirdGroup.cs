using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    public class BirdGroup
    {

        //FIXME - sort out the difference between:
        //  1) aborting the creation of a new object
        //  2) canceling changes to an existing object
        //  3) deleting an existing object

        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("Bird Groups");
        private static readonly Dictionary<Guid, BirdGroup> BirdGroups = new Dictionary<Guid, BirdGroup>();

        private Feature Feature { get; set; }
        internal Guid Guid { get; private set; }
        internal Observation Observation { get; private set; }

        //public properties for WPF/XAML interface binding
        public int Size { get; set; }
        public char Behavior { get; set; }
        public char Species { get; set; }
        public string Comments { get; set; }


        private BirdGroup()
        { }

        internal static BirdGroup FromEnvelope(Envelope extents)
        {
            BirdGroup birdGroup = BirdGroups.Values.FirstOrDefault(birdGroups => birdGroups.Feature.FeatureDataRow.Geometry.Within(extents));
            if (birdGroup != null)
                return birdGroup;
            return FromFeature(MobileUtilities.GetFeature(FeatureLayer, extents));
        }

        [Obsolete]
        private static BirdGroup FromGuid(Guid guid)
        {
            if (BirdGroups.ContainsKey(guid))
                return BirdGroups[guid];

            var feature = MobileUtilities.GetFeature(FeatureLayer, guid);
            if (feature == null)
            {
                Trace.TraceError("Fail! Unable to get feature with id = {0} from {1}", guid, FeatureLayer.Name);
                return null;
            }
            return FromFeature(feature);
        }

        internal static BirdGroup CreateWith(Observation observation)
        {
            if (observation == null)
            {
                Trace.TraceError("Fail! null observation in BirdGroup.CreateWith(observation)");
                return null;
            }

            var feature = MobileUtilities.CreateNewFeature(FeatureLayer);
            if (feature == null)
            {
                Trace.TraceError("Fail! Unable to create a new feature in {0}", FeatureLayer.Name);
                return null;
            }

            var birdGroup = FromFeature(feature);
            birdGroup.Observation = observation;
            return birdGroup;
        }

        private static BirdGroup FromFeature(Feature feature)
        {
            var birdGroup = new BirdGroup { Feature = feature };
            birdGroup.LoadAttributes();
            BirdGroups[birdGroup.Guid] = birdGroup;
            return birdGroup;
        }

        private void LoadAttributes()
        {
            //BirdGroups do not have a primary key GUID, so use GlobalID
            Guid = new Guid(Feature.FeatureDataRow.GlobalId.ToByteArray());
            //For new features, ObservationID will be null, so we won't be loading an observation feature
            if (Feature.FeatureDataRow["ObservationID"] is Guid)
                Observation = Observation.FromGuid((Guid)Feature.FeatureDataRow["ObservationID"]);
            if (Feature.FeatureDataRow["GroupSize"] is int)
                Size = (int)Feature.FeatureDataRow["GroupSize"];
            if (Feature.FeatureDataRow["Behavior"] is string)
                Behavior = ((string)Feature.FeatureDataRow["Behavior"])[0];
            if (Feature.FeatureDataRow["Species"] is string)
                Species = ((string)Feature.FeatureDataRow["Species"])[0];
            if (Feature.FeatureDataRow["Comments"] is string)
                Comments = (string)Feature.FeatureDataRow["Comments"];
        }

        internal bool Save()
        {
            Feature.Geometry = GetLocation(BirdGroupLocationRelativeTo.TransectHeading);
            Feature.FeatureDataRow["ObservationID"] = Observation.Guid;
            Feature.FeatureDataRow["GroupSize"] = Size;
            Feature.FeatureDataRow["Behavior"] = Behavior.ToString();
            Feature.FeatureDataRow["Species"] = Species.ToString();
            Feature.FeatureDataRow["Comments"] = Comments ?? (object)DBNull.Value;
            return Feature.SaveEdits();
        }

        internal void Delete()
        {
            BirdGroups.Remove(Guid);
            Feature.CancelEdit();
        }

        private Point GetLocation(BirdGroupLocationRelativeTo angleBasis)
        {
            Azimuth azimuth;
            switch (angleBasis)
            {
                case BirdGroupLocationRelativeTo.BoatHeading:
                    azimuth = Observation.GpsPoint.Bearing;
                    break;
                default:
                    try
                    {
                        //If this throws an exception, then fall back to the boat bearing
                        azimuth = Observation.GpsPoint.TrackLog.Transect.NormalizedAzimuth(Observation.GpsPoint);
                    }
                    catch(Exception ex)
                    {
                        Trace.TraceError("Unable to compute azimuth of transect.  Using boat's azimuth.\n{0}", ex);
                        azimuth = Observation.GpsPoint.Bearing;
                    }
                    break;
            }

            //Add observation angle: clockwise from stern(0 = stern, 90 = port 180 = bow, 270 = starboard)
            azimuth += (Observation.Angle - 180);

            double birdX = Observation.GpsPoint.Location.X + Observation.Distance * Math.Cos(azimuth.ToTrigRadians());
            double birdY = Observation.GpsPoint.Location.Y + Observation.Distance * Math.Sin(azimuth.ToTrigRadians());
            return new Point(birdX, birdY);
        }

        enum BirdGroupLocationRelativeTo
        {
            BoatHeading,
            TransectHeading
        }
    }
}
