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

        //FIXME - Should update location based on a property change event on Observation.Angle/Distance.

        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("Murrelets");

        static readonly Dictionary<Guid, BirdGroup> BirdGroups = new Dictionary<Guid, BirdGroup>();

        public Feature Feature { get; private set; }
        public Guid Guid { get; private set; }

        public Observation Observation { get; private set; }
        public int Size { get; set; }
        public char Behavior { get; set; }
        public char Species { get; set; }
        public string Comments { get; set; }


        private BirdGroup()
        { }

        public static BirdGroup FromGuid(Guid guid)
        {
            if (BirdGroups.ContainsKey(guid))
                return BirdGroups[guid];

            var feature = MobileUtilities.GetFeature(FeatureLayer, guid);
            if (feature == null)
                return null;
            var birdGroup = new BirdGroup { Feature = feature };
            birdGroup.LoadAttributes1();
            birdGroup.LoadAttributes2();
            BirdGroups[birdGroup.Guid] = birdGroup;
            return birdGroup;
        }

        public static BirdGroup CreateWith(Observation observation)
        {
            if (observation == null)
                throw new ArgumentNullException("observation");

            var feature = MobileUtilities.CreateNewFeature(FeatureLayer);
            if (feature == null)
                return null;
            var birdGroup = new BirdGroup
                                {
                                    Feature = feature,
                                    Guid = new Guid(feature.FeatureDataRow.GlobalId.ToByteArray()),
                                    Observation = observation
                                };

            //get default attributes
            birdGroup.LoadAttributes2();
            BirdGroups[birdGroup.Guid] = birdGroup;
            //birdGroup.Save();
            return birdGroup;
        }

        private void LoadAttributes1()
        {
            Guid = new Guid(Feature.FeatureDataRow.GlobalId.ToByteArray());
            Observation = Observation.FromGuid((Guid)Feature.FeatureDataRow["ObservationID"]);
        }

        private void LoadAttributes2()
        {
            if (Feature.FeatureDataRow["GroupSize"] is int)
                Size = (int)Feature.FeatureDataRow["GroupSize"];
            if (Feature.FeatureDataRow["Behavior"] is string)
                Behavior = ((string)Feature.FeatureDataRow["Behavior"])[0];
            if (Feature.FeatureDataRow["Species"] is string)
                Species = ((string)Feature.FeatureDataRow["Species"])[0];
            if (Feature.FeatureDataRow["Comments"] is string)
                Comments = (string)Feature.FeatureDataRow["Comments"];
        }

        public bool Save()
        {
            Feature.Geometry = GetLocation(BirdGroupLocationRelativeTo.TransectHeading);
            Feature.FeatureDataRow["ObservationID"] = Observation.Guid;
            Feature.FeatureDataRow["GroupSize"] = Size;
            Feature.FeatureDataRow["Behavior"] = Behavior.ToString();
            Feature.FeatureDataRow["Species"] = Species.ToString();
            Feature.FeatureDataRow["Comments"] = Comments ?? (object)DBNull.Value;
            return Feature.SaveEdits();
        }

        //FIXME - sort out the difference between:
        //  1) aborting the creation of a new object
        //  2) canceling changes to an existing object
        //  3) deleting an existing object

        public void Delete()
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

        internal static BirdGroup FromPoint(Coordinate point)
        {
            return BirdGroups.Values.FirstOrDefault(birdGroups => birdGroups.Feature.FeatureDataRow.Geometry.Within(new Envelope(point,20,20)));
        }

        enum BirdGroupLocationRelativeTo
        {
            BoatHeading,
            TransectHeading
        }
    }
}
