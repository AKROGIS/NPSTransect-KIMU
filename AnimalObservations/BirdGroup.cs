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

        //TODO - sort out the difference between:
        //  1) aborting the creation of a new object
        //  2) canceling changes to an existing object
        //  3) deleting an existing object

        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("BirdGroups");
        private static readonly Dictionary<Guid, BirdGroup> BirdGroups = new Dictionary<Guid, BirdGroup>();

        private Feature Feature { get; set; }
        internal Guid Guid { get; private set; }
        internal Observation Observation { get; private set; }

        //public properties for WPF/XAML interface binding
        public int Size { get; set; }
        public char Behavior { get; set; }
        public char Species { get; set; }
        public string Comments { get; set; }


        #region Constructors

        internal static IEnumerable<BirdGroup> AllWithObservation(Observation observation)
        {
            var results = new List<BirdGroup>();
            //First get all the matching bird groups that have already been loaded
            results.AddRange(BirdGroups.Values.Where(bird => bird.Observation == observation));

            //Next search the database for matching birdgroups, but only load/add them if they are not already loaded.
            string whereClause = string.Format("ObservationID = {0}", observation.Guid);
            results.AddRange(from birdFeature in MobileUtilities.GetFeatures(FeatureLayer, whereClause)
                             where !BirdGroups.ContainsKey(new Guid(birdFeature.FeatureDataRow.GlobalId.ToByteArray()))
                             select FromFeature(birdFeature));
            return results;
        }

        private BirdGroup()
        { }

        //May return null if no feature is found within extents
        internal static BirdGroup FromEnvelope(Envelope extents)
        {
            //Check to see if it is in our cache, if not, then load from database
            BirdGroup birdGroup = BirdGroups.Values.FirstOrDefault(birds => birds.Feature.FeatureDataRow.Geometry.Within(extents)) ??
                                  FromFeature(MobileUtilities.GetFeature(FeatureLayer, extents));
            if (birdGroup != null && birdGroup.Observation == null)
                throw new ApplicationException("Existing bird group has no observation");
            return birdGroup;
        }

        internal static BirdGroup CreateWith(Observation observation)
        {
            if (observation == null)
                throw new ArgumentNullException("observation");

            var birdGroup = FromFeature(MobileUtilities.CreateNewFeature(FeatureLayer));
            if (birdGroup == null)
                throw new ApplicationException("Database returned null when asked to create a new feature");
            birdGroup.Observation = observation;
            return birdGroup;
        }

        private static BirdGroup FromFeature(Feature feature)
        {
            if (feature == null)
                return null;
            if (!feature.IsEditing)
                feature.StartEditing();
            var birdGroup = new BirdGroup { Feature = feature };
            birdGroup.LoadAttributes();
            BirdGroups[birdGroup.Guid] = birdGroup;
            return birdGroup;
        }

        private void LoadAttributes()
        {
            //BirdGroups do not have a primary key GUID (i.e. BirdGroupID), so use GlobalID for new and existing
            Guid = new Guid(Feature.FeatureDataRow.GlobalId.ToByteArray());

            //For new features, ObservationID will be null, so we can't load an observation feature from the database
            //For new features, We will rely on the caller to set the Observation property
            if (Feature.FeatureDataRow["ObservationID"] is Guid)
                Observation = Observation.FromGuid((Guid) Feature.FeatureDataRow["ObservationID"]);

            //Simple Attributes
            if (Feature.FeatureDataRow["GroupSize"] is int)
                Size = (int)Feature.FeatureDataRow["GroupSize"];
            if (Feature.FeatureDataRow["Behavior"] is string)
                Behavior = ((string)Feature.FeatureDataRow["Behavior"])[0];
            if (Feature.FeatureDataRow["Species"] is string)
                Species = ((string)Feature.FeatureDataRow["Species"])[0];
            Comments = Feature.FeatureDataRow["Comments"] as string;
        }

        #endregion

        #region Saving/Deleting

        internal bool Save()
        {
            //Toggle one of the following lines for choice of reference system
            Feature.Geometry = GetLocation(BirdGroupLocationRelativeTo.BoatHeading);
            //Feature.Geometry = GetLocation(BirdGroupLocationRelativeTo.BoatHeading);
            Feature.FeatureDataRow["ObservationID"] = Observation.Guid;
            Feature.FeatureDataRow["GroupSize"] = Size;
            Feature.FeatureDataRow["Behavior"] = Behavior.ToString();
            Feature.FeatureDataRow["Species"] = Species.ToString();
            Feature.FeatureDataRow["Comments"] = Comments ?? (object)DBNull.Value;
            return Feature.SaveEdits();
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

        internal void Delete()
        {
            BirdGroups.Remove(Guid);
            Feature.Delete(); //Deletes the feature data row corresponding to this feature and saves the changes to the feature layer
        }

        #endregion

        enum BirdGroupLocationRelativeTo
        {
            BoatHeading,
            TransectHeading
        }

    }
}
