using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    public class Observation
    {
        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("Observations");

        static readonly Dictionary<Guid, Observation> Observations = new Dictionary<Guid, Observation>();

        public Feature Feature { get; private set; }
        public Guid Guid { get; private set; }

        public GpsPoint GpsPoint { get; private set; }
        public int Angle { get; set; }
        public int Distance { get; set; }
        public ObservableCollection<BirdGroup2> BirdGroups { get; private set; }

        private Observation()
        {
            BirdGroups = new ObservableCollection<BirdGroup2>();
        }

        public static Observation FromGuid(Guid guid)
        {
            if (Observations.ContainsKey(guid))
                return Observations[guid];

            var feature = MobileUtilities.GetFeature(FeatureLayer, guid);
            if (feature == null)
                return null;
            var observation = new Observation {Feature = feature};

            observation.LoadAttributes1();
            observation.LoadAttributes2();
            //FIXME - load related birdgroups 
            Observations[observation.Guid] = observation;
            return observation;
        }

        public static Observation CreateWith(GpsPoint gpsPoint)
        {
            if (gpsPoint == null)
                throw new ArgumentNullException("gpsPoint");

            var feature = MobileUtilities.CreateNewFeature(FeatureLayer);
            if (feature == null)
                return null;
            var observation = new Observation
                                  {
                                      Feature = feature,
                                      Guid = new Guid(feature.FeatureDataRow.GlobalId.ToByteArray()),
                                      GpsPoint = gpsPoint
                                  };
            //get default attributes
            observation.LoadAttributes2();
            Observations[gpsPoint.Guid] = observation;
            //write the Geometry and default attributes to the database
            //observation.Save();
            return observation;
        }

        private void LoadAttributes1()
        {
            Guid = new Guid(Feature.FeatureDataRow.GlobalId.ToByteArray());
            GpsPoint = GpsPoint.FromGuid((Guid)Feature.FeatureDataRow["GPSPointID"]);
        }

        private void LoadAttributes2()
        {
            if (Feature.FeatureDataRow["Angle"] is int)
                Angle = (int)Feature.FeatureDataRow["Angle"];
            if (Feature.FeatureDataRow["Distance"] is int)
                Distance = (int)Feature.FeatureDataRow["Distance"];
        }

        public void Save()
        {
            Feature.Geometry = new Point(GpsPoint.Location);
            Feature.FeatureDataRow["GPSPointID"] = GpsPoint.Guid;
            Feature.FeatureDataRow["Angle"] = Angle;
            Feature.FeatureDataRow["Distance"] = Distance;
            Feature.SaveEdits();
            SaveBirds();
        }

        private void SaveBirds()
        {
            foreach (BirdGroup2 bird in BirdGroups)
                bird.Save(this);
        }


        internal static Observation FromPoint(Coordinate point)
        {
            Debug.Assert(point != null, "Fail, null point in Observation.FromPoint()");

            BirdGroup birdGroup = BirdGroup.FromPoint(point);
            if (birdGroup != null)
                return birdGroup.Observation;

            //FIXME - this only searches the previously loaded/created observations
            return Observations.Values.FirstOrDefault(observation => observation.Feature.FeatureDataRow.Geometry.Within(new Envelope(point, 20, 20)));
        }
    }
}


