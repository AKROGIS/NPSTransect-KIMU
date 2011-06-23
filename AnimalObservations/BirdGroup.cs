#define BROKEN_WHERE_GUID

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

        //These are used in XAML data binding so they must be public properties
        //One-Time, One-Way bindings:
        //NOTE - currently not used, since datagrid uses enums in BirdGroup2 for picklists
        public static IDictionary<string, string> BehaviorDomain { get; private set; }
        public static IDictionary<string, string> SpeciesDomain { get; private set; }


        private Feature Feature { get; set; }
        internal Guid Guid { get; private set; }
        internal Observation Observation { get; private set; }
        internal string Error { get; set; }

        //public properties for WPF/XAML interface binding
        public int Size { get; set; }
        public char Behavior { get; set; }
        public char Species { get; set; }
        public string Comments { get; set; }


        #region Constructors

        //Class Constructor
        static BirdGroup()
        {
            BehaviorDomain = MobileUtilities.GetCodedValueDictionary<string>(FeatureLayer, "Behavior");
            SpeciesDomain = MobileUtilities.GetCodedValueDictionary<string>(FeatureLayer, "Species");
        }

        //Instance Constructor  - not permitted, use static create/from methods.
        private BirdGroup()
        { }

        internal static IEnumerable<BirdGroup> AllWithObservation(Observation observation)
        {
            var results = new List<BirdGroup>();
            //First get all the matching bird groups that have already been loaded
            results.AddRange(BirdGroups.Values.Where(bird => bird.Observation == observation));

            //Next search the database for matching birdgroups, but only load/add them if they are not already loaded.
#if BROKEN_WHERE_GUID
            int columnIndex = FeatureLayer.Columns.IndexOf("ObservationID");
            var rows = MobileUtilities.GetFeatureRows(FeatureLayer, observation.Guid, columnIndex);
#else
            string whereClause = string.Format("ObservationID = '{{{0}}}'", observation.Guid);
            var rows = MobileUtilities.GetFeatureRows(FeatureLayer, whereClause)
#endif
            //We need to enable editing before we can check the 
            //foreach (var feature in birds)
            //    if (!feature.IsEditing)
            //        feature.StartEditing();

            results.AddRange(from birdFeature in rows
                             where !BirdGroups.ContainsKey(new Guid(birdFeature.GlobalId.ToByteArray()))
                             select FromFeature(new Feature(birdFeature)));
            return results;
        }

        //May return null if no feature is found within extents
        internal static BirdGroup FromEnvelope(Envelope extents)
        {
            //Check to see if it is in our cache, if not, then load from database
            return BirdGroups.Values.FirstOrDefault(birds => birds.Feature.FeatureDataRow.Geometry.Within(extents)) ??
                   FromFeature(MobileUtilities.GetFeature(FeatureLayer, extents));
        }

        internal static BirdGroup FromObservation(Observation observation)
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
                Observation = Observation.FromGuid((Guid)Feature.FeatureDataRow["ObservationID"]);

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

        internal bool ValidateBeforeSave()
        {
            var errors = new StringBuilder();
            if (Size < 1)
                errors.Append("Bird group size must be a positive integer.\n");
            if (Size > 99)
                errors.Append("Bird group size cannot be greater than 99.\n");
            if (Behavior != 'W' && Behavior != 'F')
                errors.Append("Bird group behaviour must be 'Water' or 'Flying'.\n");
            Error = errors.ToString();
            return errors.Length == 0;
        }

        internal bool Save()
        {
            //Toggle one of the following lines for choice of reference system
            Feature.Geometry = GetLocation(BirdGroupLocationRelativeTo.BoatHeading);
            //Feature.Geometry = GetLocation(BirdGroupLocationRelativeTo.TransectHeading);
            Feature.FeatureDataRow["ObservationID"] = Observation.Guid;
            Feature.FeatureDataRow["GroupSize"] = Size;
            Feature.FeatureDataRow["Behavior"] = Behavior.ToString();
            Feature.FeatureDataRow["Species"] = Species.ToString();
            Feature.FeatureDataRow["Comments"] = Comments ?? (object)DBNull.Value;
            if (!Feature.SaveEdits())
            {
                var errors = new StringBuilder();
                if (!Feature.HasValidGeometry)
                    errors.Append("Geometry is invalid.\n");
                if (!Feature.HasValidAttributes)
                    errors.Append("One or more attributes are invalid.\n");
                Error = errors.ToString();
                return false;
            }
            Error = string.Empty;
            return true;
        }

        private Point GetLocation(BirdGroupLocationRelativeTo angleBasis)
        {
            Azimuth azimuth;
            switch (angleBasis)
            {
                case BirdGroupLocationRelativeTo.TransectHeading:
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
                default:
                    azimuth = Observation.GpsPoint.Bearing;
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
            //per docs: Feature.Deletes the feature data row corresponding to this feature and saves the changes to the feature layer
            //However this does not work.  However Feature.FeatureDataRow.Delete() does work.
            //Feature.Delete(); 
            Feature.FeatureDataRow.Delete();
            Feature.SaveEdits();
        }

        #endregion

        enum BirdGroupLocationRelativeTo
        {
            BoatHeading,
            TransectHeading
        }

    }
}
