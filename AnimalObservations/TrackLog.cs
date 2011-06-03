using System;
using System.Collections.Generic;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    public class TrackLog
    {
        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("Tracks");
        private static readonly Dictionary<Guid, TrackLog> TrackLogs = new Dictionary<Guid, TrackLog>();

        //These are used in XAML data binding so they must be public properties
        //One-Time, One-Way bindings:
        public static IDictionary<int, string> WeatherDomain { get; private set; }
        public static IDictionary<int, string> VisibilityDomain { get; private set; }
        public static IDictionary<int, string> BeaufortDomain { get; private set; }

        private Feature Feature { get; set; }
        internal Guid Guid { get; private set; }
        internal DateTime StartingTime { get; set; }
        internal DateTime FinishingTime { get; set; }

        //public properties for WPF/XAML interface binding
        public Transect Transect { get; set; }
        public string Vessel { get; set; }
        public string DataRecorder { get; set; }
        public string Observer1 { get; set; }
        public string Observer2 { get; set; }
        public string ProtocolId { get; set; }
        public int Weather { get; set; }
        public int Visibility { get; set; }
        public int Beaufort { get; set; }
        public bool? OnTransect { get; set; }

        #region constructors

        //Class Constructor
        static TrackLog()
        {
            WeatherDomain = MobileUtilities.GetCodedValueDictionary<int>(FeatureLayer, "Weather");
            VisibilityDomain = MobileUtilities.GetCodedValueDictionary<int>(FeatureLayer, "Visibility");
            BeaufortDomain = MobileUtilities.GetCodedValueDictionary<int>(FeatureLayer, "Beaufort");
        }

        //Instance Constructor  - not permitted, use static create/from methods.
        private TrackLog()
        { }

        //May return null if no feature is found with matching guid
        internal static TrackLog FromGuid(Guid guid)
        {
            if (TrackLogs.ContainsKey(guid))
                return TrackLogs[guid];

            string whereClause = string.Format("TrackID = {0}", guid);
            TrackLog trackLog = CreateFromFeature(MobileUtilities.GetFeature(FeatureLayer, whereClause));
            if (trackLog != null && trackLog.Transect == null)
                throw new ApplicationException("Existing track log has no transect");
            return trackLog;
        }

        internal static TrackLog CreateWith(Transect transect)
        {
            if (transect == null)
                throw new ArgumentNullException("transect");

            //May throw an exception, but should never return null
            var trackLog = CreateFromFeature(MobileUtilities.CreateNewFeature(FeatureLayer));
            trackLog.Transect = transect;
            return trackLog;
        }

        internal static TrackLog CloneFrom(TrackLog oldTrackLog)
        {
            if (oldTrackLog == null)
                throw new ArgumentNullException("oldTrackLog");

            TrackLog trackLog = CreateWith(oldTrackLog.Transect);
            trackLog.LoadAttributes(oldTrackLog);
            return trackLog;
        }

        private static TrackLog CreateFromFeature(Feature feature)
        {
            if (feature == null)
                return null;
            feature.Geometry = new Polyline();
            var trackLog = new TrackLog { Feature = feature };
            trackLog.LoadAttributes();
            TrackLogs[trackLog.Guid] = trackLog;
            return trackLog;
        }

        private void LoadAttributes()
        {
            bool existing = Feature.FeatureDataRow["TrackID"] is Guid;

            if (existing)
                Guid = (Guid)Feature.FeatureDataRow["TrackID"];
            else
                Guid = new Guid(Feature.FeatureDataRow.GlobalId.ToByteArray());

            //For new features, TransectID will be null, so we can't load a Transect feature from the database
            //For new features, We will rely on the caller to set the Transect property 
            if (existing && Feature.FeatureDataRow["TransectID"] is string)
                Transect = Transect.FromName((string) Feature.FeatureDataRow["TransectID"]);

            //Simple Attributes
            Vessel = Feature.FeatureDataRow["Vessel"] as string;
            DataRecorder = Feature.FeatureDataRow["Recorder"] as string;
            Observer1 = Feature.FeatureDataRow["Observer1"] as string;
            Observer2 = Feature.FeatureDataRow["Observer2"] as string;
            ProtocolId = Feature.FeatureDataRow["Protocol_Id"] as string;
            if (Feature.FeatureDataRow["Weather"] is int)
                Weather = (int)Feature.FeatureDataRow["Weather"];
            if (Feature.FeatureDataRow["Visibility"] is int)
                Visibility = (int)Feature.FeatureDataRow["Visibility"];
            if (Feature.FeatureDataRow["Beaufort"] is int)
                Beaufort = (int)Feature.FeatureDataRow["Beaufort"];
            if (Feature.FeatureDataRow["Start"] is DateTime)
                StartingTime = (DateTime)Feature.FeatureDataRow["Start"];
            if (Feature.FeatureDataRow["End"] is DateTime)
                FinishingTime = (DateTime)Feature.FeatureDataRow["End"];
            if (Feature.FeatureDataRow["OnTransect"] is string)
                OnTransect = ((string)Feature.FeatureDataRow["OnTransect"]) == "True";

        }

        private void LoadAttributes(TrackLog templateTrackLog)
        {
            Vessel = templateTrackLog.Vessel;
            DataRecorder = templateTrackLog.DataRecorder;
            Observer1 = templateTrackLog.Observer1;
            Observer2 = templateTrackLog.Observer2;
            ProtocolId = templateTrackLog.ProtocolId;
            Weather = templateTrackLog.Weather;
            Visibility = templateTrackLog.Visibility;
            Beaufort = templateTrackLog.Beaufort;
            OnTransect = templateTrackLog.OnTransect;
        }

        #endregion

        #region Update and Save/Delete

        internal void AddPoint(Coordinate coordinate)
        {
            //ignore save errors here (there should be none), we will check/report when the tracklog is finalized.
            if (coordinate == null)
                throw new ArgumentNullException("coordinate");
            Feature.Geometry.AddCoordinate(coordinate);
            if (Feature.Geometry.CurrentPart.Count == 2)
                Save();
            if (Feature.Geometry.CurrentPart.Count % 4 == 0)
                QuickSave();
        }

        internal bool Save()
        {
            SyncPropertiesToFeature();
            return QuickSave();
        }

        private bool QuickSave()
        {
            Feature.FeatureDataRow["End"] = FinishingTime = DateTime.Now;    
            return Feature.SaveEdits();
        }

        private void SyncPropertiesToFeature()
        {
            Feature.FeatureDataRow["TrackID"] = Guid;
            Feature.FeatureDataRow["TransectID"] = Transect.Name;
            Feature.FeatureDataRow["Vessel"] = Vessel ?? (object)DBNull.Value;
            Feature.FeatureDataRow["Recorder"] = DataRecorder ?? (object)DBNull.Value;
            Feature.FeatureDataRow["Observer1"] = Observer1 ?? (object)DBNull.Value;
            Feature.FeatureDataRow["Observer2"] = Observer2 ?? (object)DBNull.Value;
            Feature.FeatureDataRow["Protocol_Id"] = ProtocolId ?? (object)DBNull.Value;
            Feature.FeatureDataRow["Weather"] = Weather;
            Feature.FeatureDataRow["Visibility"] = Visibility;
            Feature.FeatureDataRow["Beaufort"] = Beaufort;
            Feature.FeatureDataRow["Start"] = StartingTime;
            Feature.FeatureDataRow["OnTransect"] = OnTransect == null ? (object)DBNull.Value : OnTransect.Value.ToString();
        }

        internal void Delete()
        {
            TrackLogs.Remove(Guid);
            DeleteFeature();
        }

        internal void DeleteFeature()
        {
            Feature.Delete(); //Deletes the feature data row corresponding to this feature and saves the changes to the feature layer
        }

        #endregion
    }
}
