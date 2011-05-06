using System;
using System.Collections.Generic;
using System.Diagnostics;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    public class TrackLog
    {
        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("Tracks");
        private static readonly Dictionary<Guid, TrackLog> TrackLogs = new Dictionary<Guid, TrackLog>();

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
        public int Weather { get; set; }
        public int Visibility { get; set; }
        public int Beaufort { get; set; }


        #region constructors

        private TrackLog()
        { }

        internal static TrackLog FromGuid(Guid guid)
        {
            if (TrackLogs.ContainsKey(guid))
                return TrackLogs[guid];

            var feature = MobileUtilities.GetFeature(FeatureLayer, guid);
            if (feature == null)
            {
                Trace.TraceError("Fail! Unable to get feature with id = {0} from {1}",guid, FeatureLayer.Name);
                return null;
            }

            var trackLog = new TrackLog
                               {
                                   Feature = feature,
                                   Guid = new Guid(feature.FeatureDataRow.GlobalId.ToByteArray()),
                                   //warning - Transect.FromName() may be null - would indicate a corrupt database.
                                   Transect = Transect.FromName((string) feature.FeatureDataRow["TransectID"])
                               };

            trackLog.LoadAttributes();

            TrackLogs[trackLog.Guid] = trackLog;
            return trackLog;
        }

        internal static TrackLog CreateWith(Transect transect)
        {
            if (transect != null)
            {
                Trace.TraceError("Fail! transect is null in TrackLog.CreateWith()");
                return null;
            }
            var feature = MobileUtilities.CreateNewFeature(FeatureLayer);
            if (feature == null)
            {
                Trace.TraceError("Fail! Unable to create a new feature in {0}", FeatureLayer.Name);
                return null;
            }

            feature.Geometry = new Polyline();
            var trackLog = new TrackLog
                               {
                                   Feature = feature,
                                   Guid = new Guid(feature.FeatureDataRow.GlobalId.ToByteArray()),
                                   Transect = transect
                               };

            trackLog.LoadAttributes();

            TrackLogs[trackLog.Guid] = trackLog;
            return trackLog;
        }

        internal static TrackLog CloneFrom(TrackLog oldTrackLog)
        {
            if (oldTrackLog == null)
            {
                Trace.TraceError("Fail! oldTrackLog is null in TrackLog.CloneFrom()");
                return null;
            }

            var feature = MobileUtilities.CreateNewFeature(FeatureLayer);
            if (feature == null)
            {
                Trace.TraceError("Fail! Unable to create a new transect is featurelayer");
                return null;
            }

            feature.Geometry = new Polyline();
            var newTrackLog = new TrackLog
                                  {
                                      Feature = feature,
                                      Guid = new Guid(feature.FeatureDataRow.GlobalId.ToByteArray()),
                                      Transect = oldTrackLog.Transect
                                  };

            newTrackLog.LoadAttributes(oldTrackLog);

            TrackLogs[newTrackLog.Guid] = newTrackLog;
            return newTrackLog;
        }

        private void LoadAttributes()
        {
            Vessel = Feature.FeatureDataRow["Vessel"] as string;
            DataRecorder = Feature.FeatureDataRow["Recorder"] as string;
            Observer1 = Feature.FeatureDataRow["Observer1"] as string;
            Observer2 = Feature.FeatureDataRow["Observer2"] as string;
            Weather = (int)Feature.FeatureDataRow["Weather"];
            Visibility = (int)Feature.FeatureDataRow["Visibility"];
            Beaufort = (int)Feature.FeatureDataRow["Beaufort"];
            StartingTime = (DateTime)Feature.FeatureDataRow["Start"];
            FinishingTime = (DateTime)Feature.FeatureDataRow["End"];
        }

        private void LoadAttributes(TrackLog templateTrackLog)
        {
            Vessel = templateTrackLog.Vessel;
            DataRecorder = templateTrackLog.DataRecorder;
            Observer1 = templateTrackLog.Observer1;
            Observer2 = templateTrackLog.Observer2;
            Weather = templateTrackLog.Weather;
            Visibility = templateTrackLog.Visibility;
            Beaufort = templateTrackLog.Beaufort;
        }

        #endregion

        #region update and save

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
            Feature.FeatureDataRow["Weather"] = Weather;
            Feature.FeatureDataRow["Visibility"] = Visibility;
            Feature.FeatureDataRow["Beaufort"] = Beaufort;
            Feature.FeatureDataRow["Start"] = StartingTime;
        }
        #endregion
    }
}
