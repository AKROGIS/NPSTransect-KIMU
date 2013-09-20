using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.FeatureCaching;

//BirdGroup is bound to the XAML interface, BirdGroupFeature is bound to the GIS database 

//TODO - merge with BirdGroup (?? need a default constructor for Datagrid WPF/XAML interface)
// The default constructor must find the related observation object - this may not be easy.
// or create without an observation object, and assign it later.  Check during save.
//TODO - sort out the difference between cancel, abort, and delete:
//  1) aborting the creation of a new object
//  2) canceling changes to an existing object
//  3) deleting an existing object

namespace AnimalObservations
{
    public class BirdGroupFeature
    {
        private static readonly FeatureSource FeatureSource = MobileUtilities.GetFeatureSource("BirdGroups");
        private static readonly Dictionary<Guid, BirdGroupFeature> BirdGroups = new Dictionary<Guid, BirdGroupFeature>();

        private Feature Feature { get; set; }
        private Guid Guid { get; set; }
        internal Observation Observation { get; private set; }
        internal int Size { get; set; }
        internal char Behavior { get; set; }
        internal char Species { get; set; }
        internal string Comments { get; set; }
        internal string Error { get; private set; }


        #region Constructors

        //Instance Constructor  - not permitted, use static create/from methods.
        private BirdGroupFeature()
        { }

        internal static IEnumerable<BirdGroupFeature> AllWithObservation(Observation observation)
        {
            var results = new List<BirdGroupFeature>();

            //First get all the matching bird groups that have already been loaded
            results.AddRange(BirdGroups.Values.Where(bird => bird.Observation == observation));

            //Next search the database for matching birdgroups, but only load/add them if they are not already loaded.
#if BROKEN_WHERE_GUID
            int columnIndex = FeatureSource.Columns.IndexOf("ObservationID");
            var rows = MobileUtilities.GetFeatureRows(FeatureSource, observation.Guid, columnIndex);
#else
            string whereClause = string.Format("ObservationID = '{{{0}}}'", observation.Guid);
            var rows = MobileUtilities.GetFeatureRows(FeatureSource, whereClause)
#endif
            results.AddRange(from birdFeature in rows
                             where !BirdGroups.ContainsKey(new Guid(birdFeature.GlobalId.ToByteArray()))
                             select FromFeature(new Feature(birdFeature)));
            return results;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="extents"></param>
        /// <returns>May return null if no feature is found within extents</returns>
        internal static BirdGroupFeature FromEnvelope(Envelope extents)
        {
            //Check to see if it is in our cache, if not, then load from database
            return BirdGroups.Values.FirstOrDefault(birds => birds.Feature.FeatureDataRow.Geometry.Within(extents)) ??
                   FromFeature(MobileUtilities.GetFeature(FeatureSource, extents));
        }

        internal static BirdGroupFeature FromObservation(Observation observation)
        {
            if (observation == null)
                throw new ArgumentNullException("observation");

            var birdGroup = FromFeature(MobileUtilities.CreateNewFeature(FeatureSource));
            if (birdGroup == null)
                throw new ApplicationException("Database returned null when asked to create a new feature");
            birdGroup.Observation = observation;
            return birdGroup;
        }

        private static BirdGroupFeature FromFeature(Feature feature)
        {
            if (feature == null)
                return null;
            if (!feature.IsEditing)
                feature.StartEditing();
            var birdGroup = new BirdGroupFeature { Feature = feature };
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

        //TODO use or remove BirdGroupFeature.ValidateBeforeSave() - save for use in birdgroup type merge

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


        #region Location

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
                    catch (Exception ex)
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

            double birdX = Observation.GpsPoint.Location.X + Observation.Distance*Math.Cos(azimuth.ToTrigRadians());
            double birdY = Observation.GpsPoint.Location.Y + Observation.Distance*Math.Sin(azimuth.ToTrigRadians());
            return new Point(birdX, birdY);
        }

        #endregion
    }
}
