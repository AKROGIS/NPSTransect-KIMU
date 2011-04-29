using System;
using System.Diagnostics;
using System.Windows.Input;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;

namespace AnimalObservations
{
    public partial class RecordTrackLogPage
    {

        private readonly CollectTrackLogTask _task;
        private readonly TrackLog _trackLog;

        public RecordTrackLogPage()
        {
            _task = MobileApplication.Current.FindTask(typeof(CollectTrackLogTask)) as CollectTrackLogTask;
            if (_task == null)
                throw new NullReferenceException("No Task!");
            _trackLog = _task.CurrentTrackLog;
            Debug.Assert(_task != null, "Fail!, Task.CurrentTrackLog is null in RecordTrackLogPage");

            InitializeComponent();

            //Page Captions
            Title = _trackLog.Transect.Name;
            Note = "Capturing GPS points in track log";
            // page icon
            var uri = new Uri("pack://application:,,,/AnimalObservations;Component/Tips72.png");
            ImageSource = new System.Windows.Media.Imaging.BitmapImage(uri);
            // back button
            CancelCommand.Text = "Stop";
            BackCommands.Add(CancelCommand);
            // forward Button
            OkCommand.Text = "New Observation";
            ForwardCommands.Add(OkCommand);

            //Keyboard Events
            Focusable = true;
            Loaded += (s, e) => Keyboard.Focus(this);
        }


        #region page navigation overrides

        protected override void OnCancelCommandExecute()
        {
            //Stop recording tracklog

            //FIXME - what if there are open observations?
            _task.StopRecording();
            _trackLog.Save();
            ((SetupTrackLogPage)PreviousPage).UpdateSource();
            MobileApplication.Current.Transition(PreviousPage);
        }

        protected override void OnOkCommandExecute()
        {
            //Log observation point and open observation attribute page

            //We create the observation at the last GPS point.
            //We do not wait for the next GPS point to interpolate, because
            //1) Observations have a 1 to 1 relationship with GPS points,
            //2) we may never get a next GPS point,
            //3) There is a lag in communication between observers and recorders,
            //   so the actual point of observation has already passed when this operation runs.

            //FIXME - Task.CurrentGpsPoint may be null if this thread caught the main thread between states.
            Debug.Assert(_task.CurrentGpsPoint != null, "Fail! Current GPS Point is null when recording an observation.");
            if (_task.CurrentGpsPoint == null)
                return;

            Observation observation = Observation.CreateWith(_task.CurrentGpsPoint);
            _task.AddObservation(observation);
            MobileApplication.Current.Transition(new EditObservationAttributesPage());
        }

        #endregion


        #region Mouse events

        System.Windows.Point _mouseDownPoint;
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            //get the location of the mouse down event for use by the mouse up event
            _mouseDownPoint = e.GetPosition(this);
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            System.Windows.Point mouseUpPoint = e.GetPosition(this);
            var drawingPoint = new System.Drawing.Point(Convert.ToInt32(mouseUpPoint.X), Convert.ToInt32(mouseUpPoint.Y));
            
            //Check to see if there is an birdgroup at this location.  If so edit it, be done
            Coordinate mapPoint = MobileApplication.Current.Map.ToMap(drawingPoint);
            Observation observation = Observation.FromPoint(mapPoint);
            if (observation != null)
            {
                _task.AddObservation(observation);
                MobileApplication.Current.Transition(new EditObservationAttributesPage());
                e.Handled = true;
                return;
            }

            //Check to see if the mouseup location is substantially different than the mouse down location, if so then pan and be done
            int dx = Convert.ToInt32(mouseUpPoint.X - _mouseDownPoint.X);
            int dy = Convert.ToInt32(mouseUpPoint.Y - _mouseDownPoint.Y);

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
