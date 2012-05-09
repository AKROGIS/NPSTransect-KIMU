using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    public class Observation
    {
        private static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("Observations");
        private static readonly Dictionary<Guid, Observation> Observations = new Dictionary<Guid, Observation>();

        private Feature Feature { get; set; }
        internal Guid Guid { get; private set; }
        internal string Error { get; set; }


        #region Public properties for WPF/XAML interface binding

        public GpsPoint GpsPoint { get; private set; }
        public ObservableCollection<BirdGroup> BirdGroups { get; private set; }
        public int Angle
        { 
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

        #endregion


        #region Constructors

        private Observation()
        {
            //Create a default record to seed the datagrid, otherwise the datagrid shows no rows
            BirdGroups = new ObservableCollection<BirdGroup> { new BirdGroup() };
            BirdGroups.CollectionChanged += BirdGroupsOnCollectionChanged;
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

            return Observations.Values.FirstOrDefault(obs => IsWithin(obs, extents)) ??
                   FromFeature(MobileUtilities.GetFeature(FeatureLayer, extents));
            //return Observations.Values.FirstOrDefault(obs => obs.Feature.FeatureDataRow.Geometry.Within(extents)) ??
            //       FromFeature(MobileUtilities.GetFeature(FeatureLayer, extents));
        }

        private static bool IsWithin(Observation obs, Envelope extents)
        {
            if (obs.Feature == null)
            {
                Trace.TraceError("Observation:{1} has no Feature", obs);
                return false;
            }
            if (obs.Feature.FeatureDataRow == null)
            {
                Trace.TraceError("Observation:{1} has no FeatureDataRow", obs);
                return false;
            }
            if (obs.Feature.FeatureDataRow.Geometry == null)
            {
                Trace.TraceError("Observation:{1} has no Geometry", obs);
                return false;
            }
            return obs.Feature.FeatureDataRow.Geometry.Within(extents);
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
            foreach (var birdGroupFeature in BirdGroupFeature.AllWithObservation(this))
                BirdGroups.Add(new BirdGroup(birdGroupFeature));
        }

        #endregion


        #region Save/Update

        internal bool ValidateBeforeSave()
        {
            var errors = new StringBuilder();
            if (Angle < 0)
                errors.Append("Angle must be a positive integer.\n");
            if (Angle > 360)
                errors.Append("Angle cannot be greater than 360.\n");
            if (Distance <= 0)
                errors.Append("Distance must be a positive integer.\n");
            if (Distance > 500)
                errors.Append("Distance cannot be greater than 500m.\n");
            if (BirdGroups.Count < 1)
                errors.Append("Each observation must have at least one bird group.\n");
            foreach (var birdGroup in BirdGroups)
                if (!string.IsNullOrEmpty(birdGroup.Error))
                    errors.Append(birdGroup.Error + "\n");
            Error = errors.ToString();
            return errors.Length == 0;
        }

        internal bool Save()
        {
            Feature.Geometry = new Point(GpsPoint.Location);
            Feature.FeatureDataRow["ObservationID"] = Guid;
            Feature.FeatureDataRow["GPSPointID"] = GpsPoint.Guid;
            Feature.FeatureDataRow["Angle"] = Angle;
            Feature.FeatureDataRow["Distance"] = Distance;
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
            return SaveBirdGroups();
        }

        private bool SaveBirdGroups()
        {
            bool failed = false;
            foreach (BirdGroup birdGroup in BirdGroups)
                if (!birdGroup.Save(this))
                    failed = true;
            return !failed;
        }

        internal void Delete()
        {
            Observations.Remove(Guid);

            //per docs: Feature.Deletes the feature data row corresponding to this feature and saves the changes to the feature layer
            //However this does not work.  However Feature.FeatureDataRow.Delete() does work.
            //Feature.Delete(); 
            Feature.FeatureDataRow.Delete();
            Feature.SaveEdits();
            foreach (BirdGroup birdGroup in BirdGroups)
                birdGroup.Delete();
        }


        #endregion

        #region Manage undo of changes made during a canceled edit session.

        private bool _isEditing;
        private BirdGroup[] _savedBirdGroups;
        private int _savedAngle;
        private int _savedDistance;
        private readonly List<BirdGroup> _deletedBirdGroups = new List<BirdGroup>();

        internal void BeginEdit()
        {
            if (_isEditing)
                return;
            _isEditing = true;
            _savedBirdGroups = BirdGroups.Select(bg => bg.Copy()).ToArray();
            _savedAngle = Angle;
            _savedDistance = Distance;
        }

        internal void CancelEdit()
        {
            BirdGroups.Clear();
            foreach (var savedBirdGroup in _savedBirdGroups)
            {
                BirdGroups.Add(savedBirdGroup);
            }
            // Ignore the default values from a 'blank' saved observation
            if (_savedAngle != 0)
                Angle = _savedAngle;
            if (_savedDistance != 0)
                Distance = _savedDistance;
            _isEditing = false;
        }

        internal bool CommitEdit()
        {
            _isEditing = false;
            foreach (var deletedBirdGroup in _deletedBirdGroups)
                deletedBirdGroup.Delete();
            _deletedBirdGroups.Clear();
            return Save();
        }

        private void BirdGroupsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            //If we delete a bird group we need to make sure it is deleted from the database 
            if (notifyCollectionChangedEventArgs.Action != NotifyCollectionChangedAction.Remove)
                return;
            foreach (var item in notifyCollectionChangedEventArgs.OldItems)
            {
                var birdGroup = item as BirdGroup;
                if (birdGroup != null)
                    _deletedBirdGroups.Add(birdGroup);
            }
        }

        #endregion
    }
}


