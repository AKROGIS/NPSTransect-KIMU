using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using ESRI.ArcGIS.Mobile;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    public class TrackLog
    {
        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("Tracks");
        
        static Dictionary<Guid, TrackLog> TrackLogs = new Dictionary<Guid, TrackLog>();

        public Feature Feature { get; private set; }
        public Guid Guid { get; private set; }

        public Transect Transect { get; set; }
        public string Vessel { get; set; }
        public string DataRecorder { get; set; }
        public string Observer1 { get; set; }
        public string Observer2 { get; set; }
        public int Weather { get; set; }
        public int Visibility { get; set; }
        public int Beaufort { get; set; }
        public DateTime StartingTime { get; set; }
        public DateTime FinishingTime { get; set; }

        CoordinateCollection _points = new CoordinateCollection();

        private TrackLog()
        { }

        public static TrackLog FromGuid(Guid guid)
        {
            if (guid == null)
                throw new ArgumentNullException("guid");

            if (TrackLogs.ContainsKey(guid))
                return TrackLogs[guid];

            TrackLog trackLog = new TrackLog();
            trackLog.Feature = MobileUtilities.GetFeature(FeatureLayer, guid);
            if (trackLog.Feature == null)
                return null;
            trackLog.Guid = new Guid(trackLog.Feature.FeatureDataRow.GlobalId.ToByteArray());
            trackLog.Transect = Transect.FromGuid((Guid)trackLog.Feature.FeatureDataRow["TransectGID"]);

            //get default attributes
            trackLog.LoadAttributes();

            TrackLogs[trackLog.Guid] = trackLog;
            return trackLog;
        }

        public static TrackLog CreateWith(Transect transect)
        {
            if (transect == null)
                throw new ArgumentNullException("transect");

            TrackLog trackLog = new TrackLog();
            trackLog.Feature = MobileUtilities.CreateNewFeature(FeatureLayer);
            if (trackLog.Feature == null)
                return null;
            trackLog.Guid = new Guid(trackLog.Feature.FeatureDataRow.GlobalId.ToByteArray());
            trackLog.Transect = transect;

            //get default attributes
            trackLog.LoadAttributes();

            TrackLogs[trackLog.Guid] = trackLog;
            return trackLog;
        }

        internal static TrackLog CloneFrom(TrackLog oldTrackLog)
        {
            if (oldTrackLog == null)
                throw new ArgumentNullException("oldTrackLog");

            TrackLog newTrackLog = new TrackLog();
            newTrackLog.Feature = MobileUtilities.CreateNewFeature(FeatureLayer);
            if (newTrackLog.Feature == null)
                return null;
            newTrackLog.Guid = new Guid(newTrackLog.Feature.FeatureDataRow.GlobalId.ToByteArray());
            newTrackLog.Transect = oldTrackLog.Transect;

            //get default attributes
            newTrackLog.LoadAttributes(oldTrackLog);

            TrackLogs[newTrackLog.Guid] = newTrackLog;
            return newTrackLog;
        }

        public IEnumerable<Coordinate> Points
        {
            get
            {
                return _points;
            }
        }

        public void AddPoint(Coordinate coordinate)
        {
            if (coordinate == null)
                throw new ArgumentNullException("coordinate");
            _points.Add(coordinate);
            if (_points.Count == 2)
                Save();
            if (_points.Count % 4 == 0)
                SaveGeometry();
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

        public void Save()
        {
            FinishingTime = DateTime.Now;
            SyncPropertiesToFeature();
            bool saveSuccess = SaveGeometry();
            //FIXME - provide better error messages, and correct/retry abilities
            if (!saveSuccess)
                Console.WriteLine("Save Failed.");
            else
                Console.WriteLine("Save Succeeded.");
        }

        public bool SaveGeometry()
        {
            Polyline p = new Polyline(_points);
            //If p is invalid (i.e has < 2 points), the SaveEdits will return false. 
            Feature.Geometry = p;
            return Feature.SaveEdits();
        }

        public void SyncPropertiesToFeature()
        {
            Feature.FeatureDataRow["TransectGID"] = Transect.Guid;
            Feature.FeatureDataRow["Vessel"] = Vessel ?? (object)DBNull.Value;
            Feature.FeatureDataRow["Recorder"] = DataRecorder ?? (object)DBNull.Value;
            Feature.FeatureDataRow["Observer1"] = Observer1 ?? (object)DBNull.Value;
            Feature.FeatureDataRow["Observer2"] = Observer2 ?? (object)DBNull.Value;
            Feature.FeatureDataRow["Weather"] = Weather;
            Feature.FeatureDataRow["Visibility"] = Visibility;
            Feature.FeatureDataRow["Beaufort"] = Beaufort;
            Feature.FeatureDataRow["Start"] = StartingTime;
            Feature.FeatureDataRow["End"] = FinishingTime;
        }
    }
}
