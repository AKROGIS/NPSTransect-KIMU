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
        private static readonly Dictionary<Guid, Observation> Observations = new Dictionary<Guid, Observation>();

        private Feature Feature { get; set; }
        internal Guid Guid { get; private set; }
        internal GpsPoint GpsPoint { get; private set; }

        //public properties for WPF/XAML interface binding
        public int Angle { get; set; }
        public int Distance { get; set; }
        public ObservableCollection<BirdGroup2> BirdGroups { get; private set; }

        #region Constructors

        private Observation()
        {
            BirdGroups = new ObservableCollection<BirdGroup2>();
        }

        //FIXME restructure the FromGuid() family of initializers.  Need to pass a query to MobileUtilities.GetFeature
        //since the where clause will differ by featurelayer.
        //See BirdGroup for a recommended refactoring.

        internal static Observation FromGuid(Guid guid)
        {
            if (Observations.ContainsKey(guid))
                return Observations[guid];

            var feature = MobileUtilities.GetFeature(FeatureLayer, guid);
            if (feature == null)
            {
                Trace.TraceError("Fail! Unable to get feature with id = {0} from {1}", guid, FeatureLayer.Name);
                return null;
            }

            var observation = new Observation { Feature = feature };

            observation.LoadAttributes1();
            observation.LoadAttributes2();
            //FIXME - load related birdgroups 
            Observations[observation.Guid] = observation;
            return observation;
        }

        internal static Observation FromEnvelope(Envelope extents)
        {
            if (extents == null)
            {
                Trace.TraceError("Fail! null search point in Observation.FromPoint()");
                return null;                
            }

            //FIXME - this only searches the previously loaded/created birdgroups/observations
            BirdGroup birdGroup = BirdGroup.FromEnvelope(extents);
            if (birdGroup != null)
                return birdGroup.Observation;
            return Observations.Values.FirstOrDefault(observation => observation.Feature.FeatureDataRow.Geometry.Within(extents));
        }

        internal static Observation CreateWith(GpsPoint gpsPoint)
        {
            if (gpsPoint == null)
                throw new ArgumentNullException("gpsPoint");

            var feature = MobileUtilities.CreateNewFeature(FeatureLayer);
            if (feature == null)
            {
                Trace.TraceError("Fail! Unable to create a new feature in {0}", FeatureLayer.Name);
                return null;
            }

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
            //Guid = new Guid(Feature.FeatureDataRow.GlobalId.ToByteArray());
            Guid = (Guid)Feature.FeatureDataRow["ObservationID"];
            GpsPoint = GpsPoint.FromGuid((Guid)Feature.FeatureDataRow["GPSPointID"]);
        }

        private void LoadAttributes2()
        {
            if (Feature.FeatureDataRow["Angle"] is int)
                Angle = (int)Feature.FeatureDataRow["Angle"];
            if (Feature.FeatureDataRow["Distance"] is int)
                Distance = (int)Feature.FeatureDataRow["Distance"];
        }

        #endregion

        #region Save/Update

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

        #endregion
    }
}


