using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.Gps;
using ESRI.ArcGIS.Mobile.WPF;

namespace AnimalObservations
{
    public class CollectTrackLogTask : Task
    {
        public CollectTrackLogTask()
        {
            Name = "Collect Observations";  
            Description = "Begin data logging along a transect";
            //Can't set the ImageSource property because of threading issues.  Use GetImageSource() override
        }


        #region Overrides

        protected override ImageSource GetImageSource()
        {
            Uri uri = new Uri("pack://application:,,,/AnimalObservations;Component/Tips72.png");
            return new BitmapImage(uri);
        }

        protected override void OnOwnerInitialized()
        {
            InitializeObservationQueue();
            InitializeGpsConnection();

            SetupBoatLayer();
        }

        public override void Execute()
        {

            //TODO - remove - this is test code for drawing the boat
            //Random r = new Random();
            //double x = 442000 + r.NextDouble() * 4700;
            //double y = 6485000 + r.NextDouble() * 4200;
            //DrawBoat(new Coordinate(x, y), 360.0 * r.NextDouble());
            //return;

            //Tasks can always be executed - no override for CanExecute() - so check valididty here.
            Envelope mapExtents = MobileApplication.Current.Map.GetExtent();
            if (mapExtents == null)
            {
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(
                    "Unable to determine extents of the map.", "Unexpected Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            if (Transect.GetWithin(mapExtents).Count() < 1)
            {
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(
                    "No transects could be found within the visible map.\n" +
                    "Either zoom out or wait until a transect comes into view.\n" + 
                    "Make sure the GPS is on and the map is tracking your location.", "No Transects Found",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            //if (false) //FIXME - remove when testing is complete
            if (!gpsConnection.IsOpen || MostRecentLocation == null)
            {
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(
                    "GPS is disconnected or doesn't yet have a fix on the satellites.\n" + 
                    "Correct the problems with the GPS and try again.", "No GPS Fix",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            MobileApplication.Current.Transition(new SetupTrackLogPage());
        }

        #endregion


        #region Manage TrackLog Recording

        //If CurrentTrackLog == null then we are not recording.
        //while current tracklog is not null recording may be turned on/off.
        //CurrentTrackLog will never be changed unless recording is off.

        private bool isRecording;
        private object TracklogLock = new object();

        public bool StartRecording()
        {
            if (CurrentTrackLog == null || !gpsConnection.IsOpen)
            {
                isRecording = false;
            }
            else
            {
                isRecording = true;
                //FIXME - the following seems to cause a conflicting event.  Removed for now

                //Collect our first point now, don't wait for an event. 
                //Connection_GpsChanged(null, null);
            }
            return isRecording;
        }

        public void StopRecording()
        {
            //Make sure the GPS event isn't using the CurrentTrackLog 
            lock (TracklogLock)
            {
                isRecording = false;
                DefaultTrackLog = CurrentTrackLog;
                CurrentTrackLog = null;
            }
        }

        public TrackLog DefaultTrackLog { get; set; }
        //Use INotifyPropertyChanged to keep UI linked
        public TrackLog CurrentTrackLog
        {
            get { return _currentTrackLog; }
            set
            {
                if (isRecording)
                    throw new InvalidOperationException("Cannot change current track log while recording.");
                if (value != _currentTrackLog)
                {
                    _currentTrackLog = value;
                    OnPropertyChanged("CurrentTrackLog");
                }
            }
        }
        private TrackLog _currentTrackLog;

        #endregion


        #region Manage Observation Queue

        //Use ObservableCollection to propegate changes to the XAML UI
        public ObservableCollection<Observation> OpenObservations { get; private set; }
        public Observation ActiveObservation
        {
            get { return _activeObservation; }
            set
            { 
                if (value != _activeObservation)
                {
                   _activeObservation = value;
                    OnPropertyChanged("ActiveObservation");
                }
            }
        }
        private Observation _activeObservation;


        private void InitializeObservationQueue()
        {
            OpenObservations = new ObservableCollection<Observation>();
            ActiveObservation = null;
        }

        public void AddObservation(Observation observation)
        {
            OpenObservations.Add(observation);
            if (ActiveObservation == null)
                ActiveObservation = observation;
        }

        public void RemoveObservation(Observation observation)
        {
            //If we are deleting the active record, make the first record active
            OpenObservations.Remove(observation);
            if (observation == ActiveObservation)
                if (OpenObservations.Count == 0)
                    ActiveObservation = null;
                else
                    ActiveObservation = OpenObservations[0];
        }

        public void RemoveObservationAlt(Observation observation)
        {
            //If we are deleting the active record, make the next record active
            int deletedIndex = OpenObservations.IndexOf(observation);
            int activeIndex = OpenObservations.IndexOf(ActiveObservation);
            OpenObservations.Remove(observation);
            if (deletedIndex <= activeIndex)
                activeIndex--;
            if (activeIndex == -1)
                ActiveObservation = null;
            else
                ActiveObservation = OpenObservations[activeIndex];
        }

        public void NextObservation()
        {
            int activeIndex = OpenObservations.IndexOf(ActiveObservation);
            activeIndex++;
            if (activeIndex == OpenObservations.Count)
                activeIndex = 0;
            ActiveObservation = OpenObservations[activeIndex];
        }

        public void PreviousObservation()
        {
            int activeIndex = OpenObservations.IndexOf(ActiveObservation);
            activeIndex--;
            if (activeIndex == -1)
                activeIndex = OpenObservations.Count - 1;
            ActiveObservation = OpenObservations[activeIndex];
        }

        #endregion


        #region Manage GPS connection and events

        /// <summary>
        /// This is updated only when we are recording.  These objects are saved to disk
        /// </summary>
        public GpsPoint CurrentGpsPoint { get; set; }

        /// <summary>
        /// This is updated whenever the GPS is receiving a position fix.  It is only used for map updates/queries
        /// </summary>
        public Coordinate MostRecentLocation { get; set; }

        private GpsConnection gpsConnection;

        private void InitializeGpsConnection()
        {
            // Get GPS Connection and set up for change events
            IGpsConnectionManager GpsMgr = MobileApplication.Current.GpsConnectionManager;
            gpsConnection = MobileApplication.Current.GpsConnectionManager.Connection;
            //FIXME - Do I want to do this, or rely on application settings/user control
            //GpsMgr.OpenAsync();
            //FIXME - if the connection is closed and re-opened.  Do I need to get a new connection object?

            gpsConnection.GpsClosed += new EventHandler(Connection_GpsClosed);
            gpsConnection.GpsError += new EventHandler<GpsErrorEventArgs>(Connection_GpsError);
            gpsConnection.GpsChanged += new EventHandler(Connection_GpsChanged);
        }

        void Connection_GpsError(object sender, ESRI.ArcGIS.Mobile.Gps.GpsErrorEventArgs e)
        {
            //FIXME - save any open edits, and wait for the GPS to recover
            //FIXME - Change logging status?  How do toggle it back on if connection returns? 
            CurrentTrackLog.Save();
            ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(e.Exception.Message, "GPS Error");
            //FIXME - Is this necessary if we are handling the GPSClosed event?
            //GpsConnection GpsConn = sender as GpsConnection;
            //if (GpsConnection == null)
            //    IsGPSConnected = false;
            //else
            //    IsGPSConnected = GpsConn.IsOpen;
        }

        void Connection_GpsClosed(object sender, EventArgs e)
        {
            //FIXME - Close any open pages???
            if (isRecording)
            {
                CurrentTrackLog.Save();
                StopRecording();
            }
        }

        void Connection_GpsChanged(object sender, EventArgs e)
        {
            Debug.Print("Connection_GpsChanged: {0}, {1}", gpsConnection.FixStatus, gpsConnection.GpsChangeType);
            if (gpsConnection.FixStatus == GpsFixStatus.Invalid)
                return;
            if (!LocationChanged(gpsConnection))
                return;
            Debug.Print("Connection_GpsChanged: {0}, {1}, {2}, {3}, {4}, {5}, {6}", isRecording, CurrentTrackLog, gpsConnection.GpsChangeType, gpsConnection.FixStatus, gpsConnection.DateTime, gpsConnection.Longitude, gpsConnection.Latitude);

            //Make sure another thread doesn't change the CurrentTrackLog while I'm working.
            lock (TracklogLock)
            {
                if (isRecording)
                {
                    CurrentGpsPoint = GpsPoint.CreateWith(CurrentTrackLog, gpsConnection);
                    CurrentTrackLog.AddPoint(CurrentGpsPoint.Location);
                    MostRecentLocation = CurrentGpsPoint.Location;
                }
                else
                {
                    MostRecentLocation = MobileApplication.Current.Project.SpatialReference.FromGps(gpsConnection.Longitude, gpsConnection.Latitude);
                }
            }
            //FIXME - don't do this when collecting attributes
            DrawBoat(MostRecentLocation, gpsConnection.Course);
        }

        private bool LocationChanged(GpsConnection gpsConnection)
        {
            return (gpsConnection.GpsChangeType & GpsChangeType.Position) != 0;
        }

        #endregion


        #region Draw the boat

        private Image boat;

        private void SetupBoatLayer()
        {
            if (!MobileApplication.Current.Dispatcher.CheckAccess())
            {
                MobileApplication.Current.Dispatcher.BeginInvoke((System.Threading.ThreadStart)delegate()
                {
                    SetupBoatLayer();
                });
                return;
            }

            //Get Boat Icon
            Uri uri = new Uri("pack://application:,,,/AnimalObservations;Component/Boat-icon.png");
 
            boat = new Image();
            //boat.BeginInit();
            boat.Source = new BitmapImage(uri);
            boat.Margin = new System.Windows.Thickness(-100,-100, 0, 0);
            //boat.EndInit();

            // add graphic layer
            GraphicLayer overlay = new GraphicLayer();
            MobileApplication.Current.Map.MapGraphicLayers.Add(overlay);
            overlay.Children.Add(boat);
        }

        private void DrawBoat(Coordinate location, double heading)
        {
            //Only update the UI on the UI thread
            if (!boat.Dispatcher.CheckAccess())
            {
                boat.Dispatcher.BeginInvoke((System.Threading.ThreadStart)delegate()
                {
                    DrawBoat(location, heading);
                });
                return;
            }

            //heading = 0 is north (up); heading is in degrees clockwise
            //image without rotation is pointing E (270 degrees).
            System.Drawing.Point point = MobileApplication.Current.Map.ToClient(location);
            if (heading > 180)
                boat.LayoutTransform = new RotateTransform(heading + 90.0);
            else
            {
                TransformGroup transform = new TransformGroup();
                transform.Children.Add(new ScaleTransform(-1.0, 1.0));
                transform.Children.Add(new RotateTransform(heading - 90.0));
                boat.LayoutTransform = transform;
            }
            boat.Margin = new System.Windows.Thickness(point.X, point.Y, 0, 0);
            //MobileApplication.Current.Map.CenterAt(location);
        }

        #endregion

    }
}
