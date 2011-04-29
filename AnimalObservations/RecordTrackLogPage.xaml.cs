using System;
using System.Diagnostics;
using System.Windows.Input;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;

namespace AnimalObservations
{
    public partial class RecordTrackLogPage : MobileApplicationPage
    {

        private CollectTrackLogTask Task;
        private TrackLog TrackLog;

        public RecordTrackLogPage()
        {
            Task = MobileApplication.Current.FindTask(typeof(CollectTrackLogTask)) as CollectTrackLogTask;
            Debug.Assert(Task != null, "Fail!, Task is null in RecordTrackLogPage");
            TrackLog = Task.CurrentTrackLog;
            Debug.Assert(Task != null, "Fail!, Task.CurrentTrackLog is null in RecordTrackLogPage");

            InitializeComponent();

            //Page Captions
            this.Title = TrackLog.Transect.Name;
            this.Note = "Capturing GPS points in track log";
            // page icon
            Uri uri = new Uri("pack://application:,,,/AnimalObservations;Component/Tips72.png");
            this.ImageSource = new System.Windows.Media.Imaging.BitmapImage(uri);
            // back button
            this.CancelCommand.Text = "Stop";
            this.BackCommands.Add(this.CancelCommand);
            // forward Button
            this.OkCommand.Text = "New Observation";
            this.ForwardCommands.Add(this.OkCommand);

            //Keyboard Events
            this.Focusable = true;
            this.Loaded += (s, e) => Keyboard.Focus(this);
        }



        #region page navigation overrides

        protected override void OnCancelCommandExecute()
        {
            //Stop recording tracklog

            //FIXME - what if there are open observations?
            Task.StopRecording();
            TrackLog.Save();
            ((SetupTrackLogPage)this.PreviousPage).UpdateSource();
            MobileApplication.Current.Transition(this.PreviousPage);
        }

        protected override void OnOkCommandExecute()
        {
            //Log observation point and open observation attribute page

            //Note, we create the observation at the last GPS point.
            //We do not wait for the next GPS point to interpolate, because
            //1) Observations have a 1 to 1 relationship with GPS points,
            //2) we may never get a next GPS point,
            //3) There is a lag in communication between observers and recorders,
            //   so the actual point of observation has already passed when this operation runs.

            //FIXME - Task.CurrentGpsPoint may be null if this thread caught the main thread between states.
            Debug.Assert(Task.CurrentGpsPoint != null, "Fail! Current GPS Point is null when recording an observation.");
            if (Task.CurrentGpsPoint == null)
                return;

            Observation observation = Observation.CreateWith(Task.CurrentGpsPoint);
            Task.AddObservation(observation);
            MobileApplication.Current.Transition(new EditObservationAttributesPage());
        }

        #endregion


        #region Mouse events

        System.Windows.Point mouseDownPoint;
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            //get the location of the mouse down event for use by the mouse up event
            mouseDownPoint = e.GetPosition(this);
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            System.Windows.Point mouseUpPoint = e.GetPosition(this);
            System.Drawing.Point drawingPoint = new System.Drawing.Point(Convert.ToInt32(mouseUpPoint.X), Convert.ToInt32(mouseUpPoint.Y));
            
            //Check to see if there is an birdgroup at this location.  If so edit it, be done
            Coordinate mapPoint = MobileApplication.Current.Map.ToMap(drawingPoint);
            Observation observation = Observation.FromPoint(mapPoint);
            if (observation != null)
            {
                Task.AddObservation(observation);
                MobileApplication.Current.Transition(new EditObservationAttributesPage());
                e.Handled = true;
                return;
            }

            //Check to see if the mouseup location is substantially different than the mouse down location, if so then pan and be done
            int dx = Convert.ToInt32(mouseUpPoint.X - mouseDownPoint.X);
            int dy = Convert.ToInt32(mouseUpPoint.Y - mouseDownPoint.Y);

            if (dx > 5 || dy > 5)
                mapControl.Map.Pan(dx, dy);

            //do default behavior
            base.OnMouseUp(e);
        }

        #endregion


        #region Keyboard Events

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                OnOkCommandExecute();
            if (e.Key == Key.Escape)
                OnCancelCommandExecute();
            if (e.Key != Key.Escape && e.Key != Key.Enter)
                base.OnKeyDown(e);
        }
        #endregion
    }
}
