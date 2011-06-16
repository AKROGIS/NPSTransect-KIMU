#define BROKEN_WHERE_GUID

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    public class Observation
    {
        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("Observations");
        private static readonly Dictionary<Guid, Observation> Observations = new Dictionary<Guid, Observation>();

        private Feature Feature { get; set; }
        internal Guid Guid { get; private set; }
        public GpsPoint GpsPoint { get; private set; }  //public for XAML binding

        //public properties for WPF/XAML interface binding
        public int Angle { 
            get
            {
                return _angle;
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value", "Angle cannot be negative.");
                if (value > 360)
                    throw new ArgumentOutOfRangeException("value", "Angle cannot be greater than 360.");
                _angle = value;
            }
        }
        private int _angle;

        public int Distance
        {
            get
            {
                return _distance;
            }
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException("value", "Distance must be positive.");
                if (value > 500)
                    throw new ArgumentOutOfRangeException("value", "Distance cannot be greater than 500.");
                _distance = value;
            }
        }
        private int _distance;

        public ObservableCollection<BirdGroup2> BirdGroups { get; private set; }

        #region Constructors

        private Observation()
        {
            //Create a default record to seed the datagrid, otherwise the datagrid shows no rows
            BirdGroups = new ObservableCollection<BirdGroup2> { new BirdGroup2() };
        }

        //May return null if no feature is found with matching guid
        internal static Observation FromGuid(Guid guid)
        {
            if (Observations.ContainsKey(guid))
                return Observations[guid];
#if BROKEN_WHERE_GUID
            int columnIndex = FeatureLayer.Columns.IndexOf("ObservationID");
            Observation observation = FromFeature(MobileUtilities.GetFeature(FeatureLayer, guid, columnIndex));
#else
            string whereClause = string.Format("ObservationID = {{{0}}}", guid);
            Observation observation = FromFeature(MobileUtilities.GetFeature(FeatureLayer, whereClause));
#endif
            if (observation != null && observation.GpsPoint == null)
                throw new ApplicationException("Existing observation has no gps point");
            return observation;
        }

        internal static Observation FromGpsPoint(GpsPoint gpsPoint)
        {
            if (gpsPoint == null)
                throw new ArgumentNullException("gpsPoint");

            //May throw an exception, but should never return null
            var observation = FromFeature(MobileUtilities.CreateNewFeature(FeatureLayer));
            observation.GpsPoint = gpsPoint;
            return observation;
        }

        //May return null if no feature is found within extents
        internal static Observation FromEnvelope(Envelope extents)
        {
            if (extents == null)
                throw new ArgumentNullException("extents");

            return Observations.Values.FirstOrDefault(obs => obs.Feature.FeatureDataRow.Geometry.Within(extents)) ??
                   FromFeature(MobileUtilities.GetFeature(FeatureLayer, extents));
        }

        private static Observation FromFeature(Feature feature)
        {
            if (feature == null)
                return null;
            if (!feature.IsEditing)
                feature.StartEditing();
            var observation = new Observation { Feature = feature };
            bool existing = observation.LoadAttributes();
            Observations[observation.Guid] = observation;
            //load birdgroups.  must becalled after updating Observations[], to avoid an infinite loop: obs->bird->obs->bird->...
            if (existing)
                observation.LoadBirdGroups();

            return observation;
        }

        private bool LoadAttributes()
        {
            bool existing = Feature.FeatureDataRow["ObservationID"] is Guid;

            if (existing)
                Guid = (Guid)Feature.FeatureDataRow["ObservationID"];
            else
                Guid = new Guid(Feature.FeatureDataRow.GlobalId.ToByteArray());

            //For new features, GPSPointID will be null, so we can't load an GPSPointID feature from the database
            //For new features, We will rely on the caller to set the GpsPoint property 
            if (existing && Feature.FeatureDataRow["GPSPointID"] is Guid)
                GpsPoint = GpsPoint.FromGuid((Guid) Feature.FeatureDataRow["GPSPointID"]);

            //Simple Attributes
            if (Feature.FeatureDataRow["Angle"] is int)
                Angle = (int)Feature.FeatureDataRow["Angle"];
            if (Feature.FeatureDataRow["Distance"] is int)
                Distance = (int)Feature.FeatureDataRow["Distance"];

            return existing;
        }

        private void LoadBirdGroups()
        {
            BirdGroups.Clear();
            foreach (var birdGroup in BirdGroup.AllWithObservation(this))
                BirdGroups.Add(new BirdGroup2(birdGroup));
        }

        #endregion

        #region Save/Update

        internal string ValidateBeforeSave()
        {
            string errors = "";
            if (Angle < 0)
                errors += "Angle must be a positive integer.\n";
            if (Angle > 360)
                errors += "Angle cannot be greater than 360.\n";
            if (Distance <= 0)
                errors += "Distance must be a positive integer.\n";
            if (Distance > 500)
                errors += "Distance cannot be greater than 500m.\n";
            if (BirdGroups.Count < 1)
                errors += "Each observation must have at least one bird group.\n";
            if (BirdGroups.Any(birdGroup => !birdGroup.IsValid))
                errors += "All bird groups must be valid (Group size is a positive integer less than 100 and behavior is not pending).\n";
            return errors;
        }

        internal bool Save()
        {
            Feature.Geometry = new Point(GpsPoint.Location);
            Feature.FeatureDataRow["ObservationID"] = Guid;
            Feature.FeatureDataRow["GPSPointID"] = GpsPoint.Guid;
            Feature.FeatureDataRow["Angle"] = Angle;
            Feature.FeatureDataRow["Distance"] = Distance;
            return Feature.SaveEdits() && SaveBirds();
        }

        private bool SaveBirds()
        {
            bool failed = false;
            foreach (BirdGroup2 bird in BirdGroups)
                if (!bird.Save(this))
                    failed = true;
            return !failed;
        }

        internal void Delete()
        {
            Observations.Remove(Guid);
            Feature.Delete(); //Deletes the feature data row corresponding to this feature and saves the changes to the feature layer
            foreach (BirdGroup2 bird in BirdGroups)
                bird.Delete();
        }


        #endregion

    }
}


